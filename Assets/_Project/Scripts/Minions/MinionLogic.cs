using System.Collections.Generic;
using UnityEngine;
using Enigma.Combat;

namespace Enigma.Minion
{
    public readonly struct TargetCandidate
    {
        public readonly Vector3 Position;
        public readonly TeamId Team;

        public TargetCandidate(Vector3 position, TeamId team)
        {
            Position = position;
            Team     = team;
        }
    }

    public static class MinionLogic
    {
        /// <summary>
        /// aggroRange 内で最も近い敵（selfTeam と異なり Neutral でもない）の index を返す。
        /// 対象が存在しない場合は -1。
        /// Neutral はジャングルオブジェクトのため、ミニオンは中立を攻撃しない。
        /// </summary>
        public static int ChooseTarget(
            Vector3 self,
            TeamId selfTeam,
            IReadOnlyList<TargetCandidate> candidates,
            float aggroRange)
        {
            int   bestIndex = -1;
            float bestDist  = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];

                // 同チームと Neutral は対象外
                if (c.Team == selfTeam || c.Team == TeamId.Neutral) continue;

                float dist = Vector3.Distance(self, c.Position);
                if (dist > aggroRange) continue;

                if (dist < bestDist)
                {
                    bestDist  = dist;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }
    }
}
