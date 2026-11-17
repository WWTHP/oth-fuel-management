using System;
using MelonLoader;
using UnityEngine.InputSystem;

namespace FuelManagement
{
    /// <summary>User settings, stored in UserData/MelonPreferences.cfg [FuelManagement].</summary>
    internal static class Settings
    {
        internal static MelonPreferences_Category Cat;
        internal static MelonPreferences_Entry<bool> Enabled;
        internal static MelonPreferences_Entry<string> MenuKey;
        internal static MelonPreferences_Entry<bool> ShowHud;
        internal static MelonPreferences_Entry<bool> HudAlign;
        internal static MelonPreferences_Entry<float> HudOffsetX;
        internal static MelonPreferences_Entry<float> HudOffsetY;
        internal static MelonPreferences_Entry<int> HudLayoutVersion;

        // Range
        internal static MelonPreferences_Entry<bool> AutoRange;
        internal static MelonPreferences_Entry<float> FixedRangeKm;
        internal static MelonPreferences_Entry<float> AutoPercentile;
        internal static MelonPreferences_Entry<float> AutoWinding;
        internal static MelonPreferences_Entry<float> AutoComfort;
        internal static MelonPreferences_Entry<float> RangeScale;
        internal static MelonPreferences_Entry<float> ClimbCost;

        // Extra tanks
        internal static MelonPreferences_Entry<string> TankKeywords;
        internal static MelonPreferences_Entry<float> TankBonus;
        internal static MelonPreferences_Entry<int> MaxExtraTanks;

        // Refuel and empty tank
        internal static MelonPreferences_Entry<float> RefuelRadius;
        internal static MelonPreferences_Entry<float> RefuelSeconds;
        internal static MelonPreferences_Entry<bool> LimpMode;
        internal static MelonPreferences_Entry<float> LimpMaxKmh;
        internal static MelonPreferences_Entry<float> LimpPower;
        internal static MelonPreferences_Entry<float> LimpFallbackSpeed;
        internal static MelonPreferences_Entry<float> LimpFallbackTorque;

        // State
        internal static MelonPreferences_Entry<float> SavedLevel;

        public static void Init()
        {
            Cat = MelonPreferences.CreateCategory("FuelManagement");
            Enabled = Cat.CreateEntry("Enabled", true, description: "Turn the fuel system on or off.");
            MenuKey = Cat.CreateEntry("MenuKey", "F10",
                description: "Key that opens the menu (Unity Input System key name).");
            ShowHud = Cat.CreateEntry("ShowHud", true, description: "Show the fuel gauge.");
            HudAlign = Cat.CreateEntry("HudAlign", true,
                description: "Centre the gauge just above the game's own car gauges. Off: bottom-left corner.");
            HudOffsetX = Cat.CreateEntry("HudOffsetX", 0f, description: "Moves the gauge right (px at 1080p; negative = left).");
            HudOffsetY = Cat.CreateEntry("HudOffsetY", 0f, description: "Moves the gauge up (px at 1080p; negative = down).");
            HudLayoutVersion = Cat.CreateEntry("HudLayoutVersion", 0, description: "Managed by the mod.");
            MigrateHudLayout();

            AutoRange = Cat.CreateEntry("AutoRange", false,
                description: "On: size the tank from the map (typical house → question mark → house trips use ~60% " +
                             "of a tank). Off (default): use FixedRangeKm.");
            FixedRangeKm = Cat.CreateEntry("FixedRangeKm", 8f,
                description: "Full-tank flat-road range in km (default). Also used by AutoRange before the map is measured.");
            AutoPercentile = Cat.CreateEntry("AutoPercentile", 0.75f,
                description: "AutoRange: which question marks count as 'typical' (0.75 = the nearest 75%).");
            AutoWinding = Cat.CreateEntry("AutoWinding", 1.5f,
                description: "AutoRange: how much longer the drive is than the straight line.");
            AutoComfort = Cat.CreateEntry("AutoComfort", 1.6f,
                description: "AutoRange: spare fuel factor for a typical round trip (1.6 → uses ~60% of a tank).");
            RangeScale = Cat.CreateEntry("RangeScale", 1f,
                description: "Multiplies the final range. Below 1 = harder, above 1 = easier.");
            ClimbCost = Cat.CreateEntry("ClimbCost", 8f,
                description: "Metres of flat road one metre of climbing costs. Downhill is free.");

            TankKeywords = Cat.CreateEntry("TankKeywords", "jerry,fuel,tank",
                description: "Mounted parts whose name contains one of these (comma-separated) count as extra tanks.");
            TankBonus = Cat.CreateEntry("TankBonus", 0.4f,
                description: "Range added by each extra-tank part (0.4 = +40%).");
            MaxExtraTanks = Cat.CreateEntry("MaxExtraTanks", 2, description: "Most extra-tank parts that count.");

            RefuelRadius = Cat.CreateEntry("RefuelRadius", 40f,
                description: "Within this many metres of a house (stopped or crawling) the tank refills.");
            RefuelSeconds = Cat.CreateEntry("RefuelSeconds", 6f, description: "Seconds to fill an empty tank.");
            LimpMode = Cat.CreateEntry("LimpMode", true,
                description: "Empty tank = the car crawls (worse than maximum damage) until refuelled.");
            LimpMaxKmh = Cat.CreateEntry("LimpMaxKmh", 10f,
                description: "Limp mode top speed in km/h (applied on top of the maximum-damage speed).");
            LimpPower = Cat.CreateEntry("LimpPower", 0.5f,
                description: "Limp mode engine power as a fraction of the maximum-damage torque (0.05-1). " +
                             "Lower = steep hills may become impossible.");
            LimpFallbackSpeed = Cat.CreateEntry("LimpFallbackSpeed", 0.35f,
                description: "Speed multiplier for limp mode if the game's damage curves can't be read.");
            LimpFallbackTorque = Cat.CreateEntry("LimpFallbackTorque", 0.4f,
                description: "Torque multiplier for limp mode if the game's damage curves can't be read.");

            SavedLevel = Cat.CreateEntry("SavedLevel", 1f, description: "Fuel level at last save (0-1). Managed by the mod.");
        }

        /// <summary>
        /// Layout 2 anchors the gauge on the tilt dials. Offsets tuned against the old fallback
        /// position would push it off them, so they're reset once.
        /// </summary>
        private static void MigrateHudLayout()
        {
            const int Current = 2;
            if (HudLayoutVersion.Value >= Current) return;
            if (HudOffsetX.Value != 0f || HudOffsetY.Value != 0f)
                Mod.Log.Msg($"Gauge now lines up with the tilt dials by itself; " +
                                $"reset offsets ({HudOffsetX.Value:0}, {HudOffsetY.Value:0}) to 0.");
            HudOffsetX.Value = 0f;
            HudOffsetY.Value = 0f;
            HudLayoutVersion.Value = Current;
            Save();
        }

        public static void Save() => Cat.SaveToFile(false);

        public static bool Pressed(MelonPreferences_Entry<string> entry)
        {
            var kb = Keyboard.current;
            if (kb == null || !Enum.TryParse(entry.Value, true, out Key key) || key == Key.None) return false;
            return kb[key].wasPressedThisFrame;
        }
    }
}
