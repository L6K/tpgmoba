using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests
{
    public sealed class DeathRecapSourceNameTests
    {
        [Test]
        public void Clean_StripsCloneSuffix()
        {
            Assert.AreEqual("Projectile", DeathRecapSourceName.Clean("Projectile(Clone)"));
        }

        [Test]
        public void Clean_ReplacesUnderscoresWithSpaces()
        {
            Assert.AreEqual("RedBot Top", DeathRecapSourceName.Clean("RedBot_Top"));
        }

        [Test]
        public void Clean_NullOrEmpty_ReturnsUnknown()
        {
            Assert.AreEqual(DeathRecapSourceName.Unknown, DeathRecapSourceName.Clean(null));
            Assert.AreEqual(DeathRecapSourceName.Unknown, DeathRecapSourceName.Clean(""));
        }

        [Test]
        public void Clean_CombinedCloneAndUnderscore()
        {
            Assert.AreEqual("RedBot Jungle", DeathRecapSourceName.Clean("RedBot_Jungle(Clone)"));
        }
    }
}
