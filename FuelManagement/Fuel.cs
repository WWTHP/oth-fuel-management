using UnityEngine;

namespace FuelManagement
{
    /// <summary>
    /// The fuel loop for the local car: burns fuel by distance driven (climbing costs extra),
    /// refills it near a house, warns when low, and switches limp mode on when the tank
    /// runs dry. The level is shared by whatever car you drive and saved in the settings.
    /// </summary>
    internal static class Fuel
    {
        private const float MaxStepM = 30f;      // longer jumps in one frame = respawn/teleport
        private const float MaxRefuelSpeed = 4f; // m/s: roll up to the house, no need to stop dead

        public static readonly FuelTank Tank = new();
        public static bool Refuelling { get; private set; }
        public static float NearestHouseM { get; private set; } = float.PositiveInfinity;

        private static Vector3 _lastPos;
        private static bool _hasLast;
        private static float _lastSavedLevel = -1f, _nextSave, _nextHouseCheck;
        private static int _warned; // 0 none, 1 = 25%, 2 = 10%, 3 = empty

        public static void Init()
        {
            Tank.Set(Validate.InRange(Settings.SavedLevel.Value, 0f, 1f) ? Settings.SavedLevel.Value : 1f);
            _lastSavedLevel = Tank.Level;
            _warned = WarnLevel(Tank.Level);
        }

        /// <summary>Full-tank range in metres, including extra tanks and the range scale.</summary>
        public static float RangeM
        {
            get
            {
                float baseM = Settings.AutoRange.Value && Houses.CalibratedRangeM.HasValue
                    ? Houses.CalibratedRangeM.Value
                    : Mathf.Max(0.5f, Settings.FixedRangeKm.Value) * 1000f;
                float scale = Validate.InRange(Settings.RangeScale.Value, 0.05f, 20f) ? Settings.RangeScale.Value : 1f;
                return Trip.RangeWithTanks(baseM * scale, Car.ExtraTanks, Settings.TankBonus.Value,
                    Settings.MaxExtraTanks.Value);
            }
        }

        public static void Update()
        {
            if (!Car.Valid) { _hasLast = false; Refuelling = false; return; }
            var body = Car.Body;
            var pos = body.position;

            if (_hasLast)
            {
                var (dist, climb) = Trip.Step(_lastPos.x, _lastPos.y, _lastPos.z, pos.x, pos.y, pos.z, MaxStepM);
                Tank.Burn(dist, climb, RangeM, Settings.ClimbCost.Value);
            }
            _lastPos = pos;
            _hasLast = true;

            if (Time.unscaledTime >= _nextHouseCheck)
            {
                _nextHouseCheck = Time.unscaledTime + 0.25f;
                NearestHouseM = Houses.NearestHouseM(pos);
            }
            UpdateRefuel(body);
            UpdateWarnings();
            UpdateLimp();
            if (Time.unscaledTime >= _nextSave) Save();
        }

        private static void UpdateRefuel(Rigidbody body)
        {
            bool near = NearestHouseM <= Settings.RefuelRadius.Value && body.linearVelocity.magnitude <= MaxRefuelSpeed;
            if (near && Tank.Level < 1f)
            {
                if (!Refuelling) Hud.Toast("Refuelling at the house…");
                Refuelling = true;
                Tank.Refuel(Time.deltaTime / Mathf.Max(0.5f, Settings.RefuelSeconds.Value));
                if (Tank.Level >= 1f) { Hud.Toast("Tank full."); Save(); }
            }
            else Refuelling = false;
        }

        private static void UpdateWarnings()
        {
            int level = WarnLevel(Tank.Level);
            if (level > _warned)
            {
                if (level == 1) Hud.Toast("Fuel low: a quarter tank left. Head for a house.");
                else if (level == 2) Hud.Toast("Fuel very low! Find a house now.", 5f);
                else Hud.Toast("Out of fuel: the engine is limping. Crawl to a house to refuel.", 6f);
            }
            _warned = level;
        }

        private static int WarnLevel(float level) => level <= 0f ? 3 : level <= 0.1f ? 2 : level <= 0.25f ? 1 : 0;

        private static void UpdateLimp()
        {
            bool want = Settings.LimpMode.Value && Tank.IsEmpty;
            if (want && !Limp.Active && Car.Damage != null) Limp.Enter(Car.Damage);
            else if (!want && Limp.Active) Limp.Exit();
        }

        /// <summary>Called from LateUpdate so limp values win over the game's own per-frame debuffs.</summary>
        public static void LateUpdate() => Limp.Enforce();

        public static void Refill()
        {
            Tank.Refuel(1f);
            Hud.Toast("Tank filled (menu).");
            Save();
        }

        public static void Save()
        {
            _nextSave = Time.unscaledTime + 30f;
            if (Mathf.Abs(Tank.Level - _lastSavedLevel) < 0.001f) return;
            _lastSavedLevel = Tank.Level;
            Settings.SavedLevel.Value = Tank.Level;
            try { Settings.Save(); }
            catch (System.Exception ex) { Mod.Log.Warning("Saving fuel level failed: " + ex.Message); }
        }

        public static void OnSceneChanged()
        {
            Save();
            Limp.Exit();
            _hasLast = false;
            Refuelling = false;
            NearestHouseM = float.PositiveInfinity;
        }
    }
}
