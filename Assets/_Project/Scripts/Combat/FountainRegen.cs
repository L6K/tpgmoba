using UnityEngine;

namespace Enigma.Combat
{
    /// <summary>
    /// 自陣の泉(ベース)付近にいる間、毎秒 HP を回復する。プレイヤー/AIチャンピオン共用。
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public sealed class FountainRegen : MonoBehaviour
    {
        [SerializeField] private Vector3 _fountainCenter;
        [SerializeField] private float _radius = 10f;
        [SerializeField] private float _hpPerSecond = 25f;

        private HealthComponent _health;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
        }

        private void Update()
        {
            if (_health.Model.IsDead) return;

            var flat = transform.position - _fountainCenter;
            flat.y = 0f;
            if (flat.sqrMagnitude > _radius * _radius) return;

            _health.Model.Heal(_hpPerSecond * Time.deltaTime);
        }
    }
}
