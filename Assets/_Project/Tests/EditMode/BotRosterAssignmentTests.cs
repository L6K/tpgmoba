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
        public void AssignPerTeam_AllowsOverlapBetweenBlueAndRed()
        {
            // 青赤間の重複は許可される仕様。複数シードで確認し、少なくとも1件は重複が起きることを期待する
            // （重複が起きない seed も理論上ありうるため、複数シードを試して仕様上の許可を検証する）。
            bool overlapFound = false;
            for (int seed = 0; seed < 20; seed++)
            {
                var result = BotRosterAssignment.AssignPerTeam(AllSix, seed, teamSize: 2);
                var blue = result.Take(2);
                var red = result.Skip(2).Take(2);
                if (blue.Intersect(red).Any())
                {
                    overlapFound = true;
                    break;
                }
            }
            Assert.IsTrue(overlapFound, "Expected at least one seed to produce blue/red overlap (allowed by spec)");
        }
    }
}
