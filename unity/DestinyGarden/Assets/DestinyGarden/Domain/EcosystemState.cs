using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>Saved ecological memory. Null in pre-v4 gardens; never inferred on restore.</summary>
    public sealed class EcosystemState
    {
        public SuccessionState Succession; // null for gardens created under v4
        public int[] Litter = new int[GardenMapData.CellCount];
        public int[] Nutrients = new int[GardenMapData.CellCount];
        public int[] WetAge = new int[GardenMapData.CellCount];
        public int[] CoverAge = new int[GardenMapData.CellCount];
        public int[] Aphids = new int[GardenMapData.CellCount];
        public Dictionary<string, PlantCycle> Cycles = new Dictionary<string, PlantCycle>(StringComparer.Ordinal);

        public static EcosystemState CreateInitial()
        {
            var e = new EcosystemState();
            for (int c = 0; c < GardenMapData.CellCount; c++)
                if (GardenMapData.SoilCapacity[c] > 0) e.Nutrients[c] = 18;
            return e;
        }

        public static EcosystemState CreateCurrent()
        {
            var e = CreateInitial(); e.Succession = new SuccessionState(); return e;
        }

        public EcosystemState Clone()
        {
            var e = new EcosystemState
            {
                Succession = Succession?.Clone(), Litter = (int[])Litter.Clone(), Nutrients = (int[])Nutrients.Clone(),
                WetAge = (int[])WetAge.Clone(), CoverAge = (int[])CoverAge.Clone(), Aphids = (int[])Aphids.Clone()
            };
            foreach (var kv in Cycles) e.Cycles.Add(kv.Key, kv.Value.Clone());
            return e;
        }
    }

    public sealed class PlantCycle
    {
        public int Age, Stress, BloomAge, Pollen, Seeds;
        public bool Fruited;
        public PlantCycle Clone() => (PlantCycle)MemberwiseClone();
    }

    public sealed class EcologicalLink
    {
        public string Kind, Actor, Species;
        public int FromCell, ToCell, Amount;
    }

    /// <summary>Life strategies used by the model and its field guide, not botanical measurements.</summary>
    public sealed class LifeTraits
    {
        public int Height = 1;              // 0 ground cover, 1 herb, 2 tall canopy
        public int Nitrogen;                // nitrogen fixation per step, requires active growth
        public int BloomRounds = 9;
        public int Longevity = 120;
        public int Season = -1;             // preferred flowering phase; -1 all phases
        public bool SelfFertile;
        public bool FungalPartner = true;
    }

    public static class PlantStrategies
    {
        private static readonly LifeTraits General = new LifeTraits();
        private static readonly Dictionary<string, LifeTraits> Traits = new Dictionary<string, LifeTraits>
        {
            ["cosmos"] = new LifeTraits { Longevity = 72, Season = 2, SelfFertile = true },
            ["poppy"] = new LifeTraits { Longevity = 64, BloomRounds = 6, Season = 0, SelfFertile = true },
            ["sunflower"] = new LifeTraits { Height = 2, Longevity = 80, BloomRounds = 12, Season = 1 },
            ["foxglove"] = new LifeTraits { Height = 2, Season = 1 },
            ["iris"] = new LifeTraits { Season = 0, BloomRounds = 8 },
            ["wildrose"] = new LifeTraits { Height = 2, Longevity = 180, BloomRounds = 12 },
            ["dandelion"] = new LifeTraits { Height = 0, SelfFertile = true, BloomRounds = 7 },
            ["strawberry"] = new LifeTraits { Height = 0, BloomRounds = 12, Season = 0 },
            ["clover"] = new LifeTraits { Height = 0, Nitrogen = 2, BloomRounds = 12, Season = 1 },
            ["vetch"] = new LifeTraits { Nitrogen = 3, Longevity = 88, Season = 0 },
            ["yarrow"] = new LifeTraits { BloomRounds = 14, Season = 1 },
            ["thyme"] = new LifeTraits { Height = 0, BloomRounds = 14, Longevity = 160 },
            ["mint"] = new LifeTraits { BloomRounds = 12, Season = 1 },
            ["primrose"] = new LifeTraits { Height = 0, Season = 0, BloomRounds = 10 },
            ["violet"] = new LifeTraits { Height = 0, SelfFertile = true, Season = 0 },
            ["loosestrife"] = new LifeTraits { Height = 2, FungalPartner = false, Season = 1, BloomRounds = 14 },
            ["goldenrod"] = new LifeTraits { Height = 2, Season = 2, BloomRounds = 14 },
        };
        public static LifeTraits Get(string id) => id != null && Traits.TryGetValue(id, out var t) ? t : General;
        public static string SeasonLabel(int season) => new[] { "芽吹き", "花盛り", "実り", "休息" }[Math.Max(0, season) % 4];
        public static int SeasonAt(WorldState s) => s.WorldRevision / 24 % 4;
        public static string Describe(string species)
        {
            var t = Get(species);
            var parts = new List<string>();
            if (t.Nitrogen > 0) parts.Add("根粒菌と窒素を土へ補う");
            if (t.Height == 0) parts.Add("地表を覆い、小さな命の隠れ場所に");
            if (t.Height == 2) parts.Add("育つと隣に木陰をつくる");
            parts.Add(t.SelfFertile ? "自家結実もできる" : "訪花昆虫の受粉で結実");
            if (t.Season >= 0) parts.Add(SeasonLabel(t.Season) + "の頃に種が実りやすい");
            if (!t.FungalPartner) parts.Add("湿地で単独の根を張る");
            return string.Join("・", parts);
        }
    }
}
