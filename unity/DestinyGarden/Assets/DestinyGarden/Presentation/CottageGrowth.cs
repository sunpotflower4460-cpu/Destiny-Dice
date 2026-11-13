using System;
using System.Collections.Generic;
using DestinyGarden.Application;
using DestinyGarden.Domain;

namespace DestinyGarden.Presentation
{
    public enum CottageStage { Cabin, Porch, Extension, Finished }

    /// <summary>The house is a projection of this garden's committed rounds.</summary>
    public readonly struct CottageGrowth
    {
        public const int PorchAt = 4;
        public const int ExtensionAt = 10;
        public const int FinishAt = 18;

        public CottageStage Stage { get; }
        public ElementId Extension { get; }

        private CottageGrowth(CottageStage stage, ElementId extension)
        {
            Stage = stage;
            Extension = extension;
        }

        public static CottageGrowth Project(int revision, IReadOnlyList<RoundSummary> rounds)
        {
            if (revision < ExtensionAt)
                return new CottageGrowth(revision < PorchAt ? CottageStage.Cabin : CottageStage.Porch, ElementId.Light);

            // The first ten resolved rolls choose the wing. Ties go to the family that arrived first.
            var counts = new int[4];
            var first = new[] { int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue };
            foreach (var round in rounds)
            {
                if (!round.Resolved || round.WorldRevision < 1 || round.WorldRevision > ExtensionAt ||
                    Array.IndexOf(RecipeCatalog.Order, round.RecipeId) < 0)
                    continue;

                int element = (int)RecipeCatalog.ElementOf(round.RecipeId);
                counts[element]++;
                first[element] = Math.Min(first[element], round.WorldRevision);
            }

            int wing = (int)ElementId.Light;
            for (int i = 0; i < counts.Length; i++)
                if (counts[i] > counts[wing] || (counts[i] == counts[wing] && first[i] < first[wing]))
                    wing = i;

            return new CottageGrowth(revision < FinishAt ? CottageStage.Extension : CottageStage.Finished, (ElementId)wing);
        }

        internal string Label
        {
            get
            {
                if (Stage == CottageStage.Cabin) return "小さな家";
                if (Stage == CottageStage.Porch) return "縁側のある家";
                switch (Extension)
                {
                    case ElementId.Water: return "水辺のテラス";
                    case ElementId.Wind: return "風を聴くロフト";
                    case ElementId.Seed: return "花の工房";
                    default: return "光の温室";
                }
            }
        }
    }
}
