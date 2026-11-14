using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>A patch of plants of one blessing's kind growing together (community.v1).</summary>
    public sealed class Community
    {
        public ElementId Element;
        public string Kind;
        public string LabelJa;
        public List<int> Cells = new List<int>();   // members, cell order
    }

    /// <summary>
    /// Communities (community.v1, docs/unity-garden/ECOLOGY.md §12). Three or more flowers in bloom that suit the
    /// same blessing (Flora.Suits), joined side by side, form a named community:
    /// water a wetland stand, light a flower meadow, wind a nectar patch, seed a berry thicket. Derived from the
    /// saved state only; no chance. What they do (Ecology): when their blessing is rolled the community answers
    /// and the plants in and around it grow; seeds that sprout near it lean toward its kind, so it spreads; and
    /// creatures come to it (butterflies to a nectar patch, dragonflies to a wetland, small birds to a thicket).
    /// Inspired by habitat corridors that grow stronger as they are joined (Cascadia), habitats whose action
    /// improves as they fill (Wingspan) and restored biomes that call their animals (Terra Nil).
    /// </summary>
    public static class Communities
    {
        public const string RulesVersion = "community.v1";
        public const string AnswerKind = "community";   // EcologyEffect.Kind: growth from a community's answer
        public const int MinSize = 3;
        public const int Reach = 1;          // members link side by side (a contiguous patch, like a habitat corridor)
        public const int SpreadReach = 2;    // seeds within this distance of a member lean toward the community
        public const int AnswerGrowth = 30;  // to each non-flowering plant in or next to an answering community
        public const int SpreadLeaning = 4;  // a seed within Reach of a member leans toward the community's kind
        public const int CallsCreaturesAt = 4; // a community this large calls its creatures (butterflies, dragonflies, small birds)

        private static readonly ElementId[] Order = { ElementId.Water, ElementId.Light, ElementId.Wind, ElementId.Seed };

        public static string KindOf(ElementId e) =>
            e == ElementId.Water ? "wetland" : e == ElementId.Light ? "meadow" : e == ElementId.Wind ? "nectar" : "thicket";

        public static string LabelOf(ElementId e) =>
            e == ElementId.Water ? "水辺の群落" : e == ElementId.Light ? "花の野" : e == ElementId.Wind ? "蜜の花畑" : "実りの藪";

        private static int Dist(int a, int b) => Math.Abs(a % 16 - b % 16) + Math.Abs(a / 16 - b / 16);

        /// <summary>
        /// The core species of each community (stricter than Flora.Suits, so the everywhere-growing dandelion does
        /// not make thickets or nectar patches): water, moisture lovers; light, sun lovers; wind, flowers that bees
        /// and butterflies love (bee + butterfly at least 3); seed, berry plants.
        /// </summary>
        public static bool Belongs(SpeciesTraits t, ElementId e)
        {
            if (t == null) return false;
            switch (e)
            {
                case ElementId.Water: return t.MoistMin >= 300;
                case ElementId.Light: return t.Light == 0;
                case ElementId.Wind: return Math.Max(0, t.Bee) + Math.Max(0, t.Butterfly) >= 3;
                default: return t.Berries;
            }
        }

        public static bool IsMember(PlantState p, ElementId e) =>
            p != null && p.Stage == PlantStage.Flower && Belongs(Flora.Get(p.Species), e);

        /// <summary>All communities, in element order (water, light, wind, seed), then by their first cell.</summary>
        public static List<Community> Find(WorldState s)
        {
            var result = new List<Community>();
            foreach (var e in Order)
            {
                var cells = new List<int>();
                foreach (var p in s.Plants) if (IsMember(p, e)) cells.Add(p.CellId);
                cells.Sort();
                var seen = new HashSet<int>();
                foreach (int start in cells)
                {
                    if (seen.Contains(start)) continue;
                    var group = new List<int>();
                    var stack = new Stack<int>();
                    stack.Push(start); seen.Add(start);
                    while (stack.Count > 0)
                    {
                        int c = stack.Pop();
                        group.Add(c);
                        foreach (int o in cells)
                            if (!seen.Contains(o) && Dist(c, o) <= Reach) { seen.Add(o); stack.Push(o); }
                    }
                    if (group.Count < MinSize) continue;
                    group.Sort();
                    result.Add(new Community { Element = e, Kind = KindOf(e), LabelJa = LabelOf(e), Cells = group });
                }
            }
            return result;
        }

        /// <summary>The largest group of flowers of this kind joined side by side (0 when none).</summary>
        public static int LargestCluster(WorldState s, ElementId e) => LargestCluster(s, e, false);

        /// <summary>
        /// The largest group of this kind joined side by side, flowers only or (season.v3) buds and flowers: a stand in
        /// the making, which is what a season's wish asks the garden to grow.
        /// </summary>
        public static int LargestCluster(WorldState s, ElementId e, bool withBuds) => LargestCluster(s, e, withBuds, Reach);

        /// <summary>season.v3: a stand as a season's wish sees it — buds and flowers within <paramref name="reach"/> cells.</summary>
        public static int LargestCluster(WorldState s, ElementId e, bool withBuds, int reach)
        {
            int best = 0;
            var cells = new List<int>();
            foreach (var p in s.Plants)
            {
                if (withBuds)
                {
                    // season.v3: a stand of the blessing's own family (as the star and the seeds lean), in bud or flower.
                    if ((p.Stage == PlantStage.Bud || p.Stage == PlantStage.Flower) && Flora.Suits(Flora.Get(p.Species), e)) cells.Add(p.CellId);
                }
                else if (IsMember(p, e)) cells.Add(p.CellId);
            }
            var seen = new HashSet<int>();
            foreach (int start in cells)
            {
                if (seen.Contains(start)) continue;
                int size = 0;
                var stack = new Stack<int>();
                stack.Push(start); seen.Add(start);
                while (stack.Count > 0)
                {
                    int c = stack.Pop(); size++;
                    foreach (int o in cells) if (!seen.Contains(o) && Dist(c, o) <= reach) { seen.Add(o); stack.Push(o); }
                }
                best = Math.Max(best, size);
            }
            return best;
        }

        /// <summary>Whether a community of this kind can begin soon: two neighbouring plants of its kind in bud or flower.</summary>
        public static bool CanBegin(WorldState s, ElementId e)
        {
            var cells = new List<int>();
            foreach (var p in s.Plants)
                if ((p.Stage == PlantStage.Bud || p.Stage == PlantStage.Flower) && Flora.Suits(Flora.Get(p.Species), e)) cells.Add(p.CellId); // season.v3
            for (int i = 0; i < cells.Count; i++)
                for (int j = i + 1; j < cells.Count; j++)
                    if (Dist(cells[i], cells[j]) <= Seasons.StandReach) return true; // season.v3: as the stand is judged
            return false;
        }

        /// <summary>Plants that grow when a community answers: its members not in flower, and non-flowering plants next to it.</summary>
        public static List<int> AnswerCells(WorldState s, Community c)
        {
            var set = new SortedSet<int>();
            foreach (var p in s.Plants)
            {
                if (p.Stage == PlantStage.Flower) continue;
                foreach (int m in c.Cells) if (Dist(p.CellId, m) <= 1) { set.Add(p.CellId); break; }
            }
            return new List<int>(set);
        }

        /// <summary>Extra weight for a species sprouting at a cell: SpreadLeaning per nearby community it suits.</summary>
        public static int SpreadWeight(IList<Community> communities, SpeciesTraits t, int cell)
        {
            if (communities == null || t == null) return 0;
            int w = 0;
            foreach (var c in communities)
            {
                if (!Belongs(t, c.Element)) continue;
                foreach (int m in c.Cells) if (Dist(m, cell) <= SpreadReach) { w += SpreadLeaning; break; }
            }
            return w;
        }
    }
}
