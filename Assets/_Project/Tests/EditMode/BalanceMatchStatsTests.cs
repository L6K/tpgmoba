using NUnit.Framework;
using Enigma.Learning;

namespace Enigma.Tests
{
    public sealed class BalanceMatchStatsTests
    {
        [Test]
        public void ToJsonLine_ContainsMatchIdSeedAndWinner()
        {
            var stats = new BalanceMatchStats(matchId: 3, seed: 42);
            stats.SetRosters(new[] { "zeph", "garon" }, new[] { "vex", "nyx" });
            stats.SetDuration(123.4f);
            stats.SetWinner("Blue");

            string json = stats.ToJsonLine();

            StringAssert.Contains("\"matchId\":3", json);
            StringAssert.Contains("\"seed\":42", json);
            StringAssert.Contains("\"durationSec\":123.4", json);
            StringAssert.Contains("\"winnerTeam\":\"Blue\"", json);
        }

        [Test]
        public void ToJsonLine_ContainsRostersAsQuotedArrays()
        {
            var stats = new BalanceMatchStats(1, 1);
            stats.SetRosters(new[] { "zeph", "garon" }, new[] { "vex", "nyx" });

            string json = stats.ToJsonLine();

            StringAssert.Contains("\"blueRoster\":[\"zeph\",\"garon\"]", json);
            StringAssert.Contains("\"redRoster\":[\"vex\",\"nyx\"]", json);
        }

        [Test]
        public void ToJsonLine_AggregatesPerChampionKillsDeathsCs()
        {
            var stats = new BalanceMatchStats(1, 1);
            stats.RecordKill("zeph");
            stats.RecordKill("zeph");
            stats.RecordDeath("zeph");
            stats.RecordCs("zeph");
            stats.RecordCs("zeph");
            stats.RecordCs("zeph");

            stats.RecordKill("garon");
            stats.RecordDeath("garon");
            stats.RecordDeath("garon");

            string json = stats.ToJsonLine();

            StringAssert.Contains("\"zeph\":{\"kills\":2,\"deaths\":1,\"cs\":3}", json);
            StringAssert.Contains("\"garon\":{\"kills\":1,\"deaths\":2,\"cs\":0}", json);
        }

        [Test]
        public void ToJsonLine_IgnoresNullOrEmptyCharId()
        {
            var stats = new BalanceMatchStats(1, 1);
            stats.RecordKill(null);
            stats.RecordDeath("");
            stats.RecordCs(null);

            string json = stats.ToJsonLine();

            StringAssert.Contains("\"perChampion\":{}", json);
        }

        [Test]
        public void RecordTowerDestroyed_KeepsOnlyFirstTeam()
        {
            var stats = new BalanceMatchStats(1, 1);
            stats.RecordTowerDestroyed("Blue");
            stats.RecordTowerDestroyed("Red");

            string json = stats.ToJsonLine();

            StringAssert.Contains("\"firstTowerTeam\":\"Blue\"", json);
        }

        [Test]
        public void RecordCoreCaptured_CountsPerTeamSeparately()
        {
            var stats = new BalanceMatchStats(1, 1);
            stats.RecordCoreCaptured("Blue");
            stats.RecordCoreCaptured("Blue");
            stats.RecordCoreCaptured("Red");

            string json = stats.ToJsonLine();

            StringAssert.Contains("\"coreCapturesBlue\":2", json);
            StringAssert.Contains("\"coreCapturesRed\":1", json);
        }

        [Test]
        public void ToJsonLine_ProducesParsableJson_BraceAndBracketBalance()
        {
            var stats = new BalanceMatchStats(5, 99);
            stats.SetRosters(new[] { "zeph" }, new[] { "vex" });
            stats.RecordKill("zeph");
            stats.RecordDeath("vex");
            stats.RecordTowerDestroyed("Red");
            stats.RecordCoreCaptured("Blue");

            string json = stats.ToJsonLine();

            int openBrace = 0, closeBrace = 0, openBracket = 0, closeBracket = 0;
            foreach (char c in json)
            {
                if (c == '{') openBrace++;
                else if (c == '}') closeBrace++;
                else if (c == '[') openBracket++;
                else if (c == ']') closeBracket++;
            }

            Assert.AreEqual(openBrace, closeBrace, "brace mismatch: " + json);
            Assert.AreEqual(openBracket, closeBracket, "bracket mismatch: " + json);
            Assert.IsTrue(json.StartsWith("{") && json.EndsWith("}"));
        }

        [Test]
        public void DefaultWinnerTeam_IsTimeout_WhenNotSet()
        {
            var stats = new BalanceMatchStats(1, 1);
            string json = stats.ToJsonLine();
            StringAssert.Contains("\"winnerTeam\":\"timeout\"", json);
        }

        // ── towerEvents / killEvents（偏り調査用の時刻付きイベント列）─────────────

        [Test]
        public void ToJsonLine_ContainsTowerEventsWithTimeAndTeam()
        {
            var stats = new BalanceMatchStats(1, 1);
            stats.RecordTowerDestroyedAt(123.4f, "Blue");
            stats.RecordTowerDestroyedAt(456.7f, "Red");

            string json = stats.ToJsonLine();

            StringAssert.Contains("\"towerEvents\":[{\"t\":123.4,\"team\":\"Blue\"},{\"t\":456.7,\"team\":\"Red\"}]", json);
        }

        [Test]
        public void ToJsonLine_ContainsKillEventsWithTimeAndTeam()
        {
            var stats = new BalanceMatchStats(1, 1);
            stats.RecordChampionKillAt(12.3f, "Red");

            string json = stats.ToJsonLine();

            StringAssert.Contains("\"killEvents\":[{\"t\":12.3,\"team\":\"Red\"}]", json);
        }

        [Test]
        public void ToJsonLine_EmptyTowerAndKillEvents_ProducesEmptyArrays()
        {
            var stats = new BalanceMatchStats(1, 1);

            string json = stats.ToJsonLine();

            StringAssert.Contains("\"towerEvents\":[]", json);
            StringAssert.Contains("\"killEvents\":[]", json);
        }

        [Test]
        public void RecordTowerDestroyedAt_DoesNotAffectFirstTowerTeam()
        {
            // 時刻付きログは既存の firstTowerTeam(最初の1本のみ)集計と独立して並存する。
            var stats = new BalanceMatchStats(1, 1);
            stats.RecordTowerDestroyed("Blue");
            stats.RecordTowerDestroyedAt(50f, "Red");

            string json = stats.ToJsonLine();

            StringAssert.Contains("\"firstTowerTeam\":\"Blue\"", json);
            StringAssert.Contains("\"towerEvents\":[{\"t\":50.0,\"team\":\"Red\"}]", json);
        }

        [Test]
        public void ToJsonLine_WithTowerAndKillEvents_ProducesParsableJson_BraceAndBracketBalance()
        {
            var stats = new BalanceMatchStats(5, 99);
            stats.RecordTowerDestroyedAt(100f, "Blue");
            stats.RecordTowerDestroyedAt(200f, "Red");
            stats.RecordChampionKillAt(30f, "Blue");
            stats.RecordChampionKillAt(45f, "Red");

            string json = stats.ToJsonLine();

            int openBrace = 0, closeBrace = 0, openBracket = 0, closeBracket = 0;
            foreach (char c in json)
            {
                if (c == '{') openBrace++;
                else if (c == '}') closeBrace++;
                else if (c == '[') openBracket++;
                else if (c == ']') closeBracket++;
            }

            Assert.AreEqual(openBrace, closeBrace, "brace mismatch: " + json);
            Assert.AreEqual(openBracket, closeBracket, "bracket mismatch: " + json);
        }
    }
}
