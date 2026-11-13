using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>
    /// ecology.v4: local habitats, finite fruit, decomposers and a small food web.
    /// Only saved ecology bytes supply chance. All other transitions use saved integer state.
    /// The water simulation remains untouched; soil fertility is a separate resource.
    /// </summary>
    public static class LivingEcosystem
    {
        public const string RulesVersion = "ecology.v4";
        public const string Aphid = "aphid", Ladybird = "ladybird", Hoverfly = "hoverfly", Spider = "spider",
            Springtail = "springtail", Snail = "snail", Frog = "frog", SeedFall = "seedfall";
        public const int ResourceCap = 100, StressLimit = 24;
        public static int Distance(int a, int b) => Math.Abs(a % 16 - b % 16) + Math.Abs(a / 16 - b / 16);
        private static bool Soil(int c) => GardenMapData.SoilCapacity[c] > 0;
        private static bool Living(WorldState s, PlantState p) =>
            !s.Ecosystem.Cycles.TryGetValue(p.PlantId, out var life) || life.Stress < 12;
        private static List<PlantState> Ordered(WorldState s)
        {
            var ps = new List<PlantState>(s.Plants);
            ps.Sort((a, b) => a.CellId.CompareTo(b.CellId));
            return ps;
        }
        private static bool Near(List<int> cells, int c, int reach)
        {
            foreach (int o in cells) if (Distance(o, c) <= reach) return true;
            return false;
        }
        public static string Habitat(WorldState s, int c)
        {
            if (Flora.Moisture(s, c) >= 650 && s.Ecosystem != null && s.Ecosystem.WetAge[c] >= 3) return "水辺";
            if (Flora.IsRockShade(c)) return "岩陰";
            if (Flora.IsShade(s, c)) return "草木の陰";
            if (Flora.Moisture(s, c) <= 300 && Flora.Drainage(s, c) >= 450) return "乾いた草地";
            return "花の草地";
        }

        /// <summary>No chance or mutation. A puddle must persist before aquatic visitors arrive.</summary>
        public static List<EcologyVisit> Presence(WorldState s, WeatherDefinition weather, string currentRecipeId, ResolvedRecipe dice = null)
        {
            var e = s.Ecosystem;
            var visits = new List<EcologyVisit>();
            if (e == null) return visits;
            var bees = new List<int>(); var butterflies = new List<int>(); var open = new List<int>();
            var moss = new List<int>(); var mushrooms = new List<int>(); var worms = new List<int>();
            var pools = new List<int>(); var litter = new List<int>(); var aphids = new List<int>();
            var cover = new List<int>(); var fruit = new List<int>(); var windSeeds = new List<int>();
            foreach (var p in Ordered(s))
            {
                if (!Living(s, p)) continue;
                var t = Flora.Get(p.Species);
                if (t == null) continue;
                if (p.Stage == PlantStage.Flower)
                {
                    if (t.Bee > 0) bees.Add(p.CellId);
                    if (t.Butterfly > 0) butterflies.Add(p.CellId);
                    // Broad, shallow flowers feed adult hoverflies; their larvae eat aphids.
                    if (t.Id == "daisy" || t.Id == "yarrow" || t.Id == "goldenrod" || t.Id == "buttercup") open.Add(p.CellId);
                }
                if (e.Cycles.TryGetValue(p.PlantId, out var life) && life.Seeds > 0)
                {
                    if (t.Berries) fruit.Add(p.CellId);
                    if (t.WindSeed) windSeeds.Add(p.CellId);
                }
            }
            for (int c = 0; c < GardenMapData.CellCount; c++)
            {
                if (!Soil(c)) continue;
                int m = Flora.Moisture(s, c);
                if (Flora.IsShade(s, c) && m >= 450 && e.WetAge[c] >= 2) moss.Add(c);
                if (Flora.IsShade(s, c) && m >= 400 && m < 950 && e.Litter[c] >= 3) mushrooms.Add(c);
                if (m >= 300 && m <= 850 && e.Litter[c] >= 2) worms.Add(c);
                if (e.WetAge[c] >= 4 && (s.SurfaceWater[c] > 0 || m >= 700)) pools.Add(c);
                if (e.Litter[c] >= 2 && m >= 250 && m < 950) litter.Add(c);
                if (e.Aphids[c] > 0) aphids.Add(c);
                if (e.CoverAge[c] >= 6) cover.Add(c);
            }
            void Add(string kind, List<int> cells, int cap = 6)
            {
                if (e.Succession?.Community != null)
                {
                    cells = CommunityRules.RestrictVisits(s, kind, cells);
                    cap = CommunityRules.VisitLimit(s, kind, cap);
                }
                if (cells.Count == 0 || cap == 0) return;
                var selected = e.Succession != null ? SuccessionRules.SelectVisits(s, kind, cells, cap)
                    : cells.GetRange(0, Math.Min(cap, cells.Count));
                if (selected.Count > 0) visits.Add(new EcologyVisit { Kind = kind, Cells = selected });
            }
            bool rain = weather != null && weather.Id == WeatherId.Drizzle;
            if (!rain) { Add(Ecology.Bee, bees, 3); Add(Ecology.Butterfly, butterflies, 3); }
            Add(Ecology.Moss, moss);
            Add(Ecology.Mushroom, mushrooms, 3);
            Add(Ecology.Worm, worms, 4);
            Add(Springtail, litter, 6);
            Add(Aphid, aphids, 6);
            var ladybirds = new List<int>(); var hoverflies = new List<int>(); var spiders = new List<int>();
            foreach (int c in aphids)
            {
                if (e.Aphids[c] >= 3 && Near(cover, c, 2)) ladybirds.Add(c);
                if (!rain && Near(open, c, 2)) hoverflies.Add(c);
                if (Near(cover, c, 1)) spiders.Add(c);
            }
            if (e.Succession?.Community != null)
                foreach (int c in open)
                {
                    var plant = s.PlantAt(c);
                    if (!e.Succession.Flowers.TryGetValue(plant.PlantId, out var resource) || resource.Nectar == 0) continue;
                    if (!ladybirds.Contains(c)) ladybirds.Add(c);
                    if (!rain && !hoverflies.Contains(c)) hoverflies.Add(c);
                }
            Add(Ladybird, ladybirds, 4); Add(Hoverfly, hoverflies, 3); Add(Spider, spiders, 3);
            var frogs = new List<int>(); var snails = new List<int>();
            foreach (int c in pools)
                if (Near(cover, c, 2) && (Near(aphids, c, 3) || (e.Succession?.Community != null && e.Succession.Community.AquaticPrey[c] > 0))) frogs.Add(c);
            foreach (int c in litter) if (Flora.Moisture(s, c) >= 600) snails.Add(c);
            Add(Frog, frogs, 2); Add(Snail, snails, 3);
            Add(Ecology.Dragonfly, pools, 4);
            if (weather != null && weather.Id != WeatherId.Clear && moss.Count >= 2 && pools.Count > 0)
                Add(Ecology.Firefly, moss, 4); // wet refuges, not a source of plant nutrition
            if (fruit.Count > 0) Add(Ecology.Bird, fruit, 3);
            else if (cover.Count >= 3 && aphids.Count >= 3) Add(Ecology.Bird, aphids, 3);
            if (CareRules.Enabled(s))
            {
                windSeeds.RemoveAll(c => CareRules.WindPower(dice, weather, c) < 100);
                Add(Ecology.Fluff, windSeeds, 3);
            }
            else
            {
                bool windy = currentRecipeId != null && RecipeCatalog.ElementOf(currentRecipeId) == ElementId.Wind;
                if (windy || (weather != null && weather.BaseWindQ >= 300)) Add(Ecology.Fluff, windSeeds, 3);
            }
            if (rain) Add(Ecology.Dew, cover, 6);
            CommunityRules.AddYoungPresence(s, visits);
            return visits;
        }

        public static EcologyResult Apply(WorldState afterSim, EcologyContext ctx, byte[] bytes, Func<int, string> newPlantId)
        {
            var s = afterSim.Clone();
            var e = s.Ecosystem;
            var r = new EcologyResult { PostState = s };
            ctx.Care = CareRules.Enabled(s) ? CareMeasure.Measure(ctx) : null;
            var buf = new EntropyBuffer(bytes ?? new byte[0]);
            var stages = new SimulationResult();
            int planted = 0;
            int Pick(int count)
            {
                if (r.Exhausted || count <= 0) return -1;
                int v = buf.UniformBelow(count);
                if (v < 0) r.Exhausted = true;
                return v;
            }
            void Link(string kind, string actor, int from, int to, int amount, string species = null) =>
                r.Relations.Add(new EcologicalLink { Kind = kind, Actor = actor, FromCell = from, ToCell = to, Amount = amount, Species = species });
            void Feed(string actor, PlantState p, int amount)
            {
                if (p.Growth >= 2500 || amount <= 0 || !Living(s, p)) return;
                int old = p.Growth;
                p.Growth = Math.Min(2500, old + amount);
                if (p.Growth == 2500) p.GrowthRemainder = 0;
                Simulation.EmitStageEvents(stages, p, old, 40);
                r.Effects.Add(new EcologyEffect { Kind = actor, CellId = p.CellId, Amount = p.Growth - old });
            }

            // Name only incoming die seeds. A dispersed seed is assigned its actual parent's species below.
            foreach (var p in Ordered(s))
            {
                if (p.Species == null)
                {
                    p.Species = Flora.ChooseSpecies(s, p, p.OriginKind, null, Pick);
                    var t = Flora.Get(p.Species);
                    r.Effects.Add(new EcologyEffect { Kind = Ecology.Germinate, CellId = p.CellId, Species = p.Species,
                        Softened = t != null && Flora.BaseHardness(p.CellId) > t.MaxHardness });
                }
                if (!e.Cycles.ContainsKey(p.PlantId)) e.Cycles.Add(p.PlantId, new PlantCycle());
            }

            if (e.Habitat != null) HabitatRules.Begin(s, r);
            if (e.Succession != null)
            {
                SuccessionRules.AgeSeedBank(s, r);
                SuccessionRules.RefreshNectar(s, ctx.Weather);
                CareRules.LightNectar(s, ctx, r);
            }

            // Habitats retain a short memory of wetness and cover. One wet roll cannot conjure a wetland.
            for (int c = 0; c < GardenMapData.CellCount; c++)
            {
                if (!Soil(c)) continue;
                int moisture = Flora.Moisture(s, c);
                e.WetAge[c] = Math.Max(0, Math.Min(12, e.WetAge[c] + (moisture >= 450 ? 1 : -2)));
                var p = s.PlantAt(c);
                bool covered = p != null && p.Stage >= PlantStage.Leaf && Living(s, p);
                e.CoverAge[c] = Math.Max(0, Math.Min(48, e.CoverAge[c] + (covered ? 1 : -2)));
                if (covered && s.WorldRevision % 3 == c % 3) e.Litter[c] = Math.Min(ResourceCap, e.Litter[c] + 1);
                if (ctx.Weather != null && ctx.Weather.Id == WeatherId.Drizzle && s.WorldRevision % 3 == 0)
                    e.Nutrients[c] = Math.Min(ResourceCap, e.Nutrients[c] + 1); // small external deposition
                bool host = covered && moisture >= 200 && moisture <= 850 && e.CoverAge[c] >= 3;
                e.Aphids[c] = Math.Max(0, Math.Min(12, e.Aphids[c] + (host ? 1 : -2)));
            }
            if (e.Succession?.Community != null) CommunityRules.BeforeActivity(s, ctx, r);
            r.Visits = Presence(s, ctx.Weather, ctx.CurrentRecipeId, ctx.Dice);
            EcologyVisit Visit(string kind) => r.Visits.Find(v => v.Kind == kind);

            // Pollinators fertilise flowers; pollen does not turn into a different species in bare soil.
            void Pollinate(string actor, IEnumerable<int> cells)
            {
                foreach (int c in cells)
                {
                    var p = s.PlantAt(c);
                    if (p == null || p.Stage != PlantStage.Flower) continue;
                    var life = e.Cycles[p.PlantId];
                    if (e.Succession != null && !SuccessionRules.Forage(s, p, actor, r)) continue;
                    if (life.Pollen >= 6) continue;
                    life.Pollen++;
                    Link("pollinated", actor, c, c, 1, p.Species);
                }
            }
            foreach (string actor in new[] { Ecology.Bee, Ecology.Butterfly })
                if (Visit(actor) is EcologyVisit pollinator)
                {
                    if (e.Succession != null && actor == Ecology.Butterfly)
                    {
                        // Bees may have used the nectar already. Butterflies can choose another
                        // suitable flower instead of following a now-empty route from the snapshot.
                        var available = new List<int>();
                        foreach (var p in Ordered(s))
                            if (p.Stage == PlantStage.Flower && Living(s, p) && Flora.Get(p.Species).Butterfly > 0) available.Add(p.CellId);
                        if (e.Succession.Community != null) available = CommunityRules.RestrictVisits(s, actor, available);
                        pollinator.Cells = SuccessionRules.SelectFlowers(s, actor, available, CommunityRules.VisitLimit(s, actor, 3));
                        if (pollinator.Cells.Count == 0) { r.Visits.Remove(pollinator); continue; }
                    }
                    Pollinate(actor, pollinator.Cells);
                }
            if (Visit(Hoverfly) != null)
            {
                var nearbyFlowers = new List<int>();
                foreach (var p in Ordered(s))
                    if (p.Stage == PlantStage.Flower && Near(Visit(Hoverfly).Cells, p.CellId, 2) && Flora.Get(p.Species).Bee > 0 &&
                        (e.Succession == null || p.Species == "daisy" || p.Species == "yarrow" || p.Species == "goldenrod" || p.Species == "buttercup"))
                        nearbyFlowers.Add(p.CellId);
                Pollinate(Hoverfly, e.Succession != null ? SuccessionRules.SelectFlowers(s, Hoverfly, nearbyFlowers, 3)
                    : nearbyFlowers.GetRange(0, Math.Min(3, nearbyFlowers.Count)));
            }

            // Detrital food web: every mineralised unit is removed from litter before becoming a nutrient.
            foreach (string actor in new[] { Ecology.Mushroom, Ecology.Worm, Springtail, Snail })
            {
                var visitor = Visit(actor);
                if (visitor == null) continue;
                foreach (int c in visitor.Cells)
                {
                    if (e.Habitat != null)
                    {
                        HabitatRules.Decompose(s, c, actor, actor == Ecology.Worm ? 3 : 2, r);
                        continue;
                    }
                    int amount = Math.Min(actor == Ecology.Worm ? 3 : 2, Math.Min(e.Litter[c], ResourceCap - e.Nutrients[c]));
                    if (amount == 0) continue;
                    e.Litter[c] -= amount; e.Nutrients[c] += amount;
                    Link("decomposed", actor, c, c, amount);
                }
            }
            // Root nodules add new nitrogen; this is an explicit source, unlike fungal redistribution.
            foreach (var p in Ordered(s))
            {
                int fixedN = PlantStrategies.Get(p.Species).Nitrogen;
                if (fixedN == 0 || p.Stage < PlantStage.Leaf || !Living(s, p) || !Flora.Fits(Flora.Get(p.Species), s, p.CellId)) continue;
                for (int c = 0; c < GardenMapData.CellCount; c++)
                {
                    if (!Soil(c) || Distance(c, p.CellId) > 1) continue;
                    int amount = Math.Min(fixedN, ResourceCap - e.Nutrients[c]);
                    if (amount <= 0) continue;
                    e.Nutrients[c] += amount;
                    Link("nitrogen-fixed", "root-nodule", p.CellId, c, amount, p.Species);
                }
            }
            // Predators depend on local prey AND a refuge; they reduce actual colonies, not a global bonus.
            foreach (string actor in new[] { Ladybird, Hoverfly, Spider, Frog, Ecology.Bird })
            {
                var visitor = Visit(actor);
                if (visitor == null || (e.Succession?.Community != null && actor == Hoverfly)) continue;
                foreach (int c in visitor.Cells)
                {
                    int preyCell = c;
                    if (actor == Frog)
                    {
                        preyCell = -1;
                        for (int target = 0; target < GardenMapData.CellCount; target++)
                            if (Distance(c, target) <= 3 && e.Aphids[target] > 0 &&
                                (preyCell < 0 || e.Aphids[target] > e.Aphids[preyCell])) preyCell = target;
                    }
                    if (preyCell < 0) continue;
                    int amount = Math.Min(e.Aphids[preyCell], actor == Ladybird ? 4 : 2);
                    if (amount <= 0) continue;
                    e.Aphids[preyCell] -= amount;
                    Link("predation", actor, c, preyCell, amount);
                }
            }

            r.Networks = Networks(s);
            // Fungi move at most three units from rich soil to poor soil, conserving the nutrient total.
            foreach (var network in r.Networks)
            {
                if (network.Count < 3) continue;
                int transfers = 0;
                foreach (int c in network)
                {
                    if (e.Nutrients[c] > 6 || transfers >= 3) continue;
                    int donor = -1;
                    foreach (int d in network)
                        if (e.Nutrients[d] >= 12 && (donor < 0 || e.Nutrients[d] > e.Nutrients[donor])) donor = d;
                    if (donor < 0) break;
                    int amount = Math.Min(3, e.Nutrients[donor] - 9);
                    e.Nutrients[donor] -= amount; e.Nutrients[c] += amount;
                    Link("nutrient-shared", Ecology.Mycorrhiza, donor, c, amount, s.PlantAt(c).Species);
                    transfers++;
                }
            }

            foreach (var p in Ordered(s))
            {
                int c = p.CellId;
                var life = e.Cycles[p.PlantId];
                var t = Flora.Get(p.Species);
                var strategy = PlantStrategies.Get(p.Species);
                life.Age++;
                bool fits = Flora.Fits(t, s, c);
                int crowd = 0;
                foreach (var other in s.Plants)
                    if (other != p && other.Stage >= PlantStage.Leaf && Distance(other.CellId, c) <= 1 &&
                        PlantStrategies.Get(other.Species).Height >= strategy.Height) crowd++;
                int pressure = (!fits ? 2 : 0) + (e.Nutrients[c] == 0 ? 1 : 0) + (e.Aphids[c] >= 6 ? 1 : 0) + (crowd >= 3 ? 1 : 0);
                int previousStress = life.Stress;
                life.Stress = Math.Max(0, Math.Min(StressLimit, life.Stress + (pressure == 0 ? -3 : pressure)));
                if (previousStress < 12 && life.Stress >= 12) Link("stressed", !fits ? "habitat" : e.Aphids[c] >= 6 ? Aphid : crowd >= 3 ? "competition" : "nutrients", c, c, life.Stress, p.Species);
                if (previousStress >= 12 && life.Stress < 12) Link("recovered", "habitat", c, c, previousStress - life.Stress, p.Species);
                if (ctx.Sim != null && ctx.Sim.Evidence.GrowthBeforeByPlant.TryGetValue(p.PlantId, out int beforeGrowth) && p.Growth > beforeGrowth && e.Nutrients[c] > 0)
                    e.Nutrients[c]--; // natural growth uses fertility, flowering plants do not keep consuming
                if (fits && life.Stress < 12 && e.Nutrients[c] > 0 && p.Stage != PlantStage.Seed && p.Stage != PlantStage.Flower)
                {
                    e.Nutrients[c]--;
                    int bonus = e.Habitat == null ? 12 : 4 + (HabitatRules.Suitability(t, s, c) - 600) * 8 / 400;
                    Feed(Ecology.Thrive, p, crowd >= 2 ? Math.Max(1, bonus / 3) : bonus);
                }
                if (p.Stage == PlantStage.Flower)
                {
                    life.BloomAge++;
                    if (strategy.SelfFertile && life.Pollen < 6) life.Pollen++;
                    if (!life.Fruited && life.BloomAge >= 3 && life.Pollen >= 2 && life.Stress < 12)
                    {
                        if (e.Succession != null && life.Seeds > 0)
                        {
                            SuccessionRules.ReleaseRipeSeed(s, p, life, ctx.RoundId, r);
                            int returned = Math.Min(life.Seeds, ResourceCap - e.Litter[c]);
                            if (life.Seeds > 0) Link("seed-expired", SuccessionRules.SeedBank, c, c, returned, p.Species);
                            e.Litter[c] += returned;
                        }
                        life.Seeds = (t.Berries ? 3 : 2) + (strategy.Season == PlantStrategies.SeasonAt(s) ? 1 : 0);
                        life.Fruited = true;
                        Link("fruited", "flower", c, c, life.Seeds, p.Species);
                    }
                }
            }

            // Finite seeds. The destination must fit the parent's species, which is always inherited.
            // Rotate dispersal routes so gravity, birds and wind each get a chance under the two-seed cap.
            var routes = new[] { SeedFall, Ecology.Bird, Ecology.Fluff };
            for (int step = 0; step < routes.Length; step++)
            {
                if (e.Succession != null && SuccessionRules.Deposits(r) >= SuccessionRules.MaxDeposits) break;
                string route = routes[(step + s.WorldRevision % routes.Length) % routes.Length];
                if (route != SeedFall && Visit(route) == null) continue;
                var parents = new List<PlantState>();
                foreach (var p in Ordered(s))
                {
                    var t = Flora.Get(p.Species);
                    var life = e.Cycles[p.PlantId];
                    if (life.Seeds <= 0 || !Living(s, p)) continue;
                    if (route == Ecology.Bird ? !t.Berries : route == Ecology.Fluff ? !t.WindSeed : t.Berries || t.WindSeed) continue;
                    if (CareRules.Enabled(s) && route == Ecology.Fluff && !Visit(route).Cells.Contains(p.CellId)) continue;
                    parents.Add(p);
                }
                if (parents.Count == 0 || planted >= Ecology.MaxSeedsPerRoll) continue;
                int choice = Pick(parents.Count);
                if (choice < 0) break;
                var parent = parents[choice];
                var targets = new List<int>();
                for (int c = 0; c < GardenMapData.CellCount; c++)
                {
                    int d = Distance(parent.CellId, c);
                    if (GardenMapData.Plantable[c] != 1) continue;
                    if (e.Succession != null ? !SuccessionRules.HasSpace(s, c)
                        : s.PlantAt(c) != null || afterSim.PlantAt(c) != null || !Flora.Fits(Flora.Get(parent.Species), s, c)) continue;
                    bool within = route == Ecology.Bird ? d >= 2 && d <= 8
                        : route == Ecology.Fluff ? c % 16 - parent.CellId % 16 >= 2 &&
                            c % 16 - parent.CellId % 16 <= (CareRules.Enabled(s) ? CareRules.WindDistance(ctx.Dice, ctx.Weather, parent.CellId) : 5) && Math.Abs(c / 16 - parent.CellId / 16) <= 1
                        : d >= 1 && d <= 2;
                    if (within) targets.Add(c);
                }
                if (targets.Count == 0) continue;
                int chance = Pick(3);
                if (chance < 0) break;
                if (chance == 2) continue;
                int index = Pick(targets.Count);
                if (index < 0) break;
                int cell = targets[index];
                if (e.Succession != null)
                {
                    if (SuccessionRules.Store(s, parent, cell, route, ctx.RoundId, r)) e.Cycles[parent.PlantId].Seeds--;
                    continue;
                }
                var seed = new PlantState
                {
                    PlantId = newPlantId != null ? newPlantId(planted) : "eco-" + s.WorldRevision + "-" + cell,
                    CellId = cell, OriginKind = "ecology", SeedSourceRoundId = ctx.RoundId, Species = parent.Species
                };
                s.Plants.Add(seed); e.Cycles.Add(seed.PlantId, new PlantCycle());
                e.Cycles[parent.PlantId].Seeds--; planted++;
                r.Effects.Add(new EcologyEffect { Kind = route, CellId = cell, Seed = true });
                r.Effects.Add(new EcologyEffect { Kind = Ecology.Germinate, CellId = cell, Species = seed.Species });
                Link("seed-dispersed", route, parent.CellId, cell, 1, seed.Species);
                r.Events.Add(new GardenEvent { Kind = "SeedCarried", PlantId = seed.PlantId, CellId = cell, Tick = 40 });
            }

            // Flowering is a phase, not a permanent endpoint. Litter feeds the next generation.
            foreach (var p in Ordered(s))
            {
                var life = e.Cycles[p.PlantId];
                var strategy = PlantStrategies.Get(p.Species);
                if (life.Stress >= StressLimit || life.Age >= strategy.Longevity)
                {
                    if (e.Succession != null) SuccessionRules.ReleaseRipeSeed(s, p, life, ctx.RoundId, r);
                    int returned = Math.Min(12, ResourceCap - e.Litter[p.CellId]);
                    e.Litter[p.CellId] += returned;
                    Link("returned-to-soil", life.Stress >= StressLimit ? "stress" : "lifecycle", p.CellId, p.CellId, returned, p.Species);
                    s.Plants.Remove(p); e.Cycles.Remove(p.PlantId);
                    r.Events.Add(new GardenEvent { Kind = "ReturnedToSoil", PlantId = p.PlantId, CellId = p.CellId, Tick = 40 });
                }
                else if (p.Stage == PlantStage.Flower && life.BloomAge >= strategy.BloomRounds)
                {
                    p.Growth = 900; p.GrowthRemainder = 0; p.Stage = PlantStage.Leaf;
                    int returned = Math.Min(6, ResourceCap - e.Litter[p.CellId]);
                    e.Litter[p.CellId] += returned;
                    life.BloomAge = 0; life.Pollen = 0; life.Fruited = false; // ripe seeds remain until dispersed or the next bloom
                    Link("flower-rest", "lifecycle", p.CellId, p.CellId, returned, p.Species);
                }
                else if (life.Stress >= 12 && p.Growth > 0)
                {
                    p.Growth = Math.Max(0, p.Growth - 80); p.GrowthRemainder = 0;
                    p.Stage = StageAt(p.Growth);
                    if (p.Stage != PlantStage.Flower) { life.BloomAge = 0; life.Pollen = 0; }
                }
                if (p.Stage != PlantStage.Flower && life.BloomAge == 0 && life.Seeds == 0) life.Fruited = false;
            }
            if (ctx.PrayerAnswered && ctx.Sim != null)
            {
                var reached = new List<int>();
                foreach (int c in CareRules.Enabled(s) ? CareRules.ReachedPlants(s, ctx.Care) : PrayerSpirits.ReachedPlantCells(ctx.Sim))
                {
                    var p = s.PlantAt(c);
                    if (p == null || p.Stage == PlantStage.Flower || reached.Count >= 3) continue;
                    reached.Add(c); Feed(Ecology.Spirit, p, 120);
                }
                if (reached.Count > 0 || !CareRules.Enabled(s)) r.Visits.Add(new EcologyVisit { Kind = Ecology.Spirit, Cells = reached });
            }
            if (ctx.Sim != null && ctx.Sim.Discharged > 0) r.Visits.Add(new EcologyVisit { Kind = Ecology.Fish });
            if (ctx.CurrentRecipeId != null && RecipeCatalog.ElementOf(ctx.CurrentRecipeId) == ElementId.Wind && Flora.BloomingSpecies(s) > 0)
                r.Visits.Add(new EcologyVisit { Kind = Ecology.Petals });
            if (e.Succession != null)
            {
                SuccessionRules.Awaken(s, afterSim, r, newPlantId);
                SuccessionRules.CleanMemory(s);
                if (e.Succession.Community != null) CommunityRules.AfterActivity(s, r);
            }
            if (e.Habitat != null) HabitatRules.End(s, r, newPlantId);
            if (CareRules.Enabled(s)) r.Care = CareReport.Build(ctx, r);
            int residents = 0;
            foreach (var visit in r.Visits)
                if (visit.Kind != Ecology.Spirit && visit.Kind != Ecology.Fish && visit.Kind != Ecology.Petals) residents++;
            if (e.Succession?.Community != null) residents = CommunityRules.ResidentKinds(r.Visits);
            r.Harmony = Harmony.Read(ctx.RecentRecipeIds, residents, Flora.BloomingSpecies(s));
            r.Networks = Networks(s);
            r.Events.AddRange(stages.Events);
            return r;
        }

        private static PlantStage StageAt(int growth) => growth >= 2500 ? PlantStage.Flower : growth >= 1500 ? PlantStage.Bud
            : growth >= 750 ? PlantStage.Leaf : growth >= 250 ? PlantStage.Sprout : PlantStage.Seed;

        /// <summary>Fungal-compatible roots connect through habitable soil, never across a rock.</summary>
        public static List<List<int>> Networks(WorldState s)
        {
            var cells = new List<int>();
            foreach (var p in Ordered(s))
                if (p.Stage >= PlantStage.Sprout && Living(s, p) && PlantStrategies.Get(p.Species).FungalPartner &&
                    Flora.Moisture(s, p.CellId) >= 150 && Flora.Moisture(s, p.CellId) <= 900) cells.Add(p.CellId);
            bool Connect(int a, int b)
            {
                int d = Distance(a, b);
                if (d > 2) return false;
                if (d <= 1) return true;
                for (int c = 0; c < GardenMapData.CellCount; c++)
                    if (Distance(a, c) == 1 && Distance(b, c) == 1 && Soil(c) && GardenMapData.Solid[c] == 0 &&
                        Flora.Moisture(s, c) >= 150 && Flora.Moisture(s, c) <= 900) return true;
                return false;
            }
            var seen = new HashSet<int>(); var result = new List<List<int>>();
            foreach (int start in cells)
            {
                if (!seen.Add(start)) continue;
                var net = new List<int>(); var queue = new Queue<int>(); queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int c = queue.Dequeue(); net.Add(c);
                    foreach (int other in cells) if (!seen.Contains(other) && Connect(c, other)) { seen.Add(other); queue.Enqueue(other); }
                }
                net.Sort(); result.Add(net);
            }
            return result;
        }
    }
}
