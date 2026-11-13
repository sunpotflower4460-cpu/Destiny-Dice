using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>
    /// The cycle of the garden (lifecycle.v1, docs/unity-garden/ECOLOGY.md §9). A flower counts the rolls it
    /// has been in bloom; when it reaches its species' span it bears its seed and returns to the soil, which
    /// frees its place and feeds the plants beside it. At most one returns in a roll, the longest past their
    /// span first. No chance is involved; water is never moved. Without it a garden fills up after about sixty
    /// rolls and the dice can no longer change it.
    /// </summary>
    public static class Lifecycle
    {
        public const string RulesVersion = "lifecycle.v1";
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
                case "dandelion": return 12;
                case "cosmos": case "poppy": case "sunflower": case "daisy": return 20;
                case null: return 20;
                default: return 30;
            }
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
