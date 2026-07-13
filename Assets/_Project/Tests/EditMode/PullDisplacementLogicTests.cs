using NUnit.Framework;
using UnityEngine;
using Enigma.Combat;

namespace Enigma.Tests
{
    public sealed class PullDisplacementLogicTests
    {
        [Test]
        public void PullTarget_NormalDistance_MovesFullPullDistanceTowardCaster()
        {
            var caster = new Vector3(0f, 0f, 0f);
            var target = new Vector3(10f, 0f, 0f);

            var result = PullDisplacementLogic.PullTarget(caster, target, pullDistance: 4f, minSeparation: 2f);

            Assert.AreEqual(new Vector3(6f, 0f, 0f), result);
        }

        [Test]
        public void PullTarget_WithinMinSeparation_ClampsBeforeOverlap()
        {
            var caster = new Vector3(0f, 0f, 0f);
            var target = new Vector3(3f, 0f, 0f);

            var result = PullDisplacementLogic.PullTarget(caster, target, pullDistance: 4f, minSeparation: 2f);

            // caster-target 間(3m)から minSeparation(2m) を残すため 1m だけ引き寄せられ、
            // caster から見て minSeparation(2m) の位置で止まる
            Assert.AreEqual(new Vector3(2f, 0f, 0f), result);
        }

        [Test]
        public void PullTarget_SameCoordinates_ReturnsOriginalPositionSafely()
        {
            var caster = new Vector3(5f, 1f, 5f);
            var target = new Vector3(5f, 2f, 5f);

            var result = PullDisplacementLogic.PullTarget(caster, target, pullDistance: 4f, minSeparation: 2f);

            Assert.AreEqual(target, result);
        }

        [Test]
        public void PullTarget_PreservesTargetY_EvenWhenCasterYDiffers()
        {
            var caster = new Vector3(0f, 10f, 0f);
            var target = new Vector3(10f, 1.5f, 0f);

            var result = PullDisplacementLogic.PullTarget(caster, target, pullDistance: 4f, minSeparation: 2f);

            Assert.AreEqual(1.5f, result.y);
        }

        [Test]
        public void PullTarget_ZeroPullDistance_ReturnsOriginalPosition()
        {
            var caster = new Vector3(0f, 0f, 0f);
            var target = new Vector3(10f, 0f, 0f);

            var result = PullDisplacementLogic.PullTarget(caster, target, pullDistance: 0f, minSeparation: 2f);

            Assert.AreEqual(target, result);
        }
    }
}
