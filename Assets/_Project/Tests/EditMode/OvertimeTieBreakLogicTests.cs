using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests.EditMode
{
    public sealed class OvertimeTieBreakLogicTests
    {
        [Test]
        public void タワー生存数が少ない側が敗者()
        {
            // Blue: タワー1本生存、Red: 2本生存 → Blue が劣勢なので Blue 敗北
            int result = OvertimeTieBreakLogic.PickLoserTeam(
                blueTowersAlive: 1, redTowersAlive: 2,
                blueStructureHp: 9999f, redStructureHp: 0f,
                coinFallbackBlueLoses: false);
            Assert.AreEqual((int)TeamId.Blue, result);
        }

        [Test]
        public void タワー生存数が同数ならHP合計が低い側が敗者()
        {
            int result = OvertimeTieBreakLogic.PickLoserTeam(
                blueTowersAlive: 2, redTowersAlive: 2,
                blueStructureHp: 100f, redStructureHp: 500f,
                coinFallbackBlueLoses: false);
            Assert.AreEqual((int)TeamId.Blue, result);
        }

        [Test]
        public void HP合計も同値ならコインがBlue敗北を指すとBlueが敗者()
        {
            int result = OvertimeTieBreakLogic.PickLoserTeam(
                blueTowersAlive: 1, redTowersAlive: 1,
                blueStructureHp: 250f, redStructureHp: 250f,
                coinFallbackBlueLoses: true);
            Assert.AreEqual((int)TeamId.Blue, result);
        }

        [Test]
        public void HP合計も同値ならコインがRed敗北を指すとRedが敗者()
        {
            int result = OvertimeTieBreakLogic.PickLoserTeam(
                blueTowersAlive: 1, redTowersAlive: 1,
                blueStructureHp: 250f, redStructureHp: 250f,
                coinFallbackBlueLoses: false);
            Assert.AreEqual((int)TeamId.Red, result);
        }

        [Test]
        public void タワー生存数の差はHP合計より優先される()
        {
            // Red の方がタワー本数は少ないが HP 合計は Red の方が高い → それでも Red 敗北
            int result = OvertimeTieBreakLogic.PickLoserTeam(
                blueTowersAlive: 2, redTowersAlive: 0,
                blueStructureHp: 10f, redStructureHp: 99999f,
                coinFallbackBlueLoses: false);
            Assert.AreEqual((int)TeamId.Red, result);
        }

        [Test]
        public void Redのタワー本数が少ない場合はRedが敗者()
        {
            int result = OvertimeTieBreakLogic.PickLoserTeam(
                blueTowersAlive: 3, redTowersAlive: 1,
                blueStructureHp: 0f, redStructureHp: 9999f,
                coinFallbackBlueLoses: true);
            Assert.AreEqual((int)TeamId.Red, result);
        }

        [Test]
        public void 全項目完全同値でもコイン値どおりに決着する()
        {
            int blueLoses = OvertimeTieBreakLogic.PickLoserTeam(0, 0, 0f, 0f, coinFallbackBlueLoses: true);
            int redLoses = OvertimeTieBreakLogic.PickLoserTeam(0, 0, 0f, 0f, coinFallbackBlueLoses: false);
            Assert.AreEqual((int)TeamId.Blue, blueLoses);
            Assert.AreEqual((int)TeamId.Red, redLoses);
        }
    }
}
