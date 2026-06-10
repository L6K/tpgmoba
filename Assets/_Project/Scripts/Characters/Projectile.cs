using UnityEngine;
using Enigma.Combat;

namespace Enigma.Character
{
    // コライダーは isTrigger 前提（BuildAetherRiftMap エディタスクリプト側で設定）
    public sealed class Projectile : MonoBehaviour
    {
        private Vector3 _direction;
        private float _speed;
        private float _damage;
        private GameObject _owner;
        private float _lifeTimer;

        private const float DefaultLifetime = 1.5f;

        public void Init(Vector3 direction, float speed, float damage, GameObject owner,
                         float lifetime = DefaultLifetime)
        {
            _direction = direction.normalized;
            _speed     = speed;
            _damage    = damage;
            _owner     = owner;
            _lifeTimer = 0f;
            _lifetime  = lifetime > 0f ? lifetime : DefaultLifetime;
        }

        private float _lifetime = DefaultLifetime;

        private void Update()
        {
            _lifeTimer += Time.deltaTime;
            if (_lifeTimer >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            transform.Translate(_direction * (_speed * Time.deltaTime), Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            // オーナー自身と Trigger は無視
            if (other.isTrigger) return;
            if (_owner != null && other.gameObject == _owner) return;

            var damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(_damage);
            }

            Destroy(gameObject);
        }
    }
}
