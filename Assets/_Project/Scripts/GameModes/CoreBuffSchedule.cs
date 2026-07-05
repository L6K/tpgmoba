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
        /// 1回目: Damage 0.25 / 60s。
        /// 2回目以降: 上記 + MoveSpeed 0.15 / 60s。
        /// 3回目以降: Damage を 0.30 に強化 + Shield 150 / 10s(不変) + StructureDamage 1.0(=対構造物ダメ×2) / 60s。
        /// 誰もコアを倒さない問題への対策(A1凍結値の変更=ユーザー承認済)で 1回目/3回目以降の Damage を
        /// 引き上げ、持続を全般的に45s→60sへ延長した(Shield の実付与10sのみ不変)。
        /// </summary>
        public static IReadOnlyList<Grant> ForKillCount(int killCount)
        {
            var grants = new List<Grant>(4);

            grants.Add(new Grant(ObjectiveBuffType.Damage, killCount >= 3 ? 0.30f : 0.25f, 60f));

            if (killCount >= 2)
                grants.Add(new Grant(ObjectiveBuffType.MoveSpeed, 0.15f, 60f));

            if (killCount >= 3)
            {
                grants.Add(new Grant(ObjectiveBuffType.Shield, 150f, 10f));
                grants.Add(new Grant(ObjectiveBuffType.StructureDamage, 1.0f, 60f));
            }

            return grants;
        }
    }
}
