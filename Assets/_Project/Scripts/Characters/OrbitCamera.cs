using UnityEngine;
using UnityEngine.InputSystem;

namespace Enigma.Character
{
    // FF14 準拠: ドラッグ中のみ回転、カーソルは常時表示
    public sealed class OrbitCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;

        private const float Sensitivity    = 0.12f;
        private const float PitchMin       = -30f;
        private const float PitchMax       = 70f;
        private const float DistanceMin    = 3f;
        private const float DistanceMax    = 12f;
        private const float DistanceDefault = 6f;
        private const float ScrollSensitivity = 0.01f;
        private const float HeightOffset   = 1.6f;

        private float _yaw;
        private float _pitch = 10f;
        private float _distance = DistanceDefault;

        private void Start()
        {
            // ドラッグ中以外は常時表示
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            bool isDragging = mouse.leftButton.isPressed || mouse.rightButton.isPressed;

            if (isDragging)
            {
                // ドラッグ中のみカーソルを非表示にして回転
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Confined;

                var delta = mouse.delta.ReadValue();
                _yaw   += delta.x * Sensitivity;
                _pitch -= delta.y * Sensitivity;
                _pitch  = Mathf.Clamp(_pitch, PitchMin, PitchMax);
            }
            else
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            // ホイールズーム
            var scroll = mouse.scroll.ReadValue();
            _distance -= scroll.y * ScrollSensitivity;
            _distance  = Mathf.Clamp(_distance, DistanceMin, DistanceMax);
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            var rotation  = Quaternion.Euler(_pitch, _yaw, 0f);
            var pivotPos  = _target.position + Vector3.up * HeightOffset;
            var backOffset = rotation * new Vector3(0f, 0f, -_distance);

            transform.position = pivotPos + backOffset;
            transform.rotation = rotation;
        }
    }
}
