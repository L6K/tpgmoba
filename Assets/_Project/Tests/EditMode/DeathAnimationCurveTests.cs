using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests
{
    public sealed class DeathAnimationCurveTests
    {
        [Test]
        public void ToppleAngle_Endpoints()
        {
            Assert.AreEqual(0f, DeathAnimationCurve.ToppleAngle(0f), 0.001f);
            Assert.AreEqual(90f, DeathAnimationCurve.ToppleAngle(1f), 0.001f);
        }

        [Test]
        public void ToppleAngle_IsMonotonicIncreasing()
        {
            float prev = DeathAnimationCurve.ToppleAngle(0f);
            for (int i = 1; i <= 10; i++)
            {
                float cur = DeathAnimationCurve.ToppleAngle(i / 10f);
                Assert.GreaterOrEqual(cur, prev);
                prev = cur;
            }
        }

        [Test]
        public void ToppleAngle_ClampsOutOfRange()
        {
            Assert.AreEqual(0f, DeathAnimationCurve.ToppleAngle(-1f), 0.001f);
            Assert.AreEqual(90f, DeathAnimationCurve.ToppleAngle(2f), 0.001f);
        }

        [Test]
        public void FadeAlpha_Endpoints()
        {
            Assert.AreEqual(1f, DeathAnimationCurve.FadeAlpha(0f), 0.001f);
            Assert.AreEqual(0f, DeathAnimationCurve.FadeAlpha(1f), 0.001f);
        }

        [Test]
        public void FadeAlpha_IsMonotonicDecreasing()
        {
            float prev = DeathAnimationCurve.FadeAlpha(0f);
            for (int i = 1; i <= 10; i++)
            {
                float cur = DeathAnimationCurve.FadeAlpha(i / 10f);
                Assert.LessOrEqual(cur, prev);
                prev = cur;
            }
        }

        [Test]
        public void SinkDepth_Endpoints()
        {
            Assert.AreEqual(0f, DeathAnimationCurve.SinkDepth(0f, 5f), 0.001f);
            Assert.AreEqual(5f, DeathAnimationCurve.SinkDepth(1f, 5f), 0.001f);
        }

        [Test]
        public void SinkDepth_IsMonotonicIncreasing()
        {
            float prev = DeathAnimationCurve.SinkDepth(0f, 3f);
            for (int i = 1; i <= 10; i++)
            {
                float cur = DeathAnimationCurve.SinkDepth(i / 10f, 3f);
                Assert.GreaterOrEqual(cur, prev);
                prev = cur;
            }
        }
    }
}
