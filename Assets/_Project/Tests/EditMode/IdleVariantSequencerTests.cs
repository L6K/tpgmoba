using NUnit.Framework;
using Enigma.Character;

namespace Enigma.Tests.EditMode
{
    public sealed class IdleVariantSequencerTests
    {
        [Test]
        public void NotifyBaseLoopCompleted_NoVariants_AlwaysReturnsMinusOne()
        {
            var seq = new IdleVariantSequencer(variantCount: 0);

            Assert.IsFalse(seq.HasVariants);
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(-1, seq.NotifyBaseLoopCompleted());
        }

        [Test]
        public void NotifyBaseLoopCompleted_InsertsVariantOnlyAfterMinLoops()
        {
            // 順繰り（seed=0）+ min==max==2 で決定的: 2 ループ目で初めてバリアント挿入
            var seq = new IdleVariantSequencer(variantCount: 3, seed: 0, minLoops: 2, maxLoops: 2);

            Assert.AreEqual(-1, seq.NotifyBaseLoopCompleted()); // 1 ループ目: まだ
            Assert.AreEqual(0,  seq.NotifyBaseLoopCompleted()); // 2 ループ目: 挿入(先頭)
        }

        [Test]
        public void RoundRobin_CyclesVariantsInOrderAcrossInsertions()
        {
            // 各挿入の間に min ループを消化し、3 種を 0→1→2→0 と順繰りで返すことを確認
            var seq = new IdleVariantSequencer(variantCount: 3, seed: 0, minLoops: 2, maxLoops: 2);

            int Insert()
            {
                Assert.AreEqual(-1, seq.NotifyBaseLoopCompleted()); // 1 ループ目
                int picked = seq.NotifyBaseLoopCompleted();          // 2 ループ目で挿入
                Assert.GreaterOrEqual(picked, 0);
                seq.NotifyVariantCompleted();                        // バリアント再生完了
                return picked;
            }

            Assert.AreEqual(0, Insert());
            Assert.AreEqual(1, Insert());
            Assert.AreEqual(2, Insert());
            Assert.AreEqual(0, Insert()); // 一巡して先頭へ
        }

        [Test]
        public void SeededRandom_PicksWithinRange_AndIsDeterministic()
        {
            int[] Run()
            {
                var seq = new IdleVariantSequencer(variantCount: 4, seed: 12345, minLoops: 1, maxLoops: 1);
                var picks = new int[6];
                for (int i = 0; i < picks.Length; i++)
                {
                    picks[i] = seq.NotifyBaseLoopCompleted();
                    Assert.GreaterOrEqual(picks[i], 0);
                    Assert.Less(picks[i], 4); // 範囲内
                    seq.NotifyVariantCompleted();
                }
                return picks;
            }

            // 同一シードは同一系列（決定的・UnityEngine.Random 不使用）
            CollectionAssert.AreEqual(Run(), Run());
        }

        [Test]
        public void NegativeVariantCount_TreatedAsNoVariants()
        {
            var seq = new IdleVariantSequencer(variantCount: -3);

            Assert.IsFalse(seq.HasVariants);
            Assert.AreEqual(-1, seq.NotifyBaseLoopCompleted());
        }
    }
}
