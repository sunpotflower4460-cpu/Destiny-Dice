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

        public static Texture2D StoneTexture() => MakeTex("Stone", 256, (u, v) =>
        {
            float n = Fbm(u * 6, v * 6, 37);
            float crack = Fbm(u * 20, v * 20, 41, 3);
            var c = Color.Lerp(new Color(0.26f, 0.28f, 0.28f), new Color(0.48f, 0.51f, 0.52f), n);
            if (crack > 0.72f && crack < 0.76f) c *= 0.55f;
            float moss = Fbm(u * 9 + 40, v * 9, 53);
            if (moss > 0.62f) c = Color.Lerp(c, new Color(0.25f, 0.42f, 0.20f), (moss - 0.62f) * 1.8f);
            return c;
        });

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

        public static Texture2D GlowSprite() => MakeTex("Glow", 64, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, a * a);
        }, false);

        public static Texture2D MistSprite() => MakeTex("Mist", 256, (u, v) =>
        {
            float n = Fbm(u * 5, v * 2.5f, 97);
            float vertical = Mathf.Sin(v * Mathf.PI);
            float horizontal = Mathf.Sin(u * Mathf.PI);
            float a = Mathf.Clamp01(n * 1.3f - 0.25f) * vertical * horizontal * 0.5f;
            return new Color(0.75f, 0.88f, 0.92f, a);
        }, false);

        /// <summary>Sea surface: soft swell bands over deep indigo-teal, brighter near the island.</summary>
        public static Texture2D SeaTexture() => MakeTex("Sea", 512, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float near = Mathf.Clamp01(1f - d * 1.15f);
            float swell = Fbm(u * 18f, v * 18f, 211, 4);
            float ripple = Mathf.Sin((u * 0.6f + v) * 140f + swell * 9f) * 0.5f + 0.5f;
            var deep = new Color(0.05f, 0.16f, 0.27f);
            var lagoon = new Color(0.20f, 0.62f, 0.66f);
            var c = Color.Lerp(deep, lagoon, near * near);
            c += new Color(0.05f, 0.09f, 0.12f) * (swell - 0.45f) * 0.9f;
            c += new Color(0.10f, 0.16f, 0.18f) * Mathf.Pow(ripple, 6f) * (0.25f + near * 0.6f);
            return new Color(Mathf.Max(0, c.r), Mathf.Max(0, c.g), Mathf.Max(0, c.b), 1f);
        }, false);

        /// <summary>River water: soft banks across u, flowing light streaks along v (tiles along the flow).</summary>
        public static Texture2D RiverTexture() => MakeTex("River", 128, (u, v) =>
        {
            float bank = Mathf.Sin(u * Mathf.PI);
            float a = Mathf.Clamp01(bank * 1.6f) * 0.92f;
            float streak = Fbm(u * 5f, v * 3f, 818, 3);
            float glint = Mathf.Pow(Mathf.Clamp01(Fbm(u * 9f + 3f, v * 6f, 819, 2) * 1.6f - 0.6f), 3f);
            var c = Color.Lerp(new Color(0.20f, 0.62f, 0.78f), new Color(0.55f, 0.90f, 0.98f), streak);
            c = Color.Lerp(c, Color.white, glint * 0.8f);
            return new Color(c.r, c.g, c.b, a);
        });

        /// <summary>Scattered glints for a scrolling sparkle layer on the sea.</summary>
        public static Texture2D SparkleTexture() => MakeTex("Sparkle", 256, (u, v) =>
        {
            int gx = Mathf.FloorToInt(u * 32), gy = Mathf.FloorToInt(v * 32);
            float h = Hash(gx, gy, 733);
            if (h < 0.80f) return new Color(1, 1, 1, 0);
            float cx = (gx + 0.5f) / 32f, cy = (gy + 0.5f) / 32f;
            float dx = (u - cx) * 32f, dy = (v - cy) * 32f;
            float a = Mathf.Clamp01(1f - (Mathf.Abs(dx) * 4f + Mathf.Abs(dy) * 1.2f)) * (h - 0.8f) * 5f;
            return new Color(1f, 0.95f, 0.82f, a);
        });

        /// <summary>Radial ring of soft light for the round island's shoreline (disc uv: 0.5 = centre, 1 = rim).</summary>
        public static Texture2D RingGlow(float ringAt, float width) => MakeTex("RingGlow", 512, (u, v) =>
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float foam = Mathf.Exp(-Mathf.Pow((r - ringAt) / width, 2f)) * 0.85f
                         + Mathf.Exp(-Mathf.Pow((r - ringAt - width) / (width * 3f), 2f)) * 0.30f;
            float n = Fbm(u * 40f, v * 40f, 404, 3);
            return new Color(0.85f, 0.97f, 1f, Mathf.Clamp01(foam * (0.65f + n * 0.7f)));
        }, false);

        /// <summary>Glow that hugs the outside of the square island (shoreline foam light).</summary>
        public static Texture2D ShoreGlow() => MakeTex("Shore", 256, (u, v) =>
        {
            const float half = 0.30f; // island half-size in uv
            float dx = Mathf.Max(0, Mathf.Abs(u - 0.5f) - half);
            float dy = Mathf.Max(0, Mathf.Abs(v - 0.5f) - half);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            bool inside = Mathf.Abs(u - 0.5f) < half && Mathf.Abs(v - 0.5f) < half;
            if (inside) return new Color(1, 1, 1, 0);
            float foam = Mathf.Exp(-d * 60f) * 0.9f + Mathf.Exp(-d * 12f) * 0.35f;
            float n = Fbm(u * 24f, v * 24f, 401, 3);
            return new Color(0.80f, 0.96f, 1f, Mathf.Clamp01(foam * (0.6f + n * 0.8f)));
        }, false);

        /// <summary>Butterfly wing (one side): two lobes, iridescent body colour, pale eye spot.</summary>
        public static Texture2D ButterflyWing(Color inner, Color outer) => MakeTex("Wing", 128, (u, v) =>
        {
            // u: 0 at the body, 1 at the wing tip. Upper lobe above v=0.45, lower below.
            float upper = Ellipse(u, v, 0.48f, 0.66f, 0.50f, 0.36f);
            float lower = Ellipse(u, v, 0.38f, 0.26f, 0.36f, 0.26f);
            float shape = Mathf.Max(upper, lower);
            if (shape <= 0f) return new Color(1, 1, 1, 0);
            float edge = Mathf.Clamp01(shape * 6f);
            float t = Mathf.Clamp01(u * 1.1f);
            var c = Color.Lerp(inner, outer, t);
            float vein = Mathf.Abs(Mathf.Sin(Mathf.Atan2(v - 0.45f, u) * 9f));
            c *= 0.85f + 0.15f * vein;
            float spot = Ellipse(u, v, 0.66f, 0.70f, 0.09f, 0.09f);
            if (spot > 0) c = Color.Lerp(c, new Color(1f, 0.97f, 0.85f), Mathf.Clamp01(spot * 8f));
            float rim = Mathf.Clamp01(1f - shape * 5f);
            c = Color.Lerp(c, new Color(0.08f, 0.05f, 0.12f), rim * 0.8f);
            return new Color(c.r, c.g, c.b, edge);
        }, false);

        private static float Ellipse(float u, float v, float cx, float cy, float rx, float ry)
        {
            float dx = (u - cx) / rx, dy = (v - cy) / ry;
            return 1f - (dx * dx + dy * dy);
        }

        /// <summary>
        /// Two-frame angel silhouette sheet (wings up | wings down): glowing robe, head,
        /// halo and feathered wings, with a soft aura so bloom can lift it.
        /// </summary>
        public static Texture2D AngelSheet()
        {
            const int w = 256, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Angels" };
            var colors = new Color[w * h];
            for (int frame = 0; frame < 2; frame++)
            {
                float lift = frame == 0 ? 0.10f : -0.06f;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < 128; x++)
                    {
                        float u = x / 128f, v = y / 128f;
                        float a = 0f;
                        // Robe: soft trapezoid.
                        float robeW = Mathf.Lerp(0.15f, 0.06f, Mathf.InverseLerp(0.12f, 0.58f, v));
                        if (v > 0.12f && v < 0.60f && Mathf.Abs(u - 0.5f) < robeW) a = 1f;
                        // Head and halo.
                        float head = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.68f));
                        if (head < 0.07f) a = 1f;
                        float halo = Mathf.Abs(Vector2.Distance(new Vector2(u, v * 2.8f), new Vector2(0.5f, 0.80f * 2.8f)) - 0.09f);
                        float haloA = Mathf.Clamp01(1f - halo / 0.018f);
                        // Wings: feathered ellipses on both sides, tilted by the frame.
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float cx = 0.5f + side * 0.20f, cy = 0.50f + lift;
                            float dx = (u - cx) / 0.20f, dy = (v - cy - (u - 0.5f) * side * lift * 1.6f) / 0.13f;
                            float e = 1f - (dx * dx + dy * dy);
                            if (e > 0f)
                            {
                                float feather = 0.75f + 0.25f * Mathf.Sin((u - 0.5f) * side * 60f);
                                a = Mathf.Max(a, Mathf.Clamp01(e * 4f) * feather);
                            }
                        }
                        float aura = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2.1f);
                        var c = new Color(1f, 0.97f, 0.88f, Mathf.Max(a, aura * aura * 0.35f));
                        if (haloA > 0.01f) c = new Color(1f, 0.85f, 0.45f, Mathf.Max(c.a, haloA));
                        colors[y * w + frame * 128 + x] = c;
                    }
            }
            tex.SetPixels(colors);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Screen overlay that makes the view feel like looking into a glass vessel: a faint cool
        /// rim of light at the edges and two soft curved reflections. Mostly transparent.
        /// </summary>
        public static Texture2D GlassEdge()
        {
            const int w = 320, h = 180;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "GlassEdge", wrapMode = TextureWrapMode.Clamp };
            var c = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1), v = y / (float)(h - 1);
                    // Elliptical vessel edge.
                    float ex = (u - 0.5f) / 0.5f, ey = (v - 0.5f) / 0.5f;
                    float r = Mathf.Sqrt(ex * ex * 0.92f + ey * ey * 1.05f);
                    float rim = Mathf.Clamp01((r - 0.86f) / 0.40f);
                    float a = rim * rim * 0.34f;
                    var col = new Color(0.62f, 0.82f, 0.92f, a);
                    // Curved reflections: left tall arc, right thin arc.
                    float arcL = Mathf.Abs(u - (0.075f + 0.05f * Mathf.Pow(v - 0.5f, 2) * 4f));
                    float sL = Mathf.Clamp01(1f - arcL / 0.016f) * Mathf.Clamp01(1f - Mathf.Abs(v - 0.52f) / 0.36f);
                    float arcR = Mathf.Abs(u - (0.935f - 0.04f * Mathf.Pow(v - 0.5f, 2) * 4f));
                    float sR = Mathf.Clamp01(1f - arcR / 0.008f) * Mathf.Clamp01(1f - Mathf.Abs(v - 0.60f) / 0.22f);
                    float s = Mathf.Max(sL * 0.16f, sR * 0.12f);
                    if (s > 0f) col = new Color(Mathf.Lerp(col.r, 1f, 0.7f), Mathf.Lerp(col.g, 1f, 0.7f), 1f, Mathf.Max(col.a, s));
                    c[y * w + x] = col;
                }
            tex.SetPixels(c);
            tex.Apply();
            return tex;
        }

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

        /// <summary>White-to-grey vertical gradient (horizon bright, zenith darker); tinted by the sky colour.</summary>
        public static Texture2D SkyTint() => MakeTex("SkyTint", 64, (u, v) =>
        {
            float k = Mathf.Lerp(1f, 0.45f, Mathf.SmoothStep(0.5f, 1f, v));
            return new Color(k, k, k, 1f);
        }, false);

        public static Texture2D SkyGradient() => MakeTex("Sky", 64, (u, v) =>
            Color.Lerp(new Color(0.05f, 0.14f, 0.18f), new Color(0.01f, 0.03f, 0.05f), v), false);

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
                flatUvs.Add(new Vector2(a.x + 0.5f, a.y + 0.5f));
                flatUvs.Add(new Vector2(b.x + 0.5f, b.y + 0.5f));
                flatUvs.Add(new Vector2(c.x + 0.5f, c.y + 0.5f));
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
        }

        /// <summary>Cube with corners pulled toward a sphere for a worn stone dice look.</summary>
        public static Mesh RoundedCubeMesh(float roundness = 0.42f)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var src = cube.GetComponent<MeshFilter>().sharedMesh;
            var verts = (Vector3[])src.vertices.Clone();
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = verts[i];
                Vector3 s = p.normalized * 0.5f;
                verts[i] = Vector3.Lerp(p, s, roundness * Mathf.Clamp01(p.magnitude / 0.87f));
            }
            var mesh = new Mesh { name = "RoundedCube" };
            mesh.vertices = verts;
            mesh.triangles = src.triangles;
            mesh.uv = src.uv;
            mesh.RecalculateNormals();
            UnityEngine.Object.DestroyImmediate(cube);
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
