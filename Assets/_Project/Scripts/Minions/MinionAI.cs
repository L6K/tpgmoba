using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enigma.Combat;
using Enigma.Character;

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
                var rend = GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = teamMaterial;
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

            float dist = Vector3.Distance(transform.position, _currentTarget.transform.position);

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
            // y は固定してレーン表面を維持
            var flatTarget = new Vector3(target.x, _fixedY, target.z);
            var pos        = transform.position;

            transform.position = Vector3.MoveTowards(pos, flatTarget, _moveSpeed * Time.deltaTime);

            var dir = flatTarget - pos;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);
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
            // コライダーを無効にして物理干渉を止める
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            StartCoroutine(DestroyAfterDelay(2f));
        }

        private IEnumerator DestroyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Destroy(gameObject);
        }
    }
}
