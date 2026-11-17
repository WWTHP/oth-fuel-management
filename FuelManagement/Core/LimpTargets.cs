using System;

namespace FuelManagement
{
    /// <summary>
    /// The speed limit and engine torque an empty-tank car gets: the game's maximum-damage
    /// values made harsher by a top-speed cap and a power factor. Pure maths, tested.
    /// </summary>
    internal static class LimpTargets
    {
        /// <summary>Never limit below this (m/s), so the car can always creep to a house.</summary>
        public const float MinSpeedMs = 0.5f;

        /// <summary>Weakest allowed power factor, so the car can still move at all.</summary>
        public const float MinPower = 0.05f;

        /// <summary>
        /// Speed limit in m/s: <paramref name="baseSpeedMs"/> × <paramref name="damageMul"/>, capped at
        /// <paramref name="maxKmh"/>. An invalid base or multiplier falls back to the cap alone;
        /// an invalid cap means no cap.
        /// </summary>
        public static float Speed(float baseSpeedMs, float damageMul, float maxKmh)
        {
            float cap = Finite(maxKmh) && maxKmh > 0f ? maxKmh / 3.6f : float.PositiveInfinity;
            float damaged = Finite(baseSpeedMs) && baseSpeedMs > 0f && ValidMul(damageMul)
                ? baseSpeedMs * damageMul
                : float.PositiveInfinity;
            float speed = Math.Min(damaged, cap);
            return float.IsPositiveInfinity(speed) ? float.PositiveInfinity : Math.Max(MinSpeedMs, speed);
        }

        /// <summary>
        /// Torque: <paramref name="baseTorque"/> × <paramref name="damageMul"/> × <paramref name="power"/>
        /// (power clamped to [<see cref="MinPower"/>, 1]). Invalid inputs leave the base unchanged.
        /// </summary>
        public static float Torque(float baseTorque, float damageMul, float power)
        {
            if (!Finite(baseTorque) || baseTorque <= 0f || !ValidMul(damageMul)) return baseTorque;
            float p = Finite(power) ? Math.Clamp(power, MinPower, 1f) : 1f;
            return baseTorque * damageMul * p;
        }

        private static bool ValidMul(float m) => Finite(m) && m > 0f && m <= 1f;
        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
