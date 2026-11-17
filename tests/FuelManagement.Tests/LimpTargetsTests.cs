namespace FuelManagement.Tests
{
    public class LimpTargetsTests
    {
        [Fact]
        public void Speed_DamagedSpeedAboveCap_ReturnsCap()
        {
            // Arrange: 30 m/s × 0.5 = 15 m/s, cap 18 km/h = 5 m/s

            // Act
            float s = LimpTargets.Speed(30f, 0.5f, 18f);

            // Assert
            Assert.Equal(5f, s, 4);
        }

        [Fact]
        public void Speed_DamagedSpeedBelowCap_ReturnsDamagedSpeed()
        {
            Assert.Equal(3f, LimpTargets.Speed(30f, 0.1f, 36f), 4);
        }

        [Fact]
        public void Speed_TinyCap_ClampedToMinimumCrawl()
        {
            Assert.Equal(LimpTargets.MinSpeedMs, LimpTargets.Speed(30f, 0.5f, 0.1f));
        }

        [Theory]
        [InlineData(float.NaN, 0.5f)]
        [InlineData(0f, 0.5f)]
        [InlineData(-5f, 0.5f)]
        [InlineData(30f, float.NaN)]
        [InlineData(30f, 0f)]
        [InlineData(30f, 1.5f)]
        public void Speed_InvalidBaseOrMultiplier_FallsBackToCap(float baseSpeed, float mul)
        {
            Assert.Equal(10f, LimpTargets.Speed(baseSpeed, mul, 36f), 4);
        }

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(0f)]
        [InlineData(-10f)]
        public void Speed_InvalidCap_UsesDamagedSpeedOnly(float cap)
        {
            Assert.Equal(15f, LimpTargets.Speed(30f, 0.5f, cap), 4);
        }

        [Fact]
        public void Speed_NothingValid_ReturnsNoLimit()
        {
            Assert.True(float.IsPositiveInfinity(LimpTargets.Speed(float.NaN, 0.5f, float.NaN)));
        }

        [Fact]
        public void Torque_ValidInputs_MultipliesDamageAndPower()
        {
            Assert.Equal(100f, LimpTargets.Torque(400f, 0.5f, 0.5f), 4);
        }

        [Theory]
        [InlineData(0f, 10f)]       // below MinPower → 0.05
        [InlineData(3f, 200f)]      // above 1 → 1
        [InlineData(float.NaN, 200f)]
        public void Torque_OutOfRangePower_IsClamped(float power, float expected)
        {
            Assert.Equal(expected, LimpTargets.Torque(400f, 0.5f, power), 4);
        }

        [Theory]
        [InlineData(float.NaN, 0.5f)]
        [InlineData(-1f, 0.5f)]
        [InlineData(400f, float.NaN)]
        [InlineData(400f, 2f)]
        public void Torque_InvalidBaseOrMultiplier_ReturnsBaseUnchanged(float baseTorque, float mul)
        {
            float t = LimpTargets.Torque(baseTorque, mul, 0.5f);

            Assert.Equal(baseTorque, t);
        }
    }
}
