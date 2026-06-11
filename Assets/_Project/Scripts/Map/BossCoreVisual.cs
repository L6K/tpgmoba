using UnityEngine;

namespace Enigma.Map
{
    /// <summary>
    /// エニグマ・コアの見た目（浮遊クリスタル＋リング＋破片）をアニメーションさせる。
    /// 見た目専用でゲームロジックには関与しない。
    /// </summary>
    public sealed class BossCoreVisual : MonoBehaviour
    {
        [SerializeField] private Transform _crystal;
        [SerializeField] private Transform _ringA;
        [SerializeField] private Transform _ringB;
        [SerializeField] private Transform _ringC;
        [SerializeField] private Transform _shardRoot;

        private const float BobAmplitude = 0.3f;
        private const float BobPeriod    = 4f;

        // ボブはローカルYの初期値を基準に上下させる
        private float _crystalBaseY;
        private bool  _hasBase;

        private void Awake()
        {
            if (_crystal != null)
            {
                _crystalBaseY = _crystal.localPosition.y;
                _hasBase      = true;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_crystal != null)
            {
                _crystal.Rotate(Vector3.up, 20f * dt, Space.Self);

                if (_hasBase)
                {
                    float bob = Mathf.Sin(Time.time / BobPeriod * Mathf.PI * 2f) * BobAmplitude;
                    var   p   = _crystal.localPosition;
                    p.y = _crystalBaseY + bob;
                    _crystal.localPosition = p;
                }
            }

            if (_ringA != null) _ringA.Rotate(Vector3.up,      15f * dt, Space.Self);
            if (_ringB != null) _ringB.Rotate(Vector3.right,  -25f * dt, Space.Self);
            if (_ringC != null) _ringC.Rotate(Vector3.forward, 10f * dt, Space.Self);

            if (_shardRoot != null) _shardRoot.Rotate(Vector3.up, -30f * dt, Space.Self);
        }
    }
}
