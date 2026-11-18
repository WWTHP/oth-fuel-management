using MelonLoader;
using UnityEngine;

namespace FuelManagement
{
    /// <summary>
    /// IMGUI: the fuel gauge (a dial next to the game's tilt dials, see <see cref="FuelDial"/>, or
    /// a bar centred above the game's gauges). The settings menu is <see cref="Menu"/>.
    /// No pop-up messages: the gauge is the only in-game indication.
    /// </summary>
    internal static class Hud
    {
        private const float GaugeW = 260f, GaugeH = 52f, Gap = 12f, StatusH = 20f;

        public static void Update()
        {
            Menu.Update();
            bool gauge = Settings.Enabled.Value && Settings.ShowHud.Value;
            if (gauge && Settings.HudAlign.Value) HudAnchor.Update();
            try { StockDial.Update(gauge && Settings.GaugeDial.Value && Settings.HudAlign.Value && Car.Valid); }
            catch (System.Exception ex) { Mod.Log.Warning("Fuel dial: " + ex.Message); }
        }

        public static void Draw()
        {
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            if (Settings.Enabled.Value && Settings.ShowHud.Value && Car.Valid)
            {
                if (Settings.GaugeDial.Value)
                {
                    // The cloned stock dial draws itself; the IMGUI dial is the fallback.
                    if (!StockDial.Ready) FuelDial.Draw(scale, Siphon.Status);
                }
                else DrawGauge(scale);
            }
            Menu.Draw(scale);
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
            string status = Siphon.Status;
            // An extra status line on top, only while siphoning has something to say. The box
            // grows upwards so it never covers the game's gauges below.
            float extra = status.Length > 0 ? StatusH : 0f;
            var b = HudLayout.Place(cluster, GaugeW, GaugeH + extra, Gap, Screen.width / scale, Screen.height / scale,
                Settings.HudOffsetX.Value, -Settings.HudOffsetY.Value);
            var r = new Rect(b.X, b.Y, b.W, b.H);
            float top = r.y + extra; // where the normal gauge content starts

            float level = Fuel.Tank.Level;
            GUI.Box(r, "");
            if (extra > 0f) GUI.Label(new Rect(r.x + 10f, r.y + 4f, r.width - 20f, StatusH), "<i>" + status + "</i>");
            var bar = new Rect(r.x + 10f, top + 8f, r.width - 20f, 12f);
            GUI.Box(bar, "");
            var old = GUI.color;
            GUI.color = level > 0.25f ? new Color(0.45f, 0.85f, 0.4f) : level > 0.1f ? new Color(1f, 0.75f, 0.2f) : new Color(1f, 0.3f, 0.25f);
            if (level > 0f) GUI.DrawTexture(new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * level, bar.height - 2f), Texture2D.whiteTexture);
            GUI.color = old;

            float rangeKm = Fuel.Tank.RemainingM(Fuel.RangeM) / 1000f;
            string house = float.IsInfinity(Fuel.NearestHouseM) ? "?" : $"{Fuel.NearestHouseM / 1000f:0.0} km";
            string state = Limp.Active ? " <color=#ff6050>LIMP</color>"
                : Fuel.Refuelling ? " <color=#80d0ff>refuelling</color>"
                : Siphon.Receiving ? " <color=#80d0ff>siphoning</color>"
                : Siphon.BeingDrained ? " <color=#ffb060>being siphoned</color>" : "";
            string tanks = Car.ExtraTanks > 0 ? $" +{Car.ExtraTanks} can" : "";
            GUI.Label(new Rect(r.x + 10f, top + 24f, r.width - 20f, 20f),
                $"<b>Fuel {level * 100f:0}%</b>{tanks} · ~{rangeKm:0.0} km · house {house}{state}");
        }
    }
}
