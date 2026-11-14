using System.Collections.Generic;
using DestinyGarden.Domain;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// The garden's own name and its keepsake (presentation only, never saved; derived each time from the saved
    /// garden). The name follows what this garden became: its most numerous flower, the creature that lives there,
    /// and what its luck brought (rainbows, lucky plants). Two gardens rarely share a name, and a garden's name
    /// changes as it grows.
    /// </summary>
    public static class GardenKeepsake
    {
        // Creatures that give a garden its character, most telling first.
        private static readonly string[] Signature =
        {
            Ecology.Butterfly, Ecology.Firefly, Ecology.Dragonfly, LivingEcosystem.Frog, Ecology.Bird,
            LivingEcosystem.Ladybird, Ecology.Bee, LivingEcosystem.Snail, Ecology.Mushroom, Ecology.Moss
        };

        /// <summary>The flower this garden has most of (blooming plants count double); null for a bare garden.</summary>
        public static string SignatureFlower(WorldState s)
        {
            if (s == null) return null;
            var score = new Dictionary<string, int>();
            foreach (var p in s.Plants)
            {
                if (p.Species == null) continue;
                score.TryGetValue(p.Species, out int v);
                score[p.Species] = v + (p.Stage == PlantStage.Flower ? 2 : 1);
            }
            string best = null; int bestScore = 0, bestIndex = int.MaxValue;
            foreach (var kv in score)
            {
                int index = Flora.IndexOf(kv.Key);
                if (kv.Value > bestScore || (kv.Value == bestScore && index < bestIndex))
                { best = kv.Key; bestScore = kv.Value; bestIndex = index; }
            }
            return best;
        }

        /// <summary>The creature that most marks this garden (from the residents present now); null when none.</summary>
        public static string SignatureCreature(ICollection<string> residentKinds)
        {
            if (residentKinds == null) return null;
            foreach (string kind in Signature) if (residentKinds.Contains(kind)) return kind;
            return null;
        }

        /// <summary>
        /// "〈虹のかかるコスモスと蝶の庭〉": an epithet from its luck (three rainbows, or seven lucky plants), its flower
        /// and its creature. A bare garden is "まだ名前のない庭".
        /// </summary>
        public static string Name(WorldState s, ICollection<string> residentKinds, int rainbows, int luckyPlants)
        {
            string flower = SignatureFlower(s);
            if (flower == null) return "まだ名前のない庭";
            var t = Flora.Get(flower);
            string creature = SignatureCreature(residentKinds);
            string epithet = rainbows >= 3 ? "虹のかかる" : luckyPlants >= 7 ? "幸運の灯る" : rainbows >= 1 ? "虹を見た" : "";
            string who = creature != null ? "と" + EcologyObservations.ResidentLabel(creature) : "";
            return epithet + (t != null ? t.LabelJa : flower) + who + "の庭";
        }

        /// <summary>The keepsake line at the end of a season: the name and what this garden's luck has brought.</summary>
        public static string SeasonKeepsake(string name, int rolls, int luckyCount, int luckyPlants, int rainbows, int seasonsMet)
        {
            var parts = new List<string> { $"〈{name}〉 {rolls}投" };
            parts.Add($"ラッキーナンバー {luckyCount}");
            if (luckyPlants > 0) parts.Add($"幸運の株 {luckyPlants}");
            if (rainbows > 0) parts.Add($"八の虹 {rainbows}");
            if (seasonsMet > 0) parts.Add($"叶った季節 {seasonsMet}");
            return string.Join("・", parts);
        }
    }
}
