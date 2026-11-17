using UnityEngine;

namespace FuelManagement
{
    /// <summary>
    /// The fuel gauge as a round dial styled like the game's tilt dials: dark disc, white tick
    /// marks, light-grey lower band. The needle sweeps over the top half from E (left) to F (right);
    /// the band shows km left and the nearest house. Sits to the right of the tilt dials, the same
    /// size. Drawn with IMGUI from textures generated once (no game assets needed).
    /// </summary>
    internal static class FuelDial
    {
        private const float FallbackSize = 140f, Gap = 12f, LowZone = 0.15f, BandTop = 0.62f;
        private const int TexSize = 256;

        // Colours sampled by eye from the stock dials.
        private static readonly Color Disc = new(0.04f, 0.04f, 0.05f, 0.88f);
        private static readonly Color Band = new(0.74f, 0.74f, 0.74f, 1f);
        private static readonly Color Ink = new(0.12f, 0.12f, 0.13f, 1f);
        private static readonly Color Red = new(1f, 0.32f, 0.26f, 1f);
        private static readonly Color Blue = new(0.5f, 0.82f, 1f, 1f);
        private static readonly Color Amber = new(1f, 0.7f, 0.35f, 1f);

        private static Texture2D _disc, _band;
        private static GUIStyle _label;

        public static void Draw(float scale, string status)
        {
            if (!EnsureTextures()) return;
            HudLayout.Box? tilt = null, other = null;
            if (Settings.HudAlign.Value)
            {
                tilt = Scaled(HudAnchor.Tilt, scale);
                other = Scaled(HudAnchor.Other, scale);
            }
            var b = HudLayout.PlaceDial(tilt, other, FallbackSize, Gap, Screen.width / scale, Screen.height / scale,
                Settings.HudOffsetX.Value, -Settings.HudOffsetY.Value);
            var r = new Rect(b.X, b.Y, b.W, b.H);
            float d = r.width;
            var c = new Vector2(r.x + d * 0.5f, r.y + d * 0.5f);
            float level = Fuel.Tank.Level;
            var old = GUI.color;
            _label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = false };

            GUI.color = Disc;
            GUI.DrawTexture(r, _disc);
            GUI.color = Band;
            GUI.DrawTexture(r, _band);

            // Ticks every 10%, longer at E, half and F; the low zone in red.
            for (int i = 0; i <= 10; i++)
            {
                float lv = i / 10f;
                bool major = i % 5 == 0;
                float w = d * (major ? 0.024f : 0.015f), len = d * (major ? 0.09f : 0.06f);
                GUI.color = lv < LowZone ? Red : Color.white;
                Rotated(HudLayout.NeedleAngle(lv), c, scale, new Rect(c.x - w * 0.5f, r.y + d * 0.06f, w, len));
            }

            GUI.color = Color.white;
            Text(new Rect(c.x - d * 0.40f, c.y - d * 0.13f, d * 0.14f, d * 0.12f), "E", d * 0.085f, Color.white);
            Text(new Rect(c.x + d * 0.26f, c.y - d * 0.13f, d * 0.14f, d * 0.12f), "F", d * 0.085f, Color.white);

            // State replaces the "FUEL" caption so nothing pops up anywhere else.
            var (caption, tint) = Limp.Active ? ("LIMP", Red)
                : Fuel.Refuelling ? ("REFUEL", Blue)
                : Siphon.Receiving ? ("SIPHON", Blue)
                : Siphon.BeingDrained ? ("SIPHONED", Amber)
                : (Car.ExtraTanks > 0 ? $"FUEL +{Car.ExtraTanks}" : "FUEL", Color.white);
            Text(new Rect(c.x - d * 0.3f, c.y - d * 0.27f, d * 0.6f, d * 0.1f), caption, d * 0.07f, tint);

            // Needle and hub.
            float nw = d * 0.03f, nl = d * 0.37f;
            GUI.color = Limp.Active ? Red : Color.white;
            Rotated(HudLayout.NeedleAngle(level), c, scale, new Rect(c.x - nw * 0.5f, c.y - nl, nw, nl));
            float hub = d * 0.09f;
            GUI.DrawTexture(new Rect(c.x - hub * 0.5f, c.y - hub * 0.5f, hub, hub), _disc);

            // Readout in the grey band (the lower part of the dial may hang off the screen edge,
            // like the stock dials, so keep it high in the band).
            float rangeKm = Fuel.Tank.RemainingM(Fuel.RangeM) / 1000f;
            string house = float.IsInfinity(Fuel.NearestHouseM) ? "house ?" : $"house {Fuel.NearestHouseM / 1000f:0.0} km";
            Text(new Rect(r.x, r.y + d * (BandTop + 0.015f), d, d * 0.1f), $"<b>~{rangeKm:0.0} km</b>", d * 0.085f, Ink);
            Text(new Rect(r.x, r.y + d * (BandTop + 0.105f), d, d * 0.09f), house, d * 0.065f, Ink);

            if (status.Length > 0)
                Text(new Rect(r.x - d * 0.5f, r.y - d * 0.16f, d * 2f, d * 0.14f), "<i>" + status + "</i>", d * 0.07f, Color.white);
            GUI.color = old;
        }

        private static HudLayout.Box? Scaled(HudLayout.Box? b, float scale) =>
            b.HasValue ? new HudLayout.Box(b.Value.X / scale, b.Value.Y / scale, b.Value.W / scale, b.Value.H / scale) : null;

        private static void Text(Rect r, string s, float size, Color color)
        {
            _label.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            _label.normal.textColor = color;
            var old = GUI.color;
            GUI.color = Color.white;
            GUI.Label(r, s, _label);
            GUI.color = old;
        }

        /// <summary>Draws a white rect rotated by <paramref name="deg"/> (clockwise) about <paramref name="pivot"/>.</summary>
        private static void Rotated(float deg, Vector2 pivot, float scale, Rect rect)
        {
            var m = GUI.matrix;
            // RotateAroundPivot takes the pivot in screen space, i.e. after our scale matrix.
            GUIUtility.RotateAroundPivot(deg, pivot * scale);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.matrix = m;
        }

        // ------------------------------------------------------------ textures

        private static bool EnsureTextures()
        {
            if (_disc != null && !_disc.WasCollected && _band != null && !_band.WasCollected) return true;
            try
            {
                _disc = Make(_ => true);
                // Disc pixels below the band line (texture rows start at the bottom).
                _band = Make(yFromTop => yFromTop >= BandTop);
                return true;
            }
            catch (System.Exception ex)
            {
                Mod.Log.Warning("Dial textures failed, using the bar gauge: " + ex.Message);
                Settings.GaugeDial.Value = false;
                return false;
            }
        }

        /// <summary>White anti-aliased disc, keeping only rows where <paramref name="keep"/>(fraction from top) holds.</summary>
        private static Texture2D Make(System.Func<float, bool> keep)
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[TexSize * TexSize];
            float rad = TexSize * 0.5f;
            for (int y = 0; y < TexSize; y++)
            {
                float fromTop = 1f - (y + 0.5f) / TexSize;
                bool rowKept = keep(fromTop);
                for (int x = 0; x < TexSize; x++)
                {
                    float dx = x + 0.5f - rad, dy = y + 0.5f - rad;
                    float a = rowKept ? Mathf.Clamp01(rad - Mathf.Sqrt(dx * dx + dy * dy)) : 0f; // 1 px soft edge
                    px[y * TexSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
