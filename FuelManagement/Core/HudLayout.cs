using System;
using System.Collections.Generic;

namespace FuelManagement
{
    /// <summary>
    /// Places the fuel gauge relative to the game's own HUD gauges. Rectangles are in GUI
    /// coordinates (origin top left, y down). Pure maths so it can be tested without the game.
    /// </summary>
    internal static class HudLayout
    {
        /// <summary>An axis-aligned rectangle (GUI coordinates).</summary>
        internal readonly record struct Box(float X, float Y, float W, float H)
        {
            public float Right => X + W;
            public float Bottom => Y + H;
            public float CenterX => X + W * 0.5f;
            public float CenterY => Y + H * 0.5f;
        }

        /// <summary>
        /// Union of the candidate rectangles that look like the game's bottom-left gauges:
        /// finite, non-trivial, not a full-screen container, centred in the bottom half and
        /// left half of the screen. Null when none qualify.
        /// </summary>
        public static Box? GaugeCluster(IEnumerable<Box> candidates, float screenW, float screenH)
        {
            if (candidates == null || !(screenW > 0f) || !(screenH > 0f)) return null;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            bool any = false;
            foreach (var b in candidates)
            {
                if (!Finite(b.X) || !Finite(b.Y) || !Finite(b.W) || !Finite(b.H)) continue;
                if (b.W < 4f || b.H < 4f || b.W > screenW * 0.5f || b.H > screenH * 0.4f) continue;
                if (b.CenterY < screenH * 0.5f || b.CenterX > screenW * 0.5f) continue;
                x0 = Math.Min(x0, b.X); y0 = Math.Min(y0, b.Y);
                x1 = Math.Max(x1, b.Right); y1 = Math.Max(y1, b.Bottom);
                any = true;
            }
            return any ? new Box(x0, y0, x1 - x0, y1 - y0) : null;
        }

        /// <summary>
        /// Where to draw a gauge of size <paramref name="w"/>×<paramref name="h"/>: centred
        /// horizontally over <paramref name="cluster"/> and <paramref name="gap"/> above it, or at
        /// the bottom-left fallback without a cluster. Offsets are added, then the result is kept
        /// on screen.
        /// </summary>
        public static Box Place(Box? cluster, float w, float h, float gap, float screenW, float screenH,
            float offsetX, float offsetY)
        {
            float x, y;
            if (cluster.HasValue)
            {
                x = cluster.Value.CenterX - w * 0.5f;
                y = cluster.Value.Y - gap - h;
            }
            else
            {
                x = 20f;
                y = screenH - 190f;
            }
            x += Finite(offsetX) ? offsetX : 0f;
            y += Finite(offsetY) ? offsetY : 0f;
            x = Math.Clamp(x, 0f, Math.Max(0f, screenW - w));
            y = Math.Clamp(y, 0f, Math.Max(0f, screenH - h));
            return new Box(x, y, w, h);
        }

        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
