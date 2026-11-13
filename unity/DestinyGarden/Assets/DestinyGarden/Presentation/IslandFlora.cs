using UnityEngine;
using UnityEngine.Rendering;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// Low meadow and taller plants that follow the island's terrain. Geometry is merged
    /// by quadrant and grows with the vitality derived from the saved garden.
    /// </summary>
    public sealed class IslandFlora : MonoBehaviour
    {
        private Material _mat;
        private float _vitality = 0.02f, _target = 0.02f;
        public float Vitality => _vitality;

        // Succession: the vitality at which each kind can first live here. Pioneers at the water
        // come first, then grasses spreading from damp ground, meadow flowers, and only after the
        // trees have grown, the plants of their shade.
        private static readonly float[] AccentStages =
        {
            0.24f, // TallGrass
            0.34f, // Clover
            0.05f, // Moss
            0.64f, // Fern
            0.08f, // Reed
            0.28f, // SeaPink
            0.40f, // Thyme
            0.72f, // Bluebell
            0.50f, // Wildflower
            0.12f, // Lichen
            0.56f, // Toadstool
        };
        private float _gust;
        public bool ReducedMotion;

        // Biome and StageJitter use index 0 for the short grass that forms the meadow.
        private enum Kind { None, TallGrass, Clover, Moss, Fern, Reed, SeaPink, Thyme, Bluebell, Wildflower, Lichen, Toadstool }
        private static readonly Color ReedGreen = new Color(0.15f, 0.27f, 0.12f);
        private static readonly Color ReedTip = new Color(0.29f, 0.42f, 0.22f);
        private Vector3 _wind;
        private float _off;

        private static float Ramp(float start, float end, float x) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, x));

        private float Colony(float x, float z, int seed, float scale) =>
            Ramp(0.36f, 0.72f, ProceduralArt.Fbm(
                x * scale + seed * 0.37f + _off, z * scale + seed * 0.61f - _off, 600 + seed, 3));

        public void Build()
        {
            var shader = Shader.Find("DestinyGarden/Foliage");
            _mat = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            Shader.SetGlobalFloat("_DGVitality", _vitality);
            Shader.SetGlobalFloat("_DGWindGust", 0f);
            Shader.SetGlobalFloat("_DGWindMotion", 1f);
            Shader.SetGlobalColor("_DGAmbient", RenderSettings.ambientLight * 0.85f);

            var chunks = new FoliageMesh[4];
            for (int i = 0; i < chunks.Length; i++) chunks[i] = new FoliageMesh();
            var isl = IslandTerrain.Seed;
            _wind = new Vector3(Mathf.Cos(isl.WindAngle), 0f, Mathf.Sin(isl.WindAngle));
            var windXZ = new Vector2(_wind.x, _wind.z);
            _off = isl.FloraOffset * 0.11f;
            Shader.SetGlobalVector("_DGWindDirection", _wind);

            var positions = new System.Random(7123 + isl.Value % 100003);
            var forms = new System.Random(9337 + isl.Value % 100019);
            var housePlot = GetComponent<GardenView>()?.HousePlot;
            var grassRoot = Color.Lerp(isl.Moss.linear, isl.Meadow.linear, 0.43f);
            var grassTip = Color.Lerp(isl.Meadow.linear, isl.MeadowLight.linear, 0.38f) * 1.14f;
            const float step = 0.255f;
            for (float gz = -4.9f; gz <= 19.9f; gz += step)
                for (float gx = -4.9f; gx <= 19.9f; gx += step)
                {
                    float x = gx + (float)(positions.NextDouble() - 0.5) * step;
                    float z = gz + (float)(positions.NextDouble() - 0.5) * step;
                    if (housePlot != null && housePlot.Overlaps(new Vector2(x, z), 0.04f)) continue;
                    float inland = IslandTerrain.DistanceInland(x, z, out float rockiness);
                    if (inland < Mathf.Lerp(1.10f, 0.60f, rockiness)) continue;
                    float h = IslandTerrain.Height(x, z);
                    if (h < IslandTerrain.SeaLevel + 0.18f) continue;
                    if (inland < 2.7f)
                    {
                        const float stride = 0.18f;
                        float dx = IslandTerrain.Height(x + stride, z) - h, dz = IslandTerrain.Height(x, z + stride) - h;
                        if (dx * dx + dz * dz > 1.45f * 1.45f * stride * stride) continue;
                    }
                    float river = IslandTerrain.DistanceToRiver(x, z);
                    if (river < 0.63f) continue;
                    float bed = IslandTerrain.DistanceToBed(x, z);
                    if (bed < 0.61f) continue;
                    float cover = IslandTerrain.MeadowPattern(x, z, windXZ);
                    float bedEdge = Ramp(0.60f, 1.55f, bed + (cover - 0.5f) * 0.44f);
                    if (bedEdge <= 0f) continue;
                    float shade = IslandTerrain.ShadeAt(x, z);
                    float bank = 1f - Ramp(0.75f, 1.48f, river);
                    float shore = 1f - Ramp(0.94f, 1.95f, inland);
                    float greenAt = IslandTerrain.MeadowGrowthStart(river, cover);
                    float radius = Vector2.Distance(IslandTerrain.Center, new Vector2(x, z));
                    float pioneer = IslandTerrain.PioneerCover(radius, inland, river, cover);
                    float meadow = bedEdge * Ramp(0.58f, 1.55f, inland) * (1f - bank * 0.52f);
                    float density = Mathf.Clamp01((0.39f + cover * 0.80f) * meadow * (1f - shade * 0.27f) *
                        (0.72f + Mathf.Min(isl.Biome[0], 1.6f) * 0.25f));

                    var mesh = chunks[(x < IslandTerrain.Center.x ? 0 : 1) + (z < IslandTerrain.Center.y ? 0 : 2)];
                    var p = new Vector3(x, h - 0.018f, z);
                    if (positions.NextDouble() < density)
                    {
                        float bladeThreshold = Mathf.Lerp(greenAt, -0.065f, Ramp(0.27f, 0.77f, pioneer));
                        AddMeadowBlade(mesh, p, cover, shade, bladeThreshold, grassRoot, grassTip, forms);
                    }

                    // Accent plants occupy the stronger parts of a habitat, leaving long stretches of shore open.
                    if (positions.NextDouble() > 0.40f * bedEdge) continue;
                    float dry = Ramp(0.45f, 1.32f, h) * (1f - bank);
                    Kind kind = Kind.None;
                    float best = 0.29f;
                    Consider(Kind.TallGrass, cover * meadow * (1f - shade * 0.62f), 0.44f);
                    Consider(Kind.Clover, meadow * (0.22f + bank * 0.4f) * (0.55f + cover * 0.45f), 0.56f);
                    Consider(Kind.Moss, shade * (0.48f + bank * 0.52f), 0.62f);
                    Consider(Kind.Fern, shade * (1f - bank * 0.65f), 0.57f);
                    Consider(Kind.Reed, bank * 1.28f, 0.38f);
                    Consider(Kind.SeaPink, shore * (1f - bank) * 0.82f, 0.32f);
                    Consider(Kind.Thyme, dry * (1f - shade) * 0.58f, 0.46f);
                    Consider(Kind.Bluebell, shade * (1f - bank) * 0.63f, 0.52f);
                    Consider(Kind.Wildflower, meadow * cover * 0.58f, 0.42f);
                    Consider(Kind.Lichen, shade * dry * 0.46f, 0.65f);
                    Consider(Kind.Toadstool, shade * bank * 0.64f, 0.62f);
                    if (kind == Kind.None) continue;

                    int k = (int)kind;
                    float threshold = Mathf.Clamp(AccentStages[k - 1] + isl.StageJitter[k] * 0.55f
                        + (1f - Mathf.Clamp01(best)) * 0.09f, 0.05f, 0.95f);
                    if (kind == Kind.Reed || kind == Kind.Moss)
                        threshold -= Ramp(0.20f, 0.67f, pioneer) * 0.13f;
                    mesh.Root = new Vector4(p.x, p.y, p.z, threshold);
                    Place(mesh, kind, p, forms);

                    void Consider(Kind candidate, float habitat, float scale)
                    {
                        int index = (int)candidate;
                        float patch = Colony(x, z, index, scale);
                        float score = habitat * Ramp(0.37f, 0.86f, patch) * Mathf.Min(1.50f, isl.Biome[index]);
                        if (score <= best) return;
                        best = score;
                        kind = candidate;
                    }
                }

            for (int i = 0; i < chunks.Length; i++)
            {
                var obj = new GameObject("Flora" + i);
                obj.transform.SetParent(transform, false);
                obj.AddComponent<MeshFilter>().sharedMesh = chunks[i].ToMesh("Flora" + i);
                var r = obj.AddComponent<MeshRenderer>();
                r.sharedMaterial = _mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private void AddMeadowBlade(FoliageMesh mesh, Vector3 root, float cover, float shade, float threshold,
            Color rootColor, Color tipColor, System.Random rng)
        {
            float variation = (float)rng.NextDouble();
            float height = Mathf.Lerp(0.13f, 0.28f, cover * 0.67f + variation * 0.33f);
            float yaw = IslandTerrain.Seed.WindAngle + Mathf.PI * 0.5f + (float)(rng.NextDouble() - 0.5) * 1.45f;
            var across = new Vector3(-_wind.z, 0f, _wind.x);
            var lean = _wind * Mathf.Lerp(0.9f, 1.55f, cover) + across * ((variation - 0.5f) * 0.8f);
            float tone = (0.87f + 0.23f * cover) * (1f - 0.18f * shade);
            mesh.Root = new Vector4(root.x, root.y, root.z, threshold);
            mesh.Blade(root, lean, height, Mathf.Lerp(0.020f, 0.044f, variation),
                rootColor * tone, tipColor * tone, yaw);
        }

        private void Place(FoliageMesh b, Kind kind, Vector3 p, System.Random rng)
        {
            float R() => (float)rng.NextDouble();
            float yaw = R() * Mathf.PI * 2f;
            Vector3 Lean(float amount) { float a = R() * Mathf.PI * 2f; return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * amount; }
            // Colour drifts smoothly across the island (a sward is one colour), with a little per-plant variation.
            float tone = (0.9f + 0.22f * ProceduralArt.Fbm(p.x * 0.32f + _off, p.z * 0.32f, 55, 2)) * (0.97f + 0.06f * R());
            Vector3 Wind(float amount) => _wind * amount;
            switch (kind)
            {
                case Kind.TallGrass:
                {
                    float patch = Colony(p.x, p.z, 22, 0.24f);
                    float height = 0.28f + 0.28f * patch;
                    int n = 3 + rng.Next(2);
                    var baseC = new Color(0.13f, 0.23f, 0.095f) * tone;
                    var tipC = new Color(0.24f, 0.37f, 0.15f) * tone;
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * (Mathf.PI * 2f / n) + yaw;
                        var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        var root = p + outward * (0.05f + R() * 0.10f);
                        var lean = outward * (0.10f + 0.17f * R()) + Wind(0.72f + 0.35f * R());
                        float hh = height * (0.75f + 0.5f * R());
                        b.Blade(root, lean, hh, 0.022f, baseC, tipC, a + 1.57f);
                        if (i == 0 && R() < 0.24f)
                            b.Leaf(root + Vector3.up * hh + lean * hh, Vector3.up + lean,
                                0.08f, 0.012f, new Color(0.39f, 0.35f, 0.17f), 1f);
                    }
                    break;
                }
                case Kind.Clover:
                {
                    var leafC = new Color(0.15f, 0.28f, 0.14f) * tone;
                    int stems = 3 + rng.Next(2);
                    for (int i = 0; i < stems; i++)
                    {
                        var c = p + Lean(0.11f + 0.08f * R()) + Vector3.up * (0.045f + R() * 0.03f);
                        for (int l = 0; l < 3; l++)
                        {
                            float a = l * 2.094f + yaw + i * 1.2f;
                            b.Leaf(c, new Vector3(Mathf.Cos(a), 0.15f, Mathf.Sin(a)), 0.055f + 0.045f * R(),
                                0.029f, leafC * (0.86f + 0.22f * R()), 0.38f);
                        }
                    }
                    break;
                }
                case Kind.Moss:
                {
                    var moss = Color.Lerp(new Color(0.16f, 0.25f, 0.14f), new Color(0.24f, 0.35f, 0.19f), R()) * tone;
                    AddGroundCover(b, p, 0.15f + 0.09f * R(), 0.021f, yaw, moss);
                    break;
                }
                case Kind.Fern:
                {
                    var baseC = new Color(0.10f, 0.25f, 0.15f) * tone;
                    var tipC = new Color(0.20f, 0.39f, 0.19f) * tone;
                    int n = 3 + rng.Next(3);
                    for (int i = 0; i < n; i++)
                    {
                        float a = yaw + i * (Mathf.PI * 2f / n);
                        var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        var side = Vector3.Cross(Vector3.up, outward);
                        float height = 0.18f + R() * 0.11f;
                        b.Blade(p, outward * 1.35f, height, 0.014f, baseC, tipC, a + Mathf.PI / 2f);
                        for (int k = 1; k <= 5; k++)
                        {
                            float t = 0.35f + k * 0.115f;
                            var at = p + Vector3.up * (height * t) + outward * (1.35f * height * t * t);
                            float length = (0.12f - 0.09f * t) * (0.86f + 0.27f * R());
                            var color = Color.Lerp(baseC, tipC, t);
                            b.Leaf(at, outward + side * 1.6f, length, length * 0.27f, color, t);
                            b.Leaf(at, outward - side * 1.6f, length, length * 0.27f, color, t);
                        }
                    }
                    break;
                }
                case Kind.Reed:
                {
                    int n = 3 + rng.Next(3);
                    for (int i = 0; i < n; i++)
                    {
                        float h = 0.35f + R() * 0.32f;
                        var root = p + Lean(0.05f + 0.12f * R());
                        var lean = Lean(0.18f) + Wind(0.35f + 0.2f * R());
                        b.Blade(root, lean, h, 0.020f, ReedGreen * tone, ReedTip * tone, yaw + i * 0.9f);
                        if (i == 0 && R() < 0.38f)
                            b.Leaf(root + Vector3.up * h + lean * h, Vector3.up + lean,
                                0.095f, 0.014f, new Color(0.34f, 0.25f, 0.13f), 1f);
                    }
                    break;
                }
                case Kind.SeaPink:
                {
                    var leaf = new Color(0.16f, 0.26f, 0.14f) * tone;
                    var pink = Color.Lerp(new Color(0.40f, 0.22f, 0.22f), new Color(0.61f, 0.36f, 0.33f), R());
                    for (int i = 0; i < 5; i++)
                    {
                        var lean = Lean(0.55f) + Wind(0.55f);
                        var root = p + Lean(0.16f);
                        b.Blade(root, lean, 0.06f + 0.08f * R(), 0.018f, leaf * 0.78f, leaf, yaw + i);
                    }
                    if (R() < 0.35f) break;
                    float h = 0.12f + R() * 0.10f;
                    var stemLean = Lean(0.26f) + Wind(0.28f);
                    b.Blade(p, stemLean, h, 0.008f, leaf, leaf, yaw);
                    b.Corolla(p + Vector3.up * h + stemLean * h, 4, yaw, 0.031f, 0.012f, 0.45f, 0.25f,
                        pink * 0.74f, pink);
                    break;
                }
                case Kind.Thyme:
                {
                    var leaf = new Color(0.23f, 0.33f, 0.18f) * tone;
                    AddGroundCover(b, p, 0.15f + 0.09f * R(), 0.028f, yaw, leaf);
                    for (int i = 0; i < 3; i++)
                        b.Leaf(p + Lean(0.12f) + Vector3.up * 0.055f, Lean(1f) + Vector3.up * 0.3f,
                            0.065f, 0.019f, leaf * 1.1f, 0.3f);
                    break;
                }
                case Kind.Bluebell:
                {
                    var stemC = new Color(0.30f, 0.50f, 0.32f);
                    int stems = 1 + rng.Next(2);
                    for (int s = 0; s < stems; s++)
                    {
                        var lean = Lean(0.35f);
                        float h = 0.18f + R() * 0.08f;
                        var root = p + Lean(0.05f);
                        b.Blade(root, lean, h, 0.008f, stemC, stemC, yaw + s);
                        var blue = Color.Lerp(new Color(0.21f, 0.24f, 0.43f), new Color(0.32f, 0.32f, 0.51f), R());
                        for (int k = 0; k < 2; k++)
                        {
                            float f = 0.6f + k * 0.14f;
                            var at = root + Vector3.up * (h * f) + lean * (h * f * f) + Vector3.down * 0.025f;
                            b.Ball(at, new Vector3(0.018f, 0.026f, 0.018f), blue, 1f, 5, 2);
                        }
                    }
                    b.Blade(p, Lean(0.4f), 0.14f, 0.02f, stemC, new Color(0.42f, 0.62f, 0.40f), yaw + 2f);
                    break;
                }
                case Kind.Wildflower:
                {
                    float colorBand = Colony(p.x, p.z, 28, 0.23f);
                    var flower = Color.Lerp(new Color(0.49f, 0.30f, 0.34f), new Color(0.60f, 0.46f, 0.24f), colorBand);
                    var stem = new Color(0.15f, 0.29f, 0.15f);
                    float h = 0.15f + R() * 0.12f;
                    var lean = Lean(0.23f) + Wind(0.35f);
                    b.Blade(p, lean, h, 0.008f, stem, stem, yaw);
                    b.Corolla(p + Vector3.up * h + lean * h, 5, yaw, 0.035f, 0.013f, 0.4f, 0.3f, flower * 0.70f, flower);
                    break;
                }
                case Kind.Lichen:
                {
                    var lichen = Color.Lerp(new Color(0.22f, 0.27f, 0.16f), new Color(0.28f, 0.35f, 0.23f), R());
                    AddGroundCover(b, p, 0.11f + 0.08f * R(), 0.013f, yaw, lichen);
                    break;
                }
                case Kind.Toadstool:
                {
                    var cap = R() < 0.5f ? new Color(0.57f, 0.33f, 0.21f) : new Color(0.50f, 0.49f, 0.38f);
                    int count = 1 + rng.Next(2);
                    for (int i = 0; i < count; i++)
                    {
                        var at = p + Lean(0.06f);
                        float h = 0.04f + R() * 0.04f;
                        b.Ball(at + Vector3.up * (h * 0.5f), new Vector3(0.012f, h * 0.55f, 0.012f), new Color(0.44f, 0.40f, 0.30f), 0.1f, 5, 2);
                        b.Ball(at + Vector3.up * h, new Vector3(0.032f, 0.018f, 0.032f), cap, 0.15f, 6, 3);
                    }
                    break;
                }
            }
        }

        private static void AddGroundCover(FoliageMesh mesh, Vector3 root, float radius, float height, float yaw, Color color)
        {
            const int segments = 9;
            color.a = 0f;
            int center = mesh.Add(root + Vector3.up * height, Vector3.up, color);
            int previous = -1;
            for (int i = 0; i < segments; i++)
            {
                float a = i * (2f * Mathf.PI / segments);
                float r = radius * (0.76f + 0.19f * Mathf.Cos(a * 3f + yaw) + 0.12f * Mathf.Sin(a * 2f));
                var p = root + new Vector3(Mathf.Cos(a + yaw) * r * 1.38f, 0f, Mathf.Sin(a + yaw) * r);
                p.y = IslandTerrain.Height(p.x, p.z) - 0.016f;
                int edge = mesh.Add(p, Vector3.up, color * 0.90f);
                if (previous >= 0) { mesh.T.Add(center); mesh.T.Add(edge); mesh.T.Add(previous); }
                previous = edge;
            }
            mesh.T.Add(center); mesh.T.Add(center + 1); mesh.T.Add(previous);
        }

        /// <summary>Target vitality 0..1 derived from the saved garden; the island grows toward it.</summary>
        public void SetVitality(float target) => _target = Mathf.Clamp01(target);

        /// <summary>Show the saved vitality at once (on load, in captures) instead of growing toward it.</summary>
        public void Snap()
        {
            _vitality = _target;
            Shader.SetGlobalFloat("_DGVitality", _vitality);
        }

        /// <summary>A gust of wind across the whole island (for a wind blessing).</summary>
        public void Gust() => _gust = 1f;

        private void Update()
        {
            _vitality = Mathf.MoveTowards(_vitality, _target, Time.deltaTime * 0.05f);
            Shader.SetGlobalFloat("_DGVitality", _vitality);
            _gust = Mathf.MoveTowards(_gust, 0f, Time.deltaTime * 0.35f);
            Shader.SetGlobalFloat("_DGWindGust", ReducedMotion ? 0f : _gust);
            Shader.SetGlobalFloat("_DGWindMotion", ReducedMotion ? 0f : 1f);
            Shader.SetGlobalColor("_DGAmbient", RenderSettings.ambientLight * 0.85f);
        }
    }
}
