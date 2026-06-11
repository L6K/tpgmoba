using System.Collections.Generic;
using NUnit.Framework;
using Enigma.UI;

namespace Enigma.Tests
{
    public class CharSelectLogicTests
    {
        // ════════════════════════════════════════════════════════
        // ChooseAiPick
        // ════════════════════════════════════════════════════════

        [Test]
        public void ChooseAiPick_OwnedAndNotTaken_ReturnsValidIndex()
        {
            // 候補が owned[1] のみ → 必ず 1 が返る
            var taken  = new List<bool> { false, false, false };
            var owned  = new List<bool> { false, true,  false };
            int result = CharSelectLogic.ChooseAiPick(taken, owned, new FakeRandomSource(0));
            Assert.AreEqual(1, result);
        }

        [Test]
        public void ChooseAiPick_TakenIsExcluded()
        {
            // owned[0] は taken → 候補は owned[1] のみ
            var taken  = new List<bool> { true,  false, false };
            var owned  = new List<bool> { true,  true,  false };
            int result = CharSelectLogic.ChooseAiPick(taken, owned, new FakeRandomSource(0));
            Assert.AreEqual(1, result);
        }

        [Test]
        public void ChooseAiPick_NoCandidates_ReturnsMinusOne()
        {
            // 所持キャラが全部 taken
            var taken  = new List<bool> { true,  true  };
            var owned  = new List<bool> { true,  true  };
            int result = CharSelectLogic.ChooseAiPick(taken, owned, new FakeRandomSource(0));
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void ChooseAiPick_RandomIndexIsApplied()
        {
            // 候補: インデックス 0, 2, 4 → random.Next(3) = 1 → candidates[1] = 2
            var taken  = new List<bool> { false, true,  false, true, false };
            var owned  = new List<bool> { true,  true,  true,  false, true };
            int result = CharSelectLogic.ChooseAiPick(taken, owned, new FakeRandomSource(1));
            Assert.AreEqual(2, result);
        }

        [Test]
        public void ChooseAiPick_AllOwned_NoneNotTaken_ReturnsMinusOne()
        {
            // 全部 owned かつ全部 taken
            var taken  = new List<bool> { true, true, true };
            var owned  = new List<bool> { true, true, true };
            int result = CharSelectLogic.ChooseAiPick(taken, owned, new FakeRandomSource(0));
            Assert.AreEqual(-1, result);
        }

        // ════════════════════════════════════════════════════════
        // ResolveAutoLock
        // ════════════════════════════════════════════════════════

        [Test]
        public void ResolveAutoLock_ValidSelection_ReturnsSame()
        {
            // currentSelection = 2 で owned[2] = true → そのまま 2
            var owned  = new List<bool> { true, false, true };
            int result = CharSelectLogic.ResolveAutoLock(2, owned);
            Assert.AreEqual(2, result);
        }

        [Test]
        public void ResolveAutoLock_InvalidSelection_ReturnsFirstOwned()
        {
            // currentSelection = -1 → 最初の owned = 1
            var owned  = new List<bool> { false, true, true };
            int result = CharSelectLogic.ResolveAutoLock(-1, owned);
            Assert.AreEqual(1, result);
        }

        [Test]
        public void ResolveAutoLock_SelectionNotOwned_ReturnsFirstOwned()
        {
            // currentSelection = 0 だが owned[0] = false → 最初の owned = 2
            var owned  = new List<bool> { false, false, true };
            int result = CharSelectLogic.ResolveAutoLock(0, owned);
            Assert.AreEqual(2, result);
        }

        [Test]
        public void ResolveAutoLock_NoneOwned_ReturnsMinusOne()
        {
            var owned  = new List<bool> { false, false, false };
            int result = CharSelectLogic.ResolveAutoLock(1, owned);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void ResolveAutoLock_EmptyOwned_ReturnsMinusOne()
        {
            var owned  = new List<bool>();
            int result = CharSelectLogic.ResolveAutoLock(-1, owned);
            Assert.AreEqual(-1, result);
        }
    }
}
