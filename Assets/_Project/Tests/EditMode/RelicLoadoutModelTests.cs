using System.Collections.Generic;
using Enigma.Data;
using NUnit.Framework;

namespace Enigma.Tests.EditMode
{
    public sealed class RelicLoadoutModelTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void TrySelect_AllowsUpToMaxSlots()
        {
            var model = new RelicLoadoutModel(Catalog(), maxSlots: 3);

            Assert.IsTrue(model.TrySelect("shield"));
            Assert.IsTrue(model.TrySelect("speed"));
            Assert.IsTrue(model.TrySelect("neutral"));
            Assert.IsFalse(model.TrySelect("cdr"));
            Assert.AreEqual(3, model.SelectedCount);
        }

        [Test]
        public void SelectionRejectsDuplicatesUnknownAndMissingDeselect()
        {
            var model = new RelicLoadoutModel(Catalog(), maxSlots: 3);

            Assert.IsTrue(model.TrySelect("shield"));
            Assert.IsFalse(model.TrySelect("shield"));
            Assert.IsFalse(model.TrySelect("missing"));
            Assert.IsFalse(model.TrySelect(null));
            Assert.IsFalse(model.Deselect("speed"));
            Assert.IsFalse(model.Deselect(""));
        }

        [Test]
        public void Deselect_FreesSlotAndSelectedPreservesSelectionOrder()
        {
            var model = new RelicLoadoutModel(Catalog(), maxSlots: 2);

            model.TrySelect("shield");
            model.TrySelect("speed");
            Assert.IsTrue(model.Deselect("shield"));
            Assert.IsTrue(model.TrySelect("neutral"));

            IReadOnlyList<Relic> selected = model.Selected();
            Assert.AreEqual(2, selected.Count);
            Assert.AreEqual("speed", selected[0].Id);
            Assert.AreEqual("neutral", selected[1].Id);
        }

        [Test]
        public void AggregateEffects_SumsByEffect()
        {
            var model = new RelicLoadoutModel(Catalog(), maxSlots: 4);

            model.TrySelect("shield");
            model.TrySelect("shield2");
            model.TrySelect("speed");

            IReadOnlyDictionary<RelicEffect, float> effects = model.AggregateEffects();

            Assert.AreEqual(35f, effects[RelicEffect.StartShield], Tolerance);
            Assert.AreEqual(0.15f, effects[RelicEffect.MoveSpeedOnKill], Tolerance);
        }

        [Test]
        public void CatalogDuplicateIds_LastWins()
        {
            var model = new RelicLoadoutModel(new[]
            {
                new Relic("dup", RelicEffect.StartShield, 10f),
                new Relic("dup", RelicEffect.MaxHpBonus, 50f)
            });

            Assert.IsTrue(model.TrySelect("dup"));

            IReadOnlyDictionary<RelicEffect, float> effects = model.AggregateEffects();
            Assert.IsFalse(effects.ContainsKey(RelicEffect.StartShield));
            Assert.AreEqual(50f, effects[RelicEffect.MaxHpBonus], Tolerance);
        }

        [Test]
        public void Clear_RemovesAllSelectionsAndAggregates()
        {
            var model = new RelicLoadoutModel(Catalog());
            model.TrySelect("shield");

            model.Clear();

            Assert.AreEqual(0, model.SelectedCount);
            Assert.AreEqual(0, model.Selected().Count);
            Assert.AreEqual(0, model.AggregateEffects().Count);
        }

        private static IReadOnlyList<Relic> Catalog()
        {
            return new[]
            {
                new Relic("shield", RelicEffect.StartShield, 20f),
                new Relic("shield2", RelicEffect.StartShield, 15f),
                new Relic("speed", RelicEffect.MoveSpeedOnKill, 0.15f),
                new Relic("neutral", RelicEffect.NeutralDamage, 0.2f),
                new Relic("cdr", RelicEffect.CooldownReduction, 0.1f)
            };
        }
    }
}
