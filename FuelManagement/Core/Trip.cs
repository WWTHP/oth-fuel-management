using System;
using System.Collections.Generic;

namespace FuelManagement
{
    /// <summary>Movement and part helpers that don't need the game.</summary>
    internal static class Trip
    {
        /// <summary>
        /// Distance and climb for one frame's movement. A step longer than
        /// <paramref name="maxStepM"/> (respawn, teleport, fast travel) counts as zero.
        /// </summary>
        public static (float DistanceM, float ClimbM) Step(
            float x0, float y0, float z0, float x1, float y1, float z1, float maxStepM)
        {
            double dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
            double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (double.IsNaN(d) || double.IsInfinity(d) || d > maxStepM) return (0f, 0f);
            return ((float)d, dy > 0 ? (float)dy : 0f);
        }

        /// <summary>
        /// Splits a comma-separated keyword list into trimmed, lower-case, non-empty keywords.
        /// </summary>
        public static List<string> ParseKeywords(string csv)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(csv)) return list;
            foreach (var part in csv.Split(','))
            {
                var k = part.Trim().ToLowerInvariant();
                if (k.Length > 0) list.Add(k);
            }
            return list;
        }

        /// <summary>
        /// Number of mounted parts whose name contains any keyword (case-insensitive). Every mounted
        /// copy counts (a can on each side = 2); a part counts once however many keywords it matches.
        /// The cap (MaxExtraTanks) is applied by the caller / <see cref="RangeWithTanks"/>.
        /// </summary>
        public static int CountExtraTanks(IEnumerable<string> partNames, IReadOnlyList<string> keywords)
        {
            if (partNames == null || keywords == null || keywords.Count == 0) return 0;
            int count = 0;
            foreach (var name in partNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                var lower = name.ToLowerInvariant();
                foreach (var k in keywords)
                    if (lower.Contains(k, StringComparison.Ordinal)) { count++; break; }
            }
            return count;
        }

        /// <summary>Full-tank range with <paramref name="extraTanks"/> extra tanks of <paramref name="bonusEach"/> each.</summary>
        public static float RangeWithTanks(float baseRangeM, int extraTanks, float bonusEach, int maxTanks)
        {
            int n = Math.Clamp(extraTanks, 0, Math.Max(0, maxTanks));
            float b = float.IsNaN(bonusEach) || bonusEach < 0f ? 0f : bonusEach;
            return baseRangeM * (1f + n * b);
        }
    }
}
