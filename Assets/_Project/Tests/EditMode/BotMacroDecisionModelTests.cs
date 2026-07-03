using NUnit.Framework;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class BotMacroDecisionModelTests
    {
        private static BotMacroContext C(
            float hp = 1f,
            int allies = 3,
            int enemies = 3,
            bool objective = false,
            float objectiveDistance = 999f,
            bool minions = false,
            bool towerThreat = false,
            bool ownTowerUnderAttack = false,
            float distanceToThreatenedTower = float.MaxValue)
        {
            return new BotMacroContext(
                hp, allies, enemies, objective, objectiveDistance, minions, towerThreat,
                ownTowerUnderAttack, distanceToThreatenedTower);
        }

        [Test]
        public void LowHp_And_Outnumbered_Retreats()
        {
            var action = BotMacroDecisionModel.Decide(C(hp: 0.2f, allies: 2, enemies: 3));
            Assert.AreEqual(BotMacroAction.Retreat, action);
        }

        [Test]
        public void LowHp_And_EqualNumbers_Retreats()
        {
            var action = BotMacroDecisionModel.Decide(C(hp: 0.2f, allies: 3, enemies: 3));
            Assert.AreEqual(BotMacroAction.Retreat, action);
        }

        [Test]
        public void LowHp_And_TowerThreat_Retreats()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.2f, allies: 4, enemies: 2, towerThreat: true));
            Assert.AreEqual(BotMacroAction.Retreat, action);
        }

        [Test]
        public void LowHp_WithAdvantage_AndNoTowerThreat_DoesNotRetreat()
        {
            var action = BotMacroDecisionModel.Decide(C(hp: 0.2f, allies: 4, enemies: 2));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void AtLowHpThreshold_DoesNotRetreat()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: BotMacroDecisionModel.LowHpFraction, allies: 1, enemies: 5));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void ObjectiveActive_SafeHp_NumbersEven_InRange_Groups()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3, objective: true, objectiveDistance: 20f));
            Assert.AreEqual(BotMacroAction.GroupForObjective, action);
        }

        [Test]
        public void ObjectiveActive_AtJoinRange_Groups()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, objective: true,
                  objectiveDistance: BotMacroDecisionModel.ObjectiveJoinRange));
            Assert.AreEqual(BotMacroAction.GroupForObjective, action);
        }

        [Test]
        public void ObjectiveActive_OutOfRange_DoesNotGroup()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, objective: true,
                  objectiveDistance: BotMacroDecisionModel.ObjectiveJoinRange + 0.1f));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void ObjectiveActive_Outnumbered_DoesNotGroup()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 2, enemies: 3, objective: true, objectiveDistance: 20f));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void ObjectiveActive_BelowSafeHp_DoesNotGroup()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: BotMacroDecisionModel.SafeHpFraction - 0.01f,
                  objective: true, objectiveDistance: 20f));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void AllyAdvantage_SafeHp_WithMinions_Pushes()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 4, enemies: 2, minions: true));
            Assert.AreEqual(BotMacroAction.Push, action);
        }

        [Test]
        public void AllyAdvantage_AtSafeHp_WithMinions_Pushes()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: BotMacroDecisionModel.SafeHpFraction, allies: 4, enemies: 2, minions: true));
            Assert.AreEqual(BotMacroAction.Push, action);
        }

        [Test]
        public void AllyAdvantage_WithoutMinions_DoesNotPush()
        {
            var action = BotMacroDecisionModel.Decide(C(hp: 0.8f, allies: 4, enemies: 2));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void TowerThreat_WithoutMinions_Defends()
        {
            var action = BotMacroDecisionModel.Decide(C(hp: 0.8f, towerThreat: true));
            Assert.AreEqual(BotMacroAction.Defend, action);
        }

        [Test]
        public void TowerThreat_WithMinions_DoesNotDefend()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, minions: true, towerThreat: true));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void NoRuleMatches_Farms()
        {
            var action = BotMacroDecisionModel.Decide(C(hp: 0.8f, allies: 3, enemies: 3));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void Retreat_HasPriorityOverPush()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.2f, allies: 4, enemies: 2, minions: true, towerThreat: true));
            Assert.AreEqual(BotMacroAction.Retreat, action);
        }

        [Test]
        public void Objective_HasPriorityOverPush()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 4, enemies: 2,
                  objective: true, objectiveDistance: 20f, minions: true));
            Assert.AreEqual(BotMacroAction.GroupForObjective, action);
        }

        [Test]
        public void OwnTowerUnderAttack_SafeHp_InRange_Defends()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3,
                  ownTowerUnderAttack: true, distanceToThreatenedTower: 20f));
            Assert.AreEqual(BotMacroAction.Defend, action);
        }

        [Test]
        public void OwnTowerUnderAttack_LowHp_Outnumbered_RetreatsInstead()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.3f, allies: 2, enemies: 4,
                  ownTowerUnderAttack: true, distanceToThreatenedTower: 20f));
            Assert.AreEqual(BotMacroAction.Retreat, action);
        }

        [Test]
        public void OwnTowerUnderAttack_OutOfDefendRange_DoesNotDefend()
        {
            // 圏外は定数から導出する(M-0 のマップ1.4倍で 45→63 に変わりリテラル50が圏内化した回帰の再発防止)
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3,
                  ownTowerUnderAttack: true,
                  distanceToThreatenedTower: BotMacroDecisionModel.DefendJoinRange + 5f));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void OwnTowerUnderAttack_AndObjectiveActive_DefendsBeforeGrouping()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3,
                  objective: true, objectiveDistance: 10f,
                  ownTowerUnderAttack: true, distanceToThreatenedTower: 20f));
            Assert.AreEqual(BotMacroAction.Defend, action);
        }
    }
}
