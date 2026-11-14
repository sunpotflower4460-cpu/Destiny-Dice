using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>Resolved recipe footprint against a fixed anchor and start state (preparedResolution).</summary>
    public sealed class ResolvedRecipe
    {
        public string RecipeId;
        public ElementId Element;
        public int AnchorCellId;
        // Water: per-tick per-cell injection amounts, key = tickOffset.
        public Dictionary<int, List<KeyValuePair<int, int>>> WaterSchedule = new Dictionary<int, List<KeyValuePair<int, int>>>();
        // Wind/Light: target cells with falloffQ, active for all 40 ticks.
        public List<KeyValuePair<int, int>> FieldCells = new List<KeyValuePair<int, int>>();
        public int StrengthQ; // wind strengthQ or light extraLightQ at anchor
        // Seed: chosen empty plantable cells (distance, cellId order).
        public List<int> SeedCells = new List<int>();
        public bool Eligible;
        public string IneligibleReason;
    }

    public static class RecipeCatalog
    {
        public static readonly string[] Order =
        {
            "water_drop", "water_rain", "wind_breeze", "wind_passage",
            "light_soft", "light_beam", "seed_single", "seed_scatter"
        };

        public static ElementId ElementOf(string recipeId)
        {
            switch (recipeId)
            {
                case "water_drop": case "water_rain": return ElementId.Water;
                case "wind_breeze": case "wind_passage": return ElementId.Wind;
                case "light_soft": case "light_beam": return ElementId.Light;
                case "seed_single": case "seed_scatter": return ElementId.Seed;
                default: throw new ArgumentException("Unknown recipe: " + recipeId);
            }
        }

        public static string LabelJa(string recipeId)
        {
            switch (recipeId)
            {
                case "water_drop": return "しずく";
                case "water_rain": return "雨";
                case "wind_breeze": return "そよ風";
                case "wind_passage": return "通り風";
                case "light_soft": return "柔らかい光";
                case "light_beam": return "一筋の光";
                case "seed_single": return "種ひと粒";
                case "seed_scatter": return "種まき";
                default: return recipeId;
            }
        }

        /// <summary>Short action shown on the prepared die and when reporting a quiet roll.</summary>
        public static string ActionLabelJa(string recipeId) => recipeId switch
        {
            "water_drop" => "土に水を注ぐ",
            "water_rain" => "雨を降らせる",
            "wind_breeze" => "そよ風を送る",
            "wind_passage" => "強い風を送る",
            "light_soft" => "周りを照らす",
            "light_beam" => "一点を照らす",
            "seed_single" => "種を1粒まく",
            "seed_scatter" => "種を2粒まく",
            _ => LabelJa(recipeId)
        };
    }

    public static class RecipeResolver
    {
        public static ResolvedRecipe Resolve(string recipeId, int anchorCellId, WorldState stateBefore)
        {
            var r = new ResolvedRecipe
            {
                RecipeId = recipeId,
                Element = RecipeCatalog.ElementOf(recipeId),
                AnchorCellId = anchorCellId,
                Eligible = true
            };
            switch (recipeId)
            {
                case "water_drop":
                    if (IsSolid(anchorCellId)) return Ineligible(r, "anchor_solid");
                    r.WaterSchedule[0] = new List<KeyValuePair<int, int>> { new KeyValuePair<int, int>(anchorCellId, 1200) };
                    break;
                case "water_rain":
                    {
                        var cells = NonSolidPlus(anchorCellId);
                        if (cells.Count < 3) return Ineligible(r, "too_few_cells");
                        for (int t = 0; t < 12; t++) r.WaterSchedule[t] = DistributeUniform(cells, 100);
                        break;
                    }
                case "wind_breeze": r.StrengthQ = 500; r.FieldCells = ManhattanFalloff(anchorCellId, 3); break;
                case "wind_passage": r.StrengthQ = 1000; r.FieldCells = ManhattanFalloff(anchorCellId, 1); break;
                case "light_soft": r.StrengthQ = 250; r.FieldCells = ManhattanFalloff(anchorCellId, 2); break;
                case "light_beam": r.StrengthQ = 500; r.FieldCells = ManhattanFalloff(anchorCellId, 0); break;
                case "seed_single": ResolveSeed(r, anchorCellId, 0, 1, 1, stateBefore); break;
                case "seed_scatter": ResolveSeed(r, anchorCellId, 2, 3, 2, stateBefore); break;
            }
            return r;
        }

        private static ResolvedRecipe Ineligible(ResolvedRecipe r, string reason)
        {
            r.Eligible = false; r.IneligibleReason = reason; return r;
        }

        private static void ResolveSeed(ResolvedRecipe r, int anchor, int minD, int maxD, int count, WorldState state)
        {
            var band = SeedBandCells(anchor, minD, maxD);
            var empty = new List<int>();
            foreach (int c in band) if (state.PlantAt(c) == null) empty.Add(c);
            if (empty.Count < count) { r.Eligible = false; r.IneligibleReason = "seed_band_full"; return; }
            for (int i = 0; i < count; i++) r.SeedCells.Add(empty[i]);
        }

        /// <summary>All plantable cells in the manhattan band, ordered by distance then cellId.</summary>
        public static List<int> SeedBandCells(int anchor, int minD, int maxD)
        {
            var cells = new List<KeyValuePair<int, int>>();
            int ax = anchor % 16, ay = anchor / 16;
            for (int id = 0; id < 256; id++)
            {
                if (GardenMapData.Plantable[id] == 0) continue;
                int d = Math.Abs(id % 16 - ax) + Math.Abs(id / 16 - ay);
                if (d >= minD && d <= maxD) cells.Add(new KeyValuePair<int, int>(d, id));
            }
            cells.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value));
            var result = new List<int>();
            foreach (var c in cells) result.Add(c.Value);
            return result;
        }

        public static bool IsSolid(int cellId) => GardenMapData.Solid[cellId] == 1;

        public static List<int> NonSolidPlus(int anchor)
        {
            var cells = new List<int>();
            int ax = anchor % 16, ay = anchor / 16;
            AddIf(cells, ax, ay); AddIf(cells, ax, ay - 1); AddIf(cells, ax + 1, ay);
            AddIf(cells, ax, ay + 1); AddIf(cells, ax - 1, ay);
            cells.Sort();
            return cells;
        }

        private static void AddIf(List<int> cells, int x, int y)
        {
            if (x < 0 || x >= 16 || y < 0 || y >= 16) return;
            int id = y * 16 + x;
            if (!IsSolid(id)) cells.Add(id);
        }

        /// <summary>Equal-weight split; remainder goes to lowest cellIds first.</summary>
        public static List<KeyValuePair<int, int>> DistributeUniform(List<int> cellsSorted, int total)
        {
            var result = new List<KeyValuePair<int, int>>();
            int n = cellsSorted.Count;
            int baseAmt = total / n;
            int rem = total % n;
            for (int i = 0; i < n; i++)
                result.Add(new KeyValuePair<int, int>(cellsSorted[i], baseAmt + (i < rem ? 1 : 0)));
            return result;
        }

        /// <summary>linear-manhattan-q-v0.2: floor((radius+1-distance)*1000/(radius+1)); solid cells excluded.</summary>
        public static List<KeyValuePair<int, int>> ManhattanFalloff(int anchor, int radius)
        {
            var result = new List<KeyValuePair<int, int>>();
            int ax = anchor % 16, ay = anchor / 16;
            for (int id = 0; id < 256; id++)
            {
                if (IsSolid(id)) continue;
                int d = Math.Abs(id % 16 - ax) + Math.Abs(id / 16 - ay);
                if (d > radius) continue;
                int q = (radius + 1 - d) * 1000 / (radius + 1);
                result.Add(new KeyValuePair<int, int>(id, q));
            }
            return result;
        }

        /// <summary>Wind shelter for east wind: ray west from the cell; first solid blocks (250), else 1000.</summary>
        public static int ShelterQEast(int cellId)
        {
            int x = cellId % 16, y = cellId / 16;
            for (int cx = x - 1; cx >= 0; cx--)
                if (IsSolid(y * 16 + cx)) return 250;
            return 1000;
        }
    }
}
