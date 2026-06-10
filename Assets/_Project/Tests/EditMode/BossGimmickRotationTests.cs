using NUnit.Framework;
using Enigma.Objective;

namespace Enigma.Tests.EditMode
{
    public sealed class BossGimmickRotationTests
    {
        [Test]
        public void Next_ReturnsGimmicksInOrder()
        {
            var rotation = new BossGimmickRotation();

            Assert.AreEqual(BossGimmick.ChasingCircles, rotation.Next());
            Assert.AreEqual(BossGimmick.SectorCleave,   rotation.Next());
            Assert.AreEqual(BossGimmick.StackMarker,    rotation.Next());
        }

        [Test]
        public void Next_WrapsAroundAfterFullCycle()
        {
            var rotation = new BossGimmickRotation();

            rotation.Next(); // ChasingCircles
            rotation.Next(); // SectorCleave
            rotation.Next(); // StackMarker

            // 一巡後は先頭に戻る
            Assert.AreEqual(BossGimmick.ChasingCircles, rotation.Next());
        }

        [Test]
        public void Reset_RestartsFromFirstGimmick()
        {
            var rotation = new BossGimmickRotation();

            rotation.Next(); // ChasingCircles
            rotation.Next(); // SectorCleave
            rotation.Reset();

            Assert.AreEqual(BossGimmick.ChasingCircles, rotation.Next());
        }
    }
}
