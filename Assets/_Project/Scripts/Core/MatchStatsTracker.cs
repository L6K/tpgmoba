using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enigma.Combat;
using Enigma.Data;

namespace Enigma.Core
{
    // 1秒ごとに HealthComponent を再スキャンして Died イベントを購読し、
    // KDA を MatchContext に集積する。動的スポーンのミニオンにも対応。
    public sealed class MatchStatsTracker : MonoBehaviour
    {
        private readonly HashSet<HealthComponent> _subscribed = new();

        private void Start()
        {
            if (!GameServices.IsInitialized) GameServices.Initialize();
            StartCoroutine(ScanLoop());
        }

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
                    hc.Model.Died += () => OnDied(hc);
            }
        }

        private static void OnDied(HealthComponent hc)
        {
            var match = GameServices.Match;
            if (match == null) return;

            // 対象がプレイヤーなら Deaths++
            if (hc.CompareTag("Player"))
            {
                match.Deaths++;
                return;
            }

            // 攻撃者がプレイヤー本体（タグ Player）なら Kills++
            var attacker = hc.LastAttacker;
            if (attacker != null && attacker.CompareTag("Player"))
                match.Kills++;
        }
    }
}
