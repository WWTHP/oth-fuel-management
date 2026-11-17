using System;
using System.Buffers.Binary;
using Code = FuelManagement.SiphonWire.Code;
using Packet = FuelManagement.SiphonWire.Packet;

namespace FuelManagement.Tests
{
    public class SiphonWireTests
    {
        private static Packet Valid() => new(7u, 2, 3, 0.05f, Code.Ok);

        private static byte[] With(Action<byte[]> edit)
        {
            var b = SiphonWire.Encode(Valid());
            edit(b);
            return b;
        }

        [Fact]
        public void Encode_ThenParse_RoundTrips()
        {
            // Arrange
            var p = new Packet(123456u, 5, 9, 0.25f, Code.Reserve);

            // Act
            bool ok = SiphonWire.TryParse(SiphonWire.Encode(p), out var back);

            // Assert
            Assert.True(ok);
            Assert.Equal(p, back);
        }

        [Fact]
        public void Encode_ProducesFixedSize()
        {
            Assert.Equal(SiphonWire.PacketSize, SiphonWire.Encode(Valid()).Length);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(16)]
        [InlineData(18)]
        public void TryParse_WrongLength_Rejected(int length)
        {
            Assert.False(SiphonWire.TryParse(new byte[length], out _));
        }

        [Fact]
        public void TryParse_ZeroId_Rejected()
        {
            Assert.False(SiphonWire.TryParse(With(b => BinaryPrimitives.WriteUInt32LittleEndian(b, 0u)), out _));
        }

        [Theory]
        [InlineData(0, 3)]
        [InlineData(-1, 3)]
        [InlineData(2, 0)]
        [InlineData(2, int.MinValue)]
        [InlineData(4, 4)]
        public void TryParse_BadPlayers_Rejected(int donor, int receiver)
        {
            var b = With(x =>
            {
                BinaryPrimitives.WriteInt32LittleEndian(x.AsSpan(4), donor);
                BinaryPrimitives.WriteInt32LittleEndian(x.AsSpan(8), receiver);
            });

            Assert.False(SiphonWire.TryParse(b, out _));
        }

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        [InlineData(-0.01f)]
        [InlineData(0.26f)]
        [InlineData(1e30f)]
        public void TryParse_BadAmount_Rejected(float amount)
        {
            var b = With(x => BinaryPrimitives.WriteSingleLittleEndian(x.AsSpan(12), amount));

            Assert.False(SiphonWire.TryParse(b, out _));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(0.25f)]
        public void TryParse_AmountAtBounds_Accepted(float amount)
        {
            var b = With(x => BinaryPrimitives.WriteSingleLittleEndian(x.AsSpan(12), amount));

            Assert.True(SiphonWire.TryParse(b, out var p));
            Assert.Equal(amount, p.Amount);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(255)]
        public void TryParse_UnknownCode_Rejected(byte code)
        {
            Assert.False(SiphonWire.TryParse(With(x => x[16] = code), out _));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Hello_RoundTrips(bool reply)
        {
            Assert.True(SiphonWire.TryParseHello(SiphonWire.EncodeHello(reply), out bool back));
            Assert.Equal(reply, back);
        }

        [Theory]
        [InlineData(new byte[0])]
        [InlineData(new byte[] { 0, 0 })]
        [InlineData(new byte[] { 2 })]
        [InlineData(new byte[] { 255 })]
        public void TryParseHello_Malformed_Rejected(byte[] data)
        {
            Assert.False(SiphonWire.TryParseHello(data, out _));
        }
    }

    public class SiphonMathTests
    {
        [Fact]
        public void RequestSize_RoomInTank_ReturnsChunk()
        {
            Assert.Equal(0.05f, SiphonMath.RequestSize(0.5f, 0.05f), 5);
        }

        [Fact]
        public void RequestSize_NearlyFull_ReturnsOnlyRoomLeft()
        {
            Assert.Equal(0.02f, SiphonMath.RequestSize(0.98f, 0.05f), 5);
        }

        [Fact]
        public void RequestSize_HugeChunk_ClampedToMaxAmount()
        {
            Assert.Equal(SiphonWire.MaxAmount, SiphonMath.RequestSize(0f, 5f));
        }

        [Theory]
        [InlineData(1f, 0.05f)]
        [InlineData(1.5f, 0.05f)]
        [InlineData(0.5f, 0f)]
        [InlineData(0.5f, -1f)]
        [InlineData(float.NaN, 0.05f)]
        [InlineData(0.5f, float.NaN)]
        [InlineData(0.5f, float.PositiveInfinity)]
        public void RequestSize_FullOrInvalid_ReturnsZero(float level, float chunk)
        {
            Assert.Equal(0f, SiphonMath.RequestSize(level, chunk));
        }

        [Fact]
        public void Grant_PlentyAboveReserve_GivesRequest()
        {
            Assert.Equal(0.05f, SiphonMath.Grant(0.8f, 0.05f, 0.15f), 5);
        }

        [Fact]
        public void Grant_CloseToReserve_GivesOnlyTheSpare()
        {
            Assert.Equal(0.02f, SiphonMath.Grant(0.17f, 0.05f, 0.15f), 5);
        }

        [Theory]
        [InlineData(0.1f, 0.05f, 0.15f)]   // below reserve
        [InlineData(0.15f, 0.05f, 0.15f)]  // exactly at reserve
        [InlineData(0f, 0.05f, 0f)]        // empty, no reserve
        [InlineData(0.8f, 0.05f, 1f)]      // keeps everything
        public void Grant_NothingSpare_GivesZero(float level, float requested, float reserve)
        {
            Assert.Equal(0f, SiphonMath.Grant(level, requested, reserve));
        }

        [Fact]
        public void Grant_HugeRequest_ClampedToMaxAmount()
        {
            Assert.Equal(SiphonWire.MaxAmount, SiphonMath.Grant(1f, 10f, 0f));
        }

        [Fact]
        public void Grant_NegativeReserve_TreatedAsZero()
        {
            Assert.Equal(0.05f, SiphonMath.Grant(0.05f, 0.1f, -1f), 5);
        }

        [Theory]
        [InlineData(float.NaN, 0.05f, 0.1f)]
        [InlineData(0.5f, float.NaN, 0.1f)]
        [InlineData(0.5f, 0f, 0.1f)]
        [InlineData(0.5f, -0.1f, 0.1f)]
        [InlineData(0.5f, 0.05f, float.NaN)]   // unknown reserve = keep everything
        public void Grant_InvalidInput_GivesZero(float level, float requested, float reserve)
        {
            Assert.Equal(0f, SiphonMath.Grant(level, requested, reserve));
        }
    }
}
