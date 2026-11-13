using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>Shared wording for live feedback and saved history. Only describes recorded causes.</summary>
    public static class EcologyObservations
    {
        public static string ResidentLabel(string kind)
        {
            if (kind != null && kind.EndsWith("-eggs", StringComparison.Ordinal)) return ResidentLabel(kind.Substring(0, kind.Length - 5)) + "の卵";
            if (kind != null && kind.EndsWith("-pupa", StringComparison.Ordinal)) return ResidentLabel(kind.Substring(0, kind.Length - 5)) + "のさなぎ";
            switch (kind)
            {
                case CommunityRules.Algae: return "藻";
                case CommunityRules.PondPrey: return "水辺の小さな命";
                case CommunityRules.Caterpillar: return "蝶の幼虫";
                case CommunityRules.Chrysalis: return "蝶のさなぎ";
                case CommunityRules.Tadpole: return "オタマジャクシ";
                case CommunityRules.Nymph: return "ヤゴ";
                case CommunityRules.LadybirdLarva: return "テントウムシの幼虫";
                case CommunityRules.HoverflyLarva: return "ハナアブの幼虫";
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
                case CommunityRules.Caterpillar: return "シロツメクサ・カラスノエンドウ・スミレ・野ばら・野イチゴなど、合う食草の葉を食べて育つ。根は残す。";
                case CommunityRules.Chrysalis: return "食べるのを休み、蓄えで蝶へ姿を変える時。";
                case CommunityRules.LadybirdLarva: return "葉の近くのアブラムシを食べ、やがてさなぎになる。";
                case CommunityRules.HoverflyLarva: return "成体が花蜜を飲む一方、幼虫は葉の小虫を食べる。";
                case CommunityRules.Nymph: return "持続した水辺で、小さな水生の餌を食べてトンボに育つ。";
                case CommunityRules.Tadpole: return "藻を食べて育つ。水辺がなくなると、ほかの場所へすぐには移れない。";
                case CommunityRules.Algae: return "湿りが続く水辺で、養分を使って光から育つ。オタマジャクシの食べもの。";
                case CommunityRules.PondPrey: return "落ち葉や藻に支えられ、ヤゴやカエルにつながる水辺の食べもの。";
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
                case "humus-formed": return $"{actor}がほどいた落ち葉の一部が、長く土を支える腐植になった。";
                case "humus-mineralised": return "土に残った腐植から、少しずつ養分が戻った。";
                case "care-nectar": return $"届いた光を受け、{plant}が養分を花蜜に使った。";
                case "care-algae": return "届いた光を受け、水辺の藻が少し多く育った。";
                case "clonal-rooted": return $"{plant}の" + (e.Actor == "rhizome" ? "地下茎" : "つる") +
                    (e.FromHabitat != null && e.ToHabitat != null && e.FromHabitat != e.ToHabitat
                        ? $"が、{e.FromHabitat}から{e.ToHabitat}の境目を越えて根づいた。" : "が伸び、そばに新しい株が根づいた。");
                case "habitat-shifted": return $"続いていた{e.FromHabitat}が、{e.ToHabitat}の居場所へ移り変わった。";
                case "resident-settled": return $"{actor}が、この庭に暮らす場所を見つけた。";
                case "brood-laid": return $"{actor}が、次の命を託す卵を残した。";
                case "resident-emerged": return $"幼い命が育ち、この庭で新しい{actor}になった。";
                case "resident-dispersed": return $"育った{actor}が、庭の外へ旅立った。";
                case "refuge-moved": return $"{actor}が、近くの新しい居場所へ移った。";
                case "brood-lost": return $"育つ条件を失った{actor}の幼い命が、土へ還った。";
                case "resident-returned": return $"{actor}の命がひとつ、土へ静かに還った。";
                case "resident-left": return $"居場所を失った{actor}が、庭を離れていった。";
                case "host-leaf-eaten": return "蝶の幼虫が食草の葉を食べ、根を残して育っている。";
                case "algae-grew": return "続いた湿りと養分から、水辺に藻が育った。";
                case "algae-eaten": return "オタマジャクシが、水辺の藻を食べて育っている。";
                case "pond-prey-eaten": return $"{actor}が、水辺の小さな餌を食べた。";
                case "pond-dried": return "乾いた水辺で、小さな命の蓄えが土へ還った。";
                case "seed-buried": return e.Actor == Ecology.Bird ? $"小鳥が運んだ{plant}の種が、土の中で時を待つ。"
                    : e.Actor == Ecology.Fluff ? $"風が運んだ{plant}の種が、次の芽吹きを待つ。" : $"{plant}の種が土に残り、次の命を待っている。";
                case "seed-awakened": return $"土に眠っていた{plant}の種が、居場所を見つけて芽吹いた。";
                case "seed-expired": return $"眠っていた{plant}の種が、土へ静かに還った。";
                case "nectar-fed": return $"{actor}が{plant}の蜜を味わい、花から花へ。";
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
            foreach (string kind in new[] { "resident-emerged", "clonal-rooted", "habitat-shifted", "resident-settled", "refuge-moved", "brood-laid", "seed-awakened", "seed-buried", "seed-dispersed", "returned-to-soil", "recovered", "stressed", "fruited",
                "flower-rest", "nutrient-shared", "nitrogen-fixed", "predation", "decomposed", "pollinated", "nectar-fed", "seed-expired", "brood-lost", "resident-returned", "resident-left",
                "resident-dispersed", "host-leaf-eaten", "algae-eaten", "pond-prey-eaten", "algae-grew", "pond-dried", "humus-formed", "humus-mineralised" })
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
