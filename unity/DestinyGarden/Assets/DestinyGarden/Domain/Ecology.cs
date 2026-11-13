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
        public string Species;  // for "germinate": the species the plant became (ecology.v3)
        public bool Softened;   // for "germinate": only possible because softening made room
    }

    public sealed class EcologyResult
    {
        public WorldState PostState;
        public List<EcologyVisit> Visits = new List<EcologyVisit>();
        public List<EcologyEffect> Effects = new List<EcologyEffect>();
        public List<GardenEvent> Events = new List<GardenEvent>();
        public List<EcologicalLink> Relations = new List<EcologicalLink>(); // ecology.v4: observed causes
        public CareReport Care; // v8: measured local dice effects; absent from earlier records
        public bool Exhausted;
        public HarmonyReading Harmony;                              // ecology.v2
        public List<List<int>> Networks = new List<List<int>>();    // mycorrhizal networks (cells), ecology.v2
        public PrayerStarReading PrayerStar;                        // ecology.v4; null when the face was not given

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
        public ResolvedRecipe Dice;
        public CareMeasure Care;
        public int FaceId;                      // face rolled this time (1..7); 0 skips the prayer-star rules
        public int PreviousFaceId;              // face of the previous resolved roll in this run; 0 for none
    }

    /// <summary>
    /// ecology.v1 (docs/unity-garden/ECOLOGY.md). Runs after the natural simulation.
    /// Residents are derived from the post-simulation state in a fixed order; their chance
    /// comes only from the saved ecology bytes. Water is never moved.
    /// </summary>
    public static class Ecology
    {
        public const string RulesVersion = "ecology.v3";
        private const string LegacyPrayerRulesVersion = "ecology.v4";
        public const string Germinate = "germinate", Thrive = "thrive";
        public const string Mycorrhiza = "mycorrhiza";
        public const int MycorrhizaReach = 2;        // manhattan distance that links two plants
        public const int MycorrhizaMinNetwork = 3;   // a network shares only with at least three plants
        public const int MycorrhizaRecipients = 3;   // youngest non-flowering plants that receive
        public const int MycorrhizaPerHarmony = 3;   // growth per harmony point (0..24)
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

        /// <summary>Flowers with affinity >= 1 for a visitor, most loved first. Equally loved flowers take turns
        /// from roll to roll (lifecycle.v2: the order turns with the world revision), so every flower can be visited.
        /// Plants without a species yet count as liked (1).</summary>
        private static List<int> Favoured(WorldState s, List<int> flowers, Func<SpeciesTraits, int> affinity, bool rotate)
        {
            var scored = new List<KeyValuePair<int, int>>();
            foreach (int c in flowers)
            {
                var t = Flora.Get(s.PlantAt(c).Species);
                int a = t == null ? 1 : affinity(t);
                if (a >= 1) scored.Add(new KeyValuePair<int, int>(c, a));
            }
            int turn = s.WorldRevision * 7;
            int Turn(int c) => (c * 37 + turn) & 255;
            scored.Sort((a, b) =>
            {
                int affinityOrder = b.Value.CompareTo(a.Value);
                if (affinityOrder != 0) return affinityOrder;
                return rotate ? Turn(a.Key).CompareTo(Turn(b.Key)) : a.Key.CompareTo(b.Key);
            });
            var list = new List<int>();
            foreach (var kv in scored) list.Add(kv.Key);
            return list;
        }

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
            IList<string> recentRecipeIds, string currentRecipeId) =>
            Presence(s, weather, recentRecipeIds, currentRecipeId, false);

        private static List<EcologyVisit> Presence(WorldState s, WeatherDefinition weather,
            IList<string> recentRecipeIds, string currentRecipeId, bool rotateFlowers)
        {
            if (s.Ecosystem != null) return LivingEcosystem.Presence(s, weather, currentRecipeId);
            var visits = new List<EcologyVisit>();
            var flowers = Flowers(s);

            // Bees go to flowers they favour, butterflies only to flowers they like (ecology.v3).
            var beeFlowers = Favoured(s, flowers, t => t.Bee, rotateFlowers);
            var butterflyFlowers = Favoured(s, flowers, t => t.Butterfly, rotateFlowers);
            if (beeFlowers.Count > 0)
                visits.Add(new EcologyVisit { Kind = Bee, Cells = beeFlowers.GetRange(0, Math.Min(3, beeFlowers.Count)) });
            if (butterflyFlowers.Count > 0 && NatureBalance.AllElementsWithinWindow(recentRecipeIds ?? new string[0]))
                visits.Add(new EcologyVisit { Kind = Butterfly, Cells = butterflyFlowers.GetRange(0, Math.Min(3, butterflyFlowers.Count)) });

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
            if (afterSim.Ecosystem != null) return LivingEcosystem.Apply(afterSim, ctx, ecologyBytes, newPlantId);
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
            var carriedBy = new Dictionary<int, string>();
            var parentOf = new Dictionary<int, int>();
            void PlantSeed(string kind, List<int> candidates, IList<int> parents = null)
            {
                if (seeds >= MaxSeedsPerRoll || candidates.Count == 0) return;
                candidates.Sort();
                int i = Pick(candidates.Count);
                if (i < 0) return;
                int cell = candidates[i];
                carriedBy[cell] = kind;
                if (parents != null && parents.Count > 0)
                {
                    int best = parents[0];
                    foreach (int pc in parents) if (Dist(pc, cell) < Dist(best, cell)) best = pc;
                    parentOf[cell] = best;
                }
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
            bool prayerGarden = ctx.FaceId > 0;
            var recentBlessings = prayerGarden ? ctx.RecentRecipeIds ?? Array.Empty<string>() : null;
            var presence = Presence(afterSim, ctx.Weather, ctx.RecentRecipeIds, ctx.CurrentRecipeId, prayerGarden);
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
                    if (Chance(3)) { int before = seeds; PlantSeed(Bee, EmptyWithin(new[] { f }, 1, 1), new[] { f }); dropped = seeds > before; }
                }
            }
            // 2. Butterflies: 1/2 to carry pollen 2..4 cells away.
            var butterfly = Get(Butterfly);
            if (butterfly != null)
            {
                r.Visits.Add(butterfly);
                if (Chance(2)) PlantSeed(Butterfly, EmptyWithin(butterfly.Cells, 2, 4), butterfly.Cells);
            }
            // 2b. Pollination (lifecycle.v2): flowers the bees and butterflies visited will bear seed when they return.
            if (prayerGarden)
                foreach (var v in new[] { bee, butterfly })
                    if (v != null)
                        foreach (int c in v.Cells)
                        {
                            var fl = s.PlantAt(c);
                            if (fl != null && fl.Stage == PlantStage.Flower) fl.Pollinated = true;
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
                            (o, c) => X(c) - X(o) >= 2 && X(c) - X(o) <= 5 && Math.Abs(Y(c) - Y(o)) <= 1), new[] { flowers[fi] });
                }
            }
            // 11-13. Presentation-only residents.
            if (ctx.Sim != null && ctx.Sim.Discharged > 0) r.Visits.Add(new EcologyVisit { Kind = Fish });
            if (Flowers(s).Count > 0 && ctx.CurrentRecipeId != null &&
                RecipeCatalog.ElementOf(ctx.CurrentRecipeId) == ElementId.Wind)
                r.Visits.Add(new EcologyVisit { Kind = Petals });
            var dew = Get(Dew);
            if (dew != null) r.Visits.Add(dew);

            // 15. Germination (ecology.v3): every plant without a species becomes one that fits its
            // place, weighted by how its seed came. Cell order; draws from the saved ecology bytes.
            var unnamed = new List<PlantState>();
            foreach (var p in s.Plants) if (p.Species == null) unnamed.Add(p);
            unnamed.Sort((a, b) => a.CellId.CompareTo(b.CellId));
            foreach (var p in unnamed)
            {
                string carrier = carriedBy.TryGetValue(p.CellId, out string ck) ? ck
                    : p.OriginKind == "seedInput" ? "die" : p.OriginKind;
                string parentSpecies = parentOf.TryGetValue(p.CellId, out int pc) ? s.PlantAt(pc)?.Species : null;
                p.Species = Flora.ChooseSpecies(s, p, carrier, parentSpecies, n => Pick(n), recentBlessings);
                var t = Flora.Get(p.Species);
                bool softened = t != null && Flora.BaseHardness(p.CellId) > Flora.HardnessLimit(t, prayerGarden);
                r.Effects.Add(new EcologyEffect { Kind = Germinate, CellId = p.CellId, Species = p.Species, Softened = softened });
            }
            // 16. Plants in a place that suits them thrive a little.
            var suited = new List<int>();
            foreach (var p in s.Plants)
            {
                var t = Flora.Get(p.Species);
                if (t != null && p.Stage != PlantStage.Seed && p.Stage != PlantStage.Flower && Flora.Fits(t, s, p.CellId, prayerGarden))
                    suited.Add(p.CellId);
            }
            suited.Sort();
            foreach (int c in suited) Nourish(Thrive, c, Flora.ThriveGrowth);

            // 17. Harmony and the mycorrhizal network (ecology.v2/v3). No chance involved.
            var kinds = new HashSet<string>();
            foreach (var v in presence) kinds.Add(v.Kind);
            r.Harmony = Harmony.Read(ctx.RecentRecipeIds, kinds.Count, Flora.BloomingSpecies(s));
            r.Networks = Networks(s);
            int share = r.Harmony.Value * MycorrhizaPerHarmony;
            if (share > 0 && ctx.Sim != null)
            {
                var before = ctx.Sim.Evidence.GrowthBeforeByPlant;
                foreach (var net in r.Networks)
                {
                    if (net.Count < MycorrhizaMinNetwork) continue;
                    bool grew = false;
                    foreach (int c in net)
                    {
                        var p = s.PlantAt(c);
                        int old = before.TryGetValue(p.PlantId, out int g0) ? g0 : 0;
                        if (p.Growth > old) { grew = true; break; }
                    }
                    if (!grew) continue;
                    var young = new List<PlantState>();
                    foreach (int c in net)
                    {
                        var p = s.PlantAt(c);
                        if (p.Stage != PlantStage.Flower) young.Add(p);
                    }
                    young.Sort((a, b) => a.Growth != b.Growth ? a.Growth.CompareTo(b.Growth) : a.CellId.CompareTo(b.CellId));
                    for (int i = 0; i < young.Count && i < MycorrhizaRecipients; i++) Nourish(Mycorrhiza, young[i].CellId, share);
                }
            }

            // 18. The prayer star beneath the die (ecology.v4, prayer-star.v1). No chance involved.
            PrayerStar.ApplyGift(r, ctx, (cell, amount) => Nourish(PrayerStar.GiftKind, cell, amount));

            // 19. The cycle (lifecycle.v1): flowers past their span return to the soil and feed their neighbours.
            if (prayerGarden)
            {
                foreach (var gone in Lifecycle.Advance(s))
                {
                    r.Effects.Add(new EcologyEffect { Kind = Lifecycle.ReturnKind, CellId = gone.CellId, Species = gone.Species });
                    r.Events.Add(new GardenEvent { Kind = Lifecycle.ReturnedEvent, PlantId = gone.PlantId, CellId = gone.CellId, Tick = 40 });
                    // lifecycle.v2: a pollinated flower's seed takes root where it stood (its line goes on), most
                    // likely its own kind, leaning with the recent blessings, thinned by crowding. An unvisited flower
                    // leaves its place open for new seeds (the die's, the creatures').
                    if (gone.Pollinated && IsEmptyPlantable(s, gone.CellId))
                    {
                        {
                            int cell = gone.CellId;
                            var seedling = new PlantState
                            {
                                PlantId = newPlantId != null ? newPlantId(idCounter++) : "fruit-" + cell,
                                CellId = cell, Growth = 0, GrowthRemainder = 0, Stage = PlantStage.Seed,
                                OriginKind = "ecology", SeedSourceRoundId = ctx.RoundId
                            };
                            s.Plants.Add(seedling);
                            seedling.Species = Flora.ChooseSpecies(s, seedling, Lifecycle.FruitKind, gone.Species, n => Pick(n), recentBlessings);
                            r.Effects.Add(new EcologyEffect { Kind = Lifecycle.FruitKind, CellId = cell, Seed = true, Species = seedling.Species });
                            r.Effects.Add(new EcologyEffect { Kind = Germinate, CellId = cell, Species = seedling.Species });
                            r.Events.Add(new GardenEvent { Kind = "SeedCarried", PlantId = seedling.PlantId, CellId = cell, Tick = 40 });
                        }
                    }
                    var near = new List<int>();
                    foreach (var p in s.Plants) if (Dist(p.CellId, gone.CellId) == 1 && p.Stage != PlantStage.Flower) near.Add(p.CellId);
                    near.Sort();
                    foreach (int c in near) Nourish(Lifecycle.HumusKind, c, Lifecycle.HumusGrowth);
                }
            }

            r.Events.AddRange(stageEvents.Events);
            return r;
        }

        /// <summary>Mycorrhizal networks: plants linked within MycorrhizaReach, as connected components (cellId order).</summary>
        public static List<List<int>> Networks(WorldState s)
        {
            if (s.Ecosystem != null) return LivingEcosystem.Networks(s);
            var cells = new List<int>();
            foreach (var p in s.Plants) cells.Add(p.CellId);
            cells.Sort();
            var seen = new HashSet<int>();
            var result = new List<List<int>>();
            foreach (int start in cells)
            {
                if (seen.Contains(start)) continue;
                var net = new List<int>();
                var stack = new Stack<int>();
                stack.Push(start); seen.Add(start);
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    net.Add(c);
                    foreach (int o in cells)
                        if (!seen.Contains(o) && Dist(c, o) <= MycorrhizaReach) { seen.Add(o); stack.Push(o); }
                }
                net.Sort();
                result.Add(net);
            }
            return result;
        }

        /// <summary>Journal payload for Resolved.ecology.</summary>
        public static Dictionary<string, object> ToPayload(EcologyResult r)
        {
            bool living = r.PostState != null && r.PostState.Ecosystem != null;
            var visits = new List<object>();
            foreach (var v in r.Visits)
            {
                var cells = new List<object>();
                foreach (int c in v.Cells) cells.Add((long)c);
                visits.Add(new Dictionary<string, object> { ["kind"] = v.Kind, ["cells"] = cells });
            }
            var effects = new List<object>();
            foreach (var e in r.Effects)
            {
                var d = new Dictionary<string, object>
                {
                    ["kind"] = e.Kind, ["cellId"] = (long)e.CellId, ["amount"] = (long)e.Amount, ["seed"] = e.Seed
                };
                if (e.Species != null) { d["species"] = e.Species; d["softened"] = e.Softened; }
                effects.Add(d);
            }
            var payload = new Dictionary<string, object>
            {
                ["rulesVersion"] = CareRules.Enabled(r.PostState) ? CareRules.RulesVersion : living && r.PostState.Ecosystem.Habitat != null ? HabitatRules.RulesVersion : living ? (r.PostState.Ecosystem.Succession != null ?
                    (r.PostState.Ecosystem.Succession.Community != null ? CommunityRules.RulesVersion : SuccessionRules.RulesVersion) : LivingEcosystem.RulesVersion)
                    : r.PrayerStar != null ? LegacyPrayerRulesVersion : RulesVersion,
                ["harmony"] = r.Harmony != null ? (long)r.Harmony.Value : 0L,
                ["star"] = r.Harmony != null ? (long)r.Harmony.Star : 0L,
                ["yin"] = r.Harmony != null ? (long)r.Harmony.Yin : 0L,
                ["yang"] = r.Harmony != null ? (long)r.Harmony.Yang : 0L,
                ["visits"] = visits,
                ["effects"] = effects,
                ["exhausted"] = r.Exhausted
            };
            if (r.Care != null) payload["care"] = r.Care.ToPayload();
            if (living)
            {
                var relations = new List<object>();
                foreach (var e in r.Relations)
                {
                    var relation = new Dictionary<string, object>
                    {
                        ["kind"] = e.Kind, ["actor"] = e.Actor, ["species"] = e.Species,
                        ["fromCell"] = e.FromCell, ["toCell"] = e.ToCell, ["amount"] = e.Amount
                    };
                    if (e.FromHabitat != null) relation["fromHabitat"] = e.FromHabitat;
                    if (e.ToHabitat != null) relation["toHabitat"] = e.ToHabitat;
                    relations.Add(relation);
                }
                payload["relations"] = relations;
                payload["season"] = PlantStrategies.SeasonAt(r.PostState);
            }
            if (r.PrayerStar != null) payload["prayerStar"] = DestinyGarden.Domain.PrayerStar.ToPayload(r.PrayerStar);
            return payload;
        }
    }
}
