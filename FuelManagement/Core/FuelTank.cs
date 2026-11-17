using System;

namespace FuelManagement
{
    /// <summary>
    /// The fuel state of one car, as a fraction of a full tank. All quantities are
    /// expressed in metres of flat-road range, so tuning stays in units a player can
    /// reason about ("a full tank goes about 9 km").
    /// </summary>
    internal sealed class FuelTank
    {
        /// <summary>Fill level of the tank, 0 (empty) to 1 (full).</summary>
        public float Level { get; private set; }

        public FuelTank(float level = 1f)
        {
            Level = Clamp01(level);
        }

        public bool IsEmpty => Level <= 0f;

        /// <summary>
        /// Burns fuel for driving <paramref name="distanceM"/> metres while climbing
        /// <paramref name="climbM"/> metres. <paramref name="rangeM"/> is the flat-road range
        /// of a full tank (including extra tanks); <paramref name="climbCost"/> is how many
        /// metres of flat road one metre of climb is worth. Descending is free.
        /// Returns the fraction of a tank burned. Invalid inputs burn nothing.
        /// </summary>
        public float Burn(float distanceM, float climbM, float rangeM, float climbCost)
        {
            if (!Finite(distanceM) || !Finite(climbM) || !Finite(rangeM) || !Finite(climbCost)) return 0f;
            if (rangeM <= 0f || distanceM < 0f) return 0f;
            float effective = distanceM + Math.Max(0f, climbM) * Math.Max(0f, climbCost);
            float burned = Math.Min(Level, effective / rangeM);
            Level -= burned;
            if (Level < 1e-6f) Level = 0f;
            return burned;
        }

        /// <summary>Adds up to <paramref name="fraction"/> of a tank. Returns what was added.</summary>
        public float Refuel(float fraction)
        {
            if (!Finite(fraction) || fraction <= 0f) return 0f;
            float added = Math.Min(1f - Level, fraction);
            Level += added;
            return added;
        }

        /// <summary>Sets the level (e.g. restored from settings), clamped to [0, 1].</summary>
        public void Set(float level) => Level = Finite(level) ? Clamp01(level) : Level;

        /// <summary>Remaining flat-road range in metres for a tank with full range <paramref name="rangeM"/>.</summary>
        public float RemainingM(float rangeM) => Finite(rangeM) && rangeM > 0f ? Level * rangeM : 0f;

        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        private static float Clamp01(float f) => float.IsNaN(f) ? 0f : Math.Clamp(f, 0f, 1f);
    }
}
