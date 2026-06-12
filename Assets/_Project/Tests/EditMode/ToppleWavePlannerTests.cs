using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Enigma.Map;
using Enigma.Data;

namespace Enigma.Tests
{
    public sealed class ToppleWavePlannerTests
    {
        private sealed class FakeRandomSource : IRandomSource
        {
            private readonly int _fixedValue;
            public FakeRandomSource(int fixedValue) { _fixedValue = fixedValue; }
            public int Next(int maxExclusive) => _fixedValue;
        }

        // ジッタ0(fake が 0 を返す)のとき遅延が XZ 距離に比例
        [Test]
        public void PlanDelays_NoJitter_DelayEqualsDistanceOverSpeed()
        {
            var planner = new ToppleWavePlanner(new FakeRandomSource(0));
            var epicenter = new Vector3(0f, 0f, 0f);
            var positions = new List<Vector3> { new Vector3(14f, 0f, 0f) };

            float[] delays = planner.PlanDelays(epicenter, positions, 14f, 1f);

            Assert.AreEqual(1f, delays[0], 0.001f);
        }

        // Y 座標差は距離に影響しない
        [Test]
        public void PlanDelays_YDifferenceDoesNotAffectDelay()
        {
            var planner = new ToppleWavePlanner(new FakeRandomSource(0));
            var epicenter = new Vector3(0f, 0f, 0f);
            var posFlat   = new List<Vector3> { new Vector3(10f, 0f, 0f) };
            var posHigh   = new List<Vector3> { new Vector3(10f, 100f, 0f) };

            float[] delaysFlat = planner.PlanDelays(epicenter, posFlat, 10f, 0f);
            float[] delaysHigh = planner.PlanDelays(epicenter, posHigh, 10f, 0f);

            Assert.AreEqual(delaysFlat[0], delaysHigh[0], 0.001f);
        }

        // fake が 999 を返すとき jitter がちょうど maxJitterSeconds 加算される
        [Test]
        public void PlanDelays_MaxJitter_AddsExactlyMaxJitterSeconds()
        {
            var planner = new ToppleWavePlanner(new FakeRandomSource(999));
            var epicenter = new Vector3(0f, 0f, 0f);
            var positions = new List<Vector3> { new Vector3(10f, 0f, 0f) };
            float waveSpeed = 10f;
            float maxJitter = 0.4f;

            float[] delays = planner.PlanDelays(epicenter, positions, waveSpeed, maxJitter);

            float expectedBase = 10f / waveSpeed;
            Assert.AreEqual(expectedBase + maxJitter, delays[0], 0.001f);
        }

        // waveSpeed が 0 や負でも Infinity/NaN にならない
        [Test]
        public void PlanDelays_ZeroOrNegativeSpeed_NeverInfinityOrNaN()
        {
            var planner = new ToppleWavePlanner(new FakeRandomSource(0));
            var epicenter = new Vector3(0f, 0f, 0f);
            var positions = new List<Vector3> { new Vector3(5f, 0f, 0f) };

            float[] delaysZero = planner.PlanDelays(epicenter, positions, 0f, 0f);
            float[] delaysNeg  = planner.PlanDelays(epicenter, positions, -5f, 0f);

            Assert.IsFalse(float.IsInfinity(delaysZero[0]), "速度0で Infinity になった");
            Assert.IsFalse(float.IsNaN(delaysZero[0]),      "速度0で NaN になった");
            Assert.IsFalse(float.IsInfinity(delaysNeg[0]),  "負速度で Infinity になった");
            Assert.IsFalse(float.IsNaN(delaysNeg[0]),       "負速度で NaN になった");
        }

        // Quaternion.AngleAxis(90, axis) * up と外向き正規化方向 d の内積が 0.99 超
        [Test]
        public void ToppleAxis_ResultProducesOutwardToppleDirection()
        {
            var cases = new[]
            {
                (epicenter: new Vector3(0f, 0f, 0f), tree: new Vector3(5f, 0f, 0f)),
                (epicenter: new Vector3(0f, 0f, 0f), tree: new Vector3(0f, 0f, 3f)),
                (epicenter: new Vector3(1f, 2f, 1f), tree: new Vector3(4f, 5f, 1f)),
                (epicenter: new Vector3(0f, 0f, 0f), tree: new Vector3(-3f, 0f, -3f)),
            };

            foreach (var (epicenter, tree) in cases)
            {
                var axis = ToppleWavePlanner.ToppleAxis(epicenter, tree);
                var rotated = Quaternion.AngleAxis(90f, axis) * Vector3.up;

                var diff = tree - epicenter;
                diff.y = 0f;
                var d = diff.normalized;

                float dot = Vector3.Dot(rotated, d);
                Assert.Greater(dot, 0.99f, $"epicenter={epicenter} tree={tree}: dot={dot}");
            }
        }

        // epicenter と treePosition が同一でも正規化された軸(magnitude ≈ 1)を返す
        [Test]
        public void ToppleAxis_SamePosition_ReturnsMagnitudeOne()
        {
            var pos = new Vector3(3f, 1f, 5f);
            var axis = ToppleWavePlanner.ToppleAxis(pos, pos);

            Assert.AreEqual(1f, axis.magnitude, 0.001f);
        }
    }
}
