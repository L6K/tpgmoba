using NUnit.Framework;
using Enigma.GameModes;

namespace Enigma.Tests
{
    public sealed class MatchHintModelTests
    {
        private static MatchHintContext Ctx(float hp = 1f, int gold = 0, bool objActive = false,
                                            bool objWarn = false, bool minions = false)
            => new MatchHintContext(hp, gold, objActive, objWarn, minions);

        [Test] public void Default_IsFarm()
            => Assert.AreEqual(MatchHint.Farm, MatchHintModel.Select(Ctx()));

        [Test] public void LowHp_IsRetreat()
            => Assert.AreEqual(MatchHint.Retreat, MatchHintModel.Select(Ctx(hp: 0.2f)));

        [Test] public void HpAtThreshold_IsNotRetreat()
            => Assert.AreNotEqual(MatchHint.Retreat, MatchHintModel.Select(Ctx(hp: MatchHintModel.LowHpFraction)));

        [Test] public void ObjectiveActive_IsContest()
            => Assert.AreEqual(MatchHint.ContestObjective, MatchHintModel.Select(Ctx(objActive: true)));

        [Test] public void ObjectiveWarning_IsObjectiveSoon()
            => Assert.AreEqual(MatchHint.ObjectiveSoon, MatchHintModel.Select(Ctx(objWarn: true)));

        [Test] public void ObjectiveActive_BeatsWarning()
            => Assert.AreEqual(MatchHint.ContestObjective, MatchHintModel.Select(Ctx(objActive: true, objWarn: true)));

        [Test] public void HighGold_IsBackToShop()
            => Assert.AreEqual(MatchHint.BackToShop, MatchHintModel.Select(Ctx(gold: 1500)));

        [Test] public void GoldAtThreshold_IsBackToShop()
            => Assert.AreEqual(MatchHint.BackToShop, MatchHintModel.Select(Ctx(gold: MatchHintModel.ShopGoldThreshold)));

        [Test] public void GoldBelowThreshold_IsNotShop()
            => Assert.AreNotEqual(MatchHint.BackToShop, MatchHintModel.Select(Ctx(gold: MatchHintModel.ShopGoldThreshold - 1)));

        [Test] public void Minions_IsPush()
            => Assert.AreEqual(MatchHint.PushWithMinions, MatchHintModel.Select(Ctx(minions: true)));

        [Test] public void LowHp_BeatsObjectiveActive()
            => Assert.AreEqual(MatchHint.Retreat, MatchHintModel.Select(Ctx(hp: 0.1f, objActive: true)));

        [Test] public void Objective_BeatsGoldAndMinions()
            => Assert.AreEqual(MatchHint.ContestObjective, MatchHintModel.Select(Ctx(gold: 2000, objActive: true, minions: true)));

        [Test] public void Gold_BeatsMinions()
            => Assert.AreEqual(MatchHint.BackToShop, MatchHintModel.Select(Ctx(gold: 1200, minions: true)));

        [Test] public void ObjectiveSoon_BeatsGold()
            => Assert.AreEqual(MatchHint.ObjectiveSoon, MatchHintModel.Select(Ctx(gold: 2000, objWarn: true)));
    }
}
