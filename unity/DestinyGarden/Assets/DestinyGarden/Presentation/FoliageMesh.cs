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
            public void Ball(Vector3 center, Vector3 radius, Color col, float sway, int lon = 6, int lat = 3)
            {
                int start = V.Count;
                for (int i = 0; i <= lat; i++)
                {
                    float phi = Mathf.PI * i / lat;
                    for (int j = 0; j < lon; j++)
                    {
                        float th = 2f * Mathf.PI * j / lon;
                        var dir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                        var c = col; c.a = sway * (dir.y > 0 ? 1f : 0.6f);
                        c *= 0.86f + 0.14f * dir.y; c.a = sway;
                        Add(center + Vector3.Scale(dir, radius), Vector3.Lerp(dir, Vector3.up, 0.35f).normalized, c);
                    }
                }
                for (int i = 0; i < lat; i++)
                    for (int j = 0; j < lon; j++)
                    {
                        int a = start + i * lon + j, b = start + i * lon + (j + 1) % lon;
                        int c2 = a + lon, d = b + lon;
                        T.Add(a); T.Add(c2); T.Add(b); T.Add(b); T.Add(c2); T.Add(d);
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
