using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enigma.Audio;
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

        private void OnEnable()
        {
            // 効果音は MonoBehaviour 側で鳴らす（ExperienceModel は plain C# のまま Unity 非依存に保つ）
            Experience.LevelChanged += OnLevelChanged;
        }

        private void OnDisable()
        {
            Experience.LevelChanged -= OnLevelChanged;
        }

        private void OnLevelChanged(int newLevel)
        {
            GameSfx.PlayUi("level_up", 0.9f);
        }

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

        // ラストヒット独占を廃止し、キラーのチームに属し死亡地点から ShareRadius 以内に
        // いる全チャンピオンへ全額付与する。本コンポーネントは「自分が受給対象か」だけを
        // 判定して自分の経験値へ加算する（各 PlayerProgression が同じ判定を独立に行う）。
        private void OnVictimDied(HealthComponent victim)
        {
            var killer = victim.LastAttacker;
            if (killer == null) return;

            var reward = victim.GetComponent<XpReward>();
            float xp = reward != null ? reward.Amount : 0f;
            if (xp <= 0f) return;

            var killerTeam = killer.GetComponent<TeamTag>();
            if (killerTeam == null) return;

            var deathPos = victim.transform.position;
            int killerId = killer.GetInstanceID();

            // 全チャンピオン（PlayerProgression 保持者）を候補に積む。数人規模なので毎回検索でよい。
            var all = Object.FindObjectsByType<PlayerProgression>(FindObjectsSortMode.None);
            var candidates = new List<XpShareLogic.Candidate>(all.Length);
            foreach (var p in all)
            {
                var tag = p.GetComponent<TeamTag>();
                if (tag == null) continue;
                float dist = Vector3.Distance(deathPos, p.transform.position);
                candidates.Add(new XpShareLogic.Candidate(p.gameObject.GetInstanceID(), tag.Team, dist));
            }

            var recipients = XpShareLogic.SelectRecipients(
                killerId, killerTeam.Team, candidates, XpShareLogic.ShareRadius);

            if (recipients.Contains(gameObject.GetInstanceID()))
                Experience.AddXp(xp);
        }
    }
}
