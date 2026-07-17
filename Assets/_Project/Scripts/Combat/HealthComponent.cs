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

        // ダメージ受付ゲート（露出前タイタン等に注入）。null なら従来通りゲートなしで全ダメージが通る。
        public IDamageGate DamageGate { get; set; }

        // ダメージポップアップ等の購読者に実ダメージ量を通知する
        public event System.Action<float> Damaged;

        // IDamageable 実装: 帰属なしのダメージ（互換維持のため attacker=null で委譲）
        public void TakeDamage(float amount)
        {
            TakeDamage(amount, null);
        }

        public void TakeDamage(float amount, GameObject attacker)
        {
            // 露出前タイタンなど、ゲートが閉じている間はダメージを 0 化して無効化する
            // （ゲート未設定=null なら amount はそのまま=従来挙動）。
            amount = DamageGating.Effective(amount, DamageGate);

            if (attacker != null)
                LastAttacker = attacker;
            // シールド吸収を考慮し、実際に減った HP 量だけを通知する(全吸収時は発火しない)
            float before = Model.CurrentHp;
            Model.TakeDamage(amount);
            float dealtToHp = before - Model.CurrentHp;
            if (dealtToHp > 0f)
                Damaged?.Invoke(dealtToHp);
        }

        private void Update()
        {
            Model.Tick(Time.deltaTime);
        }
    }
}
