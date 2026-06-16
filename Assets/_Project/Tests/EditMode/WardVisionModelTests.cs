using NUnit.Framework;
using Enigma.Vision;

namespace Enigma.Tests.EditMode
{
    public sealed class WardVisionModelTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void Place_AssignsIncreasingIds_AndDefaults()
        {
            var m = new WardVisionModel(maxActivePerTeam: 3, defaultLifetime: 90f, defaultVisionRadius: 12f);
            var a = m.Place(0, 1f, 2f, 0f);
            var b = m.Place(0, 3f, 4f, 0f);
            Assert.AreEqual(1, a.Id);
            Assert.AreEqual(2, b.Id);
            Assert.AreEqual(0, a.Team);
            Assert.AreEqual(1f, a.X, Eps);
            Assert.AreEqual(2f, a.Z, Eps);
            Assert.AreEqual(12f, a.VisionRadius, Eps);
            Assert.AreEqual(90f, a.RemainingSeconds, Eps);
        }

        [Test]
        public void Place_OverMax_EvictsOldestSameTeam()
        {
            var m = new WardVisionModel(maxActivePerTeam: 2, defaultLifetime: 90f);
            var w1 = m.Place(0, 0f, 0f, 0f);
            var w2 = m.Place(0, 0f, 0f, 0f);
            var w3 = m.Place(0, 0f, 0f, 0f); // 3本目 → 最古(w1)が落ちる

            Assert.AreEqual(2, m.CountForTeam(0));
            var active = m.ActiveWardsForTeam(0);
            CollectionAssert.AreEquivalent(new[] { w2.Id, w3.Id }, Ids(active));
            Assert.IsFalse(m.Remove(w1.Id), "w1 は既に除去済みのはず");
        }

        [Test]
        public void TeamLimits_AreIndependent()
        {
            var m = new WardVisionModel(maxActivePerTeam: 1);
            m.Place(0, 0f, 0f, 0f);
            m.Place(1, 0f, 0f, 0f);
            m.Place(0, 0f, 0f, 0f); // チーム0の上限超過 → チーム0が1本に保たれ、チーム1は無関係
            Assert.AreEqual(1, m.CountForTeam(0));
            Assert.AreEqual(1, m.CountForTeam(1));
        }

        [Test]
        public void Tick_DecrementsAndExpires_AtZero()
        {
            var m = new WardVisionModel(defaultLifetime: 10f);
            m.Place(0, 0f, 0f, 0f);
            m.Tick(4f);
            Assert.AreEqual(1, m.CountForTeam(0));
            Assert.AreEqual(6f, m.ActiveWards()[0].RemainingSeconds, Eps);
            m.Tick(6f); // ちょうど 0 で除去
            Assert.AreEqual(0, m.CountForTeam(0));
        }

        [Test]
        public void Remove_ReturnsTrueForExisting_FalseOtherwise()
        {
            var m = new WardVisionModel();
            var w = m.Place(0, 0f, 0f, 0f);
            Assert.IsTrue(m.Remove(w.Id));
            Assert.IsFalse(m.Remove(w.Id));
            Assert.IsFalse(m.Remove(999));
        }

        [Test]
        public void Clear_RemovesAll()
        {
            var m = new WardVisionModel();
            m.Place(0, 0f, 0f, 0f);
            m.Place(1, 0f, 0f, 0f);
            m.Clear();
            Assert.AreEqual(0, m.CountForTeam(0));
            Assert.AreEqual(0, m.CountForTeam(1));
            Assert.AreEqual(0, m.ActiveWards().Count);
        }

        private static int[] Ids(System.Collections.Generic.IReadOnlyList<Ward> wards)
        {
            var ids = new int[wards.Count];
            for (int i = 0; i < wards.Count; i++) ids[i] = wards[i].Id;
            return ids;
        }
    }
}
