using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// The island's living ground cover: about twelve kinds of plants that grow where their
    /// conditions suit them (moisture, rock and tree shade, slope, shore, river banks), each in
    /// its own colonies. Presentation only. Every instance carries a vitality threshold; the
    /// island fills in as the garden's vitality (derived from the saved garden state) rises.
    /// All instances are merged into a few meshes and sway in one wind (DestinyGarden/Foliage).
    /// </summary>
    public sealed class IslandFlora : MonoBehaviour
    {
        private Material _mat;
        private float _vitality = 0.02f, _target = 0.02f;
        public float Vitality => _vitality;

        // Succession: the vitality at which each kind can first live here. Pioneers at the water
        // come first, then grasses spreading from damp ground, meadow flowers, and only after the
        // trees have grown, the plants of their shade.
        private static readonly float[] Stage =
        {
            0.22f, // ShortGrass
            0.56f, // TallGrass
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

        private enum Kind { ShortGrass, TallGrass, Clover, Moss, Fern, Reed, SeaPink, Thyme, Bluebell, Wildflower, Lichen, Toadstool }

        private static Color Hsv(float h, float s, float v) => Color.HSVToRGB(Mathf.Repeat(h, 1f), s, v);

        private static Vector3 _wind = Vector3.right;
        private static float _off;

        // Colonies: large, coherent patches of one kind (a sward, a reed bed), different on every island.
        private static float Colony(float x, float z, int seed, float scale) =>
            0.15f + 1.6f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.68f,
                ProceduralArt.Fbm(x * scale * 0.75f + seed * 0.37f + _off, z * scale * 0.75f + seed * 0.61f - _off, 600 + seed, 3)));

        public void Build()
        {
            var shader = Shader.Find("DestinyGarden/Foliage");
            _mat = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            Shader.SetGlobalFloat("_DGVitality", _vitality);
            Shader.SetGlobalFloat("_DGWindGust", 0f);
            Shader.SetGlobalColor("_DGAmbient", RenderSettings.ambientLight * 0.85f);

            var chunks = new FoliageMesh[4];
            for (int i = 0; i < chunks.Length; i++) chunks[i] = new FoliageMesh();
            var isl = IslandTerrain.Seed;
            _wind = new Vector3(Mathf.Cos(isl.WindAngle), 0f, Mathf.Sin(isl.WindAngle));
            _off = isl.FloraOffset * 0.11f;
            var rng = new System.Random(7123 + isl.Value % 100003);
            const float step = 0.34f; // sparse: each plant has room to live
            int kinds = System.Enum.GetValues(typeof(Kind)).Length;
            var weights = new float[kinds];
            for (float gz = -3.0f; gz <= 18.0f; gz += step)
                for (float gx = -3.0f; gx <= 18.0f; gx += step)
                {
                    float x = gx + (float)(rng.NextDouble() - 0.5) * step * 0.9f;
                    float z = gz + (float)(rng.NextDouble() - 0.5) * step * 0.9f;
                    float d = Vector2.Distance(new Vector2(x, z), IslandTerrain.Center);
                    if (d > 10.3f) continue;
                    float h = IslandTerrain.Height(x, z);
                    if (h < IslandTerrain.SeaLevel + 0.12f) continue;
                    float river = IslandTerrain.DistanceToRiver(x, z);
                    if (river < 0.42f) continue;
                    float bed = IslandTerrain.DistanceToBed(x, z);
                    if (bed < 0.58f) continue;

                    float shore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8.6f, 10.0f, d));
                    float shade = IslandTerrain.ShadeAt(x, z);
                    float bank = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.55f, 1.45f, river));
                    float wet = Mathf.Max(bank, Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.6f, 1.3f, bed)) * 0.6f);
                    float hi = Mathf.InverseLerp(0.3f, 1.4f, h);
                    float dry = (1f - wet) * hi;
                    float meadow = (1f - shore) * (1f - bank * 0.7f) * (1f - shade * 0.6f);

                    weights[(int)Kind.ShortGrass] = isl.Biome[0] * ((meadow + 0.2f) * Colony(x, z, 1, 0.45f));
                    weights[(int)Kind.TallGrass] = isl.Biome[1] * (0.5f * meadow * (1f - shade) * Colony(x, z, 2, 0.38f));
                    weights[(int)Kind.Clover] = isl.Biome[2] * (0.45f * meadow * (0.4f + wet) * Colony(x, z, 3, 0.55f));
                    weights[(int)Kind.Moss] = isl.Biome[3] * ((1.2f * shade * (0.4f + wet) + 0.35f * bank) * Colony(x, z, 4, 0.6f));
                    weights[(int)Kind.Fern] = isl.Biome[4] * (1.4f * shade * Colony(x, z, 5, 0.5f));
                    weights[(int)Kind.Reed] = isl.Biome[5] * (2.0f * bank * Colony(x, z, 6, 0.7f));
                    weights[(int)Kind.SeaPink] = isl.Biome[6] * (2.0f * shore * Colony(x, z, 7, 0.6f));
                    weights[(int)Kind.Thyme] = isl.Biome[7] * (1.2f * dry * (1f - shade) * Colony(x, z, 8, 0.5f));
                    weights[(int)Kind.Bluebell] = isl.Biome[8] * (1.0f * shade * (1f - bank) * Colony(x, z, 9, 0.65f));
                    weights[(int)Kind.Wildflower] = isl.Biome[9] * (0.55f * meadow * Colony(x, z, 10, 0.5f));
                    weights[(int)Kind.Lichen] = isl.Biome[10] * (0.12f * shade * hi * Colony(x, z, 11, 0.8f));
                    weights[(int)Kind.Toadstool] = isl.Biome[11] * (0.32f * shade * wet * Colony(x, z, 12, 0.9f));
                    // A plant grows here only if some kind can truly live here; then one of the kinds
                    // that suit this spot best (within half of the best) takes it.
                    float bestW = 0f;
                    for (int k = 0; k < kinds; k++) bestW = Mathf.Max(bestW, weights[k]);
                    if (bestW < 0.55f) continue;
                    float total = 0f;
                    for (int k = 0; k < kinds; k++) if (weights[k] >= bestW * 0.72f) total += weights[k];
                    double pick = rng.NextDouble() * total;
                    int kind = 0;
                    for (float acc = 0f; kind < kinds; kind++)
                    {
                        if (weights[kind] < bestW * 0.72f) continue;
                        acc += weights[kind];
                        if (pick < acc) break;
                    }
                    if (kind >= kinds) kind = kinds - 1;
                    float suit = Mathf.Clamp01(weights[kind] / 1.6f);
                    float near = (Kind)kind == Kind.Moss || (Kind)kind == Kind.Reed ? Mathf.Min(0.16f, river * 0.035f) : 0f;
                    float threshold = Mathf.Clamp(Stage[kind] + isl.StageJitter[kind] + (1f - suit) * 0.16f + near + (float)rng.NextDouble() * 0.10f, 0.02f, 0.98f);

                    var b = chunks[(x < 7.5f ? 0 : 1) + (z < 7.5f ? 0 : 2)];
                    var p = new Vector3(x, h - 0.01f, z);
                    b.Root = new Vector4(p.x, p.y, p.z, threshold);
                    Place(b, (Kind)kind, p, rng, shade);
                }

            for (int i = 0; i < chunks.Length; i++)
            {
                var obj = new GameObject("Flora" + i);
                obj.transform.SetParent(transform, false);
                obj.AddComponent<MeshFilter>().sharedMesh = chunks[i].ToMesh("Flora" + i);
                var r = obj.AddComponent<MeshRenderer>();
                r.material = _mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private static void Place(FoliageMesh b, Kind kind, Vector3 p, System.Random rng, float shade)
        {
            float R() => (float)rng.NextDouble();
            float yaw = R() * Mathf.PI * 2f;
            Vector3 Lean(float amount) { float a = R() * Mathf.PI * 2f; return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * amount; }
            // Colour drifts smoothly across the island (a sward is one colour), with a little per-plant variation.
            float tone = (0.9f + 0.22f * ProceduralArt.Fbm(p.x * 0.32f + _off, p.z * 0.32f, 55, 2)) * (0.97f + 0.06f * R());
            Vector3 Wind(float amount) => _wind * amount;
            switch (kind)
            {
                case Kind.ShortGrass:
                {
                    // A fountain-shaped tuft: blades fan out from the centre and bend with the island's wind.
                    float patch = Colony(p.x, p.z, 21, 0.28f);
                    float h0 = 0.15f + 0.17f * Mathf.Clamp01(patch / 1.2f);
                    int n = 6 + rng.Next(3);
                    float warm = Mathf.Clamp01(Mathf.InverseLerp(0.5f, 1.3f, p.y)); // higher, drier ground goes golden at the tips
                    var baseC = new Color(0.14f, 0.32f, 0.16f) * tone;
                    var tipC = Color.Lerp(new Color(0.46f, 0.70f, 0.34f), new Color(0.74f, 0.75f, 0.40f), warm * 0.7f) * tone;
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * (Mathf.PI * 2f / n) + yaw;
                        var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        var root = p + outward * (0.02f + R() * 0.05f);
                        var lean = outward * (0.16f + 0.16f * R()) + Wind(0.30f + 0.25f * R());
                        b.Blade(root, lean, h0 * (0.75f + 0.5f * R()), 0.034f + 0.012f * R(), baseC, tipC, a + 1.57f);
                    }
                    break;
                }
                case Kind.TallGrass:
                {
                    float patch = Colony(p.x, p.z, 22, 0.24f);
                    float height = 0.42f + 0.34f * Mathf.Clamp01(patch / 1.2f);
                    int n = 4 + rng.Next(3);
                    var baseC = new Color(0.18f, 0.36f, 0.19f) * tone;
                    var tipC = Color.Lerp(new Color(0.50f, 0.68f, 0.36f), new Color(0.80f, 0.74f, 0.44f), 0.5f) * tone;
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * (Mathf.PI * 2f / n) + yaw;
                        var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        var root = p + outward * (0.02f + R() * 0.05f);
                        var lean = outward * (0.10f + 0.12f * R()) + Wind(0.55f + 0.35f * R());
                        float hh = height * (0.75f + 0.5f * R());
                        b.Blade(root, lean, hh, 0.030f, baseC, tipC, a + 1.57f);
                        if (i < 2 && R() < 0.75f)
                            b.Ball(root + Vector3.up * hh + lean * hh, new Vector3(0.016f, 0.06f, 0.016f), new Color(0.90f, 0.84f, 0.58f), 1f, 5, 2);
                    }
                    break;
                }
                case Kind.Clover:
                {
                    var leafC = new Color(0.30f, 0.58f, 0.32f) * tone;
                    for (int i = 0; i < 3 + rng.Next(3); i++)
                    {
                        var c = p + Lean(0.09f) + Vector3.up * (0.03f + R() * 0.03f);
                        for (int l = 0; l < 3; l++)
                        {
                            float a = l * 2.094f + yaw;
                            b.Ball(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.028f, new Vector3(0.026f, 0.007f, 0.026f), leafC, 0.4f, 5, 2);
                        }
                    }
                    if (R() < 0.45f)
                    {
                        b.Blade(p, Lean(0.1f), 0.09f, 0.008f, leafC, leafC, yaw);
                        b.Ball(p + Vector3.up * 0.10f, new Vector3(0.026f, 0.024f, 0.026f), Color.Lerp(Color.white, new Color(1f, 0.82f, 0.88f), R() * 0.6f), 1f);
                    }
                    break;
                }
                case Kind.Moss:
                {
                    // Low mossy mounds in small families, not flat discs.
                    var mossC = Color.Lerp(new Color(0.18f, 0.40f, 0.22f), new Color(0.40f, 0.58f, 0.26f), R()) * tone;
                    for (int i = 0; i < 3 + rng.Next(3); i++)
                    {
                        float r = 0.05f + 0.07f * R();
                        b.Ball(p + Lean(0.12f) + Vector3.up * 0.015f, new Vector3(r, r * 0.75f, r), mossC * (0.9f + R() * 0.2f), 0f, 6, 3);
                    }
                    break;
                }
                case Kind.Fern:
                {
                    var baseC = new Color(0.22f, 0.46f, 0.28f) * tone;
                    var tipC = new Color(0.46f, 0.74f, 0.42f) * tone;
                    int n = 4 + rng.Next(3);
                    for (int i = 0; i < n; i++)
                    {
                        float a = yaw + i * (Mathf.PI * 2f / n);
                        var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        b.Blade(p, outward * 0.9f, 0.20f + R() * 0.10f, 0.07f, baseC, tipC, a + Mathf.PI / 2f);
                    }
                    break;
                }
                case Kind.Reed:
                {
                    var baseC = new Color(0.30f, 0.44f, 0.24f) * tone;
                    var tipC = new Color(0.58f, 0.68f, 0.40f) * tone;
                    int n = 4 + rng.Next(4);
                    for (int i = 0; i < n; i++)
                    {
                        float h = 0.50f + R() * 0.35f;
                        var lean = Lean(0.10f);
                        b.Blade(p + Lean(0.05f), lean, h, 0.018f, baseC, tipC, yaw + i * 0.9f);
                        if (i < 2 && R() < 0.7f)
                            b.Ball(p + Vector3.up * (h * 0.82f) + lean * (h * 0.6f), new Vector3(0.026f, 0.075f, 0.026f), new Color(0.42f, 0.28f, 0.18f), 0.9f, 6, 3);
                    }
                    break;
                }
                case Kind.SeaPink:
                {
                    b.Ball(p, new Vector3(0.10f, 0.05f, 0.10f), new Color(0.32f, 0.46f, 0.30f) * tone, 0.1f, 6, 3);
                    var pink = Color.Lerp(new Color(1f, 0.62f, 0.78f), new Color(1f, 0.80f, 0.88f), R());
                    for (int i = 0; i < 4 + rng.Next(3); i++)
                    {
                        float h = 0.12f + R() * 0.07f;
                        var lean = Lean(0.18f);
                        b.Blade(p, lean, h, 0.007f, new Color(0.40f, 0.52f, 0.32f), new Color(0.50f, 0.60f, 0.38f), yaw + i);
                        b.Ball(p + Vector3.up * h + lean * h, Vector3.one * 0.03f, pink, 1f);
                    }
                    break;
                }
                case Kind.Thyme:
                {
                    // A low green cushion of tiny leaves, sprinkled with flowers.
                    var leaf = new Color(0.28f, 0.46f, 0.28f) * tone;
                    for (int i = 0; i < 5; i++)
                    {
                        float r = 0.045f + 0.04f * R();
                        b.Ball(p + Lean(0.09f) + Vector3.up * 0.02f, new Vector3(r, r * 0.65f, r), leaf * (0.9f + 0.2f * R()), 0.2f, 6, 3);
                    }
                    var purple = Color.Lerp(new Color(0.72f, 0.56f, 0.92f), new Color(0.86f, 0.66f, 0.92f), R());
                    for (int i = 0; i < 5 + rng.Next(5); i++)
                        b.Ball(p + Lean(0.11f) + Vector3.up * 0.055f, Vector3.one * 0.013f, purple, 0.3f, 4, 2);
                    break;
                }
                case Kind.Bluebell:
                {
                    var stemC = new Color(0.30f, 0.50f, 0.32f);
                    for (int s = 0; s < 2 + rng.Next(3); s++)
                    {
                        var lean = Lean(0.35f);
                        float h = 0.18f + R() * 0.08f;
                        var root = p + Lean(0.05f);
                        b.Blade(root, lean, h, 0.008f, stemC, stemC, yaw + s);
                        var blue = Color.Lerp(new Color(0.42f, 0.50f, 0.96f), new Color(0.62f, 0.56f, 0.98f), R());
                        for (int k = 0; k < 3; k++)
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
                    var hues = new[] { new Color(1f, 0.98f, 0.92f), new Color(1f, 0.88f, 0.40f), new Color(1f, 0.70f, 0.82f), new Color(0.80f, 0.72f, 1f), new Color(0.62f, 0.82f, 1f) };
                    var flower = hues[rng.Next(hues.Length)];
                    var stemC = new Color(0.34f, 0.54f, 0.30f);
                    for (int i = 0; i < 1 + rng.Next(3); i++)
                    {
                        float h = 0.13f + R() * 0.12f;
                        var lean = Lean(0.2f);
                        var root = p + Lean(0.05f);
                        b.Blade(root, lean, h, 0.008f, stemC, stemC, yaw + i);
                        var head = root + Vector3.up * h + lean * h;
                        for (int k = 0; k < 5; k++)
                        {
                            float a = k * 1.2566f + yaw;
                            b.Ball(head + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.022f, new Vector3(0.018f, 0.006f, 0.018f), flower, 1f, 4, 2);
                        }
                        b.Ball(head + Vector3.up * 0.004f, Vector3.one * 0.011f, new Color(1f, 0.82f, 0.30f), 1f, 4, 2);
                    }
                    break;
                }
                case Kind.Lichen:
                {
                    var c = Color.Lerp(new Color(0.76f, 0.80f, 0.52f), new Color(0.66f, 0.76f, 0.62f), R());
                    for (int i = 0; i < 2 + rng.Next(3); i++)
                    {
                        float r = 0.05f + R() * 0.08f;
                        b.Ball(p + Lean(0.12f), new Vector3(r, 0.012f, r), c * (0.9f + R() * 0.2f), 0f, 6, 2);
                    }
                    break;
                }
                case Kind.Toadstool:
                {
                    var cap = R() < 0.5f ? new Color(0.92f, 0.62f, 0.46f) : new Color(0.86f, 0.90f, 0.98f);
                    for (int i = 0; i < 1 + rng.Next(3); i++)
                    {
                        var at = p + Lean(0.06f);
                        float h = 0.04f + R() * 0.04f;
                        b.Ball(at + Vector3.up * (h * 0.5f), new Vector3(0.012f, h * 0.55f, 0.012f), new Color(0.96f, 0.94f, 0.88f), 0.1f, 5, 2);
                        b.Ball(at + Vector3.up * h, new Vector3(0.032f, 0.018f, 0.032f), cap, 0.15f, 6, 3);
                    }
                    break;
                }
            }
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
            Shader.SetGlobalColor("_DGAmbient", RenderSettings.ambientLight * 0.85f);
        }
    }
}
