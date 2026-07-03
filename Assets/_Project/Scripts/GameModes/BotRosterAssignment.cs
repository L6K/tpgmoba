using System;
using System.Collections.Generic;

namespace Enigma.GameMode
{
    /// <summary>
    /// 3v3 フルボット編成のキャラ割当（純粋ロジック、UnityEngine 非依存）。
    /// プレイヤーのピックを除いた全キャラを決定的にシャッフルし、ボット5体分を返す。
    /// シャッフルは System.Random(seed) で行うため同一シードなら結果が再現する。
    /// </summary>
    public static class BotRosterAssignment
    {
        public const int BotCount = 5;

        /// <summary>
        /// allIds からプレイヤーピックを除外し、決定的にシャッフルして botCount 体分の ID を返す。
        /// 候補が botCount に満たない場合のみ重複（繰り返し）を許可して埋める。
        /// 現状は全6キャラ-プレイヤー1=5でちょうど重複なしになる。
        /// </summary>
        public static string[] Assign(IReadOnlyList<string> allIds, string playerPick, int seed, int botCount = BotCount)
        {
            if (allIds == null) throw new ArgumentNullException(nameof(allIds));
            if (botCount < 0) throw new ArgumentOutOfRangeException(nameof(botCount));

            // プレイヤーピックを除いた候補（null/空・重複は除く）。元の並び順を保持する。
            var pool = new List<string>(allIds.Count);
            var seen = new HashSet<string>();
            foreach (var id in allIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (id == playerPick) continue;
                if (!seen.Add(id)) continue;
                pool.Add(id);
            }

            Shuffle(pool, seed);

            var result = new string[botCount];
            if (pool.Count == 0)
            {
                // 候補ゼロ（全候補がプレイヤーピックと一致など）の場合の保険。
                // プレイヤーピックがあればそれで、無ければ空文字で埋める。
                for (int i = 0; i < botCount; i++)
                    result[i] = playerPick ?? string.Empty;
                return result;
            }

            for (int i = 0; i < botCount; i++)
                result[i] = pool[i % pool.Count]; // 候補不足時のみ周回（繰り返し許可）
            return result;
        }

        /// <summary>
        /// バランスシム用: チームごとに独立してシャッフルし、重複なしで割当てる。
        /// 通常プレイの Assign（単一プールから全ボットへ重複なし割当）と異なり、
        /// 青チーム・赤チーム内では重複させないが、青赤間の重複は許可する
        /// （同キャラのミラー対戦を許し、全キャラの出場機会を最大化するため）。
        /// 同一 seed から teamCount 側は seed、対戦相手側は seed+1 でずらして相関を避ける。
        /// </summary>
        public static string[] AssignPerTeam(IReadOnlyList<string> allIds, int seed, int teamSize)
        {
            var blue = AssignTeam(allIds, seed, teamSize);
            var red = AssignTeam(allIds, seed + 1, teamSize);

            var result = new string[teamSize * 2];
            for (int i = 0; i < teamSize; i++) result[i] = blue[i];
            for (int i = 0; i < teamSize; i++) result[teamSize + i] = red[i];
            return result;
        }

        private static string[] AssignTeam(IReadOnlyList<string> allIds, int seed, int teamSize)
        {
            var pool = new List<string>(allIds.Count);
            var seen = new HashSet<string>();
            foreach (var id in allIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (!seen.Add(id)) continue;
                pool.Add(id);
            }

            Shuffle(pool, seed);

            var result = new string[teamSize];
            if (pool.Count == 0)
            {
                for (int i = 0; i < teamSize; i++) result[i] = string.Empty;
                return result;
            }

            for (int i = 0; i < teamSize; i++)
                result[i] = pool[i % pool.Count];
            return result;
        }

        // Fisher-Yates シャッフル。System.Random(seed) で決定的に並べ替える。
        private static void Shuffle(List<string> list, int seed)
        {
            var rng = new Random(seed);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
