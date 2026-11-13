using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// Mesh builder for everything drawn with DestinyGarden/Foliage: positions in world space,
    /// vertex colour = albedo with alpha = sway (0 root, 1 tip), TEXCOORD1 = instance root + growth threshold.
    /// </summary>
    public sealed class FoliageMesh
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<Vector4> R = new List<Vector4>();
            public readonly List<int> T = new List<int>();
            public Vector4 Root;

            public int Add(Vector3 p, Vector3 n, Color c)
            {
                V.Add(p); N.Add(n); C.Add(c); R.Add(Root);
                return V.Count - 1;
            }

            internal void Triangle(int a, int b, int c)
            {
                T.Add(a); T.Add(b); T.Add(c);
            }

            internal enum BladeShape { Lance, Oval, Heart, Toothed, Lobed, Ray, Rounded, Ruffled }

            // Denser samples at the tip round off wide petals, and the middle rows hold
            // the shoulders and lobes of leaves. Each end is a single vertex.
            private static readonly float[] BladeRows = { 0.12f, 0.25f, 0.38f, 0.51f, 0.64f, 0.77f, 0.88f, 0.95f };

            internal void CurvedBlade(Vector3 root, Vector3 direction, Vector3 face, float length, float width,
                float bend, float cup, BladeShape shape, Color baseColor, Color tipColor, float sway)
            {
                var axis = direction.normalized;
                var side = Vector3.Cross(face, axis).normalized;
                var normal = Vector3.Cross(axis, side);
                baseColor.a = sway;
                int first = Add(root, normal, baseColor);
                for (int i = 0; i < BladeRows.Length; i++)
                {
                    float t = BladeRows[i];
                    float w = BladeWidth(t, i, shape) * width;
                    float rise = bend * t * t + 0.055f * Mathf.Sin(Mathf.PI * t);
                    var center = root + axis * (length * t) + normal * (length * rise);
                    var faceNormal = (normal - axis * (2f * bend * t + 0.173f * Mathf.Cos(Mathf.PI * t))).normalized;
                    var color = Color.Lerp(baseColor, tipColor, Mathf.SmoothStep(0f, 1f, t));
                    var edgeColor = color * (0.89f + 0.15f * t);
                    edgeColor.a = sway;
                    var vein = color * (1.06f - t * 0.06f);
                    vein.a = sway;
                    float skew = 0.055f * Mathf.Sin(t * 5.4f);
                    int row = Add(center - side * (w * (1f + skew)) + normal * (cup * w),
                        (faceNormal + side * cup).normalized, edgeColor);
                    Add(center, faceNormal, vein);
                    Add(center + side * (w * (1f - skew)) + normal * (cup * w),
                        (faceNormal - side * cup).normalized, edgeColor);
                    if (i == 0)
                    {
                        Triangle(first, row, row + 1);
                        Triangle(first, row + 1, row + 2);
                    }
                    else
                    {
                        for (int j = 0; j < 2; j++)
                        {
                            int a = row - 3 + j, b = a + 1, c = row + j, d = c + 1;
                            Triangle(a, c, b);
                            Triangle(b, c, d);
                        }
                    }
                }
                int tip = Add(root + length * (axis + normal * bend), (normal - 2f * bend * axis).normalized,
                    new Color(tipColor.r, tipColor.g, tipColor.b, sway));
                Triangle(tip - 3, tip, tip - 2);
                Triangle(tip - 2, tip, tip - 1);
            }

            private static float BladeWidth(float t, int row, BladeShape shape)
            {
                float ellipse = Mathf.Sin(Mathf.PI * t);
                switch (shape)
                {
                    case BladeShape.Lance: return ellipse * (1.22f - 0.49f * t);
                    case BladeShape.Heart: return Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.59f)) * (1.26f - 0.55f * t);
                    case BladeShape.Toothed: return Mathf.Sqrt(ellipse) * (row % 2 == 0 ? 0.77f : 1f);
                    case BladeShape.Lobed: return ellipse * (row % 2 == 0 ? 0.51f : 1.12f) * (0.68f + t * 0.64f);
                    case BladeShape.Ray: return Mathf.Sin(Mathf.PI * Mathf.Pow(t, 1.28f)) * (0.74f + t * 0.44f);
                    case BladeShape.Rounded: return Mathf.Sqrt(Mathf.Sin(Mathf.PI * Mathf.Pow(t, 1.65f)));
                    case BladeShape.Ruffled: return Mathf.Sqrt(Mathf.Sin(Mathf.PI * Mathf.Pow(t, 1.56f))) * (row % 2 == 0 ? 0.91f : 1.05f);
                    default: return Mathf.Sqrt(ellipse) * (0.98f - t * 0.16f);
                }
            }

            /// <summary>A tapering, curving blade (grass, reed, frond, stem). Colour alpha = sway.</summary>
            public void Blade(Vector3 root, Vector3 lean, float h, float w, Color baseC, Color tipC, float yaw)
            {
                var side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                int prevL = -1, prevR = -1;
                for (int k = 0; k <= 3; k++)
                {
                    float f = k / 3f;
                    var p = root + Vector3.up * (h * f) + lean * (h * f * f);
                    var col = Color.Lerp(baseC, tipC, f); col.a = f;
                    var n = Vector3.Lerp(Vector3.Cross(side, Vector3.up + lean * (2f * f)).normalized, Vector3.up, 0.55f).normalized;
                    if (k == 3)
                    {
                        int tip = Add(p, n, col);
                        T.Add(prevL); T.Add(tip); T.Add(prevR);
                        break;
                    }
                    float wk = w * (1f - f * 0.82f);
                    int l = Add(p - side * wk, n, col), r = Add(p + side * wk, n, col);
                    if (prevL >= 0) { T.Add(prevL); T.Add(l); T.Add(prevR); T.Add(prevR); T.Add(l); T.Add(r); }
                    prevL = l; prevR = r;
                }
            }

            /// <summary>A flat pointed leaf from a base point along a direction.</summary>
            public void Leaf(Vector3 at, Vector3 dir, float len, float wid, Color c, float sway)
            {
                var d = dir.normalized;
                var side = Vector3.Cross(d, Vector3.up);
                if (side.sqrMagnitude < 0.001f) side = Vector3.right;
                side.Normalize();
                var n = Vector3.Cross(side, d).normalized;
                if (n.y < 0) n = -n;
                c.a = sway;
                int a = Add(at, n, c);
                int b = Add(at + d * (len * 0.42f) + side * wid, n, c);
                int e = Add(at + d * (len * 0.42f) - side * wid, n, c);
                int t = Add(at + d * len, n, c);
                T.Add(a); T.Add(b); T.Add(t); T.Add(a); T.Add(t); T.Add(e);
            }

            /// <summary>A soft low-poly ellipsoid (cushions, heads, bells, caps).</summary>
            public void Ball(Vector3 center, Vector3 radius, Color col, float sway, int lon = 6, int lat = 3,
                float lobes = 0f, float phase = 0f)
            {
                int start = V.Count;
                for (int i = 0; i <= lat; i++)
                {
                    float phi = Mathf.PI * i / lat;
                    float sinPhi = Mathf.Sin(phi);
                    for (int j = 0; j < lon; j++)
                    {
                        float th = 2f * Mathf.PI * j / lon;
                        var dir = new Vector3(sinPhi * Mathf.Cos(th), Mathf.Cos(phi), sinPhi * Mathf.Sin(th));
                        float ripple = lobes > 0f ? lobes * sinPhi *
                            (0.68f * Mathf.Cos(th * 3f + phase + phi) + 0.32f * Mathf.Sin(th * 5f - phase)) : 0f;
                        var c = col * (0.86f + 0.14f * dir.y + ripple * 0.30f);
                        c.a = sway;
                        Add(center + Vector3.Scale(dir, radius) * (1f + ripple), Vector3.Lerp(dir, Vector3.up, 0.35f).normalized, c);
                    }
                }
                for (int i = 0; i < lat; i++)
                    for (int j = 0; j < lon; j++)
                    {
                        int a = start + i * lon + j, b = start + i * lon + (j + 1) % lon;
                        int c2 = a + lon, d = b + lon;
                        if (i > 0) Triangle(a, b, c2);
                        if (i + 1 < lat) Triangle(b, d, c2);
                    }
            }

            /// <summary>
            /// A real petal: narrow at the base, widest past the middle, rounded at the tip, cupped
            /// across its width and arched along its length. Colour runs from base to tip.
            /// </summary>
            public void Petal(Vector3 center, Vector3 outward, float len, float wid, float lift, float cup,
                Color baseC, Color tipC, float sway = 1f)
            {
                var o = new Vector3(outward.x, 0f, outward.z);
                if (o.sqrMagnitude < 1e-5f) o = Vector3.forward;
                o.Normalize();
                var side = Vector3.Cross(Vector3.up, o).normalized;
                const int rows = 5;
                int start = V.Count;
                for (int i = 0; i <= rows; i++)
                {
                    float t = i / (float)rows;
                    float wp = wid * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.12f, 1f, t)) * (t < 0.15f ? 0.6f : 1f);
                    var spine = center + o * (len * t) + Vector3.up * (lift * t * t * len);
                    for (int j = -1; j <= 1; j++)
                    {
                        var p = spine + side * (j * wp) + Vector3.up * (cup * wp * (j * j));
                        var col = Color.Lerp(baseC, tipC, Mathf.SmoothStep(0f, 1f, t));
                        if (j != 0) col = Color.Lerp(col, tipC, 0.25f);
                        col.a = sway;
                        var n = Vector3.Lerp(Vector3.up, -o * lift, 0.25f).normalized;
                        Add(p, n, col);
                    }
                }
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < 2; j++)
                    {
                        int a = start + i * 3 + j, b = a + 1, c = a + 3, d = c + 1;
                        T.Add(a); T.Add(c); T.Add(b); T.Add(b); T.Add(c); T.Add(d);
                    }
            }

            /// <summary>A tapered stem with a stable ring frame and optional root flare.</summary>
            public void Tube(IList<Vector3> pts, float r0, float r1, Color c0, Color c1, float sway0, float sway1,
                int sides = 6, float flare = 0f)
            {
                int start = V.Count;
                Vector3 side = Vector3.zero;
                for (int i = 0; i < pts.Count; i++)
                {
                    float t = i / (float)(pts.Count - 1);
                    Vector3 dir = (pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]).normalized;
                    side -= Vector3.Dot(dir, side) * dir;
                    if (side.sqrMagnitude < 0.001f)
                        side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up);
                    side.Normalize();
                    Vector3 up = Vector3.Cross(side, dir);
                    float baseWeight = (1f - t) * (1f - t);
                    float r = Mathf.Lerp(r0, r1, t) * (1f + flare * baseWeight * baseWeight);
                    var color = Color.Lerp(c0, c1, t);
                    for (int s = 0; s < sides; s++)
                    {
                        float a = s / (float)sides * Mathf.PI * 2f;
                        var n = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                        float ridge = Mathf.Sin(a * 3f + 0.28f * t) * 0.62f + Mathf.Cos(a * 5f - 0.40f * t) * 0.38f;
                        var col = color * (0.93f + 0.17f * ridge);
                        col.a = Mathf.Lerp(sway0, sway1, t);
                        Add(pts[i] + n * (r * (1f + (0.07f + 0.20f * flare * baseWeight) * ridge)), n, col);
                    }
                }
                for (int i = 0; i + 1 < pts.Count; i++)
                    for (int s = 0; s < sides; s++)
                    {
                        int a = start + i * sides + s, b = start + i * sides + (s + 1) % sides;
                        int c = a + sides, d = b + sides;
                        T.Add(a); T.Add(c); T.Add(b); T.Add(b); T.Add(c); T.Add(d);
                    }
            }

            /// <summary>Ring of petals around a centre (helper for most blooms).</summary>
            public void Corolla(Vector3 center, int count, float yaw, float len, float wid, float lift, float cup,
                Color baseC, Color tipC)
            {
                for (int k = 0; k < count; k++)
                {
                    float a = yaw + k * Mathf.PI * 2f / count;
                    Petal(center, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), len, wid, lift, cup, baseC, tipC);
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                m.SetVertices(V); m.SetNormals(N); m.SetColors(C); m.SetUVs(1, R); m.SetTriangles(T, 0);
                m.RecalculateBounds();
                return m;
            }
        }
}
