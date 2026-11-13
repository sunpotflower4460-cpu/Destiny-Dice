using System.Collections.Generic;
using DestinyGarden.Domain;
using UnityEngine;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// The sacred island the 16x16 garden grid sits on. Presentation only: inside the
    /// garden area the surface passes exactly through each cell's model height, so every plant,
    /// puddle and effect stays where the simulation puts it. Outside, the land rises into soft
    /// hills, rocky headlands and beaches. A small river (scenery, not part of the water model) runs
    /// from a spring behind the garden to the sea.
    /// </summary>
    public static class IslandTerrain
    {
        public const float SeaLevel = -0.80f;
        public static readonly Vector2 Center = new Vector2(7.5f, 7.5f);
        internal const float CoastMapWidth = 31f;
        internal const float CoastDistanceRange = 5f;
        private const float GardenRadius = 5.6f;
        private const float Turn = 2f * Mathf.PI;
        private const float ShelfDepthRange = 1.3f;

        private static IslandSeed _seed = IslandSeed.FromValue(1);
        private static CoastProfile[] _coast;
        private static Vector2 _ridge;
        /// <summary>The island this garden has (set once per scene before anything is built).</summary>
        public static IslandSeed Seed => _seed;

        /// <summary>River centre line (x, z), spring first: an arc around the garden and out to the sea.</summary>
        public static Vector2[] RiverPath => _seed.River;

        public static void Configure(IslandSeed seed)
        {
            _seed = seed;
            _warpCache = null;
            _coast = null;
        }

        private static float Off => _seed.FloraOffset * 0.07f;
        internal static float NearBayAngle => -Mathf.PI / 2f + (_seed.FloraOffset - 50f) * 0.010f;

        private struct CoastProfile
        {
            public float Radius, Rock, Across;
        }

        private static float CoastLobe(float angle, float center, float sharpness) =>
            Mathf.Exp((Mathf.Cos(angle - center) - 1f) * sharpness);

        private static void BuildCoast()
        {
            const int count = 384;
            _coast = new CoastProfile[count];
            _ridge = new Vector2(Mathf.Cos(_seed.RidgeAngle), Mathf.Sin(_seed.RidgeAngle));
            // Keep a bay in view when the camera first arrives. Its flanking points and the
            // second, sheltered bay at the river mouth follow the individual island's seed.
            float front = NearBayAngle;
            Vector2 mouth = Vector2.Lerp(RiverPath[RiverPath.Length - 2], RiverPath[RiverPath.Length - 1], 0.48f) - Center;
            float mouthAngle = Mathf.Atan2(mouth.y, mouth.x);
            Vector2 spring = RiverPath[0] - Center;
            float springAngle = Mathf.Atan2(spring.y, spring.x);
            for (int i = 0; i < count; i++)
            {
                float a = i * Turn / count;
                float ridgeAngle = a - _seed.RidgeAngle;
                float cape = CoastLobe(a, front - 0.84f, 9f);
                float point = CoastLobe(a, front + 1.14f, 6f);
                float springHill = CoastLobe(a, springAngle, 10f);
                float bay = CoastLobe(a, front + 0.05f, 13f);
                float estuary = CoastLobe(a, mouthAngle, 24f);
                float shelter = Mathf.Max(bay, estuary);
                float brokenEdge = ProceduralArt.Fbm(Mathf.Cos(a) * 3.4f + Off, Mathf.Sin(a) * 3.4f, 927, 3) - 0.5f;
                float radius = 10.45f + 0.69f * Mathf.Cos(2f * ridgeAngle + 0.8f)
                    + 0.45f * Mathf.Cos(ridgeAngle) + 0.31f * Mathf.Sin(3f * a + _seed.WindAngle)
                    + 1.35f * cape + 0.82f * point - 1.8f * shelter + brokenEdge * (0.7f + cape * 0.85f);
                radius = Mathf.Lerp(radius, Mathf.Max(radius, 10.65f), springHill);
                float rock = 0.36f + 0.60f * cape + 0.55f * point + 0.26f * CoastLobe(a, _seed.RidgeAngle, 4f)
                    + 0.20f * Mathf.Sin(3f * a - _seed.WindAngle) + brokenEdge * 0.52f;
                _coast[i].Radius = Mathf.Clamp(radius, 8.55f, 12.35f);
                _coast[i].Rock = Ramp(0.16f, 0.92f, rock) * (1f - shelter * 0.96f);
            }
            for (int i = 0; i < count; i++)
            {
                // Correct the radial distance so that the surf does not stretch across a
                // steeply angled shore at the sides of a headland.
                float tangent = (_coast[(i + 1) % count].Radius - _coast[(i + count - 1) % count].Radius) * count / (2f * Turn);
                float r = _coast[i].Radius;
                _coast[i].Across = r / Mathf.Sqrt(r * r + tangent * tangent);
            }
        }

        private static CoastProfile CoastAt(float angle)
        {
            if (_coast == null) BuildCoast();
            float sample = Mathf.Repeat(angle / Turn, 1f) * _coast.Length;
            int i = Mathf.FloorToInt(sample);
            float t = sample - i;
            var a = _coast[i % _coast.Length];
            var b = _coast[(i + 1) % _coast.Length];
            return new CoastProfile
            {
                Radius = Mathf.Lerp(a.Radius, b.Radius, t),
                Rock = Mathf.Lerp(a.Rock, b.Rock, t),
                Across = Mathf.Lerp(a.Across, b.Across, t)
            };
        }

        internal static float ShoreRadius(float angle) => CoastAt(angle).Radius;
        internal static float ShoreRockiness(float angle) => CoastAt(angle).Rock;

        internal static Vector2 ShelteredLand(float angle, float preferredRadius, float coastClearance, float riverClearance,
            Vector2 avoid = default, float clearance = 0f)
        {
            // Move large scenery a little inward at narrow bays. Try along the slope if a
            // radial move would put the object in the river.
            float radius = Mathf.Min(preferredRadius, ShoreRadius(angle) - coastClearance);
            var original = IslandSeed.At(angle, radius);
            float clearanceSquared = clearance * clearance;
            if (HasRoom(original)) return original;
            int attempts = clearance > 0f ? 13 : 5;
            for (int i = 1; i <= attempts; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float a = angle + side * i * 0.072f;
                    float r = Mathf.Min(preferredRadius, ShoreRadius(a) - coastClearance);
                    var p = IslandSeed.At(a, r);
                    if (HasRoom(p)) return p;
                }
            }
            var inland = IslandSeed.At(angle, Mathf.Min(radius, 6.15f));
            if (clearance <= 0f || HasRoom(inland)) return inland;
            return IslandSeed.At(angle + Mathf.Sign(Mathf.DeltaAngle(Azimuth(avoid), angle * Mathf.Rad2Deg)) * 0.42f, 6.15f);

            bool HasRoom(Vector2 p) => (p - avoid).sqrMagnitude >= clearanceSquared
                && DistanceToRiver(p.x, p.y) >= riverClearance;

            float Azimuth(Vector2 p) => Mathf.Atan2(p.y - Center.y, p.x - Center.x) * Mathf.Rad2Deg;
        }

        private static float Ramp(float start, float end, float value) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));

        private static float ShelfDepth(float offshore, float rock) =>
            ShelfDepthRange * Ramp(0f, Mathf.Lerp(3.65f, 1.42f, rock), offshore);

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
        private static float Outer(float d) => Ramp(GardenRadius, 8.8f, d);

        public static float Height(float x, float z)
        {
            var offset = new Vector2(x, z) - Center;
            float d = offset.magnitude;
            if (d <= GardenRadius) return GridHeight(x, z);
            var coast = CoastAt(Mathf.Atan2(offset.y, offset.x));
            float inland = coast.Radius - d;
            if (inland <= 0f) return SeaLevel - ShelfDepth(-inland * coast.Across, coast.Rock);

            float h = GridHeight(x, z);
            float outer = Outer(d);
            float broad = ProceduralArt.Fbm(x * 0.24f + 3.1f + Off, z * 0.24f + 7.7f - Off, 913, 3);
            float hills = (broad - 0.45f) * _seed.HillAmp;
            float ridge = Ramp(2.8f, 9.2f, Vector2.Dot(offset, _ridge));
            float bumpH = 0f;
            foreach (var b in _seed.Bumps)
            {
                float dd = (x - b.X) * (x - b.X) + (z - b.Z) * (z - b.Z);
                bumpH += b.H * Mathf.Exp(-dd / (2f * b.R * b.R));
            }
            float relief = hills + bumpH * 0.82f + ridge * (_seed.BackAmp + broad * 0.38f);
            float rock = coast.Rock;
            float eroded = ProceduralArt.Fbm(x * 1.13f + Off, z * 1.13f, 938, 3) - 0.5f;
            float ledges = Mathf.Sin(inland * 5.4f + x * 1.06f - z * 0.57f + eroded * 3.5f);
            relief += rock * (0.42f + _seed.RimAmp + eroded * 0.67f + ledges * 0.12f);
            relief -= (1f - rock) * Ramp(0.5f, 3f, 4.3f - inland) * 0.42f;
            h += relief * outer;

            // Cut a channel through the relief before tapering the land into the bay.
            float carve = (1f - Ramp(0.18f, 1.12f, DistanceToRiver(x, z))) * outer;
            h -= carve * (0.30f + Mathf.Max(0f, relief) * 0.63f);
            float sp = Vector2.Distance(new Vector2(x, z), RiverPath[0]);
            h -= 0.26f * (1f - Ramp(0.25f, 0.95f, sp));

            float width = Mathf.Min(coast.Radius - GardenRadius, Mathf.Lerp(3.9f, 1.65f + eroded * 0.75f, rock) / coast.Across);
            float t = Mathf.Clamp01(inland / width);
            // The beach starts low and long. The bedrock rises in two uneven shelves.
            float beach = t * (0.52f + t * (1.96f - 1.48f * t));
            float cliff = t * (2f - t) * 0.62f
                + Ramp(0.10f, 0.36f, t + eroded * 0.24f) * 0.22f
                + Ramp(0.52f, 0.95f, t + eroded * 0.35f) * 0.16f;
            return Mathf.Lerp(SeaLevel, Mathf.Max(SeaLevel + 0.14f, h), Mathf.Lerp(beach, cliff, rock));
        }

        /// <summary>Polar mesh of the whole island (centre outwards), uv = world xz mapped to the colour bake.</summary>
        public static Mesh BuildMesh()
        {
            const int rings = 88, segments = 208;
            const float radius = 12.9f;
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

        internal static float DistanceInland(float x, float z, out float rock)
        {
            var offset = new Vector2(x, z) - Center;
            var coast = CoastAt(Mathf.Atan2(offset.y, offset.x));
            rock = coast.Rock;
            return (coast.Radius - offset.magnitude) * coast.Across;
        }

        /// <summary>Red is signed distance to the coast, green is depth, blue is surf exposure.</summary>
        public static Texture2D BakeCoastMap()
        {
            const int size = 384;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f) * (CoastMapWidth / size);
                    var coast = CoastAt(Mathf.Atan2(p.y, p.x));
                    float offshore = (p.magnitude - coast.Radius) * coast.Across;
                    pixels[y * size + x] = new Color(
                        Mathf.Clamp01(0.5f + offshore / CoastDistanceRange),
                        ShelfDepth(offshore, coast.Rock) / ShelfDepthRange,
                        coast.Rock, 1f);
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Island Coast and Shelf",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // Kept from the bake so the ground can be repainted: bare earth greening with the island's
        // vitality (from the water outward), and the beds coloured by live soil moisture.
        private static Color[] _bakePixels, _meadow, _bare;
        private static Color32[] _shoreColor;
        private static float[] _earthMask, _shade, _greenAt, _bankF, _shoreF;
        private static List<KeyValuePair<int, float>>[] _bedWeights;
        private static Texture2D _bakeTex;
        private static readonly Color Sand = new Color(0.78f, 0.71f, 0.57f);
        private static readonly Color WetSand = new Color(0.51f, 0.49f, 0.39f);
        private static readonly Color DarkRock = new Color(0.29f, 0.34f, 0.36f);
        private static readonly Color LightRock = new Color(0.56f, 0.60f, 0.57f);
        private static readonly Color Bank = new Color(0.31f, 0.42f, 0.27f);

        // Use the same broad, curved patches to place the blades and colour the ground beneath them.
        internal static float MeadowPattern(float x, float z, Vector2 wind)
        {
            float along = x * wind.x + z * wind.y;
            float across = -x * wind.y + z * wind.x;
            float curve = ProceduralArt.Fbm(x * 0.18f + Off, z * 0.18f, 73, 2) - 0.5f;
            float cover = ProceduralArt.Fbm(along * 0.28f + Off, across * 0.54f + curve * 1.4f, 77, 3);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.27f, 0.74f, cover));
        }

        internal static float MeadowGrowthStart(float riverDistance, float cover) =>
            0.16f + Mathf.Min(0.16f, riverDistance * 0.025f) + (0.5f - cover) * 0.16f;

        // Some hardy vegetation is present from the first visit, along the slopes and banks.
        internal static float PioneerCover(float radius, float inland, float river, float cover)
        {
            float outer = Ramp(5.3f, 7.7f, radius);
            float coast = Ramp(0.86f, 2.2f, inland) * Ramp(0.22f, 0.70f, cover);
            float bank = (1f - Ramp(1.08f, 2.6f, river)) * Ramp(0.50f, 0.85f, river);
            return outer * Mathf.Max(coast, bank * 0.76f);
        }

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
                float green = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_greenAt[i] - 0.13f, _greenAt[i] + 0.14f, vitality));
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
                col = Color.Lerp(col, Bank, _bankF[i] * 0.63f * green);
                col = Color.Lerp(col, _shoreColor[i], _shoreF[i]);
                col *= _shade[i];
                _bakePixels[i] = new Color(col.r, col.g, col.b, 1f);
            }
            _bakeTex.SetPixels(_bakePixels);
            _bakeTex.Apply();
        }

        /// <summary>Top-down colour of the island (bare at first; see PaintBeds for the living state).</summary>
        public static Texture2D BakeColor() => BakeColor(null);

        internal static Texture2D BakeColor(CottagePlot cottage)
        {
            const int size = 512;
            const int heightSteps = 128;
            const float heightStep = BakeSize / heightSteps;
            var heights = new float[heightSteps + 1, heightSteps + 1];
            for (int y = 0; y <= heightSteps; y++)
                for (int x = 0; x <= heightSteps; x++)
                    heights[x, y] = Height(BakeMin + x * heightStep, BakeMin + y * heightStep);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "IslandColor", wrapMode = TextureWrapMode.Clamp };
            int n = size * size;
            _bakePixels = new Color[n]; _meadow = new Color[n]; _bare = new Color[n]; _shoreColor = new Color32[n];
            _earthMask = new float[n]; _shade = new float[n]; _greenAt = new float[n]; _bankF = new float[n]; _shoreF = new float[n];
            _bedWeights = new List<KeyValuePair<int, float>>[n];
            var meadow = _seed.Meadow;
            var meadowLight = _seed.MeadowLight;
            var moss = _seed.Moss;
            var wind = new Vector2(Mathf.Cos(_seed.WindAngle), Mathf.Sin(_seed.WindAngle));
            var soilA = new Color(0.51f, 0.42f, 0.33f);
            var soilB = new Color(0.39f, 0.34f, 0.30f);
            var clay = new Color(0.65f, 0.52f, 0.40f);
            var dampBank = new Color(0.33f, 0.29f, 0.24f);
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    float x = BakeMin + (px + 0.5f) / size * BakeSize, z = BakeMin + (py + 0.5f) / size * BakeSize;
                    float n1 = ProceduralArt.Fbm(x * 0.55f, z * 0.55f, 71, 4);
                    float n2 = ProceduralArt.Fbm(x * 2.4f, z * 2.4f, 72, 3);
                    int pi = py * size + px;
                    float cover = MeadowPattern(x, z, wind);
                    var col = Color.Lerp(meadow, meadowLight, n1 * 0.45f + cover * 0.30f);
                    // Fine strands merge into a quiet ground colour at a distance.
                    float along = x * wind.x + z * wind.y;
                    float across = -x * wind.y + z * wind.x;
                    float fibers = ProceduralArt.Fbm(along * 1.4f + Off, across * 5.1f + (cover - 0.5f) * 1.8f, 75, 2);
                    _meadow[pi] = Color.Lerp(col, moss, 0.34f * (1f - cover)) * (0.95f + 0.10f * fibers);
                    float r = DistanceToRiver(x, z);
                    var bare = Color.Lerp(soilA, soilB, Mathf.SmoothStep(0.3f, 0.7f, n2));
                    bare = Color.Lerp(bare, clay, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.72f, n1)) * 0.60f);
                    float bank = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.6f, r));
                    _bare[pi] = Color.Lerp(bare, dampBank, bank * (0.50f + 0.30f * n2));
                    // Life spreads from the water: greening starts by the river and the spring.
                    _greenAt[pi] = MeadowGrowthStart(r, cover);
                    // Earth under the beds as soft organic patches with a wide, noisy edge (never a frame).
                    int cx0 = Mathf.RoundToInt(x), cz0 = Mathf.RoundToInt(z);
                    // Bed density: a smooth sum over nearby soil cells (no per-cell spots), edged with noise.
                    float wobble = (n1 - 0.5f) * 0.5f;
                    {
                        List<KeyValuePair<int, float>> weights = null;
                        float density = 0f;
                        for (int dz = -3; dz <= 3; dz++)
                            for (int dx = -3; dx <= 3; dx++)
                            {
                                int cx = cx0 + dx, cz = cz0 + dz;
                                if (cx < 0 || cx > 15 || cz < 0 || cz > 15 || GardenMapData.SoilCapacity[cz * 16 + cx] == 0) continue;
                                float dd = Vector2.Distance(new Vector2(x, z), Warp(cz * 16 + cx));
                                if (dd > 3.2f) continue;
                                float w = Mathf.Exp(-dd * dd / (2f * 1.35f * 1.35f));
                                (weights ??= new List<KeyValuePair<int, float>>()).Add(new KeyValuePair<int, float>(cz * 16 + cx, w));
                                density += w;
                            }
                        float edge = density + wobble * 2f + (0.5f - cover) * 0.85f;
                        float mask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 3.25f, edge)) * (0.47f + 0.23f * n2);
                        _earthMask[pi] = mask;
                        if (mask > 0.001f) _bedWeights[pi] = weights;
                    }
                    _bankF[pi] = bank;
                    // Fine grain and mid-scale mottling so the ground never reads as a flat colour up close.
                    float grain = ProceduralArt.Fbm(x * 5.5f + Off, z * 5.5f, 91, 2);
                    float mid = ProceduralArt.Fbm(x * 1.4f, z * 1.4f, 92, 3);
                    _shade[pi] = (0.95f + 0.10f * n2) * (0.94f + 0.12f * grain) * (0.95f + 0.10f * mid);
                    float inland = DistanceInland(x, z, out float rockiness);
                    float pioneer = PioneerCover(Vector2.Distance(Center, new Vector2(x, z)), inland, r, cover);
                    _bare[pi] = Color.Lerp(_bare[pi], Color.Lerp(moss, meadow, 0.35f) * (0.86f + 0.20f * fibers), pioneer * 0.86f);
                    float boundary = inland + (mid - 0.5f) * 0.9f + (n1 - 0.5f) * 1.35f;
                    _shoreF[pi] = 1f - Ramp(0.40f, Mathf.Lerp(2.05f, 2.58f, rockiness), boundary);
                    if (_shoreF[pi] > 0f)
                    {
                        float hx = (px + 0.5f) * (heightSteps / (float)size), hz = (py + 0.5f) * (heightSteps / (float)size);
                        int ix = (int)hx, iz = (int)hz;
                        float tx = hx - ix, tz = hz - iz;
                        float a = heights[ix, iz], b = heights[ix + 1, iz], c = heights[ix, iz + 1], e = heights[ix + 1, iz + 1];
                        float low = Mathf.Lerp(a, b, tx), high = Mathf.Lerp(c, e, tx);
                        float elevation = Mathf.Lerp(low, high, tz) - SeaLevel;
                        float slopeX = Mathf.Lerp(b - a, e - c, tz), slopeZ = high - low;
                        float slope = Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ) / heightStep;

                        float wet = 1f - Ramp(0.025f, 0.38f, inland + (n2 - 0.5f) * 0.17f);
                        var sand = Color.Lerp(Sand * (0.89f + 0.19f * grain + 0.14f * mid), WetSand, wet * 0.83f);
                        // Skew the strata and break them into short weathered faces.
                        float strata = Mathf.Sin((elevation + x * 0.14f - z * 0.08f + (mid - 0.5f) * 0.28f) * 12.8f);
                        float faceNoise = ProceduralArt.Fbm((x + z * 0.71f) * 1.12f + Off, elevation * 2.15f, 927, 3);
                        float face = Ramp(0.23f, 0.76f, faceNoise * 0.74f + n1 * 0.26f);
                        var rock = Color.Lerp(DarkRock, LightRock, Mathf.Clamp01(face * 0.62f + Ramp(-0.30f, 0.46f, strata) * 0.28f));
                        rock *= (1f - wet * 0.26f) * (0.88f + grain * 0.19f);
                        float ledgeMoss = Ramp(0.36f, 1.30f, inland) * (1f - Ramp(0.55f, 1.6f, slope)) * Ramp(0.30f, 0.59f, mid);
                        rock = Color.Lerp(rock, moss * 0.94f, ledgeMoss * 0.79f);
                        float exposed = Ramp(0.15f, 0.76f, rockiness + (n1 - 0.5f) * 0.45f + Ramp(0.55f, 1.75f, slope) * 0.17f);
                        _shoreColor[pi] = Color.Lerp(sand, rock, exposed);
                    }
                    // Blend the worn approach into both soil colours so the path survives greening.
                    float pathDistance = cottage != null ? cottage.PathDistance(new Vector2(x, z)) : float.PositiveInfinity;
                    if (pathDistance < 0.70f && r > 0.68f && inland > 0.35f)
                    {
                        float width = 0.39f + (grain - 0.5f) * 0.17f + (n2 - 0.5f) * 0.13f;
                        float worn = (1f - Ramp(width * 0.43f, width + 0.13f, pathDistance))
                            * (1f - _earthMask[pi] * 0.80f) * Ramp(0.35f, 0.92f, inland);
                        var gravel = Color.Lerp(new Color(0.49f, 0.43f, 0.33f), new Color(0.65f, 0.57f, 0.43f), grain);
                        _bare[pi] = Color.Lerp(_bare[pi], gravel, worn * 0.88f);
                        _meadow[pi] = Color.Lerp(_meadow[pi], gravel, worn * 0.91f);
                    }
                    var bareCol = Color.Lerp(_bare[pi], _shoreColor[pi], _shoreF[pi]) * _shade[pi];
                    _bakePixels[pi] = new Color(bareCol.r, bareCol.g, bareCol.b, 1f);
                }
            tex.SetPixels(_bakePixels);
            tex.Apply();
            _bakeTex = tex;
            return tex;
        }

        /// <summary>River surface along the carved bed, with level cross sections and soft banks.</summary>
        public static Mesh BuildRiverMesh()
        {
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < RiverPath.Length; i++)
            {
                Vector2 p0 = RiverPath[Mathf.Max(0, i - 1)], p1 = RiverPath[i], p2 = RiverPath[i + 1],
                    p3 = RiverPath[Mathf.Min(RiverPath.Length - 1, i + 2)];
                const int steps = 16;
                for (int k = 0; k < steps; k++)
                {
                    float t = k / (float)steps;
                    pts.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
                                    (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
                }
            }
            pts.Add(RiverPath[RiverPath.Length - 1]);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var tris = new List<int>();
            float along = 0f;
            const int columns = 9;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 dir = (i + 1 < pts.Count ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x);
                float k = i / (float)(pts.Count - 1);
                float half = Mathf.Lerp(0.27f, 0.48f, k) + Ramp(0.82f, 1f, k) * 0.30f;
                half += 0.055f * Mathf.Sin(along * 1.18f);
                if (i > 0) along += Vector2.Distance(pts[i], pts[i - 1]);
                float bed = Height(pts[i].x, pts[i].y);
                float water = bed + Mathf.Lerp(0.16f, 0.10f, k);
                float mouth = Ramp(SeaLevel + 0.025f, SeaLevel + 0.24f, water);
                for (int col = 0; col < columns; col++)
                {
                    float u = col / (float)(columns - 1);
                    Vector2 q = pts[i] + side * (half * (u * 2f - 1f));
                    float ground = Height(q.x, q.y);
                    verts.Add(new Vector3(q.x, water, q.y));
                    uvs.Add(new Vector2(u, along * 0.35f));
                    float bank = Ramp(-0.02f, 0.075f, water - ground);
                    colors.Add(new Color(1f, 1f, 1f, mouth * bank));
                    if (i == 0 || col == 0) continue;
                    int d = i * columns + col;
                    int c = d - 1, b = d - columns, a = b - 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }
            var mesh = new Mesh { name = "River" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
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
