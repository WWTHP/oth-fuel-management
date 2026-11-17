using System;

namespace FuelManagement
{
    /// <summary>How much fuel moves in one siphon step. Amounts are fractions of a full tank.</summary>
    internal static class SiphonMath
    {
        /// <summary>
        /// What the receiver asks for: <paramref name="chunk"/> (clamped to
        /// [0, <see cref="SiphonWire.MaxAmount"/>]), but never more than fits in its tank.
        /// </summary>
        public static float RequestSize(float receiverLevel, float chunk)
        {
            if (!Finite(receiverLevel) || !Finite(chunk) || chunk <= 0f) return 0f;
            float room = 1f - Math.Clamp(receiverLevel, 0f, 1f);
            return Math.Max(0f, Math.Min(Math.Min(chunk, SiphonWire.MaxAmount), room));
        }

        /// <summary>
        /// What the donor gives: the request, but never dipping below <paramref name="reserve"/>
        /// (clamped to [0, 1]). Invalid input gives nothing.
        /// </summary>
        public static float Grant(float donorLevel, float requested, float reserve)
        {
            if (!Finite(donorLevel) || !Finite(requested) || requested <= 0f) return 0f;
            float keep = Finite(reserve) ? Math.Clamp(reserve, 0f, 1f) : 1f;
            float spare = Math.Clamp(donorLevel, 0f, 1f) - keep;
            return Math.Max(0f, Math.Min(Math.Min(requested, SiphonWire.MaxAmount), spare));
        }

        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
