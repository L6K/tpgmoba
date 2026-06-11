using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enigma.Combat;

namespace Enigma.Character
{
    // MatchStatsTracker と同じ1秒スキャン方式でDied購読を動的追加する。
    // 両者が同じHealthComponentを購読しても互いの処理に干渉しない。
    public sealed class PlayerProgression : MonoBehaviour
    {
        private ExperienceModel _experience;

        // 遅延初期化: Awakeより前のアクセスにも安全
        public ExperienceModel Experience => _experience ??= new ExperienceModel();

        // レベルごとにダメージ4%増加
        public float DamageMultiplier => 1f + 0.04f * (Experience.Level - 1);

        private readonly HashSet<HealthComponent> _subscribed = new();

        private void Start()
        {
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
                    hc.Model.Died += () => OnVictimDied(hc);
            }
        }

        private void OnVictimDied(HealthComponent victim)
        {
            if (victim.LastAttacker != gameObject) return;

            var reward = victim.GetComponent<XpReward>();
            float xp = reward != null ? reward.Amount : 0f;
            if (xp > 0f)
                Experience.AddXp(xp);
        }
    }
}
