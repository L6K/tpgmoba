using NUnit.Framework;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class BotSkillSelectorTests
    {
        // ヘルパー: スロット状態を簡潔に作る
        private static BotSkillSelector.SlotState S(bool ready, float range)
            => new BotSkillSelector.SlotState(ready, range);

        // 全スロット準備済み・射程内で、ターゲット高HP → R は出ず Q が選ばれる
        [Test]
        public void Picks_Q_When_All_Ready_And_Target_Healthy()
        {
            int slot = BotSkillSelector.Select(
                S(true, 10f), S(true, 10f), S(true, 10f),
                targetDistance: 5f, targetHpRatio: 0.9f);
            Assert.AreEqual(0, slot);
        }

        // Q が CD 中なら E にフォールバック（高HPなので R は不可）
        [Test]
        public void Falls_Back_To_E_When_Q_On_Cooldown()
        {
            int slot = BotSkillSelector.Select(
                S(false, 10f), S(true, 10f), S(true, 10f),
                targetDistance: 5f, targetHpRatio: 0.9f);
            Assert.AreEqual(1, slot);
        }

        // ターゲット HP<40% かつ R 射程内 → R が最優先
        [Test]
        public void Picks_R_When_Target_Low_Hp_And_In_Range()
        {
            int slot = BotSkillSelector.Select(
                S(true, 10f), S(true, 10f), S(true, 10f),
                targetDistance: 5f, targetHpRatio: 0.3f);
            Assert.AreEqual(2, slot);
        }

        // HP がちょうど閾値(0.4) は R 不可（< 判定）→ Q
        [Test]
        public void R_Excluded_At_Exactly_Threshold_Hp()
        {
            int slot = BotSkillSelector.Select(
                S(true, 10f), S(true, 10f), S(true, 10f),
                targetDistance: 5f, targetHpRatio: 0.4f);
            Assert.AreEqual(0, slot);
        }

        // 低HPでも R が射程外なら R 不可 → Q（Q は射程内）
        [Test]
        public void R_Excluded_When_Out_Of_Range_Even_If_Low_Hp()
        {
            int slot = BotSkillSelector.Select(
                S(true, 12f), S(true, 12f), S(true, 4f),
                targetDistance: 8f, targetHpRatio: 0.2f);
            Assert.AreEqual(0, slot);
        }

        // Q が射程外なら除外され、射程内の E が選ばれる
        [Test]
        public void Out_Of_Range_Slot_Is_Excluded()
        {
            int slot = BotSkillSelector.Select(
                S(true, 4f), S(true, 12f), S(true, 12f),
                targetDistance: 8f, targetHpRatio: 0.9f);
            Assert.AreEqual(1, slot);
        }

        // 全スロット CD 中 → -1
        [Test]
        public void Returns_Minus_One_When_All_On_Cooldown()
        {
            int slot = BotSkillSelector.Select(
                S(false, 10f), S(false, 10f), S(false, 10f),
                targetDistance: 5f, targetHpRatio: 0.3f);
            Assert.AreEqual(-1, slot);
        }

        // 準備済みでも全て射程外 → -1
        [Test]
        public void Returns_Minus_One_When_All_Out_Of_Range()
        {
            int slot = BotSkillSelector.Select(
                S(true, 4f), S(true, 4f), S(true, 4f),
                targetDistance: 20f, targetHpRatio: 0.2f);
            Assert.AreEqual(-1, slot);
        }

        // E は射程内なら Radius 距離条件を問わず撃てる（近距離でも可）
        [Test]
        public void E_Castable_In_Range_Regardless_Of_Distance()
        {
            int slot = BotSkillSelector.Select(
                S(false, 10f), S(true, 10f), S(false, 10f),
                targetDistance: 1f, targetHpRatio: 0.9f);
            Assert.AreEqual(1, slot);
        }
    }
}
