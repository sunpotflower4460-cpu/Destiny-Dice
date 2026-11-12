using System.Collections.Generic;
using UnityEngine;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// Island trees and shrubs (presentation only), built in local space with DestinyGarden/Foliage so
    /// their leaves sway and glow through in the light. Five kinds: broadleaf, blossom, weeping willow
    /// (by the river), conifer and shrub. Each is grown from a seed so no two look alike.
    /// </summary>
    public static class TreeForms
    {
        public enum Kind { Broadleaf, Blossom, Willow, Conifer, Shrub }

        private static readonly Color Bark = new Color(0.42f, 0.33f, 0.26f);
        private static readonly Color BarkLight = new Color(0.56f, 0.46f, 0.36f);

        public static FoliageMesh Build(Kind kind, int seed)
        {
            var b = new FoliageMesh { Root = new Vector4(0f, 0f, 0f, -1f) };
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            switch (kind)
            {
                case Kind.Broadleaf: Broadleaf(b, R, false); break;
                case Kind.Blossom: Broadleaf(b, R, true); break;
                case Kind.Willow: Willow(b, R); break;
                case Kind.Conifer: Conifer(b, R); break;
                default: Shrub(b, R); break;
            }
            return b;
        }

        private static List<Vector3> Curve(Vector3 a, Vector3 bend, Vector3 c, int n = 5)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                pts.Add(Vector3.Lerp(Vector3.Lerp(a, bend, t), Vector3.Lerp(bend, c, t), t));
            }
            return pts;
        }

        /// <summary>A soft cluster of leaf balls around a point, lighter on top.</summary>
        private static void Clump(FoliageMesh b, System.Func<float> R, Vector3 at, float size, Color dark, Color light, float sway)
        {
            int n = 5 + (int)(R() * 3);
            for (int i = 0; i < n; i++)
            {
                var off = new Vector3(R() - 0.5f, (R() - 0.35f) * 0.8f, R() - 0.5f) * size * 1.3f;
                float r = size * (0.42f + R() * 0.32f);
                float up = Mathf.Clamp01(0.5f + off.y / size);
                var col = Color.Lerp(dark, light, up * 0.8f + R() * 0.2f);
                b.Ball(at + off, new Vector3(r, r * 0.82f, r), col, sway, 9, 5);
                // Feathered edge: small leaves pointing outward from the surface soften the lumpy silhouette.
                int cards = 5 + (int)(R() * 3);
                for (int k = 0; k < cards; k++)
                {
                    var d = new Vector3(R() - 0.5f, R() - 0.35f, R() - 0.5f).normalized;
                    var lc = col * (0.82f + R() * 0.16f);
                    b.Leaf(at + off + d * r * 0.8f, d, r * 0.42f, r * 0.17f, lc, sway + 0.1f);
                }
            }
        }

        private static void Broadleaf(FoliageMesh b, System.Func<float> R, bool blossom)
        {
            var lean = new Vector3(R() - 0.5f, 0f, R() - 0.5f) * 0.25f;
            var top = new Vector3(0f, 1.45f, 0f) + lean;
            b.Tube(Curve(Vector3.zero, new Vector3(0f, 0.7f, 0f) + lean * 0.3f, top), 0.12f, 0.05f, Bark, BarkLight, 0f, 0.15f);
            var dark = blossom ? new Color(0.86f, 0.50f, 0.64f) : new Color(0.16f, 0.36f, 0.20f);
            var light = blossom ? new Color(1f, 0.82f, 0.88f) : new Color(0.40f, 0.62f, 0.30f);
            int branches = 4 + (int)(R() * 2);
            for (int i = 0; i < branches; i++)
            {
                float a = i * Mathf.PI * 2f / branches + R() * 0.6f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float h = 0.85f + R() * 0.45f;
                var from = new Vector3(0f, h, 0f) + lean * (h / 1.45f);
                var tip = from + dir * (0.45f + R() * 0.3f) + Vector3.up * (0.25f + R() * 0.25f);
                b.Tube(Curve(from, from + dir * 0.2f + Vector3.up * 0.05f, tip, 4), 0.04f, 0.015f, Bark, BarkLight, 0.1f, 0.35f, 5);
                Clump(b, R, tip, 0.40f + R() * 0.10f, dark, light, 0.45f);
                if (blossom && R() < 0.6f) Clump(b, R, tip + Vector3.down * 0.12f, 0.16f, new Color(0.40f, 0.62f, 0.38f), new Color(0.58f, 0.76f, 0.48f), 0.45f);
            }
            Clump(b, R, top + Vector3.up * 0.15f, 0.52f, dark, light, 0.4f);
        }

        private static void Willow(FoliageMesh b, System.Func<float> R)
        {
            var top = new Vector3(0.1f, 1.35f, 0f);
            b.Tube(Curve(Vector3.zero, new Vector3(0.08f, 0.6f, 0f), top), 0.15f, 0.07f, Bark, BarkLight, 0f, 0.1f);
            var dark = new Color(0.26f, 0.46f, 0.22f);
            var light = new Color(0.52f, 0.72f, 0.34f);
            Clump(b, R, top + Vector3.up * 0.2f, 0.42f, dark, light, 0.3f);
            int strands = 22;
            for (int i = 0; i < strands; i++)
            {
                float a = i * Mathf.PI * 2f / strands + R() * 0.3f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var p0 = top + dir * (0.2f + R() * 0.35f) + Vector3.up * (0.15f + R() * 0.2f);
                float fall = 0.9f + R() * 0.45f;
                var pts = new List<Vector3>
                {
                    p0, p0 + dir * 0.25f + Vector3.up * 0.08f, p0 + dir * 0.42f - Vector3.up * (fall * 0.35f),
                    p0 + dir * 0.48f - Vector3.up * (fall * 0.7f), p0 + dir * 0.5f - Vector3.up * fall
                };
                b.Tube(pts, 0.012f, 0.006f, dark, light, 0.3f, 1f, 4);
                for (int k = 1; k < pts.Count; k++)
                    b.Leaf(pts[k], (dir * 0.4f + Vector3.down).normalized, 0.14f, 0.035f, Color.Lerp(dark, light, k / 5f), 0.4f + k * 0.15f);
            }
        }

        private static void Conifer(FoliageMesh b, System.Func<float> R)
        {
            float height = 1.8f + R() * 0.4f;
            b.Tube(Curve(Vector3.zero, new Vector3(0f, height * 0.5f, 0f), new Vector3(0f, height, 0f)), 0.09f, 0.03f, Bark, BarkLight, 0f, 0.2f);
            var dark = new Color(0.08f, 0.26f, 0.20f);
            var light = new Color(0.22f, 0.44f, 0.30f);
            int tiers = 5;
            for (int k = 0; k < tiers; k++)
            {
                float t = k / (float)(tiers - 1);
                float y = Mathf.Lerp(0.45f, height - 0.15f, t);
                float r = Mathf.Lerp(0.62f, 0.14f, t);
                int boughs = 7 - k;
                for (int i = 0; i < boughs; i++)
                {
                    float a = i * Mathf.PI * 2f / boughs + k * 0.5f;
                    var dir = new Vector3(Mathf.Cos(a), -0.35f, Mathf.Sin(a)).normalized;
                    b.Leaf(new Vector3(0f, y, 0f), dir, r * 1.15f, r * 0.42f, Color.Lerp(dark, light, t * 0.6f + R() * 0.3f), 0.3f + t * 0.4f);
                }
                b.Ball(new Vector3(0f, y + 0.02f, 0f), new Vector3(r * 0.55f, 0.12f, r * 0.55f), Color.Lerp(dark, light, t), 0.3f, 7, 3);
            }
        }

        private static void Shrub(FoliageMesh b, System.Func<float> R)
        {
            var dark = new Color(0.18f, 0.40f, 0.24f);
            var light = new Color(0.42f, 0.64f, 0.34f);
            Clump(b, R, new Vector3(0f, 0.30f, 0f), 0.44f, dark, light, 0.3f);
            if (R() < 0.6f)
            {
                var berry = R() < 0.5f ? new Color(0.86f, 0.20f, 0.26f) : new Color(0.94f, 0.90f, 0.98f);
                for (int i = 0; i < 9; i++)
                {
                    var p = new Vector3(R() - 0.5f, 0.28f + R() * 0.25f, R() - 0.5f) * 0.9f;
                    p.y = 0.25f + R() * 0.3f;
                    b.Ball(p, Vector3.one * 0.035f, berry, 0.4f, 5, 2);
                }
            }
        }
    }
}
