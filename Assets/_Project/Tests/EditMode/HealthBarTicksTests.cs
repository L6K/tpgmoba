using NUnit.Framework;
using Enigma.UI;

namespace Enigma.Tests
{
    public sealed class HealthBarTicksTests
    {
        // ---- TickUnit ----

        [Test]
        public void TickUnit_Boundary500_Returns50()
        {
            Assert.AreEqual(50f, HealthBarTicks.TickUnit(500f), 0.001f);
        }

        [Test]
        public void TickUnit_JustAbove500_Returns100()
        {
            Assert.AreEqual(100f, HealthBarTicks.TickUnit(501f), 0.001f);
        }

        [Test]
        public void TickUnit_Boundary2000_Returns100()
        {
            Assert.AreEqual(100f, HealthBarTicks.TickUnit(2000f), 0.001f);
        }

        [Test]
        public void TickUnit_JustAbove2000_Returns500()
        {
            Assert.AreEqual(500f, HealthBarTicks.TickUnit(2001f), 0.001f);
        }

        [Test]
        public void TickUnit_SmallValue_Returns50()
        {
            Assert.AreEqual(50f, HealthBarTicks.TickUnit(30f), 0.001f);
        }

        [Test]
        public void TickUnit_LargeValue_Returns500()
        {
            Assert.AreEqual(500f, HealthBarTicks.TickUnit(5000f), 0.001f);
        }

        // ---- InnerTickCount ----

        [Test]
        public void InnerTickCount_30hp_Returns0()
        {
            // unit=50, floor(30/50)=0
            Assert.AreEqual(0, HealthBarTicks.InnerTickCount(30f));
        }

        [Test]
        public void InnerTickCount_200hp_Returns3()
        {
            // unit=50, 200 is exact multiple → 4-1=3
            Assert.AreEqual(3, HealthBarTicks.InnerTickCount(200f));
        }

        [Test]
        public void InnerTickCount_600hp_Returns5()
        {
            // unit=100, 600 is exact multiple → 6-1=5
            Assert.AreEqual(5, HealthBarTicks.InnerTickCount(600f));
        }

        [Test]
        public void InnerTickCount_500hp_Returns9()
        {
            // unit=50, 500 is exact multiple → 10-1=9
            Assert.AreEqual(9, HealthBarTicks.InnerTickCount(500f));
        }

        [Test]
        public void InnerTickCount_2000hp_Returns19()
        {
            // unit=100, 2000 is exact multiple → 20-1=19
            Assert.AreEqual(19, HealthBarTicks.InnerTickCount(2000f));
        }

        [Test]
        public void InnerTickCount_5000hp_Returns9()
        {
            // unit=500, 5000 is exact multiple → 10-1=9
            Assert.AreEqual(9, HealthBarTicks.InnerTickCount(5000f));
        }

        [Test]
        public void InnerTickCount_50hp_Exact_Returns0()
        {
            // unit=50, 50 is exact → 1-1=0
            Assert.AreEqual(0, HealthBarTicks.InnerTickCount(50f));
        }

        [Test]
        public void InnerTickCount_120hp_NonExact_Returns2()
        {
            // unit=50, floor(120/50)=2
            Assert.AreEqual(2, HealthBarTicks.InnerTickCount(120f));
        }

        [Test]
        public void InnerTickCount_1000hp_Returns9()
        {
            // unit=100, 1000 is exact → 10-1=9
            Assert.AreEqual(9, HealthBarTicks.InnerTickCount(1000f));
        }

        // ---- TickRatio ----

        [Test]
        public void TickRatio_200hp_Index1_Returns0point25()
        {
            // unit=50, index=1 → 50/200=0.25
            Assert.AreEqual(0.25f, HealthBarTicks.TickRatio(200f, 1), 0.001f);
        }

        [Test]
        public void TickRatio_200hp_Index3_Returns0point75()
        {
            // unit=50, index=3 → 150/200=0.75
            Assert.AreEqual(0.75f, HealthBarTicks.TickRatio(200f, 3), 0.001f);
        }

        [Test]
        public void TickRatio_600hp_Index1_Returns0point1667()
        {
            // unit=100, index=1 → 100/600≈0.1667
            Assert.AreEqual(100f / 600f, HealthBarTicks.TickRatio(600f, 1), 0.001f);
        }

        [Test]
        public void TickRatio_5000hp_Index1_Returns0point1()
        {
            // unit=500, index=1 → 500/5000=0.1
            Assert.AreEqual(0.1f, HealthBarTicks.TickRatio(5000f, 1), 0.001f);
        }
    }
}
