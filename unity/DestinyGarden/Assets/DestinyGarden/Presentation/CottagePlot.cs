using UnityEngine;

namespace DestinyGarden.Presentation
{
    /// <summary>The house's local x/z footprint, including room for later additions.</summary>
    internal sealed class CottagePlot
    {
        private static readonly Vector2 Lower = new Vector2(-1.10f, -2.50f);
        private static readonly Vector2 Upper = new Vector2(2.30f, 0.92f);
        private static readonly float[] Radii = { 0f, 0.64f, -0.48f, 1.15f };
        private readonly Vector2 _outward, _right;

        internal Vector2 Center { get; }
        internal Vector3 Forward => new Vector3(_outward.x, 0f, _outward.y);
        internal float Floor { get; private set; }

        private CottagePlot(Vector2 center, Vector2 outward)
        {
            Center = center;
            _outward = outward;
            _right = new Vector2(_outward.y, -_outward.x);
        }

        private Vector2 At(float x, float z) => Center + _right * x + _outward * z;

        internal float Ground(float x, float z)
        {
            Vector2 point = At(x, z);
            return IslandTerrain.Height(point.x, point.y);
        }

        internal bool Overlaps(Vector2 world, float clearance)
        {
            Vector2 offset = world - Center;
            float x = Vector2.Dot(offset, _right);
            float z = Vector2.Dot(offset, _outward);
            return x > Lower.x - clearance && x < Upper.x + clearance &&
                   z > Lower.y - clearance && z < Upper.y + clearance;
        }

        internal static CottagePlot Find(IslandSeed seed, Vector2 altar)
        {
            CottagePlot best = null;
            float bestScore = float.NegativeInfinity;
            for (int turn = 0; turn < 48; turn++)
            {
                float angle = seed.CottageAngle + turn * (2f * Mathf.PI / 48f);
                var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                foreach (float offset in Radii)
                {
                    var plot = new CottagePlot(IslandSeed.At(angle, seed.CottageRadius + offset), outward);
                    float hazard = 0f;
                    float low = float.PositiveInfinity;
                    float high = float.NegativeInfinity;
                    float floor = float.NegativeInfinity;
                    for (int row = 0; row < 5; row++)
                    {
                        float z = Mathf.Lerp(Lower.y, Upper.y, row / 4f);
                        for (int col = 0; col < 5; col++)
                        {
                            float x = Mathf.Lerp(Lower.x, Upper.x, col / 4f);
                            Vector2 p = plot.At(x, z);
                            float coast = IslandTerrain.DistanceInland(p.x, p.y, out _);
                            float river = IslandTerrain.DistanceToRiver(p.x, p.y);
                            float bed = IslandTerrain.DistanceToBed(p.x, p.y);
                            float height = IslandTerrain.Height(p.x, p.y);
                            if (row >= 2)
                            {
                                low = Mathf.Min(low, height);
                                high = Mathf.Max(high, height);
                                floor = Mathf.Max(floor, height);
                            }
                            hazard += Mathf.Max(0f, 0.65f - coast) * 24f
                                + Mathf.Max(0f, 0.93f - river) * 18f
                                + Mathf.Max(0f, 0.68f - bed) * 21f
                                + Mathf.Max(0f, 1.16f - Vector2.Distance(p, altar)) * 21f;
                        }
                    }
                    // Check the platform and front stair at their real corners as well as on the grid.
                    for (int x = 0; x < 6; x++)
                        for (int z = 0; z < 4; z++)
                            floor = Mathf.Max(floor, plot.Ground(Mathf.Lerp(-0.88f, 2.14f, x / 5f),
                                Mathf.Lerp(-1.37f, 0.78f, z / 3f)));
                    float approach = plot.Ground(-0.23f, -1.95f);
                    float ridge = Mathf.Cos(angle - seed.CottageAngle);
                    float score = ridge * 2.3f - Mathf.Abs(offset) * 0.28f
                        - (high - low) * 3.2f - Mathf.Abs(floor - approach) * 3.6f - hazard;
                    if (score <= bestScore) continue;
                    plot.Floor = floor + 0.14f;
                    best = plot;
                    bestScore = score;
                }
            }
            return best;
        }
    }
}
