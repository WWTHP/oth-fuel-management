using System;
using System.Collections.Generic;

namespace FuelManagement
{
    /// <summary>
    /// Picks a full-tank range from the map's layout, so that "house → question mark → house"
    /// is relaxed for most question marks and only the remote ones need planning.
    ///
    /// For every question mark we take the straight-line distance to its nearest house.
    /// The <c>percentile</c> of those distances (default 75th) is a "typical" trip leg.
    /// Range = leg × 2 (there and back) × winding (roads aren't straight) × comfort
    /// (spare fuel so the typical trip uses about 1/comfort of a tank).
    ///
    /// With the defaults (p75, winding 1.5, comfort 1.6) a typical round trip uses ~60%
    /// of a tank, a p75 trip with a detour still makes it, and a question mark twice as
    /// far from any house as typical is out of reach without jerry cans or a house hop.
    /// </summary>
    internal static class RangeCalibration
    {
        public const float MinRangeM = 2000f;
        public const float MaxRangeM = 60000f;
        public const int MinSamples = 3;

        /// <summary>Straight-line (XZ) distance from each point to its nearest house.</summary>
        public static List<float> NearestHouseDistances(
            IReadOnlyList<(float X, float Z)> questionMarks, IReadOnlyList<(float X, float Z)> houses)
        {
            var result = new List<float>(questionMarks?.Count ?? 0);
            if (questionMarks == null || houses == null || houses.Count == 0) return result;
            foreach (var q in questionMarks)
            {
                double best = double.MaxValue;
                foreach (var h in houses)
                {
                    double dx = q.X - h.X, dz = q.Z - h.Z;
                    double d = dx * dx + dz * dz;
                    if (d < best) best = d;
                }
                float dist = (float)Math.Sqrt(best);
                if (!float.IsNaN(dist) && !float.IsInfinity(dist)) result.Add(dist);
            }
            return result;
        }

        /// <summary>
        /// Linear-interpolated percentile (0..1) of <paramref name="values"/>; NaN when empty.
        /// </summary>
        public static float Percentile(IReadOnlyList<float> values, float p)
        {
            if (values == null || values.Count == 0) return float.NaN;
            var sorted = new List<float>(values);
            sorted.Sort();
            p = Math.Clamp(float.IsNaN(p) ? 0.5f : p, 0f, 1f);
            float pos = p * (sorted.Count - 1);
            int lo = (int)Math.Floor(pos);
            int hi = Math.Min(lo + 1, sorted.Count - 1);
            return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
        }

        /// <summary>
        /// Full-tank range in metres from the nearest-house distances, or null when there are
        /// fewer than <see cref="MinSamples"/> samples (caller falls back to a fixed range).
        /// Clamped to [<see cref="MinRangeM"/>, <see cref="MaxRangeM"/>].
        /// </summary>
        public static float? RangeFromDistances(IReadOnlyList<float> nearestHouseDistances,
            float percentile, float winding, float comfort)
        {
            if (nearestHouseDistances == null || nearestHouseDistances.Count < MinSamples) return null;
            float leg = Percentile(nearestHouseDistances, percentile);
            if (float.IsNaN(leg)) return null;
            float w = Positive(winding, 1.5f), c = Positive(comfort, 1.6f);
            return Math.Clamp(leg * 2f * w * c, MinRangeM, MaxRangeM);
        }

        private static float Positive(float f, float fallback) =>
            float.IsNaN(f) || float.IsInfinity(f) || f <= 0f ? fallback : f;
    }
}
