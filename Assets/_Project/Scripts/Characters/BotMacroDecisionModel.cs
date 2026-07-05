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
        // 自チームキル数 - 敵チームキル数。タワー戦線が膠着していてもキルで優勢な場合を
        // 閉幕トリガーに反映するため（タワー射程強化でタワー損失差が発生しにくくなった対策）。
        public readonly int TeamKillLead;

        public BotMacroContext(
            float selfHpFraction, int alliesAlive, int enemiesAlive,
            bool objectiveActiveOrSoon, float distanceToObjective,
            bool alliedMinionsPresent, bool underTowerThreat,
            bool ownTowerUnderAttack, float distanceToThreatenedTower,
            float bossHpFraction = 1f,
            int ownTowersAlive = 0, int enemyTowersAlive = 0,
            int ownTowersMax = 0, int enemyTowersMax = 0,
            int teamKillLead = 0)
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
            TeamKillLead         = teamKillLead;
        }
    }

    public static class BotMacroDecisionModel
    {
        public const float LowHpFraction      = 0.35f;
        public const float SafeHpFraction     = 0.45f;
        // マップ半径由来の距離しきい値。M-0(平面1.4倍拡張)に合わせて更新(35→49, 45→63)。
        // ObjectiveJoinRange は 49→63 の再緩和がコア圏をマップ中央部ほぼ全域まで拡張し、
        // 頭数条件緩和(allies>=enemies-1)・膠着時集合と重なって全Botが90秒以降 GroupForObjective に
        // 恒久ロックする実測(1試合ライブ観測+JSONL)を招いたため 49 に巻き戻す。
        public const float ObjectiveJoinRange = 49f;
        public const float DefendJoinRange    = 63f;
        // 膠着時集合(IsStalemate)は objective active なら常時発動していたため頭数条件と合わせて
        // 恒久ロックの主因になった。コア至近の試合のみ発動するよう距離ゲートを追加する。
        public const float StalemateGroupRange = 40f;

        // ボスの残HPがこの割合を下回ったら「押し切りライン」とみなし、敵接近による
        // ボス討伐コミットの離脱を止める(GroupForObjective 自体はこれまで通り継続、
        // ApplyMacroOverride 側の交戦継続判定に使う)。300試合実測で1000→533離脱が
        // 頻発したため、533/1000=0.533 より高い 0.6 を境界にして早期コミットさせる。
        public const float BossCommitHpFraction = 0.6f;

        // 「自チーム優勢」とみなすタワー本数差(自陣が失った本数 = EnemyTowersDestroyed 相当)。
        // 敵タワー撃破数 >= 自陣被撃破数 + 1 を「タワー差」で表すと
        // (EnemyTowersMax-EnemyTowersAlive) - (OwnTowersMax-OwnTowersAlive) >= 1。
        public const int CloseOutTowerAdvantage = 1;

        // 「自チーム優勢」とみなすキル差(自チームキル数-敵チームキル数)。タワー射程16強化で
        // タワーが OT まで落ちなくなり CloseOutTowerAdvantage が一度も成立しない試合が
        // あったため、タワー戦線と独立にキル優勢だけでも押し切りへ移行できるようにする。
        public const int CloseOutKillLead = 3;

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

            // 頭数条件は「1人差まで参加可」への緩和が、膠着時集合と重なって全Bot恒久ロックを
            // 招いたため「同数以上」に巻き戻す。
            if (ctx.ObjectiveActiveOrSoon &&
                ctx.SelfHpFraction >= SafeHpFraction &&
                ctx.AlliesAlive >= ctx.EnemiesAlive &&
                ctx.DistanceToObjective <= ObjectiveJoinRange)
            {
                return BotMacroAction.GroupForObjective;
            }

            // 膠着時のコア集合: キル差・タワー損失差とも均衡していて動くきっかけが無い試合でも、
            // オブジェクティブが有効/まもなくなら押し切り不成立でも集合させる。
            // ただし距離ゲート無しでは「コアが常時Activeで頭数条件も常に真」の試合において
            // 遠方のBotまで恒久的に GroupForObjective へ張り付き、ファーム/レーンが全停止する
            // (実測: 開始~100秒以降全6Botロック)。コア至近の試合のみに限定する。
            if (ctx.ObjectiveActiveOrSoon &&
                ctx.SelfHpFraction >= SafeHpFraction &&
                ctx.DistanceToObjective <= StalemateGroupRange &&
                IsStalemate(ctx))
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

        // 自チーム優勢 = 「敵の破壊済みタワー本数が自陣より CloseOutTowerAdvantage 本以上多い」
        // または「キル差が CloseOutKillLead 以上」。タワーが強化されて OT まで落ちない試合でも
        // キル優勢だけで押し切りへ移行できるようにする(OR条件、どちらかで十分)。
        private static bool IsTeamAhead(in BotMacroContext ctx)
        {
            if (ctx.TeamKillLead >= CloseOutKillLead) return true;

            if (ctx.OwnTowersMax <= 0 && ctx.EnemyTowersMax <= 0) return false;

            int ownLost   = ctx.OwnTowersMax   - ctx.OwnTowersAlive;
            int enemyLost = ctx.EnemyTowersMax - ctx.EnemyTowersAlive;
            return enemyLost >= ownLost + CloseOutTowerAdvantage;
        }

        // 膠着 = キル差0 かつタワー損失差0。タワーデータ未供給(Max<=0 かつ Max<=0)は
        // IsTeamAhead 同様「判定不能」として除外する(0本ずつ扱いにすると未配線ケースが
        // すべて膠着扱いになり、頭数条件による絞り込みが無意味になってしまうため)。
        private static bool IsStalemate(in BotMacroContext ctx)
        {
            if (ctx.TeamKillLead != 0) return false;
            if (ctx.OwnTowersMax <= 0 && ctx.EnemyTowersMax <= 0) return false;

            int ownLost   = ctx.OwnTowersMax   - ctx.OwnTowersAlive;
            int enemyLost = ctx.EnemyTowersMax - ctx.EnemyTowersAlive;
            return ownLost == enemyLost;
        }
    }
}
