using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Enigma.Combat
{
    // 予兆円: delay 経過後に範囲内の IDamageable へダメージを与えて消滅
    public sealed class TelegraphCircle : MonoBehaviour
    {
        private float      _damage;
        private float      _radius;
        private GameObject _owner;

        public void Init(float radius, float delaySeconds, float damage, GameObject owner)
        {
            _radius  = radius;
            _damage  = damage;
            _owner   = owner;

            // 直径をスケールに反映（薄い円柱プレハブ前提）
            transform.localScale = new Vector3(radius * 2f, transform.localScale.y, radius * 2f);

            StartCoroutine(ExplodeAfter(delaySeconds));
        }

        private IEnumerator ExplodeAfter(float delay)
        {
            yield return new WaitForSeconds(delay);

            // 中心から半径内にある全コライダーを取得してダメージ。
            // CharacterController + CapsuleCollider のような複数コライダー持ちに
            // 多重ヒットしないよう IDamageable 単位で重複排除する
            var hits = Physics.OverlapSphere(transform.position, _radius);
            var damaged = new HashSet<IDamageable>();
            foreach (var col in hits)
            {
                if (_owner != null && col.gameObject == _owner) continue;

                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null && damaged.Add(damageable))
                {
                    float finalDamage = DamageUtility.ApplyTeamBuff(_damage, _owner);
                    if (damageable is HealthComponent hc)
                        hc.TakeDamage(finalDamage, _owner);
                    else
                        damageable.TakeDamage(finalDamage);
                }
            }

            // 演出のために少し待ってから消滅
            yield return new WaitForSeconds(0.15f);
            Destroy(gameObject);
        }
    }
}
