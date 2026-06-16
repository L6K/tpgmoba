using System.Collections.Generic;

namespace Enigma.Data
{
    /// <summary>レリック1種の表示メタデータ + 効果定義。</summary>
    public readonly struct RelicInfo
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly RelicEffect Effect;
        public readonly float Magnitude;

        public RelicInfo(string id, string displayName, string description,
            RelicEffect effect, float magnitude)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Effect = effect;
            Magnitude = magnitude;
        }

        public Relic ToRelic() => new Relic(Id, Effect, Magnitude);
    }

    /// <summary>
    /// 選択可能なレリックの静的カタログ。現状は試合開始時に適用できる3効果
    /// （最大HP / 開始シールド / クールダウン短縮）のみを提供する。
    /// MoveSpeedOnKill / NeutralDamage はキル・ダメージ系フックが必要なため未収録。
    /// </summary>
    public static class RelicCatalog
    {
        public static IReadOnlyList<RelicInfo> All { get; } = new List<RelicInfo>
        {
            new RelicInfo("relic_vital_mirror",   "守りの古鏡",     "最大HP +150",     RelicEffect.MaxHpBonus,        150f),
            new RelicInfo("relic_giant_heart",    "巨人の心臓",     "最大HP +300",     RelicEffect.MaxHpBonus,        300f),
            new RelicInfo("relic_aegis_charm",    "不屈の盾札",     "開始時シールド +200", RelicEffect.StartShield,       200f),
            new RelicInfo("relic_bulwark_seal",   "城塞の封印",     "開始時シールド +350", RelicEffect.StartShield,       350f),
            new RelicInfo("relic_haste_glass",    "加速の砂時計",   "スキルCD -15%",   RelicEffect.CooldownReduction, 0.15f),
            new RelicInfo("relic_chrono_compass", "時詠みの羅針盤", "スキルCD -25%",   RelicEffect.CooldownReduction, 0.25f),
        };

        /// <summary>RelicLoadoutModel に渡す Relic 一覧（メタデータを落とした効果のみ）。</summary>
        public static IReadOnlyList<Relic> Relics()
        {
            var list = new List<Relic>(All.Count);
            for (int i = 0; i < All.Count; i++)
                list.Add(All[i].ToRelic());
            return list;
        }

        public static bool TryGet(string id, out RelicInfo info)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Id == id)
                {
                    info = All[i];
                    return true;
                }
            }
            info = default;
            return false;
        }
    }
}
