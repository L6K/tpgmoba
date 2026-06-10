using UnityEngine;
using UnityEngine.InputSystem;
using Enigma.Combat;

namespace Enigma.Character
{
    // 左クリック（ドラッグ < 5px）でレイキャスト、HealthComponent を持つ非プレイヤーをターゲット選択
    public sealed class TargetingSystem : MonoBehaviour
    {
        [SerializeField] private GameObject _targetRingPrefab;

        private const float MaxClickDragPixels = 5f;

        public HealthComponent CurrentTarget { get; private set; }

        private GameObject _ringInstance;
        private Vector2    _pressPosition;
        private bool       _pressing;

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _pressPosition = mouse.position.ReadValue();
                _pressing = true;
            }

            if (mouse.leftButton.wasReleasedThisFrame && _pressing)
            {
                _pressing = false;
                var releasePos = mouse.position.ReadValue();
                float drag = Vector2.Distance(_pressPosition, releasePos);

                // ドラッグ距離が閾値未満のときのみクリックとみなす
                if (drag < MaxClickDragPixels)
                {
                    TrySelect(releasePos);
                }
            }

            // ターゲットリングをターゲット足元に追従
            UpdateRing();
        }

        /// <summary>SkillCaster がアーム中にクリックを横取りするために呼ぶ。選択処理をスキップ。</summary>
        public void CancelPendingClick()
        {
            _pressing = false;
        }

        private void TrySelect(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null) return;

            var ray = cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
            if (!Physics.Raycast(ray, out var hit, 200f)) return;

            var hc = hit.collider.GetComponentInParent<HealthComponent>();
            if (hc == null || hc.gameObject == gameObject)
            {
                // 地面など：ターゲット解除
                ClearTarget();
                return;
            }

            SetTarget(hc);
        }

        private void SetTarget(HealthComponent hc)
        {
            if (CurrentTarget != null)
            {
                CurrentTarget.Model.Died -= OnTargetDied;
            }

            CurrentTarget = hc;
            CurrentTarget.Model.Died += OnTargetDied;

            if (_targetRingPrefab != null && _ringInstance == null)
            {
                _ringInstance = Instantiate(_targetRingPrefab);
            }

            if (_ringInstance != null) _ringInstance.SetActive(true);
        }

        public void ClearTarget()
        {
            if (CurrentTarget != null)
            {
                CurrentTarget.Model.Died -= OnTargetDied;
                CurrentTarget = null;
            }

            if (_ringInstance != null) _ringInstance.SetActive(false);
        }

        private void OnTargetDied()
        {
            ClearTarget();
        }

        private void UpdateRing()
        {
            if (_ringInstance == null || CurrentTarget == null) return;
            if (!_ringInstance.activeSelf) return;

            var pos = CurrentTarget.transform.position;
            pos.y += 0.05f;
            _ringInstance.transform.position = pos;
        }

        private void OnDestroy()
        {
            if (CurrentTarget != null)
            {
                CurrentTarget.Model.Died -= OnTargetDied;
            }

            if (_ringInstance != null) Destroy(_ringInstance);
        }
    }
}
