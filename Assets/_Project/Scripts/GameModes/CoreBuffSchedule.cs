using System.Collections.Generic;

namespace Enigma.GameModes
{
    // 中央オブジェクト(エニグマ・コア)の撃破回数に応じた報酬バフの付与内容を返す純関数。
    // CentralObjectiveDirector からロジックを抽出し、EditMode テストで検証可能にする。
    public static class CoreBuffSchedule
    {
        /// <summary>単一のバフ付与指示(種別・倍率・持続秒)。</summary>
        public readonly struct Grant
        {
            public readonly ObjectiveBuffType Type;
            public readonly float             Magnitude;
            public readonly float             Duration;

            public Grant(ObjectiveBuffType type, float magnitude, float duration)
            {
                Type      = type;
                Magnitude = magnitude;
                Duration  = duration;
            }
        }

        /// <summary>
        /// 撃破回数(1始まり)に応じた付与指示のリストを返す。
        /// 1回目: Damage 0.20 / 45s。
        /// 2回目以降: 上記 + MoveSpeed 0.15 / 45s。
        /// 3回目以降: Damage を 0.25 に強化 + Shield 150 / 10s + StructureDamage 1.0(=対構造物ダメ×2) / 45s。
        /// </summary>
        public static IReadOnlyList<Grant> ForKillCount(int killCount)
        {
            var grants = new List<Grant>(4);

            grants.Add(new Grant(ObjectiveBuffType.Damage, killCount >= 3 ? 0.25f : 0.20f, 45f));

            if (killCount >= 2)
                grants.Add(new Grant(ObjectiveBuffType.MoveSpeed, 0.15f, 45f));

            if (killCount >= 3)
            {
                grants.Add(new Grant(ObjectiveBuffType.Shield, 150f, 10f));
                grants.Add(new Grant(ObjectiveBuffType.StructureDamage, 1.0f, 45f));
            }

            return grants;
        }
    }
}
