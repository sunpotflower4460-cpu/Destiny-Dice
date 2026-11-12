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
                if (d > radius) return new Color(1, 1, 1, 0);
                if (d > radius - 2) return new Color(0.75f, 0.85f, 0.85f, 0.85f);
                return Color.white;
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
