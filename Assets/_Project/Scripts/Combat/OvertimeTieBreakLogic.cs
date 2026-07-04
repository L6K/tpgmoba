namespace Enigma.Combat
{
    // オーバータイム減衰で両タイタンが同 tick で致死になった際のタイブレーク判定。
    // 純ロジック(UnityEngine 非依存)。反復順アーティファクト(Titan_Blue が常に先に
    // 死ぬ)を避けるため、盤面状況(タワー生存数→構造物残HP→コイン)で敗者を決める。
    public static class OvertimeTieBreakLogic
    {
        /// <summary>
        /// 同時致死になった側のうち、敗者(先に死んだ扱いにする側)の TeamId 相当値を返す。
        /// 優先順位: ①生存タワー数が少ない方 ②同数なら構造物残HP合計が低い方
        /// ③それも同値なら coinFallbackBlueLoses に従う。
        /// 戻り値は Enigma.Combat.TeamId の実値(Blue=0, Red=1)に合わせる。
        /// </summary>
        public static int PickLoserTeam(int blueTowersAlive, int redTowersAlive,
            float blueStructureHp, float redStructureHp, bool coinFallbackBlueLoses)
        {
            if (blueTowersAlive != redTowersAlive)
                return blueTowersAlive < redTowersAlive ? (int)TeamId.Blue : (int)TeamId.Red;

            if (blueStructureHp != redStructureHp)
                return blueStructureHp < redStructureHp ? (int)TeamId.Blue : (int)TeamId.Red;

            return coinFallbackBlueLoses ? (int)TeamId.Blue : (int)TeamId.Red;
        }
    }
}
