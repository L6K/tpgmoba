using NUnit.Framework;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class AttackMotionTests
    {
        // --- TryBegin 受理 ---

        [Test]
        public void TryBegin_FromNone_Accepted()
        {
            var motion = new AttackMotion();
            bool result = motion.TryBegin(0.2f, 0.3f, () => { });
            Assert.IsTrue(result);
            Assert.AreEqual(AttackPhase.Windup, motion.Phase);
        }

        // --- Windup 中の TryBegin 拒否 ---

        [Test]
        public void TryBegin_DuringWindup_Rejected()
        {
            var motion = new AttackMotion();
            motion.TryBegin(0.5f, 0.3f, () => { });
            // まだ Windup が終わっていない
            bool result = motion.TryBegin(0.5f, 0.3f, () => { });
            Assert.IsFalse(result);
            Assert.AreEqual(AttackPhase.Windup, motion.Phase);
        }

        // --- Windup 完了で onStrike 発火 → Recovery 移行 ---

        [Test]
        public void Tick_WindupComplete_FiresOnStrikeAndEntersRecovery()
        {
            bool fired = false;
            var motion = new AttackMotion();
            motion.TryBegin(0.2f, 0.3f, () => fired = true);

            motion.Tick(0.21f); // Windup 完了
            Assert.IsTrue(fired, "onStrike が発火していない");
            Assert.AreEqual(AttackPhase.Recovery, motion.Phase);
        }

        // --- Recovery 完了で None ---

        [Test]
        public void Tick_RecoveryComplete_ReturnsToNone()
        {
            var motion = new AttackMotion();
            motion.TryBegin(0.1f, 0.3f, () => { });
            motion.Tick(0.11f); // Windup 完了 → Recovery
            motion.Tick(0.31f); // Recovery 完了
            Assert.AreEqual(AttackPhase.None, motion.Phase);
        }

        // --- CancelRecovery は Recovery 中のみ有効 ---

        [Test]
        public void CancelRecovery_DuringRecovery_SetsNone()
        {
            var motion = new AttackMotion();
            motion.TryBegin(0.1f, 0.3f, () => { });
            motion.Tick(0.11f); // Recovery へ
            motion.CancelRecovery();
            Assert.AreEqual(AttackPhase.None, motion.Phase);
        }

        [Test]
        public void CancelRecovery_DuringWindup_DoesNothing()
        {
            var motion = new AttackMotion();
            motion.TryBegin(0.5f, 0.3f, () => { });
            motion.CancelRecovery(); // Windup 中なので無効
            Assert.AreEqual(AttackPhase.Windup, motion.Phase);
        }

        [Test]
        public void CancelRecovery_WhenNone_DoesNothing()
        {
            var motion = new AttackMotion();
            motion.CancelRecovery(); // None でも例外が出ないこと
            Assert.AreEqual(AttackPhase.None, motion.Phase);
        }

        // --- Recovery 中の TryBegin で新 Windup に置換（攻撃キャンセル） ---

        [Test]
        public void TryBegin_DuringRecovery_ReplacesWithNewWindup()
        {
            int strikeCount = 0;
            var motion = new AttackMotion();
            motion.TryBegin(0.1f, 0.5f, () => strikeCount++);
            motion.Tick(0.11f); // 1回目 Strike + Recovery へ
            Assert.AreEqual(1, strikeCount);
            Assert.AreEqual(AttackPhase.Recovery, motion.Phase);

            bool result = motion.TryBegin(0.2f, 0.3f, () => strikeCount++);
            Assert.IsTrue(result, "Recovery 中の TryBegin が拒否された");
            Assert.AreEqual(AttackPhase.Windup, motion.Phase);

            motion.Tick(0.21f); // 2回目 Strike
            Assert.AreEqual(2, strikeCount);
            Assert.AreEqual(AttackPhase.Recovery, motion.Phase);
        }

        // --- MovementLocked は Windup 中のみ true ---

        [Test]
        public void MovementLocked_TrueOnlyDuringWindup()
        {
            var motion = new AttackMotion();
            Assert.IsFalse(motion.MovementLocked, "None 時は false であるべき");

            motion.TryBegin(0.3f, 0.3f, () => { });
            Assert.IsTrue(motion.MovementLocked, "Windup 時は true であるべき");

            motion.Tick(0.31f); // Recovery へ
            Assert.IsFalse(motion.MovementLocked, "Recovery 時は false であるべき");

            motion.Tick(0.31f); // None へ
            Assert.IsFalse(motion.MovementLocked, "None（完了後）は false であるべき");
        }

        // --- onStrike は一度だけ発火する ---

        [Test]
        public void OnStrike_FiredExactlyOnce()
        {
            int count = 0;
            var motion = new AttackMotion();
            motion.TryBegin(0.1f, 0.3f, () => count++);
            motion.Tick(0.05f); // まだ Windup 中
            motion.Tick(0.06f); // Windup 完了（累計0.11秒）
            motion.Tick(0.31f); // Recovery 完了
            Assert.AreEqual(1, count, "onStrike は1回だけ発火するべき");
        }
    }
}
