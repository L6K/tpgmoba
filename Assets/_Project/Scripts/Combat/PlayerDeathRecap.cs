using UnityEngine;
using Enigma.UI;

namespace Enigma.Combat
{
    /// <summary>
    /// プレイヤーの被ダメージを攻撃者ごとに記録し、死亡時に「何にどれだけ削られたか」の
    /// リキャップを HUD へ表示するハンブルオブジェクト。集計ロジックは DeathRecapModel に委譲する。
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public sealed class PlayerDeathRecap : MonoBehaviour
    {
        [SerializeField] private HealthComponent _health;
        [SerializeField] private GameHudController _hud;

        // 直近 12 秒の被ダメージを集計する（DeathRecapModel の既定窓）。
        private readonly DeathRecapModel _recap = new DeathRecapModel();

        private void Awake()
        {
            if (_health == null) _health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            if (_health == null) return;
            _health.Damaged       += OnDamaged;
            _health.Model.Died    += OnDied;
            _health.Model.Revived += OnRevived;
        }

        private void OnDisable()
        {
            if (_health == null) return;
            _health.Damaged       -= OnDamaged;
            if (_health.Model == null) return;
            _health.Model.Died    -= OnDied;
            _health.Model.Revived -= OnRevived;
        }

        // 実際に HP が削れた量だけ通知される（シールド全吸収時は発火しない）。
        // 攻撃者は直前に HealthComponent.LastAttacker へ設定済み。
        private void OnDamaged(float amount)
        {
            string source = ResolveSource(_health.LastAttacker);
            _recap.Record(source, amount, Time.time);
        }

        private void OnDied()
        {
            if (_hud == null) return;
            var entries = _recap.BuildRecap(Time.time);
            _hud.ShowDeathRecap("被ダメージ内訳", entries);
        }

        private void OnRevived()
        {
            // 復活したら次の死に向けて記録をリセットする。
            _recap.Clear();
        }

        // 攻撃者 GameObject を読みやすい表示名へ。弾の場合はオーナー GO が渡ってくる。
        private static string ResolveSource(GameObject attacker)
        {
            if (attacker == null) return DeathRecapSourceName.Unknown;
            return DeathRecapSourceName.Clean(attacker.name);
        }
    }
}
