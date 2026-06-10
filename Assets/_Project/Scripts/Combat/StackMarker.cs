using System.Collections;
using UnityEngine;
using Enigma.Objective;

namespace Enigma.Combat
{
    // 頭割りマーカー: 対象に追従し delay 後にその時点の位置を中心に範囲ダメージ
    public sealed class StackMarker : MonoBehaviour
    {
        private Transform  _target;
        private float      _totalDamage;
        private float      _radius;
        private GameObject _owner;

        public void Init(
            Transform target,
            float delaySeconds,
            float totalDamage,
            float radius,
            GameObject owner)
        {
            _target      = target;
            _totalDamage = totalDamage;
            _radius      = radius;
            _owner       = owner;

            StartCoroutine(TrackAndExplode(delaySeconds));
        }

        private IEnumerator TrackAndExplode(float delay)
        {
            float elapsed = 0f;
            while (elapsed < delay)
            {
                if (_target != null)
                {
                    // 対象の頭上 2.2m に追従
                    transform.position = _target.position + Vector3.up * 2.2f;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            // delay 経過時点の位置を爆発中心にする
            var explosionCenter = _target != null
                ? _target.position
                : transform.position - Vector3.up * 2.2f;

            var hits = Physics.OverlapSphere(explosionCenter, _radius);

            // owner 以外の HealthComponent を収集して人数を数える
            var targets = new System.Collections.Generic.List<HealthComponent>();
            foreach (var col in hits)
            {
                if (_owner != null && col.gameObject == _owner) continue;

                var hc = col.GetComponentInParent<HealthComponent>();
                if (hc != null && !targets.Contains(hc))
                    targets.Add(hc);
            }

            float dmgPerTarget = StackDamageLogic.DamagePerTarget(_totalDamage, targets.Count);
            foreach (var hc in targets)
                hc.TakeDamage(dmgPerTarget);

            Destroy(gameObject);
        }
    }
}
