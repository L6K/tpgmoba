using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enigma.Combat;

namespace Enigma.Character
{
    // プレイヤーのゴールド管理。
    // - 毎秒 +2G（時間収入）
    // - 敵を最後に攻撃して倒した場合に GoldReward.Amount を加算
    public sealed class PlayerWallet : MonoBehaviour
    {
        private GoldWallet _wallet;

        // 遅延初期化: Awake 前のアクセスにも安全
        public GoldWallet Wallet => _wallet ??= new GoldWallet(500);

        private readonly HashSet<HealthComponent> _subscribed = new();

        private void Start()
        {
            StartCoroutine(IncomeLoop());
            StartCoroutine(ScanLoop());
        }

        // 毎秒 +2G の時間収入
        private IEnumerator IncomeLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                Wallet.Add(2);
            }
        }

        // PlayerProgression と同じ1秒スキャン方式で HealthComponent を動的購読
        private IEnumerator ScanLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                SubscribeNew();
            }
        }

        private void SubscribeNew()
        {
            var all = Object.FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            foreach (var hc in all)
            {
                if (_subscribed.Add(hc))
                    hc.Model.Died += () => OnVictimDied(hc);
            }
        }

        private void OnVictimDied(HealthComponent victim)
        {
            if (victim.LastAttacker != gameObject) return;

            var reward = victim.GetComponent<GoldReward>();
            if (reward != null && reward.Amount > 0)
                Wallet.Add(reward.Amount);
        }
    }
}
