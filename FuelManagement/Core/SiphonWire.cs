using System;
using System.Buffers.Binary;

namespace FuelManagement
{
    /// <summary>
    /// Wire format of the siphon messages (the bytes after the message-type byte). Packets come
    /// from other players, so parsing rejects anything malformed instead of coercing it.
    ///
    /// Hello: 1 byte, 0 = announce, 1 = reply.
    /// Siphon (request and give): id u32, donor i32, receiver i32, amount f32, code u8 = 17 bytes,
    /// little endian. Amount is a fraction of a full tank.
    /// </summary>
    internal static class SiphonWire
    {
        public const int HelloSize = 1;
        public const int PacketSize = 17;

        /// <summary>Largest amount one packet may move (a quarter of a tank).</summary>
        public const float MaxAmount = 0.25f;

        /// <summary>Why a donor gave what it gave. Requests always carry <see cref="Ok"/>.</summary>
        public enum Code : byte
        {
            Ok = 0,
            Refused = 1,   // donor switched siphoning off (or the fuel system)
            TooFar = 2,    // donor's game doesn't see the receiver's car close by
            Reserve = 3,   // donor is at or below the fuel they keep for themselves
            NoCar = 4,     // donor has no car right now
        }

        internal readonly record struct Packet(uint Id, int Donor, int Receiver, float Amount, Code Code);

        public static byte[] EncodeHello(bool reply) => new[] { reply ? (byte)1 : (byte)0 };

        public static bool TryParseHello(ReadOnlySpan<byte> data, out bool reply)
        {
            reply = false;
            if (data.Length != HelloSize || data[0] > 1) return false;
            reply = data[0] == 1;
            return true;
        }

        public static byte[] Encode(Packet p)
        {
            var b = new byte[PacketSize];
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(0), p.Id);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(4), p.Donor);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(8), p.Receiver);
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(12), p.Amount);
            b[16] = (byte)p.Code;
            return b;
        }

        /// <summary>
        /// Parses a siphon packet. Rejects: wrong length, id 0, non-positive or equal player ids,
        /// an amount that isn't finite or lies outside [0, <see cref="MaxAmount"/>], unknown codes.
        /// </summary>
        public static bool TryParse(ReadOnlySpan<byte> data, out Packet packet)
        {
            packet = default;
            if (data.Length != PacketSize) return false;
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(data);
            int donor = BinaryPrimitives.ReadInt32LittleEndian(data[4..]);
            int receiver = BinaryPrimitives.ReadInt32LittleEndian(data[8..]);
            float amount = BinaryPrimitives.ReadSingleLittleEndian(data[12..]);
            byte code = data[16];
            if (id == 0 || donor <= 0 || receiver <= 0 || donor == receiver) return false;
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f || amount > MaxAmount) return false;
            if (code > (byte)Code.NoCar) return false;
            packet = new Packet(id, donor, receiver, amount, (Code)code);
            return true;
        }
    }
}
