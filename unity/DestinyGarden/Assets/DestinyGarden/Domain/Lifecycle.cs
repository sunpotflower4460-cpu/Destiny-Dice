using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>
    /// The cycle of the garden (lifecycle.v1, docs/unity-garden/ECOLOGY.md §9). A flower counts the rolls it
    /// has been in bloom; when it reaches its species' span it bears its seed and returns to the soil, which
    /// frees its place and feeds the plants beside it. At most one returns in a roll, the longest past their
    /// span first. A flower that bees or butterflies visited while in bloom (lifecycle.v2) lets its seed fall on open
    /// ground nearby as it returns; an unvisited one leaves only its nourishment. Water is never moved. Without it a garden fills up after about sixty
    /// rolls and the dice can no longer change it.
    /// </summary>
    public static class Lifecycle
    {
        public const string RulesVersion = "lifecycle.v4";
        public const int SeedSetOneIn = 2;           // lifecycle.v4: a pollinated flower sets viable seed 1 time in 2
        public const string RootKind = "root";       // EcologyEffect.Kind: a suited plant the weather did not move puts down roots
        public const string WitherKind = "wither";   // EcologyEffect.Kind: a plant that no longer suits its place withered
        public const string WitheredEvent = "Withered";
        public const int RootGrowth = 30;
        public const int WitherAfter = 8;            // idle rolls in an unsuitable place before a plant withers
        public const int MaxWithersPerRoll = 1;
        public const string FruitKind = "fruit";     // EcologyEffect.Kind: the seed a pollinated flower lets fall
        public const string ReturnKind = "return";   // EcologyEffect.Kind: a flower returned to the soil
        public const string HumusKind = "humus";     // EcologyEffect.Kind: a neighbour fed by it
        public const string ReturnedEvent = "Returned";
        public const int MaxReturnsPerRoll = 1;
        public const int HumusGrowth = 40;

        /// <summary>Rolls a species stays in flower: the pioneer is short-lived, annuals middling, the rest longer.</summary>
        public static int SpanOf(string species)
        {
            switch (species)
            {
                case "dandelion": return 16;
                case "cosmos": case "poppy": case "sunflower": case "daisy": return 26;
                case null: return 26;
                default: return 40;
            }
        }

        /// <summary>
        /// lifecycle.v3: plants that went WitherAfter rolls without growing in a place that no longer suits them
        /// (the soil dried or drowned) wither, at most one a roll, the longest idle first. They leave nothing.
        /// </summary>
        public static List<PlantState> Wither(WorldState s)
        {
            var due = new List<PlantState>();
            foreach (var p in s.Plants) if (p.Stage != PlantStage.Flower && p.IdleRolls >= WitherAfter) due.Add(p);
            due.Sort((a, b) => a.IdleRolls != b.IdleRolls ? b.IdleRolls.CompareTo(a.IdleRolls) : a.CellId.CompareTo(b.CellId));
            if (due.Count > MaxWithersPerRoll) due.RemoveRange(MaxWithersPerRoll, due.Count - MaxWithersPerRoll);
            foreach (var p in due) s.Plants.Remove(p);
            return due;
        }

        /// <summary>Counts one more roll for every flower, then removes the flowers that return this roll.</summary>
        public static List<PlantState> Advance(WorldState s)
        {
            var due = new List<PlantState>();
            foreach (var p in s.Plants)
            {
                if (p.Stage != PlantStage.Flower) continue;
                p.BloomRolls++;
                if (p.BloomRolls >= SpanOf(p.Species)) due.Add(p);
            }
            due.Sort((a, b) =>
            {
                int oa = a.BloomRolls - SpanOf(a.Species), ob = b.BloomRolls - SpanOf(b.Species);
                return oa != ob ? ob.CompareTo(oa) : a.CellId.CompareTo(b.CellId);
            });
            if (due.Count > MaxReturnsPerRoll) due.RemoveRange(MaxReturnsPerRoll, due.Count - MaxReturnsPerRoll);
            foreach (var p in due) s.Plants.Remove(p);
            return due;
        }
    }
}
