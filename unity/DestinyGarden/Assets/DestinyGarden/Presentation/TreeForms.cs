using System;
using UnityEngine;

namespace DestinyGarden.Presentation
{
    /// <summary>Seeded, local-space meshes for the five kinds of island tree and shrub.</summary>
    public static class TreeForms
    {
        public enum Kind { Broadleaf, Blossom, Willow, Conifer, Shrub }

        private const float Turn = 2f * Mathf.PI;
        private const float GoldenAngle = 2.399963f;
        private static readonly Color Bark = new Color(0.25f, 0.17f, 0.115f);
        private static readonly Color BarkLight = new Color(0.42f, 0.32f, 0.23f);
        private static readonly Color LeafShade = new Color(0.085f, 0.205f, 0.105f);
        private static readonly Color LeafSun = new Color(0.32f, 0.53f, 0.245f);

        public static FoliageMesh Build(Kind kind, int seed)
        {
            var mesh = new FoliageMesh { Root = new Vector4(0f, 0f, 0f, -1f) };
            var rng = new System.Random(seed);
            float Next() => (float)rng.NextDouble();
            switch (kind)
            {
                case Kind.Broadleaf: Deciduous(mesh, Next, false); break;
                case Kind.Blossom: Deciduous(mesh, Next, true); break;
                case Kind.Willow: Willow(mesh, Next); break;
                case Kind.Conifer: Conifer(mesh, Next); break;
                default: Shrub(mesh, Next); break;
            }
            return mesh;
        }

        private static Vector3 Radial(float angle) => new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        private static Vector3 Bend(Vector3 start, Vector3 control, Vector3 end, float t) =>
            Vector3.Lerp(Vector3.Lerp(start, control, t), Vector3.Lerp(control, end, t), t);

        private static void Stem(FoliageMesh mesh, Vector3 start, Vector3 control, Vector3 end, float width, float tip,
            float baseSway, float tipSway, int rings = 5, int sides = 6, float flare = 0f)
        {
            var points = new Vector3[rings];
            for (int i = 0; i < rings; i++) points[i] = Bend(start, control, end, i / (float)(rings - 1));
            mesh.Tube(points, width, tip, Bark, BarkLight, baseSway, tipSway, sides, flare);
        }

        private static void Roots(FoliageMesh mesh, Func<float> next, Vector3 trunk, float width)
        {
            float phase = next() * Turn;
            for (int i = 0; i < 4; i++)
            {
                var outwards = Radial(phase + i * Turn / 4f + (next() - 0.5f) * 0.62f);
                var tip = outwards * (width * (1.55f + next() * 1.7f)) + Vector3.down * 0.17f;
                Stem(mesh, trunk, tip * 0.48f + Vector3.up * 0.06f, tip, width * 0.52f, 0.010f, 0f, 0f, 4, 5);
            }
        }

        private static void Deciduous(FoliageMesh mesh, Func<float> next, bool blossom)
        {
            float phase = next() * Turn;
            float height = (blossom ? 1.69f : 1.98f) + next() * 0.32f;
            float width = 0.105f + next() * 0.036f;
            var basePoint = Vector3.down * 0.18f;
            var control = Radial(phase + 1.4f) * 0.20f + Vector3.up * (height * 0.48f);
            var top = Radial(phase) * (0.21f + next() * 0.20f) + Vector3.up * height;
            Stem(mesh, basePoint, control, top, width, 0.013f, 0f, 0.20f, 9, 10, 0.54f);
            Roots(mesh, next, Bend(basePoint, control, top, 0.13f), width);

            int count = 4 + (int)(next() * 2f);
            for (int i = 0; i < count; i++)
            {
                float level = (i + next() * 0.50f) / count;
                float angle = phase + i * GoldenAngle + (next() - 0.5f) * 0.52f;
                var dir = Radial(angle);
                var fork = Bend(basePoint, control, top, 0.44f + level * 0.35f);
                float reach = (blossom ? 0.93f : 0.82f) - level * 0.26f + next() * 0.22f;
                var elbow = fork + dir * (reach * 0.32f) + Vector3.up * 0.21f;
                var tip = fork + dir * reach + Vector3.up * (0.26f + level * 0.16f + next() * 0.12f);
                Stem(mesh, fork, elbow, tip, width * (0.59f - level * 0.23f), 0.013f, 0.08f, 0.29f);
                CrownEnd(mesh, next, tip, dir, 0.35f + next() * 0.12f, blossom);

                for (int k = 0; k < 2; k++)
                {
                    float split = 0.49f + k * 0.17f + next() * 0.12f;
                    var offshoot = Bend(fork, elbow, tip, split);
                    var spread = Radial(angle + (k == 0 ? -1f : 1f) * (0.65f + next() * 0.53f));
                    var branchEnd = offshoot + spread * (0.24f + next() * 0.22f) + Vector3.up * (0.13f + next() * 0.24f);
                    Stem(mesh, offshoot, offshoot + spread * 0.10f + Vector3.up * 0.13f, branchEnd,
                        0.027f, 0.007f, 0.16f, 0.39f, 4, 5);
                    CrownEnd(mesh, next, branchEnd, spread, 0.25f + next() * 0.11f, blossom);
                }
            }

            CrownEnd(mesh, next, top - Vector3.up * 0.06f, Radial(phase + 0.66f), 0.44f, blossom);
            CrownEnd(mesh, next, top + Radial(phase + 2.7f) * 0.21f + Vector3.up * 0.12f, Radial(phase + 2.7f), 0.29f, blossom);
        }

        private static void CrownEnd(FoliageMesh mesh, Func<float> next, Vector3 tip, Vector3 dir, float size, bool blossom)
        {
            var center = tip + dir * (size * 0.17f) + Vector3.up * (size * 0.16f);
            var spread = new Vector3(size, size * (blossom ? 0.46f : 0.61f), size * (0.67f + next() * 0.29f));
            var shade = blossom ? new Color(0.13f, 0.24f, 0.13f) : LeafShade;
            var sun = blossom ? new Color(0.28f, 0.45f, 0.22f) : LeafSun;
            LeafCluster(mesh, next, center, dir, spread, shade, sun, blossom ? 17 : 25, 0.30f, 0.46f);

            if (!blossom) return;
            float phase = next() * Turn;
            var side = Vector3.Cross(Vector3.up, dir);
            for (int i = 0; i < 7; i++)
            {
                float angle = phase + i * GoldenAngle;
                float spreadFactor = Mathf.Sqrt((i + next()) / 7f);
                var outward = dir * Mathf.Cos(angle) + side * Mathf.Sin(angle);
                var p = center + outward * (size * 0.78f * spreadFactor)
                    + Vector3.up * (size * Mathf.Lerp(0.43f, 0.06f, spreadFactor) + (next() - 0.5f) * size * 0.31f);
                var normal = (Vector3.up * (0.50f + next() * 0.70f) + outward * (0.43f + next() * 0.58f)).normalized;
                Bloom(mesh, p, normal, angle, size * (0.27f + next() * 0.21f), next());
            }
        }

        private static void Willow(FoliageMesh mesh, Func<float> next)
        {
            float phase = next() * Turn;
            float height = 1.64f + next() * 0.23f;
            var basePoint = Vector3.down * 0.17f;
            var bend = Radial(phase) * 0.13f + Vector3.up * 0.79f;
            var top = Radial(phase + 0.69f) * 0.36f + Vector3.up * height;
            Stem(mesh, basePoint, bend, top, 0.137f, 0.027f, 0f, 0.16f, 8, 10, 0.45f);
            Roots(mesh, next, Bend(basePoint, bend, top, 0.15f), 0.14f);
            var dark = new Color(0.105f, 0.245f, 0.12f);
            var light = new Color(0.31f, 0.49f, 0.21f);

            for (int i = 0; i < 6; i++)
            {
                float angle = phase + i * Turn / 6f + (next() - 0.5f) * 0.55f;
                var dir = Radial(angle);
                var fork = Bend(basePoint, bend, top, 0.63f + next() * 0.34f);
                float reach = 0.58f + next() * 0.45f;
                var arch = fork + dir * (reach * 0.42f) + Vector3.up * (0.56f + next() * 0.29f);
                var tip = fork + dir * reach + Vector3.up * (0.05f + next() * 0.27f);
                Stem(mesh, fork, arch, tip, 0.052f, 0.010f, 0.08f, 0.40f, 7);
                var crown = Bend(fork, arch, tip, 0.61f) + Vector3.up * 0.09f;
                LeafCluster(mesh, next, crown, dir, new Vector3(0.37f, 0.23f, 0.28f), dark, light, 26, 0.18f, 0.43f);

                for (int j = 0; j < 6; j++)
                {
                    float t = 0.45f + (j + next() * 0.70f) / 11f;
                    var start = Bend(fork, arch, tip, t);
                    float fanAngle = angle + (j % 2 == 0 ? -1f : 1f) * (0.18f + next() * 0.92f);
                    var fan = Radial(fanAngle);
                    float fall = Mathf.Min(start.y - 0.20f, 0.60f + next() * 0.82f);
                    var curve = start + fan * (0.20f + next() * 0.18f) + Vector3.up * 0.09f;
                    var end = start + fan * (0.27f + next() * 0.14f) - Vector3.up * fall;
                    Stem(mesh, start, curve, end, 0.011f, 0.0025f, 0.26f, 0.91f, 6, 4);
                    var side = Vector3.Cross(Vector3.up, fan);
                    for (int k = 0; k < 12; k++)
                    {
                        float u = (k + 0.45f) / 12f;
                        var p = Bend(start, curve, end, u);
                        var leafDir = fan * (0.30f + next() * 0.47f)
                            + side * ((k % 2 == 0 ? 1f : -1f) * (0.48f + next() * 0.54f))
                            + Vector3.down * (0.26f + u * 0.63f);
                        float len = (0.13f + next() * 0.13f) * (1f - u * 0.36f);
                        var tone = Color.Lerp(dark, light, 0.24f + next() * 0.60f);
                        FoldedLeaf(mesh, p, leafDir, len, len * 0.16f, tone, Mathf.Lerp(0.27f, 0.95f, u), next() - 0.5f);
                    }
                }
            }

            LeafCluster(mesh, next, top + Vector3.up * 0.14f, Radial(phase),
                new Vector3(0.35f, 0.22f, 0.29f), dark, light, 24, 0.20f, 0.31f);
        }

        private static void Conifer(FoliageMesh mesh, Func<float> next)
        {
            float phase = next() * Turn;
            float height = 2.12f + next() * 0.38f;
            var basePoint = Vector3.down * 0.14f;
            var bend = Radial(phase + 1.5f) * 0.10f + Vector3.up * (height * 0.48f);
            var top = Radial(phase) * (0.14f + next() * 0.20f) + Vector3.up * height;
            Stem(mesh, basePoint, bend, top, 0.085f, 0.006f, 0f, 0.37f, 9, 8, 0.32f);
            var shade = new Color(0.065f, 0.19f, 0.128f);
            var sun = new Color(0.22f, 0.41f, 0.25f);
            for (int tier = 0; tier < 8; tier++)
            {
                float t = tier / 7f;
                int branches = 6 - tier / 2;
                for (int i = 0; i < branches; i++)
                {
                    float a = phase + i * Turn / branches + tier * 1.37f + (next() - 0.5f) * 0.62f;
                    var dir = Radial(a);
                    var side = Vector3.Cross(Vector3.up, dir);
                    var fork = Bend(basePoint, bend, top, Mathf.Lerp(0.24f, 0.98f, t) + (next() - 0.5f) * 0.063f);
                    float span = Mathf.Lerp(0.74f, 0.12f, t) * (0.75f + next() * 0.50f);
                    var elbow = fork + dir * (span * 0.43f) - Vector3.up * (span * 0.24f);
                    var tip = fork + dir * span + Vector3.up * (span * (0.03f + next() * 0.15f));
                    Stem(mesh, fork, elbow, tip, 0.008f + span * 0.017f, 0.0025f, 0.11f, 0.48f, 4, 5);

                    for (int k = 0; k < 7; k++)
                    {
                        float u = (k + 0.4f) / 7f;
                        var p = Bend(fork, elbow, tip, u);
                        float len = (0.15f + span * (0.26f + next() * 0.19f)) * (1f - u * 0.34f);
                        var fan = dir * 0.59f + side * ((k % 2 == 0 ? 1f : -1f) * (0.34f + next() * 0.65f))
                            + Vector3.up * (0.12f + next() * 0.38f);
                        var color = Color.Lerp(shade, sun, 0.20f + t * 0.19f + u * 0.23f + next() * 0.37f);
                        float sway = 0.28f + u * 0.32f;
                        FoldedLeaf(mesh, p, fan, len, len * 0.32f, color, sway, (next() - 0.5f) * 0.91f);
                        FoldedLeaf(mesh, p + fan * (len * 0.15f), fan + Vector3.up * 0.61f,
                            len * 0.71f, len * 0.15f, color * 1.06f, sway, 0.24f);
                        FoldedLeaf(mesh, p, dir * 0.42f + fan * 0.49f - Vector3.up * 0.23f,
                            len * 0.85f, len * 0.21f, color * 0.78f, sway, -0.31f);
                    }
                }
            }
            LeafCluster(mesh, next, top - Vector3.up * 0.045f, Radial(phase), new Vector3(0.13f, 0.16f, 0.12f),
                shade, sun, 18, 0.22f, 0.49f);
        }

        private static void Shrub(FoliageMesh mesh, Func<float> next)
        {
            float phase = next() * Turn;
            var shade = new Color(0.10f, 0.25f, 0.12f);
            var sun = new Color(0.36f, 0.52f, 0.25f);
            for (int i = 0; i < 5; i++)
            {
                var dir = Radial(phase + i * GoldenAngle + next() * 0.4f);
                var tip = dir * (0.17f + next() * 0.27f) + Vector3.up * (0.21f + next() * 0.30f);
                Stem(mesh, Vector3.down * 0.08f, dir * 0.10f + Vector3.up * 0.19f, tip, 0.028f, 0.006f, 0f, 0.27f, 4, 5);
                LeafCluster(mesh, next, tip, dir, new Vector3(0.30f, 0.20f, 0.24f),
                    shade, sun, 24, 0.38f, 0.46f);
            }
        }

        /// <summary>Overlapping leaves around a branch tip. Their placement leaves gaps between boughs.</summary>
        private static void LeafCluster(FoliageMesh mesh, Func<float> next, Vector3 center, Vector3 dir, Vector3 spread,
            Color shade, Color sun, int count, float width, float sway)
        {
            var side = Vector3.Cross(Vector3.up, dir);
            float phase = next() * Turn;
            for (int i = 0; i < count; i++)
            {
                float a = phase + i * GoldenAngle + (next() - 0.5f) * 0.50f;
                float y = Mathf.Lerp(0.96f, -0.78f, (i + next()) / count);
                float radial = Mathf.Sqrt(1f - y * y) * (0.52f + next() * 0.42f);
                float x = Mathf.Cos(a), z = Mathf.Sin(a);
                var outward = dir * x + side * z;
                var p = center + (dir * (x * spread.x) + side * (z * spread.z)) * radial + Vector3.up * (spread.y * y);
                var forward = outward + dir * 0.30f + Vector3.up * (-0.42f + next() * 1.03f);
                float len = spread.x * (0.54f + next() * 0.64f);
                var color = Color.Lerp(shade, sun, 0.34f + y * 0.32f + next() * 0.34f);
                FoldedLeaf(mesh, p - forward * (len * 0.24f), forward, len, len * width * (0.76f + next() * 0.43f),
                    color, sway + next() * 0.18f, (next() - 0.5f) * 1.23f);
            }
        }

        /// <summary>Seven vertices give the leaf a curved midrib and a tapered, asymmetric edge.</summary>
        private static void FoldedLeaf(FoliageMesh mesh, Vector3 root, Vector3 forward, float length, float width,
            Color color, float sway, float twist)
        {
            var axis = forward.normalized;
            var side = Vector3.Cross(Vector3.up, axis).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            var normal = Vector3.Cross(axis, side);
            side = (side + normal * twist).normalized;
            normal = Vector3.Cross(axis, side);
            var near = root + axis * (length * 0.41f) + normal * (length * 0.10f);
            var far = root + axis * (length * 0.74f) + normal * (length * 0.15f);
            var left = (normal - side * 0.38f).normalized;
            var right = (normal + side * 0.38f).normalized;
            int a = mesh.Add(root, normal, Tint(color, 0.72f, sway - 0.06f));
            int b = mesh.Add(near - side * width, left, Tint(color, 0.95f, sway));
            int c = mesh.Add(near + normal * (length * 0.067f), normal, Tint(color, 1.09f, sway));
            int d = mesh.Add(near + side * (width * 0.93f), right, Tint(color, 0.85f, sway));
            int e = mesh.Add(far - side * (width * 0.59f), left, Tint(color, 1.03f, sway + 0.035f));
            int f = mesh.Add(far + side * (width * 0.64f), right, Tint(color, 0.96f, sway + 0.035f));
            int g = mesh.Add(root + axis * length + normal * (length * 0.075f), normal, Tint(color, 1.11f, sway + 0.07f));
            Triangle(mesh, a, b, c);
            Triangle(mesh, a, c, d);
            Triangle(mesh, b, e, c);
            Triangle(mesh, c, e, g);
            Triangle(mesh, c, g, f);
            Triangle(mesh, c, f, d);
        }

        private static void Bloom(FoliageMesh mesh, Vector3 center, Vector3 normal, float phase, float radius, float tone)
        {
            var x = Vector3.Cross(normal, Vector3.forward).normalized;
            var y = Vector3.Cross(x, normal);
            var pink = Color.Lerp(new Color(0.64f, 0.29f, 0.36f), new Color(0.89f, 0.67f, 0.69f), tone);
            var pale = Color.Lerp(new Color(0.94f, 0.64f, 0.68f), new Color(0.96f, 0.86f, 0.80f), tone);
            int root = mesh.Add(center, normal, Tint(pink, 0.86f, 0.61f));
            for (int i = 0; i < 5; i++)
            {
                float a = phase + i * Turn / 5f;
                var outwards = x * Mathf.Cos(a) + y * Mathf.Sin(a);
                var side = Vector3.Cross(normal, outwards);
                var tipNormal = (normal - outwards * 0.20f).normalized;
                var shoulder = center + (outwards * 0.69f + normal * 0.12f) * radius;
                int left = mesh.Add(shoulder - side * (radius * 0.40f), tipNormal, Tint(pink, 1.07f, 0.64f));
                int tip = mesh.Add(center + (outwards + normal * 0.25f) * radius, tipNormal, Tint(pale, 1f, 0.67f));
                int right = mesh.Add(shoulder + side * (radius * 0.40f), tipNormal, Tint(pale, 0.95f, 0.64f));
                Triangle(mesh, root, left, tip);
                Triangle(mesh, root, tip, right);
            }
        }

        private static Color Tint(Color color, float brightness, float sway)
        {
            color *= brightness;
            color.a = Mathf.Clamp01(sway);
            return color;
        }

        private static void Triangle(FoliageMesh mesh, int a, int b, int c)
        {
            mesh.T.Add(a);
            mesh.T.Add(b);
            mesh.T.Add(c);
        }
    }
}
