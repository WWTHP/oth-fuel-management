using System.Collections.Generic;

namespace FuelManagement.Tests
{
    public class TripTests
    {
        private static readonly string[] JerryAndFuelTank = { "jerry", "fuel tank" };
        private static readonly string[] OneJerrycan = { "Side_Jerrycan" };

        [Fact]
        public void Step_Uphill_ReturnsDistanceAndClimb()
        {
            var (dist, climb) = Trip.Step(0f, 0f, 0f, 3f, 4f, 0f, 30f);

            Assert.Equal(5f, dist, 4);
            Assert.Equal(4f, climb, 4);
        }

        [Fact]
        public void Step_Downhill_ReturnsZeroClimb()
        {
            var (dist, climb) = Trip.Step(0f, 4f, 0f, 3f, 0f, 0f, 30f);

            Assert.Equal(5f, dist, 4);
            Assert.Equal(0f, climb);
        }

        [Fact]
        public void Step_LongerThanMax_CountsAsTeleport()
        {
            Assert.Equal((0f, 0f), Trip.Step(0f, 0f, 0f, 100f, 0f, 0f, 30f));
        }

        [Fact]
        public void Step_NaNPosition_ReturnsZero()
        {
            Assert.Equal((0f, 0f), Trip.Step(float.NaN, 0f, 0f, 1f, 0f, 0f, 30f));
        }

        [Fact]
        public void ParseKeywords_MixedInput_TrimsLowercasesAndDropsEmpty()
        {
            Assert.Equal(JerryAndFuelTank, Trip.ParseKeywords(" Jerry, ,FUEL Tank ,"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ParseKeywords_Empty_ReturnsEmpty(string csv)
        {
            Assert.Empty(Trip.ParseKeywords(csv));
        }

        [Fact]
        public void CountExtraTanks_JerryCanPart_CountsOnce()
        {
            var parts = new List<string> { "Side_Jerry Cans", "Roof_Rack", "Antenna" };

            Assert.Equal(1, Trip.CountExtraTanks(parts, Trip.ParseKeywords("jerry,fuel,tank")));
        }

        [Fact]
        public void CountExtraTanks_SameCanOnBothSides_CountsEachCopy()
        {
            var parts = new List<string> { "Side_Jerrycan", "side_jerrycan", "Rear_FuelTank", null, "" };

            Assert.Equal(3, Trip.CountExtraTanks(parts, Trip.ParseKeywords("jerry,fuel,tank")));
        }

        [Fact]
        public void CountExtraTanks_NameMatchingSeveralKeywords_CountsOnce()
        {
            var parts = new List<string> { "Jerry_FuelTank" };

            Assert.Equal(1, Trip.CountExtraTanks(parts, Trip.ParseKeywords("jerry,fuel,tank")));
        }

        [Fact]
        public void CountExtraTanks_NoKeywords_ReturnsZero()
        {
            Assert.Equal(0, Trip.CountExtraTanks(OneJerrycan, new List<string>()));
        }

        [Fact]
        public void CountExtraTanks_NullParts_ReturnsZero()
        {
            Assert.Equal(0, Trip.CountExtraTanks(null, new List<string> { "jerry" }));
        }

        [Theory]
        [InlineData(0, 10000f)]
        [InlineData(1, 14000f)]
        [InlineData(2, 18000f)]
        [InlineData(5, 18000f)]   // capped at maxTanks
        [InlineData(-1, 10000f)]
        public void RangeWithTanks_ExtraTanks_AddBonusEach(int tanks, float expected)
        {
            Assert.Equal(expected, Trip.RangeWithTanks(10000f, tanks, 0.4f, 2), 1);
        }

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(-0.5f)]
        public void RangeWithTanks_InvalidBonus_AddsNothing(float bonus)
        {
            Assert.Equal(10000f, Trip.RangeWithTanks(10000f, 2, bonus, 2));
        }
    }
}
