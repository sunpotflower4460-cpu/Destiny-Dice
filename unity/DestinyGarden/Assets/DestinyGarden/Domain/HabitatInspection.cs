using System;
using System.Collections.Generic;
using System.Text;

namespace DestinyGarden.Domain
{
    public sealed class HabitatEdge
    {
        public string From, To;
        public int Contacts;
    }
    public static class HabitatInspection
    {
        public static List<HabitatEdge> Edges(WorldState s, string habitat = null)
        {
            var map = new Dictionary<string, HabitatEdge>(StringComparer.Ordinal);
            for (int c = 0; c < GardenMapData.CellCount; c++)
            {
                if (GardenMapData.Plantable[c] != 1) continue;
                void Add(int n)
                {
                    if (GardenMapData.Plantable[n] != 1) return;
                    string a = LivingEcosystem.Habitat(s, c), b = LivingEcosystem.Habitat(s, n);
                    if (a == b || (habitat != null && habitat != a && habitat != b)) return;
                    if (Array.IndexOf(HabitatRules.Labels, a) > Array.IndexOf(HabitatRules.Labels, b)) { string swap = a; a = b; b = swap; }
                    string key = a + ":" + b;
                    if (!map.TryGetValue(key, out var edge)) { edge = new HabitatEdge { From = a, To = b }; map.Add(key, edge); }
                    edge.Contacts++;
                }
                if (c % 16 < 15) Add(c + 1);
                if (c / 16 < 15) Add(c + 16);
            }
            var result = new List<HabitatEdge>(map.Values);
            result.Sort((a, b) => string.CompareOrdinal(a.From, b.From) != 0 ? string.CompareOrdinal(a.From, b.From) : string.CompareOrdinal(a.To, b.To));
            return result;
        }
        public static int Continuity(WorldState s, int cell)
        {
            var h = s.Ecosystem?.Habitat;
            if (h == null || h.LastHabitat[cell] != Array.IndexOf(HabitatRules.Labels, LivingEcosystem.Habitat(s, cell))) return 0;
            return h.StableAge[cell];
        }
        public static string RootStatus(WorldState s, PlantState p)
        {
            var h = s.Ecosystem?.Habitat; if (h == null || HabitatRules.RootRoute(p.Species) == null) return null;
            if (s.Ecosystem.Cycles.TryGetValue(p.PlantId, out var cycle) && cycle.Stress >= 12) return "株の回復を待つ";
            if (p.Stage < PlantStage.Leaf || p.Growth < 1000) return "葉と根が育つのを待つ";
            if (p.Stage == PlantStage.Flower) return "いまは花を育てている";
            if (HabitatRules.Suitability(Flora.Get(p.Species), s, p.CellId) < 700) return "土と光がよく合うのを待つ";
            if (!h.RootReserves.TryGetValue(p.PlantId, out int reserve) || reserve < HabitatRules.RootCost) return "根に力を蓄えている";
            if (s.Ecosystem.Nutrients[p.CellId] < HabitatRules.RootNutrients) return "養分が戻るのを待つ";
            for (int c = 0; c < GardenMapData.CellCount; c++)
                if (GardenMapData.Plantable[c] == 1 && s.PlantAt(c) == null && LivingEcosystem.Distance(c, p.CellId) == 1 &&
                    HabitatRules.Suitability(Flora.Get(p.Species), s, c) >= 700) return "根を広げる条件が整っている";
            return "隣の合う空き地を待つ";
        }
        public static string Describe(WorldState s, string habitat = null)
        {
            var h = s.Ecosystem?.Habitat; if (h == null) return "";
            int humus = 0, settled = 0;
            for (int c = 0; c < GardenMapData.CellCount; c++)
            {
                if (GardenMapData.Plantable[c] != 1 || (habitat != null && LivingEcosystem.Habitat(s, c) != habitat)) continue;
                humus += h.Humus[c]; if (Continuity(s, c) >= 6) settled++;
            }
            var sb = new StringBuilder(); sb.AppendLine($"\n土に残る時間　腐植 {humus} / 居場所が落ち着いた土 {settled}区画");
            sb.AppendLine("落ち葉の一部は腐植となって土をやわらげ、ゆっくり養分へ戻る。先に生きた株の働きが、次の根を支える。");
            var edges = Edges(s, habitat); sb.AppendLine("\n生態系の境目");
            if (edges.Count == 0) sb.AppendLine("異なる居場所が接する場所は、まだない。");
            foreach (var edge in edges) sb.AppendLine($"{edge.From} ↔ {edge.To}　{edge.Contacts}接点");
            var plants = new List<PlantState>(s.Plants); plants.Sort((a, b) => a.CellId.CompareTo(b.CellId));
            bool header = false;
            foreach (var p in plants)
            {
                if (habitat != null && LivingEcosystem.Habitat(s, p.CellId) != habitat) continue;
                string status = RootStatus(s, p); if (status == null) continue;
                if (!header) { sb.AppendLine("\n根から広がる植物"); header = true; }
                sb.AppendLine((Flora.Get(p.Species)?.LabelJa ?? "植物") + " — " + status);
            }
            return sb.ToString();
        }
    }
}
