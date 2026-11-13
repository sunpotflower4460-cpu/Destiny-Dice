using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>Optional v5 memory. Its absence preserves the exact v4 state and behaviour.</summary>
    public sealed class SuccessionState
    {
        public CommunityState Community; // absent in saved v5 gardens
        public List<RestingSeed> SeedBank = new List<RestingSeed>();
        public Dictionary<string, FlowerResource> Flowers = new Dictionary<string, FlowerResource>(StringComparer.Ordinal);
        public Dictionary<string, PlantAncestry> Ancestry = new Dictionary<string, PlantAncestry>(StringComparer.Ordinal);
        public SuccessionState Clone()
        {
            var s = new SuccessionState { Community = Community?.Clone() };
            foreach (var seed in SeedBank) s.SeedBank.Add(seed.Clone());
            foreach (var pair in Flowers) s.Flowers.Add(pair.Key, pair.Value.Clone());
            foreach (var pair in Ancestry) s.Ancestry.Add(pair.Key, pair.Value.Clone());
            return s;
        }
    }

    public sealed class RestingSeed
    {
        public string SeedId, Species, ParentPlantId, SourceRoundId, Carrier;
        public int CellId, ParentCellId, Age, BornRevision;
        public bool Rested;
        public RestingSeed Clone() => (RestingSeed)MemberwiseClone();
    }

    public sealed class FlowerResource
    {
        public int Nectar, LastForaged;
        public FlowerResource Clone() => (FlowerResource)MemberwiseClone();
    }

    public sealed class PlantAncestry
    {
        public string ParentPlantId, SeedId, Carrier;
        public int BornRevision;
        public PlantAncestry Clone() => (PlantAncestry)MemberwiseClone();
    }

    public sealed class SeedStrategy
    {
        public int MinimumAge = 2, Persistence = 96;
        public bool NeedsRestSeason;
    }

    public static class SeedStrategies
    {
        private static readonly SeedStrategy General = new SeedStrategy();
        private static readonly Dictionary<string, SeedStrategy> All = new Dictionary<string, SeedStrategy>
        {
            ["poppy"] = new SeedStrategy { Persistence = 64 },
            ["cosmos"] = new SeedStrategy { Persistence = 64 },
            ["sunflower"] = new SeedStrategy { Persistence = 80 },
            ["goldenrod"] = new SeedStrategy { Persistence = 128 },
            ["clover"] = new SeedStrategy { Persistence = 144 },
            ["primrose"] = new SeedStrategy { Persistence = 160, NeedsRestSeason = true },
            ["violet"] = new SeedStrategy { Persistence = 160, NeedsRestSeason = true },
            ["anemone"] = new SeedStrategy { Persistence = 160, NeedsRestSeason = true }
        };
        public static SeedStrategy Get(string species) => species != null && All.TryGetValue(species, out var t) ? t : General;
    }
}
