using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace FuelManagement
{
    /// <summary>
    /// Finds where the game draws its own car gauges on screen, so the fuel gauge can sit centred
    /// just above them at any resolution or HUD scale. The anchor is the pair of tilt dials
    /// (<c>ContextUIInclineIndicator</c>, bottom left). Only if they're hidden do the health,
    /// RPM and odometer panels count (0.1.1 used only those: on a 3440x1440 screen the health UI
    /// is a full-screen container and the RPM display sits bottom right, so nothing matched).
    /// Rescans every 2 s (HUD panels appear/disappear with the game state); logs the rects
    /// when they change so placement can be checked from the log.
    /// </summary>
    internal static class HudAnchor
    {
        /// <summary>Union of the game's gauges in GUI pixels (top-left origin), or null if not found.</summary>
        public static HudLayout.Box? Cluster { get; private set; }

        /// <summary>The tilt dials alone (screen pixels), or null when they're hidden.</summary>
        public static HudLayout.Box? Tilt { get; private set; }

        /// <summary>The other HUD panels (health, RPM, odometer) when they qualify, else null.</summary>
        public static HudLayout.Box? Other { get; private set; }

        /// <summary>The stock tilt dials found active in the last scan (template for <see cref="StockDial"/>).</summary>
        public static List<ContextUIInclineIndicator> Indicators { get; } = new();

        private static bool _dumped;

        private static float _next;
        private static string _lastLog = "";
        private static readonly Il2CppStructArray<Vector3> Corners = new(4);

        public static void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;
            var incline = new List<HudLayout.Box>();
            var boxes = new List<HudLayout.Box>();
            var names = new List<string>();
            try
            {
                var dials = Object.FindObjectsOfType<ContextUIInclineIndicator>();
                Collect(dials, incline, names);
                Indicators.Clear();
                foreach (var d in dials)
                    if (d != null && d.gameObject.activeInHierarchy) Indicators.Add(d);
                Collect(Object.FindObjectsOfType<CarHealthUI>(), boxes, names);
                Collect(Object.FindObjectsOfType<ContextUIEngineRPMDisplay>(), boxes, names);
                Collect(Object.FindObjectsOfType<ContextUIOdometerPanel>(), boxes, names);
            }
            catch (System.Exception ex)
            {
                Cluster = Tilt = Other = null;
                Indicators.Clear();
                Log("HUD anchor scan failed: " + ex.Message);
                return;
            }
            var tilt = HudLayout.GaugeCluster(incline, Screen.width, Screen.height);
            Tilt = tilt;
            Other = HudLayout.GaugeCluster(boxes, Screen.width, Screen.height);
            Cluster = tilt ?? Other;
            Log($"HUD anchors [{string.Join("; ", names)}] screen {Screen.width}x{Screen.height} → cluster " +
                (Cluster.HasValue ? Fmt(Cluster.Value) + (tilt.HasValue ? " (tilt dials)" : " (other panels)")
                                  : "none (fallback position)"));
        }

        private static void Collect<T>(Il2CppArrayBase<T> found, List<HudLayout.Box> boxes, List<string> names)
            where T : Component
        {
            if (found == null) return;
            foreach (var c in found)
            {
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                var rt = c.GetComponent<RectTransform>();
                if (rt == null) continue;
                var canvas = c.GetComponentInParent<Canvas>();
                if (canvas == null) continue;
                var root = canvas.rootCanvas;
                Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
                rt.GetWorldCorners(Corners);
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    var p = RectTransformUtility.WorldToScreenPoint(cam, Corners[i]);
                    float gy = Screen.height - p.y; // screen y is up, GUI y is down
                    x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
                    y0 = Mathf.Min(y0, gy); y1 = Mathf.Max(y1, gy);
                }
                var box = new HudLayout.Box(x0, y0, x1 - x0, y1 - y0);
                boxes.Add(box);
                if (c.TryCast<ContextUIInclineIndicator>() != null) DumpOnce(c);
                names.Add($"{typeof(T).Name} {Fmt(box)}");
            }
        }

        /// <summary>
        /// Logs the tilt dial's parts once (paths, sprites, colours, sizes), so a later version
        /// can draw the fuel dial with the game's own artwork.
        /// </summary>
        private static void DumpOnce(Component dial)
        {
            if (_dumped) return;
            _dumped = true;
            try
            {
                var lines = new List<string>();
                var root = dial.transform;
                foreach (var g in dial.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                {
                    if (g == null || lines.Count >= 40) continue;
                    var rt = g.rectTransform;
                    string what = g.TryCast<UnityEngine.UI.Image>() is { } img
                        ? $"Image sprite={(img.sprite != null ? img.sprite.name : "none")} type={img.type}"
                        : g.GetIl2CppType().Name;
                    lines.Add($"  {Path(g.transform, root)}: {what} color={g.color} size={rt.rect.width:0}x{rt.rect.height:0} " +
                              $"active={g.gameObject.activeSelf}");
                }
                Mod.Log.Msg($"Tilt dial parts ({dial.gameObject.name}):\n" + string.Join("\n", lines));
            }
            catch (System.Exception ex) { Mod.Log.Warning("Tilt dial dump failed: " + ex.Message); }
        }

        private static string Path(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var x = t; x != null && parts.Count < 8; x = x.parent)
            {
                parts.Add(x.name);
                if (x == root) break;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string Fmt(HudLayout.Box b) => $"({b.X:0},{b.Y:0} {b.W:0}x{b.H:0})";

        private static void Log(string msg)
        {
            if (msg == _lastLog) return;
            _lastLog = msg;
            Mod.Log.Msg(msg);
        }
    }
}
