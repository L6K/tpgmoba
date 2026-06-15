using NUnit.Framework;
using Enigma.Ability;
using Enigma.Data;

namespace Enigma.Tests
{
    public sealed class CastModeLogicTests
    {
        // --- Quick ---

        [Test]
        public void Quick_KeyDown_NonInstant_ReturnsCast()
        {
            var logic = new CastModeLogic(CastMode.Quick);
            var result = logic.HandleKeyDown(0, isInstant: false);
            Assert.AreEqual(CastAction.Cast, result);
        }

        [Test]
        public void Quick_KeyDown_Instant_ReturnsCast()
        {
            var logic = new CastModeLogic(CastMode.Quick);
            var result = logic.HandleKeyDown(0, isInstant: true);
            Assert.AreEqual(CastAction.Cast, result);
        }

        [Test]
        public void Quick_KeyUp_ReturnsNone()
        {
            var logic = new CastModeLogic(CastMode.Quick);
            logic.HandleKeyDown(0, isInstant: false);
            var result = logic.HandleKeyUp(0);
            Assert.AreEqual(CastAction.None, result);
        }

        // --- QuickWithIndicator ---

        [Test]
        public void QuickWithIndicator_KeyDown_ReturnsShowIndicator()
        {
            var logic = new CastModeLogic(CastMode.QuickWithIndicator);
            var result = logic.HandleKeyDown(1, isInstant: false);
            Assert.AreEqual(CastAction.ShowIndicator, result);
            Assert.AreEqual(1, logic.ArmedSlot);
        }

        [Test]
        public void QuickWithIndicator_KeyUp_ReturnsCastAndDisarms()
        {
            var logic = new CastModeLogic(CastMode.QuickWithIndicator);
            logic.HandleKeyDown(1, isInstant: false);
            var result = logic.HandleKeyUp(1);
            Assert.AreEqual(CastAction.Cast, result);
            Assert.AreEqual(-1, logic.ArmedSlot);
        }

        [Test]
        public void QuickWithIndicator_KeyUp_WrongSlot_ReturnsNone()
        {
            var logic = new CastModeLogic(CastMode.QuickWithIndicator);
            logic.HandleKeyDown(1, isInstant: false);
            var result = logic.HandleKeyUp(2);
            Assert.AreEqual(CastAction.None, result);
            Assert.AreEqual(1, logic.ArmedSlot);
        }

        [Test]
        public void QuickWithIndicator_Instant_ReturnsCast()
        {
            var logic = new CastModeLogic(CastMode.QuickWithIndicator);
            var result = logic.HandleKeyDown(0, isInstant: true);
            Assert.AreEqual(CastAction.Cast, result);
        }

        // --- Normal ---

        [Test]
        public void Normal_KeyDown_ReturnsShowIndicator()
        {
            var logic = new CastModeLogic(CastMode.Normal);
            var result = logic.HandleKeyDown(2, isInstant: false);
            Assert.AreEqual(CastAction.ShowIndicator, result);
            Assert.AreEqual(2, logic.ArmedSlot);
        }

        [Test]
        public void Normal_Confirm_ReturnsCastAndDisarms()
        {
            var logic = new CastModeLogic(CastMode.Normal);
            logic.HandleKeyDown(2, isInstant: false);
            var result = logic.HandleConfirm();
            Assert.AreEqual(CastAction.Cast, result);
            Assert.AreEqual(-1, logic.ArmedSlot);
        }

        [Test]
        public void Normal_Cancel_ReturnsCancelAndDisarms()
        {
            var logic = new CastModeLogic(CastMode.Normal);
            logic.HandleKeyDown(2, isInstant: false);
            var result = logic.HandleCancel();
            Assert.AreEqual(CastAction.Cancel, result);
            Assert.AreEqual(-1, logic.ArmedSlot);
        }

        [Test]
        public void Normal_SwitchSlot_ReArms()
        {
            var logic = new CastModeLogic(CastMode.Normal);
            logic.HandleKeyDown(0, isInstant: false);
            Assert.AreEqual(0, logic.ArmedSlot);

            // 別スロット押下で切替
            logic.HandleKeyDown(1, isInstant: false);
            Assert.AreEqual(1, logic.ArmedSlot);
        }

        [Test]
        public void Normal_Instant_ReturnsCastRegardlessOfMode()
        {
            var logic = new CastModeLogic(CastMode.Normal);
            var result = logic.HandleKeyDown(3, isInstant: true);
            Assert.AreEqual(CastAction.Cast, result);
        }

        [Test]
        public void Normal_Confirm_WhenNotArmed_ReturnsNone()
        {
            var logic = new CastModeLogic(CastMode.Normal);
            var result = logic.HandleConfirm();
            Assert.AreEqual(CastAction.None, result);
        }

        [Test]
        public void Cancel_WhenNotArmed_ReturnsNone()
        {
            var logic = new CastModeLogic(CastMode.Normal);
            var result = logic.HandleCancel();
            Assert.AreEqual(CastAction.None, result);
        }

        [Test]
        public void SyncMode_ResetsArmedSlot()
        {
            var logic = new CastModeLogic(CastMode.Normal);
            logic.HandleKeyDown(0, isInstant: false);
            Assert.AreEqual(0, logic.ArmedSlot);

            logic.SyncMode(CastMode.Quick);
            Assert.AreEqual(-1, logic.ArmedSlot);
        }
    }
}
