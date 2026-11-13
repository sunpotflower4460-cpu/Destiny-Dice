using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    public static class HabitatRules
    {
        public const string RulesVersion = "ecology.v7", Vegetative = "vegetative";
        public const int HumusCap = 100, ReserveCap = 12, RootCost = 6, RootNutrients = 2, RootGrowthCost = 100;
        public static readonly string[] Labels = { "水辺", "岩陰", "草木の陰", "乾いた草地", "花の草地" };
        public static string RootRoute(string species) => species == "strawberry" || species == "thyme" ? "stolon"
            : species == "mint" || species == "violet" ? "rhizome" : null;
        private static void Link(EcologyResult r, string kind, string actor, int from, int to, int amount, string species = null,
            string fromHabitat = null, string toHabitat = null) => r.Relations.Add(new EcologicalLink
            { Kind = kind, Actor = actor, FromCell = from, ToCell = to, Amount = amount, Species = species, FromHabitat = fromHabitat, ToHabitat = toHabitat });

        /// <summary>Hard species limits stay authoritative; within them, the quality of a site varies.</summary>
        public static int Suitability(SpeciesTraits t, WorldState s, int c)
        {
            if (t == null || !Flora.Fits(t, s, c)) return 0;
            int Window(int value, int min, int max) => Math.Max(600, 1000 - Math.Abs(value - (min + max) / 2) * 400 / Math.Max(1, (max - min) / 2));
            int wet = Window(Flora.Moisture(s, c), t.MoistMin, t.MoistMax);
            int drainage = Window(Flora.Drainage(s, c), t.DrainMin, t.DrainMax);
            int soft = Math.Max(600, 1000 - Flora.Hardness(s, c) * 400 / Math.Max(1, t.MaxHardness));
            int light = t.Light == 2 && !Flora.IsRockShade(c) ? 800 : 1000;
            return Math.Min(Math.Min(wet, drainage), Math.Min(soft, light));
        }
        public static void Begin(WorldState s, EcologyResult r)
        {
            var h = s.Ecosystem.Habitat;
            for (int c = 0; c < GardenMapData.CellCount; c++)
            {
                if (GardenMapData.SoilCapacity[c] == 0 || h.Humus[c] == 0 || s.WorldRevision % 4 != c % 4) continue;
                int moisture = Flora.Moisture(s, c);
                if (moisture < 300 || moisture > 850 || s.Ecosystem.Nutrients[c] >= LivingEcosystem.ResourceCap) continue;
                h.Humus[c]--; s.Ecosystem.Nutrients[c]++;
                Link(r, "humus-mineralised", "soil-life", c, c, 1);
            }
        }
        /// <summary>Split actual litter into lasting humus and immediately available nutrients, with no duplicated matter.</summary>
        public static void Decompose(WorldState s, int c, string actor, int demand, EcologyResult r)
        {
            var e = s.Ecosystem; var h = e.Habitat;
            int nRoom = LivingEcosystem.ResourceCap - e.Nutrients[c], hRoom = HumusCap - h.Humus[c];
            int amount = Math.Min(demand, Math.Min(e.Litter[c], nRoom + hRoom));
            if (amount <= 0) return;
            int humus = Math.Min(hRoom, Math.Max(amount >= 2 ? 1 : 0, amount - nRoom));
            int nutrients = amount - humus;
            e.Litter[c] -= amount; h.Humus[c] += humus; e.Nutrients[c] += nutrients;
            if (humus > 0) Link(r, "humus-formed", actor, c, c, humus);
            if (nutrients > 0) Link(r, "decomposed", actor, c, c, nutrients);
        }
        public static void End(WorldState s, EcologyResult r, Func<int, string> newPlantId)
        {
            var h = s.Ecosystem.Habitat; var alive = new HashSet<string>();
            var plants = new List<PlantState>(s.Plants); plants.Sort((a, b) => a.CellId.CompareTo(b.CellId));
            foreach (var p in plants)
            {
                alive.Add(p.PlantId); if (RootRoute(p.Species) == null) continue;
                h.RootReserves.TryGetValue(p.PlantId, out int reserve);
                bool healthy = s.Ecosystem.Cycles.TryGetValue(p.PlantId, out var cycle) && cycle.Stress < 12;
                int add = healthy && p.Stage >= PlantStage.Leaf && Suitability(Flora.Get(p.Species), s, p.CellId) >= 700 ? 1 : -2;
                h.RootReserves[p.PlantId] = Math.Max(0, Math.Min(ReserveCap, reserve + add));
            }
            foreach (string id in new List<string>(h.RootReserves.Keys)) if (!alive.Contains(id)) h.RootReserves.Remove(id);
            int born = 0; foreach (var effect in r.Effects) if (effect.Seed) born++;
            if (born < Ecology.MaxSeedsPerRoll)
                foreach (var parent in plants)
                {
                    if (!h.RootReserves.TryGetValue(parent.PlantId, out int reserve) || reserve < RootCost || parent.Stage < PlantStage.Leaf ||
                        parent.Stage == PlantStage.Flower || parent.Growth < 1000 || s.Ecosystem.Nutrients[parent.CellId] < RootNutrients ||
                        s.Ecosystem.Cycles[parent.PlantId].Stress >= 12 || Suitability(Flora.Get(parent.Species), s, parent.CellId) < 700) continue;
                    var targets = new List<int>();
                    for (int c = 0; c < GardenMapData.CellCount; c++)
                        if (GardenMapData.Plantable[c] == 1 && s.PlantAt(c) == null && LivingEcosystem.Distance(c, parent.CellId) == 1 &&
                            Suitability(Flora.Get(parent.Species), s, c) >= 700) targets.Add(c);
                    targets.Sort((a, b) => Suitability(Flora.Get(parent.Species), s, a) != Suitability(Flora.Get(parent.Species), s, b)
                        ? Suitability(Flora.Get(parent.Species), s, b).CompareTo(Suitability(Flora.Get(parent.Species), s, a))
                        : ((a + s.WorldRevision * 13) % 256).CompareTo((b + s.WorldRevision * 13) % 256));
                    if (targets.Count == 0) continue;
                    int target = targets[0]; string route = RootRoute(parent.Species);
                    string id = newPlantId != null ? newPlantId(born) : "root-" + s.WorldRevision + "-" + target;
                    var child = new PlantState { PlantId = id, CellId = target, Species = parent.Species, Growth = 250,
                        Stage = PlantStage.Sprout, OriginKind = Vegetative };
                    s.Plants.Add(child); s.Ecosystem.Cycles.Add(id, new PlantCycle());
                    s.Ecosystem.Succession.Ancestry.Add(id, new PlantAncestry { ParentPlantId = parent.PlantId, Carrier = route, BornRevision = s.WorldRevision });
                    h.RootReserves[parent.PlantId] -= RootCost; s.Ecosystem.Nutrients[parent.CellId] -= RootNutrients; parent.Growth -= RootGrowthCost;
                    parent.GrowthRemainder = 0; parent.Stage = parent.Growth >= 1500 ? PlantStage.Bud : PlantStage.Leaf;
                    string from = LivingEcosystem.Habitat(s, parent.CellId), to = LivingEcosystem.Habitat(s, target);
                    Link(r, "clonal-rooted", route, parent.CellId, target, 1, parent.Species, from, to);
                    r.Effects.Add(new EcologyEffect { Kind = Vegetative, CellId = target, Amount = 250 });
                    r.Events.Add(new GardenEvent { Kind = "VegetativeRooted", CellId = target, PlantId = id, Tick = 40 });
                    break; // one root-derived child per roll, sharing the existing two-birth cap
                }
            for (int c = 0; c < GardenMapData.CellCount; c++)
            {
                if (GardenMapData.Plantable[c] != 1) continue;
                int now = Array.IndexOf(Labels, LivingEcosystem.Habitat(s, c)), before = h.LastHabitat[c];
                if (now == before) h.StableAge[c] = Math.Min(96, h.StableAge[c] + 1);
                else
                {
                    if (before >= 0 && h.StableAge[c] >= 6) Link(r, "habitat-shifted", "succession", c, c, h.StableAge[c], null, Labels[before], Labels[now]);
                    h.LastHabitat[c] = now; h.StableAge[c] = 1;
                }
            }
        }
    }
}
