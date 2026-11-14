using UnityEngine;
using UnityEngine.Rendering;

namespace DestinyGarden.Presentation
{
    internal static class BottleCork
    {
        private const float TopRadius = 3.25f;
        private const float Bottom = BottleMesh.MouthHeight - 0.50f;
        private const float Top = BottleMesh.MouthHeight + 2.12f;
        private const float CapTextureCenter = 0.79f;
        private const float CapTextureRadius = 0.18f;
        private const float SideTextureHeight = 0.56f;

        internal static Mesh CreateMesh()
        {
            // The narrow foot passes through the rolled lip. Only the cork above the lip flares.
            float neck = BottleMesh.NeckInnerRadius;
            var mesh = CottageModel.Revolve("Cork stopper", new[]
            {
                new Vector2(0f, Bottom),
                new Vector2(neck - 0.13f, Bottom),
                new Vector2(neck - 0.02f, Bottom + 0.16f),
                new Vector2(neck + 0.09f, BottleMesh.MouthHeight + 0.11f),
                new Vector2(3.22f, BottleMesh.MouthHeight + 0.37f),
                new Vector2(3.44f, Top - 0.49f),
                new Vector2(3.47f, Top - 0.29f),
                new Vector2(3.44f, Top - 0.15f),
                new Vector2(3.37f, Top - 0.05f),
                new Vector2(TopRadius, Top),
                new Vector2(0f, Top)
            });
            var uv = mesh.uv;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            for (int i = 0; i < uv.Length; i++)
            {
                var point = vertices[i];
                uv[i] = normals[i].y > 0.99f
                    ? new Vector2(0.5f + point.x / TopRadius * CapTextureRadius,
                        CapTextureCenter + point.z / TopRadius * CapTextureRadius)
                    : new Vector2(uv[i].x, Mathf.InverseLerp(Bottom, Top, point.y) * SideTextureHeight);
            }
            mesh.uv = uv;
            return mesh;
        }

        internal static Texture2D CreateTexture()
        {
            const int size = 256;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    pixels[y * size + x] = v < 0.59f ? Side(u, v) : Cap(u, v);
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Cork grain and ribbon", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return texture;
        }

        private static Color Side(float u, float v)
        {
            // Blend the ends of the grain so the vertical seam can face the camera.
            float height = Mathf.Clamp01(v / SideTextureHeight);
            var grain = Color.Lerp(Grain(u * 4f, height), Grain((u - 1f) * 4f, height), Mathf.SmoothStep(0f, 1f, u));
            float compression = Mathf.SmoothStep(0.65f, 1f, Mathf.InverseLerp(0.10f, 0.30f, height));
            float ribbonHeight = 0.53f + 0.026f * Mathf.Sin(u * 2f * Mathf.PI);
            float offset = Mathf.Abs(height - ribbonHeight);
            float ribbon = 1f - SmoothRange(0.144f, 0.172f, offset);
            var wine = new Color(0.48f, 0.13f, 0.28f) * (0.80f + 0.26f * grain.r);
            var gold = new Color(1f, 0.78f, 0.43f);
            float hem = 1f - SmoothRange(0.010f, 0.030f, Mathf.Abs(offset - 0.114f));
            float ribbonEdge = SmoothRange(0.11f, 0.16f, offset);
            var cloth = Color.Lerp(wine * (1.04f - ribbonEdge * 0.22f), gold, hem * 0.86f);
            var color = Color.Lerp(grain * compression, cloth, ribbon);
            // The stamped seal faces the garden's opening view.
            var sealPoint = new Vector2((u - 0.75f) / 0.036f, (height - ribbonHeight) / 0.27f);
            float sealEdge = sealPoint.magnitude;
            float seal = 1f - SmoothRange(0.92f, 1.06f, sealEdge);
            float stamp = Mathf.Sqrt(Mathf.Abs(sealPoint.x)) + Mathf.Sqrt(Mathf.Abs(sealPoint.y));
            float impression = 1f - SmoothRange(0.88f, 1.02f, stamp);
            float bead = (1f - SmoothRange(0.04f, 0.12f, Mathf.Abs(sealEdge - 0.77f)))
                * Mathf.Clamp01(0.53f + sealPoint.y * 0.53f - sealPoint.x * 0.20f);
            var wax = Color.Lerp(new Color(0.46f, 0.14f, 0.22f), new Color(0.70f, 0.29f, 0.32f), bead * 0.72f);
            return Color.Lerp(color, Color.Lerp(wax, gold, impression * 0.90f), seal);
        }

        private static Color Cap(float u, float v)
        {
            float x = (u - 0.5f) / CapTextureRadius, y = (v - CapTextureCenter) / CapTextureRadius;
            Color grain = Grain(x * 0.92f, y * 0.92f) * 1.04f;
            float radius = Mathf.Sqrt(x * x + y * y);
            float ring = 1f - SmoothRange(0.010f, 0.027f, Mathf.Abs(radius - 0.49f));
            float star = Mathf.Sqrt(Mathf.Abs(x) / 0.32f) + Mathf.Sqrt(Mathf.Abs(y) / 0.32f);
            float stamp = Mathf.Max(ring, 1f - SmoothRange(0.92f, 1.08f, star));
            return Color.Lerp(grain, new Color(0.39f, 0.22f, 0.13f), stamp * 0.65f);
        }

        private static float SmoothRange(float lower, float upper, float value) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lower, upper, value));

        private static Color Grain(float x, float y)
        {
            float flecks = ProceduralArt.Fbm(x * 25f, y * 17f, 1859, 2);
            float grain = ProceduralArt.Fbm(x * 9f, y * 6f, 107, 3);
            float pores = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.63f, 0.83f, flecks));
            var cork = Color.Lerp(new Color(0.54f, 0.33f, 0.17f), new Color(0.88f, 0.69f, 0.46f), grain);
            return Color.Lerp(cork, new Color(0.36f, 0.21f, 0.11f), pores * 0.63f);
        }

        internal static Material CreateMaterial(Shader lit, Texture2D texture)
        {
            // Fade with the lip as the camera enters the garden. Draw between the far and near glass.
            var material = new Material(lit) { name = "Matte cork", renderQueue = 3049 };
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0.14f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("DepthNormals", false);
            return material;
        }
    }
}
