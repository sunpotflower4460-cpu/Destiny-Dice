using System;
using System.Collections.Generic;
using UnityEngine;

namespace DestinyGarden.Presentation
{
    internal static class GardenUiArt
    {
        internal enum Surface { Enamel, Brass, Amethyst, Water, Wind, Light, Seed }
        internal enum Symbol
        {
            Bottle, Die, Star, Menu, Back, Observe, House, Journal, Leaf, Compass, Settings, Help,
            Water, Wind, Seed
        }

        private static readonly Dictionary<Surface, Sprite> Surfaces = new Dictionary<Surface, Sprite>();
        private static readonly Dictionary<Symbol, Sprite> Symbols = new Dictionary<Symbol, Sprite>();
        private static Sprite _well, _focus, _panel, _plaque, _rule, _titleRule, _veil;

        internal static Sprite Button(Surface surface)
        {
            if (Surfaces.TryGetValue(surface, out var sprite) && sprite != null) return sprite;
            var (top, bottom, rim) = surface switch
            {
                Surface.Brass   => (0xffedb9u, 0xd8a268u, 0xfbe0a5u),
                Surface.Amethyst => (0x684861u, 0x34213du, 0xd4a9c2u),
                Surface.Water   => (0x245875u, 0x142e43u, 0x78b7d9u),
                Surface.Wind    => (0x286454u, 0x142f32u, 0x87c4a5u),
                Surface.Light   => (0x625132u, 0x362a22u, 0xd4b16du),
                Surface.Seed    => (0x62513du, 0x352c26u, 0xc5a57bu),
                _               => (0x2f5359u, 0x172a3bu, 0x9cb3a8u)
            };
            return Surfaces[surface] = Plate(surface.ToString(), 9f, 14, top, bottom, rim, 1f);
        }

        internal static Sprite Well => _well != null ? _well :
            _well = Plate("Button base", 9f, 14, 0x25253b, 0x121428, 0x61566f, 1f);

        internal static Sprite Panel => _panel != null ? _panel :
            _panel = Plate("Garden panel", 17f, 32, 0x33304d, 0x18182d, 0xcfaa75, 0.96f, engraved: true);

        internal static Sprite Plaque => _plaque != null ? _plaque :
            _plaque = Plate("Garden plaque", 9f, 14, 0x2c3047, 0x181b30, 0x8a8095, 0.93f);

        internal static Sprite Focus => _focus != null ? _focus :
            _focus = Bake("Button focus", 64, 64, 14, (x, y) =>
            {
                float depth = CutCornerDepth(x, y, 64, 64, 10f);
                return Pigment(0xffedb6, Band(depth, 1.6f, 0.9f));
            });

        internal static Sprite Rule => _rule != null ? _rule :
            _rule = Bake("Engraved rule", 128, 2, 0, (x, _) =>
                Pigment(0xd5b478, 0.7f * Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(x / 64f - 1f))));

        internal static Sprite TitleRule => _titleRule != null ? _titleRule :
            _titleRule = Bake("Gilded title ornament", 384, 16, 0, (x, y) =>
            {
                float u = Mathf.Abs(x - 192f);
                float taper = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(132f, 192f, u));
                float line = Band(y, 8f, 0.48f) * Mathf.Clamp01((u - 13f) * 0.20f);
                float curl = Band(y, 8f + 4f * Mathf.Sin((u - 16f) * 0.095f), 0.42f)
                    * Mathf.Clamp01((u - 13f) * 0.26f) * Mathf.Clamp01((56f - u) * 0.15f);
                float star = Diamond(u, y, 0f, 8f, 6.5f, 6.8f);
                float leaf = Diamond(u, y, 9.5f, 8f, 2.1f, 2.0f);
                return Pigment(0xf5d498, Mathf.Max(Mathf.Max(star, leaf * 0.85f), Mathf.Max(line, curl) * taper * 0.81f));
            });

        internal static Sprite Veil => _veil != null ? _veil :
            _veil = Bake("Opening title shade", 96, 64, 0, (x, y) =>
            {
                float u = Mathf.Abs(x / 48f - 1f), v = Mathf.Abs(y / 32f - 1f);
                float across = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, u));
                float down = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 1f, v));
                return Pigment(0x1b142d, 0.78f * across * down);
            });

        internal static Color Pigment(uint rgb, float alpha = 1f) => new Color(
            ((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);

        private static Sprite Plate(string name, float cut, int slice, uint top, uint bottom, uint rim,
            float opacity, bool engraved = false)
        {
            const int width = 96, height = 80;
            Color light = Pigment(top), shade = Pigment(bottom), edge = Pigment(rim);
            return Bake(name, width, height, slice, (x, y) =>
            {
                float depth = CutCornerDepth(x, y, width, height, cut);
                float vertical = y / height;
                Color metal = Color.Lerp(shade, light, vertical);
                // Leave a little variation along the fixed edges of the pressed paper.
                float grain = Mathf.Sin(x * 1.73f + y * 2.31f) * Mathf.Sin(x * 0.81f - y * 1.47f);
                metal *= 1f + 0.031f * grain * (1f - Mathf.InverseLerp(5f, 13f, depth));
                metal = Color.Lerp(metal, edge, Band(depth, 1.7f, 0.75f) * (0.46f + 0.44f * vertical));
                metal = Color.Lerp(metal, shade * 0.65f, Band(depth, 3.7f, 0.6f) * 0.52f);
                metal = Color.Lerp(metal, edge, Band(depth, 5.5f, 0.55f) * 0.28f);
                if (engraved)
                {
                    // Both ends of each cut corner have a small engraved leaf and a fine inner rule.
                    float u = Mathf.Min(x, width - x), v = Mathf.Min(y, height - y);
                    float leaf = Mathf.Max(Diamond(u, v, 25f, 11f, 3.8f, 1.6f),
                        Diamond(u, v, 11f, 25f, 1.6f, 3.8f));
                    metal = Color.Lerp(metal, edge, Mathf.Max(leaf * 0.78f, Band(depth, 8.5f, 0.4f) * 0.19f));
                }
                metal.a = Mathf.Clamp01(depth + 0.35f) * opacity;
                return metal;
            });
        }

        // The straight edges and bevel occupy the fixed portions of the nine-slice.
        private static float CutCornerDepth(float x, float y, int width, int height, float cut)
        {
            float u = Mathf.Min(x, width - x) - 0.75f;
            float v = Mathf.Min(y, height - y) - 0.75f;
            return Mathf.Min(Mathf.Min(u, v), (u + v - cut) * 0.707107f);
        }

        private static float Band(float depth, float center, float halfWidth) =>
            Mathf.Clamp01(halfWidth + 0.5f - Mathf.Abs(depth - center));

        private static float Diamond(float x, float y, float cx, float cy, float rx, float ry) =>
            Mathf.Clamp01(2f - 2f * (Mathf.Abs(x - cx) / rx + Mathf.Abs(y - cy) / ry));

        private static Sprite Bake(string name, int width, int height, int border, Func<float, float, Color> pixel)
        {
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixel(x + 0.5f, y + 0.5f);
            return MakeSprite(name, width, height, border, pixels);
        }

        private static Sprite MakeSprite(string name, int width, int height, int border, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, Vector4.one * border);
            sprite.name = name;
            return sprite;
        }

        internal static Sprite Icon(Symbol symbol)
        {
            if (Symbols.TryGetValue(symbol, out var sprite) && sprite != null) return sprite;
            var ink = new Glyph();
            switch (symbol)
            {
                case Symbol.Bottle:
                    ink.Ring(32, 32, 29, 1.2f);
                    ink.Ring(32, 32, 26, 0.65f);
                    ink.Stroke(2, (26, 47), (38, 47), (38, 43), (36, 43), (36, 38), (45, 33), (49, 26),
                        (48, 20), (44, 15), (20, 15), (16, 20), (15, 26), (19, 33), (28, 38), (28, 43), (26, 43), (26, 47));
                    ink.Stroke(1.6f, (27, 48), (25, 53), (39, 53), (37, 48));
                    ink.Stroke(1.7f, (18, 22), (24, 24), (33, 22), (40, 25), (46, 23));
                    ink.Stroke(1.8f, (31, 25), (31, 30), (28, 33), (24, 34), (26, 30), (31, 29),
                        (34, 34), (39, 36), (37, 32), (32, 30));
                    ink.Star(9, 33, 3, 5);
                    ink.Star(54, 36, 3, 5);
                    ink.Star(32, 58, 2.7f, 4);
                    break;
                case Symbol.Die:
                    ink.Stroke(2.7f, (8, 43), (32, 56), (56, 43), (54, 19), (32, 7), (10, 19), (8, 43),
                        (32, 30), (56, 43));
                    ink.Stroke(2.7f, (32, 30), (32, 7));
                    ink.Star(32, 43, 5, 5);
                    ink.Dot(16, 32, 2.8f);
                    ink.Dot(25, 21, 2.8f);
                    ink.Dot(48, 32, 2.7f);
                    ink.Dot(44, 25, 2.7f);
                    ink.Dot(38, 18, 2.7f);
                    break;
                case Symbol.Star:
                    ink.Star(29, 31, 20, 25);
                    ink.Star(49, 49, 7, 9);
                    break;
                case Symbol.Menu:
                    ink.Stroke(3.4f, (11, 44), (52, 44));
                    ink.Stroke(3.4f, (11, 31), (46, 31));
                    ink.Stroke(3.4f, (11, 18), (38, 18));
                    ink.Star(49, 18, 5, 6);
                    break;
                case Symbol.Back:
                    ink.Stroke(3.8f, (29, 48), (13, 32), (29, 16));
                    ink.Stroke(3.8f, (13, 32), (52, 32));
                    break;
                case Symbol.Observe:
                    ink.Ring(27, 37, 16.5f, 3);
                    ink.Stroke(4, (39, 25), (53, 11));
                    ink.Stroke(2.5f, (27, 28), (27, 35), (34, 42), (38, 44), (35, 36), (27, 34),
                        (23, 40), (17, 42), (19, 36), (27, 34));
                    break;
                case Symbol.House:
                    ink.Stroke(3, (6, 31), (32, 54), (58, 31));
                    ink.Stroke(3, (13, 36), (13, 12), (51, 12), (51, 36));
                    ink.Stroke(3, (27, 12), (27, 26), (37, 26), (37, 12));
                    ink.Stroke(3, (43, 45), (43, 52), (50, 52), (50, 39));
                    break;
                case Symbol.Journal:
                    ink.Stroke(3, (32, 12), (22, 16), (8, 16), (8, 49), (22, 49), (32, 45), (42, 49),
                        (56, 49), (56, 16), (42, 16), (32, 12), (32, 45));
                    ink.Stroke(2.4f, (15, 40), (22, 40), (26, 38));
                    ink.Stroke(2.4f, (15, 31), (22, 31), (26, 29));
                    ink.Stroke(2.4f, (39, 38), (43, 40), (49, 40));
                    ink.Stroke(2.4f, (39, 29), (43, 31), (49, 31));
                    break;
                case Symbol.Leaf:
                    ink.Stroke(3, (12, 11), (34, 33), (46, 46));
                    ink.Stroke(3, (19, 20), (17, 33), (23, 44), (36, 51), (54, 54), (51, 38), (43, 25),
                        (31, 19), (19, 20));
                    ink.Stroke(2.4f, (28, 27), (27, 37));
                    ink.Stroke(2.4f, (35, 35), (44, 35));
                    break;
                case Symbol.Compass:
                    ink.Ring(32, 32, 22, 2.8f);
                    ink.Stroke(2.8f, (43, 44), (36, 28), (21, 20), (28, 36), (43, 44));
                    ink.Dot(32, 32, 2.2f);
                    break;
                case Symbol.Settings:
                    ink.Stroke(3, (11, 45), (31, 45));
                    ink.Stroke(3, (42, 45), (53, 45));
                    ink.Ring(36, 45, 5.4f, 2.7f);
                    ink.Stroke(3, (11, 31), (19, 31));
                    ink.Stroke(3, (30, 31), (53, 31));
                    ink.Ring(24.5f, 31, 5.4f, 2.7f);
                    ink.Stroke(3, (11, 17), (33, 17));
                    ink.Stroke(3, (44, 17), (53, 17));
                    ink.Ring(38.5f, 17, 5.4f, 2.7f);
                    break;
                case Symbol.Help:
                    ink.Ring(32, 32, 23, 2.7f);
                    ink.Stroke(3.5f, (23, 40), (26, 45), (32, 47), (38, 45), (41, 40), (39, 35), (32, 31), (32, 26));
                    ink.Dot(32, 19, 2.4f);
                    break;
                case Symbol.Water:
                    ink.Stroke(4, (32, 55), (19, 38), (14, 28), (15, 20), (21, 13), (32, 10),
                        (43, 13), (49, 20), (50, 28), (45, 38), (32, 55));
                    ink.Stroke(3.2f, (22, 26), (24, 21), (29, 18));
                    break;
                case Symbol.Wind:
                    ink.Stroke(4, (9, 40), (40, 40), (47, 43), (49, 48), (47, 53), (41, 54), (37, 51));
                    ink.Stroke(4, (5, 30), (52, 30), (57, 27), (57, 22), (53, 19), (48, 20));
                    ink.Stroke(3.6f, (15, 19), (35, 19), (39, 16), (39, 11), (35, 8), (30, 9));
                    break;
                case Symbol.Seed:
                    ink.Stroke(3.6f, (31, 10), (31, 29), (36, 39));
                    ink.Stroke(3.4f, (31, 28), (22, 29), (15, 35), (12, 44), (24, 42), (30, 36), (31, 28));
                    ink.Stroke(3.4f, (33, 35), (36, 47), (45, 52), (55, 53), (51, 43), (44, 36), (33, 35));
                    ink.Stroke(3.2f, (18, 10), (45, 10));
                    break;
            }
            return Symbols[symbol] = ink.ToSprite(symbol.ToString());
        }

        // The glyphs share a 64-unit coordinate system. The mask keeps their color independent of the plate.
        private sealed class Glyph
        {
            private const int Size = 64;
            private readonly Color32[] _pixels = new Color32[Size * Size];

            internal void Stroke(float width, params (float x, float y)[] path)
            {
                for (int i = 1; i < path.Length; i++)
                {
                    var a = new Vector2(path[i - 1].x, path[i - 1].y);
                    var axis = new Vector2(path[i].x, path[i].y) - a;
                    float lengthSquared = axis.sqrMagnitude;
                    Paint((x, y) =>
                    {
                        var offset = new Vector2(x, y) - a;
                        float t = Mathf.Clamp01(Vector2.Dot(offset, axis) / lengthSquared);
                        return width * 0.5f + 0.65f - (offset - axis * t).magnitude;
                    });
                }
            }

            internal void Ring(float cx, float cy, float radius, float width) =>
                Paint((x, y) => Band(Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)), radius, width * 0.5f));

            internal void Dot(float cx, float cy, float radius) =>
                Paint((x, y) => radius + 0.5f - Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)));

            internal void Star(float cx, float cy, float rx, float ry) =>
                Paint((x, y) =>
                {
                    float u = Mathf.Abs(x - cx) / rx, v = Mathf.Abs(y - cy) / ry;
                    float distance = Mathf.Sqrt(u) + Mathf.Sqrt(v);
                    return (1f - distance) * Mathf.Min(rx, ry) + 0.5f;
                });

            private void Paint(Func<float, float, float> coverage)
            {
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        int i = y * Size + x;
                        byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(coverage(x + 0.5f, y + 0.5f)) * 255f);
                        _pixels[i] = new Color32(255, 255, 255, Math.Max(alpha, _pixels[i].a));
                    }
            }

            internal Sprite ToSprite(string name) => MakeSprite(name, Size, Size, 0, _pixels);
        }
    }
}
