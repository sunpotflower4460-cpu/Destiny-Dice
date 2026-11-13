using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>v8 ties ecological responses to the resolved, effective dice footprint.</summary>
    public static class CareRules
    {
        public const string RulesVersion = "ecology.v8", TargetVersion = "habitat-focus.v8", MappingVersion = "ecological-faces.v8";
        public static bool Enabled(WorldState s) => s?.Ecosystem?.Habitat?.LinkedCare == true;
        public static int ExtraField(ResolvedRecipe dice, WeatherDefinition weather, int cell, ElementId element)
        {
            if (dice == null || dice.Element != element || RecipeResolver.IsSolid(cell)) return 0;
            int baseQ = element == ElementId.Light ? weather?.BaseLightQ ?? 0 : weather?.BaseWindQ ?? 0;
            int terrain = element == ElementId.Light ? GardenMapData.LightExposureQ[cell] : RecipeResolver.ShelterQEast(cell);
            foreach (var kv in dice.FieldCells)
                if (kv.Key == cell)
                    return Math.Min(1000, baseQ + dice.StrengthQ * kv.Value / 1000) * terrain / 1000 - baseQ * terrain / 1000;
            return 0;
        }
        public static int WindPower(ResolvedRecipe dice, WeatherDefinition weather, int cell) =>
            ExtraField(dice, weather, cell, ElementId.Wind) +
            (weather != null && weather.BaseWindQ >= 300 ? weather.BaseWindQ * RecipeResolver.ShelterQEast(cell) / 1000 : 0);
        public static int WindDistance(ResolvedRecipe dice, WeatherDefinition weather, int cell) =>
            Math.Min(5, 2 + WindPower(dice, weather, cell) / 250);
        public static int LocalNeed(ResolvedRecipe dice, WorldState s, WeatherDefinition weather, int cell, int physicalNeed)
        {
            if (!Enabled(s)) return physicalNeed;
            int moisture = Flora.Moisture(s, cell); var p = s.PlantAt(cell); var t = Flora.Get(p?.Species);
            var e = s.Ecosystem;
            if (dice.Element == ElementId.Water)
            {
                if (moisture >= 850) return 0;
                int target = t != null ? (t.MoistMin + t.MoistMax) / 2 : 400;
                var pond = e.Succession.Community;
                if (e.WetAge[cell] >= 6 && (pond.Algae[cell] > 0 || pond.AquaticPrey[cell] > 0)) target = Math.Max(target, 800);
                foreach (var family in pond.Families)
                    if (family.CellId == cell && (family.Kind == LivingEcosystem.Frog || family.Kind == Ecology.Dragonfly)) target = Math.Max(target, 800);
                if (e.Litter[cell] > 0 && moisture < 300) target = Math.Max(target, 450);
                return Math.Max(physicalNeed, Math.Max(0, (target - moisture) * 1000 / Math.Max(1, target)));
            }
            if (dice.Element == ElementId.Wind)
            {
                bool ripe = p != null && t != null && t.WindSeed && e.Cycles.TryGetValue(p.PlantId, out var cycle) && cycle.Stress < 12 && cycle.Seeds > 0;
                return ripe && WindPower(dice, weather, cell) >= 100 ? 1000 : physicalNeed;
            }
            if (dice.Element == ElementId.Light)
            {
                if (moisture <= 150) return 0;
                bool flower = p != null && p.Stage == PlantStage.Flower && t != null && Flora.Fits(t, s, cell) &&
                    e.Cycles.TryGetValue(p.PlantId, out var cycle) && cycle.Stress < 12 &&
                    e.Succession.Flowers.TryGetValue(p.PlantId, out var f) && f.Nectar < SuccessionRules.NectarCap;
                bool pond = CommunityRules.Pond(s, cell) && e.Succession.Community.Algae[cell] < CommunityRules.AlgaeCap;
                if (e.Nutrients[cell] > 0 && ExtraField(dice, weather, cell, ElementId.Light) >= 100 && (flower || pond)) return 1000;
            }
            if (dice.Element == ElementId.Seed)
                foreach (var seed in e.Succession.SeedBank) if (seed.CellId == cell) return physicalNeed / 4;
            return physicalNeed;
        }
        public static void LightNectar(WorldState s, EcologyContext ctx, EcologyResult r)
        {
            if (!Enabled(s)) return;
            foreach (var p in s.Plants)
            {
                int c = p.CellId;
                if (p.Stage != PlantStage.Flower || ExtraField(ctx.Dice, ctx.Weather, c, ElementId.Light) < 100 ||
                    !Flora.Fits(Flora.Get(p.Species), s, c) || s.Ecosystem.Cycles[p.PlantId].Stress >= 12 || s.Ecosystem.Nutrients[c] == 0) continue;
                var flower = s.Ecosystem.Succession.Flowers[p.PlantId];
                if (flower.Nectar == SuccessionRules.NectarCap) continue;
                flower.Nectar++; s.Ecosystem.Nutrients[c]--;
                r.Relations.Add(new EcologicalLink { Kind = "care-nectar", Actor = ctx.CurrentRecipeId, Species = p.Species,
                    FromCell = ctx.Dice.AnchorCellId, ToCell = c, Amount = 1 });
            }
        }
        public static List<int> ReachedPlants(WorldState s, CareMeasure measure)
        {
            var cells = new List<int>();
            if (measure != null) foreach (var p in s.Plants) if (measure.Touched[p.CellId]) cells.Add(p.CellId);
            cells.Sort(); return cells;
        }
    }
}
