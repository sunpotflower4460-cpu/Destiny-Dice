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

        private static IslandSeed _seed = IslandSeed.FromValue(1);
        /// <summary>The island this garden has (set once per scene before anything is built).</summary>
        public static IslandSeed Seed => _seed;

        /// <summary>River centre line (x, z), spring first: an arc around the garden and out to the sea.</summary>
        public static Vector2[] RiverPath => _seed.River;

        public static void Configure(IslandSeed seed)
        {
            _seed = seed;
            _warpCache = null;
        }

        private static float Off => _seed.FloraOffset * 0.07f;

        /// <summary>
        /// Organic display position of a cell: a smooth warp field (about ±0.35 cell) so that
        /// plants, puddles and effects never line up on the model grid. Neighbouring cells move
        /// together, so the garden reads as one soft surface. Rocks stay unwarped.
        /// </summary>
        private static Vector2[] _warpCache;

        public static Vector2 Warp(int cellId)
        {
            if (_warpCache == null)
            {
                _warpCache = new Vector2[GardenMapData.CellCount];
                for (int c = 0; c < GardenMapData.CellCount; c++) _warpCache[c] = ComputeWarp(c);
            }
            return _warpCache[cellId];
        }

        private static Vector2 ComputeWarp(int cellId)
        {
            int x = cellId % 16, z = cellId / 16;
            if (GardenMapData.Solid[cellId] == 1) return new Vector2(x, z);
            float wx = (ProceduralArt.Fbm(x * 0.41f + 1.3f + Off, z * 0.41f + 4.1f, 301, 2) - 0.5f) * 1.25f;
            float wz = (ProceduralArt.Fbm(x * 0.41f + 7.7f, z * 0.41f + 2.2f + Off, 302, 2) - 0.5f) * 1.25f;
            return new Vector2(x + wx, z + wz);
        }

        /// <summary>Things that cast shade or hold moisture for the flora: (x, z) and radius.</summary>
        public static readonly List<Vector3> ShadeCasters = new List<Vector3>();
        public static void AddShade(float x, float z, float radius) => ShadeCasters.Add(new Vector3(x, radius, z));

        /// <summary>0 in open sun, up to 1 right beside a rock or under a tree.</summary>
        public static float ShadeAt(float x, float z)
        {
            float best = 0f;
            foreach (var c in ShadeCasters)
            {
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(c.x, c.z));
                best = Mathf.Max(best, Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(c.y * 0.6f, c.y * 1.6f, d)));
            }
            return best;
        }

        /// <summary>Distance from (x, z) to the nearest garden bed centre (warped), or 9 when far.</summary>
        public static float DistanceToBed(float x, float z)
        {
            float best = 9f;
            int cx0 = Mathf.RoundToInt(x), cz0 = Mathf.RoundToInt(z);
            for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int cx = cx0 + dx, cz = cz0 + dz;
                    if (cx < 0 || cx > 15 || cz < 0 || cz > 15) continue;
                    int c = cz * 16 + cx;
                    if (GardenMapData.SoilCapacity[c] == 0) continue;
                    best = Mathf.Min(best, Vector2.Distance(new Vector2(x, z), Warp(c)));
                }
            return best;
        }

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
            float hills = (ProceduralArt.Fbm(x * 0.22f + 3.1f + Off, z * 0.22f + 7.7f - Off, 913, 3) - 0.45f) * _seed.HillAmp;
            h += hills * outer;
            // Highlands on this island's ridge side, extra hills and hollows, and a gentle rim:
            // a shallow bowl with the garden at its heart. All of it differs from garden to garden.
            float proj = (x - Center.x) * Mathf.Cos(_seed.RidgeAngle) + (z - Center.y) * Mathf.Sin(_seed.RidgeAngle);
            float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.5f, 8.3f, proj)) * outer;
            h += back * (_seed.BackAmp * 0.9f + 0.5f * ProceduralArt.Fbm(x * 0.28f + 9f + Off, z * 0.28f + 2f, 931, 2));
            float bumpH = 0f;
            foreach (var b in _seed.Bumps)
            {
                float dd = (x - b.X) * (x - b.X) + (z - b.Z) * (z - b.Z);
                bumpH += b.H * Mathf.Exp(-dd / (2f * b.R * b.R));
            }
            h += bumpH * outer;
            float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(7.4f, 8.9f, d)) * _seed.RimAmp;
            h += rim;
            // Shore: fall into the sea.
            float shore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ShoreStart, ShoreEnd, d));
            h = Mathf.Lerp(h, SeaLevel - 0.7f, shore);
            // River bed, cut deeper where it crosses the hills and the rim.
            float r = DistanceToRiver(x, z);
            float carve = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.25f, 1.0f, r)) * outer;
            h -= carve * (0.22f + (rim + back * 0.7f + Mathf.Max(0f, bumpH) * 0.8f) * 0.9f);
            // The spring pool.
            float sp = Vector2.Distance(new Vector2(x, z), RiverPath[0]);
            h -= 0.30f * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.3f, 1.1f, sp));
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

        // Kept from the bake so the ground can be repainted: bare earth greening with the island's
        // vitality (from the water outward), and the beds coloured by live soil moisture.
        private static Color[] _bakePixels, _meadow, _bare;
        private static float[] _earthMask, _shade, _greenAt, _bankF, _shoreF;
        private static List<KeyValuePair<int, float>>[] _bedWeights;
        private static Texture2D _bakeTex;
        private static readonly Color Sand = new Color(0.86f, 0.80f, 0.64f);
        private static readonly Color Bank = new Color(0.26f, 0.40f, 0.28f);

        /// <summary>
        /// Repaints the island ground: bare earth greens as vitality rises (near the water first);
        /// the beds take their colour from the saved soil moisture, blended smoothly between cells.
        /// </summary>
        public static void PaintBeds(WorldState state, Color dry, Color moist, Color wet, float vitality)
        {
            if (_bakeTex == null || state == null) return;
            var m = new float[GardenMapData.CellCount];
            for (int c = 0; c < GardenMapData.CellCount; c++)
                if (GardenMapData.SoilCapacity[c] > 0) m[c] = (float)state.SoilWater[c] / GardenMapData.SoilCapacity[c];
            for (int i = 0; i < _bakePixels.Length; i++)
            {
                float green = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_greenAt[i], _greenAt[i] + 0.22f, vitality));
                var col = Color.Lerp(_bare[i], _meadow[i], green);
                var list = _bedWeights[i];
                if (list != null)
                {
                    float sw = 0f, sm = 0f, sh = 0f;
                    foreach (var kv in list)
                    {
                        sw += kv.Value; sm += kv.Value * m[kv.Key];
                        if (state.Ecosystem?.Habitat != null) sh += kv.Value * state.Ecosystem.Habitat.Humus[kv.Key] / 100f;
                    }
                    float moisture = sw > 0f ? sm / sw : 0.5f;
                    Color earth = moisture < 0.45f ? Color.Lerp(dry, moist, moisture / 0.45f)
                        : Color.Lerp(moist, wet, Mathf.Clamp01((moisture - 0.45f) / 0.40f));
                    if (state.Ecosystem?.Habitat != null && sw > 0f)
                        earth = Color.Lerp(earth, new Color(0.27f, 0.23f, 0.17f), Mathf.Clamp01(sh / sw) * 0.35f);
                    col = Color.Lerp(col, earth, _earthMask[i]);
                }
                col = Color.Lerp(col, Bank, _bankF[i] * 0.8f * green);
                col = Color.Lerp(col, Sand, _shoreF[i]);
                col *= _shade[i];
                _bakePixels[i] = new Color(col.r, col.g, col.b, 1f);
            }
            _bakeTex.SetPixels(_bakePixels);
            _bakeTex.Apply();
        }

        /// <summary>Top-down colour of the island (bare at first; see PaintBeds for the living state).</summary>
        public static Texture2D BakeColor()
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "IslandColor", wrapMode = TextureWrapMode.Clamp };
            int n = size * size;
            _bakePixels = new Color[n]; _meadow = new Color[n]; _bare = new Color[n];
            _earthMask = new float[n]; _shade = new float[n]; _greenAt = new float[n]; _bankF = new float[n]; _shoreF = new float[n];
            _bedWeights = new List<KeyValuePair<int, float>>[n];
            var meadow = _seed.Meadow;
            var meadowLight = _seed.MeadowLight;
            var moss = _seed.Moss;
            float wc = Mathf.Cos(_seed.WindAngle), ws = Mathf.Sin(_seed.WindAngle);
            var soilA = new Color(0.52f, 0.47f, 0.38f);
            var soilB = new Color(0.43f, 0.40f, 0.33f);
            var clay = new Color(0.68f, 0.62f, 0.50f);
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    float x = BakeMin + px / (float)size * BakeSize, z = BakeMin + py / (float)size * BakeSize;
                    float d = Vector2.Distance(new Vector2(x, z), Center);
                    float n1 = ProceduralArt.Fbm(x * 0.55f, z * 0.55f, 71, 4);
                    float n2 = ProceduralArt.Fbm(x * 2.4f, z * 2.4f, 72, 3);
                    int pi = py * size + px;
                    var col = Color.Lerp(meadow, meadowLight, Mathf.SmoothStep(0.35f, 0.75f, n1));
                    // Blade-like streaks drawn along the wind: the ground itself looks combed by it.
                    float wu = x * wc + z * ws, wv = -x * ws + z * wc;
                    float streak = ProceduralArt.Fbm(wu * 0.9f + Off, wv * 8f, 77, 2);
                    _meadow[pi] = Color.Lerp(col, moss, Mathf.SmoothStep(0.55f, 0.85f, n2) * 0.55f) * (0.86f + 0.26f * streak);
                    float r = DistanceToRiver(x, z);
                    var bare = Color.Lerp(soilA, soilB, Mathf.SmoothStep(0.3f, 0.7f, n2));
                    bare = Color.Lerp(bare, clay, Mathf.SmoothStep(0.55f, 0.8f, n1) * 0.55f); // pale clay patches
                    _bare[pi] = Color.Lerp(bare, new Color(0.42f, 0.36f, 0.29f), Mathf.SmoothStep(1.6f, 0.5f, r) * 0.7f);
                    // Life spreads from the water: greening starts by the river and the spring.
                    _greenAt[pi] = Mathf.Clamp(0.05f + Mathf.Min(0.30f, r * 0.034f) + (n1 - 0.5f) * 0.14f, 0.02f, 0.52f);
                    // Earth under the beds as soft organic patches with a wide, noisy edge (never a frame).
                    int cx0 = Mathf.RoundToInt(x), cz0 = Mathf.RoundToInt(z);
                    // Bed density: a smooth sum over nearby soil cells (no per-cell spots), edged with noise.
                    float wobble = (n1 - 0.5f) * 0.5f;
                    {
                        var weights = new List<KeyValuePair<int, float>>();
                        for (int dz = -3; dz <= 3; dz++)
                            for (int dx = -3; dx <= 3; dx++)
                            {
                                int cx = cx0 + dx, cz = cz0 + dz;
                                if (cx < 0 || cx > 15 || cz < 0 || cz > 15 || GardenMapData.SoilCapacity[cz * 16 + cx] == 0) continue;
                                float dd = Vector2.Distance(new Vector2(x, z), Warp(cz * 16 + cx));
                                if (dd > 3.2f) continue;
                                float w = Mathf.Exp(-dd * dd / (2f * 1.35f * 1.35f));
                                weights.Add(new KeyValuePair<int, float>(cz * 16 + cx, w));
                            }
                        float density = 0f;
                        foreach (var kv in weights) density += kv.Value;
                        float mask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 3.2f, density + wobble * 2f)) * (0.40f + 0.26f * n2);
                        _earthMask[pi] = mask;
                        if (weights.Count > 0 && mask > 0.001f) _bedWeights[pi] = weights;
                    }
                    _bankF[pi] = Mathf.SmoothStep(1.4f, 0.5f, r);
                    float shore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ShoreStart - 0.4f, ShoreStart + 1.0f, d));
                    _shoreF[pi] = shore * (0.85f + 0.15f * n2);
                    _shade[pi] = 0.92f + 0.16f * n2;
                    var bareCol = Color.Lerp(_bare[pi], Sand, _shoreF[pi]) * _shade[pi];
                    _bakePixels[pi] = new Color(bareCol.r, bareCol.g, bareCol.b, 1f);
                }
            tex.SetPixels(_bakePixels);
            tex.Apply();
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
