using System.Collections;
using UnityEngine;
using Enigma.Combat;
using Enigma.Character;

namespace Enigma.Minion
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class JungleMonster : MonoBehaviour
    {
        [SerializeField] private float _meleeDamage    = 10f;
        [SerializeField] private float _attackInterval = 1.5f;
        [SerializeField] private float _moveSpeed      = 4f;
        [SerializeField] private float _attackRange    = 2.2f;
        [SerializeField] private float _leashDistance  = 12f;
        [SerializeField] private float _respawnDelay   = 60f;

        // HPバー Fill（ビルダーが Initialize で結線）
        private Transform _barFill;

        private HealthComponent _health;
        private AttackCooldown  _attackCooldown;

        private Vector3          _campCenter;
        private HealthComponent  _target;

        private enum State { Idle, Combat, Return }
        private State _state = State.Idle;

        // 固定 y（地面スライド防止）
        private float _fixedY;

        private void Awake()
        {
            _health         = GetComponent<HealthComponent>();
            _attackCooldown = new AttackCooldown(_attackInterval);
        }

        /// <summary>ビルダーから呼び出す初期化エントリポイント。</summary>
        public void Initialize(Vector3 campCenter, Transform barFill)
        {
            _campCenter = campCenter;
            _barFill    = barFill;
        }

        private void Start()
        {
            _fixedY = transform.position.y;

            // 被弾検知: HP が減ったら攻撃者をターゲット化する
            _health.Model.Changed += OnHealthChanged;
            _health.Model.Died    += OnDied;

            // 満タン表示にリセット
            UpdateBar(1f);
        }

        private void OnDestroy()
        {
            if (_health?.Model == null) return;
            _health.Model.Changed -= OnHealthChanged;
            _health.Model.Died    -= OnDied;
        }

        private void Update()
        {
            if (_health.Model.IsDead) return;

            // 死亡・消滅したターゲットを破棄
            if (_target != null && (_target.Model.IsDead || _target.gameObject == null))
                _target = null;

            switch (_state)
            {
                case State.Idle:
                    // キャンプ中心で待機（Update は何もしない）
                    break;

                case State.Combat:
                    UpdateCombat();
                    break;

                case State.Return:
                    UpdateReturn();
                    break;
            }
        }

        private void UpdateCombat()
        {
            // ターゲット消失 → 帰還
            if (_target == null)
            {
                _state = State.Return;
                return;
            }

            // キャンプ中心からリーシュ距離超過 → 帰還
            float distFromCamp = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(_campCenter.x,        0f, _campCenter.z));
            if (distFromCamp > _leashDistance)
            {
                _target = null;
                _state  = State.Return;
                return;
            }

            float dist = Vector3.Distance(transform.position, _target.transform.position);
            if (dist > _attackRange)
            {
                MoveToward(_target.transform.position);
            }
            else
            {
                // 射程内: チームバフ適用済みダメージを付与
                if (_attackCooldown.TryConsume(Time.time))
                {
                    float dmg = DamageUtility.ApplyTeamBuff(_meleeDamage, gameObject);
                    _target.TakeDamage(dmg, gameObject);
                }
            }
        }

        private void UpdateReturn()
        {
            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(_campCenter.x,        0f, _campCenter.z));

            if (dist <= 0.5f)
            {
                // 帰還完了: スナップして全回復
                transform.position = new Vector3(_campCenter.x, _fixedY, _campCenter.z);
                _health.Model.Revive();
                UpdateBar(1f);
                _state = State.Idle;
                return;
            }

            MoveToward(_campCenter);
        }

        private void MoveToward(Vector3 target)
        {
            var flatTarget = new Vector3(target.x, _fixedY, target.z);
            var pos        = transform.position;
            transform.position = Vector3.MoveTowards(pos, flatTarget, _moveSpeed * Time.deltaTime);

            var dir = flatTarget - pos;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        private void OnHealthChanged(float current, float max)
        {
            UpdateBar(max > 0f ? current / max : 0f);

            // HP が減った = 被弾 → 攻撃者をターゲット化して戦闘へ
            if (_state == State.Idle && current < max)
            {
                var attacker = _health.LastAttacker;
                if (attacker != null)
                {
                    var hc = attacker.GetComponent<HealthComponent>();
                    if (hc != null && !hc.Model.IsDead)
                    {
                        _target = hc;
                        _state  = State.Combat;
                    }
                }
            }
        }

        private void OnDied()
        {
            // 見た目を潰してコライダーを止める
            var s = transform.localScale;
            transform.localScale = new Vector3(s.x, 0.3f, s.z);

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(_respawnDelay);

            // 位置・外見をリセットして復帰
            transform.position   = new Vector3(_campCenter.x, _fixedY, _campCenter.z);
            var s                = transform.localScale;
            transform.localScale = new Vector3(s.x, 1f, s.z);

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = true;

            _health.Model.Revive();
            UpdateBar(1f);

            _target = null;
            _state  = State.Idle;
        }

        private void UpdateBar(float ratio)
        {
            if (_barFill == null) return;
            var scale = _barFill.localScale;
            scale.x             = ratio;
            _barFill.localScale = scale;
        }
    }
}
