using MelonLoader;
using UnityEngine;

namespace FuelManagement
{
    /// <summary>
    /// Settings menu (IMGUI). <see cref="DrawBody"/> is the settings hub tab (registered in Mod);
    /// without the hub, MenuKey (default F10) opens it in a window of its own. Changes apply live and
    /// are saved when the window closes or the hub tab is left.
    /// </summary>
    internal static class Menu
    {
        public static bool IsOpen { get; private set; }

        private const float W = 480f, RowH = 26f, Pad = 12f, LabelW = 230f, ValueW = 60f;
        private static Vector2 _scroll;
        private static float _contentH = 700f, _y, _w = W - 24f;
        private static bool _dirty;
        private static CursorLockMode _prevLock;
        private static bool _prevVisible;

        public static void Update()
        {
            if (!HubTab.HubPresent && Settings.Pressed(Settings.MenuKey)) Toggle();
            if (IsOpen && HubTab.HubPresent) Toggle(); // the hub took over
            if (IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private static void Toggle()
        {
            IsOpen = !IsOpen;
            if (IsOpen)
            {
                _prevLock = Cursor.lockState;
                _prevVisible = Cursor.visible;
                return;
            }
            SaveIfDirty();
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
        }

        public static void SaveIfDirty()
        {
            if (!_dirty) return;
            Settings.Save();
            _dirty = false;
        }

        public static void ResetDefaults()
        {
            Settings.ResetToDefaults();
            _dirty = true;
        }

        /// <summary>Standalone window (no hub). Call with GUI.matrix already scaled.</summary>
        public static void Draw(float scale)
        {
            if (!IsOpen) return;
            float viewH = Mathf.Min(Screen.height / scale - 80f, _contentH + 90f);
            var win = new Rect(60f, 40f, W, viewH);
            GUI.Box(win, "");
            GUI.Box(win, "");
            GUI.Label(new Rect(win.x + Pad, win.y + 8f, W - 2 * Pad, RowH), $"<b>Fuel management</b>   ({Settings.MenuKey.Value} to close)");
            var view = new Rect(win.x, win.y + 38f, W, viewH - 38f - 44f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, W - 24f, _contentH));
            try { _contentH = DrawBody(W - 24f); }
            finally { GUI.EndScrollView(); }
            float by = win.yMax - 36f;
            if (GUI.Button(new Rect(win.x + Pad, by, 140f, 26f), "Reset defaults")) ResetDefaults();
            if (GUI.Button(new Rect(win.xMax - Pad - 110f, by, 110f, 26f), "Close")) Toggle();
        }

        /// <summary>Menu content at (0, 0), <paramref name="width"/> wide. Returns the height used.</summary>
        public static float DrawBody(float width)
        {
            _w = width;
            _y = 4f;

            Section("Tank");
            Info($"Full tank: {Fuel.RangeM / 1000f:0.0} km " +
                 $"({(Settings.AutoRange.Value && Houses.CalibratedRangeM.HasValue ? "auto from map" : "fixed")}" +
                 $"{(Car.ExtraTanks > 0 ? $", {Car.ExtraTanks} extra tank part(s)" : "")}).   Fuel {Fuel.Tank.Level * 100f:0}%");
            if (Button("Fill tank")) Fuel.Refill();
            Toggle(Settings.Enabled, "Fuel system on");
            Toggle(Settings.AutoRange, "Size tank from the map");
            Slider(Settings.RangeScale, "Range scale", 0.3f, 3f, "0.00");
            if (!Settings.AutoRange.Value) Slider(Settings.FixedRangeKm, "Fixed range (km)", 1f, 40f, "0.0");
            Slider(Settings.ClimbCost, "Climb cost (m per m)", 0f, 30f, "0");
            Slider(Settings.TankBonus, "Range per extra tank", 0f, 1.5f, "0.00");

            Section("Gauge");
            Toggle(Settings.ShowHud, "Show gauge");
            Toggle(Settings.GaugeDial, "Dial gauge (off: bar)");
            Toggle(Settings.HudAlign, "Line gauge up with the game's gauges");
            Slider(Settings.HudOffsetX, "Gauge offset right (px)", -400f, 400f, "0");
            Slider(Settings.HudOffsetY, "Gauge offset up (px)", -200f, 400f, "0");

            Section("Empty tank");
            Toggle(Settings.LimpMode, "Limp mode when empty");
            if (Settings.LimpMode.Value)
            {
                Slider(Settings.LimpMaxKmh, "Limp top speed (km/h)", 3f, 40f, "0");
                Slider(Settings.LimpPower, "Limp power (x max-damage torque)", 0.1f, 1f, "0.00");
            }

            Section("Siphon (multiplayer)");
            Info($"Hold {Settings.SiphonKey.Value} stopped next to another car. Link: {Net.Describe()}");
            Toggle(Settings.AllowSiphon, "Let other players siphon from me");
            Slider(Settings.SiphonReserve, "Keep for myself (fraction of tank)", 0f, 0.5f, "0.00");

            if (CrashGuard.DisabledStages.Count > 0)
            {
                Section("Crash protection");
                Info("Switched off after the game crashed in it: " + string.Join(", ", CrashGuard.DisabledStages));
                if (Button("Re-enable (restart the game)")) CrashGuard.ReEnableAll();
            }
            return _y + Pad;
        }

        private static void Section(string title)
        {
            _y += 8f;
            GUI.Label(new Rect(Pad, _y, _w - 2 * Pad, RowH), $"<b><color=#9fd3ff>{title}</color></b>");
            _y += RowH;
        }

        private static void Info(string text)
        {
            GUI.Label(new Rect(Pad + 8f, _y, _w - 2 * Pad - 8f, RowH), text);
            _y += RowH;
        }

        private static bool Button(string text)
        {
            bool hit = GUI.Button(new Rect(Pad + 8f, _y + 2f, 200f, RowH - 4f), text);
            _y += RowH;
            return hit;
        }

        private static void Toggle(MelonPreferences_Entry<bool> e, string label)
        {
            bool v = GUI.Toggle(new Rect(Pad + 8f, _y, _w - 2 * Pad, RowH), e.Value, " " + label);
            if (v != e.Value) { e.Value = v; _dirty = true; }
            _y += RowH;
        }

        private static void Slider(MelonPreferences_Entry<float> e, string label, float min, float max, string fmt)
        {
            GUI.Label(new Rect(Pad + 8f, _y, LabelW, RowH), label);
            float sx = Pad + 8f + LabelW;
            float sw = _w - sx - ValueW - Pad;
            float v = GUI.HorizontalSlider(new Rect(sx, _y + 8f, sw, RowH - 8f), e.Value, min, max);
            GUI.Label(new Rect(sx + sw + 8f, _y, ValueW, RowH),
                e.Value.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture));
            if (Mathf.Abs(v - e.Value) > 1e-4f) { e.Value = v; _dirty = true; }
            _y += RowH;
        }
    }
}
