using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace FuelManagement
{
    /// <summary>
    /// Knows where the houses (Cabin points of interest = fuel stations) and the question
    /// marks (every other point of interest) are, and sizes the tank from that layout
    /// (see <see cref="RangeCalibration"/>). Points of interest stream in, so the scan
    /// repeats every few seconds and the calibration only ever grows its sample.
    /// </summary>
    internal static class Houses
    {
        private static readonly Dictionary<long, Vector3> Cabins = new();
        private static readonly Dictionary<long, (float X, float Z)> QuestionMarks = new();
        private static float _nextScan;
        private static int _lastCabinCount = -1, _lastPoiCount = -1;

        /// <summary>Calibrated full-tank range in metres (no tanks, no scale), or null until measured.</summary>
        public static float? CalibratedRangeM { get; private set; }

        public static void Reset()
        {
            Cabins.Clear();
            QuestionMarks.Clear();
            CalibratedRangeM = null;
            _lastCabinCount = _lastPoiCount = -1;
            _nextScan = 0f;
        }

        public static void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 5f;
            try { Scan(); }
            catch (System.Exception ex) { Mod.Log.Warning("House scan failed: " + ex.Message); }
        }

        private static void Scan()
        {
            var types = new Dictionary<string, int>();
            foreach (var poi in Object.FindObjectsOfType<PointOfInterest>())
            {
                if (poi == null) continue;
                string type;
                try { type = poi.Info != null ? poi.Info.Type.ToString() : "Unknown"; }
                catch { type = "Unknown"; }
                var p = poi.transform.position;
                // Key by rounded position: the same marker found again doesn't double count, and
                // markers that stream out of range are remembered.
                long key = ((long)Mathf.RoundToInt(p.x) << 32) ^ (uint)Mathf.RoundToInt(p.z);
                if (type == "Cabin" || poi.TryCast<Cabin>() != null) Cabins[key] = p;
                else QuestionMarks[key] = (p.x, p.z);
                types.TryGetValue(type, out int n);
                types[type] = n + 1;
            }

            if (Cabins.Count == _lastCabinCount && QuestionMarks.Count == _lastPoiCount) return;
            _lastCabinCount = Cabins.Count;
            _lastPoiCount = QuestionMarks.Count;

            var houses = new List<(float X, float Z)>();
            foreach (var c in Cabins.Values) houses.Add((c.x, c.z));
            var dists = RangeCalibration.NearestHouseDistances(new List<(float X, float Z)>(QuestionMarks.Values), houses);
            CalibratedRangeM = RangeCalibration.RangeFromDistances(dists,
                Settings.AutoPercentile.Value, Settings.AutoWinding.Value, Settings.AutoComfort.Value);

            var typeList = new List<string>();
            foreach (var kv in types) typeList.Add($"{kv.Key}={kv.Value}");
            string stats = dists.Count == 0 ? "no distances"
                : $"nearest-house distance p50 {RangeCalibration.Percentile(dists, 0.5f):0} m, " +
                  $"p75 {RangeCalibration.Percentile(dists, 0.75f):0} m, " +
                  $"p95 {RangeCalibration.Percentile(dists, 0.95f):0} m, max {RangeCalibration.Percentile(dists, 1f):0} m";
            Mod.Log.Msg($"POIs [{string.Join(", ", typeList)}]: {Cabins.Count} house(s), " +
                        $"{QuestionMarks.Count} question mark(s); {stats}; " +
                        $"auto range {(CalibratedRangeM.HasValue ? $"{CalibratedRangeM.Value / 1000f:0.0} km" : "n/a")}.");
        }

        /// <summary>Straight-line distance (m) from <paramref name="pos"/> to the nearest house, or +inf.</summary>
        public static float NearestHouseM(Vector3 pos)
        {
            float best = float.PositiveInfinity;
            foreach (var c in Cabins.Values)
            {
                float d = Vector3.Distance(pos, c);
                if (d < best) best = d;
            }
            return best;
        }
    }
}
