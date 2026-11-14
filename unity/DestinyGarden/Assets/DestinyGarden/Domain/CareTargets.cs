using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DestinyGarden.Domain
{
    public static class CareTargets
    {
        private const string CellPrefix = "cell:";
        public const string CellMode = "cell";

        public static string CellSiteId(int cell) => CellPrefix + cell.ToString(CultureInfo.InvariantCulture);

        public static bool TryCell(string siteId, out int cell)
        {
            cell = -1;
            if (siteId == null || !siteId.StartsWith(CellPrefix, StringComparison.Ordinal) ||
                !int.TryParse(siteId.Substring(CellPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int id) ||
                id >= GardenMapData.CellCount || GardenMapData.Plantable[id] != 1 || siteId != CellSiteId(id)) return false;
            cell = id;
            return true;
        }

        public static int Anchor(PreparedFaces faces) => faces.AnchorCellId >= 0 ? faces.AnchorCellId : Sites.ById(faces.ResolvedSiteId).CenterCellId;
        public static string Name(string id)
        {
            if (id == null) return "";
            if (TryCell(id, out int cell)) return Position(cell);
            if (id.StartsWith("habitat:", StringComparison.Ordinal) && int.TryParse(id.Substring(8), out int n) && n >= 0 && n < HabitatRules.Labels.Length)
                return HabitatRules.Labels[n];
            foreach (var site in Sites.All) if (site.SiteId == id) return site.LabelJa;
            return id;
        }
        public static string Position(int cell) => $"区画{cell % GardenMapData.Width + 1}-{cell / GardenMapData.Width + 1}";
        public static void ResolveHabitat(WorldState s, string label, PreparedFaces faces)
        {
            int index = Array.IndexOf(HabitatRules.Labels, label);
            if (index < 0) throw new ArgumentException("Unknown habitat.", nameof(label));
            int chosen = -1, best = -1;
            for (int c = 0; c < GardenMapData.CellCount; c++)
            {
                if (GardenMapData.Plantable[c] != 1 || LivingEcosystem.Habitat(s, c) != label) continue;
                int need = 0; var p = s.PlantAt(c);
                if (p != null && s.Ecosystem.Cycles.TryGetValue(p.PlantId, out var cycle)) need += cycle.Stress * 4;
                foreach (var seed in s.Ecosystem.Succession.SeedBank) if (seed.CellId == c) need += 6;
                foreach (var family in s.Ecosystem.Succession.Community.Families)
                    if (family.CellId == c) need += 8 + family.AdultHunger * 3 + family.NurseryHunger * 3;
                if (s.Ecosystem.Litter[c] > 0 && s.Ecosystem.Nutrients[c] < 6) need += 8;
                int Rotation(int cell) => (cell + s.WorldRevision * 13) % GardenMapData.CellCount;
                if (need > best || (need == best && (chosen < 0 || Rotation(c) < Rotation(chosen)))) { chosen = c; best = need; }
            }
            if (chosen < 0)
            {
                faces.TargetNote = label + "はまだないため、今回は" + Name(faces.DrawnSiteId) + "へ届ける。";
                return;
            }
            faces.ResolvedSiteId = "habitat:" + index; faces.AnchorCellId = chosen;
        }
        public static string HabitatAdvice(string label)
        {
            switch (label)
            {
                case "水辺": return "水は湿りの持続を支える。光が届き養分があれば藻が育ち、幼生の食べものへ。強い風で乾かすと藻や水生の餌も減る。";
                case "岩陰": return "水で落ち葉がほどける湿りを保つ。光は岩で弱まり、日陰を好む草花には湿りと空き地も必要。風も岩の後ろでは弱くなる。";
                case "草木の陰": return "種から背の高い株が育つと、隣にも陰が生まれる。水は下草と分解者を支える。光だけで日陰向きの条件を置き換えることはできない。";
                case "乾いた草地": return "水は乾きすぎた根を助ける。風は届いた実りの綿毛を東へ運ぶが、土も乾く。強い光も乾きを進めるので、株の負担を観察する。";
                default: return "水と光で育った花が蜜と食草を支える。追加の光は届いた元気な花の養分を蜜へ使う。種や根から増えた株が、後の陰や住人の隠れ場所になる。";
            }
        }
        public static string Guidance(WorldState s, string habitat)
        {
            if (!CareRules.Enabled(s)) return "";
            var sb = new StringBuilder("\nダイスとのつながり\n");
            foreach (string label in HabitatRules.Labels)
            {
                if (habitat != null && habitat != label) continue;
                int cells = 0; for (int c = 0; c < 256; c++) if (GardenMapData.Plantable[c] == 1 && LivingEcosystem.Habitat(s, c) == label) cells++;
                sb.AppendLine(label + $"（{cells}区画） — " + HabitatAdvice(label));
            }
            sb.AppendLine("居場所を選んで『次の届け先』にできる。公開中の面と届け先はそのまま、次の準備から反映する。願いは出目の確率を変えない。");
            return sb.ToString();
        }
        public static string PreparedPlan(WorldState s, PreparedFaces faces, WeatherDefinition weather)
        {
            if (!CareRules.Enabled(s) || faces == null) return "";
            int anchor = Anchor(faces); var sb = new StringBuilder("今回の面と届け先\n");
            if (!TryCell(faces.ResolvedSiteId, out _)) sb.Append(Name(faces.ResolvedSiteId)).Append(" / ");
            sb.AppendLine(Position(anchor) + "（庭の輪郭が今回の中心）");
            if (faces.TargetNote != null) sb.AppendLine(faces.TargetNote);
            var seen = new HashSet<string>();
            for (int face = 0; face < faces.Faces.Length; face++)
            {
                string id = faces.Faces[face]; bool lucky = face == 6;
                if (!seen.Add(id + (lucky ? ":seven" : ""))) continue;
                var dice = lucky ? LuckySeven.ResolveAmplified(id, anchor, s) : RecipeResolver.Resolve(id, anchor, s);
                var direct = new HashSet<int>();
                if (dice.Element == ElementId.Water)
                    foreach (var tick in dice.WaterSchedule.Values) foreach (var kv in tick) if (GardenMapData.Plantable[kv.Key] == 1) direct.Add(kv.Key);
                if (dice.Element == ElementId.Wind || dice.Element == ElementId.Light)
                    foreach (var kv in dice.FieldCells) if (GardenMapData.Plantable[kv.Key] == 1 && CareRules.ExtraField(dice, weather, kv.Key, dice.Element) > 0) direct.Add(kv.Key);
                foreach (int cell in dice.SeedCells) direct.Add(cell);
                var places = new List<string>();
                foreach (string label in HabitatRules.Labels)
                {
                    int count = 0; foreach (int cell in direct) if (LivingEcosystem.Habitat(s, cell) == label) count++;
                    if (count > 0) places.Add(label + count);
                }
                string action = dice.Element == ElementId.Water ? "局所へ水。流れた先の土にも届く。過湿にも注意。"
                    : dice.Element == ElementId.Wind ? "届いた実りの綿毛を東へ。岩の後ろで弱まり、土も乾く。"
                    : dice.Element == ElementId.Light ? "届いた株の生長、花蜜、水辺の藻へ。乾いた土では負担も見る。"
                    : $"空いている{dice.SeedCells.Count}区画へ種。種類はその土の条件に合わせて決まる。";
                sb.AppendLine((lucky ? "7の目 " : "") + RecipeCatalog.LabelJa(id) + $"（直接届く土 {direct.Count}区画：" + string.Join("・", places) + "） → " + action);
            }
            if (faces.Faces.Length == 7) sb.AppendLine("七の目は同じ中心から入力の強さが2倍。光と風の上限、地形による弱まりは残り、働きは土や生き物の条件で変わる。");
            return sb.ToString();
        }
    }
}
