using UnityEngine;

namespace Enigma.Combat
{
    public sealed class HealthComponent : MonoBehaviour, IDamageable
    {
        [SerializeField] private float _maxHp = 200f;

        public HealthModel Model { get; private set; }

        private void Awake()
        {
            Model = new HealthModel(_maxHp);
        }

        public void TakeDamage(float amount)
        {
            Model.TakeDamage(amount);
        }
    }
}
