using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Enigma.Combat;
using Enigma.Minion;

namespace Enigma.Tests
{
    public sealed class MinionLogicTests
    {
        // --- ヘルパー ---
        private static TargetCandidate C(float x, float z, TeamId team) =>
            new TargetCandidate(new Vector3(x, 0f, z), team);

        // --- テストケース ---

        [Test]
        public void EnemyInRange_ReturnsNearestIndex()
        {
            // Blue ミニオンから見て、近い Red が選ばれる
            var candidates = new List<TargetCandidate>
            {
                C(5f,  0f, TeamId.Red),   // index 0, dist=5
                C(10f, 0f, TeamId.Red),   // index 1, dist=10
            };

            int result = MinionLogic.ChooseTarget(Vector3.zero, TeamId.Blue, candidates, 15f);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void EnemyOutOfRange_ReturnsMinusOne()
        {
            var candidates = new List<TargetCandidate>
            {
                C(20f, 0f, TeamId.Red),   // 範囲外
            };

            int result = MinionLogic.ChooseTarget(Vector3.zero, TeamId.Blue, candidates, 8f);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void SameTeam_IsExcluded()
        {
            var candidates = new List<TargetCandidate>
            {
                C(1f, 0f, TeamId.Blue),   // 同チームは対象外
            };

            int result = MinionLogic.ChooseTarget(Vector3.zero, TeamId.Blue, candidates, 50f);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void Neutral_IsExcluded()
        {
            // Neutral（ジャングルボス等）はミニオンの攻撃対象外
            var candidates = new List<TargetCandidate>
            {
                C(1f, 0f, TeamId.Neutral),
            };

            int result = MinionLogic.ChooseTarget(Vector3.zero, TeamId.Blue, candidates, 50f);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void EmptyList_ReturnsMinusOne()
        {
            int result = MinionLogic.ChooseTarget(
                Vector3.zero, TeamId.Blue, new List<TargetCandidate>(), 50f);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void EnemyAtExactRange_IsIncluded()
        {
            // 境界値: distance == aggroRange は含む
            var candidates = new List<TargetCandidate>
            {
                C(8f, 0f, TeamId.Red),
            };

            int result = MinionLogic.ChooseTarget(Vector3.zero, TeamId.Blue, candidates, 8f);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void RedTeam_ChoosesBlueEnemy()
        {
            // Red チームが Blue を敵と判定する
            var candidates = new List<TargetCandidate>
            {
                C(3f, 0f, TeamId.Blue),
            };

            int result = MinionLogic.ChooseTarget(Vector3.zero, TeamId.Red, candidates, 10f);
            Assert.AreEqual(0, result);
        }
    }
}
