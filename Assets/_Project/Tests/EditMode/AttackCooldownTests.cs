using NUnit.Framework;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class AttackCooldownTests
    {
        [Test]
        public void TryConsume_FirstCall_ReturnsTrue()
        {
            var cd = new AttackCooldown(0.5f);
            Assert.IsTrue(cd.TryConsume(0f));
        }

        [Test]
        public void TryConsume_WithinCooldown_ReturnsFalse()
        {
            var cd = new AttackCooldown(0.5f);
            cd.TryConsume(0f);
            Assert.IsFalse(cd.TryConsume(0.3f));
        }

        [Test]
        public void TryConsume_AfterCooldownElapsed_ReturnsTrue()
        {
            var cd = new AttackCooldown(0.5f);
            cd.TryConsume(0f);
            Assert.IsTrue(cd.TryConsume(0.5f));
        }
    }
}
