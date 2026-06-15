using Enigma.Vfx;
using NUnit.Framework;

namespace Enigma.Tests
{
    public sealed class AttackVfxProfileTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void VfxColor_Constructor_ClampsChannels()
        {
            var color = new VfxColor(-0.5f, 0.5f, 1.5f);

            Assert.AreEqual(0f, color.R, Tolerance);
            Assert.AreEqual(0.5f, color.G, Tolerance);
            Assert.AreEqual(1f, color.B, Tolerance);
        }

        [Test]
        public void VfxColor_Lerp_ReturnsEndpoints()
        {
            var a = new VfxColor(0.1f, 0.2f, 0.3f);
            var b = new VfxColor(0.9f, 0.8f, 0.7f);

            VfxColor low = VfxColor.Lerp(a, b, -1f);
            VfxColor high = VfxColor.Lerp(a, b, 2f);

            Assert.AreEqual(a.R, low.R, Tolerance);
            Assert.AreEqual(a.G, low.G, Tolerance);
            Assert.AreEqual(a.B, low.B, Tolerance);
            Assert.AreEqual(b.R, high.R, Tolerance);
            Assert.AreEqual(b.G, high.G, Tolerance);
            Assert.AreEqual(b.B, high.B, Tolerance);
        }

        [Test]
        public void VfxColor_Lerp_ReturnsMidpoint()
        {
            var a = new VfxColor(0f, 0.2f, 0.4f);
            var b = new VfxColor(1f, 0.8f, 1f);

            VfxColor color = VfxColor.Lerp(a, b, 0.5f);

            Assert.AreEqual(0.5f, color.R, Tolerance);
            Assert.AreEqual(0.5f, color.G, Tolerance);
            Assert.AreEqual(0.7f, color.B, Tolerance);
        }

        [Test]
        public void For_Zeph_ReturnsConfiguredProfile()
        {
            AttackVfxProfile profile = AttackVfxProfiles.For(ChampionVfx.Zeph);

            Assert.AreEqual(ChampionVfx.Zeph, profile.Id);
            Assert.AreEqual(0.10f, profile.Primary.R, Tolerance);
            Assert.AreEqual(0.90f, profile.Primary.G, Tolerance);
            Assert.AreEqual(1.00f, profile.Primary.B, Tolerance);
            Assert.AreEqual(0.90f, profile.Secondary.R, Tolerance);
            Assert.AreEqual(0.20f, profile.Secondary.G, Tolerance);
            Assert.AreEqual(1.00f, profile.Secondary.B, Tolerance);
            Assert.AreEqual(0.25f, profile.BeamWidthStart, Tolerance);
            Assert.AreEqual(0.60f, profile.BeamWidthEnd, Tolerance);
            Assert.AreEqual(0.35f, profile.TrailLingerSeconds, Tolerance);
            Assert.AreEqual(1.00f, profile.ImpactScale, Tolerance);
            Assert.AreEqual(3.50f, profile.EmissionIntensity, Tolerance);
        }

        [Test]
        public void For_AllKnownChampions_ReturnsMatchingIdAndGrowingWidth()
        {
            ChampionVfx[] ids =
            {
                ChampionVfx.Zeph,
                ChampionVfx.Garon,
                ChampionVfx.Veil,
                ChampionVfx.Rin,
                ChampionVfx.Nova,
                ChampionVfx.Thorne
            };

            foreach (ChampionVfx id in ids)
            {
                AttackVfxProfile profile = AttackVfxProfiles.For(id);

                Assert.AreEqual(id, profile.Id);
                Assert.Greater(profile.BeamWidthEnd, profile.BeamWidthStart);
            }
        }

        [Test]
        public void For_UnknownChampion_ReturnsZeph()
        {
            AttackVfxProfile profile = AttackVfxProfiles.For((ChampionVfx)999);

            Assert.AreEqual(ChampionVfx.Zeph, profile.Id);
        }

        [Test]
        public void Parse_KnownKeys_IgnoresCase()
        {
            Assert.AreEqual(ChampionVfx.Garon, AttackVfxProfiles.Parse("GARON"));
            Assert.AreEqual(ChampionVfx.Garon, AttackVfxProfiles.Parse("garon"));
        }

        [Test]
        public void Parse_UnknownOrEmptyKeys_ReturnsZeph()
        {
            Assert.AreEqual(ChampionVfx.Zeph, AttackVfxProfiles.Parse("zzz"));
            Assert.AreEqual(ChampionVfx.Zeph, AttackVfxProfiles.Parse(null));
            Assert.AreEqual(ChampionVfx.Zeph, AttackVfxProfiles.Parse(string.Empty));
        }
    }
}
