using System;
using System.Collections.Generic;
using DestinyGarden.Application;
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

        /// <summary>The garden's keepsake card (庭のしおり): built from saved facts only, every 48 rolls and from the menu.</summary>
        public sealed class Card
        {
            public string Name, House;
            public int Rolls, LuckyNumbers, Rainbows, LuckyPlants, Wishes;
            public List<string> Flowers = new List<string>();     // flower names that bloomed in this garden, catalog order
            public List<string> Creatures = new List<string>();   // creatures met, in the order they first came
        }

        public const int KeepsakeEvery = 48;
        public const int CardNames = 7;

        /// <summary>A card is due after this roll (the 48th, 96th, ...).</summary>
        public static bool CardDue(int rolls) => rolls > 0 && rolls % KeepsakeEvery == 0;

        // Links that show a flower was open in this garden.
        private static readonly HashSet<string> FlowerLinks = new HashSet<string>(StringComparer.Ordinal)
        { "fruited", "flower-rest", "pollinated", "nectar-fed", "care-nectar", LivingEcosystem.RainbowPollinated };

        // Present in the ecology but not creatures one "meets".
        private static readonly HashSet<string> NotCreatures = new HashSet<string>(StringComparer.Ordinal)
        { Ecology.Dew, Ecology.Fluff, Ecology.Moss, Ecology.Mushroom, CommunityRules.Algae };

        public static Card BuildCard(string name, WorldState s, IReadOnlyList<RoundSummary> rounds, string house, int luckyPlants)
        {
            var card = new Card { Name = name, House = house, LuckyPlants = luckyPlants, Rolls = s != null ? s.WorldRevision : 0 };
            var flowers = new HashSet<string>(StringComparer.Ordinal);
            var creatures = new List<string>();
            if (s != null) foreach (var p in s.Plants) if (p.Stage == PlantStage.Flower && p.Species != null) flowers.Add(p.Species);
            if (rounds != null)
                foreach (var r in rounds)
                {
                    if (!r.Resolved) continue;
                    if (r.LuckyNumber) card.LuckyNumbers++;
                    if (r.Rainbow) card.Rainbows++;
                    if (r.IntentMatched && (r.IntentKind == "face" || r.IntentKind == "family")) card.Wishes++;
                    foreach (var link in r.Relations)
                        if (link.Species != null && FlowerLinks.Contains(link.Kind)) flowers.Add(link.Species);
                    foreach (string kind in r.EcologyKinds)
                    {
                        if (kind == null || NotCreatures.Contains(kind) || kind.EndsWith("-eggs", StringComparison.Ordinal) ||
                            kind.EndsWith("-pupa", StringComparison.Ordinal)) continue;
                        string label = EcologyObservations.ResidentLabel(kind);
                        if (label != null && !creatures.Contains(label)) creatures.Add(label);
                    }
                }
            var ordered = new List<string>(flowers);
            ordered.Sort((a, b) => Flora.IndexOf(a) != Flora.IndexOf(b) ? Flora.IndexOf(a).CompareTo(Flora.IndexOf(b)) : string.CompareOrdinal(a, b));
            foreach (string sp in ordered) card.Flowers.Add(Flora.Get(sp)?.LabelJa ?? sp);
            // The creatures that mark a garden first (butterflies, fireflies ...), then the others in the order they came.
            var signature = new List<string>();
            foreach (string kind in Signature) signature.Add(EcologyObservations.ResidentLabel(kind));
            int Rank(string label) { int i = signature.IndexOf(label); return i < 0 ? signature.Count : i; }
            var firstCame = new List<string>(creatures);
            creatures.Sort((a, b) => Rank(a) != Rank(b) ? Rank(a).CompareTo(Rank(b)) : firstCame.IndexOf(a).CompareTo(firstCame.IndexOf(b)));
            card.Creatures = creatures;
            return card;
        }

        /// <summary>Names joined with "・", at most seven, then "ほかN".</summary>
        public static string NameList(IList<string> names, int max = CardNames)
        {
            if (names == null || names.Count == 0) return null;
            var shown = new List<string>();
            for (int i = 0; i < names.Count && i < max; i++) shown.Add(names[i]);
            string list = string.Join("・", shown);
            return names.Count > max ? list + $" ほか{names.Count - max}" : list;
        }

        /// <summary>Flower names are longer than creature names: six keep the line whole.</summary>
        public const int CardFlowerNames = 6;
    }
}
