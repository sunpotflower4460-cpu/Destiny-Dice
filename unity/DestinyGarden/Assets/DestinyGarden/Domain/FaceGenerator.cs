using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    public sealed class FaceCandidate
    {
        public string RecipeId;
        public ElementId Element;
        public bool Eligible;
        public string IneligibleReason;
        public int LocalNeedQ;
        public int RecipeWeight; // 100..300
    }

    public sealed class PreparedFaces
    {
        public string DrawnSiteId;
        public string ResolvedSiteId;
        public int AnchorCellId = -1; // v8 pins the resolved cell before faces are published
        public string TargetNote;
        public bool ConnectedHabitats; // resolution policy pinned by the coordinator
        public string[] Faces; // length 6, index+1 = faceId
        public List<FaceCandidate> Candidates = new List<FaceCandidate>();
        public List<ResolvedRecipe> Resolved = new List<ResolvedRecipe>();
        public int MappingBytesConsumed;
    }

    /// <summary>
    /// Dynamic 6-face generation (dynamic-faces.v0.6): site draw first, then local needs,
    /// then 3 distinct families, 3 first slots, 3 remaining slots, Fisher-Yates.
    /// 15 UniformBelow calls on the normal path.
    /// </summary>
    public static class FaceGenerator
    {
        private static readonly ElementId[] FamilyOrder =
            { ElementId.Water, ElementId.Wind, ElementId.Light, ElementId.Seed };

        public static PreparedFaces Generate(WorldState stateBefore, WeatherDefinition weather,
            string preferenceMode, string preferenceSiteId, EntropyBuffer mapping)
        {
            int siteIndex = mapping.UniformBelow(3);
            if (siteIndex < 0) return null; // mapping entropy exhausted
            string drawnSiteId = Sites.All[siteIndex].SiteId;
            string resolvedSiteId = preferenceMode == "fixed" ? preferenceSiteId : drawnSiteId;
            int anchor = Sites.ById(resolvedSiteId).CenterCellId;

            var prepared = new PreparedFaces { DrawnSiteId = drawnSiteId, ResolvedSiteId = resolvedSiteId };
            if (CareRules.Enabled(stateBefore))
            {
                prepared.AnchorCellId = anchor;
                if (preferenceMode == "habitat") CareTargets.ResolveHabitat(stateBefore, preferenceSiteId, prepared);
                anchor = prepared.AnchorCellId;
            }

            foreach (string recipeId in RecipeCatalog.Order)
            {
                var resolved = RecipeResolver.Resolve(recipeId, anchor, stateBefore);
                prepared.Resolved.Add(resolved);
                var c = new FaceCandidate
                {
                    RecipeId = recipeId,
                    Element = resolved.Element,
                    Eligible = resolved.Eligible,
                    IneligibleReason = resolved.IneligibleReason
                };
                if (c.Eligible)
                {
                    c.LocalNeedQ = ComputeLocalNeedQ(resolved, stateBefore, weather);
                    int need = (200 * c.LocalNeedQ + 500) / 1000;
                    c.RecipeWeight = 100 + need;
                }
                prepared.Candidates.Add(c);
            }

            // Family weights: floor average of eligible recipe weights.
            var familyWeight = new Dictionary<ElementId, int>();
            var eligibleFamilies = new List<ElementId>();
            foreach (var fam in FamilyOrder)
            {
                int sum = 0, count = 0;
                foreach (var c in prepared.Candidates)
                    if (c.Eligible && c.Element == fam) { sum += c.RecipeWeight; count++; }
                if (count > 0) { familyWeight[fam] = sum / count; eligibleFamilies.Add(fam); }
            }
            if (eligibleFamilies.Count < 3)
                throw new InvalidOperationException("Fewer than 3 eligible families; garden configuration is invalid.");

            var slots = new List<string>();
            // 1) Three distinct families without replacement, weighted.
            var remainingFamilies = new List<ElementId>(eligibleFamilies);
            var chosenFamilies = new List<ElementId>();
            for (int i = 0; i < 3; i++)
            {
                int total = 0;
                foreach (var f in remainingFamilies) total += familyWeight[f];
                int pick = WeightedPickIndex(mapping, remainingFamilies.ConvertAll(f => familyWeight[f]), total);
                if (pick < 0) return null;
                chosenFamilies.Add(remainingFamilies[pick]);
                remainingFamilies.RemoveAt(pick);
            }
            // 2) One recipe per chosen family, in draw order.
            foreach (var fam in chosenFamilies)
            {
                var recipes = EligibleRecipes(prepared.Candidates, fam);
                int total = SumWeights(recipes);
                int pick = WeightedPickIndex(mapping, recipes.ConvertAll(c => c.RecipeWeight), total);
                if (pick < 0) return null;
                slots.Add(recipes[pick].RecipeId);
            }
            // 3) Remaining three slots from all eligible recipes with caps (recipe<=2, family<=3).
            for (int s = 0; s < 3; s++)
            {
                var pool = new List<FaceCandidate>();
                foreach (var c in prepared.Candidates)
                {
                    if (!c.Eligible) continue;
                    if (CountOf(slots, c.RecipeId) >= 2) continue;
                    if (CountFamily(slots, c.Element) >= 3) continue;
                    pool.Add(c);
                }
                if (pool.Count == 0)
                    throw new InvalidOperationException("No candidate left for remaining slot; configuration invalid.");
                int total = SumWeights(pool);
                int pick = WeightedPickIndex(mapping, pool.ConvertAll(c => c.RecipeWeight), total);
                if (pick < 0) return null;
                slots.Add(pool[pick].RecipeId);
            }
            // 4) Fisher-Yates i=5..1.
            for (int i = 5; i >= 1; i--)
            {
                int j = mapping.UniformBelow(i + 1);
                if (j < 0) return null;
                (slots[i], slots[j]) = (slots[j], slots[i]);
            }
            prepared.Faces = slots.ToArray();
            prepared.MappingBytesConsumed = mapping.Offset;
            return prepared;
        }

        private static int WeightedPickIndex(EntropyBuffer buf, List<int> weights, int total)
        {
            int v = buf.UniformBelow(total);
            if (v < 0) return -1;
            int acc = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                acc += weights[i];
                if (v < acc) return i;
            }
            return weights.Count - 1;
        }

        private static List<FaceCandidate> EligibleRecipes(List<FaceCandidate> all, ElementId fam)
        {
            var l = new List<FaceCandidate>();
            foreach (var c in all) if (c.Eligible && c.Element == fam) l.Add(c);
            return l;
        }

        private static int SumWeights(List<FaceCandidate> l)
        {
            int s = 0; foreach (var c in l) s += c.RecipeWeight; return s;
        }

        private static int CountOf(List<string> slots, string recipeId)
        {
            int n = 0; foreach (var s in slots) if (s == recipeId) n++; return n;
        }

        private static int CountFamily(List<string> slots, ElementId fam)
        {
            int n = 0; foreach (var s in slots) if (RecipeCatalog.ElementOf(s) == fam) n++; return n;
        }

        private static int ClampQ(int v) => v < 0 ? 0 : (v > 1000 ? 1000 : v);

        private static int MoistureQ(WorldState s, int cellId)
        {
            int cap = GardenMapData.SoilCapacity[cellId];
            return cap <= 0 ? 0 : 1000 * s.SoilWater[cellId] / cap;
        }

        /// <summary>Local need per GAMEPLAY 4.2/4.3 using the recipe's own footprint only.</summary>
        public static int ComputeLocalNeedQ(ResolvedRecipe r, WorldState s, WeatherDefinition weather)
        {
            const int low = 400, high = 700, sat = 1000;
            long doseSum = 0, weighted = 0;

            void Accumulate(int cellId, long dose, int needQ)
            {
                if (dose <= 0) return;
                needQ = CareRules.LocalNeed(r, s, weather, cellId, needQ);
                doseSum += dose;
                weighted += dose * needQ;
            }

            switch (r.Element)
            {
                case ElementId.Water:
                    {
                        var perCell = new Dictionary<int, long>();
                        foreach (var tick in r.WaterSchedule.Values)
                            foreach (var kv in tick)
                                perCell[kv.Key] = (perCell.TryGetValue(kv.Key, out long v) ? v : 0) + kv.Value;
                        foreach (var kv in perCell)
                        {
                            if (GardenMapData.SoilCapacity[kv.Key] <= 0) continue;
                            int m = MoistureQ(s, kv.Key);
                            Accumulate(kv.Key, kv.Value, ClampQ(1000 * Math.Max(0, low - m) / low));
                        }
                        break;
                    }
                case ElementId.Light:
                    foreach (var kv in r.FieldCells)
                    {
                        int cell = kv.Key;
                        if (GardenMapData.SoilCapacity[cell] <= 0) continue;
                        int exposure = GardenMapData.LightExposureQ[cell];
                        int baseEff = weather.BaseLightQ * exposure / 1000;
                        int extra = r.StrengthQ * kv.Value / 1000;
                        int totalEff = Math.Min(1000, weather.BaseLightQ + extra) * exposure / 1000;
                        int additional = totalEff - baseEff;
                        if (additional <= 0) continue;
                        int needQ = ClampQ(1000 * Math.Max(0, sat - baseEff) / sat);
                        Accumulate(cell, (long)additional * 40, needQ);
                    }
                    break;
                case ElementId.Wind:
                    foreach (var kv in r.FieldCells)
                    {
                        int cell = kv.Key;
                        if (GardenMapData.SoilCapacity[cell] <= 0) continue;
                        int shelter = RecipeResolver.ShelterQEast(cell);
                        int baseEff = weather.BaseWindQ * shelter / 1000;
                        int extra = r.StrengthQ * kv.Value / 1000;
                        int totalEff = Math.Min(1000, weather.BaseWindQ + extra) * shelter / 1000;
                        int additional = totalEff - baseEff;
                        if (additional <= 0) continue;
                        int m = MoistureQ(s, cell);
                        int needQ = ClampQ(1000 * Math.Max(0, m - high) / (1000 - high));
                        Accumulate(cell, (long)additional * 40, needQ);
                    }
                    break;
                case ElementId.Seed:
                    {
                        int minD = r.RecipeId == "seed_single" ? 0 : 2;
                        int maxD = r.RecipeId == "seed_single" ? 1 : 3;
                        foreach (int cell in RecipeResolver.SeedBandCells(r.AnchorCellId, minD, maxD))
                            Accumulate(cell, 1, s.PlantAt(cell) == null ? 1000 : 0);
                        break;
                    }
            }
            return doseSum == 0 ? 0 : (int)(weighted / doseSum);
        }
    }
}
