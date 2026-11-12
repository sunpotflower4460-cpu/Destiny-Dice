using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>Shared wording for live feedback and saved history. Only describes recorded causes.</summary>
    public static class EcologyObservations
    {
        public static string ResidentLabel(string kind)
        {
            switch (kind)
            {
                case Ecology.Bee: return "花蜂";
                case Ecology.Butterfly: return "蝶";
                case Ecology.Moss: return "光る苔";
                case Ecology.Firefly: return "蛍";
                case Ecology.Mushroom: return "きのこ";
                case Ecology.Worm: return "ミミズ";
                case Ecology.Dragonfly: return "トンボ";
                case Ecology.Bird: return "小鳥";
                case Ecology.Fluff: return "綿毛";
                case Ecology.Dew: return "露";
                case LivingEcosystem.Aphid: return "アブラムシ";
                case LivingEcosystem.Ladybird: return "テントウムシ";
                case LivingEcosystem.Hoverfly: return "ハナアブ";
                case LivingEcosystem.Spider: return "クモ";
                case LivingEcosystem.Springtail: return "トビムシ";
                case LivingEcosystem.Snail: return "カタツムリ";
                case LivingEcosystem.Frog: return "カエル";
                default: return null;
            }
        }

        public static string ResidentGuide(string kind)
        {
            switch (kind)
            {
                case Ecology.Bee: return "雨のやんだ花へ。花粉を届け、実りを支える。";
                case Ecology.Butterfly: return "好みの花を訪れて受粉を助ける。苦手な花には立ち寄らない。";
                case LivingEcosystem.Hoverfly: return "開いた形の花とアブラムシが近い所へ。訪花と食物網の二つをつなぐ。";
                case Ecology.Bird: return "実った果実を食べて、同じ種を遠くへ運ぶ。葉の小虫も食べる。";
                case Ecology.Mushroom: return "湿った陰に落ち葉がたまると育ち、落ち葉を養分へ戻す。";
                case Ecology.Worm: return "ほどよく湿った落ち葉の土で分解を助ける。水びたしの土は苦手。";
                case LivingEcosystem.Springtail: return "湿った落ち葉に暮らし、細かな残りを土へ還す。";
                case LivingEcosystem.Snail: return "湿った落ち葉を食べて分解を助ける。";
                case LivingEcosystem.Aphid: return "茂った葉で少しずつ増える。増えすぎると株の負担に。";
                case LivingEcosystem.Ladybird: return "育った葉の隠れ場所と獲物がそろうと訪れ、アブラムシを食べる。";
                case LivingEcosystem.Spider: return "葉の重なる隠れ場所から、近くのアブラムシを捕らえる。";
                case LivingEcosystem.Frog: return "湿り続ける水辺と葉陰、近くの小虫がそろうと暮らす。";
                case Ecology.Dragonfly: return "一時的な水たまりではなく、湿りの続く水辺に現れる。";
                case Ecology.Moss: return "湿りが続く岩陰や草木の陰を示す、小さな住み場所。";
                case Ecology.Firefly: return "苔と湿った水辺がそろう、やわらかな空の庭に灯る。";
                default: return "庭の条件に応じて訪れる。";
            }
        }

        public static string Describe(EcologicalLink e)
        {
            string plant = Flora.Get(e.Species)?.LabelJa ?? "植物";
            string actor = ResidentLabel(e.Actor) ?? "小さな命";
            switch (e.Kind)
            {
                case "seed-dispersed": return e.Actor == Ecology.Bird ? $"小鳥が{plant}の実を運び、離れた土に種を残した。"
                    : e.Actor == Ecology.Fluff ? $"風に乗った{plant}の種が、東の土へ。" : $"{plant}の実った種が、そばの土にこぼれた。";
                case "pollinated": return $"{actor}が{plant}を訪れ、花粉を届けた。";
                case "fruited": return $"受粉を終えた{plant}に、次の世代の種が実った。";
                case "decomposed": return $"{actor}が落ち葉をほどき、土に養分を戻した。";
                case "nitrogen-fixed": return $"{plant}と根粒菌が、まわりの土を豊かにした。";
                case "nutrient-shared": return $"菌根を通じ、養分の乏しい{plant}の根へ分け合いが届いた。";
                case "predation": return $"{actor}がアブラムシを食べ、葉のまわりが落ち着いた。";
                case "stressed": return e.Actor == "competition" ? $"混み合う葉の下で、{plant}が少し弱っている。"
                    : e.Actor == LivingEcosystem.Aphid ? $"アブラムシの増えた葉で、{plant}が少し弱っている。"
                    : e.Actor == "nutrients" ? $"養分の乏しい土で、{plant}が少し弱っている。" : $"水や光が合わず、{plant}が少し弱っている。";
                case "recovered": return $"居場所が落ち着き、{plant}が元気を取り戻した。";
                case "flower-rest": return $"{plant}が花を休め、こぼれた花びらが土に還った。";
                case "returned-to-soil": return $"{plant}が命を終え、次の芽を支える土へ還った。";
                default: return null;
            }
        }

        public static string Highlight(IList<EcologicalLink> links)
        {
            foreach (string kind in new[] { "seed-dispersed", "returned-to-soil", "recovered", "stressed", "fruited",
                "flower-rest", "nutrient-shared", "nitrogen-fixed", "predation", "decomposed", "pollinated" })
                foreach (var e in links) if (e.Kind == kind) return Describe(e);
            return null;
        }

        public static string HabitatSummary(WorldState s)
        {
            if (s.Ecosystem == null) return null;
            var counts = new Dictionary<string, int>();
            foreach (string label in new[] { "水辺", "岩陰", "草木の陰", "乾いた草地", "花の草地" }) counts[label] = 0;
            int litter = 0, nutrients = 0;
            for (int c = 0; c < GardenMapData.CellCount; c++)
            {
                if (GardenMapData.Plantable[c] != 1) continue;
                counts[LivingEcosystem.Habitat(s, c)]++;
                litter += s.Ecosystem.Litter[c]; nutrients += s.Ecosystem.Nutrients[c];
            }
            var parts = new List<string>();
            foreach (string label in new[] { "水辺", "岩陰", "草木の陰", "乾いた草地", "花の草地" })
                if (counts[label] > 0) parts.Add(label + " " + counts[label] + "区画");
            return PlantStrategies.SeasonLabel(PlantStrategies.SeasonAt(s)) + "の頃　" + string.Join(" / ", parts) +
                $"\n土の循環　落ち葉 {litter} → 分解者 → 養分 {nutrients} → 根 → 花と実";
        }
    }
}
