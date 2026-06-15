using System.Collections;
using UnityEngine;
using Enigma.Combat;

namespace Enigma.Map
{
    public sealed class TreeTopplePresenter : MonoBehaviour
    {
        private const float ToppleDuration = 1.2f;
        private const float HoldDuration = 0.8f;
        private const float SinkDuration = 1.5f;

        private bool _falling;

        public void Fall(Vector3 worldToppleAxis, float delaySeconds)
        {
            // 二重再生ガード
            if (_falling) return;
            _falling = true;
            StartCoroutine(FallRoutine(worldToppleAxis, delaySeconds));
        }

        private IEnumerator FallRoutine(Vector3 worldToppleAxis, float delaySeconds)
        {
            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);

            // 転倒
            var initialRot = transform.rotation;
            float elapsed = 0f;
            while (elapsed < ToppleDuration)
            {
                float t = ToppleDuration > 0f ? elapsed / ToppleDuration : 1f;
                // ワールド軸で前掛けする。木はランダム yaw を持つためローカル軸は使わない。
                // コライダーはルートごと回るので見た目と一致する。
                transform.rotation = Quaternion.AngleAxis(DeathAnimationCurve.ToppleAngle(t), worldToppleAxis) * initialRot;
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.rotation = Quaternion.AngleAxis(90f, worldToppleAxis) * initialRot;

            // 倒れた木を少し見せる
            yield return new WaitForSeconds(HoldDuration);

            // 沈下: 倒れた後に測ることで横倒し状態の高さになる
            float depth = 2f;
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                bool hasB = false;
                Bounds b = default;
                foreach (var r in renderers)
                {
                    if (!hasB) { b = r.bounds; hasB = true; }
                    else b.Encapsulate(r.bounds);
                }
                depth = b.size.y * 1.2f;
            }

            var sinkStartPos = transform.position;
            elapsed = 0f;
            while (elapsed < SinkDuration)
            {
                float t = SinkDuration > 0f ? elapsed / SinkDuration : 1f;
                transform.position = sinkStartPos - new Vector3(0f, DeathAnimationCurve.SinkDepth(t, depth), 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // コライダーも一緒に消え、見えない壁を残さない
            gameObject.SetActive(false);
        }
    }
}
