using UnityEngine;
using UnityEngine.InputSystem;
using Enigma.Combat;

namespace Enigma.Character
{
    // 暫定実装: Input System の直接ポーリング（InputActions アセット未使用）
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform         _cameraTransform;
        [SerializeField] private float             _moveSpeed = 6f;
        [SerializeField] private float             _turnSpeedDegrees = 720f;
        [SerializeField] private Animator          _animator; // 見た目モデルのアニメーター（任意）
        [SerializeField] private PlayerAttackMotor _motor;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");

        private const float Gravity = -20f;

        private CharacterController _cc;
        private float _verticalVelocity;
        private PlayerItems _playerItems;
        private StatusEffectController _statusEffects;
        private float _dashTimeRemaining;
        private Vector3 _dashVelocity;

        // characters.json を正とするピック済み移動速度を反映する（composition root から呼ぶ）
        public void SetMoveSpeed(float moveSpeed)
        {
            _moveSpeed = moveSpeed;
        }

        private void Awake()
        {
            _cc           = GetComponent<CharacterController>();
            _playerItems  = GetComponent<PlayerItems>();
            _statusEffects = StatusEffectController.GetOrAdd(gameObject);
        }

        // dir は水平方向(正規化不要、内部で正規化)。distance(m) を duration 秒で移動するダッシュ。
        public void RequestDash(Vector3 dir, float distance, float duration = 0.15f)
        {
            if (duration <= 0f || distance <= 0f) return;
            if (_statusEffects != null && !_statusEffects.CanMove) return; // ルート/スタン中はダッシュ不可
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            _dashVelocity = dir.normalized * (distance / duration);
            _dashTimeRemaining = duration;
        }

        private void Update()
        {
            if (_dashTimeRemaining > 0f)
            {
                _dashTimeRemaining -= Time.deltaTime;
                if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
                _verticalVelocity += Gravity * Time.deltaTime;
                var dstep = _dashVelocity * Time.deltaTime;
                dstep.y = _verticalVelocity * Time.deltaTime;
                _cc.Move(dstep);
                var look = _dashVelocity; look.y = 0f;
                if (look.sqrMagnitude > 0.0001f)
                    transform.rotation = MovementLogic.RotateTowards(transform.rotation, look.normalized, _turnSpeedDegrees * Time.deltaTime);
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // カーソルロック状態に依存しない（FF14準拠操作）
            var input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;

            bool hasInput = input != Vector2.zero;

            // Windup 中は移動入力を無視（重力は適用継続）
            bool ccImmobile    = _statusEffects != null && !_statusEffects.CanMove;
            bool movementLocked = (_motor != null && _motor.Motion.MovementLocked) || ccImmobile;

            // 移動入力があり Recovery 中はリカバリをキャンセルしてから移動
            if (hasInput && _motor != null && _motor.Motion.Phase == AttackPhase.Recovery)
            {
                _motor.Motion.CancelRecovery();
            }

            float cameraYaw = _cameraTransform != null ? _cameraTransform.eulerAngles.y : 0f;
            var moveDir = movementLocked
                ? Vector3.zero
                : MovementLogic.CameraRelativeMove(input, cameraYaw);

            // 重力
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            _verticalVelocity += Gravity * Time.deltaTime;

            var motion = moveDir * (_moveSpeed * (_playerItems != null ? _playerItems.MoveSpeedMultiplier : 1f) * (_statusEffects != null ? _statusEffects.MoveSpeedMultiplier : 1f) * Time.deltaTime);
            motion.y = _verticalVelocity * Time.deltaTime;
            _cc.Move(motion);

            // 移動中は移動方向へ回頭
            if (moveDir != Vector3.zero)
            {
                transform.rotation = MovementLogic.RotateTowards(
                    transform.rotation, moveDir, _turnSpeedDegrees * Time.deltaTime);
            }

            if (_animator != null)
                _animator.SetFloat(SpeedParam, moveDir == Vector3.zero ? 0f : 1f);
        }
    }
}
