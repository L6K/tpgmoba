using System.Collections.Generic;
using NUnit.Framework;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class TitanExposureLogicTests
    {
        [Test]
        public void BothLanesFullyAlive_NotExposed()
        {
            var towers = new List<(bool isAlive, int laneId)>
            {
                (true, 0), (true, 0), (true, 1), (true, 1),
            };
            Assert.IsFalse(TitanExposureLogic.IsTitanExposed(towers));
        }

        [Test]
        public void OneLaneFullyDead_OtherLaneAlive_IsExposed()
        {
            var towers = new List<(bool isAlive, int laneId)>
            {
                (false, 0), (false, 0), (true, 1), (true, 1),
            };
            Assert.IsTrue(TitanExposureLogic.IsTitanExposed(towers));
        }

        [Test]
        public void OneLanePartiallyDead_NotExposed()
        {
            // 外タワーのみ破壊、内タワーは生存 → まだ開通していない
            var towers = new List<(bool isAlive, int laneId)>
            {
                (false, 0), (true, 0), (true, 1), (true, 1),
            };
            Assert.IsFalse(TitanExposureLogic.IsTitanExposed(towers));
        }

        [Test]
        public void AllTowersDead_IsExposed()
        {
            var towers = new List<(bool isAlive, int laneId)>
            {
                (false, 0), (false, 0), (false, 1), (false, 1),
            };
            Assert.IsTrue(TitanExposureLogic.IsTitanExposed(towers));
        }

        [Test]
        public void EmptyList_NotExposed()
        {
            var towers = new List<(bool isAlive, int laneId)>();
            Assert.IsFalse(TitanExposureLogic.IsTitanExposed(towers));
        }

        [Test]
        public void OnlyOneLanePresent_AllDead_IsExposed()
        {
            // シーンに1レーン分のタワー情報しか渡されないケース(安全側にならず正しく判定する)
            var towers = new List<(bool isAlive, int laneId)>
            {
                (false, 0), (false, 0),
            };
            Assert.IsTrue(TitanExposureLogic.IsTitanExposed(towers));
        }

        [Test]
        public void OnlyOneLanePresent_Alive_NotExposed()
        {
            var towers = new List<(bool isAlive, int laneId)>
            {
                (true, 0), (false, 0),
            };
            Assert.IsFalse(TitanExposureLogic.IsTitanExposed(towers));
        }

        [Test]
        public void NullList_NotExposed()
        {
            Assert.IsFalse(TitanExposureLogic.IsTitanExposed(null));
        }
    }
}
