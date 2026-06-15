namespace Enigma.Character
{
    public enum BotMacroAction { Farm, Push, GroupForObjective, Retreat, Defend }

    public readonly struct BotMacroContext
    {
        public readonly float SelfHpFraction;
        public readonly int AlliesAlive;
        public readonly int EnemiesAlive;
        public readonly bool ObjectiveActiveOrSoon;
        public readonly float DistanceToObjective;
        public readonly bool AlliedMinionsPresent;
        public readonly bool UnderTowerThreat;

        public BotMacroContext(
            float selfHpFraction, int alliesAlive, int enemiesAlive,
            bool objectiveActiveOrSoon, float distanceToObjective,
            bool alliedMinionsPresent, bool underTowerThreat)
        {
            SelfHpFraction       = selfHpFraction;
            AlliesAlive          = alliesAlive;
            EnemiesAlive         = enemiesAlive;
            ObjectiveActiveOrSoon = objectiveActiveOrSoon;
            DistanceToObjective  = distanceToObjective;
            AlliedMinionsPresent = alliedMinionsPresent;
            UnderTowerThreat     = underTowerThreat;
        }
    }

    public static class BotMacroDecisionModel
    {
        public const float LowHpFraction      = 0.35f;
        public const float SafeHpFraction     = 0.45f;
        public const float ObjectiveJoinRange = 35f;

        public static BotMacroAction Decide(in BotMacroContext ctx)
        {
            if (ctx.SelfHpFraction < LowHpFraction &&
                (ctx.EnemiesAlive >= ctx.AlliesAlive || ctx.UnderTowerThreat))
            {
                return BotMacroAction.Retreat;
            }

            if (ctx.ObjectiveActiveOrSoon &&
                ctx.SelfHpFraction >= SafeHpFraction &&
                ctx.EnemiesAlive <= ctx.AlliesAlive &&
                ctx.DistanceToObjective <= ObjectiveJoinRange)
            {
                return BotMacroAction.GroupForObjective;
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
    }
}
