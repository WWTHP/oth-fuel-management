using System.Collections.Generic;
using Box = FuelManagement.HudLayout.Box;

namespace FuelManagement.Tests
{
    public class HudLayoutTests
    {
        private const float W = 1920f, H = 1080f;

        [Fact]
        public void GaugeCluster_TwoBottomLeftGauges_ReturnsUnion()
        {
            // Arrange
            var boxes = new List<Box> { new(40f, 960f, 200f, 80f), new(250f, 980f, 100f, 60f) };

            // Act
            var c = HudLayout.GaugeCluster(boxes, W, H);

            // Assert
            Assert.Equal(new Box(40f, 960f, 310f, 80f), c);
        }

        [Fact]
        public void GaugeCluster_FullScreenContainer_IsIgnored()
        {
            var boxes = new List<Box> { new(0f, 0f, W, H), new(40f, 960f, 200f, 80f) };

            Assert.Equal(new Box(40f, 960f, 200f, 80f), HudLayout.GaugeCluster(boxes, W, H));
        }

        [Theory]
        [InlineData(40f, 40f, 200f, 80f)]       // top of the screen
        [InlineData(1500f, 960f, 200f, 80f)]    // bottom right
        [InlineData(40f, 960f, 2f, 80f)]        // too thin
        [InlineData(float.NaN, 960f, 200f, 80f)]
        [InlineData(40f, 960f, float.PositiveInfinity, 80f)]
        public void GaugeCluster_OnlyUnsuitableBoxes_ReturnsNull(float x, float y, float w, float h)
        {
            Assert.Null(HudLayout.GaugeCluster(new List<Box> { new(x, y, w, h) }, W, H));
        }

        [Fact]
        public void GaugeCluster_NullOrBadScreen_ReturnsNull()
        {
            Assert.Null(HudLayout.GaugeCluster(null, W, H));
            Assert.Null(HudLayout.GaugeCluster(new List<Box> { new(40f, 960f, 200f, 80f) }, 0f, H));
        }

        [Fact]
        public void Place_WithCluster_CentresAboveWithGap()
        {
            var cluster = new Box(40f, 960f, 300f, 80f);

            var b = HudLayout.Place(cluster, 260f, 52f, 12f, W, H, 0f, 0f);

            Assert.Equal(new Box(60f, 896f, 260f, 52f), b);
            Assert.Equal(cluster.CenterX, b.CenterX);
        }

        [Fact]
        public void Place_NoCluster_UsesBottomLeftFallback()
        {
            Assert.Equal(new Box(20f, H - 190f, 260f, 52f), HudLayout.Place(null, 260f, 52f, 12f, W, H, 0f, 0f));
        }

        [Fact]
        public void Place_Offsets_AreAdded()
        {
            var b = HudLayout.Place(null, 260f, 52f, 12f, W, H, 30f, -40f);

            Assert.Equal(new Box(50f, H - 230f, 260f, 52f), b);
        }

        [Fact]
        public void Place_OffScreen_IsClampedOnScreen()
        {
            var b = HudLayout.Place(new Box(0f, 10f, 20f, 20f), 260f, 52f, 12f, W, H, -500f, 0f);

            Assert.Equal(new Box(0f, 0f, 260f, 52f), b);
        }

        [Fact]
        public void Place_NaNOffsets_AreIgnored()
        {
            var b = HudLayout.Place(null, 260f, 52f, 12f, W, H, float.NaN, float.PositiveInfinity);

            Assert.Equal(new Box(20f, H - 190f, 260f, 52f), b);
        }

        [Fact]
        public void Box_Edges_AreDerivedFromPositionAndSize()
        {
            var b = new Box(10f, 20f, 30f, 40f);

            Assert.Equal((40f, 60f, 25f, 40f), (b.Right, b.Bottom, b.CenterX, b.CenterY));
        }
    }
}
