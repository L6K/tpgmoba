using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests
{
    public sealed class GoldWalletTests
    {
        [Test]
        public void InitialGold_IsSetByConstructor()
        {
            var wallet = new GoldWallet(500);
            Assert.AreEqual(500, wallet.Gold);
        }

        [Test]
        public void Add_IncreasesGold()
        {
            var wallet = new GoldWallet(100);
            wallet.Add(50);
            Assert.AreEqual(150, wallet.Gold);
        }

        [Test]
        public void TrySpend_Success_DeductsGoldAndReturnsTrue()
        {
            var wallet = new GoldWallet(200);
            bool result = wallet.TrySpend(80);
            Assert.IsTrue(result);
            Assert.AreEqual(120, wallet.Gold);
        }

        [Test]
        public void TrySpend_Insufficient_ReturnsFalseAndLeavesGoldUnchanged()
        {
            var wallet = new GoldWallet(50);
            bool result = wallet.TrySpend(100);
            Assert.IsFalse(result);
            // 残高は変化しない
            Assert.AreEqual(50, wallet.Gold);
        }

        [Test]
        public void Changed_FiredOnAdd()
        {
            var wallet = new GoldWallet(0);
            int fired = 0;
            wallet.Changed += () => fired++;

            wallet.Add(10);

            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Changed_FiredOnSuccessfulTrySpend()
        {
            var wallet = new GoldWallet(200);
            int fired = 0;
            wallet.Changed += () => fired++;

            wallet.TrySpend(50);

            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Changed_NotFiredOnFailedTrySpend()
        {
            var wallet = new GoldWallet(10);
            int fired = 0;
            wallet.Changed += () => fired++;

            wallet.TrySpend(100);

            // 失敗時は Changed を発火させない
            Assert.AreEqual(0, fired);
        }
    }
}
