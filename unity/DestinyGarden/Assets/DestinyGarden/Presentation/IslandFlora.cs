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
        private float _vitality = 0.30f, _target = 0.30f;
        private float _gust;
        public bool ReducedMotion;

        private enum Kind { ShortGrass, TallGrass, Clover, Moss, Fern, Reed, SeaPink, Thyme, Bluebell, Wildflower, Lichen, Toadstool }

        private static Color Hsv(float h, float s, float v) => Color.HSVToRGB(Mathf.Repeat(h, 1f), s, v);

        private static float Colony(float x, float z, int seed, float scale) =>
            0.15f + 1.6f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.68f,
                ProceduralArt.Fbm(x * scale + seed * 0.37f, z * scale + seed * 0.61f, 600 + seed, 3)));

        public void Build()
        {
            var shader = Shader.Find("DestinyGarden/Foliage");
            _mat = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            Shader.SetGlobalFloat("_DGVitality", _vitality);
            Shader.SetGlobalFloat("_DGWindGust", 0f);
            Shader.SetGlobalColor("_DGAmbient", RenderSettings.ambientLight * 0.85f);

            var chunks = new FoliageMesh[4];
            for (int i = 0; i < chunks.Length; i++) chunks[i] = new FoliageMesh();
            var rng = new System.Random(7123);
            const float step = 0.24f;
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
                    if (rng.NextDouble() > 0.80) continue; // bare earth here and there

                    float shore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8.6f, 10.0f, d));
                    float shade = IslandTerrain.ShadeAt(x, z);
                    float bank = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.55f, 1.45f, river));
                    float wet = Mathf.Max(bank, Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.6f, 1.3f, bed)) * 0.6f);
                    float hi = Mathf.InverseLerp(0.3f, 1.4f, h);
                    float dry = (1f - wet) * hi;
                    float meadow = (1f - shore) * (1f - bank * 0.7f) * (1f - shade * 0.6f);

                    weights[(int)Kind.ShortGrass] = (meadow + 0.2f) * Colony(x, z, 1, 0.45f);
                    weights[(int)Kind.TallGrass] = 0.9f * meadow * (1f - shade) * Colony(x, z, 2, 0.38f);
                    weights[(int)Kind.Clover] = 0.7f * meadow * (0.4f + wet) * Colony(x, z, 3, 0.55f);
                    weights[(int)Kind.Moss] = (1.2f * shade * (0.4f + wet) + 0.35f * bank) * Colony(x, z, 4, 0.6f);
                    weights[(int)Kind.Fern] = 1.4f * shade * Colony(x, z, 5, 0.5f);
                    weights[(int)Kind.Reed] = 2.0f * bank * Colony(x, z, 6, 0.7f);
                    weights[(int)Kind.SeaPink] = 2.0f * shore * Colony(x, z, 7, 0.6f);
                    weights[(int)Kind.Thyme] = 1.2f * dry * (1f - shade) * Colony(x, z, 8, 0.5f);
                    weights[(int)Kind.Bluebell] = 1.0f * shade * (1f - bank) * Colony(x, z, 9, 0.65f);
                    weights[(int)Kind.Wildflower] = 0.55f * meadow * Colony(x, z, 10, 0.5f);
                    weights[(int)Kind.Lichen] = 0.8f * shade * hi * Colony(x, z, 11, 0.8f);
                    weights[(int)Kind.Toadstool] = 0.32f * shade * wet * Colony(x, z, 12, 0.9f);
                    float total = 0f;
                    for (int k = 0; k < kinds; k++) total += weights[k];
                    if (total <= 0.01f) continue;
                    double pick = rng.NextDouble() * total;
                    int kind = 0;
                    for (float acc = 0f; kind < kinds; kind++) { acc += weights[kind]; if (pick < acc) break; }
                    if (kind >= kinds) kind = kinds - 1;
                    float suit = Mathf.Clamp01(weights[kind] / 1.6f);
                    float threshold = Mathf.Clamp((float)rng.NextDouble() * 0.72f + (1f - suit) * 0.32f - 0.06f, 0f, 0.97f);

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
            float tone = 0.9f + R() * 0.2f;
            switch (kind)
            {
                case Kind.ShortGrass:
                {
                    int n = 3 + rng.Next(3);
                    var baseC = new Color(0.22f, 0.42f, 0.22f) * tone;
                    var tipC = Color.Lerp(new Color(0.46f, 0.70f, 0.36f), new Color(0.58f, 0.74f, 0.40f), R()) * tone;
                    for (int i = 0; i < n; i++)
                        b.Blade(p + Lean(0.05f), Lean(0.25f), 0.12f + R() * 0.14f, 0.036f, baseC, tipC, yaw + i * 1.1f);
                    break;
                }
                case Kind.TallGrass:
                {
                    int n = 4 + rng.Next(3);
                    var baseC = new Color(0.30f, 0.46f, 0.26f) * tone;
                    var tipC = new Color(0.74f, 0.74f, 0.50f) * tone;
                    for (int i = 0; i < n; i++)
                    {
                        float h = 0.32f + R() * 0.24f;
                        var lean = Lean(0.30f);
                        b.Blade(p + Lean(0.06f), lean, h, 0.028f, baseC, tipC, yaw + i * 1.3f);
                        if (i < 2)
                        {
                            var tip = p + Vector3.up * h + lean * h;
                            b.Ball(tip, new Vector3(0.018f, 0.05f, 0.018f), new Color(0.93f, 0.86f, 0.62f), 1f, 5, 2);
                        }
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
                    var mossC = Color.Lerp(new Color(0.20f, 0.44f, 0.24f), new Color(0.40f, 0.56f, 0.24f), R()) * tone;
                    for (int i = 0; i < 2 + rng.Next(3); i++)
                    {
                        float r = 0.09f + R() * 0.12f;
                        b.Ball(p + Lean(0.10f), new Vector3(r, r * 0.42f, r * (0.8f + R() * 0.4f)), mossC * (0.9f + R() * 0.2f), 0f, 7, 3);
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
                    var mat = new Color(0.40f, 0.50f, 0.36f) * tone;
                    b.Ball(p, new Vector3(0.16f, 0.05f, 0.14f), mat, 0.15f, 7, 3);
                    var purple = Color.Lerp(new Color(0.72f, 0.56f, 0.92f), new Color(0.86f, 0.66f, 0.92f), R());
                    for (int i = 0; i < 6 + rng.Next(6); i++)
                        b.Ball(p + Lean(0.12f) + Vector3.up * 0.05f, Vector3.one * 0.016f, purple, 0.3f, 4, 2);
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
                    var c = Color.Lerp(new Color(0.66f, 0.70f, 0.46f), new Color(0.56f, 0.66f, 0.60f), R());
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

        /// <summary>A gust of wind across the whole island (for a wind blessing).</summary>
        public void Gust() => _gust = 1f;

        private void Update()
        {
            _vitality = Mathf.MoveTowards(_vitality, _target, Time.deltaTime * 0.06f);
            Shader.SetGlobalFloat("_DGVitality", _vitality);
            _gust = Mathf.MoveTowards(_gust, 0f, Time.deltaTime * 0.35f);
            Shader.SetGlobalFloat("_DGWindGust", ReducedMotion ? 0f : _gust);
            Shader.SetGlobalColor("_DGAmbient", RenderSettings.ambientLight * 0.85f);
        }
    }
}
