using System;
using System.Collections.Generic;
using UnityEngine;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// Code-generated textures and meshes (no external art assets). Everything is
    /// deterministic so every build and platform renders the same garden.
    /// </summary>
    public static class ProceduralArt
    {
        private static float Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519u);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / 4294967295f;
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            return Mathf.Lerp(
                Mathf.Lerp(Hash(xi, yi, seed), Hash(xi + 1, yi, seed), u),
                Mathf.Lerp(Hash(xi, yi + 1, seed), Hash(xi + 1, yi + 1, seed), u), v);
        }

        public static float Fbm(float x, float y, int seed, int octaves = 4)
        {
            float sum = 0, amp = 0.5f, freq = 1f, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * ValueNoise(x * freq, y * freq, seed + i * 101);
                norm += amp; amp *= 0.5f; freq *= 2.1f;
            }
            return sum / norm;
        }

        private static Texture2D MakeTex(string name, int size, Func<float, float, Color> pixel, bool repeat = true)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = name;
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    colors[y * size + x] = pixel(x / (float)size, y / (float)size);
            tex.SetPixels(colors);
            tex.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return tex;
        }

        public static Texture2D SoilTexture() => MakeTex("Soil", 256, (u, v) =>
        {
            float n = Fbm(u * 7, v * 7, 11);
            float grain = Fbm(u * 46, v * 46, 23, 2);
            var c = Color.Lerp(new Color(0.40f, 0.31f, 0.22f), new Color(0.62f, 0.52f, 0.38f), n);
            if (grain > 0.78f) c *= 1.35f;
            if (grain < 0.18f) c *= 0.7f;
            return c;
        });

        public static Texture2D StoneTexture(bool mossOnTop = false) => MakeTex(mossOnTop ? "Mossy Rock" : "Stone", 256, (u, v) =>
        {
            float n = Fbm(u * 6, v * 6, 37);
            float crack = Fbm(u * 20, v * 20, 41, 3);
            var c = Color.Lerp(new Color(0.26f, 0.28f, 0.28f), new Color(0.48f, 0.51f, 0.52f), n);
            if (crack > 0.72f && crack < 0.76f) c *= 0.55f;
            float moss = Fbm(u * 9 + 40, v * 9, 53);
            if (moss > 0.62f) c = Color.Lerp(c, new Color(0.25f, 0.42f, 0.20f), (moss - 0.62f) * 1.8f);
            if (mossOnTop)
            {
                float damp = Fbm(u * 7.5f + 5f, v * 5f, 66, 3);
                float height = v + (damp - 0.50f) * 0.68f + (crack - 0.5f) * 0.15f;
                float cover = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.61f, 0.98f, height));
                var green = Color.Lerp(new Color(0.21f, 0.33f, 0.22f), new Color(0.42f, 0.51f, 0.31f), damp);
                c = Color.Lerp(c, green * (0.90f + 0.20f * crack), cover * 0.92f);
            }
            return c;
        }, !mossOnTop);

        public static Texture2D MossTexture() => MakeTex("Moss", 256, (u, v) =>
        {
            float n = Fbm(u * 10, v * 10, 67);
            float detail = Fbm(u * 52, v * 52, 71, 2);
            var c = Color.Lerp(new Color(0.24f, 0.40f, 0.20f), new Color(0.42f, 0.62f, 0.30f), n);
            if (detail > 0.82f) c = Color.Lerp(c, new Color(0.48f, 0.71f, 0.28f), 0.5f);
            return c * (0.85f + detail * 0.3f);
        });

        public static Texture2D WoodTexture() => MakeTex("Wood", 256, (u, v) =>
        {
            float plank = Mathf.Floor(v * 4) / 4f;
            float grain = Fbm(u * 30, v * 4 + plank * 9, 83, 3);
            var c = Color.Lerp(new Color(0.32f, 0.24f, 0.16f), new Color(0.45f, 0.35f, 0.22f), grain);
            float edge = Math.Abs(v * 4 - Mathf.Floor(v * 4) - 0.5f);
            if (edge > 0.46f) c *= 0.55f;
            return c;
        });

        internal static Texture2D PlasterTexture() => MakeTex("Lime Plaster", 128, (u, v) =>
        {
            float wash = Fbm(u * 4.1f, v * 4.1f, 361, 3);
            float sand = Fbm(u * 44f, v * 44f, 362, 2);
            var color = Color.Lerp(new Color(0.71f, 0.62f, 0.49f), new Color(0.92f, 0.85f, 0.72f), wash);
            return color * (0.92f + sand * 0.13f);
        });

        internal static Texture2D CottageShingleTexture() => MakeTex("Unglazed roof tile", 128, (u, v) =>
        {
            float cloud = Fbm(u * 5.4f, v * 6.2f, 460, 3);
            float grit = Fbm(u * 36f, v * 31f, 461, 2);
            float edge = SoftStep(0.025f, 0.18f, Mathf.Min(u, 1f - u));
            float clay = 0.73f + 0.17f * cloud + 0.08f * grit + 0.07f * edge;
            return new Color(clay, clay, clay);
        });

        public static Texture2D GlowSprite() => MakeTex("Glow", 64, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, a * a);
        }, false);

        internal static Texture2D LanternReflection() => MakeTex("Lantern reflection", 64, (u, v) =>
        {
            float along = Mathf.Sin(v * Mathf.PI);
            float width = 0.13f + along * 0.22f;
            float across = 1f - Mathf.Clamp01(Mathf.Abs(u - 0.5f) / width);
            float ripples = 0.58f + 0.42f * Mathf.Sin(v * 43f + Mathf.Sin(v * 17f) * 0.42f);
            return new Color(1f, 1f, 1f, along * along * across * across * ripples);
        }, false);

        public static Texture2D BlossomPetalSprite() => MakeTex("Blossom Petal", 64, (u, v) =>
        {
            float x = (u - 0.5f) * 2f + 0.20f * (v - 0.5f);
            float y = (v - 0.5f) * 2f;
            x /= 0.77f + 0.16f * y;
            float edge = 1f - Mathf.Sqrt(x * x + y * y);
            float notchWidth = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.025f, 0.18f, Mathf.Abs(x)));
            float notchDepth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 0.93f, y));
            float alpha = Mathf.SmoothStep(0f, 1f, edge / 0.075f) * (1f - notchWidth * notchDepth);
            var color = Color.Lerp(new Color(0.89f, 0.53f, 0.59f), new Color(1f, 0.95f, 0.89f), v);
            color.a = alpha;
            return color;
        }, false);

        public static Texture2D MistSprite() => MakeTex("Mist", 256, (u, v) =>
        {
            float n = Fbm(u * 5, v * 2.5f, 97);
            float vertical = Mathf.Sin(v * Mathf.PI);
            float horizontal = Mathf.Sin(u * Mathf.PI);
            float a = Mathf.Clamp01(n * 1.3f - 0.25f) * vertical * horizontal * 0.5f;
            return new Color(0.75f, 0.88f, 0.92f, a);
        }, false);

        /// <summary>River water: soft banks across u, flowing light streaks along v (tiles along the flow).</summary>
        public static Texture2D RiverTexture() => MakeTex("River", 128, (u, v) =>
        {
            const float turn = 2f * Mathf.PI;
            float edge = Mathf.Min(u, 1f - u);
            float clear = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.16f, edge));
            float flow = Mathf.Sin(turn * (u * 2.6f + v) + Mathf.Sin(turn * (v * 2f - u)));
            float fine = Mathf.Sin(turn * (u * 7.8f - v * 3f) + flow * 0.8f);
            float glint = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.63f, 1f, (flow + fine) * 0.5f));
            var deep = new Color(0.15f, 0.34f, 0.36f);
            var shallow = new Color(0.33f, 0.55f, 0.52f);
            var color = Color.Lerp(shallow, deep, clear * (0.60f + 0.23f * flow));
            float bankGlints = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.07f, 0.23f, edge))) * 0.29f;
            color = Color.Lerp(color, new Color(0.70f, 0.83f, 0.81f), glint * 0.44f + bankGlints);
            return new Color(color.r, color.g, color.b, clear * (0.75f + 0.19f * glint));
        });

        public static Texture2D IvoryTexture() => MakeTex("Dice Ivory", 128, (u, v) =>
        {
            float cloud = Fbm(u * 4f, v * 4f, 831, 3);
            float grain = Fbm(u * 23f, v * 23f, 832, 2);
            var stone = Color.Lerp(new Color(0.71f, 0.65f, 0.53f), new Color(0.96f, 0.92f, 0.82f), cloud);
            return stone * (0.94f + grain * 0.12f);
        });

        /// <summary>One lobe of a butterfly wing, with a palette-specific band and veins.</summary>
        internal static Texture2D ButterflyWing(Color inside, Color outside, bool hindwing, int pattern) =>
            MakeTex(hindwing ? "Butterfly hindwing" : "Butterfly forewing", 128, (u, v) =>
            {
                float center = hindwing ? 0.45f - 0.13f * u : 0.46f + 0.24f * u;
                float shape = Ellipse(u, v, hindwing ? 0.42f : 0.49f, center,
                    hindwing ? 0.48f : 0.49f, hindwing ? 0.40f : 0.39f);
                if (hindwing && pattern == 1)
                    shape = Mathf.Max(shape, Ellipse(u, v + (u - 0.70f) * 0.9f, 0.78f, 0.14f, 0.19f, 0.064f));
                float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - shape));
                float branch = Mathf.Atan2(v - 0.45f, u + 0.075f);
                float vein = 1f - SoftStep(0.043f, 0.16f, Mathf.Abs(Mathf.Sin(branch * 8f + u * 1.33f)));
                float fade = SoftStep(0.12f, 0.90f, u);
                float shimmer = Mathf.Sin(u * 15f + v * 7f) * 0.11f + Mathf.Cos(v * 18f) * 0.05f;
                var ink = Color.Lerp(outside * 0.36f, new Color(0.12f, 0.10f, 0.16f), 0.46f);
                var color = Color.Lerp(inside, outside, Mathf.Clamp01(fade + shimmer));
                float band = Mathf.Abs(radial - (pattern == 0 ? 0.66f : 0.78f));
                float scaleBand = (1f - SoftStep(0.020f, pattern == 2 ? 0.12f : 0.085f, band)) * SoftStep(0.16f, 0.46f, u);
                color = Color.Lerp(color, pattern == 0 ? new Color(0.42f, 0.92f, 1f) : new Color(1f, 0.91f, 0.68f),
                    scaleBand * (pattern == 0 ? 0.53f : 0.34f));
                color = Color.Lerp(color, ink, vein * fade * (pattern == 1 ? 0.64f : 0.36f));
                color = Color.Lerp(color, ink, SoftStep(0.84f, 0.985f, radial) * 0.92f);
                float spot = Ellipse(u, v, hindwing ? 0.60f : 0.76f, hindwing ? 0.31f : 0.72f, 0.095f, 0.093f);
                if (pattern == 2 || pattern == 4)
                {
                    color = Color.Lerp(color, ink, SoftStep(-0.03f, 0.22f, spot) * 0.79f);
                    color = Color.Lerp(color, new Color(1f, 0.90f, 0.64f), SoftStep(0.44f, 0.86f, spot));
                }
                else if (pattern == 1 || pattern == 3)
                    color = Color.Lerp(color, new Color(1f, 0.95f, 0.81f), SoftStep(0.23f, 0.70f, spot) * 0.80f);
                color.a = SoftStep(-0.055f, 0.072f, shape);
                return color;
            }, false);

        /// <summary>Transparent membrane with branched veins, tapered tip and a fine pale edge.</summary>
        internal static Texture2D InsectWing(Color tint, bool longWing, bool hindwing) =>
            MakeTex(longWing ? "Dragonfly membrane" : "Insect membrane", 128, (u, v) =>
            {
                float span = Mathf.Clamp01((u - 0.01f) / 0.96f);
                float center = 0.45f + 0.12f * span - (hindwing ? 0.07f : 0f) * Mathf.Sin(span * Mathf.PI);
                float w = (longWing ? 0.32f : 0.40f) * Mathf.Sqrt(Mathf.Sin(Mathf.PI * Mathf.Pow(span, longWing ? 0.68f : 1.15f)) + 0.0001f);
                float edge = (w - Mathf.Abs(v - center)) * Mathf.Min(1f, span * 56f);
                float alpha = SoftStep(-0.002f, 0.029f, edge) * (1f - SoftStep(0.958f, 0.984f, u));
                float vein = Mathf.Abs(Mathf.Sin(Mathf.Atan2(v - center, u + 0.12f) * (longWing ? 19f : 10f) + u * 1.2f));
                float ribs = 1f - SoftStep(0.08f, 0.23f, vein);
                float cross = longWing ? (1f - SoftStep(0.055f, 0.22f, Mathf.Abs(Mathf.Sin((u * 17f + v * 3.2f) * Mathf.PI)))) * 0.26f : 0f;
                float rim = 1f - SoftStep(0.011f, 0.050f, edge);
                float sheen = 0.5f + 0.5f * Mathf.Sin(u * 10f - v * 7f);
                var color = Color.Lerp(tint, new Color(0.98f, 0.97f, 0.81f), sheen * 0.50f + rim * 0.28f);
                color = Color.Lerp(color, new Color(0.38f, 0.61f, 0.64f), ribs * 0.30f + cross * 0.19f);
                color.a = alpha * (0.35f + 0.18f * sheen + 0.23f * ribs + 0.16f * rim + cross);
                return color;
            }, false);

        private static float Ellipse(float u, float v, float cx, float cy, float rx, float ry)
        {
            float dx = (u - cx) / rx, dy = (v - cy) / ry;
            return 1f - (dx * dx + dy * dy);
        }

        private static float SoftStep(float start, float end, float x) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, x));

        /// <summary>Flat disc mesh (radius r, rings x segments) with uv spanning [0,1] across the diameter.</summary>
        public static Mesh DiscMesh(float radius, int segments)
        {
            var verts = new List<Vector3> { Vector3.zero };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                if (i > 0) { tris.Add(0); tris.Add(i + 1); tris.Add(i); }
            }
            var mesh = new Mesh { name = "Disc" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A tileable linear noise texture for the distant cloud layer.</summary>
        public static Texture2D CloudNoise()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "Cloud noise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float noise = PeriodicNoise(u, v, 5) * 0.54f + PeriodicNoise(u, v, 10) * 0.26f
                        + PeriodicNoise(u, v, 20) * 0.14f + PeriodicNoise(u, v, 40) * 0.06f;
                    byte value = (byte)Mathf.RoundToInt(255f * noise);
                    pixels[y * size + x] = new Color32(value, value, value, 255);
                }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        private static float PeriodicNoise(float u, float v, int period)
        {
            float x = u * period, y = v * period;
            int i = Mathf.FloorToInt(x), j = Mathf.FloorToInt(y);
            int right = (i + 1) % period, top = (j + 1) % period;
            float a = Mathf.SmoothStep(0f, 1f, x - i), b = Mathf.SmoothStep(0f, 1f, y - j);
            return Mathf.Lerp(Mathf.Lerp(Hash(i, j, period), Hash(right, j, period), a),
                Mathf.Lerp(Hash(i, top, period), Hash(right, top, period), a), b);
        }

        public static Texture2D RoundedPanelSprite()
        {
            const int size = 64, radius = 14;
            return MakeTex("Panel", size, (u, v) =>
            {
                float x = u * size, y = v * size;
                float cx = Mathf.Clamp(x, radius, size - radius), cy = Mathf.Clamp(y, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                // Feathered glass edge, no bright rim (the rim read as a stock button).
                float a = Mathf.Clamp01((radius - d) / 2.2f);
                return new Color(1f, 1f, 1f, a);
            }, false);
        }

        public static Sprite RoundedSprite()
        {
            var tex = RoundedPanelSprite();
            return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
        }

        public static Sprite CircleSprite()
        {
            var tex = GlowSpriteHard();
            return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D GlowSpriteHard() => MakeTex("Circle", 64, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            if (d > 1f) return new Color(1, 1, 1, 0);
            if (d > 0.92f) return new Color(0.85f, 0.9f, 0.9f, 1f);
            return Color.white;
        }, false);

        /// <summary>Displaced, flat-shaded rock mesh with several deterministic variants.</summary>
        public static Mesh RockMesh(int variant)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var src = sphere.GetComponent<MeshFilter>().sharedMesh;
            var verts = src.vertices;
            var displaced = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 dir = verts[i].normalized;
                float n = Fbm(dir.x * 2.4f + variant * 7.3f, dir.y * 2.4f + dir.z * 1.7f, 131 + variant * 17, 3);
                float r = 0.5f * (0.78f + n * 0.5f);
                displaced[i] = dir * r;
                if (displaced[i].y < -0.18f) displaced[i].y = -0.18f;
            }
            var tris = src.triangles;
            var flatVerts = new List<Vector3>();
            var flatNorms = new List<Vector3>();
            var flatUvs = new List<Vector2>();
            var flatTris = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = displaced[tris[t]], b = displaced[tris[t + 1]], c = displaced[tris[t + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                int baseIdx = flatVerts.Count;
                flatVerts.Add(a); flatVerts.Add(b); flatVerts.Add(c);
                flatNorms.Add(normal); flatNorms.Add(normal); flatNorms.Add(normal);
                flatUvs.Add(RockUv(a));
                flatUvs.Add(RockUv(b));
                flatUvs.Add(RockUv(c));
                flatTris.Add(baseIdx); flatTris.Add(baseIdx + 1); flatTris.Add(baseIdx + 2);
            }
            UnityEngine.Object.DestroyImmediate(sphere);
            var mesh = new Mesh { name = "Rock" + variant };
            mesh.SetVertices(flatVerts);
            mesh.SetNormals(flatNorms);
            mesh.SetUVs(0, flatUvs);
            mesh.SetTriangles(flatTris, 0);
            mesh.RecalculateBounds();
            return mesh;

            Vector2 RockUv(Vector3 p) => new Vector2(
                0.5f + p.x * 0.67f + p.z * 0.22f + (variant - 1) * 0.04f, p.y + 0.5f);
        }

        /// <summary>Rounded box with flat faces and continuous normals along the bevel.</summary>
        public static Mesh RoundedCubeMesh()
        {
            const float half = 0.5f, bevel = 0.105f, core = half - bevel;
            var stops = new[] { -half, -core - bevel * 0.5f, -core, 0f, core, core + bevel * 0.5f, half };
            int row = stops.Length;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var face in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                Vector3 u = Mathf.Abs(face.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, face);
                Vector3 v = Vector3.Cross(face, u);
                for (int y = 0; y < row; y++)
                    for (int x = 0; x < row; x++)
                    {
                        Vector3 p = face * half + u * stops[x] + v * stops[y];
                        var basePoint = new Vector3(Mathf.Clamp(p.x, -core, core), Mathf.Clamp(p.y, -core, core), Mathf.Clamp(p.z, -core, core));
                        Vector3 normal = (p - basePoint).normalized;
                        int d = verts.Count;
                        verts.Add(basePoint + normal * bevel);
                        normals.Add(normal);
                        uvs.Add(new Vector2(stops[x] + half, stops[y] + half));
                        if (x == 0 || y == 0) continue;
                        int c = d - 1, b = d - row, a = b - 1;
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(b); triangles.Add(d); triangles.Add(c);
                    }
            }
            var mesh = new Mesh { name = "RoundedCube" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        /// <summary>Irregular flat disc used for surface water.</summary>
        public static Mesh IrregularDiscMesh(int variant)
        {
            const int segments = 18;
            var verts = new List<Vector3> { Vector3.zero };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var tris = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float r = 0.5f * (0.82f + Hash(i, variant, 211) * 0.30f);
                verts.Add(new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int i = 1; i <= segments; i++)
            {
                int next = i == segments ? 1 : i + 1;
                tris.Add(0); tris.Add(next); tris.Add(i);
            }
            var mesh = new Mesh { name = "Disc" + variant };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>Simple bent leaf: two quads folded along the midrib.</summary>
        public static Mesh LeafMesh()
        {
            var verts = new List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(0.09f, 0.03f, 0.10f), new Vector3(-0.09f, 0.03f, 0.10f),
                new Vector3(0.07f, 0.05f, 0.22f), new Vector3(-0.07f, 0.05f, 0.22f), new Vector3(0, 0.03f, 0.32f),
                new Vector3(0, 0.06f, 0.10f), new Vector3(0, 0.08f, 0.22f)
            };
            var tris = new List<int> { 0, 6, 1, 0, 2, 6, 6, 7, 3, 6, 1, 3, 6, 4, 7, 6, 2, 4, 7, 5, 3, 7, 4, 5 };
            var uvs = new List<Vector2>();
            foreach (var v in verts) uvs.Add(new Vector2(v.x + 0.5f, v.z));
            var mesh = new Mesh { name = "Leaf" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
