using UnityEngine;

namespace Enigma.UI
{
    // ダメージポップアップ1個を自走させるコンポーネント。
    // DamagePopupManager が生成し、浮上しながらフェードアウトして自己破棄する。
    public sealed class DamagePopup : MonoBehaviour
    {
        private const float RiseDuration   = 1.0f;
        private const float RiseDistance   = 1.2f;
        private const float PunchDuration  = 0.12f;
        private const float PunchScale     = 1.5f;

        private TextMesh   _textMesh;
        private TextMesh   _shadowMesh;
        private float      _elapsed;
        private Color      _startColor;

        public void Init(float amount, bool isPlayerDamage)
        {
            _textMesh             = GetComponent<TextMesh>();
            _startColor           = isPlayerDamage ? Color.red : Color.white;
            _textMesh.color       = _startColor;
            _textMesh.text        = Mathf.RoundToInt(amount).ToString();

            // 影テキスト: 本体の子 GO に同一テキストを黒で描画
            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(transform, false);
            shadowGo.transform.localPosition = new Vector3(0.025f, -0.025f, 0.001f);
            shadowGo.transform.localScale    = Vector3.one;

            _shadowMesh              = shadowGo.AddComponent<TextMesh>();
            _shadowMesh.text         = _textMesh.text;
            _shadowMesh.fontSize     = _textMesh.fontSize;
            _shadowMesh.characterSize = _textMesh.characterSize;
            _shadowMesh.anchor       = _textMesh.anchor;
            _shadowMesh.alignment    = _textMesh.alignment;
            _shadowMesh.color        = new Color(0f, 0f, 0f, _startColor.a);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            // スケールパンチ: 0〜PunchDuration で PunchScale→1.0 へイーズアウト
            if (_elapsed < PunchDuration)
            {
                float t = _elapsed / PunchDuration;
                // イーズアウト: 1-(1-t)^2
                float eased = 1f - (1f - t) * (1f - t);
                float s = Mathf.Lerp(PunchScale, 1f, eased);
                transform.localScale = new Vector3(s, s, s);
            }
            else
            {
                transform.localScale = Vector3.one;
            }

            // 上昇: イーズアウト（残り寿命比率の二乗で速度減衰）
            float lifeRatio    = Mathf.Clamp01(_elapsed / RiseDuration);
            // 積分: ∫(1-t)^2 dt = t - t^2 + t^3/3 の 0→lifeRatio を RiseDistance でスケール
            // 簡易近似として (1-(1-lifeRatio)^2) を使用
            float easeOutPos   = 1f - (1f - lifeRatio) * (1f - lifeRatio);
            transform.localPosition = Vector3.up * (RiseDistance * easeOutPos);

            // フェードアウト（後半0.5秒で透明へ）
            float alpha = 1f - Mathf.Clamp01((_elapsed - RiseDuration * 0.5f) / (RiseDuration * 0.5f));
            _textMesh.color = new Color(_startColor.r, _startColor.g, _startColor.b, alpha);

            // 影の透明度を本体に同期
            if (_shadowMesh != null)
                _shadowMesh.color = new Color(0f, 0f, 0f, alpha);

            // カメラへのビルボード
            var cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;

            if (_elapsed >= RiseDuration)
                Destroy(gameObject);
        }
    }
}
