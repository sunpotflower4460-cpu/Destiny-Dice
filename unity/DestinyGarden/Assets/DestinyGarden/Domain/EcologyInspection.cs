using System;
using System.Collections.Generic;
using System.Text;

namespace DestinyGarden.Domain
{
    public sealed class PlantReading
    {
        public string PlantId, Label, Habitat, Origin;
        public int CellId, Stress, Nectar;
        public List<string> Needs = new List<string>();
    }

    /// <summary>Read-only observations of current conditions, not inferred events or forecasts.</summary>
    public static class EcologyInspection
    {
        public static readonly string[] Habitats = { "水辺", "岩陰", "草木の陰", "乾いた草地", "花の草地" };
        public static List<PlantReading> ReadPlants(WorldState s, string habitat = null)
        {
            var list = new List<PlantReading>();
            foreach (var p in s.Plants)
            {
                string place = LivingEcosystem.Habitat(s, p.CellId);
                if (habitat != null && place != habitat) continue;
                var t = Flora.Get(p.Species);
                var reading = new PlantReading { PlantId = p.PlantId, Label = t?.LabelJa ?? "まだ名のない株",
                    Habitat = place, CellId = p.CellId, Nectar = -1,
                    Origin = p.OriginKind == "seedInput" ? "サイコロが届けた種から" : "はじめからこの庭にいた株" };
                if (s.Ecosystem != null)
                {
                    if (s.Ecosystem.Cycles.TryGetValue(p.PlantId, out var life)) reading.Stress = life.Stress;
                    if (s.Ecosystem.Nutrients[p.CellId] == 0) reading.Needs.Add("養分が乏しい");
                    if (s.Ecosystem.Aphids[p.CellId] >= 6) reading.Needs.Add("アブラムシが多い");
                    var m = s.Ecosystem.Succession;
                    if (m != null)
                    {
                        if (m.Flowers.TryGetValue(p.PlantId, out var flower) && p.Stage == PlantStage.Flower) reading.Nectar = flower.Nectar;
                        if (m.Ancestry.TryGetValue(p.PlantId, out var ancestry))
                        {
                            bool parentAlive = s.Plants.Exists(other => other.PlantId == ancestry.ParentPlantId);
                            reading.Origin = parentAlive ? "この庭の親株が残した種から" : "今はない親株が土に残した種から";
                        }
                    }
                }
                if (p.OriginKind == "ecology" && (s.Ecosystem?.Succession == null || !s.Ecosystem.Succession.Ancestry.ContainsKey(p.PlantId)))
                    reading.Origin = "この庭の生き物が運んだ種から";
                if (t == null) reading.Needs.Add("まだ花の種類を見分けられない");
                if (t != null)
                {
                    int moisture = Flora.Moisture(s, p.CellId), drainage = Flora.Drainage(s, p.CellId);
                    if (moisture < t.MoistMin) reading.Needs.Add("土が乾きすぎている");
                    if (moisture > t.MoistMax) reading.Needs.Add("土の水が多すぎる");
                    if (Flora.Hardness(s, p.CellId) > t.MaxHardness) reading.Needs.Add("土がまだ硬い");
                    if (drainage < t.DrainMin) reading.Needs.Add("水が抜けにくい");
                    if (drainage > t.DrainMax) reading.Needs.Add("水が抜けやすい");
                    bool shade = Flora.IsShade(s, p.CellId);
                    if (t.Light == 0 && shade) reading.Needs.Add("日向を好む株が陰になっている");
                    if (t.Light == 2 && !shade) reading.Needs.Add("日陰を好む株に光が強い");
                    int crowd = 0;
                    foreach (var other in s.Plants)
                        if (other != p && other.Stage >= PlantStage.Leaf && LivingEcosystem.Distance(other.CellId, p.CellId) <= 1 &&
                            PlantStrategies.Get(other.Species).Height >= PlantStrategies.Get(p.Species).Height) crowd++;
                    if (crowd >= 3) reading.Needs.Add("隣の株と混み合っている");
                }
                list.Add(reading);
            }
            list.Sort((a, b) => a.Stress != b.Stress ? b.Stress.CompareTo(a.Stress) : a.CellId.CompareTo(b.CellId));
            return list;
        }
        public static string Describe(WorldState s, string habitat = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("庭を観察　" + (habitat ?? "全体") + "　（上下にスクロール）");
            if (s.Ecosystem == null) return sb.AppendLine("この庭の住み場所と花の条件は、図鑑から眺められます。").ToString();
            foreach (string place in Habitats)
            {
                if (habitat != null && place != habitat) continue;
                int cells = 0, plants = 0, blooms = 0, litter = 0, nutrients = 0;
                var species = new SortedSet<string>(StringComparer.Ordinal);
                for (int c = 0; c < GardenMapData.CellCount; c++)
                {
                    if (GardenMapData.Plantable[c] != 1 || LivingEcosystem.Habitat(s, c) != place) continue;
                    cells++; litter += s.Ecosystem.Litter[c]; nutrients += s.Ecosystem.Nutrients[c];
                    var p = s.PlantAt(c); if (p == null) continue;
                    plants++; if (p.Stage == PlantStage.Flower) blooms++;
                    species.Add(Flora.Get(p.Species)?.LabelJa ?? "まだ名のない株");
                }
                sb.AppendLine($"\n{place}　{cells}区画 / {plants}株 / 開花{blooms}株");
                sb.AppendLine(cells == 0 ? "この居場所は、まだ庭に生まれていない。" :
                    $"落ち葉 {litter}・養分 {nutrients}　" + (species.Count == 0 ? "まだ根づいた株はない。" : string.Join("・", species)));
            }
            var memory = s.Ecosystem.Succession;
            if (memory != null)
            {
                sb.AppendLine("\n土の中で待つ命");
                var groups = new SortedDictionary<string, int>(StringComparer.Ordinal);
                int total = 0;
                foreach (var seed in memory.SeedBank)
                {
                    if (habitat != null && LivingEcosystem.Habitat(s, seed.CellId) != habitat) continue;
                    total++;
                    string label = (Flora.Get(seed.Species)?.LabelJa ?? "種") + " — " +
                        (SuccessionRules.WaitingFor(s, seed) ?? "芽吹く条件が整っている");
                    groups[label] = groups.TryGetValue(label, out int n) ? n + 1 : 1;
                }
                if (total == 0) sb.AppendLine("眠っている種は、まだない。");
                else foreach (var pair in groups) sb.AppendLine(pair.Key + $"（{pair.Value}粒）");
            }
            sb.Append(CommunityInspection.Describe(s, habitat));
            sb.AppendLine("\n株が教えてくれること");
            var readings = ReadPlants(s, habitat);
            if (readings.Count == 0) sb.AppendLine("この居場所に、まだ株はない。");
            foreach (var plant in readings)
            {
                sb.AppendLine($"{plant.Label}（{plant.Habitat}）　{plant.Origin}");
                sb.AppendLine("　" + (plant.Needs.Count > 0 ? string.Join("・", plant.Needs) :
                    plant.Stress >= 12 ? "条件は戻った。負担が抜けるまで、もう少し時間が必要。" : "今の居場所の条件が合っている。") +
                    (plant.Nectar >= 0 ? (plant.Nectar > 0 ? $"　花蜜 {plant.Nectar}：訪れる命の食べもの。" : "　花蜜は補われるのを待っている。") : ""));
            }
            return sb.ToString();
        }
    }
}
