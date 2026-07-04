namespace Enigma.Character
{
    public enum BotMacroAction { Farm, Push, GroupForObjective, Retreat, Defend, CloseOutSiege }

    public readonly struct BotMacroContext
    {
        public readonly float SelfHpFraction;
        public readonly int AlliesAlive;
        public readonly int EnemiesAlive;
        public readonly bool ObjectiveActiveOrSoon;
        public readonly float DistanceToObjective;
        public readonly bool AlliedMinionsPresent;
        public readonly bool UnderTowerThreat;
        public readonly bool OwnTowerUnderAttack;
        public readonly float DistanceToThreatenedTower;
        // ボス(エニグマコア)討伐コミット判定用。ボス不在/未交戦なら 1 のまま扱ってよい
        // (押し切りラインの比較にのみ使うため、非交戦時に false 側へ倒れても影響しない)。
        public readonly float BossHpFraction;
        // 自チーム優勢の判定に使うタワー生存数(自陣/敵陣、生存中のみカウント)。
        public readonly int OwnTowersAlive;
        public readonly int EnemyTowersAlive;
        public readonly int OwnTowersMax;
        public readonly int EnemyTowersMax;

        public BotMacroContext(
            float selfHpFraction, int alliesAlive, int enemiesAlive,
            bool objectiveActiveOrSoon, float distanceToObjective,
            bool alliedMinionsPresent, bool underTowerThreat,
            bool ownTowerUnderAttack, float distanceToThreatenedTower,
            float bossHpFraction = 1f,
            int ownTowersAlive = 0, int enemyTowersAlive = 0,
            int ownTowersMax = 0, int enemyTowersMax = 0)
        {
            SelfHpFraction       = selfHpFraction;
            AlliesAlive          = alliesAlive;
            EnemiesAlive         = enemiesAlive;
            ObjectiveActiveOrSoon = objectiveActiveOrSoon;
            DistanceToObjective  = distanceToObjective;
            AlliedMinionsPresent = alliedMinionsPresent;
            UnderTowerThreat     = underTowerThreat;
            OwnTowerUnderAttack  = ownTowerUnderAttack;
            DistanceToThreatenedTower = distanceToThreatenedTower;
            BossHpFraction       = bossHpFraction;
            OwnTowersAlive       = ownTowersAlive;
            EnemyTowersAlive     = enemyTowersAlive;
            OwnTowersMax         = ownTowersMax;
            EnemyTowersMax       = enemyTowersMax;
        }
    }

    public static class BotMacroDecisionModel
    {
        public const float LowHpFraction      = 0.35f;
        public const float SafeHpFraction     = 0.45f;
        // マップ半径由来の距離しきい値。M-0(平面1.4倍拡張)に合わせて更新(35→49, 45→63)。
        public const float ObjectiveJoinRange = 49f;
        public const float DefendJoinRange    = 63f;

        // ボスの残HPがこの割合を下回ったら「押し切りライン」とみなし、敵接近による
        // ボス討伐コミットの離脱を止める(GroupForObjective 自体はこれまで通り継続、
        // ApplyMacroOverride 側の交戦継続判定に使う)。300試合実測で1000→533離脱が
        // 頻発したため、533/1000=0.533 より高い 0.6 を境界にして早期コミットさせる。
        public const float BossCommitHpFraction = 0.6f;

        // 「自チーム優勢」とみなすタワー本数差(自陣が失った本数 = EnemyTowersDestroyed 相当)。
        // 敵タワー撃破数 >= 自陣被撃破数 + 1 を「タワー差」で表すと
        // (EnemyTowersMax-EnemyTowersAlive) - (OwnTowersMax-OwnTowersAlive) >= 1。
        public const int CloseOutTowerAdvantage = 1;

        public static BotMacroAction Decide(in BotMacroContext ctx)
        {
            if (ctx.SelfHpFraction < LowHpFraction &&
                (ctx.EnemiesAlive >= ctx.AlliesAlive || ctx.UnderTowerThreat))
            {
                return BotMacroAction.Retreat;
            }

            if (ctx.OwnTowerUnderAttack &&
                ctx.SelfHpFraction >= SafeHpFraction &&
                ctx.DistanceToThreatenedTower <= DefendJoinRange)
            {
                return BotMacroAction.Defend;
            }

            if (ctx.ObjectiveActiveOrSoon &&
                ctx.SelfHpFraction >= SafeHpFraction &&
                ctx.EnemiesAlive <= ctx.AlliesAlive &&
                ctx.DistanceToObjective <= ObjectiveJoinRange)
            {
                return BotMacroAction.GroupForObjective;
            }

            if (IsTeamAhead(ctx) &&
                ctx.SelfHpFraction >= SafeHpFraction)
            {
                return BotMacroAction.CloseOutSiege;
            }

            if (ctx.AlliesAlive > ctx.EnemiesAlive &&
                ctx.SelfHpFraction >= SafeHpFraction &&
                ctx.AlliedMinionsPresent)
            {
                return BotMacroAction.Push;
            }

            if (ctx.UnderTowerThreat && !ctx.AlliedMinionsPresent)
                return BotMacroAction.Defend;

            return BotMacroAction.Farm;
        }

        // 自チーム優勢=敵の破壊済みタワー本数が自陣の破壊済み本数より CloseOutTowerAdvantage 本以上多い。
        // Max が両方 0(未供給/取得前)なら判定不能として false を返す。
        private static bool IsTeamAhead(in BotMacroContext ctx)
        {
            if (ctx.OwnTowersMax <= 0 && ctx.EnemyTowersMax <= 0) return false;

            int ownLost   = ctx.OwnTowersMax   - ctx.OwnTowersAlive;
            int enemyLost = ctx.EnemyTowersMax - ctx.EnemyTowersAlive;
            return enemyLost >= ownLost + CloseOutTowerAdvantage;
        }
    }
}
