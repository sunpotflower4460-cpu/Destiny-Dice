using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DestinyGarden.Domain;

namespace DestinyGarden.Application
{
    public sealed class GardenMoment
    {
        public string RoundId, RecipeId, Kind, Species, Actor, Habitat, Headline, Detail, MemoryKey;
        public int Revision, CellId, FromCell, Rank;
        public string Caption => Revision + "投目" + (RecipeId != null ? "・" + RecipeCatalog.LabelJa(RecipeId) : "") + " / " + (Habitat ?? "庭");
    }
    /// <summary>Short scenes from resolved facts. No predictions, invented visits or changes to the saved garden.</summary>
    public static class GardenMoments
    {
        private const string StarGift = "star-gift";

        public static List<GardenMoment> FromOutcome(RollOutcome outcome, string roundId = null)
        {
            if (outcome == null) return new List<GardenMoment>();
            return FromResolved(outcome.Simulation, outcome.Ecology, outcome.RecipeId, roundId);
        }

        internal static List<GardenMoment> FromResolved(SimulationResult simulation, EcologyResult ecology, string recipeId, string roundId)
        {
            var events = new List<GardenEvent>();
            if (simulation != null) events.AddRange(simulation.Events);
            if (ecology != null) events.AddRange(ecology.Events);
            return Read(ecology?.PostState ?? simulation?.PostState, ecology?.Relations ?? new List<EcologicalLink>(),
                events, recipeId, roundId, ecology?.Care, simulation?.DiceWaterInjected ?? 0, ecology?.Effects);
        }
        public static List<GardenMoment> Read(WorldState state, IList<EcologicalLink> links, IList<GardenEvent> events,
            string recipeId, string roundId, CareReport care = null, long diceWater = 0) =>
            Read(state, links, events, recipeId, roundId, care, diceWater, null);

        private static List<GardenMoment> Read(WorldState state, IList<EcologicalLink> links, IList<GardenEvent> events,
            string recipeId, string roundId, CareReport care, long diceWater, IEnumerable<EcologyEffect> effects)
        {
            var candidates = new List<GardenMoment>(); if (state == null) return candidates;
            recipeId = recipeId ?? care?.RecipeId;
            var gifts = new Dictionary<int, int>();
            if (effects != null)
                foreach (var effect in effects)
                    if (effect.Kind == PrayerStar.GiftKind && effect.Amount > 0 &&
                        effect.CellId >= 0 && effect.CellId < GardenMapData.CellCount)
                        gifts[effect.CellId] = effect.Amount;
            foreach (var e in events)
                if (IsPlantReturn(e.Kind)) gifts.Remove(e.CellId);

            void Add(string kind, int c, int from, int rank, string headline, string detail = null, string species = null, string actor = null, string memoryKey = null)
            {
                if (c < 0 || c >= GardenMapData.CellCount) return;
                candidates.Add(new GardenMoment { RoundId = roundId, RecipeId = recipeId, Revision = state.WorldRevision, Kind = kind,
                    CellId = c, FromCell = from, Rank = rank, Species = species, Actor = actor, Habitat = LivingEcosystem.Habitat(state, c), Headline = headline, Detail = detail, MemoryKey = memoryKey });
            }
            EcologicalLink At(string kind, EcologicalLink source, bool species = false, bool actor = false)
            {
                foreach (var other in links)
                    if (other.Kind == kind && other.ToCell == source.ToCell && other.Amount > 0 &&
                        (!species || (source.Species != null && other.Species == source.Species)) && (!actor || other.Actor == source.Actor)) return other;
                return null;
            }
            foreach (var e in links)
            {
                if (e.Amount <= 0 && e.Kind != "returned-to-soil" && e.Kind != "brood-lost" && e.Kind != "resident-returned" && e.Kind != "seed-expired") continue;
                string plant = Flora.Get(e.Species)?.LabelJa ?? "草花", actor = EcologyObservations.ResidentLabel(e.Actor) ?? "小さな命";
                string headline = null, detail = null; int rank = 50;
                switch (e.Kind)
                {
                    case "care-nectar":
                        var visitor = At("nectar-fed", e, true);
                        headline = visitor != null ? "蜜を補った" + plant + "に、" + (EcologyObservations.ResidentLabel(visitor.Actor) ?? "小さな命") + "が訪れた。" : "光を受けた" + plant + "が、蜜を補った。";
                        detail = "花は、根元の養分を蜜に使った。"; rank = 95; break;
                    case "algae-eaten":
                        headline = e.FromCell != e.ToCell ? actor + "が、隣の水辺で藻を食べた。"
                            : At("algae-grew", e) != null ? "藻の育った水辺で、" + actor + "が食べた。" : actor + "が、水辺の藻を食べた。";
                        detail = At("care-algae", e) != null ? "この水辺には、ダイスの光も届いていた。" : null; rank = 94; break;
                    case "recovered":
                        var predator = At("predation", e);
                        headline = predator != null ? "小虫の減った葉で、" + plant + "が持ち直した。" : plant + "が、弱りから持ち直した。";
                        detail = predator != null ? (EcologyObservations.ResidentLabel(predator.Actor) ?? "小さな命") + "が、同じ葉のアブラムシを食べた。" : null; rank = 105; break;
                    case "humus-formed":
                        headline = actor + "がほどいた落ち葉の一部が、腐植になった。";
                        detail = At("decomposed", e, actor: true) != null ? "同じ土に、養分も戻った。" : null; rank = 76; break;
                    case "resident-emerged":
                        string young = e.Actor == LivingEcosystem.Frog ? "オタマジャクシ" : e.Actor == Ecology.Dragonfly ? "ヤゴ" : actor + "の幼い命";
                        headline = young + "が育ち、新しい" + actor + "になった。"; rank = 120; break;
                    case "clonal-rooted":
                        headline = plant + "の" + (e.Actor == "rhizome" ? "地下茎" : "つる") + "が、隣の土に根づいた。";
                        if (e.FromHabitat != null && e.ToHabitat != null && e.FromHabitat != e.ToHabitat) detail = e.FromHabitat + "から、" + e.ToHabitat + "の境目を越えた。";
                        rank = 108; break;
                    case "seed-awakened": headline = "土に眠っていた" + plant + "が、芽を出した。"; rank = 110; break;
                    case "seed-buried": case "seed-dispersed":
                        headline = e.Actor == Ecology.Fluff ? "風に運ばれた" + plant + "の種が、" + (e.ToCell % 16 > e.FromCell % 16 ? "東の" : "次の") + "土に残った。"
                            : e.Actor == Ecology.Bird ? "小鳥が運んだ" + plant + "の種が、離れた土に残った。" : plant + "の種が、土にこぼれた。";
                        rank = 88; break;
                    case "fruited":
                        headline = At("pollinated", e, true) != null ? "花粉を受け取った" + plant + "に、種が実った。" : plant + "に、次の種が実った。";
                        rank = 98; break;
                    case "nutrient-shared": headline = "菌根を通って、" + plant + "の根に養分が届いた。"; rank = 80; break;
                    case "nitrogen-fixed": headline = plant + "の根のまわりで、養分が増えた。"; detail = "根粒菌と暮らす株が、周りの土を豊かにした。"; rank = 63; break;
                    case "brood-lost": headline = actor + "の幼い命が、この場所で土へ還った。"; rank = 125; break;
                    case "resident-left": headline = actor + "が、居場所を離れていった。"; rank = 121; break;
                    case "pond-dried": headline = "水辺が乾き、小さな命の食べものが減った。"; rank = 115; break;
                    case "stressed": headline = EcologyObservations.Describe(e); rank = 111; break;
                    case "returned-to-soil": headline = plant + "の株が、土へ還った。"; rank = 96; break;
                    case "flower-rest": headline = plant + "が花を閉じ、葉の姿に戻った。"; rank = 82; break;
                    case "resident-settled": headline = actor + "が、この庭に暮らし始めた。"; rank = 109; break;
                    case "brood-laid": headline = actor + "が、この場所に卵を残した。"; rank = 107; break;
                    case "refuge-moved": headline = actor + "が、近くの居場所へ移った。"; rank = 106; break;
                    case "habitat-shifted":
                        if (e.FromHabitat == null || e.ToHabitat == null) break;
                        headline = e.FromHabitat + "だった場所が、" + e.ToHabitat + "へ移り変わった。"; rank = 104; break;
                    case "host-leaf-eaten": headline = "蝶の幼虫が、食草の葉を食べた。"; detail = "根はその場所に残った。"; rank = 70; break;
                    case "predation": headline = actor + "が、葉のアブラムシを食べた。"; rank = 64; break;
                    case LivingEcosystem.RainbowPollinated: headline = "虹の下で、" + plant + "が花粉を受け取った。"; rank = 101; break;
                    case "pollinated": headline = actor + "が" + plant + "に、花粉を届けた。"; rank = 66; break;
                    case "nectar-fed": headline = actor + "が、" + plant + "の蜜を飲んだ。"; rank = 62; break;
                    case "decomposed": headline = actor + "が落ち葉をほどき、養分が土に戻った。"; rank = 65; break;
                    case "humus-mineralised": headline = "土に残った腐植から、養分が戻った。"; rank = 61; break;
                    case "care-algae": case "algae-grew": headline = "水辺に、藻が育った。"; rank = 60; break;
                    case "pond-prey-eaten":
                        bool nearbyNursery = e.Actor == CommunityRules.Nymph && e.FromCell != e.ToCell;
                        headline = nearbyNursery ? "ヤゴが、隣の水辺で餌を見つけた。" : actor + "が、水辺の小さな餌を食べた。";
                        rank = nearbyNursery ? 94 : 68;
                        break;
                    case "seed-expired": headline = "土に眠っていた" + plant + "の種が、土へ還った。"; rank = 75; break;
                    case "resident-returned": headline = actor + "の命がひとつ、土へ還った。"; rank = 83; break;
                    case "resident-dispersed": headline = "育った" + actor + "が、庭の外へ旅立った。"; rank = 102; break;
                }
                if (headline != null) Add(e.Kind, e.ToCell, e.FromCell, rank, headline, detail, e.Species, e.Actor, MemoryKey(state, e, events, roundId));
            }
            foreach (var e in events)
            {
                var stage = GrowthStage(e.Kind);
                if (stage == null) continue;
                var p = state.PlantAt(e.CellId);
                // Do not focus a vanished/replaced flower, or say a flower is open after it rested in this roll.
                if (p == null || p.PlantId != e.PlantId || p.Stage < stage.Value) continue;
                string plant = Flora.Get(p.Species)?.LabelJa ?? "草花";
                bool fromStar = state.Ecosystem != null && gifts.TryGetValue(e.CellId, out int giftGrowth) &&
                    StarCrossedStage(e.Kind, p.Growth, giftGrowth, e.Tick);
                string pause = fromStar ? "" : "、";
                string headline = stage == PlantStage.Flower ? plant + "が咲いた。"
                    : stage == PlantStage.Bud ? plant + "に" + pause + "つぼみがついた。"
                    : stage == PlantStage.Leaf ? plant + "が" + pause + "葉を開いた。"
                    : stage == PlantStage.Sprout ? plant + "が" + pause + "芽を出した。" : plant + "の種が、土に入った。";
                if (fromStar) headline = "星の恵みで、" + headline;
                bool placed = false; foreach (var earlier in events) if (earlier.Kind == "SeedPlaced" && earlier.PlantId == p.PlantId) placed = true;
                string detail = placed && stage > PlantStage.Seed ? "この投で蒔かれた種から、育った芽。" : null;
                Add(e.Kind, e.CellId, e.CellId, 72 + (int)stage.Value * 7 + (fromStar ? 8 : 0),
                    headline, detail, p.Species, memoryKey: "p:" + p.PlantId);
            }
            foreach (int cell in gifts.Keys)
            {
                var plant = state.PlantAt(cell);
                if (plant == null || string.IsNullOrEmpty(plant.PlantId)) continue;
                string name = Flora.Get(plant.Species)?.LabelJa ?? "草花";
                string subject = name + (plant.Stage == PlantStage.Seed ? "の種" : "の株");
                Add(StarGift, cell, cell, 71, "星の恵みが、" + subject + "に届いた。", species: plant.Species, memoryKey: "p:" + plant.PlantId);
            }
            int cellOffset = state.WorldRevision * 13;
            candidates.Sort((a, b) =>
            {
                int milestone = MilestonePriority(b.Kind).CompareTo(MilestonePriority(a.Kind));
                if (milestone != 0) return milestone;
                int rank = b.Rank.CompareTo(a.Rank);
                if (rank != 0) return rank;
                int cell = ((a.CellId + cellOffset) % 256).CompareTo((b.CellId + cellOffset) % 256);
                if (cell != 0) return cell;
                return string.CompareOrdinal(a.Kind + ":" + a.Actor + ":" + a.Species + ":" + a.FromCell,
                    b.Kind + ":" + b.Actor + ":" + b.Species + ":" + b.FromCell);
            });
            var result = new List<GardenMoment>(); var cells = new HashSet<int>();
            bool giftShown = false;
            foreach (var moment in candidates)
            {
                bool isGift = moment.Kind == StarGift;
                if ((isGift && giftShown) || !cells.Add(moment.CellId)) continue;
                result.Add(moment);
                giftShown |= isGift;
            }
            if (result.Count == 0)
            {
                // The operation itself is recorded. Do not fill a quiet roll with an invented ecological event.
                string text = diceWater > 0 ? "ダイスの水が、庭に注がれた。"
                    : recipeId != null ? $"「{RecipeCatalog.ActionLabelJa(recipeId)}」の面が出た。"
                    : "一投ぶんの庭が、記録された。";
                result.Add(new GardenMoment { RoundId = roundId, RecipeId = recipeId, Revision = state.WorldRevision,
                    Kind = "recorded-roll", CellId = -1, FromCell = -1, Headline = text });
            }
            return result;
        }

        internal static bool IsPlantReturn(string kind) => kind == "ReturnedToSoil" || kind == Lifecycle.ReturnedEvent;

        // Lead with visible growth and new life. Quiet rolls and setbacks keep their usual order.
        private static int MilestonePriority(string kind) => kind switch
        {
            "Bloomed" => 2,
            "BudFormed" or "LeafOpened" or "Germinated" or "seed-awakened" or "clonal-rooted" or
            "fruited" or "recovered" or "resident-emerged" or "resident-settled" => 1,
            _ => 0
        };

        private static PlantStage? GrowthStage(string kind) => kind switch
        {
            "SeedPlaced" => PlantStage.Seed,
            "Germinated" => PlantStage.Sprout,
            "LeafOpened" => PlantStage.Leaf,
            "BudFormed" => PlantStage.Bud,
            "Bloomed" => PlantStage.Flower,
            _ => null
        };

        // The caller uses this only for the ecosystem that applies the star as its final growth update.
        internal static bool StarCrossedStage(string kind, int finalGrowth, int giftGrowth, int tick)
        {
            if (tick != Simulation.TicksPerRound || giftGrowth <= 0 || giftGrowth > finalGrowth) return false;
            var stage = GrowthStage(kind);
            if (stage == null || stage == PlantStage.Seed) return false;
            int threshold = Simulation.GrowthThreshold(stage.Value);
            return finalGrowth >= threshold && finalGrowth - giftGrowth < threshold;
        }

        internal static IEnumerable<EcologyEffect> ReadStarEffects(IDictionary<string, object> ecology)
        {
            if (!ecology.TryGetValue("effects", out object value) || !(value is IEnumerable effects)) yield break;
            foreach (var effect in effects)
            {
                if (!(effect is IDictionary<string, object> row) || !row.TryGetValue("kind", out object kind) ||
                    (kind as string) != PrayerStar.GiftKind || !row.TryGetValue("cellId", out object cell) ||
                    !row.TryGetValue("amount", out object amount)) continue;
                yield return new EcologyEffect { Kind = PrayerStar.GiftKind, CellId = WorldStateSerializer.ToInt(cell), Amount = WorldStateSerializer.ToInt(amount) };
            }
        }

        private static string MemoryKey(WorldState state, EcologicalLink link, IList<GardenEvent> events, string roundId)
        {
            if (link.Kind == "seed-buried" && roundId != null && state.Ecosystem?.Succession != null)
            {
                string id = null;
                foreach (var seed in state.Ecosystem.Succession.SeedBank)
                    if (seed.SourceRoundId == roundId && seed.BornRevision == state.WorldRevision && seed.CellId == link.ToCell &&
                        seed.ParentCellId == link.FromCell && seed.Species == link.Species && seed.Carrier == link.Actor)
                    { if (id != null) return null; id = seed.SeedId; }
                return id == null ? null : "s:" + id;
            }
            var plant = state.PlantAt(link.ToCell);
            foreach (var ev in events)
            {
                if (ev.CellId != link.ToCell) continue;
                if (IsPlantReturn(ev.Kind)) return link.Kind == "returned-to-soil" && ev.PlantId != null ? "p:" + ev.PlantId : null;
            }
            if (plant == null || plant.Species != link.Species) return null;
            if (link.Kind == "seed-awakened" || link.Kind == "clonal-rooted")
            {
                foreach (var ev in events)
                    if (ev.PlantId == plant.PlantId && ev.CellId == plant.CellId && ev.Kind == (link.Kind == "seed-awakened" ? "Germinated" : "VegetativeRooted")) return "p:" + plant.PlantId;
                return null;
            }
            switch (link.Kind)
            {
                case "care-nectar": case "recovered": case "stressed": case "fruited": case "flower-rest":
                case "pollinated": case LivingEcosystem.RainbowPollinated: case "nectar-fed": case "nutrient-shared": case "nitrogen-fixed": return "p:" + plant.PlantId;
                default: return null;
            }
        }
        public static List<GardenMoment> Recent(IList<JournalEntry> entries, string runId, int maxRounds = 12)
        {
            var recipes = new Dictionary<string, string>(); var resolved = new List<JournalEntry>();
            foreach (var e in entries)
            {
                if (e.RunId != runId || e.RoundId == null) continue;
                if (e.Kind == "OutcomeCommitted") recipes[e.RoundId] = e.Payload["recipeId"] as string;
                if (e.Kind == "Resolved") resolved.Add(e);
            }
            var result = new List<GardenMoment>(); var seen = new HashSet<string>(); int count = 0;
            for (int i = resolved.Count - 1; i >= 0 && count < maxRounds; i--)
            {
                var e = resolved[i]; if (!seen.Add(e.RoundId)) continue; count++;
                var state = WorldStateSerializer.FromPayload((Dictionary<string, object>)e.Payload["postState"]);
                var links = new List<EcologicalLink>(); var events = new List<GardenEvent>(); CareReport care = null;
                IEnumerable<EcologyEffect> effects = null;
                if (e.Payload.TryGetValue("ecology", out object eco) && eco is IDictionary<string, object> d)
                {
                    effects = ReadStarEffects(d);
                    if (d.TryGetValue("care", out object cr) && cr is IDictionary<string, object> cd) care = CareReport.FromPayload(cd);
                    if (d.TryGetValue("relations", out object relations) && relations is IEnumerable list)
                        foreach (IDictionary<string, object> item in list)
                            links.Add(new EcologicalLink { Kind = item["kind"] as string, Actor = item["actor"] as string, Species = item["species"] as string,
                                FromCell = WorldStateSerializer.ToInt(item["fromCell"]), ToCell = WorldStateSerializer.ToInt(item["toCell"]), Amount = WorldStateSerializer.ToInt(item["amount"]),
                                FromHabitat = item.TryGetValue("fromHabitat", out object fh) ? fh as string : null, ToHabitat = item.TryGetValue("toHabitat", out object th) ? th as string : null });
                }
                if (e.Payload.TryGetValue("events", out object recorded) && recorded is IEnumerable eventList)
                    foreach (IDictionary<string, object> item in eventList)
                        events.Add(new GardenEvent { Kind = item["kind"] as string, PlantId = item["plantId"] as string,
                            CellId = WorldStateSerializer.ToInt(item["cellId"]), Tick = WorldStateSerializer.ToInt(item["tick"]) });
                recipes.TryGetValue(e.RoundId, out string recipe);
                result.AddRange(Read(state, links, events, recipe ?? care?.RecipeId, e.RoundId, care,
                    e.Payload.TryGetValue("diceWaterInjected", out object dw) ? Convert.ToInt64(dw) : 0, effects));
            }
            return result;
        }
        public static GardenMoment First(IList<GardenMoment> moments, string habitat = null)
        {
            foreach (var m in moments) if (habitat == null || m.Habitat == habitat) return m;
            return null;
        }
        public static string Describe(IList<GardenMoment> moments, string habitat = null, int max = 4)
        {
            var sb = new StringBuilder("庭に起きたこと　" + (habitat ?? "全体") + "\n"); int shown = 0;
            var counts = new Dictionary<string, int>();
            foreach (var m in moments)
            {
                if (habitat != null && m.Habitat != habitat) continue;
                string key = m.RoundId ?? m.Revision.ToString(); counts.TryGetValue(key, out int n);
                if (n >= 2) continue; if (shown >= max) break;
                counts[key] = n + 1; shown++;
                sb.AppendLine("\n" + m.Caption); sb.AppendLine(m.Headline);
                if (m.Detail != null) sb.AppendLine(m.Detail);
            }
            if (shown == 0) sb.AppendLine(habitat == null ? "この庭には、まだ一投の出来事が残っていない。" : "この居場所の最近の抜粋は、まだない。");
            return sb.ToString();
        }
    }
}
