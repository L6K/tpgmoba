using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enigma.Combat;
using Enigma.Character;
using Enigma.Core;
using Enigma.GameModes;

namespace Enigma.Minion
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class MinionAI : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed    = 3.5f;
        [SerializeField] private float _aggroRange   = 8f;
        [SerializeField] private float _attackRange  = 1.8f;
        [SerializeField] private float _attackDamage = 5f;
        [SerializeField] private float _attackInterval = 1.2f;

        // HPバー FillWrapper Transform（ビルダーが結線）
        [SerializeField] private Transform _barFill;

        // Blue チーム（味方）のときに Fill に適用するマテリアル（ビルダーが BarGreen を結線）
        [SerializeField] private Material _allyBarMat;

        private HealthComponent   _health;
        private TeamTag           _teamTag;
        private AttackCooldown    _attackCooldown;
        private CharacterController _controller;

        private IReadOnlyList<Vector3> _waypoints;
        private int                    _waypointIndex;

        private HealthComponent _currentTarget;
        private float           _scanTimer;
        private const float     ScanInterval = 0.5f;

        // y 座標を固定するために初期 y を記録
        private float _fixedY;

        private void Awake()
        {
            _health         = GetComponent<HealthComponent>();
            _teamTag        = GetComponent<TeamTag>();
            _attackCooldown = new AttackCooldown(_attackInterval);
            _controller     = GetComponent<CharacterController>();
        }

        private void Start()
        {
            _fixedY = transform.position.y;
            _health.Model.Changed += OnHealthChanged;
            _health.Model.Died    += OnDied;

            // 満タン表示にリセット（Initialize が呼ばれた後でも上書きして統一）
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

        /// <summary>スポーナーからチームとウェイポイントを受け取り初期化する。</summary>
        public void Initialize(TeamId team, IReadOnlyList<Vector3> waypoints, Material teamMaterial)
        {
            _teamTag.SetTeam(team);
            _waypoints     = waypoints;
            _waypointIndex = 0;

            if (teamMaterial != null)
            {
                // 見た目は子の "Visual"(FBX) 配下の複数 Renderer。HP バー等の Renderer を
                // 巻き込まないよう、Visual サブツリーが存在すればそこに限定して適用する。
                var visual = transform.Find("Visual");
                var rends  = visual != null
                    ? visual.GetComponentsInChildren<Renderer>(true)
                    : GetComponentsInChildren<Renderer>(true);

                foreach (var rend in rends)
                {
                    if (rend == null) continue;
                    rend.sharedMaterial = teamMaterial;
                }
            }

            // Blue チーム（味方）のときは Fill Renderer を BarGreen に差し替える
            if (team == TeamId.Blue && _allyBarMat != null && _barFill != null)
            {
                var fillRend = _barFill.GetComponentInChildren<Renderer>();
                if (fillRend != null) fillRend.sharedMaterial = _allyBarMat;
            }

            // 満タン表示にリセット
            if (_barFill != null)
            {
                var s = _barFill.localScale;
                s.x = 1f;
                _barFill.localScale = s;
            }
        }

        /// <summary>
        /// 試合経過に応じた強化倍率を HP と攻撃力へ適用する（スポーナーが呼ぶ）。
        /// HP は現 MaxHp との差分を AddMaxHp で寄せて全回復させ、攻撃力は直接乗算する。
        /// </summary>
        public void ApplyTimeScaling(float multiplier)
        {
            if (multiplier <= 1f) return;

            // _health は Awake で結線済み。未結線でも遅延初期化に任せて安全に取得する。
            var model = _health != null ? _health.Model : GetComponent<HealthComponent>().Model;
            float delta = model.MaxHp * (multiplier - 1f);
            if (delta > 0f) model.AddMaxHp(delta);

            _attackDamage *= multiplier;
        }

        private void Update()
        {
            if (_health.Model.IsDead) return;

            // 索敵スキャン（負荷軽減のため一定間隔）
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = ScanInterval;
                ScanForTarget();
            }

            // 死亡済みターゲットを破棄
            if (_currentTarget != null && _currentTarget.Model.IsDead)
                _currentTarget = null;

            if (_currentTarget != null)
            {
                ChaseAndAttack();
            }
            else
            {
                FollowWaypoint();
            }
        }

        private void ScanForTarget()
        {
            var cols = Physics.OverlapSphere(transform.position, _aggroRange);
            var candidates = new List<TargetCandidate>(cols.Length);

            foreach (var col in cols)
            {
                var hc  = col.GetComponent<HealthComponent>();
                var tag = col.GetComponent<TeamTag>();

                // HealthComponent と TeamTag を両方持ち、死亡していない対象のみ候補に追加
                if (hc == null || tag == null || hc.Model.IsDead) continue;
                if (col.gameObject == gameObject) continue;

                candidates.Add(new TargetCandidate(col.transform.position, tag.Team));
            }

            int idx = MinionLogic.ChooseTarget(
                transform.position, _teamTag.Team, candidates, _aggroRange);

            if (idx >= 0)
            {
                // candidates の index と cols の index を対応させるため再検索
                int validCount = 0;
                foreach (var col in cols)
                {
                    var hc  = col.GetComponent<HealthComponent>();
                    var tag = col.GetComponent<TeamTag>();
                    if (hc == null || tag == null || hc.Model.IsDead) continue;
                    if (col.gameObject == gameObject) continue;

                    if (validCount == idx)
                    {
                        _currentTarget = hc;
                        break;
                    }
                    validCount++;
                }
            }
            else
            {
                _currentTarget = null;
            }
        }

        private void ChaseAndAttack()
        {
            if (_currentTarget == null) return;

            // 距離は対象コライダーの最近接点で測る。タイタン(カプセル半径2.6)のような太い構造物は
            // 中心間距離だと _attackRange(1.8) 以内に物理的に入れず、永遠に攻撃できないため。
            var targetCol = _currentTarget.GetComponent<Collider>();
            Vector3 closest = targetCol != null
                ? targetCol.ClosestPoint(transform.position)
                : _currentTarget.transform.position;
            float dist = Vector3.Distance(transform.position, closest);

            if (dist > _attackRange)
            {
                MoveToward(_currentTarget.transform.position);
            }
            else
            {
                // 射程内：攻撃間隔ごとにダメージ
                if (_attackCooldown.TryConsume(Time.time))
                {
                    float finalDamage = DamageUtility.ApplyTeamBuff(_attackDamage, gameObject);
                    finalDamage *= 1f + (GameServices.ObjectiveBuffs != null
                        ? GameServices.ObjectiveBuffs.GetMagnitude(_teamTag.Team, ObjectiveBuffType.MinionPower, Time.time) : 0f);
                    _currentTarget.TakeDamage(finalDamage, gameObject);
                }
            }
        }

        private void FollowWaypoint()
        {
            if (_waypoints == null || _waypointIndex >= _waypoints.Count) return;

            var target = _waypoints[_waypointIndex];
            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(target.x,             0f, target.z));

            if (dist <= 1.5f)
            {
                _waypointIndex++;
                return;
            }

            MoveToward(target);
        }

        private void MoveToward(Vector3 target)
        {
            var pos = transform.position;
            // 水平方向のみで向き・移動方向を決める（y は接地に委ねる/固定する）
            var flatDir = new Vector3(target.x - pos.x, 0f, target.z - pos.z);

            if (_controller != null)
            {
                // CharacterController で実体衝突しながら移動（タワー・壁をすり抜けない）。
                // Vector3.down は軽い接地押し付けでスロープ/段差に追従させる。
                float dt   = Time.deltaTime;
                var   step = flatDir.sqrMagnitude > 0.0001f
                    ? flatDir.normalized * _moveSpeed * dt
                    : Vector3.zero;
                _controller.Move(step + Vector3.down * 2f * dt);
            }
            else
            {
                // フォールバック: 従来の transform 直接移動（y を固定してレーン表面を維持）
                var flatTarget = new Vector3(target.x, _fixedY, target.z);
                transform.position = Vector3.MoveTowards(pos, flatTarget, _moveSpeed * Time.deltaTime);
            }

            if (flatDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(flatDir);
        }

        private void OnHealthChanged(float current, float max)
        {
            if (_barFill == null || max <= 0f) return;
            var scale = _barFill.localScale;
            scale.x = current / max;
            _barFill.localScale = scale;
        }

        private void OnDied()
        {
            // コライダーを無効にして物理干渉を止める（移動停止は Update の IsDead 早期 return）
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            // CharacterController も無効化して死体の押し合いを止める
            if (_controller != null) _controller.enabled = false;

            // 見た目と破棄は DeathPresenter に委譲。無ければ従来どおり自前で破棄する
            if (GetComponent<DeathPresenter>() == null)
                StartCoroutine(DestroyAfterDelay(2f));
        }

        private IEnumerator DestroyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Destroy(gameObject);
        }
    }
}
