using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    public sealed class EcologyVisit
    {
        public string Kind;
        public List<int> Cells = new List<int>();
    }

    public sealed class EcologyEffect
    {
        public string Kind;   // which resident caused it
        public int CellId;
        public int Amount;    // growth added; 0 for a carried seed
        public bool Seed;
    }

    public sealed class EcologyResult
    {
        public WorldState PostState;
        public List<EcologyVisit> Visits = new List<EcologyVisit>();
        public List<EcologyEffect> Effects = new List<EcologyEffect>();
        public List<GardenEvent> Events = new List<GardenEvent>();
        public bool Exhausted;

        public bool Has(string kind) => Visits.Exists(v => v.Kind == kind);
        public EcologyVisit Visit(string kind) => Visits.Find(v => v.Kind == kind);
    }

    public sealed class EcologyContext
    {
        public string RoundId;
        public string CurrentRecipeId;
        public IList<string> RecentRecipeIds;   // oldest first, including this roll
        public bool PrayerAnswered;
        public SimulationResult Sim;            // this roll's natural simulation
        public WeatherDefinition Weather;
    }

    /// <summary>
    /// ecology.v1 (docs/unity-garden/ECOLOGY.md). Runs after the natural simulation.
    /// Residents are derived from the post-simulation state in a fixed order; their chance
    /// comes only from the saved ecology bytes. Water is never moved.
    /// </summary>
    public static class Ecology
    {
        public const string RulesVersion = "ecology.v1";
        public const int MaxSeedsPerRoll = 2;
        public const string Bee = "bee", Butterfly = "butterfly", Spirit = "spirit", Moss = "moss",
            Firefly = "firefly", Mushroom = "mushroom", Worm = "worm", Dragonfly = "dragonfly",
            Bird = "bird", Fluff = "fluff", Fish = "fish", Petals = "petals", Dew = "dew";

        private static int X(int c) => c % 16;
        private static int Y(int c) => c / 16;
        private static int Dist(int a, int b) => Math.Abs(X(a) - X(b)) + Math.Abs(Y(a) - Y(b));

        private static bool IsWet(WorldState s, int c) =>
            GardenMapData.SoilCapacity[c] > 0 && s.SoilWater[c] * 100 >= 60 * GardenMapData.SoilCapacity[c];
        /// <summary>Rock shade: soil touching a rock (N/E/S/W), or with reduced light exposure.</summary>
        private static bool IsShade(int c)
        {
            if (GardenMapData.LightExposureQ[c] < 1000) return true;
            int x = X(c), y = Y(c);
            return (x > 0 && GardenMapData.Solid[c - 1] == 1) || (x < 15 && GardenMapData.Solid[c + 1] == 1) ||
                   (y > 0 && GardenMapData.Solid[c - 16] == 1) || (y < 15 && GardenMapData.Solid[c + 16] == 1);
        }
        private static bool IsMoistShade(WorldState s, int c) =>
            GardenMapData.SoilCapacity[c] > 0 && IsShade(c) && s.SoilWater[c] * 100 >= 55 * GardenMapData.SoilCapacity[c];
        private static bool IsEmptyPlantable(WorldState s, int c) =>
            GardenMapData.Plantable[c] == 1 && s.PlantAt(c) == null;

        private static List<int> Flowers(WorldState s)
        {
            var list = new List<int>();
            foreach (var p in s.Plants) if (p.Stage == PlantStage.Flower) list.Add(p.CellId);
            list.Sort();
            return list;
        }

        /// <summary>
        /// Residents that live in the garden because of its state (no chance involved).
        /// Used by the ecology step and by the view between rolls.
        /// </summary>
        public static List<EcologyVisit> Presence(WorldState s, WeatherDefinition weather,
            IList<string> recentRecipeIds, string currentRecipeId)
        {
            var visits = new List<EcologyVisit>();
            var flowers = Flowers(s);

            if (flowers.Count > 0)
                visits.Add(new EcologyVisit { Kind = Bee, Cells = flowers.GetRange(0, Math.Min(3, flowers.Count)) });
            if (flowers.Count > 0 && NatureBalance.AllElementsWithinWindow(recentRecipeIds ?? new string[0]))
                visits.Add(new EcologyVisit { Kind = Butterfly, Cells = flowers.GetRange(0, Math.Min(3, flowers.Count)) });

            var moss = new List<int>();
            for (int c = 0; c < GardenMapData.CellCount; c++)
                if (IsMoistShade(s, c)) moss.Add(c);
            if (moss.Count >= 2)
            {
                visits.Add(new EcologyVisit { Kind = Moss, Cells = moss.GetRange(0, Math.Min(6, moss.Count)) });
                var mossCells = moss.GetRange(0, Math.Min(6, moss.Count));
                if (weather != null && weather.Id != WeatherId.Clear)
                    visits.Add(new EcologyVisit { Kind = Firefly, Cells = new List<int>(mossCells) });
                if (s.Plants.Count >= 6)
                {
                    var caps = new List<int>();
                    foreach (int c in mossCells) if (s.PlantAt(c) == null && caps.Count < 3) caps.Add(c);
                    if (caps.Count > 0) visits.Add(new EcologyVisit { Kind = Mushroom, Cells = caps });
                }
            }

            var wetPlants = new List<int>();
            foreach (var p in s.Plants) if (IsWet(s, p.CellId)) wetPlants.Add(p.CellId);
            wetPlants.Sort();
            if (wetPlants.Count >= 4) visits.Add(new EcologyVisit { Kind = Worm, Cells = wetPlants.GetRange(0, 4) });

            var puddles = new List<int>();
            for (int c = 0; c < GardenMapData.CellCount; c++) if (s.SurfaceWater[c] > 0) puddles.Add(c);
            if (puddles.Count >= 2) visits.Add(new EcologyVisit { Kind = Dragonfly, Cells = puddles.GetRange(0, Math.Min(4, puddles.Count)) });

            int insects = 0;
            foreach (var v in visits)
                if (v.Kind == Bee || v.Kind == Butterfly || v.Kind == Firefly || v.Kind == Dragonfly) insects++;
            if (insects >= 2) visits.Add(new EcologyVisit { Kind = Bird });

            bool windy = (currentRecipeId != null && RecipeCatalog.ElementOf(currentRecipeId) == ElementId.Wind)
                         || (weather != null && weather.BaseWindQ >= 300);
            if (windy && flowers.Count > 0) visits.Add(new EcologyVisit { Kind = Fluff, Cells = new List<int> { flowers[0] } });
            if (weather != null && weather.Id == WeatherId.Drizzle) visits.Add(new EcologyVisit { Kind = Dew });
            return visits;
        }

        public static EcologyResult Apply(WorldState afterSim, EcologyContext ctx, byte[] ecologyBytes,
            Func<int, string> newPlantId)
        {
            var s = afterSim.Clone();
            var r = new EcologyResult { PostState = s };
            var buf = new EntropyBuffer(ecologyBytes ?? new byte[0]);
            int seeds = 0, idCounter = 0;
            var stageEvents = new SimulationResult();

            bool Chance(int n)
            {
                if (r.Exhausted) return false;
                int v = buf.UniformBelow(n);
                if (v < 0) { r.Exhausted = true; return false; }
                return v == 0;
            }
            int Pick(int count)
            {
                if (r.Exhausted || count <= 0) return -1;
                int v = buf.UniformBelow(count);
                if (v < 0) r.Exhausted = true;
                return v;
            }
            void PlantSeed(string kind, List<int> candidates)
            {
                if (seeds >= MaxSeedsPerRoll || candidates.Count == 0) return;
                candidates.Sort();
                int i = Pick(candidates.Count);
                if (i < 0) return;
                int cell = candidates[i];
                var plant = new PlantState
                {
                    PlantId = newPlantId != null ? newPlantId(idCounter++) : "eco-" + cell,
                    CellId = cell, Growth = 0, GrowthRemainder = 0, Stage = PlantStage.Seed,
                    OriginKind = "ecology", SeedSourceRoundId = ctx.RoundId
                };
                s.Plants.Add(plant);
                seeds++;
                r.Effects.Add(new EcologyEffect { Kind = kind, CellId = cell, Seed = true });
                r.Events.Add(new GardenEvent { Kind = "SeedCarried", PlantId = plant.PlantId, CellId = cell, Tick = 40 });
            }
            void Nourish(string kind, int cell, int amount)
            {
                var plant = s.PlantAt(cell);
                if (plant == null || plant.Growth >= 2500) return;
                int old = plant.Growth;
                plant.Growth = Math.Min(2500, plant.Growth + amount);
                if (plant.Growth >= 2500) plant.GrowthRemainder = 0;
                Simulation.EmitStageEvents(stageEvents, plant, old, 40);
                r.Effects.Add(new EcologyEffect { Kind = kind, CellId = cell, Amount = plant.Growth - old });
            }
            List<int> EmptyWithin(IEnumerable<int> origins, int minD, int maxD, Func<int, int, bool> extra = null)
            {
                var set = new SortedSet<int>();
                foreach (int o in origins)
                    for (int c = 0; c < GardenMapData.CellCount; c++)
                    {
                        int d = Dist(o, c);
                        if (d < minD || d > maxD || !IsEmptyPlantable(s, c)) continue;
                        if (extra != null && !extra(o, c)) continue;
                        set.Add(c);
                    }
                return new List<int>(set);
            }

            // Residents are judged on the state the natural simulation produced.
            var presence = Presence(afterSim, ctx.Weather, ctx.RecentRecipeIds, ctx.CurrentRecipeId);
            EcologyVisit Get(string kind) => presence.Find(v => v.Kind == kind);

            // 1. Bees: each visited flower, 1/3 to drop a seed next to it (bees: at most one).
            var bee = Get(Bee);
            if (bee != null)
            {
                r.Visits.Add(bee);
                bool dropped = false;
                foreach (int f in bee.Cells)
                {
                    if (dropped) break;
                    if (Chance(3)) { int before = seeds; PlantSeed(Bee, EmptyWithin(new[] { f }, 1, 1)); dropped = seeds > before; }
                }
            }
            // 2. Butterflies: 1/2 to carry pollen 2..4 cells away.
            var butterfly = Get(Butterfly);
            if (butterfly != null)
            {
                r.Visits.Add(butterfly);
                if (Chance(2)) PlantSeed(Butterfly, EmptyWithin(butterfly.Cells, 2, 4));
            }
            // 3. Spirits of an answered prayer: +120 to up to three reached plants (not flowers).
            if (ctx.PrayerAnswered && ctx.Sim != null)
            {
                var cells = new List<int>();
                foreach (int c in PrayerSpirits.ReachedPlantCells(ctx.Sim))
                {
                    var p = s.PlantAt(c);
                    if (p != null && p.Stage != PlantStage.Flower && cells.Count < 3) cells.Add(c);
                }
                r.Visits.Add(new EcologyVisit { Kind = Spirit, Cells = cells });
                foreach (int c in cells) Nourish(Spirit, c, 120);
            }
            // 4-6. Glowing moss, fireflies (+40 near the moss), mushrooms (+60 next to them).
            var moss = Get(Moss);
            if (moss != null)
            {
                r.Visits.Add(moss);
                var firefly = Get(Firefly);
                if (firefly != null)
                {
                    r.Visits.Add(firefly);
                    var near = new SortedSet<int>();
                    foreach (var p in s.Plants)
                        foreach (int m in moss.Cells)
                            if (Dist(p.CellId, m) <= 2) { near.Add(p.CellId); break; }
                    int n = 0;
                    foreach (int c in near) { if (n++ == 4) break; Nourish(Firefly, c, 40); }
                }
                var mushroom = Get(Mushroom);
                if (mushroom != null)
                {
                    r.Visits.Add(mushroom);
                    var near = new SortedSet<int>();
                    foreach (var p in s.Plants)
                        foreach (int m in mushroom.Cells)
                            if (Dist(p.CellId, m) == 1) { near.Add(p.CellId); break; }
                    int n = 0;
                    foreach (int c in near) { if (n++ == 3) break; Nourish(Mushroom, c, 60); }
                }
            }
            // 7. Earthworms: +30 to the four youngest plants on wet soil.
            var worm = Get(Worm);
            if (worm != null)
            {
                var wet = new List<PlantState>();
                foreach (var p in s.Plants) if (IsWet(afterSim, p.CellId)) wet.Add(p);
                wet.Sort((a, b) => a.Growth != b.Growth ? a.Growth.CompareTo(b.Growth) : a.CellId.CompareTo(b.CellId));
                var cells = new List<int>();
                for (int i = 0; i < wet.Count && i < 4; i++) cells.Add(wet[i].CellId);
                r.Visits.Add(new EcologyVisit { Kind = Worm, Cells = cells });
                foreach (int c in cells) Nourish(Worm, c, 30);
            }
            // 8. Dragonflies over puddles (no direct effect; they call the bird).
            var dragonfly = Get(Dragonfly);
            if (dragonfly != null) r.Visits.Add(dragonfly);
            // 9. A small bird: 1/2 to drop a seed anywhere in the garden.
            var bird = Get(Bird);
            if (bird != null)
            {
                r.Visits.Add(bird);
                if (Chance(2))
                {
                    var any = new List<int>();
                    for (int c = 0; c < GardenMapData.CellCount; c++) if (IsEmptyPlantable(s, c)) any.Add(c);
                    PlantSeed(Bird, any);
                }
            }
            // 10. Fluff on the wind: 1/2 to fly 2..5 cells east (same row or one row off).
            var fluff = Get(Fluff);
            if (fluff != null)
            {
                r.Visits.Add(fluff);
                if (Chance(2))
                {
                    var flowers = Flowers(s);
                    int fi = Pick(flowers.Count);
                    if (fi >= 0)
                        PlantSeed(Fluff, EmptyWithin(new[] { flowers[fi] }, 2, 6,
                            (o, c) => X(c) - X(o) >= 2 && X(c) - X(o) <= 5 && Math.Abs(Y(c) - Y(o)) <= 1));
                }
            }
            // 11-13. Presentation-only residents.
            if (ctx.Sim != null && ctx.Sim.Discharged > 0) r.Visits.Add(new EcologyVisit { Kind = Fish });
            if (Flowers(s).Count > 0 && ctx.CurrentRecipeId != null &&
                RecipeCatalog.ElementOf(ctx.CurrentRecipeId) == ElementId.Wind)
                r.Visits.Add(new EcologyVisit { Kind = Petals });
            var dew = Get(Dew);
            if (dew != null) r.Visits.Add(dew);

            r.Events.AddRange(stageEvents.Events);
            return r;
        }

        /// <summary>Journal payload for Resolved.ecology.</summary>
        public static Dictionary<string, object> ToPayload(EcologyResult r)
        {
            var visits = new List<object>();
            foreach (var v in r.Visits)
            {
                var cells = new List<object>();
                foreach (int c in v.Cells) cells.Add((long)c);
                visits.Add(new Dictionary<string, object> { ["kind"] = v.Kind, ["cells"] = cells });
            }
            var effects = new List<object>();
            foreach (var e in r.Effects)
                effects.Add(new Dictionary<string, object>
                {
                    ["kind"] = e.Kind, ["cellId"] = (long)e.CellId, ["amount"] = (long)e.Amount, ["seed"] = e.Seed
                });
            return new Dictionary<string, object>
            {
                ["rulesVersion"] = RulesVersion,
                ["visits"] = visits,
                ["effects"] = effects,
                ["exhausted"] = r.Exhausted
            };
        }
    }
}
