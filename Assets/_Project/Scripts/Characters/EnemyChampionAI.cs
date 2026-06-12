using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

        private const float MoveSpeed   = 5.5f;
        private const float Gravity     = -20f;
        private const float TurnSpeed   = 10f;
        // タワー等がウェイポイント上に立つことがあるため、コライダー越しでも「到達」と
        // みなせる半径にする(タワー半径1.2 + 自身0.5 + 余裕)
        private const float WaypointReach = 3.0f;

        // 障害物に引っかかって進めない場合のスタック検知(2秒間ほぼ動かなければ次WPへ)
        private const float StuckSeconds      = 2f;
        private const float StuckMoveEpsilon  = 0.3f;

        private const float SenseRadius   = 16f;
        private const float SenseInterval = 0.3f;

        private const float AttackCdSeconds  = 1.6f;
        private const float AttackRange      = 11f;
        private const float AttackDamage     = 16f;
        private const float ProjectileSpeed  = 30f;

        private const float RespawnDelay = 8f;
        private static readonly Vector3 RespawnPos = new Vector3(52f, 1.1f, -6f);

        private CharacterController _controller;
        private HealthComponent _health;

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

        private bool _isDead;

        private void Awake()
        {
            _controller     = GetComponent<CharacterController>();
            _health         = GetComponent<HealthComponent>();
            _attackCooldown = new AttackCooldown(AttackCdSeconds);
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

            if (decision.HasAttackTarget)
            {
                var target = decision.TargetIsAttackerChampion && _attackerChampion != null
                    ? _attackerChampion
                    : _nearestEnemy;
                FaceAndAttack(target);
            }

            ApplyMovement(decision.Move);
        }

        // 0.3 秒ごとに OverlapSphere で Blue チームの敵を収集し、
        // 知覚スナップショットを組み立てる。判断は持たない。
        private void Sense()
        {
            _nearestEnemy = null;
            _attackerChampion = null;

            float nearestDist = float.MaxValue;
            var nearestKind = LaneThreatKind.None;
            float towerDist = float.MaxValue;
            float attackerDist = float.MaxValue;
            bool allyMinionNearby = false;

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

                if (tag.Team == TeamId.Red)
                {
                    // 味方ミニオン（同チーム）の近接判定。アタックゾーン進入可否に使う
                    if (col.GetComponent<Enigma.Minion.MinionAI>() != null && dist <= AttackRange)
                        allyMinionNearby = true;
                    continue;
                }

                if (tag.Team != TeamId.Blue) continue;

                var hc = col.GetComponent<HealthComponent>();
                if (hc == null || hc.Model.IsDead) continue;

                var kind = ClassifyTarget(col);

                if (kind == LaneThreatKind.Tower)
                {
                    if (dist < towerDist) towerDist = dist;
                    continue;
                }

                // 最寄りの攻撃対象（チャンピオン/ミニオン）
                if (dist < nearestDist)
                {
                    nearestDist   = dist;
                    nearestKind   = kind;
                    _nearestEnemy = hc;
                }

                // 自分を攻撃してきた敵チャンピオン
                if (kind == LaneThreatKind.Champion && lastAttacker != null
                    && col.gameObject == lastAttacker)
                {
                    _attackerChampion = hc;
                    attackerDist = dist;
                }
            }

            _perception = new LaneBotPerception(
                _health.Model.MaxHp > 0f ? _health.Model.CurrentHp / _health.Model.MaxHp : 0f,
                nearestKind == LaneThreatKind.None ? float.MaxValue : nearestDist,
                nearestKind,
                _attackerChampion != null,
                attackerDist,
                towerDist,
                allyMinionNearby);
        }

        private static LaneThreatKind ClassifyTarget(Collider col)
        {
            if (col.GetComponent<Enigma.Minion.MinionAI>() != null) return LaneThreatKind.Minion;

            // タワー/オブジェクティブは Damageable + 非 CharacterController の静的体として扱う。
            // プレイヤー/AI チャンピオンは CharacterController を持つ。
            if (col.GetComponent<CharacterController>() != null || col.CompareTag("Player"))
                return LaneThreatKind.Champion;

            // タワー判定: TowerAttack コンポーネントの有無で識別する
            if (col.GetComponentInParent<Enigma.Objective.TowerAttack>() != null)
                return LaneThreatKind.Tower;

            return LaneThreatKind.Champion;
        }

        private void ApplyMovement(LaneMove move)
        {
            Vector3 horizontal = Vector3.zero;

            if (move == LaneMove.Forward)
                horizontal = StepAlongPath(forward: true);
            else if (move == LaneMove.Backward)
                horizontal = StepAlongPath(forward: false);

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

            var motion = horizontal * MoveSpeed;
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
            if (forward && _waypointIndex < (_waypoints?.Length ?? 1) - 1) _waypointIndex++;
            else if (!forward && _waypointIndex > 0) _waypointIndex--;
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
                return Vector3.zero;
            }

            return flat.normalized;
        }

        private void FaceAndAttack(HealthComponent target)
        {
            if (target == null) return;

            var to = target.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(to);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, look, TurnSpeed * Time.deltaTime);
            }

            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist > AttackRange) return;
            if (!_attackCooldown.TryConsume(Time.time)) return;
            if (_projectilePrefab == null || _muzzle == null) return;

            // PlayerAttackMotor は使わず即時発射
            var dir = (target.transform.position - _muzzle.position).normalized;
            // ビーム見た目を進行方向へ向けるため LookRotation を与える
            var proj = Instantiate(_projectilePrefab, _muzzle.position, Quaternion.LookRotation(dir));
            proj.Init(dir, ProjectileSpeed, AttackDamage, gameObject);
            _clipSwitcher?.PlayAttack(0.45f);
        }

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
            transform.position = RespawnPos;
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
