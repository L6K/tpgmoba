using UnityEngine;

namespace Enigma.UI
{
    // ダメージポップアップ1個を自走させるコンポーネント。
    // DamagePopupManager が生成し、浮上しながらフェードアウトして自己破棄する。
    public sealed class DamagePopup : MonoBehaviour
    {
        private const float RiseDuration  = 1.0f;
        private const float RiseDistance  = 1.2f;

        private TextMesh   _textMesh;
        private float      _elapsed;
        private Color      _startColor;

        public void Init(float amount, bool isPlayerDamage)
        {
            _textMesh             = GetComponent<TextMesh>();
            _startColor           = isPlayerDamage ? Color.red : Color.white;
            _textMesh.color       = _startColor;
            _textMesh.text        = Mathf.RoundToInt(amount).ToString();
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(_elapsed / RiseDuration);

            // 上昇
            transform.localPosition = Vector3.up * (RiseDistance * t);

            // フェードアウト（後半0.5秒で透明へ）
            float alpha = 1f - Mathf.Clamp01((_elapsed - RiseDuration * 0.5f) / (RiseDuration * 0.5f));
            _textMesh.color = new Color(_startColor.r, _startColor.g, _startColor.b, alpha);

            // カメラへのビルボード
            var cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;

            if (_elapsed >= RiseDuration)
                Destroy(gameObject);
        }
    }
}
