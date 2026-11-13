using DestinyGarden.Domain;
using UnityEngine;
using BladeShape = DestinyGarden.Presentation.FoliageMesh.BladeShape;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// Garden plant forms (presentation only). Twenty-four species with their own sprout, leaves,
    /// bud and bloom. The species follows where the plant stands (sun, rock or tree shade,
    /// river bank, high ground) and who sowed it: the gardener's die and the first garden, or
    /// the garden's small lives (OriginKind "ecology"), so the ecosystem's own choices show.
    /// </summary>
    public static class PlantForms
    {
        public enum Species
        {
            Cosmos, Daisy, Bellflower, Poppy, Sunflower, Foxglove, Anemone, Iris,
            ForgetMeNot, Buttercup, Lavender, WildRose, Dandelion, Strawberry, Lily,
            Clover, Vetch, Yarrow, Thyme, Mint, Primrose, Violet, Loosestrife, Goldenrod
        }

        private enum Leaves { Strap, Rosette, Heart, Round, Fine, Cut, Compound, Whorled, Trifoliate }
        private enum Bloom { Radial, Bells, Spike, Iris, Cluster, Pom, Trumpet, Berry }

        private struct Profile
        {
            public float Height;
            public Leaves Leaf;
            public Bloom Shape;
            public Color Stem, Foliage, Bud, PetalBase, PetalTip, Center;
            public int Petals;
            public float PetalLen, PetalWid, Lift, Cup, CenterSize;
        }

        private static Profile P(float h, Leaves leaf, Bloom shape, Color foliage, Color bud, Color pb, Color pt, Color center,
            int petals = 6, float len = 0.14f, float wid = 0.05f, float lift = 0.35f, float cup = 0.4f, float centerSize = 0.035f) =>
            new Profile
            {
                Height = h, Leaf = leaf, Shape = shape, Stem = new Color(0.24f, 0.42f, 0.22f), Foliage = foliage, Bud = bud,
                PetalBase = pb, PetalTip = pt, Center = center, Petals = petals, PetalLen = len, PetalWid = wid,
                Lift = lift, Cup = cup, CenterSize = centerSize
            };

        private static readonly Color Green = new Color(0.30f, 0.56f, 0.30f);
        private static readonly Color GreyGreen = new Color(0.44f, 0.56f, 0.44f);
        private static readonly Color DeepGreen = new Color(0.20f, 0.44f, 0.26f);
        private static readonly Color Gold = new Color(1f, 0.80f, 0.26f);

        private static readonly Profile[] Profiles =
        {
            P(0.76f, Leaves.Cut, Bloom.Radial, Green, new Color(0.63f, 0.23f, 0.36f), new Color(0.64f, 0.17f, 0.36f), new Color(1f, 0.68f, 0.82f), Gold, 8, 0.218f, 0.082f, 0.07f, 0.21f, 0.045f),
            P(0.58f, Leaves.Rosette, Bloom.Radial, Green, new Color(0.69f, 0.75f, 0.45f), new Color(0.78f, 0.77f, 0.64f), new Color(1f, 0.99f, 0.94f), Gold, 19, 0.165f, 0.028f, -0.14f, 0.17f, 0.056f),
            P(0.69f, Leaves.Heart, Bloom.Bells, DeepGreen, new Color(0.45f, 0.40f, 0.68f), new Color(0.33f, 0.31f, 0.66f), new Color(0.74f, 0.64f, 0.96f), new Color(0.88f, 0.82f, 0.92f)),
            P(0.65f, Leaves.Whorled, Bloom.Radial, GreyGreen, new Color(0.44f, 0.49f, 0.25f), new Color(0.47f, 0.044f, 0.088f), new Color(1f, 0.36f, 0.27f), new Color(0.16f, 0.076f, 0.11f), 4, 0.220f, 0.163f, 0.27f, 0.38f, 0.053f),
            P(1.06f, Leaves.Heart, Bloom.Radial, Green, new Color(0.36f, 0.52f, 0.26f), new Color(0.92f, 0.53f, 0.077f), new Color(1f, 0.88f, 0.32f), new Color(0.35f, 0.19f, 0.077f), 20, 0.185f, 0.038f, -0.10f, 0.18f, 0.103f),
            P(0.99f, Leaves.Rosette, Bloom.Spike, DeepGreen, new Color(0.69f, 0.37f, 0.62f), new Color(0.69f, 0.24f, 0.53f), new Color(1f, 0.72f, 0.86f), new Color(0.90f, 0.72f, 0.82f)),
            P(0.53f, Leaves.Whorled, Bloom.Radial, DeepGreen, new Color(0.76f, 0.74f, 0.90f), new Color(0.55f, 0.49f, 0.78f), new Color(0.96f, 0.91f, 1f), new Color(0.16f, 0.12f, 0.30f), 7, 0.161f, 0.073f, 0.13f, 0.16f, 0.045f),
            P(0.80f, Leaves.Strap, Bloom.Iris, Green, new Color(0.50f, 0.38f, 0.80f), new Color(0.44f, 0.32f, 0.82f), new Color(0.76f, 0.66f, 1f), new Color(1f, 0.86f, 0.30f)),
            P(0.39f, Leaves.Round, Bloom.Cluster, Green, new Color(0.54f, 0.64f, 0.86f), new Color(0.31f, 0.52f, 0.89f), new Color(0.61f, 0.83f, 1f), new Color(1f, 0.92f, 0.40f)),
            P(0.43f, Leaves.Whorled, Bloom.Radial, Green, new Color(0.69f, 0.71f, 0.30f), new Color(0.98f, 0.67f, 0.096f), new Color(1f, 0.91f, 0.38f), new Color(0.94f, 0.66f, 0.10f), 5, 0.132f, 0.079f, 0.34f, 0.32f, 0.031f),
            P(0.70f, Leaves.Fine, Bloom.Spike, GreyGreen, new Color(0.62f, 0.50f, 0.86f), new Color(0.60f, 0.46f, 0.90f), new Color(0.80f, 0.68f, 1f), new Color(0.70f, 0.56f, 0.94f)),
            P(0.62f, Leaves.Compound, Bloom.Radial, DeepGreen, new Color(0.78f, 0.31f, 0.43f), new Color(0.89f, 0.37f, 0.51f), new Color(1f, 0.82f, 0.81f), Gold, 5, 0.190f, 0.116f, 0.19f, 0.33f, 0.046f),
            P(0.33f, Leaves.Rosette, Bloom.Pom, Green, new Color(0.62f, 0.66f, 0.30f), new Color(0.91f, 0.60f, 0.081f), new Color(1f, 0.90f, 0.31f), new Color(0.96f, 0.72f, 0.10f)),
            P(0.31f, Leaves.Trifoliate, Bloom.Berry, DeepGreen, new Color(0.80f, 0.85f, 0.67f), new Color(0.89f, 0.88f, 0.75f), new Color(1f, 0.98f, 0.94f), new Color(0.80f, 0.10f, 0.13f), 5, 0.074f, 0.044f, 0.10f, 0.18f, 0.018f),
            P(0.84f, Leaves.Whorled, Bloom.Trumpet, Green, new Color(0.78f, 0.65f, 0.54f), new Color(0.88f, 0.49f, 0.58f), new Color(1f, 0.94f, 0.84f), new Color(0.69f, 0.38f, 0.15f), 6, 0.228f, 0.074f, -0.52f, 0.30f, 0.027f),
            P(0.28f, Leaves.Round, Bloom.Pom, Green, new Color(0.80f, 0.84f, 0.70f), new Color(0.96f, 0.95f, 0.84f), new Color(1f, 0.84f, 0.90f), Gold, 10, 0.04f, 0.02f),
            P(0.55f, Leaves.Compound, Bloom.Spike, Green, new Color(0.64f, 0.32f, 0.70f), new Color(0.58f, 0.26f, 0.72f), new Color(0.90f, 0.66f, 0.94f), Gold),
            P(0.66f, Leaves.Fine, Bloom.Cluster, GreyGreen, new Color(0.82f, 0.82f, 0.72f), new Color(1f, 0.94f, 0.80f), new Color(1f, 1f, 0.94f), Gold),
            P(0.22f, Leaves.Round, Bloom.Cluster, GreyGreen, new Color(0.76f, 0.48f, 0.76f), new Color(0.84f, 0.55f, 0.82f), new Color(0.96f, 0.78f, 0.94f), Gold),
            P(0.58f, Leaves.Compound, Bloom.Spike, DeepGreen, new Color(0.68f, 0.72f, 0.80f), new Color(0.80f, 0.78f, 0.94f), new Color(0.96f, 0.94f, 1f), Gold),
            P(0.35f, Leaves.Rosette, Bloom.Radial, Green, new Color(0.88f, 0.48f, 0.66f), new Color(0.94f, 0.54f, 0.74f), new Color(1f, 0.82f, 0.88f), Gold, 5, 0.10f, 0.07f),
            P(0.25f, Leaves.Heart, Bloom.Radial, DeepGreen, new Color(0.48f, 0.38f, 0.76f), new Color(0.42f, 0.32f, 0.72f), new Color(0.78f, 0.70f, 0.98f), Gold, 5, 0.075f, 0.05f),
            P(0.90f, Leaves.Compound, Bloom.Spike, Green, new Color(0.82f, 0.34f, 0.68f), new Color(0.84f, 0.34f, 0.72f), new Color(1f, 0.68f, 0.88f), Gold),
            P(0.92f, Leaves.Compound, Bloom.Cluster, Green, new Color(0.88f, 0.74f, 0.26f), new Color(1f, 0.78f, 0.16f), new Color(1f, 0.94f, 0.52f), Gold),
        };

        /// <summary>The colour a flower of this saved species shows when it opens (for the opening burst).</summary>
        public static Color BloomColor(string speciesId)
        {
            if (speciesId != null && ById.TryGetValue(speciesId, out var sp))
            {
                var pr = Profiles[(int)sp];
                return Color.Lerp(pr.PetalBase, pr.PetalTip, 0.5f);
            }
            return new Color(1f, 0.9f, 0.7f);
        }

        public static string LabelJa(Species s)
        {
            int i = (int)s;
            return i >= 0 && i < Flora.All.Length ? Flora.All[i].LabelJa : "花";
        }

        private static uint Hash(string id)
        {
            uint h = 2166136261u;
            foreach (char ch in id ?? "") { h ^= ch; h *= 16777619u; }
            return h;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Species> ById =
            new System.Collections.Generic.Dictionary<string, Species>
            {
                ["cosmos"] = Species.Cosmos, ["daisy"] = Species.Daisy, ["bellflower"] = Species.Bellflower,
                ["poppy"] = Species.Poppy, ["sunflower"] = Species.Sunflower, ["foxglove"] = Species.Foxglove,
                ["anemone"] = Species.Anemone, ["iris"] = Species.Iris, ["forgetmenot"] = Species.ForgetMeNot,
                ["buttercup"] = Species.Buttercup, ["lavender"] = Species.Lavender, ["wildrose"] = Species.WildRose,
                ["dandelion"] = Species.Dandelion, ["strawberry"] = Species.Strawberry, ["lily"] = Species.Lily,
                ["clover"] = Species.Clover, ["vetch"] = Species.Vetch, ["yarrow"] = Species.Yarrow,
                ["thyme"] = Species.Thyme, ["mint"] = Species.Mint, ["primrose"] = Species.Primrose,
                ["violet"] = Species.Violet, ["loosestrife"] = Species.Loosestrife, ["goldenrod"] = Species.Goldenrod
            };

        /// <summary>The saved species (ecology.v3); before the first ecology step, a stand-in from the place.</summary>
        public static Species Choose(PlantState plant, Vector3 at)
        {
            if (plant.Species != null && ById.TryGetValue(plant.Species, out var saved)) return saved;
            float shade = IslandTerrain.ShadeAt(at.x, at.z);
            float bank = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.8f, 2.4f, IslandTerrain.DistanceToRiver(at.x, at.z)));
            float high = Mathf.InverseLerp(0.35f, 1.15f, at.y);
            bool wild = plant.OriginKind == "ecology";
            Species[] pool;
            if (wild)
                pool = shade > 0.35f ? new[] { Species.Strawberry, Species.ForgetMeNot, Species.Bellflower }
                    : bank > 0.4f ? new[] { Species.ForgetMeNot, Species.Buttercup, Species.Iris }
                    : high > 0.6f ? new[] { Species.Lavender, Species.WildRose, Species.Dandelion }
                    : new[] { Species.Buttercup, Species.Dandelion, Species.WildRose, Species.Lavender, Species.Strawberry };
            else
                pool = shade > 0.35f ? new[] { Species.Bellflower, Species.Foxglove, Species.Anemone }
                    : bank > 0.4f ? new[] { Species.Iris, Species.ForgetMeNot, Species.Lily }
                    : high > 0.6f ? new[] { Species.Sunflower, Species.Poppy, Species.Cosmos, Species.Daisy }
                    : new[] { Species.Cosmos, Species.Daisy, Species.Poppy, Species.Anemone, Species.Lily };
            return pool[Hash(plant.PlantId) % (uint)pool.Length];
        }

        internal const float DisplayScale = 1.75f;
        private const float Turn = 2f * Mathf.PI;
        private const float GoldenAngle = 2.399963f;

        private struct PlantPose
        {
            public Vector3 Root, Lean;
            public float Height, Yaw;

            public Vector3 At(float t) => Root + Vector3.up * (Height * t) + Lean * (Height * t * t);
        }

        private static PlantPose Pose(PlantState plant, Vector3 root, Profile pr)
        {
            uint id = Hash(plant.PlantId);
            float g = Mathf.Clamp01(plant.Growth / 2500f);
            float yaw = Unit(id, 1) * Turn;
            float mature = pr.Height * Mathf.Lerp(0.90f, 1.10f, Unit(id, 5));
            float height = 0.017f;
            switch (plant.Stage)
            {
                case PlantStage.Sprout: height = Mathf.Lerp(0.12f, 0.20f, g * 2.7f); break;
                case PlantStage.Leaf: height = mature * Mathf.Lerp(0.46f, 0.73f, g); break;
                case PlantStage.Bud: height = mature * Mathf.Lerp(0.68f, 0.97f, g); break;
                case PlantStage.Flower: height = mature; break;
            }
            return new PlantPose
            {
                Root = root, Yaw = yaw, Height = height,
                Lean = Radial(yaw + 1.9f) * Mathf.Lerp(0.047f, 0.16f, Unit(id, 9))
            };
        }

        private static float Unit(uint id, uint salt)
        {
            uint n = unchecked((id ^ (salt * 2246822519u)) * 3266489917u);
            n ^= n >> 16;
            return (n & 0xffffu) / 65535f;
        }

        private static Vector3 Radial(float a) => new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

        internal static Vector3 TipPosition(PlantState plant, Vector3 root, Species species)
        {
            var pose = Pose(plant, root, Profiles[(int)species]);
            return root + (pose.At(1f) - root) * DisplayScale;
        }

        public static void Build(FoliageMesh b, PlantState plant, Vector3 root, Species species)
        {
            var pr = Profiles[(int)species];
            var pose = Pose(plant, root, pr);
            uint seed = Hash(plant.PlantId);
            var head = pose.At(1f);
            var forward = Radial(pose.Yaw);
            if (plant.Stage == PlantStage.Seed)
            {
                float length = Mathf.Lerp(0.079f, 0.104f, Unit(seed, 8));
                var husk = Color.Lerp(new Color(0.23f, 0.124f, 0.06f), new Color(0.47f, 0.29f, 0.14f), Unit(seed, 13));
                b.CurvedBlade(head - forward * (length * 0.5f), forward + Vector3.up * 0.10f, Vector3.up,
                    length, length * 0.23f, 0.05f, -0.55f, BladeShape.Lance, husk * 0.66f, husk, 0f);
                return;
            }

            float width = plant.Stage == PlantStage.Sprout ? 0.007f : (species == Species.Sunflower ? 0.026f : 0.013f);
            b.Tube(new[] { pose.At(0f), pose.At(0.24f), pose.At(0.52f), pose.At(0.76f), head },
                width, width * 0.47f, pr.Stem * 0.75f, pr.Foliage * 0.84f, 0f, 0.74f, 5);
            if (plant.Stage == PlantStage.Sprout)
            {
                var green = Color.Lerp(pr.Foliage, new Color(0.66f, 0.78f, 0.32f), 0.32f);
                LeafBlade(b, head, forward + Vector3.up * 0.40f, 0.118f, 0.036f, green, BladeShape.Oval, 0.76f);
                LeafBlade(b, head, -forward + Vector3.up * 0.55f, 0.100f, 0.033f, green * 0.90f, BladeShape.Oval, 0.76f);
                return;
            }

            BuildLeaves(b, pr, species, pose, seed, plant.Stage == PlantStage.Leaf);
            if (plant.Stage == PlantStage.Leaf) return;
            if (plant.Stage == PlantStage.Bud)
            {
                BuildBud(b, pr, species, head, (Vector3.up + pose.Lean * 0.8f).normalized, pose.Yaw);
                return;
            }
            BuildBloom(b, pr, species, pose, seed);
        }

        private static void LeafBlade(FoliageMesh b, Vector3 p, Vector3 direction, float length, float width, Color color,
            BladeShape shape, float sway, float bend = -0.18f)
        {
            b.CurvedBlade(p, direction, Vector3.up, length, width, bend, -0.29f, shape,
                color * 0.66f, color * 1.12f, sway);
        }

        private static void Stalk(FoliageMesh b, Vector3 start, Vector3 end, float radius, Color color, float sway)
        {
            b.Tube(new[] { start, Vector3.Lerp(start, end, 0.52f) + Vector3.up * radius, end },
                radius, radius * 0.66f, color * 0.83f, color, sway * 0.77f, sway, 5);
        }

        private static void BuildLeaves(FoliageMesh b, Profile pr, Species species, PlantPose pose, uint seed, bool young)
        {
            float scale = young ? 0.81f : 1f;
            float yaw = pose.Yaw;
            switch (pr.Leaf)
            {
                case Leaves.Strap:
                    for (int i = 0; i < 6; i++)
                    {
                        var outwards = Radial(yaw + Mathf.PI / 2f + (i - 2.5f) * 0.48f);
                        var dir = outwards * (0.26f + Mathf.Abs(i - 2.5f) * 0.14f) + Vector3.up * 0.83f;
                        var p = pose.Root + outwards * 0.023f + Vector3.up * 0.006f;
                        float len = (0.40f + Unit(seed, (uint)(27 + i)) * 0.27f) * scale;
                        LeafBlade(b, p, dir, len, 0.032f * scale, pr.Foliage * (0.78f + i * 0.055f),
                            BladeShape.Lance, 0.54f + 0.055f * i, -0.15f);
                    }
                    break;
                case Leaves.Rosette:
                    bool dandelion = species == Species.Dandelion;
                    for (int i = 0; i < 7; i++)
                    {
                        var dir = Radial(yaw + i * GoldenAngle + 0.33f * Unit(seed, (uint)(22 + i)));
                        float len = (dandelion ? 0.29f : 0.245f) + Unit(seed, (uint)(33 + i)) * 0.09f;
                        var p = pose.At(0.05f + i * 0.007f) + dir * 0.017f;
                        LeafBlade(b, p, dir + Vector3.up * (0.28f + i * 0.035f), len * scale,
                            (dandelion ? 0.064f : 0.055f) * scale, pr.Foliage * (0.80f + 0.05f * i),
                            dandelion ? BladeShape.Lobed : BladeShape.Toothed, 0.35f + i * 0.050f);
                    }
                    break;
                case Leaves.Trifoliate:
                    for (int i = 0; i < 4; i++)
                    {
                        var dir = Radial(yaw + i * GoldenAngle);
                        var p = pose.Root + dir * (0.127f * scale) + Vector3.up * (0.07f + i * 0.024f);
                        Stalk(b, pose.At(0.10f), p, 0.005f, pr.Stem, 0.50f);
                        for (int j = -1; j <= 1; j++)
                            LeafBlade(b, p, Radial(yaw + i * GoldenAngle + j * 0.89f) + Vector3.up * 0.25f,
                                (j == 0 ? 0.150f : 0.118f) * scale, 0.044f * scale,
                                pr.Foliage * (j == 0 ? 1.12f : 0.93f), BladeShape.Toothed, 0.66f);
                    }
                    break;
                case Leaves.Cut:
                case Leaves.Compound:
                    bool fine = pr.Leaf == Leaves.Cut;
                    for (int i = 0; i < 5; i++)
                    {
                        float t = 0.19f + i * 0.135f;
                        var dir = Radial(yaw + i * GoldenAngle);
                        var start = pose.At(t);
                        var tip = start + (dir + Vector3.up * 0.31f) * ((0.247f - i * 0.020f) * scale);
                        Stalk(b, start, tip, fine ? 0.0036f : 0.005f, pr.Stem, t + 0.14f);
                        var cross = Vector3.Cross(Vector3.up, dir);
                        int pairs = fine ? 5 : 2;
                        for (int j = 0; j < pairs; j++)
                        {
                            float along = Mathf.Lerp(0.29f, 0.86f, (j + 0.2f) / pairs);
                            var p = Vector3.Lerp(start, tip, along);
                            for (int sign = -1; sign <= 1; sign += 2)
                            {
                                var outward = dir * 0.53f + cross * sign + Vector3.up * 0.31f;
                                float len = Mathf.Lerp(fine ? 0.084f : 0.112f, fine ? 0.041f : 0.074f, along);
                                LeafBlade(b, p, outward, len * scale, (fine ? 0.008f : 0.030f) * scale, pr.Foliage,
                                    fine ? BladeShape.Lance : BladeShape.Toothed, t + 0.16f);
                                if (fine && j < 3)
                                    LeafBlade(b, p + outward.normalized * (len * 0.36f),
                                        dir * 0.85f + cross * (sign * 0.47f) + Vector3.up * 0.44f,
                                        len * 0.57f * scale, 0.006f * scale, pr.Foliage * 1.06f, BladeShape.Lance, t + 0.20f);
                            }
                        }
                        LeafBlade(b, Vector3.Lerp(start, tip, 0.93f), dir + Vector3.up * 0.26f,
                            (fine ? 0.053f : 0.088f) * scale, (fine ? 0.008f : 0.027f) * scale,
                            pr.Foliage, fine ? BladeShape.Lance : BladeShape.Toothed, t + 0.18f);
                    }
                    break;
                default:
                    BuildStemLeaves(b, pr, species, pose, seed, scale);
                    break;
            }
        }

        private static void BuildStemLeaves(FoliageMesh b, Profile pr, Species species, PlantPose pose, uint seed, float scale)
        {
            int count = pr.Leaf == Leaves.Fine || species == Species.Lily ? 7 : 5;
            float width = 0.054f, length = 0.21f;
            BladeShape shape = BladeShape.Oval;
            switch (pr.Leaf)
            {
                case Leaves.Heart:
                    width = species == Species.Sunflower ? 0.116f : 0.083f;
                    length = species == Species.Sunflower ? 0.30f : 0.236f;
                    shape = BladeShape.Heart;
                    break;
                case Leaves.Fine: width = 0.018f; length = 0.18f; shape = BladeShape.Lance; break;
                case Leaves.Round: width = 0.055f; length = 0.137f; break;
                case Leaves.Whorled:
                    width = species == Species.Lily ? 0.031f : 0.061f;
                    length = species == Species.Lily ? 0.225f : 0.201f;
                    shape = species == Species.Lily ? BladeShape.Lance : BladeShape.Lobed;
                    break;
            }
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float level = Mathf.Lerp(0.13f, 0.73f, t);
                if (species == Species.Buttercup || species == Species.Anemone) level *= 0.57f;
                if (pr.Leaf == Leaves.Fine) level *= 0.79f;
                float a = pose.Yaw + i * GoldenAngle + 0.45f * Unit(seed, (uint)(40 + i));
                var dir = Radial(a);
                var origin = pose.At(level);
                float len = length * (1.06f - t * 0.39f) * scale;
                var p = origin + dir * (len * 0.27f) + Vector3.up * (len * 0.18f);
                Stalk(b, origin, p, pr.Leaf == Leaves.Heart ? 0.007f : 0.0045f, pr.Stem, 0.30f + t * 0.32f);
                LeafBlade(b, p, dir + Vector3.up * (0.44f - t * 0.15f), len, width * (1f - t * 0.28f) * scale,
                    pr.Foliage * (0.91f + 0.18f * t), shape, 0.49f + 0.30f * t, -0.24f);
            }
        }

        private static void BuildBud(FoliageMesh b, Profile pr, Species species, Vector3 head, Vector3 axis, float yaw)
        {
            float len = species == Species.Iris || species == Species.Lily ? 0.125f : 0.083f;
            float width = species == Species.Sunflower ? 0.064f : 0.043f;
            if (pr.Shape == Bloom.Spike)
            {
                // Closed florets emerge along the upper stalk in a staggered spiral.
                for (int i = 0; i < 5; i++)
                {
                    var dir = Radial(yaw + i * GoldenAngle);
                    var p = head - Vector3.up * (0.036f * i) + dir * (0.017f + 0.009f * i);
                    b.Ball(p, new Vector3(0.022f, 0.031f, 0.023f) * (0.74f + i * 0.068f),
                        Color.Lerp(pr.Foliage * 0.76f, pr.Bud, 0.15f + 0.15f * i), 0.62f, 7, 4);
                }
                return;
            }

            var core = head + axis * (len * 0.40f);
            b.Ball(core, new Vector3(width * 0.91f, len * 0.58f, width * 0.91f), pr.Bud, 0.76f, 10, 6);
            for (int i = 0; i < 5; i++)
            {
                var dir = Radial(yaw + i * Turn / 5f);
                var start = head + dir * (width * 0.43f);
                var outward = (axis + dir * 0.62f).normalized;
                b.CurvedBlade(start, outward, dir, len * (i % 2 == 0 ? 0.93f : 0.75f), width * 0.52f,
                    -0.24f, 0.12f, BladeShape.Lance, pr.Stem * 0.66f, pr.Foliage, 0.73f);
            }
        }

        private static void BuildBloom(FoliageMesh b, Profile pr, Species species, PlantPose pose, uint seed)
        {
            var head = pose.At(1f);
            float yaw = pose.Yaw;
            var dir = Radial(yaw);
            var normal = (Vector3.up + dir * 0.29f).normalized;
            switch (pr.Shape)
            {
                case Bloom.Radial:
                    BuildOpenFlower(b, pr, species, head, normal, yaw, seed);
                    if (species == Species.Cosmos || species == Species.WildRose || species == Species.Buttercup)
                    {
                        var offset = Radial(yaw + 1.79f) * (species == Species.Buttercup ? 0.20f : 0.245f);
                        var sideHead = head + offset - Vector3.up * (pose.Height * 0.25f);
                        Stalk(b, pose.At(0.60f), sideHead, 0.0058f, pr.Stem, 0.66f);
                        BuildOpenFlower(b, pr, species, sideHead, (Vector3.up + offset * 2f).normalized, yaw + 0.77f, seed + 17u, 0.60f);
                    }
                    break;
                case Bloom.Bells:
                    for (int i = 0; i < 3; i++)
                    {
                        float a = yaw + i * 2.42f;
                        var outwards = Radial(a);
                        var from = pose.At(0.95f - i * 0.17f);
                        var calyx = from + outwards * (0.078f + i * 0.024f) + Vector3.up * 0.022f;
                        Stalk(b, from, calyx, 0.006f, pr.Stem, 0.69f);
                        FlowerTube(b, pr, calyx, outwards * 0.74f + Vector3.down * 0.79f, 0.19f - i * 0.018f,
                            0.062f - i * 0.005f, false);
                    }
                    break;
                case Bloom.Spike:
                    BuildSpike(b, pr, species, pose);
                    break;
                case Bloom.Iris:
                    for (int i = 0; i < 3; i++)
                    {
                        float a = yaw + i * Turn / 3f;
                        var outwards = Radial(a);
                        var standard = Radial(a + Mathf.PI / 3f);
                        b.CurvedBlade(head, outwards, Vector3.up, 0.241f, 0.076f, -0.61f, 0.30f,
                            BladeShape.Ruffled, new Color(0.93f, 0.71f, 0.24f), pr.PetalBase * 0.92f, 0.76f);
                        b.CurvedBlade(head + outwards * 0.032f + Vector3.up * 0.012f, outwards, Vector3.up,
                            0.084f, 0.014f, 0.06f, 0.10f, BladeShape.Lance, pr.Center, new Color(0.86f, 0.46f, 0.10f), 0.76f);
                        b.CurvedBlade(head + Vector3.up * 0.010f, standard * 0.43f + Vector3.up, standard,
                            0.232f, 0.072f, -0.27f, 0.37f, BladeShape.Ruffled, pr.PetalBase, pr.PetalTip, 0.79f);
                    }
                    break;
                case Bloom.Cluster:
                    for (int i = 0; i < 7; i++)
                    {
                        float a = yaw + i * GoldenAngle;
                        var outwards = Radial(a);
                        float r = 0.03f + 0.019f * i;
                        var p = head + outwards * r + Vector3.up * (0.035f - i * 0.012f);
                        Stalk(b, pose.At(0.66f + i % 3 * 0.09f), p, 0.0036f, pr.Stem, 0.69f);
                        PetalRing(b, p, (Vector3.up + outwards * 0.50f).normalized, 5, a, 0.045f, 0.026f,
                            0.06f, 0.17f, BladeShape.Rounded, pr.PetalBase, pr.PetalTip, seed + (uint)i);
                        b.Ball(p + Vector3.up * 0.012f, new Vector3(0.013f, 0.008f, 0.013f), pr.Center, 0.74f, 6, 4);
                    }
                    break;
                case Bloom.Pom:
                    Sepals(b, pr, head, normal, yaw, 0.080f);
                    for (int i = 0; i < 4; i++)
                    {
                        float t = i / 3f;
                        var p = head + normal * (i * 0.013f);
                        PetalRing(b, p, normal, 17 - i * 3, yaw + i * 0.31f, Mathf.Lerp(0.141f, 0.064f, t),
                            Mathf.Lerp(0.018f, 0.011f, t), Mathf.Lerp(0.24f, 0.88f, t), 0.14f, BladeShape.Ray,
                            pr.PetalBase, pr.PetalTip, seed + (uint)i * 17u);
                    }
                    b.Ball(head + normal * 0.085f, new Vector3(0.035f, 0.024f, 0.035f), pr.Center, 0.76f, 8, 5);
                    break;
                case Bloom.Trumpet:
                    BuildLily(b, pr, head, (Vector3.up * 0.76f + dir * 0.63f).normalized, yaw, seed);
                    break;
                case Bloom.Berry:
                    for (int i = 0; i < 3; i++)
                    {
                        float a = yaw + i * 2.7f;
                        var outwards = Radial(a);
                        var p = head + outwards * (0.062f + i * 0.025f) - Vector3.up * (i * 0.046f);
                        Stalk(b, pose.At(0.59f), p, 0.005f, pr.Stem, 0.65f);
                        PetalRing(b, p, (Vector3.up + outwards * 0.48f).normalized, 5, a, pr.PetalLen, pr.PetalWid,
                            pr.Lift, pr.Cup, BladeShape.Rounded, pr.PetalBase, pr.PetalTip, seed + (uint)i * 7u);
                        b.Ball(p + Vector3.up * 0.017f, new Vector3(0.024f, 0.013f, 0.024f), Gold, 0.74f, 8, 4);
                    }
                    // Small fruit forms stay below the white flowers on their own bent stems.
                    for (int i = 0; i < 2; i++)
                    {
                        var outwards = Radial(yaw + 0.8f + i * 2.81f);
                        var p = pose.At(0.69f) + outwards * (0.14f + i * 0.035f);
                        Stalk(b, pose.At(0.52f), p, 0.005f, pr.Stem, 0.58f);
                        BuildBerry(b, p, outwards * 0.30f + Vector3.down, pr.Center * (1f - i * 0.18f));
                    }
                    break;
            }
        }

        private static void BuildOpenFlower(FoliageMesh b, Profile pr, Species species, Vector3 head, Vector3 normal,
            float yaw, uint seed, float scale = 1f)
        {
            bool composite = species == Species.Cosmos || species == Species.Daisy || species == Species.Sunflower;
            bool secondRow = species == Species.Sunflower || species == Species.WildRose;
            var shape = species == Species.Poppy || species == Species.Cosmos ? BladeShape.Ruffled
                : species == Species.Sunflower || species == Species.Daisy ? BladeShape.Ray : BladeShape.Rounded;
            Sepals(b, pr, head, normal, yaw, pr.PetalLen * scale * 0.42f);
            PetalRing(b, head, normal, pr.Petals, yaw, pr.PetalLen * scale, pr.PetalWid * scale, pr.Lift, pr.Cup,
                shape, pr.PetalBase, pr.PetalTip, seed, pr.CenterSize * 0.63f * scale);
            if (secondRow)
                PetalRing(b, head + normal * (0.009f * scale), normal, pr.Petals, yaw + Mathf.PI / pr.Petals,
                    pr.PetalLen * 0.82f * scale, pr.PetalWid * 0.79f * scale, pr.Lift + 0.14f, pr.Cup, shape,
                    pr.PetalBase * 0.90f, pr.PetalTip, seed + 173u, pr.CenterSize * 0.59f * scale);

            float radius = pr.CenterSize * scale;
            var crown = head + normal * (0.016f * scale);
            b.Ball(crown, new Vector3(radius, radius * 0.46f, radius), pr.Center * 0.87f, 0.76f, 11, 5);
            var side = Vector3.Cross(normal, Vector3.forward).normalized;
            var forward = Vector3.Cross(side, normal);
            int stamens = composite ? (species == Species.Sunflower ? 33 : 17) : 10;
            for (int i = 0; i < stamens; i++)
            {
                float a = yaw + i * GoldenAngle;
                float r = composite ? Mathf.Sqrt((i + 0.55f) / stamens) * 0.86f : 0.92f + (i % 3) * 0.12f;
                var p = crown + (side * Mathf.Cos(a) + forward * Mathf.Sin(a)) * (radius * r);
                float h = composite ? radius * (0.40f - 0.24f * r * r) : radius * (0.20f + (i % 3) * 0.12f);
                var tone = Color.Lerp(pr.Center, species == Species.Poppy || species == Species.Anemone
                    ? new Color(0.39f, 0.25f, 0.42f) : Gold, composite ? 0.19f + 0.27f * r : 0.67f);
                float size = Mathf.Clamp(radius * (composite ? 0.147f : 0.127f), 0.0026f, 0.015f);
                b.Ball(p + normal * h, new Vector3(size, size * 1.32f, size), tone, 0.76f, 5, 2);
            }
        }

        private static void Sepals(FoliageMesh b, Profile pr, Vector3 p, Vector3 normal, float yaw, float radius)
        {
            PetalRing(b, p - normal * (radius * 0.13f), normal, 5, yaw + 0.43f, radius, radius * 0.23f,
                -0.36f, -0.16f, BladeShape.Lance, pr.Stem * 0.77f, pr.Foliage, 0u);
        }

        private static void PetalRing(FoliageMesh b, Vector3 center, Vector3 normal, int count, float angle, float length,
            float width, float bend, float cup, BladeShape shape, Color baseColor, Color tipColor, uint seed, float inner = 0f)
        {
            var side = Vector3.Cross(normal, Vector3.forward).normalized;
            var forward = Vector3.Cross(side, normal);
            for (int i = 0; i < count; i++)
            {
                float variation = Unit(seed, (uint)(32 + i)) - 0.5f;
                float a = angle + i * Turn / count + variation * (Turn / count) * 0.25f;
                var dir = side * Mathf.Cos(a) + forward * Mathf.Sin(a);
                var p = center + dir * inner + normal * (variation * width * 0.14f);
                b.CurvedBlade(p, dir, normal, length * (1f + variation * 0.19f), width * (1f + variation * 0.17f),
                    bend + variation * 0.18f, cup, shape, baseColor * (0.98f + variation * 0.14f),
                    tipColor * (0.98f + variation * 0.085f), 0.76f);
            }
        }

        private static void BuildSpike(FoliageMesh b, Profile pr, Species species, PlantPose pose)
        {
            bool foxglove = species == Species.Foxglove;
            for (int i = 0; i < 7; i++)
            {
                float t = i / 7f;
                float a = pose.Yaw + (foxglove ? Mathf.Sin(i * 2.1f) * 0.87f : i * GoldenAngle);
                var p = pose.At(Mathf.Lerp(0.54f, 0.985f, t));
                var dir = Radial(a);
                if (foxglove)
                {
                    var calyx = p + dir * (0.067f - 0.034f * t);
                    Stalk(b, p, calyx, 0.0046f, pr.Stem, 0.67f);
                    if (i < 5)
                        FlowerTube(b, pr, calyx, dir * 0.87f + Vector3.down * 0.52f, Mathf.Lerp(0.157f, 0.091f, t),
                            Mathf.Lerp(0.046f, 0.029f, t), true);
                    else
                        b.Ball(calyx, new Vector3(0.019f, 0.028f, 0.020f), pr.Bud, 0.72f, 7, 4);
                }
                else
                {
                    for (int j = 0; j < 3; j++)
                    {
                        var outwards = Radial(a + j * Turn / 3f);
                        var at = p + outwards * Mathf.Lerp(0.036f, 0.012f, t);
                        var face = (outwards + Vector3.up * 0.59f).normalized;
                        PetalRing(b, at, face, 4, a + j, Mathf.Lerp(0.046f, 0.023f, t), Mathf.Lerp(0.022f, 0.013f, t),
                            0.21f, 0.22f, BladeShape.Rounded, pr.PetalBase, pr.PetalTip, (uint)(i * 17 + j));
                        b.Ball(at + face * 0.010f, new Vector3(0.011f, 0.010f, 0.011f), pr.Center, 0.74f, 5, 3);
                    }
                }
            }
        }

        private static readonly float[] BellRows = { 0f, 0.24f, 0.53f, 0.76f, 1f };
        private static readonly float[] BellRadii = { 0.27f, 0.64f, 0.86f, 0.91f, 1.04f };

        /// <summary>Hollow bells with five lobes at the opening and a darker throat.</summary>
        private static void FlowerTube(FoliageMesh b, Profile pr, Vector3 origin, Vector3 direction, float length, float width, bool foxglove)
        {
            var axis = direction.normalized;
            var side = Vector3.Cross(axis, Vector3.up).normalized;
            var up = Vector3.Cross(side, axis);
            const int sides = 20;
            int first = b.V.Count;
            for (int row = 0; row < BellRows.Length; row++)
            {
                float t = BellRows[row];
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Turn / sides;
                    float rib = Mathf.Cos(a * 5f);
                    var radial = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    float rim = t * t * t;
                    float radius = width * BellRadii[row] * (1f + rib * (0.024f + rim * 0.094f));
                    float lobe = rim * (0.11f * rib + (foxglove ? -0.16f * Mathf.Sin(a) : 0f));
                    var p = origin + axis * (length * (t + lobe)) + radial * radius;
                    var color = Color.Lerp(pr.PetalBase, pr.PetalTip, 0.08f + t * 0.84f) * (0.95f + rib * 0.072f);
                    color.a = 0.70f + t * 0.09f;
                    b.Add(p, (radial - axis * (row < 2 ? 0.28f : 0.09f)).normalized, color);
                }
            }
            int rimStart = first + (BellRows.Length - 1) * sides;
            int inside = b.V.Count;
            for (int s = 0; s < sides; s++)
            {
                float a = s * Turn / sides;
                var radial = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                var color = Color.Lerp(pr.PetalBase * 0.51f, pr.Center * 0.85f, foxglove ? 0.55f : 0.27f);
                color.a = 0.76f;
                b.Add(origin + axis * (length * 0.48f) + radial * (width * 0.56f), -radial, color);
            }
            for (int row = 0; row < BellRows.Length - 1; row++)
                for (int s = 0; s < sides; s++)
                {
                    int a = first + row * sides + s, e = first + row * sides + (s + 1) % sides;
                    b.Triangle(a, a + sides, e);
                    b.Triangle(e, a + sides, e + sides);
                }
            for (int s = 0; s < sides; s++)
            {
                int n = (s + 1) % sides;
                b.Triangle(rimStart + s, inside + s, rimStart + n);
                b.Triangle(rimStart + n, inside + s, inside + n);
            }
            if (foxglove)
            {
                for (int i = 0; i < 3; i++)
                {
                    var p = origin + axis * (length * (0.77f + 0.061f * (i % 2)))
                        - up * (width * 0.69f) + side * ((i - 1) * width * 0.26f);
                    b.Ball(p, new Vector3(0.0048f, 0.0035f, 0.0048f), new Color(0.37f, 0.085f, 0.23f), 0.77f, 5, 3);
                }
            }
            b.Ball(origin + axis * (length * 0.55f), Vector3.one * (width * 0.20f), pr.Center * 0.78f, 0.75f, 6, 4);
        }

        private static void BuildLily(FoliageMesh b, Profile pr, Vector3 head, Vector3 normal, float yaw, uint seed)
        {
            var throat = head + normal * 0.074f;
            Stalk(b, head - normal * 0.012f, throat, 0.022f, pr.Foliage, 0.73f);
            PetalRing(b, throat, normal, 3, yaw, pr.PetalLen, pr.PetalWid, pr.Lift, pr.Cup, BladeShape.Lance,
                pr.PetalBase, pr.PetalTip, seed);
            PetalRing(b, throat + normal * 0.003f, normal, 3, yaw + Mathf.PI / 3f, pr.PetalLen * 0.92f, pr.PetalWid * 1.11f,
                pr.Lift * 0.68f, pr.Cup, BladeShape.Oval, pr.PetalBase, pr.PetalTip, seed + 15u);
            var side = Vector3.Cross(normal, Vector3.forward).normalized;
            var forward = Vector3.Cross(side, normal);
            for (int i = 0; i < 6; i++)
            {
                float a = yaw + (i + 0.5f) * Turn / 6f;
                var dir = side * Mathf.Cos(a) + forward * Mathf.Sin(a);
                var tip = throat + dir * (0.062f + (i % 2) * 0.015f) + normal * (0.116f - (i % 2) * 0.015f);
                Stalk(b, throat, tip, 0.004f, new Color(0.80f, 0.76f, 0.47f), 0.76f);
                b.Ball(tip, new Vector3(0.014f, 0.010f, 0.011f), pr.Center, 0.76f, 6, 4);
            }
        }

        private static void BuildBerry(FoliageMesh b, Vector3 origin, Vector3 direction, Color color)
        {
            var axis = direction.normalized;
            var side = Vector3.Cross(axis, Vector3.right).normalized;
            var across = Vector3.Cross(side, axis);
            const int rings = 5, sides = 10;
            int first = b.V.Count;
            for (int row = 0; row < rings; row++)
            {
                float t = (row + 0.02f) / (rings - 0.90f);
                float radius = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.64f)) * 0.044f;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Turn / sides + (row % 2) * 0.04f;
                    var radial = side * Mathf.Cos(a) + across * Mathf.Sin(a);
                    var p = origin + axis * (t * 0.094f) + radial * radius;
                    var tone = color * (1.14f - t * 0.44f);
                    tone.a = 0.67f;
                    b.Add(p, (radial + axis * (t - 0.43f)).normalized, tone);
                }
            }
            for (int row = 0; row < rings - 1; row++)
                for (int i = 0; i < sides; i++)
                {
                    int a = first + row * sides + i, c = first + row * sides + (i + 1) % sides;
                    b.Triangle(a, a + sides, c);
                    b.Triangle(c, a + sides, c + sides);
                }
            int tip = b.Add(origin + axis * 0.096f, axis, new Color(color.r * 0.70f, color.g * 0.70f, color.b * 0.70f, 0.67f));
            for (int i = 0; i < sides; i++) b.Triangle(tip - sides + i, tip, tip - sides + (i + 1) % sides);
        }
    }
}
