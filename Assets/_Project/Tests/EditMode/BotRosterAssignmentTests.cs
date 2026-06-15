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
    }
}
