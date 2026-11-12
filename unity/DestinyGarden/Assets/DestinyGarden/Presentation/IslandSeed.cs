using System;
using System.Collections.Generic;
using UnityEngine;

namespace DestinyGarden.Presentation
{
    public struct Bump { public float X, Z, R, H; }
    public struct StoneSpot { public float Angle, Radius, Height, Width; }
    public struct TreeSpot { public float Angle, Radius, Size; public TreeForms.Kind Kind; }

    /// <summary>
    /// The character of one garden's island: where the highlands rise, how the hills lie, where the
    /// spring is and which way the river winds, where the standing stones, trees and the cottage
    /// stand, which plants this island favours, the colour of its meadow and the hour it begins.
    /// Derived only from the run id (saved in the journal), so a restored garden has the same island
    /// and every new visitor gets a different one. The 16x16 garden in the middle never changes.
    /// </summary>
    public sealed class IslandSeed
    {
        public int Value;
        public float StartPhase;
        public float RidgeAngle, WindAngle;
        public float HillAmp, RimAmp, BackAmp;
        public Bump[] Bumps;
        public Vector2[] River;
        public float[] Biome = new float[12];        // per IslandFlora kind
        public float[] StageJitter = new float[12];
        public float FloraOffset;
        public Color Meadow, MeadowLight, Moss;
        public StoneSpot[] Stones;
        public TreeSpot[] Trees;
        public float CottageAngle, CottageRadius;

        public static readonly Vector2 Center = new Vector2(7.5f, 7.5f);
        private static readonly float[] StartPhases = { 0.10f, 0.16f, 0.28f, 0.38f }; // a bright first impression: dawn-morning to noon

        public static int HashOf(string text)
        {
            uint h = 2166136261u;
            foreach (char ch in text ?? "") { h ^= ch; h *= 16777619u; }
            return (int)(h & 0x7fffffff);
        }

        public static IslandSeed FromRunId(string runId, int? forced = null) =>
            FromValue(forced ?? HashOf(runId ?? "garden"));

        public static Vector2 At(float angle, float radius) =>
            new Vector2(Center.x + Mathf.Cos(angle) * radius, Center.y + Mathf.Sin(angle) * radius);

        public static float DistanceToPath(Vector2[] path, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < path.Length; i++)
            {
                Vector2 a = path[i], b = path[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        public static IslandSeed FromValue(int value)
        {
            var r = new System.Random(value);
            float R() => (float)r.NextDouble();
            var s = new IslandSeed { Value = value };
            s.StartPhase = StartPhases[r.Next(StartPhases.Length)];
            s.RidgeAngle = R() * Mathf.PI * 2f;
            s.WindAngle = R() * Mathf.PI * 2f;
            s.HillAmp = 1.0f + 0.9f * R();
            s.RimAmp = 0.15f + 0.30f * R();
            s.BackAmp = 0.40f + 0.70f * R();
            s.FloraOffset = R() * 100f;

            // Hills and hollows in the outer land (never in the garden itself).
            int nb = 3 + r.Next(3);
            s.Bumps = new Bump[nb];
            for (int i = 0; i < nb; i++)
            {
                var p = At(R() * Mathf.PI * 2f, 7.6f + R() * 2.2f);
                bool hollow = R() < 0.2f;
                s.Bumps[i] = new Bump { X = p.x, Z = p.y, R = 1.6f + R() * 1.6f, H = hollow ? -(0.15f + R() * 0.25f) : 0.35f + R() * 0.9f };
            }

            // The river: a spring in the highlands, an arc around the garden, out to the sea.
            float a0 = s.RidgeAngle + (R() - 0.5f) * 0.9f;
            float dir = R() < 0.5f ? 1f : -1f;
            float sweep = (75f + R() * 80f) * Mathf.Deg2Rad;
            var river = new List<Vector2>();
            const int n = 9;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float rad = 8.3f - 1.1f * Mathf.Sin(Mathf.PI * t) + (R() - 0.5f) * 0.5f;
                river.Add(At(a0 + dir * sweep * t, Mathf.Max(7.0f, rad)));
            }
            river.Add(At(a0 + dir * (sweep + 0.12f), 12.9f));
            s.River = river.ToArray();

            // Which plants this island favours: two signature kinds, one it lacks.
            for (int k = 0; k < 12; k++) { s.Biome[k] = 0.6f + 0.8f * R(); s.StageJitter[k] = (R() - 0.5f) * 0.12f; }
            int[] signature = { 1, 4, 5, 9, 7, 8 }; // TallGrass, Fern, Reed, Wildflower, Thyme, Bluebell
            int a = signature[r.Next(signature.Length)], b = signature[r.Next(signature.Length)];
            s.Biome[a] *= 1.8f; s.Biome[b] *= 1.6f;
            s.Biome[signature[r.Next(signature.Length)]] *= 0.45f;

            // Meadow colour.
            switch (r.Next(5))
            {
                case 0: s.Meadow = new Color(0.44f, 0.64f, 0.44f); s.MeadowLight = new Color(0.62f, 0.78f, 0.52f); s.Moss = new Color(0.28f, 0.46f, 0.30f); break;
                case 1: s.Meadow = new Color(0.52f, 0.64f, 0.38f); s.MeadowLight = new Color(0.70f, 0.78f, 0.46f); s.Moss = new Color(0.36f, 0.46f, 0.26f); break;
                case 2: s.Meadow = new Color(0.40f, 0.62f, 0.50f); s.MeadowLight = new Color(0.58f, 0.78f, 0.60f); s.Moss = new Color(0.26f, 0.46f, 0.38f); break;
                case 3: s.Meadow = new Color(0.55f, 0.68f, 0.40f); s.MeadowLight = new Color(0.74f, 0.82f, 0.48f); s.Moss = new Color(0.38f, 0.50f, 0.28f); break;
                default: s.Meadow = new Color(0.36f, 0.56f, 0.40f); s.MeadowLight = new Color(0.52f, 0.70f, 0.48f); s.Moss = new Color(0.22f, 0.40f, 0.28f); break;
            }

            // Standing stones along the shore, taller toward the highlands.
            var stones = new List<StoneSpot>();
            int ns = 7 + r.Next(5);
            for (int tries = 0; tries < 80 && stones.Count < ns; tries++)
            {
                float ang = R() * Mathf.PI * 2f;
                bool tooClose = false;
                foreach (var o in stones) if (Mathf.Abs(Mathf.DeltaAngle(o.Angle * Mathf.Rad2Deg, ang * Mathf.Rad2Deg)) < 18f) tooClose = true;
                if (tooClose) continue;
                float ridge = Mathf.Max(0f, Mathf.Cos(ang - s.RidgeAngle));
                stones.Add(new StoneSpot { Angle = ang, Radius = 9.3f + R() * 1.6f, Height = 0.7f + R() * 1.6f + ridge * (0.8f + R() * 1.2f), Width = 0.95f + R() * 0.4f });
            }
            s.Stones = stones.ToArray();

            // Trees: never across the default front view, never on the river (except willows).
            float[] w = { 0.3f + R(), 0.1f + R() * 0.6f, 0.1f + R() * 0.6f, 0.1f + R() * 0.8f, 0.2f + R() * 0.6f };
            float wsum = 0; foreach (float x in w) wsum += x;
            var trees = new List<TreeSpot>();
            int nt = 11 + r.Next(6);
            for (int tries = 0; tries < 200 && trees.Count < nt; tries++)
            {
                float ang = R() * Mathf.PI * 2f, rad = 7.2f + R() * 1.7f;
                float deg = Mathf.Repeat(ang * Mathf.Rad2Deg, 360f);
                if (deg > 235f && deg < 305f) continue;
                var p = At(ang, rad);
                double pick = R() * wsum; int kind = 0; float acc = 0;
                for (; kind < w.Length; kind++) { acc += w[kind]; if (pick < acc) break; }
                if (kind >= w.Length) kind = w.Length - 1;
                var k = (TreeForms.Kind)kind;
                if (k != TreeForms.Kind.Willow && DistanceToPath(s.River, p) < 1.2f) continue;
                bool crowded = false;
                foreach (var o in trees) if (Vector2.Distance(At(o.Angle, o.Radius), p) < 1.5f) crowded = true;
                if (crowded) continue;
                trees.Add(new TreeSpot { Angle = ang, Radius = rad, Size = (k == TreeForms.Kind.Shrub ? 0.6f : 0.75f) + R() * 0.45f, Kind = k });
            }
            s.Trees = trees.ToArray();

            // The cottage: near the highlands, off the river.
            float ca = s.RidgeAngle + (R() - 0.5f) * 1.6f, cr = 7.8f + R() * 0.6f;
            for (int tries = 0; tries < 12 && DistanceToPath(s.River, At(ca, cr)) < 1.5f; tries++) ca += 0.3f;
            s.CottageAngle = ca; s.CottageRadius = cr;
            return s;
        }
    }
}
