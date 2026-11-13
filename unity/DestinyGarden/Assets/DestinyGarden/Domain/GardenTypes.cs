using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    public enum ElementId { Water = 0, Wind = 1, Light = 2, Seed = 3 }

    public enum WeatherId { Clear = 0, Cloudy = 1, Drizzle = 2 }

    public enum PlantStage { Seed = 0, Sprout = 1, Leaf = 2, Bud = 3, Flower = 4 }

    public static class RuleVersions
    {
        public const string SimulationProfile = "simulation-profile-v0.2";
        public const string Map = "garden-map.v0.3";
        public const string WeatherPolicy = "weather-episodes.v0.4";
        public const string TargetPolicy = "target-sites.v0.6";
        public const string MappingPolicy = "dynamic-faces.v0.6";
        public const string RunLifecycle = "run-entry.v0.8";
    }

    /// <summary>One plant individual. Growth is the integer model value 0..2500.</summary>
    public sealed class PlantState
    {
        public string PlantId;
        public int CellId;
        public int Growth;
        public int GrowthRemainder;
        public PlantStage Stage;
        public string OriginKind = "initialMap"; // initialMap | seedInput | ecology | vegetative
        public string SeedSourceRoundId;         // set only for seedInput plants
        public string Species;                   // ecology.v3 (Flora); null until the first ecology step sees it
        public int BloomRolls;                   // lifecycle.v1: rolls spent in flower (saved only when > 0)
        public bool Pollinated;                  // lifecycle.v2: a bee or butterfly visited it in flower (saved only when true)

        public PlantState Clone() => new PlantState
        {
            PlantId = PlantId, CellId = CellId, Growth = Growth,
            GrowthRemainder = GrowthRemainder, Stage = Stage,
            OriginKind = OriginKind, SeedSourceRoundId = SeedSourceRoundId, Species = Species,
            BloomRolls = BloomRolls, Pollinated = Pollinated
        };
    }

    /// <summary>Dynamic garden state. Static terrain data lives in GardenMapData.</summary>
    public sealed class WorldState
    {
        public int WorldRevision;
        public EcosystemState Ecosystem; // ecology.v4; absent in older gardens
        public int[] SoilWater = new int[GardenMapData.CellCount];
        public int[] SurfaceWater = new int[GardenMapData.CellCount];
        public int[] EvaporationRemainder = new int[GardenMapData.CellCount];
        public List<PlantState> Plants = new List<PlantState>();

        public static WorldState CreateInitial()
        {
            var s = new WorldState { WorldRevision = 0 };
            for (int i = 0; i < GardenMapData.CellCount; i++)
                s.SoilWater[i] = GardenMapData.InitialSoilWater[i];
            s.Plants.Add(new PlantState { PlantId = "initial-light-bud", CellId = 135, Growth = 1700, Stage = PlantStage.Bud });
            s.Plants.Add(new PlantState { PlantId = "initial-shadow-bud", CellId = 136, Growth = 1700, Stage = PlantStage.Bud });
            s.Plants.Add(new PlantState { PlantId = "initial-wind-sheltered", CellId = 122, Growth = 500, Stage = PlantStage.Sprout });
            s.Plants.Add(new PlantState { PlantId = "initial-wind-exposed", CellId = 154, Growth = 500, Stage = PlantStage.Sprout });
            return s;
        }

        public WorldState Clone()
        {
            var c = new WorldState { WorldRevision = WorldRevision, Ecosystem = Ecosystem?.Clone() };
            Array.Copy(SoilWater, c.SoilWater, SoilWater.Length);
            Array.Copy(SurfaceWater, c.SurfaceWater, SurfaceWater.Length);
            Array.Copy(EvaporationRemainder, c.EvaporationRemainder, EvaporationRemainder.Length);
            foreach (var p in Plants) c.Plants.Add(p.Clone());
            return c;
        }

        public PlantState PlantAt(int cellId)
        {
            foreach (var p in Plants) if (p.CellId == cellId) return p;
            return null;
        }

        public long TotalWater()
        {
            long t = 0;
            for (int i = 0; i < GardenMapData.CellCount; i++) t += SoilWater[i] + SurfaceWater[i];
            return t;
        }
    }

    public sealed class SiteDefinition
    {
        public string SiteId;
        public string LabelJa;
        public int CenterCellId;
    }

    public static class Sites
    {
        // Fixed order inlet / shade / meadow (target-sites.v0.6).
        public static readonly SiteDefinition[] All =
        {
            new SiteDefinition { SiteId = "inlet", LabelJa = "入口", CenterCellId = 85 },
            new SiteDefinition { SiteId = "shade", LabelJa = "岩陰", CenterCellId = 135 },
            new SiteDefinition { SiteId = "meadow", LabelJa = "ひらけた土", CenterCellId = 154 },
        };

        public static SiteDefinition ById(string siteId)
        {
            foreach (var s in All) if (s.SiteId == siteId) return s;
            throw new ArgumentException("Unknown siteId: " + siteId);
        }
    }

    public sealed class WeatherDefinition
    {
        public WeatherId Id;
        public string LabelJa;
        public int BaseLightQ;
        public int BaseWindQ;
        public int RainPerTargetAtScheduledTick;
    }

    public static class Weathers
    {
        // Fixed order clear / cloudy / drizzle (weather-episodes.v0.4).
        public static readonly WeatherDefinition[] All =
        {
            new WeatherDefinition { Id = WeatherId.Clear, LabelJa = "晴れ", BaseLightQ = 500, BaseWindQ = 0, RainPerTargetAtScheduledTick = 0 },
            new WeatherDefinition { Id = WeatherId.Cloudy, LabelJa = "薄曇り", BaseLightQ = 350, BaseWindQ = 100, RainPerTargetAtScheduledTick = 0 },
            new WeatherDefinition { Id = WeatherId.Drizzle, LabelJa = "小雨", BaseLightQ = 250, BaseWindQ = 0, RainPerTargetAtScheduledTick = 1 },
        };
        public static readonly int[] RainTickOffsets = { 0, 4, 8, 12, 16, 20, 24, 28, 32, 36 };
        public const int RoundsPerEpisode = 3;
    }
}
