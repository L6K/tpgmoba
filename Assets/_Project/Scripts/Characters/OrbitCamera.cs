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

        // 被弾シェイク: 振幅は時間で減衰し、毎フレーム System.Random でオフセットを揺らす。
        // UnityEngine.Random を避けるため独自の乱数源を持つ
        private const float ShakeDecaySeconds = 0.5f;
        private readonly System.Random _shakeRng = new System.Random();
        private float _shakeAmplitude;

        // 外部（被弾フィードバック）から呼び出してシェイクを開始/上書きする。
        // amplitude=0.15 を基準に、より強い揺れが来たときのみ上書きする
        public void AddShake(float amplitude)
        {
            if (amplitude > _shakeAmplitude)
                _shakeAmplitude = amplitude;
        }

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

            transform.position = pivotPos + backOffset + ComputeShakeOffset();
            transform.rotation = rotation;
        }

        // 減衰中のランダムオフセットを返す。振幅は ShakeDecaySeconds で 0 へ向かって線形減衰する
        private Vector3 ComputeShakeOffset()
        {
            if (_shakeAmplitude <= 0f) return Vector3.zero;

            _shakeAmplitude -= 0.15f / ShakeDecaySeconds * Time.deltaTime;
            if (_shakeAmplitude < 0f) _shakeAmplitude = 0f;

            float x = ((float)_shakeRng.NextDouble() * 2f - 1f) * _shakeAmplitude;
            float y = ((float)_shakeRng.NextDouble() * 2f - 1f) * _shakeAmplitude;
            return new Vector3(x, y, 0f);
        }
    }
}
