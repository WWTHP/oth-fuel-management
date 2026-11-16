using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace FuelManagement
{
    /// <summary>
    /// Finds where the game draws its own car gauges (health, RPM, odometer panels) on screen,
    /// so the fuel gauge can sit centred just above them at any resolution or HUD scale.
    /// Rescans every 2 s (HUD panels appear/disappear with the game state); logs the rects
    /// when they change so placement can be checked from the log.
    /// </summary>
    internal static class HudAnchor
    {
        /// <summary>Union of the game's gauges in GUI pixels (top-left origin), or null if not found.</summary>
        public static HudLayout.Box? Cluster { get; private set; }

        private static float _next;
        private static string _lastLog = "";
        private static readonly Il2CppStructArray<Vector3> Corners = new(4);

        public static void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;
            var boxes = new List<HudLayout.Box>();
            var names = new List<string>();
            try
            {
                Collect(Object.FindObjectsOfType<CarHealthUI>(), boxes, names);
                Collect(Object.FindObjectsOfType<ContextUIEngineRPMDisplay>(), boxes, names);
                Collect(Object.FindObjectsOfType<ContextUIOdometerPanel>(), boxes, names);
            }
            catch (System.Exception ex)
            {
                Cluster = null;
                Log("HUD anchor scan failed: " + ex.Message);
                return;
            }
            Cluster = HudLayout.GaugeCluster(boxes, Screen.width, Screen.height);
            Log($"HUD anchors [{string.Join("; ", names)}] screen {Screen.width}x{Screen.height} → cluster " +
                (Cluster.HasValue ? Fmt(Cluster.Value) : "none (fallback position)"));
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
                names.Add($"{typeof(T).Name} {Fmt(box)}");
            }
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
