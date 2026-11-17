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
        public void PlaceDial_BesideTiltPair_MatchesSizeAndSpacing()
        {
            // Arrange: two 180 px dials with a 20 px gap between them
            var tilt = new Box(100f, 900f, 380f, 180f);

            // Act
            var d = HudLayout.PlaceDial(tilt, null, 140f, 12f, W, H, 0f, 0f);

            // Assert: third dial, same gap, same top, same size
            Assert.Equal(new Box(500f, 900f, 180f, 180f), d);
        }

        [Fact]
        public void PlaceDial_SingleTiltDial_UsesTenPercentSpacing()
        {
            var d = HudLayout.PlaceDial(new Box(100f, 900f, 180f, 180f), null, 140f, 12f, W, H, 0f, 0f);

            Assert.Equal(new Box(298f, 900f, 180f, 180f), d);
        }

        [Fact]
        public void PlaceDial_TiltWinsOverOtherPanels()
        {
            var d = HudLayout.PlaceDial(new Box(100f, 900f, 180f, 180f), new Box(1000f, 100f, 50f, 50f), 140f, 12f,
                W, H, 0f, 0f);

            Assert.Equal(298f, d.X);
        }

        [Fact]
        public void PlaceDial_OtherPanelsOnly_CentresAbove()
        {
            var d = HudLayout.PlaceDial(null, new Box(100f, 900f, 300f, 80f), 140f, 12f, W, H, 0f, 0f);

            Assert.Equal(new Box(180f, 748f, 140f, 140f), d);
        }

        [Fact]
        public void PlaceDial_NoAnchors_BottomLeftFallback()
        {
            Assert.Equal(new Box(20f, H - 160f, 140f, 140f), HudLayout.PlaceDial(null, null, 140f, 12f, W, H, 0f, 0f));
        }

        [Fact]
        public void PlaceDial_BadFallbackSize_Uses140()
        {
            Assert.Equal(140f, HudLayout.PlaceDial(null, null, float.NaN, 12f, W, H, 0f, 0f).W);
        }

        [Fact]
        public void PlaceDial_PushedDown_LowerHalfMayHangOffScreen()
        {
            var d = HudLayout.PlaceDial(null, null, 140f, 12f, W, H, 0f, 1000f);

            Assert.Equal(H - 70f, d.Y);
        }

        [Fact]
        public void PlaceDial_PushedRight_StaysOnScreen()
        {
            var d = HudLayout.PlaceDial(null, null, 140f, 12f, W, H, 5000f, float.NaN);

            Assert.Equal(W - 140f, d.X);
        }

        [Theory]
        [InlineData(0f, -90f)]
        [InlineData(0.5f, 0f)]
        [InlineData(1f, 90f)]
        [InlineData(0.25f, -45f)]
        [InlineData(-1f, -90f)]
        [InlineData(2f, 90f)]
        [InlineData(float.NaN, -90f)]
        public void NeedleAngle_MapsLevelToHalfCircle(float level, float expected)
        {
            Assert.Equal(expected, HudLayout.NeedleAngle(level), 4);
        }

        [Fact]
        public void Box_Edges_AreDerivedFromPositionAndSize()
        {
            var b = new Box(10f, 20f, 30f, 40f);

            Assert.Equal((40f, 60f, 25f, 40f), (b.Right, b.Bottom, b.CenterX, b.CenterY));
        }
    }
}
