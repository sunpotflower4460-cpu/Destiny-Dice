using System;
using System.Collections.Generic;
using DestinyGarden.Application;
using DestinyGarden.Domain;

namespace DestinyGarden.Presentation
{
    public enum CottageStage { Cabin, Porch, Extension, Finished, Styled }
    public enum CottageStyle { None, RainHouse, Windmill, Observatory, GreenRoof }

    /// <summary>The house is a projection of this garden's committed rounds.</summary>
    public readonly struct CottageGrowth
    {
        public const int PorchAt = 4;
        public const int ExtensionAt = 10;
        public const int FinishAt = 18;
        public const int StyleAt = 24;

        public CottageStage Stage { get; }
        public ElementId Extension { get; }
        public CottageStyle Style { get; }

        private CottageGrowth(CottageStage stage, ElementId extension, CottageStyle style = CottageStyle.None)
        {
            Stage = stage;
            Extension = extension;
            Style = style;
        }

        public static CottageGrowth Project(int revision, IReadOnlyList<RoundSummary> rounds)
        {
            if (revision < ExtensionAt)
                return new CottageGrowth(revision < PorchAt ? CottageStage.Cabin : CottageStage.Porch, ElementId.Light);

            ElementId wing = LeadingElement(rounds, 1, ExtensionAt, ElementId.Light);
            if (revision < StyleAt)
                return new CottageGrowth(revision < FinishAt ? CottageStage.Extension : CottageStage.Finished, wing);

            // Let the next chapter of the garden shape the roof without replacing its earlier wing.
            ElementId roof = LeadingElement(rounds, FinishAt + 1, StyleAt, wing);
            return new CottageGrowth(CottageStage.Styled, wing, roof switch
            {
                ElementId.Water => CottageStyle.RainHouse,
                ElementId.Wind => CottageStyle.Windmill,
                ElementId.Seed => CottageStyle.GreenRoof,
                _ => CottageStyle.Observatory
            });
        }

        // Ties go to the family whose first resolved roll arrived earliest in this window.
        private static ElementId LeadingElement(IReadOnlyList<RoundSummary> rounds, int start, int end, ElementId fallback)
        {
            var counts = new int[4];
            var first = new[] { int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue };
            foreach (var round in rounds)
            {
                if (!round.Resolved || round.WorldRevision < start || round.WorldRevision > end ||
                    Array.IndexOf(RecipeCatalog.Order, round.RecipeId) < 0)
                    continue;

                int element = (int)RecipeCatalog.ElementOf(round.RecipeId);
                counts[element]++;
                first[element] = Math.Min(first[element], round.WorldRevision);
            }

            int leader = (int)fallback;
            for (int i = 0; i < counts.Length; i++)
                if (counts[i] > counts[leader] || (counts[i] == counts[leader] && first[i] < first[leader]))
                    leader = i;

            return (ElementId)leader;
        }

        public string Label
        {
            get
            {
                if (Stage == CottageStage.Cabin) return "小さな家";
                if (Stage == CottageStage.Porch) return "縁側のある家";
                switch (Style)
                {
                    case CottageStyle.RainHouse: return "雨音の家";
                    case CottageStyle.Windmill: return "風車の家";
                    case CottageStyle.Observatory: return "星見の家";
                    case CottageStyle.GreenRoof: return "草屋根の家";
                }
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
