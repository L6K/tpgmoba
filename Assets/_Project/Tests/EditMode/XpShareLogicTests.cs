using System.Collections.Generic;
using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests
{
    public sealed class XpShareLogicTests
    {
        private static XpShareLogic.Candidate C(int id, TeamId team, float dist) =>
            new XpShareLogic.Candidate(id, team, dist);

        [Test]
        public void AllyWithinRadius_Receives()
        {
            var killer = C(1, TeamId.Blue, 0f);
            var ally   = C(2, TeamId.Blue, 10f); // 半径内
            var result = XpShareLogic.SelectRecipients(
                1, TeamId.Blue, new List<XpShareLogic.Candidate> { killer, ally }, 16f);

            Assert.IsTrue(result.Contains(1));
            Assert.IsTrue(result.Contains(2));
            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void Enemy_IsExcluded()
        {
            var killer = C(1, TeamId.Blue, 0f);
            var enemy  = C(2, TeamId.Red, 5f); // 半径内だが敵チーム
            var result = XpShareLogic.SelectRecipients(
                1, TeamId.Blue, new List<XpShareLogic.Candidate> { killer, enemy }, 16f);

            Assert.IsTrue(result.Contains(1));
            Assert.IsFalse(result.Contains(2));
        }

        [Test]
        public void Killer_ReceivesEvenWhenOutOfRadius()
        {
            // キラーは死亡地点から遠く離れていても必ず受給する
            var killer = C(1, TeamId.Blue, 999f);
            var result = XpShareLogic.SelectRecipients(
                1, TeamId.Blue, new List<XpShareLogic.Candidate> { killer }, 16f);

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.Contains(1));
        }

        [Test]
        public void AllyOutsideRadius_IsExcluded()
        {
            var killer = C(1, TeamId.Blue, 0f);
            var farAlly = C(2, TeamId.Blue, 20f); // 半径外の味方
            var result = XpShareLogic.SelectRecipients(
                1, TeamId.Blue, new List<XpShareLogic.Candidate> { killer, farAlly }, 16f);

            Assert.IsTrue(result.Contains(1));
            Assert.IsFalse(result.Contains(2));
        }

        [Test]
        public void DuplicateIds_AreNotRepeated()
        {
            // 同一 id が複数回現れても受給は一度だけ
            var killer = C(1, TeamId.Blue, 0f);
            var dupe1  = C(2, TeamId.Blue, 5f);
            var dupe2  = C(2, TeamId.Blue, 6f);
            var result = XpShareLogic.SelectRecipients(
                1, TeamId.Blue, new List<XpShareLogic.Candidate> { killer, dupe1, dupe2 }, 16f);

            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void NoRecipients_ReturnsEmptySafely()
        {
            // 候補なし／null でも例外を投げず空集合
            Assert.AreEqual(0, XpShareLogic.SelectRecipients(
                1, TeamId.Blue, new List<XpShareLogic.Candidate>(), 16f).Count);
            Assert.AreEqual(0, XpShareLogic.SelectRecipients(
                1, TeamId.Blue, null, 16f).Count);
        }
    }
}
