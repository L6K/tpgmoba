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

        private const float Damage          = 15f;
        private const float ProjectileSpeed = 30f;
        private const float CooldownSeconds = 1.5f;
        private const float Range           = 12f;

        private const float AutoWindup   = 0.15f;
        private const float AutoRecovery = 0.25f;

        private AttackCooldown  _cooldown;
        private TargetingSystem _targeting;

        private void Awake()
        {
            _cooldown  = new AttackCooldown(CooldownSeconds);
            _targeting = GetComponent<TargetingSystem>();
        }

        private void Update()
        {
            if (_targeting == null) return;

            var target = _targeting.CurrentTarget;
            if (target == null) return;

            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist > Range) return;

            // Windup 中はクールダウンを消費しない
            if (_motor != null && _motor.Motion.Phase == AttackPhase.Windup) return;

            if (!_cooldown.TryConsume(Time.time)) return;
            if (_projectilePrefab == null || _muzzle == null) return;

            if (_motor != null)
            {
                // Strike 時点のターゲット位置を使うためキャプチャ
                HealthComponent capturedTarget = target;
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

        private void FireProjectile(HealthComponent target)
        {
            if (target == null || _projectilePrefab == null || _muzzle == null) return;
            var dir = (target.transform.position - _muzzle.position).normalized;
            var proj = Instantiate(_projectilePrefab, _muzzle.position, Quaternion.identity);
            proj.Init(dir, ProjectileSpeed, Damage, gameObject);
        }
    }
}
