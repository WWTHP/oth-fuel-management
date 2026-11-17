namespace FuelManagement.Tests
{
    public class FuelTankTests
    {
        [Fact]
        public void Burn_FlatDistance_BurnsDistanceOverRange()
        {
            // Arrange
            var tank = new FuelTank(1f);

            // Act
            float burned = tank.Burn(2500f, 0f, 10000f, 8f);

            // Assert
            Assert.Equal(0.25f, burned, 5);
            Assert.Equal(0.75f, tank.Level, 5);
        }

        [Fact]
        public void Burn_Climb_AddsClimbCostPerMetre()
        {
            var tank = new FuelTank(1f);

            tank.Burn(1000f, 100f, 10000f, 8f); // 1000 + 800 effective metres

            Assert.Equal(0.82f, tank.Level, 5);
        }

        [Fact]
        public void Burn_Descent_IsNotRefunded()
        {
            var tank = new FuelTank(1f);

            tank.Burn(1000f, -100f, 10000f, 8f);

            Assert.Equal(0.9f, tank.Level, 5);
        }

        [Fact]
        public void Burn_MoreThanLeft_EmptiesTankAndReturnsWhatWasLeft()
        {
            var tank = new FuelTank(0.1f);

            float burned = tank.Burn(5000f, 0f, 10000f, 0f);

            Assert.Equal(0.1f, burned, 5);
            Assert.True(tank.IsEmpty);
        }

        [Theory]
        [InlineData(float.NaN, 0f, 10000f, 1f)]
        [InlineData(100f, float.PositiveInfinity, 10000f, 1f)]
        [InlineData(100f, 0f, 0f, 1f)]
        [InlineData(100f, 0f, -5f, 1f)]
        [InlineData(-100f, 0f, 10000f, 1f)]
        [InlineData(100f, 0f, 10000f, float.NaN)]
        public void Burn_InvalidInput_BurnsNothing(float distance, float climb, float range, float climbCost)
        {
            var tank = new FuelTank(0.5f);

            float burned = tank.Burn(distance, climb, range, climbCost);

            Assert.Equal(0f, burned);
            Assert.Equal(0.5f, tank.Level);
        }

        [Fact]
        public void Burn_NegativeClimbCost_TreatedAsZero()
        {
            var tank = new FuelTank(1f);

            tank.Burn(1000f, 100f, 10000f, -5f);

            Assert.Equal(0.9f, tank.Level, 5);
        }

        [Fact]
        public void Refuel_PartlyFull_AddsUpToFull()
        {
            var tank = new FuelTank(0.7f);

            float added = tank.Refuel(0.5f);

            Assert.Equal(0.3f, added, 5);
            Assert.Equal(1f, tank.Level);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void Refuel_InvalidAmount_AddsNothing(float amount)
        {
            var tank = new FuelTank(0.4f);

            Assert.Equal(0f, tank.Refuel(amount));
            Assert.Equal(0.4f, tank.Level);
        }

        [Theory]
        [InlineData(-0.5f, 0f)]
        [InlineData(1.5f, 1f)]
        [InlineData(float.NaN, 0f)]
        public void Constructor_OutOfRange_Clamps(float level, float expected)
        {
            Assert.Equal(expected, new FuelTank(level).Level);
        }

        [Fact]
        public void Set_NaN_KeepsCurrentLevel()
        {
            var tank = new FuelTank(0.6f);

            tank.Set(float.NaN);

            Assert.Equal(0.6f, tank.Level);
        }

        [Fact]
        public void Set_ValidLevel_ClampsAndApplies()
        {
            var tank = new FuelTank(0.6f);

            tank.Set(2f);

            Assert.Equal(1f, tank.Level);
        }

        [Theory]
        [InlineData(10000f, 5000f)]
        [InlineData(0f, 0f)]
        [InlineData(float.NaN, 0f)]
        public void RemainingM_HalfTank_ReturnsHalfRange(float range, float expected)
        {
            Assert.Equal(expected, new FuelTank(0.5f).RemainingM(range));
        }
    }
}
