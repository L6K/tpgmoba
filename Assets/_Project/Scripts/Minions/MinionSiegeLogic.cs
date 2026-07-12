namespace Enigma.Minion
{
    // ミニオンの対構造物ダメージ倍率(純ロジック、UnityEngine 非依存)。
    // Bot 戦の測定でボトルネックが「タワーが落ちない」(Pre-OT陥落11%)と判明したため、
    // ミニオンの対構造物ダメージを直接引き上げる是正(2026-07-12 ユーザー承認)。
    // MeleeSiegeLogic(近接チャンピオン用、1.5倍)とは別軸。ミニオンは全射程共通で3.0倍。
    public static class MinionSiegeLogic
    {
        // ミニオンの対構造物ダメージ倍率
        public const float StructureMultiplier = 3.0f;

        /// <summary>対象が構造物(タワー/タイタン)かどうかで倍率を返す(非構造物は等倍)。</summary>
        public static float Multiplier(bool targetIsStructure)
        {
            return targetIsStructure ? StructureMultiplier : 1f;
        }
    }
}
