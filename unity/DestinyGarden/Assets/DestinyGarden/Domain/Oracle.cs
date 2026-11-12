using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    public sealed class ObservationGroup
    {
        public string GroupId;
        public string Kind;
        public List<int> CellIds = new List<int>();
        public List<string> PlantIds = new List<string>();
        public int StageRank;
    }

    public sealed class SelectedMessage
    {
        public string TemplateId;
        public string Text;
        public string CardId; // null when card unavailable
        public string FocusGroupId;
        public string SecondaryGroupId;
        public int FocusCellId = -1;
        public int SecondaryCellId = -1;
        public string FallbackReason; // null for factual selection
        public int[] ScoreTuple; // cardAffinity, recentNovelty, priority
    }

    /// <summary>
    /// Oracle selection (oracle-focus.v0.7). Facts come only from RoundEvidence and
    /// stage events of this round. verified_path_to_root stays false in this version
    /// because path evidence is not recorded, so its entry can never be selected.
    /// </summary>
    public static class Oracle
    {
        private sealed class Candidate
        {
            public OracleEntry Entry;
            public ObservationGroup Group;
            public int CellId;
            public string PlantId;
            public int CardAffinity;
            public int RecentNovelty;
        }

        public static List<ObservationGroup> BuildGroups(string roundId, SimulationResult sim)
        {
            var ev = sim.Evidence;
            var groups = new List<ObservationGroup>();
            AddComponents(groups, roundId, "WaterEntered:dice", ev.DiceWaterCells);
            var absorbed = new HashSet<int>();
            for (int i = 0; i < 256; i++) if (ev.CumulativeAbsorbed[i] > 0) absorbed.Add(i);
            AddComponents(groups, roundId, "Infiltrated", absorbed);
            AddComponents(groups, roundId, "PuddleFormed", ev.PuddleCells);
            AddComponents(groups, roundId, "LightReceived", ev.LightReceivedCells);
            AddComponents(groups, roundId, "WindReceived", ev.WindReceivedCells);
            AddComponents(groups, roundId, "WindSheltered", ev.WindShelteredCells);

            var seedCells = new HashSet<int>();
            foreach (var e in sim.Events) if (e.Kind == "SeedPlaced") seedCells.Add(e.CellId);
            AddComponents(groups, roundId, "SeedPlaced", seedCells, sim);

            foreach (var kind in new[] { "Germinated", "LeafOpened", "BudFormed", "Bloomed" })
            {
                var cells = new HashSet<int>();
                foreach (var e in sim.Events) if (e.Kind == kind) cells.Add(e.CellId);
                AddComponents(groups, roundId, kind, cells, sim);
            }

            var growthCells = new HashSet<int>();
            foreach (var p in sim.PostState.Plants)
                if (ev.GrowthBeforeByPlant.TryGetValue(p.PlantId, out int bg) && p.Growth > bg)
                    growthCells.Add(p.CellId);
            AddComponents(groups, roundId, "GrowthObserved", growthCells, sim);

            if (ev.SurfaceFlowTotal > 0)
            {
                groups.Add(new ObservationGroup
                {
                    GroupId = roundId + ":SurfaceFlowObserved:global",
                    Kind = "SurfaceFlowObserved"
                });
            }
            foreach (var g in groups)
            {
                g.StageRank = g.Kind == "Bloomed" ? 4 : g.Kind == "BudFormed" ? 3
                    : g.Kind == "LeafOpened" ? 2 : g.Kind == "Germinated" ? 1 : 0;
            }
            return groups;
        }

        private static void AddComponents(List<ObservationGroup> groups, string roundId, string kind,
            HashSet<int> cells, SimulationResult sim = null)
        {
            var remaining = new HashSet<int>(cells);
            while (remaining.Count > 0)
            {
                int seed = int.MaxValue;
                foreach (int c in remaining) if (c < seed) seed = c;
                var component = new List<int>();
                var stack = new Stack<int>();
                stack.Push(seed); remaining.Remove(seed);
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    component.Add(c);
                    int x = c % 16, y = c / 16;
                    TryPush(remaining, stack, x, y - 1); TryPush(remaining, stack, x + 1, y);
                    TryPush(remaining, stack, x, y + 1); TryPush(remaining, stack, x - 1, y);
                }
                component.Sort();
                var g = new ObservationGroup
                {
                    GroupId = roundId + ":" + kind + ":" + component[0],
                    Kind = kind,
                    CellIds = component
                };
                if (sim != null)
                {
                    foreach (var p in sim.PostState.Plants)
                        if (component.Contains(p.CellId) && !g.PlantIds.Contains(p.PlantId))
                            g.PlantIds.Add(p.PlantId);
                    g.PlantIds.Sort(StringComparer.Ordinal);
                }
                groups.Add(g);
            }
        }

        private static void TryPush(HashSet<int> remaining, Stack<int> stack, int x, int y)
        {
            if (x < 0 || x >= 16 || y < 0 || y >= 16) return;
            int id = y * 16 + x;
            if (remaining.Remove(id)) stack.Push(id);
        }

        public static SelectedMessage Select(string roundId, SimulationResult sim, ResolvedRecipe dice,
            string cardId, IList<string> recentMessageIds)
        {
            string element = dice.Element.ToString().ToLowerInvariant();
            if (cardId == null)
                return Fallback(element, null, "card_unavailable");

            var groups = BuildGroups(roundId, sim);
            var ev = sim.Evidence;
            var candidates = new List<Candidate>();

            foreach (var entry in OracleCatalogData.Entries)
            {
                if (entry.Kind != "factual") continue;
                if (Array.IndexOf(entry.AllowedElements, element) < 0) continue;
                foreach (var binding in FindBindings(entry, sim, dice, groups))
                {
                    candidates.Add(new Candidate
                    {
                        Entry = entry,
                        Group = binding.Item1,
                        CellId = binding.Item2,
                        PlantId = binding.Item3,
                        CardAffinity = Array.IndexOf(entry.CardTags, cardId) >= 0 ? 1 : 0,
                        RecentNovelty = recentMessageIds != null && ContainsId(recentMessageIds, entry.Id) ? 0 : 1
                    });
                }
            }
            if (candidates.Count == 0)
                return Fallback(element, cardId, "no_factual_candidate");

            candidates.Sort(CompareFinalRanking);
            var winner = candidates[0];

            // Slot groups: fixed-importance top, winner's group, direct-input top.
            var slotGroups = new List<ObservationGroup>();
            void AddSlot(ObservationGroup g)
            {
                if (g != null && !slotGroups.Contains(g) && slotGroups.Count < 3) slotGroups.Add(g);
            }
            AddSlot(TopFixedImportanceGroup(candidates, null));
            AddSlot(winner.Group);
            AddSlot(TopFixedImportanceGroup(candidates, IsDirectInputKind));

            ObservationGroup secondary = null;
            int secondaryCell = -1;
            if (winner.Entry.Scope == "input_pair")
            {
                foreach (var g in groups)
                {
                    if (g.Kind == "SeedPlaced" && g != winner.Group) { secondary = g; break; }
                }
                if (secondary != null && secondary.CellIds.Count > 0) secondaryCell = secondary.CellIds[0];
            }
            if (secondary == null)
            {
                var ordered = new List<ObservationGroup>(slotGroups);
                ordered.Sort((a, b) => CompareFixedImportance(a, b, candidates));
                foreach (var g in ordered)
                    if (g != winner.Group) { secondary = g; break; }
                if (secondary != null && secondary.CellIds.Count > 0) secondaryCell = secondary.CellIds[0];
            }

            return new SelectedMessage
            {
                TemplateId = winner.Entry.Id,
                Text = winner.Entry.Text,
                CardId = cardId,
                FocusGroupId = winner.Group.GroupId,
                SecondaryGroupId = secondary != null ? secondary.GroupId : null,
                FocusCellId = winner.CellId,
                SecondaryCellId = secondaryCell,
                ScoreTuple = new[] { winner.CardAffinity, winner.RecentNovelty, winner.Entry.Priority }
            };
        }

        private static bool ContainsId(IList<string> list, string id)
        {
            foreach (string s in list) if (s == id) return true;
            return false;
        }

        private static bool IsDirectInputKind(string kind) =>
            kind == "WaterEntered:dice" || kind == "WindReceived" || kind == "LightReceived" || kind == "SeedPlaced";

        private static int CompareFinalRanking(Candidate a, Candidate b)
        {
            int c = b.CardAffinity.CompareTo(a.CardAffinity);
            if (c != 0) return c;
            c = b.RecentNovelty.CompareTo(a.RecentNovelty);
            if (c != 0) return c;
            c = b.Entry.Priority.CompareTo(a.Entry.Priority);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Entry.Id, b.Entry.Id);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Group.GroupId, b.Group.GroupId);
            if (c != 0) return c;
            return a.CellId.CompareTo(b.CellId);
        }

        private static int MaxPriorityForGroup(ObservationGroup g, List<Candidate> candidates)
        {
            int max = -1;
            foreach (var c in candidates) if (c.Group == g) max = Math.Max(max, c.Entry.Priority);
            return max;
        }

        private static int CompareFixedImportance(ObservationGroup a, ObservationGroup b, List<Candidate> candidates)
        {
            int c = MaxPriorityForGroup(b, candidates).CompareTo(MaxPriorityForGroup(a, candidates));
            if (c != 0) return c;
            c = b.StageRank.CompareTo(a.StageRank);
            if (c != 0) return c;
            return string.CompareOrdinal(a.GroupId, b.GroupId);
        }

        private static ObservationGroup TopFixedImportanceGroup(List<Candidate> candidates, Func<string, bool> kindFilter)
        {
            var seen = new List<ObservationGroup>();
            foreach (var c in candidates)
            {
                if (kindFilter != null && !kindFilter(c.Group.Kind)) continue;
                if (!seen.Contains(c.Group)) seen.Add(c.Group);
            }
            if (seen.Count == 0) return null;
            seen.Sort((a, b) => CompareFixedImportance(a, b, candidates));
            return seen[0];
        }

        private static SelectedMessage Fallback(string element, string cardId, string reason)
        {
            foreach (var e in OracleCatalogData.Entries)
            {
                if (e.Kind == "fallback" && Array.IndexOf(e.AllowedElements, element) >= 0)
                {
                    return new SelectedMessage
                    {
                        TemplateId = e.Id, Text = e.Text,
                        CardId = reason == "card_unavailable" ? null : cardId,
                        FallbackReason = reason
                    };
                }
            }
            throw new InvalidOperationException("No fallback entry for element " + element);
        }

        private static ObservationGroup GroupContaining(List<ObservationGroup> groups, string kind, int cellId)
        {
            foreach (var g in groups)
                if (g.Kind == kind && g.CellIds.Contains(cellId)) return g;
            return null;
        }

        private static bool Has(OracleEntry e, string predicate) => Array.IndexOf(e.RequiresAll, predicate) >= 0;

        /// <summary>All complete same-subject bindings for one entry, in (cellId, plantId) order.</summary>
        private static List<Tuple<ObservationGroup, int, string>> FindBindings(
            OracleEntry entry, SimulationResult sim, ResolvedRecipe dice, List<ObservationGroup> groups)
        {
            var result = new List<Tuple<ObservationGroup, int, string>>();
            var ev = sim.Evidence;
            var before = ev.StateBefore;
            if (Has(entry, "verified_path_to_root")) return result; // evidence not recorded in this version

            bool SoilMoistened(int cell) => ev.CumulativeAbsorbed[cell] > 0;
            bool SoilWasDry(int cell)
            {
                int cap = GardenMapData.SoilCapacity[cell];
                return cap > 0 && 1000 * before.SoilWater[cell] / cap < 100;
            }
            bool RainAt(int cell) => ev.WeatherRainCells.Contains(cell);
            bool GrowthAdvanced(PlantState p) =>
                ev.GrowthBeforeByPlant.TryGetValue(p.PlantId, out int bg) && p.Growth > bg;

            void AddGroupCells(string kind, Func<int, bool> cellOk)
            {
                foreach (var g in groups)
                {
                    if (g.Kind != kind) continue;
                    foreach (int cell in g.CellIds)
                        if (cellOk(cell)) result.Add(Tuple.Create(g, cell, (string)null));
                }
            }

            switch (entry.Id)
            {
                case "message.soil.absorbed":
                    AddGroupCells("Infiltrated", SoilMoistened); break;
                case "message.soil.dry_absorbed":
                    AddGroupCells("Infiltrated", c => SoilMoistened(c) && SoilWasDry(c)); break;
                case "message.roots.absorbed":
                    foreach (var p in before.Plants)
                    {
                        if (p.Growth < 250) continue; // already germinated before absorption
                        if (!SoilMoistened(p.CellId)) continue;
                        var g = GroupContaining(groups, "Infiltrated", p.CellId);
                        if (g != null) result.Add(Tuple.Create(g, p.CellId, p.PlantId));
                    }
                    break;
                case "message.water.puddle":
                    AddGroupCells("PuddleFormed", c => true); break;
                case "message.water.flow":
                    foreach (var g in groups)
                        if (g.Kind == "SurfaceFlowObserved")
                            result.Add(Tuple.Create(g, -1, (string)null));
                    break;
                case "message.wind.reached":
                    AddGroupCells("WindReceived", c => true); break;
                case "message.wind.sheltered":
                    AddGroupCells("WindSheltered", c => true); break;
                case "message.wind.moisture_recovered":
                    AddGroupCells("WindReceived", c => ev.MoistureReturnedCells.Contains(c)); break;
                case "message.light.reached":
                    AddGroupCells("LightReceived", c => true); break;
                case "message.light.sheltered":
                    AddGroupCells("LightReceived", c => ev.LightAttenuatedCells.Contains(c)); break;
                case "message.light.growth":
                    foreach (var p in sim.PostState.Plants)
                    {
                        if (!ev.LightReceivedCells.Contains(p.CellId) || !GrowthAdvanced(p)) continue;
                        var g = GroupContaining(groups, "LightReceived", p.CellId);
                        if (g != null) result.Add(Tuple.Create(g, p.CellId, p.PlantId));
                    }
                    break;
                case "message.seed.moist_soil":
                    foreach (var kv in ev.SeedSoilQAtPlacement)
                    {
                        if (kv.Value < 400 || kv.Value > 700) continue;
                        var g = GroupContaining(groups, "SeedPlaced", kv.Key);
                        if (g != null) result.Add(Tuple.Create(g, kv.Key, (string)null));
                    }
                    break;
                case "message.seed.pair":
                    {
                        var seedGroups = new List<ObservationGroup>();
                        int placed = 0;
                        foreach (var e in sim.Events) if (e.Kind == "SeedPlaced") placed++;
                        foreach (var g in groups) if (g.Kind == "SeedPlaced") seedGroups.Add(g);
                        if (placed == 2 && seedGroups.Count > 0)
                        {
                            seedGroups.Sort((a, b) => string.CompareOrdinal(a.GroupId, b.GroupId));
                            result.Add(Tuple.Create(seedGroups[0], seedGroups[0].CellIds[0], (string)null));
                        }
                        break;
                    }
                case "message.plant.germinated": AddStage(groups, sim, "Germinated", result); break;
                case "message.plant.leaf_opened": AddStage(groups, sim, "LeafOpened", result); break;
                case "message.plant.bud_formed": AddStage(groups, sim, "BudFormed", result); break;
                case "message.plant.bloomed": AddStage(groups, sim, "Bloomed", result); break;
                case "message.growth.water_and_light":
                    foreach (var p in sim.PostState.Plants)
                    {
                        if (!GrowthAdvanced(p) || !ev.GrowthSufficientPlants.Contains(p.PlantId)) continue;
                        var g = GroupContaining(groups, "GrowthObserved", p.CellId);
                        if (g != null) result.Add(Tuple.Create(g, p.CellId, p.PlantId));
                    }
                    break;
                case "message.plant.shadowed_bud":
                    foreach (var p in before.Plants)
                    {
                        if (p.Stage != PlantStage.Bud) continue;
                        if (GardenMapData.LightOccluderCellId[p.CellId] < 0) continue;
                        if (!ev.LightReceivedCells.Contains(p.CellId)) continue;
                        var after = sim.PostState.PlantAt(p.CellId);
                        if (after == null || after.PlantId != p.PlantId || after.Stage != PlantStage.Bud) continue;
                        var g = GroupContaining(groups, "LightReceived", p.CellId);
                        if (g != null) result.Add(Tuple.Create(g, p.CellId, p.PlantId));
                    }
                    break;
                case "message.weather.rain_light":
                    AddGroupCells("LightReceived", RainAt); break;
                case "message.weather.rain_wind":
                    AddGroupCells("WindReceived", RainAt); break;
                case "message.weather.rain_seed":
                    AddGroupCells("SeedPlaced", RainAt); break;
                case "message.weather.earlier_seed_germinated":
                    foreach (var e in sim.Events)
                    {
                        if (e.Kind != "Germinated" || !RainAt(e.CellId)) continue;
                        var beforePlant = before.PlantAt(e.CellId);
                        if (beforePlant == null || beforePlant.PlantId != e.PlantId) continue;
                        if (beforePlant.OriginKind != "seedInput") continue;
                        var g = GroupContaining(groups, "Germinated", e.CellId);
                        if (g != null) result.Add(Tuple.Create(g, e.CellId, e.PlantId));
                    }
                    break;
            }
            result.Sort((a, b) =>
            {
                int c = a.Item2.CompareTo(b.Item2);
                if (c != 0) return c;
                return string.CompareOrdinal(a.Item3 ?? "", b.Item3 ?? "");
            });
            return result;
        }

        private static void AddStage(List<ObservationGroup> groups, SimulationResult sim, string kind,
            List<Tuple<ObservationGroup, int, string>> result)
        {
            foreach (var e in sim.Events)
            {
                if (e.Kind != kind) continue;
                var g = GroupContaining(groups, kind, e.CellId);
                if (g != null) result.Add(Tuple.Create(g, e.CellId, e.PlantId));
            }
        }
    }
}
