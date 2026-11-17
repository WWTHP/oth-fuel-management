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

        /// <summary>
        /// Where to draw a round dial gauge. Next to the game's tilt dials (<paramref name="tilt"/>):
        /// same size and height, to their right, spaced like the two tilt dials are. Else centred
        /// <paramref name="gap"/> above <paramref name="other"/> panels, else bottom left. Offsets are
        /// added, then it's kept on screen, except that the lower half may hang off the bottom
        /// edge the way the stock dials do.
        /// </summary>
        public static Box PlaceDial(Box? tilt, Box? other, float fallbackSize, float gap, float screenW, float screenH,
            float offsetX, float offsetY)
        {
            float size = Finite(fallbackSize) && fallbackSize > 0f ? fallbackSize : 140f;
            float x, y;
            if (tilt.HasValue)
            {
                var t = tilt.Value;
                size = t.H;
                // Two dials side by side: whatever width is left over is the gap between them.
                float spacing = t.W > 1.5f * size ? Math.Clamp(t.W - 2f * size, 0.02f * size, 0.5f * size) : 0.1f * size;
                x = t.Right + spacing;
                y = t.Y;
            }
            else if (other.HasValue)
            {
                x = other.Value.CenterX - size * 0.5f;
                y = other.Value.Y - gap - size;
            }
            else
            {
                x = 20f;
                y = screenH - size - 20f;
            }
            x += Finite(offsetX) ? offsetX : 0f;
            y += Finite(offsetY) ? offsetY : 0f;
            x = Math.Clamp(x, 0f, Math.Max(0f, screenW - size));
            y = Math.Clamp(y, 0f, Math.Max(0f, screenH - size * 0.5f));
            return new Box(x, y, size, size);
        }

        /// <summary>
        /// Needle angle in degrees for a fill level: -90 (pointing left, E) to +90 (pointing
        /// right, F), 0 = straight up. Clockwise positive, as GUI rotation is. Out-of-range levels
        /// are clamped; NaN reads empty.
        /// </summary>
        public static float NeedleAngle(float level)
        {
            float l = float.IsNaN(level) ? 0f : Math.Clamp(level, 0f, 1f);
            return -90f + 180f * l;
        }

        /// <summary>
        /// Where a dial mark for <paramref name="level"/> sits at <paramref name="radius"/> from the
        /// dial centre: x right, y up (UI space), on the same E→F half circle as the needle.
        /// </summary>
        public static (float X, float Y) DialPoint(float level, float radius)
        {
            double a = NeedleAngle(level) * Math.PI / 180.0;
            return ((float)(Math.Sin(a) * radius), (float)(Math.Cos(a) * radius));
        }

        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
