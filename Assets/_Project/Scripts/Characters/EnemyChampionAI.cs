using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enigma.Ability;
using Enigma.Audio;
using Enigma.Combat;

namespace Enigma.Character
{
    // 敵レーナー AI チャンピオン（Humble Object）。
    // 判断は LaneBotLogic（plain C#）に委譲し、本クラスは知覚収集・移動・攻撃・
    // リスポーンといった Unity 依存の入出力のみを担う。
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(HealthComponent))]
    [RequireComponent(typeof(TeamTag))]
    public sealed class EnemyChampionAI : MonoBehaviour
    {
        [SerializeField] private Vector3[] _waypoints;
        [SerializeField] private Projectile _projectilePrefab;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Transform _barFill;
        [SerializeField] private LocomotionClipSwitcher _clipSwitcher;

        // スキル発動用。ApplyCharacter で data.Skills を保持する（0=Q方向,1=E地点AoE,2=R対象指定）。
        // null/空ならスキルを撃たず従来どおり AA のみ（後方互換）。
        private SkillDefinition[] _skills;

        // 地点AoE スキルのテレグラフ。プレイヤーの SkillCaster._telegraphPrefab と同一アセットをビルダーが結線する。
        [SerializeField] private TelegraphCircle _telegraphPrefab;

        // ジャングラーのみ true（ビルダーが RedBot_Jungle に設定）。敵不在時に中立キャンプを狩る。
        [SerializeField] private bool _farmsNeutralCamps;

        // リスポーン地点（=自ベースの泉付近）。チームごとに異なるためビルダーが結線する。
        [SerializeField] private Vector3 _respawnPos = new Vector3(52f, 1.1f, -6f);

        // ステータスは ApplyCharacter でピックキャラ値に上書きされうるため SerializeField 化。
        // 既定値は旧 const 値を踏襲する。
        [SerializeField] private float _moveSpeed       = 5.5f;
        [SerializeField] private float _attackCdSeconds = 1.6f;
        [SerializeField] private float _attackRange     = 11f;
        [SerializeField] private float _attackDamage    = 16f;

        private const float Gravity     = -20f;
        private const float TurnSpeed   = 10f;
        // タワー等がウェイポイント上に立つことがあるため、コライダー越しでも「到達」と
        // みなせる半径にする(タワー半径1.2 + 自身0.5 + 余裕)
        private const float WaypointReach = 3.0f;

        // 障害物に引っかかって進めない場合のスタック検知(2秒間ほぼ動かなければ次WPへ)
        private const float StuckSeconds      = 2f;
        private const float StuckMoveEpsilon  = 0.3f;

        // 前方障害物の SphereCast 設定（胸高から水平に飛ばして迂回方向を求める）
        private const float ObstacleProbeRadius   = 0.5f;
        private const float ObstacleProbeDistance = 2.5f;

        private const float SenseRadius   = 16f;
        private const float SenseInterval = 0.3f;

        private const float ProjectileSpeed  = 30f;

        private const float RespawnDelay = 8f;

        // 中立キャンプ狩りの探索半径と射程（その場で殴れる近さ）。
        // キャンプ空き地(木なし半径4.5)+余裕。これより遠くで採用すると経路外の
        // 直線接近になり森でスタックする(キャンプはルートのウェイポイントなので、
        // 接近はルート移動に任せ、空き地に入ってからロックする)
        private const float NeutralFarmRadius = 6f;

        // スキルスロット数（Q/E/R）。
        private const int SkillSlotCount = 3;

        private CharacterController _controller;
        private HealthComponent _health;
        private TeamTag _teamTag;
        private StatusEffectController _statusEffects;

        private LaneBotState _state = LaneBotState.Push;
        private int _waypointIndex;
        private float _verticalVelocity;
        private float _stuckTimer;
        private Vector3 _stuckAnchor;

        private AttackCooldown _attackCooldown;
        private float _senseTimer;

        // 直近の知覚収集結果（Update でロジックに渡す）
        private LaneBotPerception _perception;
        private HealthComponent _nearestEnemy;
        private HealthComponent _attackerChampion;
        // 最寄り敵がチャンピオン種別か（スキル使用可否の判定に使う）。
        private bool _nearestEnemyIsChampion;

        // 最寄り敵タワー/タイタンの HealthComponent（終盤にウェーブと一緒に折るため保持）。
        private HealthComponent _nearestTowerHc;
        // 味方ミニオンが射程内に居るか（タワーダイブ＝被弾の盾になる味方が居るかの判定）。
        private bool _allyMinionNearby;

        // スロット毎の次回使用可能時刻（Time.time 基準）。初回は試合開始からずらして撃つ。
        private readonly float[] _skillReadyAt = new float[SkillSlotCount];

        // 中立キャンプ狩り対象（_farmsNeutralCamps のときのみ採用）。
        private HealthComponent _neutralTarget;

        // 中立狩りの無進展検知。射線が木に塞がれた等で HP も距離も進展しない場合、
        // 一定時間でそのキャンプを見送って巡回へ復帰する（デッドロック防止）
        private const float NeutralAttackRange      = 5.5f; // 空き地(木なし半径4.5)内から撃つ
        private const float NeutralProgressTimeout  = 5f;
        private const float NeutralBlacklistSeconds = 15f;
        private HealthComponent _neutralTracked;
        private float _neutralLastHp;
        private float _neutralBestDist;
        private float _neutralDeadline;
        private HealthComponent _neutralBlacklisted;
        private float _neutralBlacklistUntil;

        private bool _isDead;

        private void Awake()
        {
            _controller     = GetComponent<CharacterController>();
            _health         = GetComponent<HealthComponent>();
            _teamTag        = GetComponent<TeamTag>();
            _attackCooldown = new AttackCooldown(_attackCdSeconds);
            _statusEffects  = StatusEffectController.GetOrAdd(gameObject);
        }

        /// <summary>
        /// ピックキャラの値で移動・攻撃ステータスと HP を上書きする。
        /// HP は MatchBootstrap と同様、現 MaxHp との差分を AddMaxHp で寄せて全回復する。
        /// 攻撃間隔が変わるため AttackCooldown は作り直す。
        /// </summary>
        public void ApplyCharacter(CharacterData data)
        {
            if (data == null) return;

            if (data.MoveSpeed > 0f)       _moveSpeed       = data.MoveSpeed;
            if (data.AttackDamage > 0f)    _attackDamage    = data.AttackDamage;
            if (data.AttackRange > 0f)     _attackRange     = data.AttackRange;
            if (data.AttackCooldown > 0f)  _attackCdSeconds = data.AttackCooldown;

            _attackCooldown = new AttackCooldown(_attackCdSeconds);

            // スキルセットを保持。スロット毎に初回使用時刻をずらし、開幕に全弾同時発射しないようにする。
            _skills = data.Skills;
            for (int slot = 0; slot < SkillSlotCount; slot++)
                _skillReadyAt[slot] = Time.time + 2f + slot * 1.5f;

            // Awake が走っていれば即時生成済み。Start 前に呼ばれても Awake で再生成されるため安全。
            if (_health != null && data.BaseHp > 0f)
            {
                float delta = data.BaseHp - _health.Model.MaxHp;
                if (Mathf.Abs(delta) > 0.001f)
                    _health.Model.AddMaxHp(delta);
            }
        }

        /// <summary>モデルスワップ後、新モデルの LocomotionClipSwitcher へ再結線する。</summary>
        public void SetClipSwitcher(LocomotionClipSwitcher switcher)
        {
            _clipSwitcher = switcher;
        }

        private void Start()
        {
            _health.Model.Changed += OnHealthChanged;
            _health.Model.Died    += OnDied;

            if (_barFill != null)
            {
                var s = _barFill.localScale;
                s.x = 1f;
                _barFill.localScale = s;
            }
        }

        private void OnDestroy()
        {
            if (_health?.Model == null) return;
            _health.Model.Changed -= OnHealthChanged;
            _health.Model.Died    -= OnDied;
        }

        private void Update()
        {
            if (_isDead) return;

            _senseTimer -= Time.deltaTime;
            if (_senseTimer <= 0f)
            {
                _senseTimer = SenseInterval;
                Sense();
            }

            var decision = LaneBotLogic.Decide(_state, _perception);
            _state = decision.State;

            // 中立狩り前処理: ジャングラーが敵不在で中立をロックしている場合、
            // LaneBotLogic の決定を上書きして「その場で停止して AA」する（スキルは使わない）。
            if (_neutralTarget != null)
            {
                if (_neutralTarget.Model.IsDead)
                {
                    // 倒したら通常巡回へ復帰。狩りの間に経路から逸れている可能性が
                    // あるため、古いインデックスを信用せず最寄りへ振り直す
                    _neutralTarget  = null;
                    _neutralTracked = null;
                    _waypointIndex  = NearestWaypointIndex();
                }
                else
                {
                    UpdateNeutralFarm();
                    return;
                }
            }

            if (decision.HasAttackTarget)
            {
                var target = decision.TargetIsAttackerChampion && _attackerChampion != null
                    ? _attackerChampion
                    : _nearestEnemy;
                // スキルは最寄り敵がチャンピオン種別のときのみ使う（攻撃者ターゲットも同種別なら可）
                bool targetIsChampion = decision.TargetIsAttackerChampion || _nearestEnemyIsChampion;
                FaceAndAttack(target, allowSkills: targetIsChampion);
            }
            else if (CanSiegeTower())
            {
                // 交戦対象が居らず、敵タワー/タイタンが AA 射程内で味方ミニオンが盾に居るとき、
                // ウェーブと一緒にタワーを折る。移動は decision.Move のまま継続させ、
                // スキルは無駄撃ちを避けて使わない(タワー相手は AA で十分)。
                FaceAndAttack(_nearestTowerHc, allowSkills: false);
            }

            ApplyMovement(decision.Move);
        }

        // 0.3 秒ごとに OverlapSphere で敵チームのユニットを収集し、
        // 知覚スナップショットを組み立てる。判断は持たない。
        // チームは TeamTag.Team を基準に判定する（同チーム=味方、それ以外=攻撃対象）。
        private void Sense()
        {
            _nearestEnemy = null;
            _attackerChampion = null;
            _nearestEnemyIsChampion = false;
            _neutralTarget = null;
            _nearestTowerHc = null;
            _allyMinionNearby = false;

            float nearestDist = float.MaxValue;
            var nearestKind = LaneThreatKind.None;
            float towerDist = float.MaxValue;
            float attackerDist = float.MaxValue;
            bool anyEnemyChampion = false; // 敵チャンピオンを知覚したか（中立狩り抑制用）

            float neutralDist = float.MaxValue; // 最寄り生存中立モンスターの距離

            TeamId myTeam = _teamTag != null ? _teamTag.Team : TeamId.Red;

            // 直近の攻撃者（弾オーナー）の GO を取得
            var lastAttacker = _health.LastAttacker;

            var cols = Physics.OverlapSphere(transform.position, SenseRadius);
            foreach (var col in cols)
            {
                if (col.gameObject == gameObject) continue;

                var tag = col.GetComponent<TeamTag>();
                if (tag == null) continue;

                var pos = col.transform.position;
                float dist = Vector3.Distance(transform.position, pos);

                // 同チーム（味方）: ミニオン近接のみ拾い、攻撃対象にはしない
                if (tag.Team == myTeam)
                {
                    if (col.GetComponent<Enigma.Minion.MinionAI>() != null && dist <= _attackRange)
                        _allyMinionNearby = true;
                    continue;
                }

                // 中立は通常の攻撃対象にしない（敵チームのみ交戦）。
                // ただしジャングラーはキャンプ狩り用に最寄り中立モンスターを別枠で拾う。
                if (tag.Team == TeamId.Neutral)
                {
                    if (_farmsNeutralCamps && dist <= NeutralFarmRadius
                        && col.GetComponent<Enigma.Minion.JungleMonster>() != null)
                    {
                        var nhc = col.GetComponent<HealthComponent>();
                        // 無進展で見送ったキャンプは一定時間採用しない
                        bool blacklisted = nhc == _neutralBlacklisted
                                           && Time.time < _neutralBlacklistUntil;
                        if (nhc != null && !nhc.Model.IsDead && !blacklisted && dist < neutralDist)
                        {
                            neutralDist    = dist;
                            _neutralTarget = nhc;
                        }
                    }
                    continue;
                }

                var hc = col.GetComponent<HealthComponent>();
                if (hc == null || hc.Model.IsDead) continue;

                var kind = ClassifyTarget(col);

                if (kind == LaneThreatKind.Tower)
                {
                    // 終盤にウェーブと一緒に折るため、最寄り敵タワー/タイタンの HC も保持する。
                    if (dist < towerDist)
                    {
                        towerDist = dist;
                        _nearestTowerHc = hc;
                    }
                    continue;
                }

                // 敵チャンピオンが至近(10m)のときのみ中立狩りを中断する。
                // ミニオンウェーブや、レーンを素通りするだけの敵レーナー(16m先)で
                // 狩りを捨てると、キャンプ横がレーンの本マップでは永遠に狩れない
                if (kind == LaneThreatKind.Champion && dist < 10f) anyEnemyChampion = true;

                // 最寄りの攻撃対象（チャンピオン/ミニオン）
                if (dist < nearestDist)
                {
                    nearestDist   = dist;
                    nearestKind   = kind;
                    _nearestEnemy = hc;
                    _nearestEnemyIsChampion = kind == LaneThreatKind.Champion;
                }

                // 自分を攻撃してきた敵チャンピオン
                if (kind == LaneThreatKind.Champion && lastAttacker != null
                    && col.gameObject == lastAttacker)
                {
                    _attackerChampion = hc;
                    attackerDist = dist;
                }
            }

            // 敵チャンピオンが視界にいる間は中立狩りをしない（対面優先）
            if (anyEnemyChampion) _neutralTarget = null;

            _perception = new LaneBotPerception(
                _health.Model.MaxHp > 0f ? _health.Model.CurrentHp / _health.Model.MaxHp : 0f,
                nearestKind == LaneThreatKind.None ? float.MaxValue : nearestDist,
                nearestKind,
                _attackerChampion != null,
                attackerDist,
                towerDist,
                _allyMinionNearby);
        }

        private static LaneThreatKind ClassifyTarget(Collider col)
        {
            if (col.GetComponent<Enigma.Minion.MinionAI>() != null) return LaneThreatKind.Minion;

            // タワー/オブジェクティブは Damageable + 非 CharacterController の静的体として扱う。
            // プレイヤー/AI チャンピオンは CharacterController を持つ。
            if (col.GetComponent<CharacterController>() != null || col.CompareTag("Player"))
                return LaneThreatKind.Champion;

            // 静的オブジェクティブ(タワー/タイタン)は CharacterController を持たない。
            // タワーは TowerAttack を持つが、タイタンは持たないため、TowerAttack の有無では
            // なく「CharacterController 非保持の静的体」を Tower 種別として一括で扱い、
            // タイタンも攻撃対象に含める(本陣タワー/タイタン破壊=決着のため)。
            return LaneThreatKind.Tower;
        }

        // 中立狩りの1フレーム分: 空き地内(NeutralAttackRange)まで寄ってから攻撃する。
        // HP も距離も一定時間進展しなければ(木に射線を塞がれた等)、見送って巡回へ復帰。
        private void UpdateNeutralFarm()
        {
            float dist = Vector3.Distance(transform.position, _neutralTarget.transform.position);

            // 対象が切り替わったら進展トラッキングを初期化
            if (_neutralTracked != _neutralTarget)
            {
                _neutralTracked  = _neutralTarget;
                _neutralLastHp   = _neutralTarget.Model.CurrentHp;
                _neutralBestDist = dist;
                _neutralDeadline = Time.time + NeutralProgressTimeout;
            }

            // 進展判定: HP 減少 or 接近できていれば締切を延長
            float hp = _neutralTarget.Model.CurrentHp;
            if (hp < _neutralLastHp - 0.5f || dist < _neutralBestDist - 0.5f)
            {
                _neutralLastHp   = Mathf.Min(_neutralLastHp, hp);
                _neutralBestDist = Mathf.Min(_neutralBestDist, dist);
                _neutralDeadline = Time.time + NeutralProgressTimeout;
            }
            else if (Time.time >= _neutralDeadline)
            {
                _neutralBlacklisted   = _neutralTarget;
                _neutralBlacklistUntil = Time.time + NeutralBlacklistSeconds;
                _neutralTarget  = null;
                _neutralTracked = null;
                _waypointIndex  = NearestWaypointIndex(); // 逸脱からの復帰
                return;
            }

            // 自分の AA 射程内まで寄る(近接キャラは 5.5m では射程外のため)。
            // 上限 5.5m は空き地(木なし)内から撃つための制約
            float engageRange = Mathf.Min(NeutralAttackRange, _attackRange * 0.9f);
            if (dist > engageRange)
            {
                MoveDirectlyToward(_neutralTarget.transform.position);
            }
            else
            {
                FaceAndAttack(_neutralTarget, allowSkills: false);
                ApplyMovement(LaneMove.Stop);
            }
        }

        // 現在位置に最も近いウェイポイントのインデックスを返す（経路逸脱からの復帰用）。
        private int NearestWaypointIndex()
        {
            if (_waypoints == null || _waypoints.Length == 0) return 0;
            int best = 0;
            float bestSq = float.MaxValue;
            for (int i = 0; i < _waypoints.Length; i++)
            {
                var d = _waypoints[i] - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude < bestSq)
                {
                    bestSq = d.sqrMagnitude;
                    best = i;
                }
            }
            return best;
        }

        // 経路を使わず指定地点へ直進する（中立狩りの接近用）。障害物スライドは共用だが、
        // 接近対象自身は回避しない（回り込みループで射程内へ入れなくなる）。
        private void MoveDirectlyToward(Vector3 worldPos)
        {
            var dir = worldPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
            dir = AvoidObstacles(dir, _neutralTarget != null ? _neutralTarget.transform : null);

            if (dir.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, look, TurnSpeed * Time.deltaTime);
            }

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -1f;
            _verticalVelocity += Gravity * Time.deltaTime;

            var motion = dir * _moveSpeed;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }

        private void ApplyMovement(LaneMove move)
        {
            Vector3 horizontal = Vector3.zero;

            if (move == LaneMove.Forward)
                horizontal = StepAlongPath(forward: true);
            else if (move == LaneMove.Backward)
                horizontal = StepAlongPath(forward: false);

            // 前方の障害物（タワー等）を検知して回り込む方向へスライドさせる
            if (horizontal.sqrMagnitude > 0.0001f)
                horizontal = AvoidObstacles(horizontal);

            if (horizontal.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(horizontal);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, look, TurnSpeed * Time.deltaTime);
            }

            // 重力
            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -1f;
            _verticalVelocity += Gravity * Time.deltaTime;

            if (_statusEffects != null && !_statusEffects.CanMove)
                horizontal = Vector3.zero;
            float speed = _moveSpeed * (_statusEffects != null ? _statusEffects.MoveSpeedMultiplier : 1f);
            var motion = horizontal * speed;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);

            UpdateStuckEscape(wantsToMove: horizontal.sqrMagnitude > 0.0001f,
                              forward: move == LaneMove.Forward);
        }

        // 移動意図があるのにほぼ動けない状態が続いたら、障害物(タワー等)に
        // 引っかかったとみなして目標ウェイポイントを1つ進める
        private void UpdateStuckEscape(bool wantsToMove, bool forward)
        {
            if (!wantsToMove)
            {
                _stuckTimer = 0f;
                _stuckAnchor = transform.position;
                return;
            }

            if (Vector3.Distance(transform.position, _stuckAnchor) > StuckMoveEpsilon)
            {
                _stuckTimer = 0f;
                _stuckAnchor = transform.position;
                return;
            }

            _stuckTimer += Time.deltaTime;
            if (_stuckTimer < StuckSeconds) return;

            _stuckTimer = 0f;
            _stuckAnchor = transform.position;

            // 盲目的な index++ はスタックが続くと終端まで暴走し、遠方ウェイポイントへの
            // 直線移動(=森を横切る)で永久さまよいになる。最寄りウェイポイント基準で
            // 1つ先へ再同期すれば、目標は常に自位置の近傍に束縛される
            int near = NearestWaypointIndex();
            int last = (_waypoints?.Length ?? 1) - 1;
            _waypointIndex = forward
                ? Mathf.Min(near + 1, last)
                : Mathf.Max(near - 1, 0);
        }

        // 経路の現在ウェイポイントへ向かう水平方向（正規化）を返す。
        // forward=true は青ベース方向（インデックス増加）、false は赤ベース方向（減少）。
        private Vector3 StepAlongPath(bool forward)
        {
            if (_waypoints == null || _waypoints.Length == 0) return Vector3.zero;

            int target = forward ? _waypointIndex : _waypointIndex - 1;
            target = Mathf.Clamp(target, 0, _waypoints.Length - 1);

            var wp = _waypoints[target];
            var flat = new Vector3(wp.x - transform.position.x, 0f, wp.z - transform.position.z);

            if (flat.magnitude <= WaypointReach)
            {
                // 到達したら進行度を更新（前進/後退で方向が異なる）
                if (forward && _waypointIndex < _waypoints.Length - 1) _waypointIndex++;
                else if (!forward && _waypointIndex > 0) _waypointIndex--;
                // ジャングラーは終端到達でピンポン反転して周回し続ける
                // (レーンボットは終端=敵ベース前で停滞するのが正)
                else if (forward && _farmsNeutralCamps)
                {
                    System.Array.Reverse(_waypoints);
                    _waypointIndex = 0;
                }
                return Vector3.zero;
            }

            return flat.normalized;
        }

        // 望みの水平方向 dir で進む前に前方を SphereCast で確認し、障害物（タワーの
        // 円柱など）にぶつかるならヒット法線に沿って横滑りする方向へ補正する。
        // これにより静止した障害物を滑らかに迂回できる。スタック検知は保険として残す。
        private Vector3 AvoidObstacles(Vector3 dir) => AvoidObstacles(dir, null);

        // ignoreRoot: 回避対象から除外するルート(接近したい攻撃対象自身を「障害物」として
        // 回り込み続けると永遠に射程内へ入れないため)
        private Vector3 AvoidObstacles(Vector3 dir, Transform ignoreRoot)
        {
            // 地面に当たらないよう胸高から水平に飛ばす
            var origin = transform.position + Vector3.up * 0.5f;

            if (!Physics.SphereCast(origin, ObstacleProbeRadius, dir, out var hit,
                                    ObstacleProbeDistance, ~0, QueryTriggerInteraction.Ignore))
                return dir;

            // 自分自身のコライダーは無視（CharacterController を持つ動体は滑って避けてよいので除外しない）
            if (hit.collider.gameObject == gameObject) return dir;
            if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot)) return dir;

            // 法線（水平成分）に沿って dir を投影し、壁沿いに滑る方向を得る
            var normal = hit.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.0001f) return dir;
            normal.Normalize();

            var slide = dir - normal * Vector3.Dot(dir, normal);
            slide.y = 0f;

            // 正面衝突などで slide がほぼゼロになる場合は法線と直交する接線方向へ逃がす
            if (slide.sqrMagnitude < 0.0001f)
                slide = Vector3.Cross(Vector3.up, normal);

            return slide.normalized;
        }

        // 敵タワー/タイタンを攻囲できる状況か: 生存中の敵タワーが AA 射程内にあり、
        // 味方ミニオンが盾として近くに居る(タワー砲のヘイトを肩代わりしてくれる)こと。
        private bool CanSiegeTower()
        {
            if (_nearestTowerHc == null || _nearestTowerHc.Model.IsDead) return false;
            if (!_allyMinionNearby) return false;

            float dist = Vector3.Distance(transform.position, _nearestTowerHc.transform.position);
            return dist <= _attackRange;
        }

        private void FaceAndAttack(HealthComponent target, bool allowSkills)
        {
            if (_statusEffects != null && !_statusEffects.CanAct) return;
            if (target == null) return;

            var to = target.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(to);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, look, TurnSpeed * Time.deltaTime);
            }

            // AA の前にスキル発動を試みる。1体に対して撃てたフレームは AA を撃たない。
            if (allowSkills && TryCastSkill(target)) return;

            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist > _attackRange) return;
            if (!_attackCooldown.TryConsume(Time.time)) return;
            if (_projectilePrefab == null || _muzzle == null) return;

            // PlayerAttackMotor は使わず即時発射
            var dir = (target.transform.position - _muzzle.position).normalized;
            // ビーム見た目を進行方向へ向けるため LookRotation を与える
            var proj = Instantiate(_projectilePrefab, _muzzle.position, Quaternion.LookRotation(dir));
            proj.Init(dir, ProjectileSpeed, _attackDamage, gameObject);
            GameSfx.PlayVariant("aa_fire", 3, _muzzle.position, 0.6f);
            _clipSwitcher?.PlayAttack(0.45f);
        }

        // BotSkillSelector で撃つスロットを選び、選ばれたらキャストして true を返す。
        // ボットはランク無しなのでダメージ倍率 scale は 1 固定。
        private bool TryCastSkill(HealthComponent target)
        {
            if (_skills == null) return false;

            float dist       = Vector3.Distance(transform.position, target.transform.position);
            float hpRatio    = target.Model.MaxHp > 0f ? target.Model.CurrentHp / target.Model.MaxHp : 1f;

            var q = SlotStateOf(0);
            var e = SlotStateOf(1);
            var r = SlotStateOf(2);

            int slot = BotSkillSelector.Select(q, e, r, dist, hpRatio);
            if (slot < 0) return false;

            var def = _skills[slot];
            // 次回使用可能時刻を更新（クールダウンは def.CooldownSeconds）
            _skillReadyAt[slot] = Time.time + def.CooldownSeconds;

            switch (def.Targeting)
            {
                case SkillTargeting.Directional:  CastBotDirectional(slot, def, target);  break;
                case SkillTargeting.GroundAoe:    CastBotGroundAoe(slot, def, target);    break;
                case SkillTargeting.Targeted:     CastBotTargeted(slot, def, target);     break;
                case SkillTargeting.TargetedAlly: CastBotTargetedAlly(def);              break;
            }

            if (def.Targeting != SkillTargeting.TargetedAlly)
                ApplyBotSelfBuffs(def);

            // スキル発射でも攻撃モーションを再生する
            _clipSwitcher?.PlayAttack(0.45f);
            return true;
        }

        // スロットの選択用状態（CD 準備済みか + 射程）を組み立てる。未結線スロットは未準備扱い。
        private BotSkillSelector.SlotState SlotStateOf(int slot)
        {
            var def = (_skills != null && slot < _skills.Length) ? _skills[slot] : null;
            if (def == null) return new BotSkillSelector.SlotState(false, 0f);
            bool ready = Time.time >= _skillReadyAt[slot];
            return new BotSkillSelector.SlotState(ready, def.Range);
        }

        // ── スキルキャスト（SkillCaster の3経路を最小限ミラー） ─────────────

        private void CastBotDirectional(int slot, SkillDefinition def, HealthComponent target)
        {
            if (_projectilePrefab == null || _muzzle == null) return;

            var dir = target.transform.position - _muzzle.position;
            dir.y   = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
            dir.Normalize();

            float lifetime = def.ProjectileSpeed > 0f ? def.Range / def.ProjectileSpeed : 1.5f;

            var proj = Instantiate(_projectilePrefab, _muzzle.position, Quaternion.LookRotation(dir));
            proj.Init(dir, def.ProjectileSpeed, def.Damage, gameObject, lifetime);
            proj.SetStatusEffects(def.StunDuration, def.RootDuration, def.SlowStrength, def.SlowDuration);

            // 発光コア + トレイル + 二段バースト（プレイヤー側と共通化）
            var color = SkillSlotColor(slot);
            SkillVfx.FireDirectionalVisuals(proj.gameObject, _muzzle.position, dir, color);
            GameSfx.Play("skill_q_fire", _muzzle.position);
        }

        private void CastBotGroundAoe(int slot, SkillDefinition def, HealthComponent target)
        {
            if (_telegraphPrefab == null) return;

            // ターゲット足元に設置（自分の y に合わせる）
            var pos = target.transform.position;
            pos.y   = transform.position.y;

            var telegraph = Instantiate(_telegraphPrefab, pos, Quaternion.identity);
            telegraph.Init(def.Radius, 0.8f, def.Damage, gameObject);
            telegraph.SetStatusEffects(def.StunDuration, def.RootDuration, def.SlowStrength, def.SlowDuration);

            var color = SkillSlotColor(slot);
            SkillVfx.SpawnBurst(_muzzle != null ? _muzzle.position : transform.position, color, 0.3f, 1.2f, 0.25f);
            SkillVfx.SpawnBurst(pos, color, 1f, 4f, 0.4f);
        }

        private void CastBotTargeted(int slot, SkillDefinition def, HealthComponent target)
        {
            if (target == null) return;

            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist > def.Range) return;

            // 味方は対象指定スキルでダメージを受けない
            TeamId myTeam    = _teamTag != null ? _teamTag.Team : TeamId.Neutral;
            var    otherTag  = target.GetComponentInParent<TeamTag>();
            TeamId otherTeam = otherTag != null ? otherTag.Team : TeamId.Neutral;
            if (!TeamRules.CanDamage(myTeam, otherTeam)) return;

            float finalDamage = DamageUtility.ApplyTeamBuff(def.Damage, gameObject);
            target.TakeDamage(finalDamage, gameObject);

            var sc = StatusEffectController.GetOrAdd(target.gameObject);
            if (sc != null)
            {
                if (def.StunDuration > 0f) sc.ApplyStun(def.StunDuration);
                if (def.RootDuration > 0f) sc.ApplyRoot(def.RootDuration);
                if (def.SlowStrength > 0f && def.SlowDuration > 0f) sc.ApplySlow(def.SlowStrength, def.SlowDuration);
            }

            // 胸元→対象へビーム一閃 + バースト+小リング（プレイヤー側と共通化）
            var color = SkillSlotColor(slot);
            var from  = transform.position + Vector3.up * 1.2f;
            var to    = target.transform.position + Vector3.up * 1.2f;
            SkillVfx.SpawnBurst(_muzzle != null ? _muzzle.position : from, color, 0.3f, 1.2f, 0.25f);
            SkillVfx.TargetedHitVisuals(from, to, color);
            GameSfx.Play("skill_r_beam", _muzzle != null ? _muzzle.position : from);
            GameSfx.Play("skill_r_hit", target.transform.position, 0.8f);
        }

        // ボットの TargetedAlly: 簡易に自分を回復+シールド（味方探索は省略）
        private void CastBotTargetedAlly(SkillDefinition def)
        {
            var hc = GetComponent<HealthComponent>();
            if (hc == null) return;
            if (def.HealAmount > 0f) hc.Model.Heal(def.HealAmount);
            if (def.ShieldAmount > 0f && def.ShieldDuration > 0f) hc.Model.AddShield(def.ShieldAmount, def.ShieldDuration);
            var color = new Color(0.36f, 0.84f, 0.42f, 1f);
            SkillVfx.SpawnBurst(transform.position, color, 0.5f, 2.5f, 0.4f);
        }

        // ボット自身へ shield/heal を適用（プレイヤー SkillCaster.ApplySelfBuffs のミラー）
        private void ApplyBotSelfBuffs(SkillDefinition def)
        {
            var hc = GetComponent<HealthComponent>();
            if (hc == null) return;
            if (def.HealAmount > 0f) hc.Model.Heal(def.HealAmount);
            if (def.ShieldAmount > 0f && def.ShieldDuration > 0f) hc.Model.AddShield(def.ShieldAmount, def.ShieldDuration);
        }

        // スロット色（プレイヤー SkillCaster と同系: Q=シアン, E=マゼンタ, R=ゴールド）
        private static Color SkillSlotColor(int slot) => slot switch
        {
            0 => Color.cyan,
            1 => Color.magenta,
            2 => new Color(1f, 0.84f, 0.2f, 1f),
            _ => Color.white,
        };

        private void OnHealthChanged(float current, float max)
        {
            if (_barFill == null || max <= 0f) return;
            var s = _barFill.localScale;
            s.x = current / max;
            _barFill.localScale = s;
        }

        private void OnDied()
        {
            // 見た目（フェード/転倒）は DeathPresenter に委譲。AI 側は当たり/移動だけ止める
            _isDead = true;
            _controller.enabled = false;
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(RespawnDelay);

            // 物理移動前に CharacterController を切ってからテレポートする
            transform.position = _respawnPos;
            _health.Model.Revive();

            _state            = LaneBotState.Push;
            _waypointIndex    = 0;
            _verticalVelocity = 0f;

            _controller.enabled = true;
            // Revive 経由で HealthModel.Revived が発火し DeathPresenter が見た目を復元する
            _isDead = false;
        }
    }
}
