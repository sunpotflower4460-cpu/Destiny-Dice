using System;
using System.Collections.Generic;
using System.Text;

namespace DestinyGarden.Domain
{
    public sealed class ResidentReading
    {
        public string Kind, Label, Habitat, YoungLabel, FoodStatus, HabitatStatus;
        public int CellId, Adults, Young;
    }
    public static class CommunityInspection
    {
        public static List<ResidentReading> Read(WorldState s, string habitat = null)
        {
            var result = new List<ResidentReading>(); var m = s.Ecosystem?.Succession?.Community;
            if (m == null) return result;
            foreach (var f in m.Families)
            {
                string place = LivingEcosystem.Habitat(s, f.CellId);
                if (habitat != null && habitat != place) continue;
                result.Add(new ResidentReading { Kind = f.Kind, Label = EcologyObservations.ResidentLabel(f.Kind), Habitat = place,
                    CellId = f.CellId, Adults = f.Adults, Young = f.Brood,
                    YoungLabel = f.Brood > 0 ? EcologyObservations.ResidentLabel(CommunityRules.YoungKind(f)) : "子育てはひと休み",
                    FoodStatus = f.Adults == 0 ? "幼い命が、この場所で育っている" : f.AdultHunger >= 4 ? "餌が足りない時が続いている"
                        : f.AdultHunger > 0 ? "餌を探している" : "この場所で暮らしている",
                    HabitatStatus = CommunityRules.HabitatSuitable(s, f.Kind, f.CellId) ? "住み場所の条件が合っている"
                        : f.Brood > 0 ? "住み場所の条件が変わり、幼い命がこの場所に残っている" : "住み場所の条件が変わり、移れる場所が必要" });
            }
            result.Sort((a, b) => string.CompareOrdinal(a.Kind, b.Kind) != 0 ? string.CompareOrdinal(a.Kind, b.Kind) : a.CellId.CompareTo(b.CellId));
            return result;
        }
        public static string Describe(WorldState s, string habitat = null)
        {
            var m = s.Ecosystem?.Succession?.Community; if (m == null) return "";
            var sb = new StringBuilder(); sb.AppendLine("\nこの庭に根づく暮らし");
            var readings = Read(s, habitat);
            if (readings.Count == 0) sb.AppendLine("ここで暮らしを始めた住人は、まだいない。");
            foreach (var f in readings)
            {
                sb.AppendLine($"{f.Label}（{f.Habitat}）　成体 {f.Adults} / {f.YoungLabel}" + (f.Young > 0 ? $" {f.Young}" : ""));
                sb.AppendLine("　" + f.FoodStatus + "。" + f.HabitatStatus + "。");
            }
            int algae = 0, prey = 0;
            for (int c = 0; c < GardenMapData.CellCount; c++)
            {
                if (habitat != null && LivingEcosystem.Habitat(s, c) != habitat) continue;
                algae += m.Algae[c]; prey += m.AquaticPrey[c];
            }
            if (habitat == null || habitat == "水辺")
            {
                sb.AppendLine($"\n水辺の食べもの　藻 {algae} / 小さな餌 {prey}");
                sb.AppendLine("養分から藻へ。落ち葉や藻から、小さな餌へ。\nオタマジャクシは藻を、ヤゴは小さな餌を食べて育つ。");
            }
            return sb.ToString();
        }
    }
}
