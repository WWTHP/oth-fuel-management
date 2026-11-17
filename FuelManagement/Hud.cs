using MelonLoader;
using UnityEngine;

namespace FuelManagement
{
    /// <summary>
    /// IMGUI: the fuel gauge (level, remaining range, nearest house), centred just above the
    /// game's own gauges (see <see cref="HudAnchor"/>), and the settings menu (MenuKey).
    /// No pop-up messages: the gauge is the only in-game indication.
    /// </summary>
    internal static class Hud
    {
        private const float GaugeW = 260f, GaugeH = 52f, Gap = 12f;
        private static bool _menuOpen;
        private static Rect _menuRect = new(60f, 120f, 420f, 0f);

        public static void Update()
        {
            if (Settings.Pressed(Settings.MenuKey)) _menuOpen = !_menuOpen;
            if (Settings.Enabled.Value && Settings.ShowHud.Value && Settings.HudAlign.Value) HudAnchor.Update();
        }

        public static void Draw()
        {
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            if (Settings.Enabled.Value && Settings.ShowHud.Value && Car.Valid) DrawGauge(scale);
            if (_menuOpen) _menuRect = GUILayout.Window(0x4655454C, _menuRect, (GUI.WindowFunction)MenuWindow, "Fuel management");
            GUI.matrix = Matrix4x4.identity;
        }

        private static void DrawGauge(float scale)
        {
            HudLayout.Box? cluster = null;
            if (Settings.HudAlign.Value && HudAnchor.Cluster.HasValue)
            {
                var c = HudAnchor.Cluster.Value;
                cluster = new HudLayout.Box(c.X / scale, c.Y / scale, c.W / scale, c.H / scale);
            }
            var b = HudLayout.Place(cluster, GaugeW, GaugeH, Gap, Screen.width / scale, Screen.height / scale,
                Settings.HudOffsetX.Value, -Settings.HudOffsetY.Value);
            var r = new Rect(b.X, b.Y, b.W, b.H);

            float level = Fuel.Tank.Level;
            GUI.Box(r, "");
            var bar = new Rect(r.x + 10f, r.y + 8f, r.width - 20f, 12f);
            GUI.Box(bar, "");
            var old = GUI.color;
            GUI.color = level > 0.25f ? new Color(0.45f, 0.85f, 0.4f) : level > 0.1f ? new Color(1f, 0.75f, 0.2f) : new Color(1f, 0.3f, 0.25f);
            if (level > 0f) GUI.DrawTexture(new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * level, bar.height - 2f), Texture2D.whiteTexture);
            GUI.color = old;

            float rangeKm = Fuel.Tank.RemainingM(Fuel.RangeM) / 1000f;
            string house = float.IsInfinity(Fuel.NearestHouseM) ? "?" : $"{Fuel.NearestHouseM / 1000f:0.0} km";
            string state = Limp.Active ? " <color=#ff6050>LIMP</color>" : Fuel.Refuelling ? " <color=#80d0ff>refuelling</color>" : "";
            string tanks = Car.ExtraTanks > 0 ? $" +{Car.ExtraTanks} can" : "";
            GUI.Label(new Rect(r.x + 10f, r.y + 24f, r.width - 20f, 20f),
                $"<b>Fuel {level * 100f:0}%</b>{tanks} · ~{rangeKm:0.0} km · house {house}{state}");
        }

        private static void MenuWindow(int id)
        {
            GUILayout.Label($"Full tank: {Fuel.RangeM / 1000f:0.0} km " +
                            $"({(Settings.AutoRange.Value && Houses.CalibratedRangeM.HasValue ? "auto from map" : "fixed")}" +
                            $"{(Car.ExtraTanks > 0 ? $", {Car.ExtraTanks} extra tank part(s)" : "")}).");
            Toggle(Settings.Enabled, "Fuel system on");
            Toggle(Settings.ShowHud, "Show gauge");
            Toggle(Settings.HudAlign, "Line gauge up with the game's gauges");
            Slider(Settings.HudOffsetX, "Gauge offset right (px)", -400f, 400f, "0");
            Slider(Settings.HudOffsetY, "Gauge offset up (px)", -200f, 400f, "0");
            Toggle(Settings.LimpMode, "Limp mode when empty");
            if (Settings.LimpMode.Value)
            {
                Slider(Settings.LimpMaxKmh, "Limp top speed (km/h)", 3f, 40f, "0");
                Slider(Settings.LimpPower, "Limp power (x max-damage torque)", 0.1f, 1f, "0.00");
            }
            Toggle(Settings.AutoRange, "Size tank from the map");
            Slider(Settings.RangeScale, "Range scale", 0.3f, 3f, "0.00");
            if (!Settings.AutoRange.Value) Slider(Settings.FixedRangeKm, "Fixed range (km)", 1f, 40f, "0.0");
            Slider(Settings.ClimbCost, "Climb cost (m per m)", 0f, 30f, "0");
            Slider(Settings.TankBonus, "Range per extra tank", 0f, 1.5f, "0.00");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill tank")) Fuel.Refill();
            if (GUILayout.Button("Close")) _menuOpen = false;
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        private static void Toggle(MelonPreferences_Entry<bool> e, string label)
        {
            bool v = GUILayout.Toggle(e.Value, label);
            if (v != e.Value) { e.Value = v; Settings.Save(); }
        }

        private static void Slider(MelonPreferences_Entry<float> e, string label, float min, float max, string fmt)
        {
            GUILayout.Label($"{label}: {e.Value.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture)}");
            float v = GUILayout.HorizontalSlider(e.Value, min, max);
            if (Mathf.Abs(v - e.Value) > 1e-4f) { e.Value = v; Settings.Save(); }
        }
    }
}
