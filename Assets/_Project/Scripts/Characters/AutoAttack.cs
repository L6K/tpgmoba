using UnityEngine;
using Enigma.Combat;

namespace Enigma.Character
{
    // TargetingSystem.CurrentTarget が射程内であれば CD ごとに自動発射
    public sealed class AutoAttack : MonoBehaviour
    {
        [SerializeField] private Projectile        _projectilePrefab;
        [SerializeField] private Transform         _muzzle;
        [SerializeField] private PlayerAttackMotor _motor;

        // characters.json を正としてランタイム/インポータから上書きできるよう、定数からフィールドへ昇格（既定値は従来の定数値）
        [SerializeField] private float _attackDamage   = 15f;
        [SerializeField] private float _projectileSpeed = 30f;
        [SerializeField] private float _attackCooldown = 1.5f;
        [SerializeField] private float _attackRange    = 12f;

        private const float AutoWindup   = 0.15f;
        private const float AutoRecovery = 0.25f;

        // AA モーション中(準備〜後隙)にターゲットへ向き直る速度
        private const float FaceTurnSpeed = 14f;

        private AttackCooldown  _cooldown;
        private TargetingSystem _targeting;
        private Transform       _faceTarget;

        // MatchBootstrap など composition root からピック済みステータスを反映する。
        // Awake 済みなら CD インスタンスも作り直す
        public void Configure(float damage, float range, float cooldownSeconds)
        {
            _attackDamage   = damage;
            _attackRange    = range;
            _attackCooldown = cooldownSeconds;
            if (_cooldown != null)
                _cooldown = new AttackCooldown(_attackCooldown);
        }

        private void Awake()
        {
            _cooldown  = new AttackCooldown(_attackCooldown);
            _targeting = GetComponent<TargetingSystem>();
        }

        private void Update()
        {
            FaceTargetDuringMotion();

            if (_targeting == null) return;

            var target = _targeting.CurrentTarget;
            if (target == null) return;

            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist > _attackRange) return;

            // Windup 中はクールダウンを消費しない
            if (_motor != null && _motor.Motion.Phase == AttackPhase.Windup) return;

            if (!_cooldown.TryConsume(Time.time)) return;
            if (_projectilePrefab == null || _muzzle == null) return;

            if (_motor != null)
            {
                // Strike 時点のターゲット位置を使うためキャプチャ
                HealthComponent capturedTarget = target;
                _faceTarget = capturedTarget.transform;
                _motor.RequestAttack(AutoWindup, AutoRecovery, () =>
                {
                    _motor.SnapToLunge();
                    FireProjectile(capturedTarget);
                });
            }
            else
            {
                // _motor 未設定時は従来どおり即時発射（後方互換）
                FireProjectile(target);
            }
        }

        // AA モーション中(準備〜後隙)はターゲットの方向へ滑らかに向き直る。
        // 移動入力による向きは PlayerController が担うが、Windup 中は移動がロックされるため競合しない
        private void FaceTargetDuringMotion()
        {
            if (_motor == null || _faceTarget == null) return;

            if (_motor.Motion.Phase == AttackPhase.None)
            {
                _faceTarget = null;
                return;
            }

            var flat = _faceTarget.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(flat),
                FaceTurnSpeed * Time.deltaTime);
        }

        private void FireProjectile(HealthComponent target)
        {
            if (target == null || _projectilePrefab == null || _muzzle == null) return;
            var dir = (target.transform.position - _muzzle.position).normalized;
            var proj = Instantiate(_projectilePrefab, _muzzle.position, Quaternion.identity);
            proj.Init(dir, _projectileSpeed, _attackDamage, gameObject);
        }
    }
}
