using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>ecology.v6: settlement, finite meals, juvenile development and habitat recovery.</summary>
    public static class CommunityRules
    {
        private const int NurseryMealReserve = 3;
        private const int WetNurseryMoisture = 650;
        private const int CaterpillarMeal = 20;
        public const string RulesVersion = "ecology.v6", Algae = "algae", PondPrey = "pond-prey";
        public const string Caterpillar = "caterpillar", Chrysalis = "chrysalis", Tadpole = "tadpole",
            Nymph = "dragonfly-nymph", LadybirdLarva = "ladybird-larva", HoverflyLarva = "hoverfly-larva";
        public const int MaxFamiliesPerKind = 2, MaxProspectsPerKind = 6, MaxAdults = 3, MaxBrood = 2;
        public const int SettlementRounds = 3, AlgaeCap = 12, PreyCap = 8;
        public static readonly string[] Kinds = { Ecology.Butterfly, LivingEcosystem.Ladybird, LivingEcosystem.Hoverfly, Ecology.Dragonfly, LivingEcosystem.Frog };
        public static bool Managed(string kind) => Array.IndexOf(Kinds, kind) >= 0;
        private static int Dist(int a, int b) => LivingEcosystem.Distance(a, b);
        public static int Reach(string kind) => kind == LivingEcosystem.Ladybird ? 3 : kind == LivingEcosystem.Hoverfly ? 5 : 4;
        public static int Development(string kind) => kind == LivingEcosystem.Frog ? 10 : kind == Ecology.Dragonfly ? 8 : kind == Ecology.Butterfly ? 6 : 4;
        private static void Link(EcologyResult r, string kind, string actor, int from, int to, int amount) =>
            r.Relations.Add(new EcologicalLink { Kind = kind, Actor = actor, FromCell = from, ToCell = to, Amount = amount });
        private static bool Healthy(WorldState s, PlantState p) => p != null && p.Stage >= PlantStage.Leaf &&
            (!s.Ecosystem.Cycles.TryGetValue(p.PlantId, out var life) || life.Stress < 12);
        public static bool HostPlant(string species) => species == "clover" || species == "vetch" || species == "violet" || species == "wildrose" || species == "strawberry";
        private static bool OpenFlower(string species) => species == "daisy" || species == "yarrow" || species == "goldenrod" || species == "buttercup";
        public static bool Pond(WorldState s, int c) => GardenMapData.SoilCapacity[c] > 0 && s.Ecosystem.WetAge[c] >= 6 &&
            (s.SurfaceWater[c] > 0 || Flora.Moisture(s, c) >= 750);
        private static bool CoverNear(WorldState s, int c)
        {
            for (int n = 0; n < GardenMapData.CellCount; n++) if (Dist(c, n) <= 2 && s.Ecosystem.CoverAge[n] >= 4) return true;
            return false;
        }
        private static bool FlowerNear(WorldState s, string kind, int c)
        {
            foreach (var p in s.Plants)
            {
                if (p.Stage != PlantStage.Flower || !Healthy(s, p) || Dist(p.CellId, c) > Reach(kind)) continue;
                var t = Flora.Get(p.Species);
                if (t != null && (kind == Ecology.Butterfly ? t.Butterfly > 0 : OpenFlower(t.Id))) return true;
            }
            return false;
        }
        private static bool PreyNear(WorldState s, int c, int reach)
        {
            for (int n = 0; n < GardenMapData.CellCount; n++) if (Dist(c, n) <= reach && s.Ecosystem.Aphids[n] >= 2) return true;
            return false;
        }
        public static bool HabitatSuitable(WorldState s, string kind, int c)
        {
            if (GardenMapData.Plantable[c] != 1) return false;
            if (kind == Ecology.Butterfly) return Healthy(s, s.PlantAt(c)) && HostPlant(s.PlantAt(c).Species);
            if (kind == LivingEcosystem.Ladybird || kind == LivingEcosystem.Hoverfly)
                return Healthy(s, s.PlantAt(c)) && s.Ecosystem.CoverAge[c] >= 4;
            return Pond(s, c) && CoverNear(s, c);
        }
        private static bool Attractive(WorldState s, string kind, int c)
        {
            if (!HabitatSuitable(s, kind, c)) return false;
            if (kind == Ecology.Butterfly) return FlowerNear(s, kind, c);
            if (kind == LivingEcosystem.Ladybird) return PreyNear(s, c, 2);
            if (kind == LivingEcosystem.Hoverfly) return FlowerNear(s, kind, c) && PreyNear(s, c, 2);
            var pond = s.Ecosystem.Succession.Community;
            return kind == LivingEcosystem.Frog ? pond.Algae[c] >= 2 && (PreyNear(s, c, 3) || pond.AquaticPrey[c] > 0) : pond.AquaticPrey[c] > 0;
        }

        private static bool Aquatic(string kind) => kind == LivingEcosystem.Frog || kind == Ecology.Dragonfly;

        private static bool WetNursery(WorldState s, int cell) =>
            GardenMapData.SoilCapacity[cell] > 0 && s.Ecosystem.WetAge[cell] >= 6 &&
            (s.SurfaceWater[cell] > 0 || Flora.Moisture(s, cell) >= WetNurseryMoisture);

        // A nursery reaches its own cell and adjoining water. Dry soil and diagonal gaps separate pools.
        private static IEnumerable<int> NurseryWater(WorldState s, int center)
        {
            if (!WetNursery(s, center)) yield break;
            yield return center;
            const int width = GardenMapData.Width;
            if (center >= width && WetNursery(s, center - width)) yield return center - width;
            if (center % width > 0 && WetNursery(s, center - 1)) yield return center - 1;
            if (center % width < width - 1 && WetNursery(s, center + 1)) yield return center + 1;
            if (center + width < GardenMapData.CellCount && WetNursery(s, center + width)) yield return center + width;
        }

        private static int[] NurseryFood(WorldState s, string kind)
        {
            var pond = s.Ecosystem.Succession.Community;
            return kind == LivingEcosystem.Frog ? pond.Algae : pond.AquaticPrey;
        }

        private static int AquaticBroodSize(WorldState s, string kind, int cell)
        {
            var food = NurseryFood(s, kind);
            int stock = 0, sources = 0;
            foreach (int c in NurseryWater(s, cell))
            {
                stock += food[c];
                if (Pond(s, c) && (s.Ecosystem.Nutrients[c] > 0 ||
                    (kind == Ecology.Dragonfly && s.Ecosystem.Litter[c] > 0)))
                    sources++;
            }
            // A connected, replenished nursery can support a second young. Hold several meals before laying.
            return Math.Min(MaxBrood, Math.Min(sources, stock / NurseryMealReserve));
        }

        private static int LocalBroodSize(WorldState s, ResidentFamily family)
        {
            if (Aquatic(family.Kind)) return AquaticBroodSize(s, family.Kind, family.CellId);
            if (family.Kind == Ecology.Butterfly)
            {
                var host = s.PlantAt(family.CellId);
                return Math.Min(MaxBrood, Math.Max(0, host.Growth - Simulation.GrowthThreshold(PlantStage.Leaf)) /
                    (CaterpillarMeal * NurseryMealReserve));
            }
            int food = 0;
            for (int c = 0; c < GardenMapData.CellCount; c++)
                if (Dist(c, family.CellId) <= NurseryPreyReach(family.Kind, true)) food += s.Ecosystem.Aphids[c];
            return Math.Min(MaxBrood, food / NurseryMealReserve);
        }

        private static int NurseryPreyReach(string kind, bool connectedHabitats) =>
            connectedHabitats && (kind == LivingEcosystem.Ladybird || kind == LivingEcosystem.Hoverfly) ? 2 : 1;
        public static void BeforeActivity(WorldState s, EcologyContext ctx, EcologyResult r)
        {
            GrowPond(s, ctx, r);
            Settle(s, ctx, r);
        }
        public static void GrowPond(WorldState s, EcologyContext ctx, EcologyResult r)
        {
            var e = s.Ecosystem; var m = e.Succession.Community;
            for (int c = 0; c < GardenMapData.CellCount; c++)
            {
                if (GardenMapData.SoilCapacity[c] == 0) continue;
                int moisture = Flora.Moisture(s, c);
                if (moisture < 450)
                {
                    int lost = Math.Min(2, m.Algae[c]) + Math.Min(2, m.AquaticPrey[c]);
                    m.Algae[c] = Math.Max(0, m.Algae[c] - 2); m.AquaticPrey[c] = Math.Max(0, m.AquaticPrey[c] - 2);
                    e.Litter[c] = Math.Min(LivingEcosystem.ResourceCap, e.Litter[c] + lost);
                    if (lost > 0) Link(r, "pond-dried", PondPrey, c, c, lost);
                    continue;
                }
                if (!Pond(s, c)) continue;
                if (e.Nutrients[c] > 0 && m.Algae[c] < AlgaeCap)
                {
                    int normal = ctx.Weather == null || ctx.Weather.Id == WeatherId.Clear ? 2 : 1;
                    int light = CareRules.Enabled(s) && CareRules.ExtraField(ctx.Dice, ctx.Weather, c, ElementId.Light) >= 100 ? 1 : 0;
                    int grown = Math.Min(normal + light, AlgaeCap - m.Algae[c]);
                    e.Nutrients[c]--; m.Algae[c] += grown; Link(r, "algae-grew", Algae, c, c, grown);
                    if (grown > normal) Link(r, "care-algae", ctx.CurrentRecipeId, ctx.Dice.AnchorCellId, c, grown - normal);
                }
                if (m.AquaticPrey[c] < PreyCap && (e.Litter[c] > 0 || m.Algae[c] > 0))
                {
                    if (e.Litter[c] > 0) { e.Litter[c]--; Link(r, "pond-food-fed", "litter", c, c, 1); }
                    else { m.Algae[c]--; Link(r, "pond-food-fed", Algae, c, c, 1); }
                    m.AquaticPrey[c]++;
                }
            }
        }
        private static void Settle(WorldState s, EcologyContext ctx, EcologyResult r)
        {
            var m = s.Ecosystem.Succession.Community;
            var next = new List<SettlementSite>();
            foreach (string kind in Kinds)
            {
                int established = m.Families.FindAll(f => f.Kind == kind).Count;
                if (established >= MaxFamiliesPerKind) continue;
                var candidates = new List<SettlementSite>();
                for (int c = 0; c < GardenMapData.CellCount; c++)
                {
                    if (!Attractive(s, kind, c) || m.Families.Exists(f => f.Kind == kind && Dist(f.CellId, c) <= 2)) continue;
                    if (ctx.ConnectedHabitats && Aquatic(kind) && AquaticBroodSize(s, kind, c) == 0) continue;
                    var old = m.Prospects.Find(p => p.Kind == kind && p.CellId == c);
                    candidates.Add(new SettlementSite { Kind = kind, CellId = c, Streak = Math.Min(SettlementRounds, (old?.Streak ?? 0) + 1) });
                }
                candidates.Sort((a, b) => a.Streak != b.Streak ? b.Streak.CompareTo(a.Streak) : a.CellId.CompareTo(b.CellId));
                int retained = 0; bool settled = false;
                foreach (var site in candidates)
                {
                    if (!settled && site.Streak >= SettlementRounds)
                    {
                        m.Families.Add(new ResidentFamily { FamilyId = ctx.RoundId + ":" + kind + ":" + site.CellId, Kind = kind, CellId = site.CellId });
                        Link(r, "resident-settled", kind, site.CellId, site.CellId, 1); settled = true;
                    }
                    else if (retained++ < MaxProspectsPerKind) next.Add(site);
                }
            }
            m.Prospects = next;
        }

        public static List<int> RestrictVisits(WorldState s, string kind, List<int> cells)
        {
            var community = s.Ecosystem.Succession?.Community;
            if (community == null || !Managed(kind)) return cells;
            var result = new List<int>();
            foreach (int c in cells)
                if (community.Families.Exists(f => f.Kind == kind && f.Adults > 0 && Dist(f.CellId, c) <= Reach(kind))) result.Add(c);
            return result;
        }
        public static int VisitLimit(WorldState s, string kind, int cap)
        {
            var community = s.Ecosystem.Succession?.Community;
            if (community == null || !Managed(kind)) return cap;
            int adults = 0; foreach (var f in community.Families) if (f.Kind == kind) adults += f.Adults;
            return Math.Min(cap, adults);
        }
        public static string YoungKind(ResidentFamily f)
        {
            if (f.BroodAge == 0) return f.Kind + "-eggs";
            if (f.Kind == Ecology.Butterfly) return f.BroodAge >= Development(f.Kind) - 2 ? Chrysalis : Caterpillar;
            if (f.Kind == LivingEcosystem.Ladybird) return f.BroodAge >= Development(f.Kind) - 1 ? "ladybird-pupa" : LadybirdLarva;
            if (f.Kind == LivingEcosystem.Hoverfly) return f.BroodAge >= Development(f.Kind) - 1 ? "hoverfly-pupa" : HoverflyLarva;
            return f.Kind == LivingEcosystem.Frog ? Tadpole : Nymph;
        }
        public static void AddYoungPresence(WorldState s, List<EcologyVisit> visits)
        {
            var m = s.Ecosystem.Succession?.Community; if (m == null) return;
            void Add(string kind, int c)
            {
                var v = visits.Find(x => x.Kind == kind);
                if (v == null) { v = new EcologyVisit { Kind = kind }; visits.Add(v); }
                if (!v.Cells.Contains(c)) v.Cells.Add(c);
            }
            foreach (var f in m.Families) if (f.Brood > 0) Add(YoungKind(f), f.CellId);
            int algae = 0, prey = 0;
            for (int c = 0; c < GardenMapData.CellCount; c++)
            {
                if (m.Algae[c] > 0 && algae++ < 4) Add(Algae, c);
                if (m.AquaticPrey[c] > 0 && prey++ < 4) Add(PondPrey, c);
            }
        }

        /// <summary>Life stages are one resident kind for harmony, not extra species.</summary>
        public static int ResidentKinds(IEnumerable<EcologyVisit> visits)
        {
            var kinds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var v in visits)
            {
                string kind = v.Kind;
                if (kind == Ecology.Spirit || kind == Ecology.Fish || kind == Ecology.Petals) continue;
                if (kind == Caterpillar || kind == Chrysalis) kind = Ecology.Butterfly;
                else if (kind == Tadpole) kind = LivingEcosystem.Frog;
                else if (kind == Nymph) kind = Ecology.Dragonfly;
                else if (kind == LadybirdLarva) kind = LivingEcosystem.Ladybird;
                else if (kind == HoverflyLarva) kind = LivingEcosystem.Hoverfly;
                else if (kind != null && (kind.EndsWith("-eggs", StringComparison.Ordinal) || kind.EndsWith("-pupa", StringComparison.Ordinal)))
                    kind = kind.Substring(0, kind.Length - 5);
                kinds.Add(kind);
            }
            return kinds.Count;
        }

        internal static void AddResidentHomes(WorldState s, List<EcologyVisit> presence)
        {
            var community = s.Ecosystem.Succession?.Community;
            if (community == null) return;
            foreach (var family in community.Families)
            {
                if (family.Adults <= 0) continue;
                var visit = presence.Find(v => v.Kind == family.Kind);
                if (visit == null)
                {
                    visit = new EcologyVisit { Kind = family.Kind };
                    presence.Add(visit);
                }
                if (!visit.Cells.Contains(family.CellId)) visit.Cells.Add(family.CellId);
            }
        }

        private static int EatArray(int[] resource, int cell, int demand) { int eaten = Math.Min(resource[cell], demand); resource[cell] -= eaten; return eaten; }
        private static int FeedYoung(WorldState s, ResidentFamily f, EcologyResult r, bool connectedHabitats)
        {
            if (f.BroodAge == 0 || YoungKind(f).Contains("pupa") || YoungKind(f) == Chrysalis) return f.Brood; // egg/yolk and pupal reserves
            var e = s.Ecosystem; var m = e.Succession.Community;
            string actor = YoungKind(f); int eaten = 0;
            if (f.Kind == Ecology.Butterfly)
            {
                var plant = s.PlantAt(f.CellId);
                if (!Healthy(s, plant) || !HostPlant(plant.Species)) return 0;
                int available = Math.Max(0, plant.Growth - Simulation.GrowthThreshold(PlantStage.Leaf));
                if (connectedHabitats) available -= available % CaterpillarMeal;
                int browsed = Math.Min(f.Brood * CaterpillarMeal, available);
                plant.Growth -= browsed;
                if (browsed > 0)
                {
                    plant.GrowthRemainder = 0;
                    plant.Stage = plant.Growth >= 2500 ? PlantStage.Flower : plant.Growth >= 1500 ? PlantStage.Bud : PlantStage.Leaf;
                    if (plant.Stage != PlantStage.Flower)
                    {
                        var cycle = e.Cycles[plant.PlantId]; cycle.BloomAge = 0; cycle.Pollen = 0;
                        if (e.Succession.Flowers.TryGetValue(plant.PlantId, out var flower)) flower.Nectar = 0;
                    }
                    Link(r, "host-leaf-eaten", actor, f.CellId, f.CellId, browsed);
                }
                return browsed / CaterpillarMeal;
            }
            if (Aquatic(f.Kind))
            {
                var food = NurseryFood(s, f.Kind);
                string meal = f.Kind == LivingEcosystem.Frog ? "algae-eaten" : "pond-prey-eaten";
                IEnumerable<int> cells = connectedHabitats ? NurseryWater(s, f.CellId) : new[] { f.CellId };
                foreach (int cell in cells)
                {
                    int amount = EatArray(food, cell, f.Brood - eaten);
                    if (amount > 0) Link(r, meal, actor, f.CellId, cell, amount);
                    eaten += amount;
                    if (eaten == f.Brood) break;
                }
                return eaten;
            }
            for (int c = 0; c < GardenMapData.CellCount && eaten < f.Brood; c++)
            {
                if (Dist(c, f.CellId) > NurseryPreyReach(f.Kind, connectedHabitats)) continue;
                int amount = EatArray(e.Aphids, c, f.Brood - eaten); eaten += amount;
                if (amount > 0) Link(r, "predation", actor, f.CellId, c, amount);
            }
            return eaten;
        }
        private static List<PlantState> OrderedPlants(WorldState s)
        {
            var plants = new List<PlantState>(s.Plants); plants.Sort((a, b) => a.CellId.CompareTo(b.CellId)); return plants;
        }
        private static bool AlternativeMeal(WorldState s, ResidentFamily f, EcologyResult r, bool connectedHabitats, WeatherDefinition weather)
        {
            if (f.Kind == LivingEcosystem.Ladybird)
                foreach (var p in OrderedPlants(s))
                    if (p.Stage == PlantStage.Flower && (!connectedHabitats || Healthy(s, p)) &&
                        OpenFlower(p.Species) && Dist(f.CellId, p.CellId) <= Reach(f.Kind) &&
                        SuccessionRules.Forage(s, p, f.Kind, r)) return true;
            if (connectedHabitats && weather?.Id != WeatherId.Drizzle &&
                (f.Kind == Ecology.Butterfly || f.Kind == LivingEcosystem.Hoverfly))
            {
                // An adult without a meal can take another reachable flower after the first visits.
                var flowers = new List<int>();
                foreach (var p in s.Plants)
                    if (p.Stage == PlantStage.Flower && Healthy(s, p) && Dist(f.CellId, p.CellId) <= Reach(f.Kind) &&
                        (f.Kind == Ecology.Butterfly || OpenFlower(p.Species))) flowers.Add(p.CellId);
                var chosen = SuccessionRules.SelectFlowers(s, f.Kind, flowers, 1);
                if (chosen.Count == 0) return false;
                var flower = s.PlantAt(chosen[0]);
                if (!SuccessionRules.Forage(s, flower, f.Kind, r)) return false;
                SuccessionRules.DepositPollen(s, flower, f.Kind, r);
                return true;
            }
            if (!Aquatic(f.Kind)) return false;
            var m = s.Ecosystem.Succession.Community;
            for (int c = 0; c < GardenMapData.CellCount; c++)
            {
                if (Dist(c, f.CellId) > Reach(f.Kind)) continue;
                if (s.Ecosystem.Aphids[c] > 0)
                { s.Ecosystem.Aphids[c]--; Link(r, "predation", f.Kind, f.CellId, c, 1); return true; }
                if (connectedHabitats) continue;
                if (m.AquaticPrey[c] > 0)
                { m.AquaticPrey[c]--; Link(r, "pond-prey-eaten", f.Kind, f.CellId, c, 1); return true; }
            }
            if (connectedHabitats)
                for (int c = 0; c < GardenMapData.CellCount; c++)
                {
                    if (m.AquaticPrey[c] == 0 || Dist(c, f.CellId) > Reach(f.Kind) || !WetNursery(s, c)) continue;
                    if (m.AquaticPrey[c] <= 2 && m.Families.Exists(other => other.Kind == Ecology.Dragonfly &&
                        other.Brood > 0 && Dist(other.CellId, c) <= 1 && WetNursery(s, other.CellId))) continue;
                    m.AquaticPrey[c]--;
                    Link(r, "pond-prey-eaten", f.Kind, f.CellId, c, 1);
                    return true;
                }
            return false;
        }
        private static void ReturnBiomass(WorldState s, ResidentFamily f, EcologyResult r, string eventKind, int count)
        {
            s.Ecosystem.Litter[f.CellId] = Math.Min(LivingEcosystem.ResourceCap, s.Ecosystem.Litter[f.CellId] + count);
            Link(r, eventKind, f.Kind, f.CellId, f.CellId, count);
        }
        public static void AfterActivity(WorldState s, EcologyResult r) => AfterActivity(s, r, false);

        public static void AfterActivity(WorldState s, EcologyResult r, bool connectedHabitats, WeatherDefinition weather = null)
        {
            var m = s.Ecosystem.Succession.Community;
            int original = r.Relations.Count; var remaining = new int[original];
            for (int i = 0; i < original; i++) if (r.Relations[i].Kind == "nectar-fed" || r.Relations[i].Kind == "predation") remaining[i] = r.Relations[i].Amount;
            var families = new List<ResidentFamily>(m.Families);
            families.Sort((a, b) => ((a.CellId + s.WorldRevision * 13) % 256) != ((b.CellId + s.WorldRevision * 13) % 256)
                ? ((a.CellId + s.WorldRevision * 13) % 256).CompareTo((b.CellId + s.WorldRevision * 13) % 256)
                : string.CompareOrdinal(a.FamilyId, b.FamilyId));
            foreach (var f in families)
            {
                bool habitat = HabitatSuitable(s, f.Kind, f.CellId);
                bool nurseryHabitat = habitat || (connectedHabitats && Aquatic(f.Kind) &&
                    WetNursery(s, f.CellId) && CoverNear(s, f.CellId));
                f.BadHabitat = nurseryHabitat ? 0 : Math.Min(4, f.BadHabitat + 1);
                if (!nurseryHabitat && f.BadHabitat >= 2 && f.Brood == 0)
                {
                    for (int c = 0; c < GardenMapData.CellCount; c++)
                        if (Dist(c, f.CellId) <= 4 && HabitatSuitable(s, f.Kind, c) && !m.Families.Exists(other => other != f && other.Kind == f.Kind && other.CellId == c))
                        { int old = f.CellId; f.CellId = c; f.BadHabitat = 0; habitat = true; Link(r, "refuge-moved", f.Kind, old, c, f.Adults); break; }
                }
                if (f.Brood > 0)
                {
                    int fed = nurseryHabitat ? FeedYoung(s, f, r, connectedHabitats) : 0;
                    if (fed == f.Brood) { f.BroodAge++; f.NurseryHunger = 0; }
                    else f.NurseryHunger = Math.Min(3, f.NurseryHunger + 1);
                    if (f.NurseryHunger >= 3)
                    { ReturnBiomass(s, f, r, "brood-lost", f.Brood); f.Brood = 0; f.BroodAge = 0; f.NurseryHunger = 0; }
                    else if (f.BroodAge >= Development(f.Kind))
                    {
                        int added = Math.Min(f.Brood, MaxAdults - f.Adults);
                        f.Adults += added;
                        if (added > 0) Link(r, "resident-emerged", f.Kind, f.CellId, f.CellId, added);
                        if (f.Brood > added) Link(r, "resident-dispersed", f.Kind, f.CellId, f.CellId, f.Brood - added);
                        f.Brood = 0; f.BroodAge = 0; f.Cooldown = 3;
                    }
                }
                int meals = 0;
                for (int adult = 0; adult < f.Adults; adult++)
                {
                    bool ate = false;
                    for (int i = 0; i < original; i++)
                        if (remaining[i] > 0 && r.Relations[i].Actor == f.Kind &&
                            Dist(f.CellId, connectedHabitats && r.Relations[i].Kind == "predation"
                                ? r.Relations[i].FromCell : r.Relations[i].ToCell) <= Reach(f.Kind))
                        { remaining[i]--; ate = true; break; }
                    if (!ate) ate = AlternativeMeal(s, f, r, connectedHabitats, weather);
                    if (ate) meals++;
                }
                f.AdultHunger = meals == f.Adults ? Math.Max(0, f.AdultHunger - 2) : Math.Min(6, f.AdultHunger + 1);
                f.Age++; if (f.Cooldown > 0) f.Cooldown--;
                if (f.Adults > 0 && (f.AdultHunger >= 6 || f.Age >= 96))
                { f.Adults--; f.AdultHunger = 0; f.Age = 0; ReturnBiomass(s, f, r, "resident-returned", 1); }
                if (f.BadHabitat >= 4)
                {
                    if (f.Adults > 0) Link(r, "resident-left", f.Kind, f.CellId, f.CellId, f.Adults);
                    f.Adults = 0;
                    if (f.Brood > 0) ReturnBiomass(s, f, r, "brood-lost", f.Brood);
                    f.Brood = 0; f.BroodAge = 0;
                }
                if (f.Adults > 0 && habitat && meals >= f.Adults && f.Brood == 0 && f.Cooldown == 0 && Attractive(s, f.Kind, f.CellId))
                {
                    int brood = connectedHabitats ? LocalBroodSize(s, f) : MaxBrood;
                    if (brood > 0)
                    {
                        f.Brood = brood; f.BroodAge = 0; f.NurseryHunger = 0;
                        Link(r, "brood-laid", f.Kind, f.CellId, f.CellId, f.Brood);
                    }
                }
                if (f.Adults == 0 && f.Brood == 0) m.Families.Remove(f);
            }
        }
    }
}
