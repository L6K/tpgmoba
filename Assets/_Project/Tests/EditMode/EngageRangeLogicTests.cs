using System.Collections.Generic;
using NUnit.Framework;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class EngageRangeLogicTests
    {
        [Test]
        public void AllSkillsOnCooldown_ReturnsAttackRange()
        {
            var skills = new List<(bool ready, float range)>
            {
                (false, 14f), (false, 6f), (false, 10f),
            };
            Assert.AreEqual(3.5f, EngageRangeLogic.Effective(3.5f, skills));
        }

        [Test]
        public void QReady_ReturnsQRange()
        {
            var skills = new List<(bool ready, float range)>
            {
                (true, 14f), (false, 6f), (false, 10f),
            };
            Assert.AreEqual(14f, EngageRangeLogic.Effective(3.5f, skills));
        }

        [Test]
        public void MultipleReady_ReturnsMax()
        {
            var skills = new List<(bool ready, float range)>
            {
                (true, 14f), (true, 6f), (true, 10f),
            };
            Assert.AreEqual(14f, EngageRangeLogic.Effective(3.5f, skills));
        }

        [Test]
        public void EmptyList_ReturnsAttackRange()
        {
            Assert.AreEqual(3.5f, EngageRangeLogic.Effective(3.5f, new List<(bool ready, float range)>()));
        }

        [Test]
        public void NullList_ReturnsAttackRange()
        {
            Assert.AreEqual(3.5f, EngageRangeLogic.Effective(3.5f, null));
        }

        [Test]
        public void ReadySkillRangeBelowAttackRange_KeepsAttackRange()
        {
            // 遠隔キャラ(AA15)で CD明けスキルが短射程(6)なら AA射程がそのまま最大値
            var skills = new List<(bool ready, float range)>
            {
                (true, 6f),
            };
            Assert.AreEqual(15f, EngageRangeLogic.Effective(15f, skills));
        }
    }
}
