using UnityEngine;

namespace Enigma.Character
{
    public sealed class HealthBarBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // カメラの方向へ向ける（ビルボード）
            transform.rotation = cam.transform.rotation;
        }
    }
}
