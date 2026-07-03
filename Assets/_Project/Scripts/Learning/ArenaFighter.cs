using UnityEngine;
using Enigma.Ability;
using Enigma.Combat;

namespace Enigma.Learning
{
    // ML-Agents 学習用の最小ファイター。移動は外部（Agent）から指示を受け、攻撃は射程内なら自動実行する。
    [RequireComponent(typeof(CharacterController))]
    public sealed class ArenaFighter : MonoBehaviour
    {
        [SerializeField] private ArenaFighter _enemy;
        [SerializeField] private Vector3 _arenaCenter = Vector3.zero;
        [SerializeField] private float _arenaRadius = 20f;
        [SerializeField] private float _moveSpeed = 5.5f;
        [SerializeField] private float _attackRange = 12f;
        [SerializeField] private float _attackDamage = 15f;
        [SerializeField] private float _attackCooldown = 1.5f;
        [SerializeField] private Color _beamColor = Color.cyan;

        private CharacterController _controller;
        private HealthComponent _health;
        private float _nextAttackTime;

        private const float Gravity = -9.81f;

        public HealthComponent Health => _health;
        public ArenaFighter Enemy => _enemy;

        public bool AttackReady => Time.time >= _nextAttackTime;

        // 攻撃CD残りを 0(即撃てる)〜1(撃った直後) に正規化した値。観測用。
        public float CooldownRemaining01
        {
            get
            {
                if (_attackCooldown <= 0f) return 0f;
                float remaining = _nextAttackTime - Time.time;
                return Mathf.Clamp01(remaining / _attackCooldown);
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _health = GetComponent<HealthComponent>();
        }

        private void Update()
        {
            TryAutoAttack();
        }

        public void ApplyMove(Vector2 dir)
        {
            Vector3 horizontal;
            if (dir.sqrMagnitude > 1f)
                dir.Normalize();

            horizontal = new Vector3(dir.x, 0f, dir.y) * _moveSpeed;
            Vector3 motion = horizontal;
            motion.y = Gravity;
            _controller.Move(motion * Time.deltaTime);

            // 学習の観測空間を健全に保つため、物理壁のすり抜けに関わらずアリーナ内へ
            // ハードクランプする(RLでは「絶対に外へ出ない」保証が観測の正規化前提になる)
            Vector3 offset = transform.position - _arenaCenter;
            offset.y = 0f;
            float maxR = _arenaRadius - 0.6f;
            if (offset.sqrMagnitude > maxR * maxR)
            {
                Vector3 clamped = _arenaCenter + offset.normalized * maxR;
                clamped.y = transform.position.y;
                _controller.enabled = false;
                transform.position = clamped;
                _controller.enabled = true;
            }
        }

        public void ResetFighter(Vector3 pos)
        {
            _controller.enabled = false;
            transform.position = pos;
            _controller.enabled = true;

            _health.Model.Revive();
            _nextAttackTime = Time.time;
        }

        private void TryAutoAttack()
        {
            if (!AttackReady) return;
            if (_health.Model.IsDead) return;
            if (_enemy == null || _enemy.Health.Model.IsDead) return;

            Vector3 myPos = transform.position;
            Vector3 enemyPos = _enemy.transform.position;
            float horizontalDist = Vector2.Distance(
                new Vector2(myPos.x, myPos.z), new Vector2(enemyPos.x, enemyPos.z));
            if (horizontalDist > _attackRange) return;

            _nextAttackTime = Time.time + _attackCooldown;

            float damage = DamageUtility.ApplyTeamBuff(_attackDamage, gameObject, _enemy.gameObject);
            _enemy.Health.TakeDamage(damage, gameObject);

            Vector3 from = myPos + Vector3.up * 1.2f;
            Vector3 to = enemyPos + Vector3.up * 1.2f;
            SkillVfx.SpawnBeam(from, to, _beamColor, 0.15f, 0.12f);
        }
    }
}
