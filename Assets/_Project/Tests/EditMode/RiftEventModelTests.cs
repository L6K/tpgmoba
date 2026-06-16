using Enigma.GameModes;
using NUnit.Framework;

namespace Enigma.Tests.EditMode
{
    public sealed class RiftEventModelTests
    {
        private const float Tolerance = 1e-3f;

        [Test]
        public void Tick_TransitionsDormantToWarningToOpen()
        {
            var model = new RiftEventModel(firstOpenAt: 20f, warningLead: 5f);

            Assert.AreEqual(RiftState.Dormant, model.Tick(14.9f, 0f, -1).State);
            Assert.AreEqual(RiftState.Warning, model.Tick(15f, 0f, -1).State);
            RiftStatus open = model.Tick(20f, 0f, -1);

            Assert.AreEqual(RiftState.Open, open.State);
            Assert.AreEqual(1, model.OpenCount);
        }

        [Test]
        public void Tick_OpenSingleTeamCapturesAfterCaptureSeconds()
        {
            var model = new RiftEventModel(firstOpenAt: 10f, warningLead: 0f, captureSeconds: 3f);

            model.Tick(10f, 0f, -1);
            RiftStatus progress = model.Tick(11f, 1f, 1);
            RiftStatus captured = model.Tick(13f, 2f, 1);

            Assert.AreEqual(RiftState.Open, progress.State);
            Assert.AreEqual(1, progress.CapturingTeam);
            Assert.AreEqual(1f / 3f, progress.CaptureProgress01, Tolerance);
            Assert.AreEqual(RiftState.Captured, captured.State);
            Assert.AreEqual(1, captured.OwnerTeam);
            Assert.AreEqual(RiftEffect.Shortcut, captured.ActiveEffect);
        }

        [Test]
        public void Tick_ContestedPresenceHoldsProgress()
        {
            var model = new RiftEventModel(firstOpenAt: 10f, warningLead: 0f, captureSeconds: 4f);

            model.Tick(10f, 0f, -1);
            RiftStatus first = model.Tick(11f, 1f, 0);
            RiftStatus contested = model.Tick(12f, 1f, -1);

            Assert.AreEqual(0.25f, first.CaptureProgress01, Tolerance);
            Assert.AreEqual(0.25f, contested.CaptureProgress01, Tolerance);
            Assert.AreEqual(-1, contested.CapturingTeam);
            Assert.AreEqual(RiftState.Open, contested.State);
        }

        [Test]
        public void Tick_OpenWindowExpiresToCooldownWhenUncaptured()
        {
            var model = new RiftEventModel(firstOpenAt: 10f, warningLead: 0f, openWindow: 5f);

            model.Tick(10f, 0f, -1);
            RiftStatus status = model.Tick(15f, 0f, -1);

            Assert.AreEqual(RiftState.Cooldown, status.State);
        }

        [Test]
        public void Tick_CapturedThenCooldownThenDormantSchedulesNextCycle()
        {
            var model = new RiftEventModel(firstOpenAt: 10f, warningLead: 0f, captureSeconds: 1f, effectDuration: 5f, cooldown: 7f);

            model.Tick(10f, 0f, -1);
            Assert.AreEqual(RiftState.Captured, model.Tick(11f, 1f, 0).State);
            Assert.AreEqual(RiftState.Cooldown, model.Tick(16f, 0f, -1).State);
            RiftStatus dormant = model.Tick(23f, 0f, -1);

            Assert.AreEqual(RiftState.Dormant, dormant.State);
            Assert.AreEqual(10f, dormant.SecondsToNextChange, Tolerance);
        }

        [Test]
        public void Tick_EffectsCycleByOpenCount()
        {
            Assert.AreEqual(RiftEffect.Shortcut, CaptureEffectAfterOpenCount(1));
            Assert.AreEqual(RiftEffect.TeamVision, CaptureEffectAfterOpenCount(2));
            Assert.AreEqual(RiftEffect.TeamHaste, CaptureEffectAfterOpenCount(3));
            Assert.AreEqual(RiftEffect.Shortcut, CaptureEffectAfterOpenCount(4));
        }

        private static RiftEffect CaptureEffectAfterOpenCount(int targetOpenCount)
        {
            var model = new RiftEventModel(firstOpenAt: 1f, warningLead: 0f, captureSeconds: 1f, effectDuration: 1f, cooldown: 1f);
            float now = 1f;
            RiftStatus status = default;

            for (int i = 0; i < targetOpenCount; i++)
            {
                model.Tick(now, 0f, -1);
                status = model.Tick(now + 1f, 1f, 0);
                now += 3f;
                model.Tick(now, 0f, -1);
                now += 1f;
            }

            return status.ActiveEffect;
        }
    }
}
