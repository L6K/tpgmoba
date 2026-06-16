using Enigma.Combat;
using NUnit.Framework;

namespace Enigma.Tests.EditMode
{
    public sealed class DeathRecapModelTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void Record_BuildRecap_GroupsBySourceAndIgnoresNonPositiveDamage()
        {
            var model = new DeathRecapModel(windowSeconds: 12f);

            model.Record("Tower", 100f, 1f);
            model.Record("Tower", 25f, 2f);
            model.Record("Minion", 10f, 3f);
            model.Record("Tower", 0f, 4f);
            model.Record("Tower", -5f, 5f);

            var recap = model.BuildRecap(5f);

            Assert.AreEqual(2, recap.Count);
            Assert.AreEqual("Tower", recap[0].SourceId);
            Assert.AreEqual(125f, recap[0].TotalDamage, Tolerance);
            Assert.AreEqual(2, recap[0].HitCount);
            Assert.AreEqual("Minion", recap[1].SourceId);
            Assert.AreEqual(10f, recap[1].TotalDamage, Tolerance);
            Assert.AreEqual(1, recap[1].HitCount);
        }

        [Test]
        public void BuildRecap_SortsByDamageDescThenSourceId()
        {
            var model = new DeathRecapModel();

            model.Record("Bravo", 50f, 0f);
            model.Record("Alpha", 50f, 0f);
            model.Record("Carry", 80f, 0f);

            var recap = model.BuildRecap(0f);

            Assert.AreEqual("Carry", recap[0].SourceId);
            Assert.AreEqual("Alpha", recap[1].SourceId);
            Assert.AreEqual("Bravo", recap[2].SourceId);
        }

        [Test]
        public void BuildRecap_FiltersWindowAndIncludesBoundary()
        {
            var model = new DeathRecapModel(windowSeconds: 10f);

            model.Record("Boundary", 10f, 90f);
            model.Record("Old", 20f, 89.99f);

            var recap = model.BuildRecap(100f);

            Assert.AreEqual(1, recap.Count);
            Assert.AreEqual("Boundary", recap[0].SourceId);
        }

        [Test]
        public void Record_MaxEvents_DropsOldestEvents()
        {
            var model = new DeathRecapModel(windowSeconds: 100f, maxEvents: 2);

            model.Record("A", 10f, 0f);
            model.Record("B", 20f, 1f);
            model.Record("C", 30f, 2f);

            var recap = model.BuildRecap(3f);

            Assert.AreEqual(2, recap.Count);
            Assert.AreEqual("C", recap[0].SourceId);
            Assert.AreEqual("B", recap[1].SourceId);
        }

        [Test]
        public void Record_NullOrEmptySource_BecomesUnknown()
        {
            var model = new DeathRecapModel();

            model.Record(null, 10f, 0f);
            model.Record("", 5f, 1f);

            var recap = model.BuildRecap(1f);

            Assert.AreEqual(1, recap.Count);
            Assert.AreEqual("Unknown", recap[0].SourceId);
            Assert.AreEqual(15f, recap[0].TotalDamage, Tolerance);
            Assert.AreEqual(2, recap[0].HitCount);
        }

        [Test]
        public void TotalInWindowAndClear_ReturnExpectedValues()
        {
            var model = new DeathRecapModel(windowSeconds: 5f);

            model.Record("A", 10f, 0f);
            model.Record("B", 20f, 4f);

            Assert.AreEqual(20f, model.TotalInWindow(6f), Tolerance);

            model.Clear();

            Assert.AreEqual(0f, model.TotalInWindow(6f), Tolerance);
            Assert.AreEqual(0, model.BuildRecap(6f).Count);
        }
    }
}
