using System;

namespace Enigma.Vfx
{
    public readonly struct VfxColor
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;

        public VfxColor(float r, float g, float b)
        {
            R = Clamp01(r);
            G = Clamp01(g);
            B = Clamp01(b);
        }

        public static VfxColor Lerp(VfxColor a, VfxColor b, float t)
        {
            float s = Clamp01(t);
            return new VfxColor(
                a.R + (b.R - a.R) * s,
                a.G + (b.G - a.G) * s,
                a.B + (b.B - a.B) * s);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }

    public enum ChampionVfx
    {
        Zeph,
        Garon,
        Veil,
        Rin,
        Nova,
        Thorne
    }

    public readonly struct AttackVfxProfile
    {
        public readonly ChampionVfx Id;
        public readonly VfxColor Primary;
        public readonly VfxColor Secondary;
        public readonly float BeamWidthStart;
        public readonly float BeamWidthEnd;
        public readonly float TrailLingerSeconds;
        public readonly float ImpactScale;
        public readonly float EmissionIntensity;

        public AttackVfxProfile(
            ChampionVfx id,
            VfxColor primary,
            VfxColor secondary,
            float beamWidthStart,
            float beamWidthEnd,
            float trailLingerSeconds,
            float impactScale,
            float emissionIntensity)
        {
            Id = id;
            Primary = primary;
            Secondary = secondary;
            BeamWidthStart = beamWidthStart;
            BeamWidthEnd = beamWidthEnd;
            TrailLingerSeconds = trailLingerSeconds;
            ImpactScale = impactScale;
            EmissionIntensity = emissionIntensity;
        }
    }

    public static class AttackVfxProfiles
    {
        public static AttackVfxProfile For(ChampionVfx id)
        {
            switch (id)
            {
                case ChampionVfx.Garon:
                    return new AttackVfxProfile(
                        ChampionVfx.Garon,
                        new VfxColor(0.70f, 0.75f, 0.85f),
                        new VfxColor(1.00f, 0.50f, 0.10f),
                        0.40f,
                        0.90f,
                        0.20f,
                        1.40f,
                        2.00f);
                case ChampionVfx.Veil:
                    return new AttackVfxProfile(
                        ChampionVfx.Veil,
                        new VfxColor(0.55f, 0.20f, 0.95f),
                        new VfxColor(0.30f, 0.00f, 0.40f),
                        0.18f,
                        0.45f,
                        0.45f,
                        3.00f,
                        3.00f);
                case ChampionVfx.Rin:
                    return new AttackVfxProfile(
                        ChampionVfx.Rin,
                        new VfxColor(1.00f, 0.85f, 0.30f),
                        new VfxColor(0.40f, 0.90f, 1.00f),
                        0.12f,
                        0.30f,
                        0.25f,
                        0.80f,
                        2.60f);
                case ChampionVfx.Nova:
                    return new AttackVfxProfile(
                        ChampionVfx.Nova,
                        new VfxColor(0.50f, 0.95f, 1.00f),
                        new VfxColor(0.95f, 0.98f, 1.00f),
                        0.30f,
                        0.55f,
                        0.30f,
                        1.10f,
                        4.00f);
                case ChampionVfx.Thorne:
                    return new AttackVfxProfile(
                        ChampionVfx.Thorne,
                        new VfxColor(1.00f, 0.15f, 0.10f),
                        new VfxColor(1.00f, 0.55f, 0.20f),
                        0.35f,
                        0.80f,
                        0.22f,
                        1.30f,
                        3.20f);
                case ChampionVfx.Zeph:
                default:
                    return new AttackVfxProfile(
                        ChampionVfx.Zeph,
                        new VfxColor(0.10f, 0.90f, 1.00f),
                        new VfxColor(0.90f, 0.20f, 1.00f),
                        0.25f,
                        0.60f,
                        0.35f,
                        1.00f,
                        3.50f);
            }
        }

        public static ChampionVfx Parse(string key)
        {
            if (string.IsNullOrEmpty(key)) return ChampionVfx.Zeph;
            if (Enum.TryParse(key, true, out ChampionVfx id) && Enum.IsDefined(typeof(ChampionVfx), id)) return id;
            return ChampionVfx.Zeph;
        }
    }
}
