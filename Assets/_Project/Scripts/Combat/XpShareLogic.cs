using System.Collections.Generic;

namespace Enigma.Combat
{
    // XP 分配の対象選定（純粋ロジック、UnityEngine 非依存）。
    // ラストヒット独占を廃し、キラーのチームかつ死亡地点から一定半径内の味方全員へ
    // 全額付与する。受け取り手の決定だけを担い、付与額の加算は呼び出し側が行う。
    public static class XpShareLogic
    {
        // 死亡地点からの受給半径（m）。
        public const float ShareRadius = 16f;

        // 受給候補。id は重複排除のための同一性キー（MonoBehaviour の GetInstanceID 等）。
        public readonly struct Candidate
        {
            public readonly int Id;
            public readonly TeamId Team;
            public readonly float DistanceToDeath;

            public Candidate(int id, TeamId team, float distanceToDeath)
            {
                Id = id;
                Team = team;
                DistanceToDeath = distanceToDeath;
            }
        }

        /// <summary>
        /// 受給対象の id 集合を返す。条件:
        /// ・キラーと同じチームに属する候補のみ。
        /// ・死亡地点から radius 以内の候補。ただしキラー自身は距離に関わらず必ず含める。
        /// ・同一 id は重複させない。
        /// 受給者ゼロでも空集合を返すだけで安全。
        /// </summary>
        // 戻り値は集合（重複なし・順不同）。HashSet にすることで Contains 判定が O(1)、
        // かつ「同一 id を重複させない」要件を型で表現する。
        public static HashSet<int> SelectRecipients(
            int killerId, TeamId killerTeam, IReadOnlyList<Candidate> candidates, float radius)
        {
            var result = new HashSet<int>();
            if (candidates == null) return result;

            foreach (var c in candidates)
            {
                if (c.Team != killerTeam) continue;

                // キラー本人は距離外でも受給する（最後の一撃を入れた本人を取りこぼさない）
                bool isKiller = c.Id == killerId;
                if (!isKiller && c.DistanceToDeath > radius) continue;

                result.Add(c.Id);
            }

            return result;
        }
    }
}
