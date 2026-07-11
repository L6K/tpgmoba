using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Enigma.GameMode;

namespace Enigma.Tests
{
    public sealed class BotRosterAssignmentTests
    {
        // 現状の6キャラ想定の固定ロスター
        private static readonly string[] AllSix =
            { "zeph", "garon", "vex", "nyx", "rook", "sable" };

        [Test]
        public void Assign_ReturnsRequestedCount()
        {
            var result = BotRosterAssignment.Assign(AllSix, "zeph", seed: 1);
            Assert.AreEqual(BotRosterAssignment.BotCount, result.Length);
        }

        [Test]
        public void Assign_HasNoDuplicates_WhenPoolIsLargeEnough()
        {
            var result = BotRosterAssignment.Assign(AllSix, "zeph", seed: 42);
            Assert.AreEqual(result.Length, result.Distinct().Count());
        }

        [Test]
        public void Assign_ExcludesPlayerPick()
        {
            var result = BotRosterAssignment.Assign(AllSix, "zeph", seed: 7);
            CollectionAssert.DoesNotContain(result, "zeph");
        }

        [Test]
        public void Assign_IsDeterministic_SameSeedSameResult()
        {
            var a = BotRosterAssignment.Assign(AllSix, "garon", seed: 99);
            var b = BotRosterAssignment.Assign(AllSix, "garon", seed: 99);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Assign_DifferentSeeds_GenerallyDiffer()
        {
            var a = BotRosterAssignment.Assign(AllSix, "vex", seed: 1);
            var b = BotRosterAssignment.Assign(AllSix, "vex", seed: 2);
            // 異なるシードでは並びが変わることを期待する（同一なら決定性シャッフルが効いていない）
            Assert.IsFalse(a.SequenceEqual(b));
        }

        [Test]
        public void Assign_CoversAllRemaining_WhenExactlyFive()
        {
            // 6キャラ - プレイヤー1 = 5 でちょうど全候補を網羅する
            var result = BotRosterAssignment.Assign(AllSix, "rook", seed: 3);
            var expected = AllSix.Where(id => id != "rook").OrderBy(s => s);
            CollectionAssert.AreEquivalent(expected, result);
        }

        [Test]
        public void Assign_IgnoresNullEmptyAndDuplicateIds()
        {
            var input = new List<string> { "zeph", "", null, "garon", "garon", "vex", "nyx", "rook", "sable" };
            var result = BotRosterAssignment.Assign(input, "zeph", seed: 5);
            Assert.AreEqual(5, result.Length);
            CollectionAssert.DoesNotContain(result, "");
            CollectionAssert.DoesNotContain(result, null);
            // 重複した garon は候補として1回のみ → 結果も1回だけ
            Assert.AreEqual(1, result.Count(id => id == "garon"));
        }

        [Test]
        public void Assign_RepeatsWhenPoolSmallerThanRequested()
        {
            // 候補が3体しかないので5体要求すると周回して埋める（繰り返し許可）
            var small = new[] { "a", "b", "c" };
            var result = BotRosterAssignment.Assign(small, playerPick: null, seed: 10);
            Assert.AreEqual(5, result.Length);
            foreach (var id in result)
                CollectionAssert.Contains(small, id);
        }

        [Test]
        public void Assign_PlayerPickNull_KeepsFullPool()
        {
            var result = BotRosterAssignment.Assign(AllSix, playerPick: null, seed: 8);
            Assert.AreEqual(5, result.Length);
            Assert.AreEqual(5, result.Distinct().Count());
        }

        [Test]
        public void AssignPerTeam_ReturnsTwoTeamsOfRequestedSize()
        {
            var result = BotRosterAssignment.AssignPerTeam(AllSix, seed: 1, teamSize: 2);
            Assert.AreEqual(4, result.Length);
        }

        [Test]
        public void AssignPerTeam_NoDuplicatesWithinEachTeam()
        {
            var result = BotRosterAssignment.AssignPerTeam(AllSix, seed: 7, teamSize: 2);
            var blue = result.Take(2).ToArray();
            var red = result.Skip(2).Take(2).ToArray();

            Assert.AreEqual(blue.Length, blue.Distinct().Count());
            Assert.AreEqual(red.Length, red.Distinct().Count());
        }

        [Test]
        public void AssignPerTeam_IsDeterministic_SameSeedSameResult()
        {
            var a = BotRosterAssignment.AssignPerTeam(AllSix, seed: 55, teamSize: 2);
            var b = BotRosterAssignment.AssignPerTeam(AllSix, seed: 55, teamSize: 2);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void AssignPerTeam_DifferentSeeds_GenerallyDiffer()
        {
            var a = BotRosterAssignment.AssignPerTeam(AllSix, seed: 1, teamSize: 2);
            var b = BotRosterAssignment.AssignPerTeam(AllSix, seed: 2, teamSize: 2);
            Assert.IsFalse(a.SequenceEqual(b));
        }

        [Test]
        public void AssignPerTeam_NoDuplicatesAcrossTeams_WhenPoolSuffices()
        {
            // 同キャラが両チームに出るとキャラ別勝率の帰属が汚染される(シムのスモークで実測)。
            // プールが 2*teamSize 以上ある限り、青赤間も含めてグローバルに重複しないことを保証する。
            for (int seed = 0; seed < 20; seed++)
            {
                var result = BotRosterAssignment.AssignPerTeam(AllSix, seed, teamSize: 2);
                Assert.AreEqual(result.Length, result.Distinct().Count(),
                    $"seed={seed} で重複が発生: [{string.Join(",", result)}]");
            }
        }

        [Test]
        public void AssignPerTeam_WrapsWithDuplicates_WhenPoolTooSmall()
        {
            // プールが枠数未満なら巡回で埋める(空文字は入らない)
            var three = new[] { "a", "b", "c" };
            var result = BotRosterAssignment.AssignPerTeam(three, seed: 3, teamSize: 2);
            Assert.AreEqual(4, result.Length);
            Assert.IsFalse(result.Any(string.IsNullOrEmpty));
        }

        // ── AssignPerTeamMirrored（ミラー実験用: Blue/Red 入れ替え）─────────────

        [Test]
        public void AssignPerTeamMirrored_SwapsBlueAndRedBlocks()
        {
            var normal = BotRosterAssignment.AssignPerTeam(AllSix, seed: 55, teamSize: 2);
            var mirrored = BotRosterAssignment.AssignPerTeamMirrored(AllSix, seed: 55, teamSize: 2);

            var normalBlue = normal.Take(2).ToArray();
            var normalRed = normal.Skip(2).Take(2).ToArray();
            var mirroredBlue = mirrored.Take(2).ToArray();
            var mirroredRed = mirrored.Skip(2).Take(2).ToArray();

            CollectionAssert.AreEqual(normalRed, mirroredBlue);
            CollectionAssert.AreEqual(normalBlue, mirroredRed);
        }

        [Test]
        public void AssignPerTeamMirrored_IsDeterministic_SameSeedSameResult()
        {
            var a = BotRosterAssignment.AssignPerTeamMirrored(AllSix, seed: 21, teamSize: 2);
            var b = BotRosterAssignment.AssignPerTeamMirrored(AllSix, seed: 21, teamSize: 2);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void AssignPerTeamMirrored_PreservesTotalRosterComposition()
        {
            // サイドを入れ替えてもキャラ構成の集合自体は完全一致するはず（切り分けの前提）。
            var normal = BotRosterAssignment.AssignPerTeam(AllSix, seed: 12, teamSize: 3);
            var mirrored = BotRosterAssignment.AssignPerTeamMirrored(AllSix, seed: 12, teamSize: 3);
            CollectionAssert.AreEquivalent(normal, mirrored);
        }

        [Test]
        public void AssignPerTeamMirrored_ReturnsRequestedLength()
        {
            var result = BotRosterAssignment.AssignPerTeamMirrored(AllSix, seed: 4, teamSize: 3);
            Assert.AreEqual(6, result.Length);
        }
    }
}
