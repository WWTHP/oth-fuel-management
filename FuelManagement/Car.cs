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

        /// <summary>The local player's id (Fusion PlayerRef.Raw), 0 if unknown.</summary>
        public static int Player { get; private set; }

        /// <summary>Speed of the local car in m/s (0 without a car).</summary>
        public static float Speed => Valid ? Body.linearVelocity.magnitude : 0f;

        private sealed class Other
        {
            public int Player;
            public Rigidbody Body;
        }

        private static readonly List<Other> Others = new();

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

        /// <summary>Finds the local car and, every scan, every other player's car.</summary>
        private static void Find()
        {
            _input = null; Body = null; Damage = null; Player = 0;
            Others.Clear();
            foreach (var vi in Object.FindObjectsOfType<VehicleInput>())
            {
                if (vi == null) continue;
                bool local;
                int player;
                try
                {
                    var obj = vi.Object;
                    if (obj == null) continue;
                    local = obj.HasInputAuthority;
                    player = obj.InputAuthority.Raw;
                }
                catch { continue; } // not spawned on the network yet
                var body = vi.GetComponentInParent<Rigidbody>();
                if (body == null) body = vi.transform.root.GetComponentInChildren<Rigidbody>();
                if (body == null) continue;
                if (!local)
                {
                    if (player > 0) Others.Add(new Other { Player = player, Body = body });
                    continue;
                }
                if (_input != null) continue; // first local car wins
                _input = vi;
                Body = body;
                Player = player;
                Damage = vi.transform.root.GetComponentInChildren<CarDamageSystem>();
            }
        }

        /// <summary>
        /// The other player's car closest to the local car within <paramref name="radius"/> metres.
        /// Returns false when there is none.
        /// </summary>
        public static bool NearestOther(float radius, out int player, out float distance)
        {
            player = 0;
            distance = float.PositiveInfinity;
            if (!Valid) return false;
            var me = Body.transform.position;
            foreach (var o in Others)
            {
                if (o.Body == null || o.Body.WasCollected) continue;
                float d = Vector3.Distance(me, o.Body.transform.position);
                if (d <= radius && d < distance) { distance = d; player = o.Player; }
            }
            return player > 0;
        }

        /// <summary>Distance from the local car to <paramref name="player"/>'s car (infinity if unknown).</summary>
        public static float DistanceTo(int player)
        {
            if (!Valid) return float.PositiveInfinity;
            foreach (var o in Others)
                if (o.Player == player && o.Body != null && !o.Body.WasCollected)
                    return Vector3.Distance(Body.transform.position, o.Body.transform.position);
            return float.PositiveInfinity;
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
