using Enigma.Combat;
using NUnit.Framework;

namespace Enigma.Tests
{
    public sealed class StatusEffectModelTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void InitialState_AllowsMovementAndAction()
        {
            var model = new StatusEffectModel();

            Assert.IsFalse(model.IsStunned);
            Assert.IsFalse(model.IsRooted);
            Assert.IsFalse(model.IsSlowed);
            Assert.IsTrue(model.CanMove);
            Assert.IsTrue(model.CanAct);
            Assert.AreEqual(1f, model.MoveSpeedMultiplier, Tolerance);
        }

        [Test]
        public void ApplyStun_DisablesMovementAndAction()
        {
            var model = new StatusEffectModel();

            model.ApplyStun(1f);

            Assert.IsTrue(model.IsStunned);
            Assert.IsFalse(model.CanMove);
            Assert.IsFalse(model.CanAct);
        }

        [Test]
        public void ApplyStun_WhenExpired_RestoresNormalState()
        {
            var model = new StatusEffectModel();

            model.ApplyStun(1f);
            model.Tick(1.1f);

            Assert.IsFalse(model.IsStunned);
            Assert.IsTrue(model.CanMove);
            Assert.IsTrue(model.CanAct);
        }

        [Test]
        public void ApplyRoot_DisablesMovementButAllowsAction()
        {
            var model = new StatusEffectModel();

            model.ApplyRoot(1f);

            Assert.IsTrue(model.IsRooted);
            Assert.IsFalse(model.CanMove);
            Assert.IsTrue(model.CanAct);
        }

        [Test]
        public void ApplyRoot_WhenExpired_RestoresMovement()
        {
            var model = new StatusEffectModel();

            model.ApplyRoot(1f);
            model.Tick(1.1f);

            Assert.IsFalse(model.IsRooted);
            Assert.IsTrue(model.CanMove);
            Assert.IsTrue(model.CanAct);
        }

        [Test]
        public void ApplyStun_WithLongerRefresh_UsesMaximumRemainingTime()
        {
            var model = new StatusEffectModel();

            model.ApplyStun(1f);
            model.Tick(0.5f);
            model.ApplyStun(1f);
            model.Tick(0.6f);

            Assert.IsTrue(model.IsStunned);
            Assert.IsFalse(model.CanMove);
        }

        [Test]
        public void ApplyStun_WithShorterRefresh_DoesNotShortenRemainingTime()
        {
            var model = new StatusEffectModel();

            model.ApplyStun(2f);
            model.Tick(0.5f);
            model.ApplyStun(0.25f);
            model.Tick(1f);

            Assert.IsTrue(model.IsStunned);
        }

        [Test]
        public void ApplySlow_ReducesMoveSpeedMultiplier()
        {
            var model = new StatusEffectModel();

            model.ApplySlow(0.4f, 2f);

            Assert.IsTrue(model.IsSlowed);
            Assert.AreEqual(0.6f, model.MoveSpeedMultiplier, Tolerance);
        }

        [Test]
        public void ApplySlow_UsesStrongestActiveSlow()
        {
            var model = new StatusEffectModel();

            model.ApplySlow(0.3f, 2f);
            model.ApplySlow(0.5f, 1f);

            Assert.AreEqual(0.5f, model.MoveSpeedMultiplier, Tolerance);
        }

        [Test]
        public void ApplySlow_WhenStrongestExpires_UsesRemainingSlow()
        {
            var model = new StatusEffectModel();

            model.ApplySlow(0.3f, 2f);
            model.ApplySlow(0.5f, 1f);
            model.Tick(1.1f);

            Assert.IsTrue(model.IsSlowed);
            Assert.AreEqual(0.7f, model.MoveSpeedMultiplier, Tolerance);
        }

        [Test]
        public void ApplySlow_ClampsStrengthAboveOne()
        {
            var model = new StatusEffectModel();

            model.ApplySlow(1.5f, 2f);

            Assert.AreEqual(0f, model.MoveSpeedMultiplier, Tolerance);
        }

        [Test]
        public void ApplySlow_ClampsStrengthBelowZero()
        {
            var model = new StatusEffectModel();

            model.ApplySlow(-0.5f, 2f);

            Assert.IsTrue(model.IsSlowed);
            Assert.AreEqual(1f, model.MoveSpeedMultiplier, Tolerance);
        }

        [Test]
        public void ApplyWithNonPositiveDuration_IsIgnored()
        {
            var model = new StatusEffectModel();
            int changedCount = 0;
            model.Changed += () => changedCount++;

            model.ApplyStun(0f);
            model.ApplyRoot(-1f);
            model.ApplySlow(1f, 0f);

            Assert.IsFalse(model.IsStunned);
            Assert.IsFalse(model.IsRooted);
            Assert.IsFalse(model.IsSlowed);
            Assert.AreEqual(0, changedCount);
        }

        [Test]
        public void StunAndSlowTogether_StunBlocksMovementUntilItExpires()
        {
            var model = new StatusEffectModel();

            model.ApplyStun(1f);
            model.ApplySlow(0.4f, 2f);
            model.Tick(1.1f);

            Assert.IsFalse(model.IsStunned);
            Assert.IsTrue(model.IsSlowed);
            Assert.IsTrue(model.CanMove);
            Assert.AreEqual(0.6f, model.MoveSpeedMultiplier, Tolerance);
        }

        [Test]
        public void Clear_RemovesAllEffects()
        {
            var model = new StatusEffectModel();

            model.ApplyStun(1f);
            model.ApplyRoot(1f);
            model.ApplySlow(0.4f, 1f);
            model.Clear();

            Assert.IsFalse(model.IsStunned);
            Assert.IsFalse(model.IsRooted);
            Assert.IsFalse(model.IsSlowed);
            Assert.IsTrue(model.CanMove);
            Assert.IsTrue(model.CanAct);
            Assert.AreEqual(1f, model.MoveSpeedMultiplier, Tolerance);
        }

        [Test]
        public void Changed_FiresWhenEffectIsApplied()
        {
            var model = new StatusEffectModel();
            int changedCount = 0;
            model.Changed += () => changedCount++;

            model.ApplyStun(1f);

            Assert.AreEqual(1, changedCount);
        }

        [Test]
        public void Changed_DoesNotFireWhenStateDoesNotChange()
        {
            var model = new StatusEffectModel();
            int changedCount = 0;
            model.Changed += () => changedCount++;

            model.ApplyStun(1f);
            model.ApplyStun(0.5f);
            model.Tick(0f);

            Assert.AreEqual(1, changedCount);
        }

        [Test]
        public void Changed_FiresWhenTickExpiresEffects()
        {
            var model = new StatusEffectModel();
            int changedCount = 0;
            model.Changed += () => changedCount++;

            model.ApplyRoot(1f);
            model.Tick(1f);

            Assert.AreEqual(2, changedCount);
        }

        [Test]
        public void Changed_FiresWhenClearRemovesEffects()
        {
            var model = new StatusEffectModel();
            int changedCount = 0;
            model.Changed += () => changedCount++;

            model.ApplySlow(0.2f, 1f);
            model.Clear();

            Assert.AreEqual(2, changedCount);
        }

        [Test]
        public void Changed_DoesNotFireWhenClearHasNothingToRemove()
        {
            var model = new StatusEffectModel();
            int changedCount = 0;
            model.Changed += () => changedCount++;

            model.Clear();

            Assert.AreEqual(0, changedCount);
        }

        [Test]
        public void Tick_WithNegativeValue_IsIgnored()
        {
            var model = new StatusEffectModel();

            model.ApplyRoot(0.5f);
            model.Tick(-1f);
            model.Tick(0.4f);

            Assert.IsTrue(model.IsRooted);
        }
    }
}
