using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests
{
    public sealed class DamageGatingTests
    {
        // --- ゲート「閉」: ダメージ 0 化 ---

        [Test]
        public void ClosedGate_ZeroesDamage()
        {
            var gate = new TitanDamageGate(); // 初期状態は閉(露出前)
            Assert.IsFalse(gate.AllowsDamage);
            Assert.AreEqual(0f, DamageGating.Effective(100f, gate));
        }

        [Test]
        public void ClosedGate_ZeroesDamage_RegardlessOfAmount()
        {
            var gate = new TitanDamageGate();
            Assert.AreEqual(0f, DamageGating.Effective(9999f, gate));
            Assert.AreEqual(0f, DamageGating.Effective(0.01f, gate));
        }

        // --- ゲート「開」: 通常通過 ---

        [Test]
        public void OpenGate_PassesDamageUnchanged()
        {
            var gate = new TitanDamageGate();
            gate.SetExposed(true);
            Assert.IsTrue(gate.AllowsDamage);
            Assert.AreEqual(123.5f, DamageGating.Effective(123.5f, gate));
        }

        // --- ゲート未設定(null): 従来動作 ---

        [Test]
        public void NullGate_PassesDamageUnchanged()
        {
            Assert.AreEqual(77f, DamageGating.Effective(77f, null));
            Assert.AreEqual(0f, DamageGating.Effective(0f, null));
        }

        // --- TitanDamageGate の状態遷移 ---

        [Test]
        public void Gate_StartsClosed()
        {
            Assert.IsFalse(new TitanDamageGate().AllowsDamage);
        }

        [Test]
        public void SetExposed_ReturnsTrueOnlyOnStateChange()
        {
            var gate = new TitanDamageGate();
            Assert.IsTrue(gate.SetExposed(true));   // 閉→開: 変化あり(ログ用)
            Assert.IsFalse(gate.SetExposed(true));  // 開→開: 変化なし
            Assert.IsTrue(gate.SetExposed(false));  // 開→閉: 変化あり
            Assert.IsFalse(gate.SetExposed(false)); // 閉→閉: 変化なし
        }

        [Test]
        public void ReopenedGate_PassesDamage_ClosedAgain_Zeroes()
        {
            var gate = new TitanDamageGate();
            gate.SetExposed(true);
            Assert.AreEqual(50f, DamageGating.Effective(50f, gate));
            gate.SetExposed(false);
            Assert.AreEqual(0f, DamageGating.Effective(50f, gate));
        }
    }
}
