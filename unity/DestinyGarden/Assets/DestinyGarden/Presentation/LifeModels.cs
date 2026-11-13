using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Blade = DestinyGarden.Presentation.FoliageMesh.BladeShape;

namespace DestinyGarden.Presentation
{
    /// <summary>Local-space models and shared rendering assets for the garden's residents.</summary>
    internal sealed class LifeModels : IDisposable
    {
        internal enum Insect { Bee, Dragonfly, Firefly, Butterfly, Hoverfly }

        internal sealed class Winged
        {
            public Transform Root;
            public Renderer Left, Right, LeftHind, RightHind;

            public void Flap(float front, float hind, float sweep = 0f)
            {
                Left.transform.localRotation = Quaternion.Euler(0f, -sweep, -front);
                Right.transform.localRotation = Quaternion.Euler(0f, sweep, front);
                if (LeftHind == null) return;
                LeftHind.transform.localRotation = Quaternion.Euler(0f, -sweep, -hind);
                RightHind.transform.localRotation = Quaternion.Euler(0f, sweep, hind);
            }
        }

        internal sealed class Bird
        {
            public Winged Wings;
            public Transform Head, Tail;
            public Transform Root => Wings.Root;
        }

        internal sealed class Fish
        {
            public Transform Root, Tail;
            public Renderer BodyPaint, TailPaint;
        }

        private readonly List<Object> _assets = new List<Object>();
        private readonly Dictionary<Insect, (Mesh Body, Mesh Fore, Mesh Hind)> _insects =
            new Dictionary<Insect, (Mesh, Mesh, Mesh)>();
        private readonly Dictionary<Insect, (Material Fore, Material Hind)> _membranes =
            new Dictionary<Insect, (Material, Material)>();
        private readonly Material _solid;
        private Mesh _worm;
        private (Mesh Body, Mesh Tail) _fish;
        private Material _fishPaint, _lantern, _lanternAura;
        private Mesh _lanternMesh, _quad;

        private static readonly Color Ink = new Color(0.10f, 0.095f, 0.13f);
        private static readonly Color Honey = new Color(0.96f, 0.65f, 0.15f);
        private static readonly Color Jade = new Color(0.15f, 0.64f, 0.66f);
        private static readonly Color FishBlue = new Color(0.14f, 0.36f, 0.39f);
        private static readonly Color FishSilver = new Color(0.39f, 0.69f, 0.68f);

        public LifeModels()
        {
            _solid = Keep(new Material(Shader.Find("DestinyGarden/Foliage")));
            _solid.SetFloat("_WindStrength", 0f);
            _solid.SetFloat("_Translucency", 0.18f);
        }

        public T Keep<T>(T asset) where T : Object
        {
            _assets.Add(asset);
            return asset;
        }

        public void Dispose()
        {
            foreach (var asset in _assets)
                if (UnityEngine.Application.isPlaying) Object.Destroy(asset);
                else Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        public static Transform Pivot(Transform parent, string name, Vector3 at = default)
        {
            var part = new GameObject(name).transform;
            part.SetParent(parent, false);
            part.localPosition = at;
            return part;
        }

        public static FoliageMesh MeshData() => new FoliageMesh { Root = new Vector4(0f, 0f, 0f, -1f) };

        public Mesh Bake(FoliageMesh mesh, string name) => Keep(mesh.ToMesh(name));

        public Material Paint(Texture2D texture = null, int queue = 3140)
        {
            var material = Keep(new Material(Shader.Find("Sprites/Default")));
            if (texture != null) material.SetTexture("_MainTex", texture);
            material.renderQueue = queue;
            return material;
        }

        public Renderer Draw(Transform part, Mesh mesh, Material paint = null, bool castsShadow = false)
        {
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = paint != null ? paint : _solid;
            renderer.shadowCastingMode = castsShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = castsShadow;
            return renderer;
        }

        public Winged Wings(Transform root, Mesh fore, Material paint, Vector3 hinge, Mesh hind = null,
            Material hindPaint = null, Vector3 hindHinge = default)
        {
            Renderer Wing(string name, int side, Vector3 at, Mesh mesh, Material material)
            {
                var pivot = Pivot(root, name, new Vector3(at.x * side, at.y, at.z));
                pivot.localScale = new Vector3(side, 1f, 1f);
                return Draw(pivot, mesh, material);
            }
            var wings = new Winged
            {
                Root = root,
                Left = Wing("Left wing", -1, hinge, fore, paint),
                Right = Wing("Right wing", 1, hinge, fore, paint)
            };
            if (hind != null)
            {
                wings.LeftHind = Wing("Left hindwing", -1, hindHinge, hind, hindPaint);
                wings.RightHind = Wing("Right hindwing", 1, hindHinge, hind, hindPaint);
            }
            return wings;
        }

        public Winged MakeInsect(Transform parent, Insect kind, int variant, Color inside = default, Color outside = default)
        {
            if (!_insects.TryGetValue(kind, out var model))
            {
                var body = InsectBody(kind);
                model = kind switch
                {
                    Insect.Bee => (body, WingFoil(0.224f, 0.094f, -0.085f, 0.024f), WingFoil(0.147f, 0.080f, -0.104f, 0.018f)),
                    Insect.Dragonfly => (body, WingFoil(0.49f, 0.105f, 0.016f, 0.030f), WingFoil(0.45f, 0.129f, -0.107f, 0.025f)),
                    Insect.Firefly => (body, WingFoil(0.118f, 0.061f, -0.057f, 0.015f), null),
                    Insect.Hoverfly => (body, WingFoil(0.23f, 0.082f, -0.041f, 0.024f), null),
                    _ => (body, WingFoil(0.40f, 0.33f, 0.045f, 0.045f), WingFoil(0.31f, 0.30f, -0.085f, 0.035f))
                };
                _insects.Add(kind, model);
            }
            (Material Fore, Material Hind) membranes;
            if (kind == Insect.Butterfly)
            {
                membranes = (Paint(Keep(ProceduralArt.ButterflyWing(inside, outside, false, variant))),
                    Paint(Keep(ProceduralArt.ButterflyWing(inside, outside, true, variant)), 3139));
            }
            else if (!_membranes.TryGetValue(kind, out membranes))
            {
                Color tint = kind == Insect.Bee ? new Color(0.98f, 0.87f, 0.66f) : new Color(0.62f, 0.90f, 0.93f);
                bool longWing = kind == Insect.Dragonfly;
                membranes = (Paint(Keep(ProceduralArt.InsectWing(tint, longWing, false))), model.Hind != null
                    ? Paint(Keep(ProceduralArt.InsectWing(tint, longWing, true)), 3139) : null);
                _membranes.Add(kind, membranes);
            }

            var root = Pivot(parent, kind + " " + (variant + 1));
            Draw(Pivot(root, "Body"), model.Body);
            float width = kind == Insect.Dragonfly ? 0.048f : 0.038f;
            var rig = Wings(root, model.Fore, membranes.Fore, new Vector3(width, 0.038f, 0.038f), model.Hind, membranes.Hind,
                new Vector3(width, 0.026f, -0.075f));
            root.gameObject.SetActive(false);
            return rig;
        }

        private Mesh InsectBody(Insect kind)
        {
            var body = MeshData();
            bool bee = kind == Insect.Bee, dragonfly = kind == Insect.Dragonfly, butterfly = kind == Insect.Butterfly;
            bool hoverfly = kind == Insect.Hoverfly;
            float width = bee ? 0.069f : dragonfly || hoverfly ? 0.059f : butterfly ? 0.028f : 0.041f;
            Color shell = bee ? Honey : dragonfly ? Jade : hoverfly ? new Color(0.62f, 0.43f, 0.19f) : Ink * 1.45f;
            Color dark = dragonfly ? new Color(0.065f, 0.26f, 0.29f) : Ink;
            if (bee)
            {
                Spindle(body, (-0.235f, 0.002f, 0.002f, dark), (-0.215f, 0.042f, 0.038f, dark),
                    (-0.188f, 0.066f, 0.053f, Honey), (-0.159f, 0.076f, 0.062f, Honey),
                    (-0.149f, 0.079f, 0.063f, dark), (-0.114f, 0.078f, 0.064f, dark),
                    (-0.103f, 0.076f, 0.062f, Honey), (-0.065f, 0.059f, 0.052f, Honey),
                    (-0.047f, 0.047f, 0.042f, dark));
            }
            else if (hoverfly)
                Spindle(body, (-0.20f, 0.006f, 0.008f, dark), (-0.17f, 0.041f, 0.036f, shell),
                    (-0.13f, 0.049f, 0.045f, dark), (-0.095f, 0.056f, 0.043f, Honey),
                    (-0.072f, 0.053f, 0.043f, dark), (-0.040f, 0.048f, 0.038f, shell));
            else if (dragonfly)
            {
                var rings = new (float, float, float, Color)[19];
                for (int i = 0; i < rings.Length; i++)
                {
                    float t = i / (float)(rings.Length - 1);
                    float radius = Mathf.Lerp(0.010f, 0.039f, t);
                    rings[i] = (Mathf.Lerp(-0.52f, -0.027f, t), radius, radius * 0.78f,
                        i % 3 == 0 ? dark : Color.Lerp(Jade, new Color(0.36f, 0.79f, 0.86f), t));
                }
                Spindle(body, rings);
            }
            else
            {
                body.Ball(new Vector3(0f, -0.008f, -0.077f), new Vector3(width * 0.85f, width * 0.65f, butterfly ? 0.109f : 0.079f), shell, 0f, 10, 6);
                if (!butterfly)
                    body.Ball(new Vector3(0f, 0.011f, 0.068f), new Vector3(0.043f, 0.031f, 0.036f), new Color(0.55f, 0.24f, 0.10f), 0f, 10, 5);
            }
            body.Ball(new Vector3(0f, 0.008f, 0.025f), new Vector3(width, width * 0.90f, width * 1.15f), shell, 0f, 12, 7);
            body.Ball(new Vector3(0f, 0.019f, 0.11f), new Vector3(width * 0.79f, width * 0.72f, width * 0.73f), dark, 0f, 10, 6);
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new Vector3(side * width * 0.66f, 0.035f, 0.116f);
                float radius = width * (dragonfly || hoverfly ? 0.62f : 0.31f);
                Color eyeColor = dragonfly ? new Color(0.29f, 0.72f, 0.79f) : hoverfly ? new Color(0.29f, 0.10f, 0.064f) : Ink * 0.72f;
                body.Ball(eye, new Vector3(radius, radius, radius * 0.86f), eyeColor, 0f, 9, 5);
                body.Ball(eye + new Vector3(side * radius * 0.36f, radius * 0.55f, radius * 0.48f), Vector3.one * (radius * 0.29f),
                    new Color(0.77f, 0.92f, 0.92f), 0f, 6, 3);
                float antenna = butterfly ? 0.133f : bee ? 0.065f : 0.038f;
                var tip = new Vector3(side * (width * 0.55f + antenna * 0.27f), 0.042f + antenna * 0.42f, 0.128f + antenna);
                body.Tube(new[] { new Vector3(side * width * 0.28f, 0.040f, 0.126f), tip - new Vector3(side * 0.009f, 0.004f, antenna * 0.4f), tip },
                    butterfly ? 0.005f : 0.004f, 0.0022f, dark, dark * 1.55f, 0f, 0f, 5);
                if (butterfly) body.Ball(tip, Vector3.one * 0.008f, dark * 1.35f, 0f, 6, 4);
                for (int i = 0; i < 3; i++)
                {
                    var joint = new Vector3(side * width * 0.66f, -0.018f, 0.067f - i * 0.044f);
                    var knee = joint + new Vector3(side * 0.036f, -0.025f, 0.024f - i * 0.024f);
                    var foot = knee + new Vector3(side * 0.009f, -0.022f, -0.024f);
                    body.Tube(new[] { joint, knee, foot }, bee ? 0.008f : 0.0045f, 0.0026f, dark, dark, 0f, 0f, 5);
                    if (bee && i == 2)
                        body.Ball(knee, new Vector3(0.017f, 0.015f, 0.018f), new Color(1f, 0.52f, 0.12f), 0f, 8, 5);
                }
            }
            return Bake(body, kind + " body");
        }

        private static void Spindle(FoliageMesh mesh, params (float Z, float Width, float Height, Color Tone)[] rings)
        {
            const int sides = 12;
            int start = mesh.V.Count;
            foreach (var ring in rings)
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / sides;
                    float x = Mathf.Cos(angle), y = Mathf.Sin(angle);
                    var c = ring.Tone;
                    float light = 0.83f + 0.17f * y;
                    mesh.Add(new Vector3(x * ring.Width, y * ring.Height, ring.Z), new Vector3(x, y, 0f),
                        new Color(c.r * light, c.g * light, c.b * light, c.a));
                }
            for (int r = 0; r < rings.Length - 1; r++)
                for (int i = 0; i < sides; i++)
                {
                    int a = start + r * sides + i, b = start + r * sides + (i + 1) % sides;
                    mesh.Triangle(a, b, a + sides);
                    mesh.Triangle(b, b + sides, a + sides);
                }
        }

        public Mesh WingFoil(float span, float chord, float sweep, float arch)
        {
            var mesh = MeshData();
            const int sections = 5;
            var uv = new Vector2[(sections + 1) * 3];
            for (int i = 0; i <= sections; i++)
                for (int j = 0; j < 3; j++)
                {
                    float u = i / (float)sections, v = j * 0.5f;
                    var position = new Vector3(span * u, Mathf.Sin(u * Mathf.PI) * arch * (1f - 0.44f * v),
                        sweep * u + chord * (v - 0.5f));
                    int at = mesh.Add(position, Vector3.up, Color.white);
                    uv[at] = new Vector2(u, v);
                    if (i == 0 || j == 0) continue;
                    mesh.Triangle(at - 4, at - 1, at - 3);
                    mesh.Triangle(at - 3, at - 1, at);
                }
            var wing = Bake(mesh, "Arched wing");
            wing.uv = uv;
            wing.RecalculateNormals();
            return wing;
        }

        public (Renderer Lamp, Renderer Aura) AddLantern(Transform root)
        {
            if (_lantern == null)
            {
                var mesh = MeshData();
                mesh.Ball(Vector3.zero, new Vector3(0.038f, 0.023f, 0.049f), Color.white, 1f, 10, 6);
                _lanternMesh = Bake(mesh, "Firefly lantern");
                _lantern = Paint(null, 3190);
                _lanternAura = Keep(IslandHarmony.GlowMaterial(Keep(ProceduralArt.GlowSprite()), Color.white, 3189));
            }
            var at = new Vector3(0f, -0.015f, -0.144f);
            var lamp = Draw(Pivot(root, "Lantern", at), _lanternMesh, _lantern);
            var aura = GlowQuad(root, "Lantern light", _lanternAura, at, 0.46f);
            return (lamp, aura);
        }

        public Renderer GlowQuad(Transform root, string name, Material paint, Vector3 at, float size)
        {
            if (_quad == null)
            {
                _quad = Keep(new Mesh { name = "Glow quad" });
                _quad.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f), new Vector3(0.5f, -0.5f) };
                _quad.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                _quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            }
            var pivot = Pivot(root, name, at);
            pivot.localScale = Vector3.one * size;
            pivot.gameObject.AddComponent<Billboard>();
            return Draw(pivot, _quad, paint);
        }

        public Bird MakeBird(Transform parent)
        {
            var root = Pivot(parent, "Small bird");
            var slate = new Color(0.25f, 0.43f, 0.57f);
            var sky = new Color(0.47f, 0.67f, 0.76f);
            var cream = new Color(0.95f, 0.81f, 0.61f);
            var russet = new Color(0.82f, 0.36f, 0.19f);
            var feet = new Color(0.41f, 0.26f, 0.21f);
            var body = MeshData();
            body.Ball(new Vector3(0f, 0.243f, -0.029f), new Vector3(0.148f, 0.153f, 0.226f), slate, 0f, 16, 9);
            body.Ball(new Vector3(0f, 0.214f, 0.081f), new Vector3(0.131f, 0.131f, 0.159f), cream, 0f, 14, 8);
            body.Ball(new Vector3(0f, 0.285f, 0.155f), new Vector3(0.107f, 0.110f, 0.107f), russet, 0f, 14, 8);
            for (int side = -1; side <= 1; side += 2)
            {
                var ankle = new Vector3(side * 0.077f, 0.018f, 0.023f);
                body.Tube(new[] { new Vector3(side * 0.077f, 0.123f, -0.011f), ankle, ankle + Vector3.forward * 0.025f },
                    0.014f, 0.009f, feet, feet * 1.2f, 0f, 0f, 6);
                for (int j = -1; j <= 1; j++)
                    body.Tube(new[] { ankle, ankle + new Vector3(j * 0.027f, -0.012f, 0.039f), ankle + new Vector3(j * 0.039f, -0.016f, 0.066f) },
                        0.009f, 0.0037f, feet, feet * 1.30f, 0f, 0f, 5);
            }
            Draw(Pivot(root, "Breast and feet"), Bake(body, "Bird body"), castsShadow: true);

            var head = Pivot(root, "Head", new Vector3(0f, 0.367f, 0.126f));
            var face = MeshData();
            face.Ball(Vector3.up * 0.019f, new Vector3(0.110f, 0.108f, 0.106f), slate, 0f, 16, 8);
            face.Ball(new Vector3(0f, 0.060f, -0.006f), new Vector3(0.105f, 0.076f, 0.096f), sky, 0f, 14, 7);
            for (int side = -1; side <= 1; side += 2)
            {
                face.Ball(new Vector3(side * 0.073f, 0.001f, 0.050f), new Vector3(0.034f, 0.051f, 0.057f), cream, 0f, 10, 6);
                var at = new Vector3(side * 0.091f, 0.039f, 0.056f);
                face.Ball(at, Vector3.one * 0.024f, Ink, 0f, 10, 6);
                face.Ball(at + new Vector3(side * 0.013f, 0.011f, 0.011f), Vector3.one * 0.0075f, new Color(0.98f, 0.96f, 0.81f), 0f, 7, 4);
            }
            Spindle(face, (0.089f, 0.043f, 0.025f, feet), (0.124f, 0.035f, 0.023f, Honey * 0.82f),
                (0.171f, 0.012f, 0.010f, Honey), (0.192f, 0.001f, 0.002f, Honey));
            Draw(head, Bake(face, "Bird head"));

            var wing = MeshData();
            wing.Ball(new Vector3(0.123f, 0f, -0.017f), new Vector3(0.146f, 0.049f, 0.101f), slate, 0f, 13, 6);
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                var at = new Vector3(0.21f - t * 0.16f, -0.004f, -0.018f - 0.04f * t);
                var direction = new Vector3(0.91f - 0.68f * t, -0.10f, -0.32f - 0.54f * t);
                wing.CurvedBlade(at, direction, Vector3.up, 0.365f - t * 0.179f, 0.041f,
                    -0.19f, -0.25f, Blade.Ray, Color.Lerp(slate, sky, t), slate * (0.73f + 0.22f * (i % 2)), 0f);
            }
            for (int i = 0; i < 5; i++)
                wing.CurvedBlade(new Vector3(0.058f + i * 0.041f, 0.033f, 0.024f - i * 0.009f), new Vector3(0.57f, 0.02f, -0.74f),
                    Vector3.up, 0.154f + i * 0.018f, 0.039f, -0.11f, -0.20f, Blade.Ray, slate, sky * 0.94f, 0f);
            var wings = Wings(root, Bake(wing, "Bird primary and covert feathers"), null, new Vector3(0.100f, 0.266f, -0.026f));
            var tail = Pivot(root, "Tail", new Vector3(0f, 0.235f, -0.194f));
            var tailMesh = MeshData();
            for (int i = -2; i <= 2; i++)
                tailMesh.CurvedBlade(new Vector3(i * 0.017f, 0f, 0f), new Vector3(i * 0.13f, 0.14f, -1f), Vector3.up,
                    0.31f - Mathf.Abs(i) * 0.026f, 0.041f, -0.06f, -0.23f, Blade.Ray, slate * 0.82f, sky * 0.89f, 0f);
            Draw(tail, Bake(tailMesh, "Bird tail"));
            wings.Flap(-62f, 0f, 76f);
            root.gameObject.SetActive(false);
            return new Bird { Wings = wings, Head = head, Tail = tail };
        }

        public Transform MakeWorm(Transform parent, int index)
        {
            if (_worm == null)
            {
                var mesh = MeshData();
                var path = new Vector3[25];
                var clay = new Color(0.49f, 0.27f, 0.24f);
                for (int i = 0; i < path.Length; i++)
                {
                    float t = i / (float)(path.Length - 1);
                    path[i] = new Vector3((t - 0.5f) * 0.38f, 0.021f + Mathf.Sin(t * Mathf.PI) * 0.023f, Mathf.Sin(t * 4.9f) * 0.072f);
                }
                mesh.Tube(path, 0.019f, 0.028f, clay, clay * 1.23f, 0f, 0f, 8);
                for (int i = 1; i < path.Length - 1; i++)
                {
                    var pigment = i >= 15 && i <= 18 ? new Color(0.72f, 0.42f, 0.30f) : clay * (i % 2 == 0 ? 1.19f : 1.08f);
                    float radius = Mathf.Lerp(0.019f, 0.028f, i / 24f);
                    mesh.Ball(path[i], new Vector3(0.011f, radius, radius * 1.11f), pigment, 0f, 8, 4);
                }
                mesh.Ball(path[0], new Vector3(0.011f, 0.017f, 0.017f), clay, 0f, 8, 4);
                mesh.Ball(path[24], new Vector3(0.016f, 0.026f, 0.027f), clay * 1.29f, 0f, 8, 4);
                _worm = Bake(mesh, "Earthworm segments");
            }
            var root = Pivot(parent, "Earthworm " + (index + 1));
            Draw(root, _worm);
            root.gameObject.SetActive(false);
            return root;
        }

        public Fish MakeFish(Transform parent, int index)
        {
            if (_fish.Body == null)
            {
                var b = MeshData();
                b.Ball(new Vector3(0f, 0f, 0.051f), new Vector3(0.117f, 0.080f, 0.371f), FishBlue, 0.96f, 16, 8);
                b.Ball(new Vector3(0f, 0.059f, 0.09f), new Vector3(0.075f, 0.032f, 0.255f), FishSilver, 0.80f, 12, 7);
                b.CurvedBlade(new Vector3(0f, 0.072f, 0.145f), new Vector3(0f, 0.70f, -0.9f), Vector3.right,
                    0.25f, 0.059f, 0.03f, -0.13f, Blade.Lance, FishBlue, FishSilver, 0.86f);
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Ball(new Vector3(side * 0.054f, 0.052f, 0.307f), Vector3.one * 0.016f, Ink, 0.95f, 7, 5);
                    b.CurvedBlade(new Vector3(side * 0.072f, 0.018f, 0.16f), new Vector3(side * 0.6f, 0.02f, -1f), Vector3.up,
                        0.218f, 0.046f, 0.06f, -0.19f, Blade.Lance, FishBlue, FishSilver, 0.80f);
                }
                var tail = MeshData();
                tail.Ball(new Vector3(0f, 0f, -0.026f), new Vector3(0.047f, 0.045f, 0.105f), FishBlue, 0.96f, 9, 5);
                for (int side = -1; side <= 1; side += 2)
                    tail.CurvedBlade(new Vector3(side * 0.015f, 0.008f, -0.053f), new Vector3(side * 0.64f, 0f, -1f), Vector3.up,
                        0.265f, 0.074f, 0.06f, -0.28f, Blade.Ray, FishBlue, FishSilver * 0.89f, 0.83f);
                _fish = (Bake(b, "Fish body"), Bake(tail, "Fish tail"));
                _fishPaint = Paint(null, 2996);
            }
            var root = Pivot(parent, "Fish " + (index + 1));
            var tailPivot = Pivot(root, "Forked tail", new Vector3(0f, 0f, -0.24f));
            var bodyPaint = Draw(Pivot(root, "Body"), _fish.Body, _fishPaint);
            var tailPaint = Draw(tailPivot, _fish.Tail, _fishPaint);
            root.gameObject.SetActive(false);
            return new Fish { Root = root, Tail = tailPivot, BodyPaint = bodyPaint, TailPaint = tailPaint };
        }
    }
}
