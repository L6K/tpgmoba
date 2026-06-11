using UnityEngine;

namespace Enigma.Combat
{
    public sealed class HealthComponent : MonoBehaviour, IDamageable
    {
        [SerializeField] private float _maxHp = 200f;

        private HealthModel _model;

        // Awake 前（他コンポーネントの Awake 等）にアクセスされても安全なよう遅延初期化
        public HealthModel Model => _model ??= new HealthModel(_maxHp);

        // 最後にダメージを与えた攻撃者（弾の場合は弾を発射したオーナー GO）
        public GameObject LastAttacker { get; private set; }

        // IDamageable 実装: 帰属なしのダメージ（互換維持のため attacker=null で委譲）
        public void TakeDamage(float amount)
        {
            TakeDamage(amount, null);
        }

        public void TakeDamage(float amount, GameObject attacker)
        {
            if (attacker != null)
                LastAttacker = attacker;
            Model.TakeDamage(amount);
        }
    }
}
