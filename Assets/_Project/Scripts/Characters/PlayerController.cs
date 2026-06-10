using UnityEngine;
using UnityEngine.InputSystem;

namespace Enigma.Character
{
    // 暫定実装: Input System の直接ポーリング（InputActions アセット未使用）
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _turnSpeedDegrees = 720f;

        private const float Gravity = -20f;

        private CharacterController _cc;
        private float _verticalVelocity;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // カーソルロック状態に依存しない（FF14準拠操作）
            var input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;

            float cameraYaw = _cameraTransform != null ? _cameraTransform.eulerAngles.y : 0f;
            var moveDir = MovementLogic.CameraRelativeMove(input, cameraYaw);

            // 重力
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            _verticalVelocity += Gravity * Time.deltaTime;

            var motion = moveDir * (_moveSpeed * Time.deltaTime);
            motion.y = _verticalVelocity * Time.deltaTime;
            _cc.Move(motion);

            // 移動中は移動方向へ回頭
            if (moveDir != Vector3.zero)
            {
                transform.rotation = MovementLogic.RotateTowards(
                    transform.rotation, moveDir, _turnSpeedDegrees * Time.deltaTime);
            }
        }
    }
}
