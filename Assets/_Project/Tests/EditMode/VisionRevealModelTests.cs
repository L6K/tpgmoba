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
    }
}
