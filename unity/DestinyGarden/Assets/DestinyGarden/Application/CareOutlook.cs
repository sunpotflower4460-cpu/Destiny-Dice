using System;
using System.Collections.Generic;
using System.Text;
using DestinyGarden.Domain;

namespace DestinyGarden.Application
{
    public sealed class CareConcern
    {
        public string SubjectId, Kind, Label, Habitat, Advice;
        public int CellId, Priority, MoistMin, MoistMax;
        public bool MoistureGoal;
        public ElementId? Reaches;
    }
    public sealed class CareFaceOutlook
    {
        public int FaceId, Moisture;
        public string RecipeId;
        public bool Improves, Worsens, Reaches;
    }
    public sealed class CareOutlookRow
    {
        public CareConcern Concern;
        public int CurrentMoisture, WeatherMoisture;
        public List<CareFaceOutlook> Faces = new List<CareFaceOutlook>();
    }
    /// <summary>Read-only view of published choices. Uses the real natural simulation, never ecology chance or IDs.</summary>
    public sealed class CareOutlook
    {
        public int FaceCount;
        public List<CareOutlookRow> Rows = new List<CareOutlookRow>();
        private static string Percent(int value) => $"{value / 10}.{value % 10}%";
        public static List<CareConcern> ReadConcerns(WorldState s)
        {
            var all = new List<CareConcern>(); if (!CareRules.Enabled(s)) return all;
            void Add(string id, string kind, string label, int c, int priority, string advice, int min = -1, int max = -1, ElementId? reaches = null) =>
                all.Add(new CareConcern { SubjectId = id, Kind = kind, Label = label, CellId = c, Priority = priority,
                    Habitat = LivingEcosystem.Habitat(s, c), Advice = advice, MoistureGoal = min >= 0, MoistMin = min, MoistMax = max, Reaches = reaches });
            foreach (var p in s.Plants)
            {
                var t = Flora.Get(p.Species); if (t == null) continue;
                int m = Flora.Moisture(s, p.CellId), min = p.Stage == PlantStage.Flower ? t.MoistMin : Math.Max(101, t.MoistMin),
                    max = p.Stage == PlantStage.Flower ? t.MoistMax : Math.Min(949, t.MoistMax);
                s.Ecosystem.Cycles.TryGetValue(p.PlantId, out var life);
                if (m < min || m > max)
                    Add(p.PlantId, "plant-moisture", t.LabelJa + (m < min ? "の乾いた根" : "の水が多い根"), p.CellId, 90 + (life?.Stress ?? 0),
                        "湿りが戻っても、養分・土・光・回復の時間も必要。", min, max);
                else if (life != null && life.Stress >= 12)
                    Add(p.PlantId, "recovery-wait", t.LabelJa + "の回復", p.CellId, 45, "湿りは範囲内。土・光・小虫・混み合いを見て、負担が抜けるのを待つ。", min, max);
                else if (s.Ecosystem.Nutrients[p.CellId] == 0)
                    Add(p.PlantId, "nutrient-wait", t.LabelJa + "の養分", p.CellId, 40, "水や光だけで養分は増えない。落ち葉の分解、腐植、近くの根の働きを待つ。", min, max);
                else if (p.Stage == PlantStage.Flower && Flora.Fits(t, s, p.CellId) && s.Ecosystem.Succession.Flowers.TryGetValue(p.PlantId, out var f) && f.Nectar < 2)
                    Add(p.PlantId, "nectar", t.LabelJa + "の蜜源", p.CellId, 55, "追加の光が届けば補充の機会。開花・元気・養分が必要で、蜜は訪花で使われる。", min, max, ElementId.Light);
                else if (t.WindSeed && life != null && life.Seeds > 0)
                    Add(p.PlantId, "ripe-seeds", t.LabelJa + "の実った種", p.CellId, 50, "十分な風が届けば東へ旅する機会。実り・空き・散布の偶然が必要。風は土も乾かす。", min, max, ElementId.Wind);
                else
                    Add(p.PlantId, "moisture-watch", t.LabelJa + "の保ちたい湿り", p.CellId, 10, "今は湿りの範囲内。補い過ぎや乾かし過ぎにならないかを見る。", min, max);
            }
            foreach (var seed in s.Ecosystem.Succession.SeedBank)
            {
                var t = Flora.Get(seed.Species); if (t == null) continue;
                int m = Flora.Moisture(s, seed.CellId); string wait = SuccessionRules.WaitingFor(s, seed);
                bool empty = s.PlantAt(seed.CellId) == null;
                if (empty && (m < t.MoistMin || m > t.MoistMax))
                    Add(seed.SeedId, "seed-moisture", t.LabelJa + "の眠る種", seed.CellId, 80,
                        (wait ?? "芽吹く条件が整っている") + "。湿り以外の条件と、1投の芽吹き上限もある。", t.MoistMin, t.MoistMax);
                else if (wait != null)
                    Add(seed.SeedId, "seed-wait", t.LabelJa + "の眠る種", seed.CellId, 30, wait + "。追加の種で空き地を埋めず、今の種を見守る方法もある。");
            }
            foreach (var family in s.Ecosystem.Succession.Community.Families)
            {
                if (family.Kind != LivingEcosystem.Frog && family.Kind != Ecology.Dragonfly) continue;
                int c = family.CellId; string label = EcologyObservations.ResidentLabel(family.Kind);
                if (!CommunityRules.Pond(s, c) && Flora.Moisture(s, c) < 750)
                    Add(family.FamilyId, "pond-moisture", label + (family.Brood > 0 ? "の幼い命の水辺" : "の水辺"), c, family.Brood > 0 ? 130 : 100,
                        "水辺の土の湿りを見る。続いてきた湿り・覆い・餌も必要で、別の池へ幼生は移せない。", 750, 1000);
                else if (family.Kind == LivingEcosystem.Frog && family.Brood > 0 && s.Ecosystem.Succession.Community.Algae[c] < family.Brood)
                    Add(family.FamilyId, "pond-food", "オタマジャクシの食べもの", c, 110,
                        "追加の光が届けば藻を支える機会。水辺の持続と養分が必要。光だけで育つとは限らない。", reaches: ElementId.Light);
            }
            all.Sort((a, b) => a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : a.CellId != b.CellId ? a.CellId.CompareTo(b.CellId)
                : string.CompareOrdinal(a.SubjectId, b.SubjectId));
            var result = new List<CareConcern>(); var counts = new Dictionary<string, int>(); var seen = new HashSet<string>();
            foreach (var concern in all)
            {
                string key = concern.Kind + ":" + concern.CellId + ":" + concern.Label;
                if (!seen.Add(key)) continue;
                counts.TryGetValue(concern.Habitat, out int n); if (n == 3) continue;
                counts[concern.Habitat] = n + 1; result.Add(concern);
            }
            return result;
        }
        public static CareOutlook Build(WorldState s, PreparedFaces faces, WeatherDefinition weather)
        {
            var plan = new CareOutlook(); if (!CareRules.Enabled(s) || faces == null || weather == null) return plan;
            plan.FaceCount = faces.Faces.Length;
            var concerns = ReadConcerns(s); if (concerns.Count == 0) return plan;
            var baseline = Simulation.Resolve(s, new ResolvedRecipe { Element = ElementId.Water }, weather, null).PostState;
            foreach (var concern in concerns) plan.Rows.Add(new CareOutlookRow { Concern = concern, CurrentMoisture = Flora.Moisture(s, concern.CellId), WeatherMoisture = Flora.Moisture(baseline, concern.CellId) });
            var outcomes = new Dictionary<string, WorldState>(); int anchor = CareTargets.Anchor(faces);
            for (int face = 0; face < plan.FaceCount; face++)
            {
                bool lucky = face == 6; string recipe = faces.Faces[face], key = recipe + (lucky ? ":seven" : "");
                var dice = lucky ? LuckySeven.ResolveAmplified(recipe, anchor, s) : RecipeResolver.Resolve(recipe, anchor, s);
                if (!outcomes.TryGetValue(key, out var after))
                {
                    after = Simulation.Resolve(s, dice, weather, n => "outlook-" + n).PostState;
                    outcomes.Add(key, after);
                }
                foreach (var row in plan.Rows)
                {
                    var need = row.Concern; int c = need.CellId, m = Flora.Moisture(after, c), b = row.WeatherMoisture;
                    var option = new CareFaceOutlook { FaceId = face + 1, RecipeId = recipe, Moisture = m };
                    if (need.MoistureGoal)
                    {
                        bool beforeGood = b >= need.MoistMin && b <= need.MoistMax, afterGood = m >= need.MoistMin && m <= need.MoistMax;
                        option.Improves = !beforeGood && (afterGood || (b < need.MoistMin && m > b && m < need.MoistMin) || (b > need.MoistMax && m < b && m > need.MoistMax));
                        option.Worsens = (beforeGood && !afterGood) || (b < need.MoistMin && (m < b || m > need.MoistMax)) || (b > need.MoistMax && (m > b || m < need.MoistMin));
                    }
                    if (need.Reaches.HasValue) option.Reaches = CareRules.ExtraField(dice, weather, c, need.Reaches.Value) >= 100;
                    row.Faces.Add(option);
                }
            }
            return plan;
        }
        public string Describe(string habitat = null)
        {
            if (FaceCount == 0) return "";
            var sb = new StringBuilder("\nお世話の見通し\n"); int shown = 0;
            foreach (var row in Rows)
            {
                var need = row.Concern; if (habitat != null && need.Habitat != habitat) continue;
                if (shown++ == 3) break;
                sb.AppendLine(need.Label + "（" + need.Habitat + "・" + CareTargets.Position(need.CellId) + "）");
                sb.AppendLine("　" + need.Advice);
                if (need.MoistureGoal)
                {
                    int good = 0, bad = 0; var improve = new List<string>();
                    foreach (var face in row.Faces)
                    { if (face.Improves) { good++; improve.Add(face.FaceId + "の目"); } if (face.Worsens) bad++; }
                    sb.AppendLine($"　湿り：今{Percent(row.CurrentMoisture)} / 天候だけなら{Percent(row.WeatherMoisture)}（この条件の範囲 {Percent(need.MoistMin)}〜{Percent(need.MoistMax)}）");
                    sb.AppendLine($"　湿りの範囲へ近づく面 {good}/{FaceCount}・外れを生む/広げる面 {bad}/{FaceCount}" + (good > 0 ? "：" + string.Join("・", improve) : ""));
                    if (row.WeatherMoisture >= need.MoistMin && row.WeatherMoisture <= need.MoistMax) sb.AppendLine("　天候だけでも湿りの範囲へ戻る見込み。");
                }
                if (need.Reaches.HasValue)
                {
                    int reached = 0; foreach (var face in row.Faces) if (face.Reaches) reached++;
                    sb.AppendLine($"　必要な追加の{(need.Reaches == ElementId.Light ? "光" : "風")}が届く面 {reached}/{FaceCount}。反応や成長の保証ではない。");
                }
            }
            if (shown == 0) sb.AppendLine("今、この居場所に急いで補いたい条件は見つかっていない。");
            sb.AppendLine("公開された面が出た場合の湿りと到達の見通し。芽吹き・回復・繁殖には、ほかの条件も必要。願いで面の確率は変わらない。");
            return sb.ToString();
        }
    }
}
