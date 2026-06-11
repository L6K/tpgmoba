using NUnit.Framework;
using Enigma.Data;

namespace Enigma.Tests
{
    [TestFixture]
    internal sealed class MatchmakingServiceTests
    {
        [Test]
        public void StartQueue_SetsStateToSearching()
        {
            var svc = new MatchmakingService(new FakeRandomSource(0));
            svc.StartQueue();
            Assert.AreEqual(MatchmakingState.Searching, svc.State);
        }

        [Test]
        public void Tick_AccumulatesAndReachesTarget_FiresFoundOnce()
        {
            // FakeRandomSource(0) → target = 2 + 0 = 2 秒
            var svc = new MatchmakingService(new FakeRandomSource(0));
            svc.StartQueue();

            int firedCount = 0;
            svc.MatchFound += () => firedCount++;

            // 1 秒では成立しない
            svc.Tick(1f);
            Assert.AreEqual(MatchmakingState.Searching, svc.State);
            Assert.AreEqual(0, firedCount);

            // さらに 1.1 秒で目標 2 秒超え → 成立
            svc.Tick(1.1f);
            Assert.AreEqual(MatchmakingState.Found, svc.State);
            Assert.AreEqual(1, firedCount);

            // Tick を追加しても二度目は発火しない
            svc.Tick(5f);
            Assert.AreEqual(1, firedCount);
        }

        [Test]
        public void Cancel_SetsIdleAndResetsElapsed()
        {
            var svc = new MatchmakingService(new FakeRandomSource(0));
            svc.StartQueue();
            svc.Tick(1f);

            svc.Cancel();

            Assert.AreEqual(MatchmakingState.Idle, svc.State);
            Assert.AreEqual(0f, svc.ElapsedSeconds, delta: 0.0001f);
        }

        [Test]
        public void StartQueue_AfterFound_ResetsAndSearchesAgain()
        {
            // FakeRandomSource(1) → target = 2 + 1 = 3 秒
            var svc = new MatchmakingService(new FakeRandomSource(1));
            svc.StartQueue();
            svc.Tick(3f); // Found に遷移
            Assert.AreEqual(MatchmakingState.Found, svc.State);

            // 再キューイング: リセットして Searching に戻る
            svc.StartQueue();
            Assert.AreEqual(MatchmakingState.Searching, svc.State);
            Assert.AreEqual(0f, svc.ElapsedSeconds, delta: 0.0001f);
        }

        [Test]
        public void Tick_WhileIdle_DoesNothing()
        {
            var svc = new MatchmakingService(new FakeRandomSource(0));
            // StartQueue を呼ばずに Tick
            svc.Tick(100f);
            Assert.AreEqual(MatchmakingState.Idle, svc.State);
            Assert.AreEqual(0f, svc.ElapsedSeconds, delta: 0.0001f);
        }
    }
}
