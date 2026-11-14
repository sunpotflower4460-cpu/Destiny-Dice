using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace DestinyGarden.Domain
{
    public sealed class CareMeasure
    {
        public bool[] Touched = new bool[GardenMapData.CellCount];
        public long[] WaterDelta = new long[GardenMapData.CellCount];
        public int[] GrowthDelta = new int[GardenMapData.CellCount];
        public int[] Planted = new int[GardenMapData.CellCount];
        public static CareMeasure Measure(EcologyContext ctx)
        {
            var before = ctx.Sim?.Evidence.StateBefore;
            if (before == null || ctx.Dice == null || ctx.Weather == null) return null;
            // Same saved start and weather, with no dice contribution. Never changes the real state or uses entropy/IDs.
            var baseline = Simulation.Resolve(before, new ResolvedRecipe { RecipeId = "care-baseline", Element = ElementId.Water }, ctx.Weather, null);
            var m = new CareMeasure(); var actual = ctx.Sim.PostState;
            for (int c = 0; c < GardenMapData.CellCount; c++)
            {
                m.WaterDelta[c] = (long)actual.SoilWater[c] + actual.SurfaceWater[c] - baseline.PostState.SoilWater[c] - baseline.PostState.SurfaceWater[c];
                var p = before.PlantAt(c);
                if (p != null) m.GrowthDelta[c] = actual.PlantAt(c).Growth - baseline.PostState.PlantAt(c).Growth;
                m.Touched[c] = m.WaterDelta[c] != 0 || ctx.Sim.Evidence.DiceWaterCells.Contains(c) ||
                    CareRules.ExtraField(ctx.Dice, ctx.Weather, c, ElementId.Light) > 0 || CareRules.ExtraField(ctx.Dice, ctx.Weather, c, ElementId.Wind) > 0;
            }
            foreach (var ev in ctx.Sim.Events) if (ev.Kind == "SeedPlaced") { m.Planted[ev.CellId]++; m.Touched[ev.CellId] = true; }
            return m;
        }
    }
    public sealed class CareImpact
    {
        public string Habitat;
        public List<int> Cells = new List<int>();
        public long WaterDelta;
        public int GrowthDelta, Planted, Nectar, Algae, WindSeeds;
    }
    public sealed class CareReport
    {
        public string RecipeId;
        public int AnchorCellId;
        public long RetainedWaterDelta;
        public int ExistingGrowthDelta;
        public List<CareImpact> Impacts = new List<CareImpact>();

        public static CareReport Build(EcologyContext ctx, EcologyResult result)
        {
            var m = ctx.Care; if (m == null) return null;
            var report = new CareReport { RecipeId = ctx.Dice.RecipeId, AnchorCellId = ctx.Dice.AnchorCellId };
            var before = ctx.Sim.Evidence.StateBefore;
            for (int c = 0; c < GardenMapData.CellCount; c++)
            { report.RetainedWaterDelta += m.WaterDelta[c]; report.ExistingGrowthDelta += m.GrowthDelta[c]; }
            foreach (string label in HabitatRules.Labels)
            {
                var impact = new CareImpact { Habitat = label };
                for (int c = 0; c < GardenMapData.CellCount; c++)
                {
                    if (!m.Touched[c] || GardenMapData.Plantable[c] != 1 || LivingEcosystem.Habitat(before, c) != label) continue;
                    impact.Cells.Add(c); impact.WaterDelta += m.WaterDelta[c]; impact.GrowthDelta += m.GrowthDelta[c]; impact.Planted += m.Planted[c];
                    foreach (var link in result.Relations)
                    {
                        if (link.ToCell == c && link.Kind == "care-nectar") impact.Nectar += link.Amount;
                        if (link.ToCell == c && link.Kind == "care-algae") impact.Algae += link.Amount;
                    }
                }
                foreach (var link in result.Relations)
                    if (link.Kind == "seed-buried" && link.Actor == Ecology.Fluff && impact.Cells.Contains(link.FromCell) &&
                        CareRules.ExtraField(ctx.Dice, ctx.Weather, link.FromCell, ElementId.Wind) >= 100) impact.WindSeeds += link.Amount;
                if (impact.Cells.Count > 0) report.Impacts.Add(impact);
            }
            return report;
        }
        private static string Signed(long value) => value > 0 ? "+" + value : value.ToString();
        public string Summary()
        {
            if (Impacts.Count == 0) return "土の区画に届いた変化はなかった。地形と空き地を観察できる。";
            int nectar = 0, algae = 0, seeds = 0, planted = 0;
            foreach (var i in Impacts) { nectar += i.Nectar; algae += i.Algae; seeds += i.WindSeeds; planted += i.Planted; }
            if (nectar + algae > 0) return $"届いた光から、花蜜 {nectar}・水辺の藻 {algae}が育った。";
            if (seeds > 0) return $"届いた風が、実った綿毛の種を{seeds}粒運んだ。";
            if (planted > 0) return $"選んだ届け先のまわりに、{planted}粒の種が入った。";
            if (RecipeCatalog.ElementOf(RecipeId) == ElementId.Water) return RetainedWaterDelta > 0 ? "届いた水が土やくぼみに残った。居場所の湿りを観察できる。" : "届いた水は流れや蒸発へ。庭に残る水の差はなかった。";
            return ExistingGrowthDelta < 0 ? "恵みは届いた。乾きも進み、株の生長は天候だけの場合より少なかった。" : "恵みが届いた場所を、記録からたどれる。";
        }
        public string Describe()
        {
            var sb = new StringBuilder(); sb.AppendLine(RecipeCatalog.LabelJa(RecipeId) + " → " + CareTargets.Position(AnchorCellId));
            foreach (var i in Impacts)
                sb.AppendLine($"　{i.Habitat} {i.Cells.Count}区画：水差 {Signed(i.WaterDelta)} / 既存株の生長差 {Signed(i.GrowthDelta)}" +
                    (i.Planted > 0 ? $" / 播種 {i.Planted}" : "") + (i.Nectar > 0 ? $" / 花蜜 {i.Nectar}" : "") +
                    (i.Algae > 0 ? $" / 藻 {i.Algae}" : "") + (i.WindSeeds > 0 ? $" / 綿毛の種 {i.WindSeeds}" : ""));
            sb.AppendLine($"庭全体の水差 {Signed(RetainedWaterDelta)} / 既存株の生長差 {Signed(ExistingGrowthDelta)}（同じ開始状態・天候で、ダイスの恵みがなかった場合との差。自然計算分のみ）");
            return sb.ToString();
        }
        public Dictionary<string, object> ToPayload()
        {
            var impacts = new List<object>();
            foreach (var i in Impacts) impacts.Add(new Dictionary<string, object> { ["habitat"] = i.Habitat, ["cells"] = i.Cells.ToArray(),
                ["waterDelta"] = i.WaterDelta, ["growthDelta"] = i.GrowthDelta, ["planted"] = i.Planted, ["nectar"] = i.Nectar, ["algae"] = i.Algae, ["windSeeds"] = i.WindSeeds });
            return new Dictionary<string, object> { ["recipeId"] = RecipeId, ["anchorCellId"] = AnchorCellId,
                ["retainedWaterDelta"] = RetainedWaterDelta, ["existingGrowthDelta"] = ExistingGrowthDelta, ["impacts"] = impacts };
        }
        public static CareReport FromPayload(IDictionary<string, object> p)
        {
            var r = new CareReport { RecipeId = (string)p["recipeId"], AnchorCellId = WorldStateSerializer.ToInt(p["anchorCellId"]),
                RetainedWaterDelta = Convert.ToInt64(p["retainedWaterDelta"]), ExistingGrowthDelta = WorldStateSerializer.ToInt(p["existingGrowthDelta"]) };
            foreach (IDictionary<string, object> item in (IEnumerable)p["impacts"])
            {
                var i = new CareImpact { Habitat = (string)item["habitat"], WaterDelta = Convert.ToInt64(item["waterDelta"]), GrowthDelta = WorldStateSerializer.ToInt(item["growthDelta"]),
                    Planted = WorldStateSerializer.ToInt(item["planted"]), Nectar = WorldStateSerializer.ToInt(item["nectar"]), Algae = WorldStateSerializer.ToInt(item["algae"]), WindSeeds = WorldStateSerializer.ToInt(item["windSeeds"]) };
                foreach (object c in (IEnumerable)item["cells"]) i.Cells.Add(WorldStateSerializer.ToInt(c));
                r.Impacts.Add(i);
            }
            return r;
        }
    }
}
