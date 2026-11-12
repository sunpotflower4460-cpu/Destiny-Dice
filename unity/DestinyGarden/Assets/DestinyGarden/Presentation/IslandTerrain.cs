using System.Collections.Generic;
using DestinyGarden.Domain;
using UnityEngine;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// The round sacred island the 16x16 garden grid sits on. Presentation only: inside the
    /// garden area the surface passes exactly through each cell's model height, so every plant,
    /// puddle and effect stays where the simulation puts it. Outside, the land rises into soft
    /// hills and falls to a shore. A small river (scenery, not part of the water model) runs
    /// from a spring behind the garden to the sea.
    /// </summary>
    public static class IslandTerrain
    {
        public const float SeaLevel = -0.80f;
        public static readonly Vector2 Center = new Vector2(7.5f, 7.5f);
        public const float ShoreStart = 9.2f;   // land begins to fall here
        public const float ShoreEnd = 11.8f;    // under the sea from here

        // River centre line (x, z), spring first. Stays on ground cells, never on soil.
        public static readonly Vector2[] RiverPath =
        {
            new Vector2(8.6f, 15.6f), new Vector2(8.3f, 14.2f), new Vector2(7.2f, 13.2f), new Vector2(5.4f, 12.8f),
            new Vector2(3.6f, 12.9f), new Vector2(2.2f, 12.2f), new Vector2(1.2f, 10.6f), new Vector2(0.2f, 9.2f),
            new Vector2(-1.4f, 8.4f), new Vector2(-3.4f, 8.0f)
        };

        private static float CellModelHeight(int x, int y)
        {
            x = Mathf.Clamp(x, 0, 15); y = Mathf.Clamp(y, 0, 15);
            int c = y * 16 + x;
            if (GardenMapData.Solid[c] == 0) return 0.30f + GardenMapData.CellHeight[c] / 1140f * 0.85f;
            // Rocks stand on the ground: use the mean of the open neighbours.
            float sum = 0f; int n = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = Mathf.Clamp(x + dx, 0, 15), ny = Mathf.Clamp(y + dy, 0, 15);
                    int nc = ny * 16 + nx;
                    if (GardenMapData.Solid[nc] == 0) { sum += 0.30f + GardenMapData.CellHeight[nc] / 1140f * 0.85f; n++; }
                }
            return n > 0 ? sum / n : 0.6f;
        }

        private static float GridHeight(float x, float z)
        {
            float cx = Mathf.Clamp(x, 0f, 15f), cz = Mathf.Clamp(z, 0f, 15f);
            int x0 = Mathf.FloorToInt(cx), z0 = Mathf.FloorToInt(cz);
            int x1 = Mathf.Min(15, x0 + 1), z1 = Mathf.Min(15, z0 + 1);
            float fx = cx - x0, fz = cz - z0;
            float a = Mathf.Lerp(CellModelHeight(x0, z0), CellModelHeight(x1, z0), fx);
            float b = Mathf.Lerp(CellModelHeight(x0, z1), CellModelHeight(x1, z1), fx);
            return Mathf.Lerp(a, b, fz);
        }

        public static float DistanceToRiver(float x, float z)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i + 1 < RiverPath.Length; i++)
            {
                Vector2 a = RiverPath[i], b = RiverPath[i + 1];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        /// <summary>0 inside the garden (exact model heights), 1 in the free outer land.</summary>
        private static float Outer(float d) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5.6f, 8.6f, d));

        public static float Height(float x, float z)
        {
            float d = Vector2.Distance(new Vector2(x, z), Center);
            float h = GridHeight(x, z);
            float outer = Outer(d);
            float hills = (ProceduralArt.Fbm(x * 0.22f + 3.1f, z * 0.22f + 7.7f, 913, 3) - 0.45f) * 0.9f;
            h += hills * outer;
            // Shore: fall into the sea.
            float shore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ShoreStart, ShoreEnd, d));
            h = Mathf.Lerp(h, SeaLevel - 0.7f, shore);
            // River bed.
            float r = DistanceToRiver(x, z);
            float carve = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.25f, 1.0f, r)) * outer;
            h -= carve * 0.22f;
            return h;
        }

        /// <summary>Polar mesh of the whole island (centre outwards), uv = world xz mapped to the colour bake.</summary>
        public static Mesh BuildMesh()
        {
            const int rings = 70, segments = 160;
            const float radius = 12.6f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            verts.Add(new Vector3(Center.x, Height(Center.x, Center.y), Center.y));
            uvs.Add(BakeUv(Center.x, Center.y));
            for (int r = 1; r <= rings; r++)
            {
                float rr = radius * r / rings;
                for (int s = 0; s < segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    float x = Center.x + Mathf.Cos(a) * rr, z = Center.y + Mathf.Sin(a) * rr;
                    verts.Add(new Vector3(x, Height(x, z), z));
                    uvs.Add(BakeUv(x, z));
                }
            }
            for (int s = 0; s < segments; s++)
            {
                int a = 1 + s, b = 1 + (s + 1) % segments;
                tris.Add(0); tris.Add(b); tris.Add(a);
            }
            for (int r = 1; r < rings; r++)
            {
                int ring0 = 1 + (r - 1) * segments, ring1 = 1 + r * segments;
                for (int s = 0; s < segments; s++)
                {
                    int s1 = (s + 1) % segments;
                    int a = ring0 + s, b = ring0 + s1, c = ring1 + s, d = ring1 + s1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }
            var mesh = new Mesh { name = "Island", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private const float BakeMin = -5.5f, BakeSize = 26f;
        private static Vector2 BakeUv(float x, float z) => new Vector2((x - BakeMin) / BakeSize, (z - BakeMin) / BakeSize);

        // Kept from the bake so the garden beds can be repainted with live soil moisture.
        private static Color[] _bakePixels, _preEarth;
        private static float[] _earthMask, _shade;
        private static List<KeyValuePair<int, float>>[] _bedWeights;
        private static Texture2D _bakeTex;

        /// <summary>
        /// Repaints the earth of the beds from the saved soil moisture: dry pale earth through to dark
        /// wet earth, blended smoothly between cells (no mounds, no seams).
        /// </summary>
        public static void PaintBeds(WorldState state, Color dry, Color moist, Color wet)
        {
            if (_bakeTex == null || state == null) return;
            var m = new float[GardenMapData.CellCount];
            for (int c = 0; c < GardenMapData.CellCount; c++)
                if (GardenMapData.SoilCapacity[c] > 0) m[c] = (float)state.SoilWater[c] / GardenMapData.SoilCapacity[c];
            for (int i = 0; i < _bakePixels.Length; i++)
            {
                var list = _bedWeights[i];
                if (list == null) continue;
                float sw = 0f, sm = 0f;
                foreach (var kv in list) { sw += kv.Value; sm += kv.Value * m[kv.Key]; }
                float moisture = sw > 0f ? sm / sw : 0.5f;
                Color earth = moisture < 0.45f ? Color.Lerp(dry, moist, moisture / 0.45f)
                    : Color.Lerp(moist, wet, Mathf.Clamp01((moisture - 0.45f) / 0.40f));
                var col = Color.Lerp(_preEarth[i], earth, _earthMask[i]) * _shade[i];
                _bakePixels[i] = new Color(col.r, col.g, col.b, 1f);
            }
            _bakeTex.SetPixels(_bakePixels);
            _bakeTex.Apply();
        }

        /// <summary>Top-down colour of the island: meadow, moss, earth around the garden, pale shore, river banks.</summary>
        public static Texture2D BakeColor()
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "IslandColor", wrapMode = TextureWrapMode.Clamp };
            var c = new Color[size * size];
            _preEarth = new Color[size * size];
            _earthMask = new float[size * size];
            _shade = new float[size * size];
            _bedWeights = new List<KeyValuePair<int, float>>[size * size];
            var meadow = new Color(0.47f, 0.63f, 0.45f);
            var meadowLight = new Color(0.64f, 0.76f, 0.54f);
            var moss = new Color(0.30f, 0.46f, 0.30f);
            var earth = new Color(0.40f, 0.31f, 0.22f);
            var sand = new Color(0.86f, 0.80f, 0.64f);
            var bank = new Color(0.26f, 0.38f, 0.28f);
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    float x = BakeMin + px / (float)size * BakeSize, z = BakeMin + py / (float)size * BakeSize;
                    float d = Vector2.Distance(new Vector2(x, z), Center);
                    float n1 = ProceduralArt.Fbm(x * 0.55f, z * 0.55f, 71, 4);
                    float n2 = ProceduralArt.Fbm(x * 2.4f, z * 2.4f, 72, 3);
                    var col = Color.Lerp(meadow, meadowLight, Mathf.SmoothStep(0.35f, 0.75f, n1));
                    col = Color.Lerp(col, moss, Mathf.SmoothStep(0.55f, 0.85f, n2) * 0.55f);
                    // Earth under the beds as soft organic patches (round falloff + noise), never square.
                    float soilNear = 9f;
                    int cx0 = Mathf.RoundToInt(x), cz0 = Mathf.RoundToInt(z);
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int cx = cx0 + dx, cz = cz0 + dz;
                            if (cx < 0 || cx > 15 || cz < 0 || cz > 15 || GardenMapData.SoilCapacity[cz * 16 + cx] == 0) continue;
                            soilNear = Mathf.Min(soilNear, Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz)));
                        }
                    float wobble = (n1 - 0.5f) * 0.5f;
                    int pi = py * size + px;
                    _preEarth[pi] = col;
                    float mask = Mathf.SmoothStep(0.92f + wobble, 0.40f + wobble, soilNear) * (0.74f + 0.16f * n2);
                    _earthMask[pi] = mask;
                    if (mask > 0.001f)
                    {
                        var weights = new List<KeyValuePair<int, float>>();
                        for (int dz = -1; dz <= 1; dz++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int cx = cx0 + dx, cz = cz0 + dz;
                                if (cx < 0 || cx > 15 || cz < 0 || cz > 15 || GardenMapData.SoilCapacity[cz * 16 + cx] == 0) continue;
                                float dd = Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz));
                                float w = Mathf.Max(0f, 1.25f - dd);
                                if (w > 0f) weights.Add(new KeyValuePair<int, float>(cz * 16 + cx, w * w));
                            }
                        if (weights.Count > 0) _bedWeights[pi] = weights;
                    }
                    col = Color.Lerp(col, earth, mask);
                    float r = DistanceToRiver(x, z);
                    col = Color.Lerp(col, bank, Mathf.SmoothStep(1.4f, 0.5f, r) * 0.8f);
                    float shore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ShoreStart - 0.4f, ShoreStart + 1.0f, d));
                    col = Color.Lerp(col, sand, shore * (0.85f + 0.15f * n2));
                    _shade[py * size + px] = 0.92f + 0.16f * n2;
                    col *= 0.92f + 0.16f * n2;
                    c[py * size + px] = new Color(col.r, col.g, col.b, 1f);
                }
            tex.SetPixels(c);
            tex.Apply();
            _bakePixels = c;
            _bakeTex = tex;
            return tex;
        }

        /// <summary>Ribbon mesh for the river, following the carved bed; uv.y runs downstream (for flow).</summary>
        public static Mesh BuildRiverMesh()
        {
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < RiverPath.Length; i++)
            {
                Vector2 p0 = RiverPath[Mathf.Max(0, i - 1)], p1 = RiverPath[i], p2 = RiverPath[i + 1],
                    p3 = RiverPath[Mathf.Min(RiverPath.Length - 1, i + 2)];
                for (int k = 0; k < 8; k++)
                {
                    float t = k / 8f;
                    pts.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
                                    (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
                }
            }
            pts.Add(RiverPath[RiverPath.Length - 1]);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float along = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 dir = (i + 1 < pts.Count ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x);
                float k = i / (float)(pts.Count - 1);
                float half = Mathf.Lerp(0.26f, 0.62f, k);
                if (i > 0) along += Vector2.Distance(pts[i], pts[i - 1]);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 q = pts[i] + side * (half * s);
                    float y = Mathf.Max(Height(q.x, q.y), Height(pts[i].x, pts[i].y)) + 0.05f;
                    y = Mathf.Max(y, SeaLevel + 0.02f);
                    verts.Add(new Vector3(q.x, y, q.y));
                    uvs.Add(new Vector2(s < 0 ? 0f : 1f, along * 0.35f));
                }
                if (i > 0)
                {
                    int a = (i - 1) * 2, b = a + 1, c = i * 2, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
            var mesh = new Mesh { name = "River" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Deterministic small offset so plants do not stand on a perfect grid (visual only).</summary>
        public static Vector3 PlantJitter(int cellId)
        {
            uint h = (uint)(cellId * 2654435761u);
            float jx = ((h & 0xFF) / 255f - 0.5f) * 0.52f;
            float jz = (((h >> 8) & 0xFF) / 255f - 0.5f) * 0.52f;
            return new Vector3(jx, 0f, jz);
        }
    }
}
