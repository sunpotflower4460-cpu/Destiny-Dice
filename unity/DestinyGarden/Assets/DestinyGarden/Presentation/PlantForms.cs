using DestinyGarden.Domain;
using UnityEngine;

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

        private enum Leaves { Pairs, Strap, Rosette, Heart, Round, Fine }
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
            P(0.78f, Leaves.Fine, Bloom.Radial, Green, new Color(0.84f, 0.40f, 0.58f), new Color(0.92f, 0.46f, 0.66f), new Color(1f, 0.80f, 0.88f), Gold, 8, 0.15f, 0.055f, 0.25f, 0.35f),
            P(0.62f, Leaves.Pairs, Bloom.Radial, Green, new Color(0.80f, 0.86f, 0.62f), new Color(0.96f, 0.95f, 0.88f), new Color(1f, 1f, 0.98f), Gold, 16, 0.13f, 0.024f, 0.12f, 0.2f, 0.048f),
            P(0.58f, Leaves.Heart, Bloom.Bells, DeepGreen, new Color(0.46f, 0.50f, 0.86f), new Color(0.42f, 0.46f, 0.90f), new Color(0.70f, 0.72f, 1f), new Color(0.96f, 0.96f, 1f)),
            P(0.70f, Leaves.Pairs, Bloom.Radial, GreyGreen, new Color(0.46f, 0.56f, 0.34f), new Color(0.80f, 0.10f, 0.12f), new Color(1f, 0.38f, 0.28f), new Color(0.10f, 0.08f, 0.10f), 4, 0.15f, 0.12f, 0.9f, 0.6f, 0.04f),
            P(1.10f, Leaves.Heart, Bloom.Radial, Green, new Color(0.36f, 0.52f, 0.26f), new Color(1f, 0.70f, 0.10f), new Color(1f, 0.88f, 0.30f), new Color(0.40f, 0.24f, 0.12f), 16, 0.13f, 0.04f, 0.1f, 0.25f, 0.075f),
            P(1.00f, Leaves.Rosette, Bloom.Spike, DeepGreen, new Color(0.78f, 0.52f, 0.78f), new Color(0.82f, 0.42f, 0.74f), new Color(1f, 0.82f, 0.94f), new Color(0.98f, 0.90f, 0.96f)),
            P(0.55f, Leaves.Round, Bloom.Radial, DeepGreen, new Color(0.86f, 0.84f, 0.94f), new Color(0.98f, 0.96f, 1f), new Color(0.86f, 0.80f, 1f), new Color(0.12f, 0.12f, 0.26f), 6, 0.14f, 0.075f, 0.4f, 0.45f, 0.045f),
            P(0.80f, Leaves.Strap, Bloom.Iris, Green, new Color(0.50f, 0.38f, 0.80f), new Color(0.44f, 0.32f, 0.82f), new Color(0.76f, 0.66f, 1f), new Color(1f, 0.86f, 0.30f)),
            P(0.40f, Leaves.Round, Bloom.Cluster, Green, new Color(0.62f, 0.72f, 0.96f), new Color(0.46f, 0.66f, 1f), new Color(0.66f, 0.82f, 1f), new Color(1f, 0.92f, 0.40f)),
            P(0.48f, Leaves.Round, Bloom.Radial, Green, new Color(0.70f, 0.70f, 0.30f), new Color(1f, 0.78f, 0.12f), new Color(1f, 0.92f, 0.36f), new Color(0.94f, 0.66f, 0.10f), 5, 0.10f, 0.075f, 0.9f, 0.55f, 0.03f),
            P(0.70f, Leaves.Fine, Bloom.Spike, GreyGreen, new Color(0.62f, 0.50f, 0.86f), new Color(0.60f, 0.46f, 0.90f), new Color(0.80f, 0.68f, 1f), new Color(0.70f, 0.56f, 0.94f)),
            P(0.62f, Leaves.Pairs, Bloom.Radial, DeepGreen, new Color(0.86f, 0.46f, 0.52f), new Color(0.96f, 0.56f, 0.62f), new Color(1f, 0.84f, 0.84f), Gold, 5, 0.14f, 0.10f, 0.35f, 0.5f, 0.04f),
            P(0.32f, Leaves.Rosette, Bloom.Pom, Green, new Color(0.62f, 0.66f, 0.30f), new Color(1f, 0.82f, 0.14f), new Color(1f, 0.92f, 0.40f), new Color(0.96f, 0.72f, 0.10f)),
            P(0.30f, Leaves.Round, Bloom.Berry, DeepGreen, new Color(0.86f, 0.90f, 0.80f), new Color(0.96f, 0.96f, 0.92f), new Color(1f, 1f, 1f), new Color(0.92f, 0.16f, 0.18f), 5, 0.06f, 0.04f, 0.3f, 0.3f, 0.02f),
            P(0.92f, Leaves.Fine, Bloom.Trumpet, Green, new Color(0.86f, 0.84f, 0.74f), new Color(1f, 0.72f, 0.78f), new Color(1f, 0.98f, 0.94f), new Color(0.80f, 0.48f, 0.20f), 6, 0.20f, 0.06f, -0.55f, 0.35f, 0.02f),
            P(0.28f, Leaves.Round, Bloom.Pom, Green, new Color(0.80f, 0.84f, 0.70f), new Color(0.96f, 0.95f, 0.84f), new Color(1f, 0.84f, 0.90f), Gold, 10, 0.04f, 0.02f),
            P(0.55f, Leaves.Pairs, Bloom.Spike, Green, new Color(0.64f, 0.32f, 0.70f), new Color(0.58f, 0.26f, 0.72f), new Color(0.90f, 0.66f, 0.94f), Gold),
            P(0.66f, Leaves.Fine, Bloom.Cluster, GreyGreen, new Color(0.82f, 0.82f, 0.72f), new Color(1f, 0.94f, 0.80f), new Color(1f, 1f, 0.94f), Gold),
            P(0.22f, Leaves.Round, Bloom.Cluster, GreyGreen, new Color(0.76f, 0.48f, 0.76f), new Color(0.84f, 0.55f, 0.82f), new Color(0.96f, 0.78f, 0.94f), Gold),
            P(0.58f, Leaves.Pairs, Bloom.Spike, DeepGreen, new Color(0.68f, 0.72f, 0.80f), new Color(0.80f, 0.78f, 0.94f), new Color(0.96f, 0.94f, 1f), Gold),
            P(0.35f, Leaves.Rosette, Bloom.Radial, Green, new Color(0.88f, 0.48f, 0.66f), new Color(0.94f, 0.54f, 0.74f), new Color(1f, 0.82f, 0.88f), Gold, 5, 0.10f, 0.07f),
            P(0.25f, Leaves.Heart, Bloom.Radial, DeepGreen, new Color(0.48f, 0.38f, 0.76f), new Color(0.42f, 0.32f, 0.72f), new Color(0.78f, 0.70f, 0.98f), Gold, 5, 0.075f, 0.05f),
            P(0.90f, Leaves.Pairs, Bloom.Spike, Green, new Color(0.82f, 0.34f, 0.68f), new Color(0.84f, 0.34f, 0.72f), new Color(1f, 0.68f, 0.88f), Gold),
            P(0.92f, Leaves.Pairs, Bloom.Cluster, Green, new Color(0.88f, 0.74f, 0.26f), new Color(1f, 0.78f, 0.16f), new Color(1f, 0.94f, 0.52f), Gold),
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

        public static void Build(FoliageMesh b, PlantState plant, Vector3 root, Species species)
        {
            var pr = Profiles[(int)species];
            uint h = Hash(plant.PlantId);
            float R(int salt) => ((h >> (salt % 24)) & 0xFF) / 255f;
            float yaw = R(1) * Mathf.PI * 2f;
            var leanDir = new Vector3(Mathf.Cos(yaw * 1.7f), 0f, Mathf.Sin(yaw * 1.7f));
            float g = plant.Growth / 2500f;

            if (plant.Stage == PlantStage.Seed)
            {
                b.Ball(root + Vector3.up * 0.03f, new Vector3(0.05f, 0.035f, 0.05f), new Color(0.72f, 0.52f, 0.30f), 0f, 6, 3);
                return;
            }
            if (plant.Stage == PlantStage.Sprout)
            {
                b.Blade(root, leanDir * 0.1f, 0.14f, 0.012f, pr.Stem, pr.Foliage, yaw);
                var top = root + Vector3.up * 0.14f + leanDir * 0.014f;
                b.Leaf(top, new Vector3(Mathf.Cos(yaw), 0.5f, Mathf.Sin(yaw)), 0.10f, 0.045f, Color.Lerp(pr.Foliage, Color.white, 0.18f), 1f);
                b.Leaf(top, new Vector3(-Mathf.Cos(yaw), 0.5f, -Mathf.Sin(yaw)), 0.10f, 0.045f, Color.Lerp(pr.Foliage, Color.white, 0.18f), 1f);
                return;
            }

            float height = pr.Height * (0.86f + R(5) * 0.28f) * (plant.Stage == PlantStage.Leaf ? 0.55f + g * 0.4f : 0.8f + g * 0.2f);
            var lean = leanDir * (0.10f + R(9) * 0.12f);
            BuildLeaves(b, pr, root, height, yaw, h);
            if (species == Species.Strawberry || species == Species.Dandelion)
                height *= 0.9f;
            b.Blade(root, lean, height, 0.016f, pr.Stem, Color.Lerp(pr.Stem, pr.Foliage, 0.5f), yaw);
            var head = root + Vector3.up * height + lean * height;

            if (plant.Stage == PlantStage.Leaf) return;
            if (plant.Stage == PlantStage.Bud)
            {
                b.Ball(head + Vector3.up * 0.04f, new Vector3(0.035f, 0.065f, 0.035f), pr.Bud, 1f, 6, 3);
                return;
            }
            BuildBloom(b, pr, species, head, yaw, h);
        }

        private static void BuildLeaves(FoliageMesh b, Profile pr, Vector3 root, float height, float yaw, uint h)
        {
            var c = pr.Foliage;
            switch (pr.Leaf)
            {
                case Leaves.Pairs:
                    for (int i = 0; i < 3; i++)
                    {
                        float y = height * (0.25f + i * 0.2f);
                        float a = yaw + i * 1.7f;
                        var at = root + Vector3.up * y;
                        b.Leaf(at, new Vector3(Mathf.Cos(a), 0.45f, Mathf.Sin(a)), 0.16f - i * 0.03f, 0.04f, c * (1f - i * 0.05f), 1f);
                        b.Leaf(at, new Vector3(-Mathf.Cos(a), 0.45f, -Mathf.Sin(a)), 0.16f - i * 0.03f, 0.04f, c * (1f - i * 0.05f), 1f);
                    }
                    break;
                case Leaves.Fine:
                    for (int i = 0; i < 6; i++)
                    {
                        float a = yaw + i * 2.4f;
                        b.Leaf(root + Vector3.up * (height * (0.15f + i * 0.11f)), new Vector3(Mathf.Cos(a), 0.55f, Mathf.Sin(a)), 0.14f, 0.018f, c, 1f);
                    }
                    break;
                case Leaves.Strap:
                    for (int i = 0; i < 4; i++)
                    {
                        float a = yaw + i * 1.6f;
                        b.Blade(root, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.25f, height * 0.7f, 0.035f, pr.Stem, c, a + 1.57f);
                    }
                    break;
                case Leaves.Rosette:
                    for (int i = 0; i < 7; i++)
                    {
                        float a = yaw + i * 0.9f;
                        b.Leaf(root + Vector3.up * 0.02f, new Vector3(Mathf.Cos(a), 0.18f, Mathf.Sin(a)), 0.20f, 0.055f, c * (0.92f + (i % 2) * 0.1f), 0.5f);
                    }
                    break;
                case Leaves.Heart:
                    for (int i = 0; i < 4; i++)
                    {
                        float a = yaw + i * 1.9f;
                        float y = height * (0.12f + i * 0.16f);
                        b.Leaf(root + Vector3.up * y, new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)), 0.20f - i * 0.025f, 0.085f, c, 1f);
                    }
                    break;
                case Leaves.Round:
                    for (int i = 0; i < 5; i++)
                    {
                        float a = yaw + i * 1.26f;
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        b.Blade(root, dir * 0.6f, 0.08f, 0.008f, pr.Stem, pr.Stem, a);
                        b.Ball(root + dir * 0.09f + Vector3.up * 0.07f, new Vector3(0.05f, 0.012f, 0.05f), c * (0.95f + (i % 2) * 0.08f), 0.6f, 6, 2);
                    }
                    break;
            }
        }

        private static void BuildBloom(FoliageMesh b, Profile pr, Species species, Vector3 head, float yaw, uint h)
        {
            switch (pr.Shape)
            {
                case Bloom.Radial:
                    b.Corolla(head, pr.Petals, yaw, pr.PetalLen, pr.PetalWid, pr.Lift, pr.Cup, pr.PetalBase, pr.PetalTip);
                    if (species == Species.Cosmos || species == Species.Daisy || species == Species.Sunflower)
                        b.Corolla(head + Vector3.up * 0.006f, pr.Petals, yaw + Mathf.PI / pr.Petals, pr.PetalLen * 0.85f, pr.PetalWid * 0.9f,
                            pr.Lift + 0.1f, pr.Cup, pr.PetalBase, pr.PetalTip);
                    b.Ball(head + Vector3.up * 0.018f, new Vector3(pr.CenterSize, pr.CenterSize * 0.55f, pr.CenterSize), pr.Center, 1f, 7, 3);
                    if (species == Species.Poppy || species == Species.Anemone || species == Species.WildRose)
                        for (int k = 0; k < 8; k++)
                        {
                            float a = yaw + k * 0.785f;
                            b.Ball(head + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * pr.CenterSize * 1.25f + Vector3.up * 0.03f,
                                Vector3.one * 0.008f, species == Species.WildRose ? Gold : new Color(0.08f, 0.06f, 0.10f), 1f, 4, 2);
                        }
                    break;
                case Bloom.Bells:
                    for (int k = 0; k < 3; k++)
                    {
                        float a = yaw + k * 2.1f;
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        var at = head + dir * 0.07f + Vector3.down * (0.03f + k * 0.035f);
                        b.Blade(head, dir * 0.6f, 0.05f, 0.006f, pr.Stem, pr.Stem, a);
                        for (int p = 0; p < 5; p++)
                        {
                            float pa = a + p * 1.2566f;
                            b.Petal(at, new Vector3(Mathf.Cos(pa), 0f, Mathf.Sin(pa)), 0.07f, 0.035f, -1.4f, 0.5f, pr.PetalBase, pr.PetalTip);
                        }
                        b.Ball(at + Vector3.down * 0.02f, Vector3.one * 0.012f, pr.Center, 1f, 4, 2);
                    }
                    break;
                case Bloom.Spike:
                    for (int k = 0; k < 9; k++)
                    {
                        float a = yaw + k * 2.4f;
                        var at = head + Vector3.down * (0.02f + k * 0.035f) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.025f;
                        float s = 1f - k * 0.05f;
                        if (species == Species.Foxglove)
                        {
                            b.Ball(at + new Vector3(Mathf.Cos(a), -0.3f, Mathf.Sin(a)) * 0.03f, new Vector3(0.028f, 0.05f, 0.028f) * s,
                                Color.Lerp(pr.PetalBase, pr.PetalTip, k / 9f), 1f, 6, 3);
                            b.Ball(at + new Vector3(Mathf.Cos(a), -0.6f, Mathf.Sin(a)) * 0.045f, Vector3.one * 0.012f * s, pr.Center, 1f, 4, 2);
                        }
                        else
                            b.Ball(at, Vector3.one * 0.024f * s, Color.Lerp(pr.PetalBase, pr.PetalTip, (k % 3) / 3f), 1f, 5, 2);
                    }
                    break;
                case Bloom.Iris:
                    for (int k = 0; k < 3; k++)
                    {
                        float a = yaw + k * 2.094f;
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        b.Petal(head + Vector3.up * 0.02f, dir, 0.11f, 0.05f, 2.4f, 0.5f, pr.PetalBase, pr.PetalTip);       // standards
                        b.Petal(head, new Vector3(Mathf.Cos(a + 1.05f), 0f, Mathf.Sin(a + 1.05f)), 0.15f, 0.06f, -0.9f, 0.3f,
                            pr.PetalBase, pr.Center);                                                                         // falls
                    }
                    break;
                case Bloom.Cluster:
                    for (int k = 0; k < 9; k++)
                    {
                        float a = yaw + k * 2.4f, r = 0.03f + (k % 3) * 0.03f;
                        var at = head + new Vector3(Mathf.Cos(a) * r, (k % 4) * 0.012f, Mathf.Sin(a) * r);
                        b.Corolla(at, 5, a, 0.026f, 0.016f, 0.2f, 0.3f, pr.PetalBase, pr.PetalTip);
                        b.Ball(at + Vector3.up * 0.004f, Vector3.one * 0.007f, pr.Center, 1f, 4, 2);
                    }
                    break;
                case Bloom.Pom:
                    for (int ring = 0; ring < 3; ring++)
                        b.Corolla(head + Vector3.up * (ring * 0.012f), 14 - ring * 3, yaw + ring * 0.3f, 0.065f - ring * 0.015f,
                            0.014f, 0.6f + ring * 0.5f, 0.2f, pr.PetalBase, pr.PetalTip);
                    break;
                case Bloom.Trumpet:
                    b.Corolla(head, 6, yaw, pr.PetalLen, pr.PetalWid, pr.Lift, pr.Cup, pr.PetalBase, pr.PetalTip);
                    for (int k = 0; k < 6; k++)
                    {
                        float a = yaw + k * 1.047f + 0.5f;
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        b.Blade(head, dir * 0.4f, 0.09f, 0.004f, new Color(0.88f, 0.90f, 0.70f), new Color(0.90f, 0.90f, 0.70f), a);
                        b.Ball(head + Vector3.up * 0.09f + dir * 0.036f, new Vector3(0.008f, 0.016f, 0.008f), pr.Center, 1f, 4, 2);
                    }
                    break;
                case Bloom.Berry:
                    for (int k = 0; k < 2; k++)
                    {
                        float a = yaw + k * 3.1f;
                        var at = head + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.05f;
                        b.Corolla(at, 5, a, pr.PetalLen, pr.PetalWid, pr.Lift, pr.Cup, pr.PetalBase, pr.PetalTip);
                        b.Ball(at + Vector3.up * 0.01f, Vector3.one * 0.012f, Gold, 1f, 4, 2);
                    }
                    b.Ball(head + Vector3.down * 0.10f + new Vector3(0.04f, 0f, -0.03f), new Vector3(0.026f, 0.032f, 0.026f), pr.Center, 0.6f, 6, 3);
                    b.Ball(head + Vector3.down * 0.13f + new Vector3(-0.05f, 0f, 0.02f), new Vector3(0.022f, 0.028f, 0.022f), pr.Center * 0.9f, 0.6f, 6, 3);
                    break;
            }
        }
    }
}
