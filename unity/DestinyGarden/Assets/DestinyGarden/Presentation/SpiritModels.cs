using UnityEngine;
using Blade = DestinyGarden.Presentation.FoliageMesh.BladeShape;

namespace DestinyGarden.Presentation
{
    /// <summary>The two winged figures use shared meshes with separate pivots and opacity per appearance.</summary>
    internal sealed class SpiritModels
    {
        internal sealed class Figure
        {
            public LifeModels.Winged Wings;
            public Renderer Body, Aura;
            public Transform Root => Wings.Root;
        }

        private readonly LifeModels _models;
        private readonly Material _paint, _aura;
        private readonly Mesh _fairyBody, _fairyFore, _fairyHind, _angelBody, _angelWing;
        private static readonly Color Pearl = new Color(0.99f, 0.95f, 0.83f);
        private static readonly Color WarmGold = new Color(1f, 0.78f, 0.43f);
        private static readonly Color Ice = new Color(0.63f, 0.82f, 0.94f);

        public SpiritModels(LifeModels models)
        {
            _models = models;
            _paint = models.Paint(null, 3322);
            _aura = models.Keep(IslandHarmony.GlowMaterial(models.Keep(ProceduralArt.GlowSprite()), Color.white, 3314));
            _fairyBody = models.Bake(Body(true), "Fairy, petal dress and seed");
            _fairyFore = models.Bake(FairyWing(false), "Fairy upper wing");
            _fairyHind = models.Bake(FairyWing(true), "Fairy lower wing");
            _angelBody = models.Bake(Body(false), "Angel, robe and halo");
            _angelWing = models.Bake(AngelWing(), "Layered angel feathers");
        }

        public Figure Make(Transform parent, bool fairy)
        {
            var root = LifeModels.Pivot(parent, fairy ? "Prayer fairy" : "Rainbow angel");
            var glow = _models.GlowQuad(root, "Soft backlight", _aura, new Vector3(0f, 0.36f, -0.11f), fairy ? 1.14f : 1.90f);
            var rig = fairy
                ? _models.Wings(root, _fairyFore, _paint, new Vector3(0.071f, 0.47f, -0.055f), _fairyHind, _paint,
                    new Vector3(0.067f, 0.416f, -0.073f))
                : _models.Wings(root, _angelWing, _paint, new Vector3(0.11f, 0.58f, -0.078f));
            var body = _models.Draw(LifeModels.Pivot(root, "Figure"), fairy ? _fairyBody : _angelBody, _paint);
            rig.Flap(fairy ? 13f : 7f, -10f, 8f);
            return new Figure { Wings = rig, Body = body, Aura = glow };
        }

        private static FoliageMesh Body(bool fairy)
        {
            var mesh = LifeModels.MeshData();
            var hair = fairy ? new Color(0.53f, 0.28f, 0.40f) : new Color(0.70f, 0.54f, 0.35f);
            var bodice = fairy ? new Color(0.38f, 0.61f, 0.58f) : new Color(0.83f, 0.79f, 0.70f);
            var rose = new Color(0.79f, 0.51f, 0.73f);
            var skin = new Color(0.99f, 0.85f, 0.70f);
            float bodyHeight = fairy ? 0.45f : 0.55f;
            float headHeight = fairy ? 0.62f : 0.765f;
            float headRadius = fairy ? 0.079f : 0.098f;
            mesh.Ball(new Vector3(0f, bodyHeight, 0.016f), new Vector3(0.105f, 0.115f, 0.071f), bodice, 1f, 15, 9);
            mesh.Ball(new Vector3(0f, headHeight - 0.086f, 0.015f), new Vector3(0.036f, 0.046f, 0.037f), skin, 1f, 10, 6);
            mesh.Ball(new Vector3(0f, headHeight, 0.026f), new Vector3(headRadius, headRadius * 1.08f, headRadius * 0.86f), skin, 1f, 15, 8);
            mesh.Ball(new Vector3(0f, headHeight + headRadius * 0.42f, 0.003f), new Vector3(headRadius * 1.10f, headRadius * 0.82f, headRadius), hair, 1f, 14, 8);
            if (fairy)
            {
                mesh.Ball(new Vector3(-0.057f, headHeight + 0.066f, -0.044f), new Vector3(0.059f, 0.049f, 0.054f), hair, 1f, 11, 7);
                for (int i = 0; i < 4; i++)
                    mesh.CurvedBlade(new Vector3(-0.064f, headHeight + 0.077f, 0.037f),
                        new Vector3(Mathf.Cos(i * 1.57f), Mathf.Sin(i * 1.57f), 0.16f), Vector3.forward,
                        0.047f, 0.019f, 0.14f, 0.23f, Blade.Rounded, WarmGold, Pearl, 1f);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float eyeHeight = headHeight + 0.002f;
                mesh.Ball(new Vector3(side * headRadius * 0.38f, eyeHeight, 0.026f + headRadius * 0.81f),
                    new Vector3(0.007f, 0.008f, 0.004f), hair * 0.79f, 1f, 7, 5);
                var shoulder = new Vector3(side * 0.081f, bodyHeight + 0.047f, 0.030f);
                var elbow = fairy ? new Vector3(side * 0.136f, 0.398f, 0.107f) : new Vector3(side * 0.205f, 0.474f, 0.092f);
                var hand = fairy ? new Vector3(side * 0.062f, 0.430f, 0.151f) : new Vector3(side * 0.126f, 0.443f, 0.175f);
                mesh.Tube(new[] { shoulder, elbow, hand }, 0.034f, 0.018f, fairy ? bodice : Pearl, fairy ? skin : Pearl * 0.90f, 1f, 1f, 9);
                mesh.Ball(hand, new Vector3(0.023f, 0.025f, 0.018f), skin, 1f, 9, 6);
            }

            if (fairy)
            {
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f + 0.22f;
                    var outwards = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    mesh.CurvedBlade(Vector3.up * 0.38f + outwards * 0.042f, outwards * 0.51f + Vector3.down,
                        outwards, 0.26f + (i % 2) * 0.032f, 0.079f, 0.29f, 0.32f, Blade.Rounded,
                        rose * 0.70f, Color.Lerp(rose, Pearl, 0.45f + i * 0.06f), 0.98f);
                }
                mesh.Tube(new[] { new Vector3(-0.037f, 0.245f, 0f), new Vector3(-0.047f, 0.121f, 0.025f), new Vector3(-0.029f, 0.027f, 0.06f) },
                    0.031f, 0.016f, skin, rose * 0.65f, 1f, 1f, 8);
                mesh.Tube(new[] { new Vector3(0.040f, 0.256f, 0f), new Vector3(0.059f, 0.158f, -0.018f), new Vector3(0.064f, 0.080f, -0.080f) },
                    0.030f, 0.016f, skin, rose * 0.65f, 1f, 1f, 8);
                mesh.Ball(new Vector3(0f, 0.441f, 0.164f), new Vector3(0.037f, 0.050f, 0.034f), WarmGold * 1.09f, 1f, 12, 7);
                mesh.CurvedBlade(new Vector3(0.003f, 0.477f, 0.161f), new Vector3(0.50f, 0.67f, 0.29f), Vector3.forward,
                    0.063f, 0.018f, 0.10f, 0.22f, Blade.Lance, new Color(0.51f, 0.71f, 0.40f), new Color(0.82f, 0.95f, 0.53f), 1f);
            }
            else
            {
                // Alternating fold widths carry the skirt into a loose, scalloped hem.
                const int around = 24;
                int start = mesh.V.Count;
                for (int row = 0; row < 6; row++)
                {
                    float t = row / 5f;
                    for (int i = 0; i < around; i++)
                    {
                        float a = i * Mathf.PI * 2f / around;
                        var outwards = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        float fold = Mathf.Cos(a * 6f - t * 1.9f);
                        float width = 0.078f + 0.15f * t * t + 0.019f * t * fold;
                        var at = outwards * width + new Vector3(-0.035f * t * t, 0.52f - 0.51f * t + 0.026f * t * fold, -0.065f * t * t);
                        var tone = Color.Lerp(Ice * 0.82f, Pearl, Mathf.Clamp01(0.55f + 0.30f * fold + outwards.z * 0.22f));
                        if (row == 5) tone = Color.Lerp(tone, WarmGold, 0.35f);
                        tone.a = row == 5 ? 0.70f : 0.97f;
                        mesh.Add(at, outwards, tone);
                    }
                }
                for (int row = 0; row < 5; row++)
                    for (int i = 0; i < around; i++)
                    {
                        int a = start + row * around + i, b = start + row * around + (i + 1) % around;
                        mesh.Triangle(a, b, a + around);
                        mesh.Triangle(b, b + around, a + around);
                    }
                mesh.Tube(new[] { new Vector3(-0.079f, 0.515f, 0.061f), new Vector3(0f, 0.481f, 0.085f), new Vector3(0.079f, 0.511f, 0.060f) },
                    0.012f, 0.012f, WarmGold, Pearl, 1f, 1f, 8);
                var ring = new Vector3[33];
                for (int i = 0; i < ring.Length; i++)
                {
                    float angle = i / (float)(ring.Length - 1) * Mathf.PI * 2f;
                    ring[i] = new Vector3(Mathf.Cos(angle) * 0.155f, 0.946f + Mathf.Sin(angle) * 0.052f, Mathf.Sin(angle) * 0.092f);
                }
                mesh.Tube(ring, 0.012f, 0.012f, WarmGold * 1.16f, Pearl, 1f, 1f, 8);
            }
            return mesh;
        }

        private static FoliageMesh FairyWing(bool hind)
        {
            var mesh = LifeModels.MeshData();
            var start = Vector3.zero;
            var dir = new Vector3(0.74f, hind ? -0.68f : 0.68f, hind ? -0.04f : 0.06f).normalized;
            float length = hind ? 0.31f : 0.49f;
            var cyan = new Color(0.51f, 0.87f, 0.89f);
            var violet = new Color(0.80f, 0.68f, 0.93f);
            var face = Vector3.forward;
            mesh.CurvedBlade(start, dir, face, length, hind ? 0.092f : 0.113f, 0.12f, 0.35f, Blade.Lance,
                hind ? violet : cyan, hind ? cyan : new Color(0.87f, 1f, 0.94f), hind ? 0.66f : 0.77f);
            var mid = dir * (length * 0.54f) + face * (length * 0.10f);
            var end = dir * (length * 0.90f) + face * (length * 0.13f);
            mesh.Tube(new[] { start, mid, end }, 0.008f, 0.003f, cyan * 0.92f, Pearl, 0.85f, 0.92f, 6);
            var side = Vector3.Cross(face, dir);
            for (int i = 0; i < 3; i++)
            {
                float t = 0.26f + 0.19f * i;
                var stem = dir * (length * t) + face * 0.043f;
                var tip = dir * (length * (t + 0.15f)) + side * ((i % 2 == 0 ? -1 : 1) * 0.057f) + face * 0.05f;
                mesh.Tube(new[] { stem, tip }, 0.005f, 0.0024f, violet, Pearl, 0.79f, 0.85f, 5);
            }
            return mesh;
        }

        private static FoliageMesh AngelWing()
        {
            var mesh = LifeModels.MeshData();
            var silver = new Color(0.68f, 0.79f, 0.89f);
            var tip = new Color(0.97f, 0.91f, 0.77f);
            mesh.CurvedBlade(Vector3.zero, new Vector3(0.96f, 0.31f, -0.08f), Vector3.forward,
                0.80f, 0.170f, -0.17f, -0.24f, Blade.Lance, silver * 0.82f, Pearl, 0.90f);
            for (int i = 0; i < 9; i++)
            {
                float t = i / 8f;
                var from = new Vector3(0.09f + 0.61f * t, 0.029f + 0.25f * t * t, -0.022f - 0.10f * t);
                var direction = new Vector3(0.55f + t * 0.21f, -0.91f + t * 0.81f, -0.18f);
                float length = 0.23f + 0.32f * Mathf.Sin(t * 2.3f);
                mesh.CurvedBlade(from, direction, Vector3.forward, length, 0.051f + 0.015f * t, 0.18f, 0.23f,
                    Blade.Ray, silver * (0.81f + 0.11f * (i % 2)), Color.Lerp(Pearl, tip, t), 0.97f);
            }
            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                mesh.CurvedBlade(new Vector3(0.055f + t * 0.56f, 0.039f + 0.27f * t * t, 0.017f - 0.05f * t),
                    new Vector3(0.82f, -0.49f + 0.46f * t, 0.05f), Vector3.forward, 0.18f + 0.12f * t,
                    0.061f, 0.11f, 0.23f, Blade.Rounded, silver, Pearl, 1f);
            }
            return mesh;
        }
    }
}
