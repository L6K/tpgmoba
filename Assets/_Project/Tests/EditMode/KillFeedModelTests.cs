using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests
{
    public sealed class KillFeedModelTests
    {
        [Test]
        public void AddEntry_CapsAtMaxEntries()
        {
            var model = new KillFeedModel();
            for (int i = 0; i < KillFeedModel.MaxEntries + 3; i++)
                model.AddEntry("K" + i, "V" + i, TeamId.Blue, TeamId.Red);

            Assert.AreEqual(KillFeedModel.MaxEntries, model.Entries.Count);
        }

        [Test]
        public void AddEntry_NewestIsFirst()
        {
            var model = new KillFeedModel();
            model.AddEntry("Alice", "Bob", TeamId.Blue, TeamId.Red);
            model.AddEntry("Carol", "Dave", TeamId.Red, TeamId.Blue);

            // 最新（Carol）が先頭、その次に Alice、上限超過で古いものは Carol→Alice の順
            Assert.AreEqual("Carol", model.Entries[0].KillerName);
            Assert.AreEqual("Alice", model.Entries[1].KillerName);
        }

        [Test]
        public void AddEntry_FiresChanged()
        {
            var model = new KillFeedModel();
            int count = 0;
            model.Changed += () => count++;

            model.AddEntry("Alice", "Bob", TeamId.Blue, TeamId.Red);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void AddEntry_DropsOldestBeyondCap()
        {
            var model = new KillFeedModel();
            for (int i = 0; i < KillFeedModel.MaxEntries; i++)
                model.AddEntry("K" + i, "V" + i, TeamId.Blue, TeamId.Red);

            // 最古は K0。上限超えで1件足すと K0 が落ちるはず
            model.AddEntry("KNew", "VNew", TeamId.Red, TeamId.Blue);

            foreach (var e in model.Entries)
                Assert.AreNotEqual("K0", e.KillerName);
        }
    }
}
