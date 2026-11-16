using System.Collections.Generic;

namespace FuelManagement.Tests
{
    public class RangeCalibrationTests
    {
        private static readonly float[] ThreeAndFourHundred = { 300f, 400f };
        private static readonly float[] JustFive = { 5f };

        [Fact]
        public void NearestHouseDistances_TwoHouses_PicksNearest()
        {
            // Arrange
            var marks = new List<(float X, float Z)> { (0f, 300f), (1000f, 0f) };
            var houses = new List<(float X, float Z)> { (0f, 0f), (1000f, 400f) };

            // Act
            var d = RangeCalibration.NearestHouseDistances(marks, houses);

            // Assert
            Assert.Equal(ThreeAndFourHundred, d);
        }

        [Fact]
        public void NearestHouseDistances_NoHouses_ReturnsEmpty()
        {
            var d = RangeCalibration.NearestHouseDistances(new List<(float X, float Z)> { (1f, 1f) },
                new List<(float X, float Z)>());

            Assert.Empty(d);
        }

        [Fact]
        public void NearestHouseDistances_NullInputs_ReturnsEmpty()
        {
            Assert.Empty(RangeCalibration.NearestHouseDistances(null, null));
        }

        [Fact]
        public void NearestHouseDistances_NaNPosition_IsSkipped()
        {
            var d = RangeCalibration.NearestHouseDistances(
                new List<(float X, float Z)> { (float.NaN, 0f), (3f, 4f) },
                new List<(float X, float Z)> { (0f, 0f) });

            Assert.Equal(JustFive, d);
        }

        [Theory]
        [InlineData(0f, 100f)]
        [InlineData(1f, 500f)]
        [InlineData(0.5f, 300f)]
        [InlineData(0.75f, 400f)]
        [InlineData(0.875f, 450f)]
        [InlineData(-1f, 100f)]
        [InlineData(float.NaN, 300f)]
        public void Percentile_SortedOrNot_Interpolates(float p, float expected)
        {
            var values = new List<float> { 500f, 100f, 300f, 200f, 400f };

            Assert.Equal(expected, RangeCalibration.Percentile(values, p), 3);
        }

        [Fact]
        public void Percentile_Empty_ReturnsNaN()
        {
            Assert.True(float.IsNaN(RangeCalibration.Percentile(new List<float>(), 0.5f)));
        }

        [Fact]
        public void RangeFromDistances_TypicalMap_IsRoundTripTimesWindingTimesComfort()
        {
            // p75 of 1000..2000 in 5 steps = 1750 m → 1750 × 2 × 1.5 × 1.6 = 8400 m
            var d = new List<float> { 1000f, 1250f, 1500f, 1750f, 2000f };

            var range = RangeCalibration.RangeFromDistances(d, 0.75f, 1.5f, 1.6f);

            Assert.Equal(8400f, range.Value, 1);
        }

        [Fact]
        public void RangeFromDistances_TypicalRoundTrip_UsesAboutSixtyPercent()
        {
            var d = new List<float> { 800f, 900f, 1000f, 1100f, 1200f };
            float range = RangeCalibration.RangeFromDistances(d, 0.75f, 1.5f, 1.6f).Value;
            float typicalTrip = RangeCalibration.Percentile(d, 0.75f) * 2f * 1.5f;

            Assert.InRange(typicalTrip / range, 0.6f, 0.65f);
        }

        [Fact]
        public void RangeFromDistances_TooFewSamples_ReturnsNull()
        {
            Assert.Null(RangeCalibration.RangeFromDistances(new List<float> { 100f, 200f }, 0.75f, 1.5f, 1.6f));
        }

        [Fact]
        public void RangeFromDistances_Null_ReturnsNull()
        {
            Assert.Null(RangeCalibration.RangeFromDistances(null, 0.75f, 1.5f, 1.6f));
        }

        [Fact]
        public void RangeFromDistances_TinyMap_ClampsToMinimum()
        {
            var d = new List<float> { 10f, 20f, 30f };

            Assert.Equal(RangeCalibration.MinRangeM, RangeCalibration.RangeFromDistances(d, 0.75f, 1.5f, 1.6f));
        }

        [Fact]
        public void RangeFromDistances_HugeMap_ClampsToMaximum()
        {
            var d = new List<float> { 50000f, 60000f, 70000f };

            Assert.Equal(RangeCalibration.MaxRangeM, RangeCalibration.RangeFromDistances(d, 0.75f, 1.5f, 1.6f));
        }

        [Theory]
        [InlineData(float.NaN, 1.6f)]
        [InlineData(0f, 1.6f)]
        [InlineData(1.5f, -1f)]
        [InlineData(1.5f, float.PositiveInfinity)]
        public void RangeFromDistances_InvalidFactors_UseDefaults(float winding, float comfort)
        {
            var d = new List<float> { 1000f, 1250f, 1500f, 1750f, 2000f };

            Assert.Equal(8400f, RangeCalibration.RangeFromDistances(d, 0.75f, winding, comfort).Value, 1);
        }
    }
}
