namespace Enigma.Character
{
    public enum LaneBotState { Push, Engage, Retreat }

    public enum LaneThreatKind { None, Champion, Minion, Tower }

    // 経路上の進行方向。Push は青ベース方向（前進）、Retreat は赤ベース方向（後退）。
    public enum LaneMove { Stop, Forward, Backward }

    /// <summary>
    /// 毎ティックの知覚スナップショット（UnityEngine 非依存）。距離はワールド距離。
    /// </summary>
    public readonly struct LaneBotPerception
    {
        // 自 HP 比率（0..1）
        public readonly float HpRatio;

        // 最寄りの敵までの距離とその種別。敵がいなければ NearestEnemyKind=None
        public readonly float NearestEnemyDistance;
        public readonly LaneThreatKind NearestEnemyKind;

        // 自分を直近で攻撃してきた敵チャンピオンが存在し、知覚範囲内にいるか
        public readonly bool HasAttackerChampion;
        public readonly float AttackerChampionDistance;

        // 敵タワーまでの距離（タワーが知覚範囲外なら float.MaxValue を渡す）
        public readonly float EnemyTowerDistance;

        // 味方ミニオンが近くにいるか（タワーダイブの可否判定に使う）
        public readonly bool AllyMinionNearby;

        public LaneBotPerception(
            float hpRatio,
            float nearestEnemyDistance,
            LaneThreatKind nearestEnemyKind,
            bool hasAttackerChampion,
            float attackerChampionDistance,
            float enemyTowerDistance,
            bool allyMinionNearby)
        {
            HpRatio                  = hpRatio;
            NearestEnemyDistance     = nearestEnemyDistance;
            NearestEnemyKind         = nearestEnemyKind;
            HasAttackerChampion      = hasAttackerChampion;
            AttackerChampionDistance = attackerChampionDistance;
            EnemyTowerDistance       = enemyTowerDistance;
            AllyMinionNearby         = allyMinionNearby;
        }
    }

    /// <summary>毎ティックの出力。MonoBehaviour 側はこれに従って移動・攻撃する。</summary>
    public readonly struct LaneBotDecision
    {
        public readonly LaneBotState State;
        public readonly LaneMove Move;

        // true のとき AttackTarget があり、射程内で攻撃可能。
        // ターゲットが攻撃してきたチャンピオンなら PreferAttacker=true。
        public readonly bool HasAttackTarget;
        public readonly bool TargetIsAttackerChampion;

        public LaneBotDecision(
            LaneBotState state, LaneMove move,
            bool hasAttackTarget, bool targetIsAttackerChampion)
        {
            State                    = state;
            Move                     = move;
            HasAttackTarget          = hasAttackTarget;
            TargetIsAttackerChampion = targetIsAttackerChampion;
        }
    }

    /// <summary>
    /// レーナー Bot の判断ロジック（純粋関数）。UnityEngine に依存しない。
    /// MonoBehaviour 側は知覚を集めて Decide を呼び、現在 State を更新する。
    /// </summary>
    public static class LaneBotLogic
    {
        public const float AggroRange  = 14f;
        public const float AttackRange = 11f;
        public const float TowerZone   = 12f;
        // ゾーン縁での待機マージン(出入りのチャタリング防止)
        public const float TowerZoneHoldMargin = 2f;

        public const float RetreatHpRatio = 0.30f;
        public const float RecoverHpRatio = 0.95f;

        /// <summary>
        /// 現在の State と知覚から次の State と行動を決める。
        /// State は呼び出し側が返り値の State で更新する（このメソッドは副作用を持たない）。
        /// </summary>
        public static LaneBotDecision Decide(LaneBotState current, in LaneBotPerception p)
        {
            // Retreat は HP 比率のみで最優先に評価する（どの状態からでも割り込む）。
            // 回復しきるまで Retreat を維持し、回復後に Push へ戻す。
            if (current == LaneBotState.Retreat)
            {
                if (p.HpRatio > RecoverHpRatio)
                    return Push(p);
                return new LaneBotDecision(LaneBotState.Retreat, LaneMove.Backward, false, false);
            }

            if (p.HpRatio < RetreatHpRatio)
                return new LaneBotDecision(LaneBotState.Retreat, LaneMove.Backward, false, false);

            // タワーゾーン規律(Push/Engage 共通): 味方ミニオンが近くにいない限り、
            // ゾーン内なら後退して出る。ゾーン縁では前進せず待機(タワー砲撃のタンク防止)
            if (!p.AllyMinionNearby)
            {
                if (p.EnemyTowerDistance <= TowerZone)
                    return new LaneBotDecision(LaneBotState.Push, LaneMove.Backward, false, false);
                if (p.EnemyTowerDistance <= TowerZone + TowerZoneHoldMargin)
                    return new LaneBotDecision(LaneBotState.Push, LaneMove.Stop, false, false);
            }

            if (current == LaneBotState.Engage)
                return Engage(p);

            // current == Push
            return Push(p);
        }

        private static LaneBotDecision Push(in LaneBotPerception p)
        {
            // 敵が aggro 範囲内なら Engage へ遷移してその場の交戦判断を行う
            if (HasEnemyInRange(p, AggroRange))
                return Engage(p);

            // 敵不在: 経路を前進
            return new LaneBotDecision(LaneBotState.Push, LaneMove.Forward, false, false);
        }

        private static LaneBotDecision Engage(in LaneBotPerception p)
        {
            // 交戦対象が消えたら Push へ戻る
            if (!HasEnemyInRange(p, AggroRange))
                return new LaneBotDecision(LaneBotState.Push, LaneMove.Forward, false, false);

            // ターゲット選択: 攻撃してきた敵チャンピオン優先、無ければ最寄りの敵。
            bool targetIsAttacker = p.HasAttackerChampion;
            float targetDist = targetIsAttacker
                ? p.AttackerChampionDistance
                : p.NearestEnemyDistance;

            // タワーゾーン規律は Decide 冒頭で処理済み(ここに来る時点でゾーン外 or 味方ミニオンあり)

            // 射程内なら停止して攻撃、射程外なら接近
            if (targetDist <= AttackRange)
                return new LaneBotDecision(LaneBotState.Engage, LaneMove.Stop, true, targetIsAttacker);

            return new LaneBotDecision(LaneBotState.Engage, LaneMove.Forward, false, targetIsAttacker);
        }

        private static bool HasEnemyInRange(in LaneBotPerception p, float range)
        {
            if (p.HasAttackerChampion && p.AttackerChampionDistance <= range)
                return true;
            return p.NearestEnemyKind != LaneThreatKind.None && p.NearestEnemyDistance <= range;
        }
    }
}
