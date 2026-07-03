using NUnit.Framework;
using Enigma.Core;

namespace Enigma.Tests
{
    public sealed class MatchEventLogTests
    {
        [Test]
        public void Log_AddsOneEventToEvents()
        {
            var log = new MatchEventLog();
            log.Log(new MatchEvent(1f, MatchEventType.ChampionKill, 0, "Alice"));

            Assert.AreEqual(1, log.Events.Count);
        }

        [Test]
        public void Log_FiresEventLoggedWithMatchingValues()
        {
            var log = new MatchEventLog();
            MatchEvent? received = null;
            log.EventLogged += e => received = e;

            var sent = new MatchEvent(2.5f, MatchEventType.TowerDestroyed, 1, "Tower_Mid");
            log.Log(sent);

            Assert.IsTrue(received.HasValue);
            Assert.AreEqual(sent.Time, received.Value.Time);
            Assert.AreEqual(sent.Type, received.Value.Type);
            Assert.AreEqual(sent.Team, received.Value.Team);
            Assert.AreEqual(sent.ActorName, received.Value.ActorName);
        }

        [Test]
        public void Clear_EmptiesEvents()
        {
            var log = new MatchEventLog();
            log.Log(new MatchEvent(0f, MatchEventType.MinionKill, 0, "Bob"));

            log.Clear();

            Assert.AreEqual(0, log.Events.Count);
        }

        [Test]
        public void Log_MultipleEvents_PreservesOrder()
        {
            var log = new MatchEventLog();
            log.Log(new MatchEvent(0f, MatchEventType.ChampionKill, 0, "First"));
            log.Log(new MatchEvent(1f, MatchEventType.ChampionDeath, 1, "Second"));
            log.Log(new MatchEvent(2f, MatchEventType.MatchEnd, 0, "Third"));

            Assert.AreEqual("First", log.Events[0].ActorName);
            Assert.AreEqual("Second", log.Events[1].ActorName);
            Assert.AreEqual("Third", log.Events[2].ActorName);
        }

        [Test]
        public void Log_AfterClear_CanLogAgain()
        {
            var log = new MatchEventLog();
            log.Log(new MatchEvent(0f, MatchEventType.CoreCaptured, 0, "Boss"));
            log.Clear();

            log.Log(new MatchEvent(3f, MatchEventType.TitanDestroyed, 1, "Titan_A"));

            Assert.AreEqual(1, log.Events.Count);
            Assert.AreEqual("Titan_A", log.Events[0].ActorName);
        }
    }
}
