using System.Collections.Generic;
using NUnit.Framework;
using Enigma.Data;

namespace Enigma.Tests
{
    public sealed class RelicCatalogTests
    {
        [Test]
        public void All_HasEntries_WithUniqueIds()
        {
            var ids = new HashSet<string>();
            foreach (var info in RelicCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(info.Id));
                Assert.IsTrue(ids.Add(info.Id), $"duplicate id: {info.Id}");
            }
            Assert.Greater(RelicCatalog.All.Count, 0);
        }

        [Test]
        public void Relics_CountMatchesAll()
        {
            Assert.AreEqual(RelicCatalog.All.Count, RelicCatalog.Relics().Count);
        }

        [Test]
        public void TryGet_KnownId_ReturnsInfo()
        {
            Assert.IsTrue(RelicCatalog.TryGet("relic_vital_mirror", out var info));
            Assert.AreEqual(RelicEffect.MaxHpBonus, info.Effect);
        }

        [Test]
        public void TryGet_UnknownId_ReturnsFalse()
        {
            Assert.IsFalse(RelicCatalog.TryGet("nope", out _));
        }

        [Test]
        public void All_ExcludesUnwiredNeutralDamage()
        {
            // 現スライスは開始時3効果 + キル時加速のみ収録。NeutralDamage は未配線。
            foreach (var info in RelicCatalog.All)
            {
                bool ok = info.Effect == RelicEffect.MaxHpBonus
                       || info.Effect == RelicEffect.StartShield
                       || info.Effect == RelicEffect.CooldownReduction
                       || info.Effect == RelicEffect.MoveSpeedOnKill;
                Assert.IsTrue(ok, $"unexpected effect in catalog: {info.Effect}");
            }
        }

        [Test]
        public void AggregateThroughLoadout_SumsMagnitudes()
        {
            var model = new RelicLoadoutModel(RelicCatalog.Relics(), 3);
            model.TrySelect("relic_vital_mirror"); // MaxHp +150
            model.TrySelect("relic_giant_heart");  // MaxHp +300
            var effects = model.AggregateEffects();
            Assert.AreEqual(450f, effects[RelicEffect.MaxHpBonus], 0.001f);
        }
    }
}
