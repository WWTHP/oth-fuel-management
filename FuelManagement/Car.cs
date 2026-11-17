using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace FuelManagement
{
    /// <summary>
    /// Finds the car the local player drives (the one with input authority) and the
    /// pieces the fuel system needs from it: body, damage system, mounted parts.
    /// Only the local car is ever modified; other players' cars are left to their own game.
    /// </summary>
    internal static class Car
    {
        public static Rigidbody Body { get; private set; }
        public static CarDamageSystem Damage { get; private set; }
        public static int ExtraTanks { get; private set; }

        private static VehicleInput _input;
        private static float _nextScan, _nextParts;
        private static string _lastParts = "";

        public static bool Valid => _input != null && !_input.WasCollected && Body != null && !Body.WasCollected;

        public static void Update()
        {
            if (Valid && Time.unscaledTime < _nextScan)
            {
                if (Time.unscaledTime >= _nextParts) ScanParts();
                return;
            }
            _nextScan = Time.unscaledTime + 1f;
            var old = _input;
            Find();
            if (_input != null && _input != old)
            {
                Mod.Log.Msg($"Local car found: {_input.transform.root.name} " +
                            $"(damage system: {(Damage != null ? "yes" : "no")}).");
                _lastParts = "";
                ScanParts();
            }
        }

        private static void Find()
        {
            _input = null; Body = null; Damage = null;
            foreach (var vi in Object.FindObjectsOfType<VehicleInput>())
            {
                if (vi == null) continue;
                try
                {
                    var obj = vi.Object;
                    if (obj == null || !obj.HasInputAuthority) continue;
                }
                catch { continue; } // not spawned on the network yet
                var body = vi.GetComponentInParent<Rigidbody>();
                if (body == null) body = vi.transform.root.GetComponentInChildren<Rigidbody>();
                if (body == null) continue;
                _input = vi;
                Body = body;
                Damage = vi.transform.root.GetComponentInChildren<CarDamageSystem>();
                return;
            }
        }

        /// <summary>Counts mounted extra-tank cosmetics (e.g. side jerry cans). Logs the part list on change.</summary>
        private static void ScanParts()
        {
            _nextParts = Time.unscaledTime + 2f;
            if (!Valid) return;
            var names = new List<string>();
            try
            {
                foreach (var part in _input.transform.root.GetComponentsInChildren<VehicleUpgradeObject>(false))
                {
                    if (part == null || !part.gameObject.activeInHierarchy) continue;
                    var so = part.VehiclePartReference;
                    names.Add(so != null ? so.name : part.gameObject.name);
                }
            }
            catch (System.Exception ex)
            {
                Mod.Log.Warning("Part scan failed: " + ex.Message);
                return;
            }
            names.Sort();
            var joined = string.Join(", ", names);
            int tanks = Trip.CountExtraTanks(names, Trip.ParseKeywords(Settings.TankKeywords.Value));
            if (joined != _lastParts || tanks != ExtraTanks)
            {
                _lastParts = joined;
                ExtraTanks = tanks;
                Mod.Log.Msg($"Mounted parts: [{joined}] → {tanks} extra tank part(s).");
            }
        }
    }
}
