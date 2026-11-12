using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    public sealed class GardenEvent
    {
        public string Kind;      // SeedPlaced / Germinated / LeafOpened / BudFormed / Bloomed
        public string PlantId;
        public int CellId;
        public int Tick;         // 1-based tick after update (seed placement uses 0)
    }

    public sealed class SimulationResult
    {
        public WorldState PostState;
        public List<GardenEvent> Events = new List<GardenEvent>();
        public long DiceWaterInjected;
        public long WeatherWaterInjected;
        public long Discharged;
        public long Evaporated;
        public long WaterBalanceResidual; // must be 0
    }

    /// <summary>
    /// Integer garden simulation (simulation-profile-v0.2 + weather v0.4 composition).
    /// One round = 40 ticks. Update order per tick: injection, fields, simultaneous
    /// flow, outlet, infiltration, evaporation, growth, events.
    /// </summary>
    public static class Simulation
    {
        public const int TicksPerRound = 40;
        private const int Conductance = 125;
        private const int SpillDepth = 150;
        private const int OutletCapacityPerTick = 100;

        public static SimulationResult Resolve(WorldState before, ResolvedRecipe dice,
            WeatherDefinition weather, Func<int, string> newPlantId)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (dice == null) throw new ArgumentNullException(nameof(dice));
            var s = before.Clone();
            var result = new SimulationResult();
            long totalBefore = s.TotalWater();

            // Precompute dice field contributions per cell (extra before exposure/shelter).
            var diceLightExtra = new int[256];
            var diceWindExtra = new int[256];
            if (dice.Element == ElementId.Light)
                foreach (var kv in dice.FieldCells) diceLightExtra[kv.Key] = dice.StrengthQ * kv.Value / 1000;
            if (dice.Element == ElementId.Wind)
                foreach (var kv in dice.FieldCells) diceWindExtra[kv.Key] = dice.StrengthQ * kv.Value / 1000;

            var rainTicks = new HashSet<int>(Weathers.RainTickOffsets);

            for (int tick = 0; tick < TicksPerRound; tick++)
            {
                // 1. Injection: weather rain + dice water + seed placement.
                if (weather.RainPerTargetAtScheduledTick > 0 && rainTicks.Contains(tick))
                {
                    for (int id = 0; id < 256; id++)
                    {
                        if (GardenMapData.Solid[id] == 1) continue;
                        s.SurfaceWater[id] += weather.RainPerTargetAtScheduledTick;
                        result.WeatherWaterInjected += weather.RainPerTargetAtScheduledTick;
                    }
                }
                if (dice.WaterSchedule.TryGetValue(tick, out var injections))
                {
                    foreach (var kv in injections)
                    {
                        s.SurfaceWater[kv.Key] += kv.Value;
                        result.DiceWaterInjected += kv.Value;
                    }
                }
                if (tick == 0 && dice.Element == ElementId.Seed)
                {
                    int n = 0;
                    foreach (int cell in dice.SeedCells)
                    {
                        if (s.PlantAt(cell) != null) continue;
                        var plant = new PlantState
                        {
                            PlantId = newPlantId != null ? newPlantId(n) : ("seed-" + cell),
                            CellId = cell, Growth = 0, GrowthRemainder = 0, Stage = PlantStage.Seed
                        };
                        s.Plants.Add(plant);
                        result.Events.Add(new GardenEvent { Kind = "SeedPlaced", PlantId = plant.PlantId, CellId = cell, Tick = 0 });
                        n++;
                    }
                }

                // 2. Effective fields.
                var effLight = new int[256];
                var effWind = new int[256];
                for (int id = 0; id < 256; id++)
                {
                    if (GardenMapData.Solid[id] == 1) continue;
                    int exposure = GardenMapData.LightExposureQ[id];
                    effLight[id] = Math.Min(1000, weather.BaseLightQ + diceLightExtra[id]) * exposure / 1000;
                    int shelter = RecipeResolver.ShelterQEast(id);
                    effWind[id] = Math.Min(1000, weather.BaseWindQ + diceWindExtra[id]) * shelter / 1000;
                }

                // 3. Simultaneous surface flow.
                ApplyFlow(s);

                // 4. Outlet discharge.
                int drain = GardenMapData.DrainCellId;
                int spillLevel = GardenMapData.CellHeight[drain] + SpillDepth;
                int discharge = Math.Min(s.SurfaceWater[drain],
                    Math.Min(Math.Max(0, GardenMapData.CellHeight[drain] + s.SurfaceWater[drain] - spillLevel),
                        OutletCapacityPerTick));
                s.SurfaceWater[drain] -= discharge;
                result.Discharged += discharge;

                // 5. Infiltration.
                for (int id = 0; id < 256; id++)
                {
                    int cap = GardenMapData.SoilCapacity[id];
                    if (cap <= 0) continue;
                    int amount = Math.Min(s.SurfaceWater[id],
                        Math.Min(cap - s.SoilWater[id], GardenMapData.InfiltrationPerTick[id]));
                    if (amount <= 0) continue;
                    s.SurfaceWater[id] -= amount;
                    s.SoilWater[id] += amount;
                }

                // 6. Evaporation (surface first, then soil).
                for (int id = 0; id < 256; id++)
                {
                    if (GardenMapData.Solid[id] == 1) continue;
                    long available = (long)s.SurfaceWater[id] + s.SoilWater[id];
                    bool dryBefore = available == 0;
                    int numerator = effLight[id] + 4 * effWind[id];
                    int demand = (s.EvaporationRemainder[id] + numerator) / 1000;
                    int nextRemainder = (s.EvaporationRemainder[id] + numerator) % 1000;
                    int actual = (int)Math.Min(demand, available);
                    int fromSurface = Math.Min(actual, s.SurfaceWater[id]);
                    s.SurfaceWater[id] -= fromSurface;
                    s.SoilWater[id] -= (actual - fromSurface);
                    result.Evaporated += actual;
                    bool dryAfter = s.SurfaceWater[id] + s.SoilWater[id] == 0;
                    s.EvaporationRemainder[id] = (dryBefore || dryAfter) ? 0 : nextRemainder;
                }

                // 7. Growth.
                foreach (var plant in s.Plants)
                {
                    int cell = plant.CellId;
                    int cap = GardenMapData.SoilCapacity[cell];
                    if (cap <= 0 || plant.Growth >= 2500) continue;
                    int m = 1000 * s.SoilWater[cell] / cap;
                    int factor;
                    if (m <= 100 || m >= 950) factor = 0;
                    else if (m < 400) factor = (m - 100) * 1000 / 300;
                    else if (m <= 700) factor = 1000;
                    else factor = (950 - m) * 1000 / 250;
                    long numerator = plant.GrowthRemainder + 25L * factor * effLight[cell];
                    int add = (int)(numerator / 1000000L);
                    plant.GrowthRemainder = (int)(numerator % 1000000L);
                    if (add <= 0) continue;
                    int oldGrowth = plant.Growth;
                    plant.Growth = Math.Min(2500, plant.Growth + add);
                    if (plant.Growth >= 2500) plant.GrowthRemainder = 0;
                    EmitStageEvents(result, plant, oldGrowth, tick + 1);
                }
            }

            s.WorldRevision = before.WorldRevision + 1;
            result.PostState = s;
            long totalAfter = s.TotalWater();
            result.WaterBalanceResidual =
                totalBefore + result.DiceWaterInjected + result.WeatherWaterInjected
                - result.Discharged - result.Evaporated - totalAfter;
            return result;
        }

        private static void EmitStageEvents(SimulationResult result, PlantState plant, int oldGrowth, int tick)
        {
            CheckThreshold(result, plant, oldGrowth, 250, PlantStage.Sprout, "Germinated", tick);
            CheckThreshold(result, plant, oldGrowth, 750, PlantStage.Leaf, "LeafOpened", tick);
            CheckThreshold(result, plant, oldGrowth, 1500, PlantStage.Bud, "BudFormed", tick);
            CheckThreshold(result, plant, oldGrowth, 2500, PlantStage.Flower, "Bloomed", tick);
        }

        private static void CheckThreshold(SimulationResult result, PlantState plant, int oldGrowth,
            int threshold, PlantStage stage, string kind, int tick)
        {
            if (oldGrowth < threshold && plant.Growth >= threshold)
            {
                plant.Stage = stage;
                result.Events.Add(new GardenEvent { Kind = kind, PlantId = plant.PlantId, CellId = plant.CellId, Tick = tick });
            }
        }

        private static readonly int[] Dx = { 0, 1, 0, -1 }; // N,E,S,W
        private static readonly int[] Dy = { -1, 0, 1, 0 };

        private static void ApplyFlow(WorldState s)
        {
            var delta = new int[256];
            var requests = new int[4];
            var targets = new int[4];
            var allocated = new int[4];
            for (int id = 0; id < 256; id++)
            {
                if (GardenMapData.Solid[id] == 1 || s.SurfaceWater[id] <= 0) continue;
                int x = id % 16, y = id / 16;
                int headI = GardenMapData.CellHeight[id] + s.SurfaceWater[id];
                int totalRequest = 0;
                for (int d = 0; d < 4; d++)
                {
                    targets[d] = -1; requests[d] = 0;
                    int nx = x + Dx[d], ny = y + Dy[d];
                    if (nx < 0 || nx >= 16 || ny < 0 || ny >= 16) continue;
                    int nid = ny * 16 + nx;
                    if (GardenMapData.Solid[nid] == 1) continue;
                    int headJ = GardenMapData.CellHeight[nid] + s.SurfaceWater[nid];
                    int req = Math.Max(0, headI - headJ) * Conductance / 1000;
                    targets[d] = nid; requests[d] = req; totalRequest += req;
                }
                if (totalRequest <= 0) continue;
                int budget = Math.Min(s.SurfaceWater[id], totalRequest);
                int allocatedSum = 0;
                var remainders = new long[4];
                for (int d = 0; d < 4; d++)
                {
                    long num = (long)requests[d] * budget;
                    allocated[d] = (int)(num / totalRequest);
                    remainders[d] = num % totalRequest;
                    allocatedSum += allocated[d];
                }
                int leftover = budget - allocatedSum;
                var order = new[] { 0, 1, 2, 3 };
                Array.Sort(order, (a, b) => remainders[b] != remainders[a]
                    ? remainders[b].CompareTo(remainders[a]) : a.CompareTo(b));
                foreach (int d in order)
                {
                    if (leftover <= 0) break;
                    if (requests[d] <= allocated[d]) continue;
                    allocated[d]++; leftover--;
                }
                for (int d = 0; d < 4; d++)
                {
                    if (targets[d] < 0 || allocated[d] <= 0) continue;
                    int amount = Math.Min(allocated[d], requests[d]);
                    delta[id] -= amount;
                    delta[targets[d]] += amount;
                }
            }
            for (int id = 0; id < 256; id++) s.SurfaceWater[id] += delta[id];
        }
    }
}
