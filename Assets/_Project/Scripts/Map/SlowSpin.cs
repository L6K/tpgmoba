using UnityEngine;

namespace Enigma.Map
{
    /// <summary>
    /// 見た目専用: Y軸ゆっくり自転 + 初期ローカルYを基準にした上下ボブ。
    /// タワー頂上のチーム色クリスタル等に付ける。ゲームロジックには関与しない。
    /// </summary>
    public sealed class SlowSpin : MonoBehaviour
    {
        [SerializeField] private float _degreesPerSecond = 40f;
        [SerializeField] private float _bobAmplitude     = 0.15f;
        [SerializeField] private float _bobPeriod        = 3f;

        private float _baseLocalY;
        private float _phase;

        private void Awake()
        {
            _baseLocalY = transform.localPosition.y;
        }

        private void Update()
        {
            // Y軸自転
            transform.Rotate(0f, _degreesPerSecond * Time.deltaTime, 0f, Space.Self);

            // 上下ボブ（初期ローカルY基準）
            if (_bobPeriod > 0.0001f)
            {
                _phase += Time.deltaTime / _bobPeriod * Mathf.PI * 2f;
                var p = transform.localPosition;
                p.y = _baseLocalY + Mathf.Sin(_phase) * _bobAmplitude;
                transform.localPosition = p;
            }
        }
    }
}
