using Enigma.Vision;
using NUnit.Framework;

namespace Enigma.Tests
{
    public sealed class VisionRevealModelTests
    {
        [Test]
        public void Update_TargetInsideSourceRadius_IsVisible()
        {
            var model = new VisionRevealModel();

            var visible = model.Update(
                new[] { new VisionSource(0f, 0f, 5f) },
                new[] { new VisionTarget(1, 3f, 4f) },
                0f);

            CollectionAssert.Contains(visible, 1);
            Assert.IsTrue(model.IsVisible(1));
        }

        [Test]
        public void Update_TargetOutsideSourceRadius_IsNotVisible()
        {
            var model = new VisionRevealModel();

            var visible = model.Update(
                new[] { new VisionSource(0f, 0f, 5f) },
                new[] { new VisionTarget(1, 5.1f, 0f) },
                0f);

            CollectionAssert.DoesNotContain(visible, 1);
            Assert.IsFalse(model.IsVisible(1));
        }

        [Test]
        public void Update_TargetOnRadiusBoundary_IsVisible()
        {
            var model = new VisionRevealModel();

            var visible = model.Update(
                new[] { new VisionSource(0f, 0f, 5f) },
                new[] { new VisionTarget(1, 5f, 0f) },
                0f);

            CollectionAssert.Contains(visible, 1);
        }

        [Test]
        public void Update_MultipleSources_VisibleIfAnySourceCoversTarget()
        {
            var model = new VisionRevealModel();

            var visible = model.Update(
                new[]
                {
                    new VisionSource(0f, 0f, 2f),
                    new VisionSource(10f, 0f, 3f)
                },
                new[] { new VisionTarget(1, 12f, 0f) },
                0f);

            CollectionAssert.Contains(visible, 1);
        }

        [Test]
        public void Update_MultipleSourcesOutsideRadius_TargetIsNotVisible()
        {
            var model = new VisionRevealModel();

            var visible = model.Update(
                new[]
                {
                    new VisionSource(0f, 0f, 2f),
                    new VisionSource(10f, 0f, 1f)
                },
                new[] { new VisionTarget(1, 5f, 0f) },
                0f);

            CollectionAssert.DoesNotContain(visible, 1);
        }

        [Test]
        public void Update_SourceWithNonPositiveRadius_RevealsNothing()
        {
            var model = new VisionRevealModel();

            var visible = model.Update(
                new[]
                {
                    new VisionSource(0f, 0f, 0f),
                    new VisionSource(0f, 0f, -1f)
                },
                new[] { new VisionTarget(1, 0f, 0f) },
                0f);

            CollectionAssert.DoesNotContain(visible, 1);
        }

        [Test]
        public void Update_WithZeroLinger_TargetBecomesInvisibleNextFrame()
        {
            var model = new VisionRevealModel(0f);

            model.Update(
                new[] { new VisionSource(0f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0f);
            model.Update(
                new[] { new VisionSource(100f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0.1f);

            Assert.IsFalse(model.IsVisible(1));
        }

        [Test]
        public void Update_WithLinger_TargetStaysVisibleUntilLingerExpires()
        {
            var model = new VisionRevealModel(1f);

            model.Update(
                new[] { new VisionSource(0f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0f);
            model.Update(
                new[] { new VisionSource(100f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0.4f);
            Assert.IsTrue(model.IsVisible(1));

            model.Update(
                new[] { new VisionSource(100f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0.7f);

            Assert.IsFalse(model.IsVisible(1));
        }

        [Test]
        public void Update_DirectVisibilityDuringLinger_ResetsLinger()
        {
            var model = new VisionRevealModel(1f);

            model.Update(
                new[] { new VisionSource(0f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0f);
            model.Update(
                new[] { new VisionSource(100f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0.8f);
            model.Update(
                new[] { new VisionSource(0f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0f);
            model.Update(
                new[] { new VisionSource(100f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0.8f);

            Assert.IsTrue(model.IsVisible(1));
        }

        [Test]
        public void Update_TargetRemovedFromList_DropsStoredLingerState()
        {
            var model = new VisionRevealModel(1f);

            model.Update(
                new[] { new VisionSource(0f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0f);
            model.Update(
                new[] { new VisionSource(100f, 0f, 5f) },
                System.Array.Empty<VisionTarget>(),
                0.1f);
            model.Update(
                new[] { new VisionSource(100f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0f);

            Assert.IsFalse(model.IsVisible(1));
        }

        [Test]
        public void Update_WithNegativeDeltaTime_DoesNotReduceLinger()
        {
            var model = new VisionRevealModel(0.5f);

            model.Update(
                new[] { new VisionSource(0f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0f);
            model.Update(
                new[] { new VisionSource(100f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                -10f);
            model.Update(
                new[] { new VisionSource(100f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0.4f);

            Assert.IsTrue(model.IsVisible(1));
        }

        [Test]
        public void Clear_RemovesAllVisibleState()
        {
            var model = new VisionRevealModel(1f);

            model.Update(
                new[] { new VisionSource(0f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0f);
            model.Clear();

            Assert.IsFalse(model.IsVisible(1));
        }

        [Test]
        public void IsVisible_BeforeUpdate_ReturnsFalse()
        {
            var model = new VisionRevealModel();

            Assert.IsFalse(model.IsVisible(1));
        }

        [Test]
        public void Update_WithEmptyInputs_DoesNotThrowAndReturnsEmpty()
        {
            var model = new VisionRevealModel();

            var visible = model.Update(
                System.Array.Empty<VisionSource>(),
                System.Array.Empty<VisionTarget>(),
                0f);

            Assert.AreEqual(0, visible.Count);
        }

        [Test]
        public void Constructor_WithNegativeLinger_ClampsToZero()
        {
            var model = new VisionRevealModel(-1f);

            model.Update(
                new[] { new VisionSource(0f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0f);
            model.Update(
                new[] { new VisionSource(100f, 0f, 5f) },
                new[] { new VisionTarget(1, 0f, 0f) },
                0f);

            Assert.IsFalse(model.IsVisible(1));
        }

        // ---- M-V: 茂み/高低差/地形遮蔽 ----

        [Test]
        public void Update_SourceAndTargetInSameBrush_IsVisible()
        {
            var model = new VisionRevealModel();

            var visible = model.Update(
                new[] { new VisionSource(0f, 0f, 5f, 0f, brushId: 2) },
                new[] { new VisionTarget(1, 1f, 1f, 0f, brushId: 2) },
                0f);

            CollectionAssert.Contains(visible, 1);
        }

        [Test]
        public void Update_TargetInBrush_SourceOutsideBrush_IsNotVisible()
        {
            var model = new VisionRevealModel();

            var visible = model.Update(
                new[] { new VisionSource(0f, 0f, 5f, 0f, brushId: -1) },
                new[] { new VisionTarget(1, 1f, 1f, 0f, brushId: 2) },
                0f);

            CollectionAssert.DoesNotContain(visible, 1);
        }

        [Test]
        public void Update_TargetInBrush_SourceInDifferentBrush_IsNotVisible()
        {
            var model = new VisionRevealModel();

            var visible = model.Update(
                new[] { new VisionSource(0f, 0f, 5f, 0f, brushId: 3) },
                new[] { new VisionTarget(1, 1f, 1f, 0f, brushId: 2) },
                0f);

            CollectionAssert.DoesNotContain(visible, 1);
        }

        [Test]
        public void Update_TargetHigherThanSourceBeyondLimit_IsNotVisible()
        {
            var model = new VisionRevealModel();

            var visible = model.Update(
                new[] { new VisionSource(0f, 0f, 5f, y: 0f, brushId: -1) },
                new[] { new VisionTarget(1, 1f, 1f, y: 1.01f, brushId: -1) },
                0f);

            CollectionAssert.DoesNotContain(visible, 1);
        }

        [Test]
        public void Update_SourceHigherThanTarget_IsVisible()
        {
            var model = new VisionRevealModel();

            var visible = model.Update(
                new[] { new VisionSource(0f, 0f, 5f, y: 10f, brushId: -1) },
                new[] { new VisionTarget(1, 1f, 1f, y: 0f, brushId: -1) },
                0f);

            CollectionAssert.Contains(visible, 1);
        }

        [Test]
        public void Update_HeightDifferenceExactlyAtLimit_IsVisible()
        {
            var model = new VisionRevealModel();

            var visible = model.Update(
                new[] { new VisionSource(0f, 0f, 5f, y: 0f, brushId: -1) },
                new[] { new VisionTarget(1, 1f, 1f, y: 1.0f, brushId: -1) },
                0f);

            CollectionAssert.Contains(visible, 1);
        }

        private sealed class FakeLineOfSightChecker : ILineOfSightChecker
        {
            public bool Result;
            public bool HasLineOfSight(in VisionSource source, in VisionTarget target) => Result;
        }

        [Test]
        public void Update_LineOfSightCheckerReturnsFalse_TargetIsNotVisible()
        {
            var checker = new FakeLineOfSightChecker { Result = false };
            var model = new VisionRevealModel(0f, checker);

            var visible = model.Update(
                new[] { new VisionSource(0f, 0f, 5f) },
                new[] { new VisionTarget(1, 1f, 1f) },
                0f);

            CollectionAssert.DoesNotContain(visible, 1);
        }

        [Test]
        public void Update_LineOfSightCheckerReturnsTrue_TargetIsVisible()
        {
            var checker = new FakeLineOfSightChecker { Result = true };
            var model = new VisionRevealModel(0f, checker);

            var visible = model.Update(
                new[] { new VisionSource(0f, 0f, 5f) },
                new[] { new VisionTarget(1, 1f, 1f) },
                0f);

            CollectionAssert.Contains(visible, 1);
        }

        [Test]
        public void Update_LegacyConstructors_DefaultToNoBrushAndZeroHeight()
        {
            var model = new VisionRevealModel();

            // 旧シグネチャ(Y省略)は Y=0/BrushId=-1 に委譲されるため、通常の円距離判定のみで可視になる。
            var visible = model.Update(
                new[] { new VisionSource(0f, 0f, 5f) },
                new[] { new VisionTarget(1, 3f, 4f) },
                0f);

            CollectionAssert.Contains(visible, 1);
        }

        [Test]
        public void Update_LingerStillFunctions_WithNewRulesActive()
        {
            var model = new VisionRevealModel(1f);

            // 茂み内で可視化した後、ソースが遠ざかっても linger 中は見え続ける。
            model.Update(
                new[] { new VisionSource(0f, 0f, 5f, 0f, brushId: 2) },
                new[] { new VisionTarget(1, 1f, 1f, 0f, brushId: 2) },
                0f);
            model.Update(
                new[] { new VisionSource(100f, 0f, 5f, 0f, brushId: -1) },
                new[] { new VisionTarget(1, 1f, 1f, 0f, brushId: 2) },
                0.5f);

            Assert.IsTrue(model.IsVisible(1));

            model.Update(
                new[] { new VisionSource(100f, 0f, 5f, 0f, brushId: -1) },
                new[] { new VisionTarget(1, 1f, 1f, 0f, brushId: 2) },
                0.7f);

            Assert.IsFalse(model.IsVisible(1));
        }
    }
}
