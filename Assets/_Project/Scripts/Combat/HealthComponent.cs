using UnityEngine;

namespace Enigma.Combat
{
    public sealed class HealthComponent : MonoBehaviour, IDamageable
    {
        [SerializeField] private float _maxHp = 200f;

        private HealthModel _model;

        // Awake 前（他コンポーネントの Awake 等）にアクセスされても安全なよう遅延初期化
        public HealthModel Model => _model ??= new HealthModel(_maxHp);

        public void TakeDamage(float amount)
        {
            Model.TakeDamage(amount);
        }
    }
}
