using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>v5 extends v4 only when optional succession memory was saved at run creation.</summary>
    public static class SuccessionRules
    {
        public const string RulesVersion = "ecology.v5", SeedBank = "seedbank";
        public const int MaxBank = 96, MaxPerCell = 3, MaxDeposits = 2, NectarCap = 6;

        private static void Link(EcologyResult r, string kind, string actor, int from, int to, int amount, string species) =>
            r.Relations.Add(new EcologicalLink { Kind = kind, Actor = actor, FromCell = from, ToCell = to, Amount = amount, Species = species });
        public static bool HasSpace(WorldState s, int cell)
        {
            var bank = s.Ecosystem.Succession.SeedBank;
            if (cell < 0 || cell >= GardenMapData.CellCount || GardenMapData.Plantable[cell] != 1 || bank.Count >= MaxBank) return false;
            int count = 0; foreach (var seed in bank) if (seed.CellId == cell) count++;
            return count < MaxPerCell;
        }
        public static int Deposits(EcologyResult r)
        {
            int n = 0; foreach (var link in r.Relations) if (link.Kind == "seed-buried") n += link.Amount; return n;
        }
        public static bool Store(WorldState s, PlantState parent, int target, string carrier, string roundId, EcologyResult r)
        {
            int count = Deposits(r);
            if (count >= MaxDeposits || !HasSpace(s, target)) return false;
            var seed = new RestingSeed
            {
                SeedId = roundId + ":" + s.WorldRevision + ":seed:" + count,
                Species = parent.Species, ParentPlantId = parent.PlantId, ParentCellId = parent.CellId,
                CellId = target, Carrier = carrier, SourceRoundId = roundId, BornRevision = s.WorldRevision,
                Rested = PlantStrategies.SeasonAt(s) == 3
            };
            s.Ecosystem.Succession.SeedBank.Add(seed);
            Link(r, "seed-buried", carrier, parent.CellId, target, 1, parent.Species);
            r.Events.Add(new GardenEvent { Kind = "SeedStored", PlantId = parent.PlantId, CellId = target, Tick = 40 });
            return true;
        }
        public static void ReleaseRipeSeed(WorldState s, PlantState parent, PlantCycle cycle, string roundId, EcologyResult r)
        {
            if (cycle.Seeds > 0 && Store(s, parent, parent.CellId, LivingEcosystem.SeedFall, roundId, r)) cycle.Seeds--;
        }
        public static void AgeSeedBank(WorldState s, EcologyResult r)
        {
            var bank = s.Ecosystem.Succession.SeedBank;
            foreach (var seed in new List<RestingSeed>(bank))
            {
                if (seed.BornRevision >= s.WorldRevision) continue;
                seed.Age++;
                if (PlantStrategies.SeasonAt(s) == 3) seed.Rested = true;
                if (seed.Age <= SeedStrategies.Get(seed.Species).Persistence) continue;
                int returned = Math.Min(1, LivingEcosystem.ResourceCap - s.Ecosystem.Litter[seed.CellId]);
                s.Ecosystem.Litter[seed.CellId] += returned;
                Link(r, "seed-expired", SeedBank, seed.CellId, seed.CellId, returned, seed.Species);
                bank.Remove(seed);
            }
        }
        public static string WaitingFor(WorldState s, RestingSeed seed)
        {
            var strategy = SeedStrategies.Get(seed.Species);
            if (seed.Age < strategy.MinimumAge) return "種が落ち着くのを待っている";
            if (s.PlantAt(seed.CellId) != null) return "根を張る空き地を待っている";
            if (strategy.NeedsRestSeason && !seed.Rested) return "休息の頃を待っている";
            if (strategy.NeedsRestSeason && PlantStrategies.SeasonAt(s) != 0) return "芽吹きの頃を待っている";
            if (!Flora.Fits(Flora.Get(seed.Species), s, seed.CellId)) return "水・光・土の条件を待っている";
            if (s.Ecosystem.Nutrients[seed.CellId] < 2) return "土に養分が戻るのを待っている";
            return null;
        }
        public static void Awaken(WorldState s, WorldState afterSim, EcologyResult r, Func<int, string> newPlantId)
        {
            var memory = s.Ecosystem.Succession;
            var seeds = new List<RestingSeed>(memory.SeedBank);
            seeds.Sort((a, b) => a.Age != b.Age ? b.Age.CompareTo(a.Age)
                : Rotation(s, a.CellId).CompareTo(Rotation(s, b.CellId)) != 0
                    ? Rotation(s, a.CellId).CompareTo(Rotation(s, b.CellId)) : string.CompareOrdinal(a.SeedId, b.SeedId));
            int emerged = 0;
            foreach (var seed in seeds)
            {
                if (emerged >= Ecology.MaxSeedsPerRoll) break;
                // A retiring root opens the site on the following roll, after its litter has settled.
                if (afterSim.PlantAt(seed.CellId) != null || WaitingFor(s, seed) != null) continue;
                string id = newPlantId != null ? newPlantId(emerged) : "eco-" + s.WorldRevision + "-" + seed.CellId;
                var plant = new PlantState { PlantId = id, Species = seed.Species, CellId = seed.CellId,
                    Stage = PlantStage.Sprout, Growth = 250, OriginKind = "ecology", SeedSourceRoundId = seed.SourceRoundId };
                s.Plants.Add(plant); s.Ecosystem.Cycles.Add(id, new PlantCycle());
                memory.Ancestry.Add(id, new PlantAncestry { ParentPlantId = seed.ParentPlantId, SeedId = seed.SeedId,
                    Carrier = seed.Carrier, BornRevision = s.WorldRevision });
                memory.SeedBank.Remove(seed); s.Ecosystem.Nutrients[seed.CellId] -= 2; emerged++;
                r.Effects.Add(new EcologyEffect { Kind = SeedBank, CellId = seed.CellId, Seed = true });
                r.Effects.Add(new EcologyEffect { Kind = Ecology.Germinate, CellId = seed.CellId, Species = seed.Species });
                Link(r, "seed-awakened", SeedBank, seed.ParentCellId, seed.CellId, 1, seed.Species);
                r.Events.Add(new GardenEvent { Kind = "Germinated", PlantId = id, CellId = seed.CellId, Tick = 40 });
            }
        }

        public static void RefreshNectar(WorldState s, WeatherDefinition weather)
        {
            var memory = s.Ecosystem.Succession;
            foreach (var p in s.Plants)
            {
                if (!memory.Flowers.TryGetValue(p.PlantId, out var flower))
                { flower = new FlowerResource(); memory.Flowers.Add(p.PlantId, flower); }
                bool healthy = !s.Ecosystem.Cycles.TryGetValue(p.PlantId, out var life) || life.Stress < 12;
                if (p.Stage != PlantStage.Flower || !healthy) { flower.Nectar = 0; continue; }
                int refill = weather == null || weather.Id == WeatherId.Clear ? 2 : weather.Id == WeatherId.Cloudy ? 1 : 0;
                if (s.Ecosystem.Habitat != null && HabitatRules.Suitability(Flora.Get(p.Species), s, p.CellId) < 800) refill = Math.Min(refill, 1);
                if (Flora.Fits(Flora.Get(p.Species), s, p.CellId)) flower.Nectar = Math.Min(NectarCap, flower.Nectar + refill);
            }
        }
        public static bool Forage(WorldState s, PlantState p, string actor, EcologyResult r)
        {
            var memory = s.Ecosystem.Succession;
            if (p.Stage != PlantStage.Flower || !memory.Flowers.TryGetValue(p.PlantId, out var flower) || flower.Nectar <= 0) return false;
            flower.Nectar--; flower.LastForaged = s.WorldRevision;
            Link(r, "nectar-fed", actor, p.CellId, p.CellId, 1, p.Species);
            return true;
        }
        private static int Rotation(WorldState s, int cell) => (cell + s.WorldRevision * 13) % GardenMapData.CellCount;
        public static List<int> SelectVisits(WorldState s, string kind, List<int> candidates, int cap)
        {
            var list = new List<int>(candidates);
            bool pollinator = kind == Ecology.Bee || kind == Ecology.Butterfly || kind == LivingEcosystem.Hoverfly;
            // Hoverfly Presence cells are prey cells; flower routing is requested separately by caller.
            if (pollinator && kind != LivingEcosystem.Hoverfly) return SelectFlowers(s, kind, list, cap);
            bool predator = kind == LivingEcosystem.Ladybird || kind == LivingEcosystem.Spider || kind == LivingEcosystem.Hoverfly;
            bool decomposer = kind == Ecology.Worm || kind == Ecology.Mushroom || kind == LivingEcosystem.Springtail || kind == LivingEcosystem.Snail;
            int Score(int c) => predator ? s.Ecosystem.Aphids[c]
                : decomposer ? Math.Min(s.Ecosystem.Litter[c], LivingEcosystem.ResourceCap - s.Ecosystem.Nutrients[c] +
                    (s.Ecosystem.Habitat != null ? HabitatRules.HumusCap - s.Ecosystem.Habitat.Humus[c] : 0)) : 0;
            list.Sort((a, b) => Score(a) != Score(b) ? Score(b).CompareTo(Score(a)) : Rotation(s, a).CompareTo(Rotation(s, b)));
            return list.GetRange(0, Math.Min(cap, list.Count));
        }
        public static List<int> SelectFlowers(WorldState s, string kind, List<int> candidates, int cap)
        {
            var m = s.Ecosystem.Succession;
            var list = new List<int>();
            foreach (int c in candidates)
            {
                var p = s.PlantAt(c);
                if (p == null || p.Stage != PlantStage.Flower) continue;
                var traits = Flora.Get(p.Species);
                int affinity = traits == null ? 0 : kind == Ecology.Butterfly ? traits.Butterfly : traits.Bee;
                if (affinity > 0 && m.Flowers.TryGetValue(p.PlantId, out var f) && f.Nectar > 0) list.Add(c);
            }
            int Pollen(int c) => s.Ecosystem.Cycles.TryGetValue(s.PlantAt(c).PlantId, out var p) && p.Pollen >= 2 ? 1 : 0;
            int Last(int c) => m.Flowers[s.PlantAt(c).PlantId].LastForaged;
            int Affinity(int c) => kind == Ecology.Butterfly ? Flora.Get(s.PlantAt(c).Species).Butterfly : Flora.Get(s.PlantAt(c).Species).Bee;
            list.Sort((a, b) => Pollen(a) != Pollen(b) ? Pollen(a).CompareTo(Pollen(b))
                : Last(a) != Last(b) ? Last(a).CompareTo(Last(b))
                : Affinity(a) != Affinity(b) ? Affinity(b).CompareTo(Affinity(a)) : Rotation(s, a).CompareTo(Rotation(s, b)));
            return list.GetRange(0, Math.Min(cap, list.Count));
        }
        public static void CleanMemory(WorldState s)
        {
            var m = s.Ecosystem.Succession;
            var alive = new HashSet<string>(); foreach (var p in s.Plants) alive.Add(p.PlantId);
            foreach (string id in new List<string>(m.Flowers.Keys)) if (!alive.Contains(id)) m.Flowers.Remove(id);
            foreach (string id in new List<string>(m.Ancestry.Keys)) if (!alive.Contains(id)) m.Ancestry.Remove(id);
        }
    }
}
