using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DestinyGarden.Domain;

namespace DestinyGarden.Application
{
    public sealed class GardenMemoryLink
    {
        public string Key, Label;
        public int Revision, CellId;
        public bool Present;
    }
    public sealed class GardenMemory
    {
        public string RunId, Key, Species, ParentKey, ParentLabel;
        public bool IsSeed, Present, Returned;
        public int LatestRevision, LastSeenRevision, CellId, SeedCount, ChildCount;
        public GardenMoment Origin;
        public List<GardenMoment> Steps = new List<GardenMoment>();
        public List<GardenMemoryLink> Next = new List<GardenMemoryLink>();
        public string Label => (Flora.Get(Species)?.LabelJa ?? "名前の分からない草花") + (IsSeed ? "の種" : "の株");
        public GardenMoment Focus => Steps.Count > 0 ? Steps[Steps.Count - 1] : Origin;
        public string Describe()
        {
            var sb = new StringBuilder(Label + "の歩み\n");
            if (Origin != null) { sb.AppendLine("\n" + Origin.Caption); sb.AppendLine(Origin.Headline); }
            foreach (var step in Steps) { sb.AppendLine("\n" + step.Caption); sb.AppendLine(step.Headline); }
            sb.AppendLine();
            if (Present) sb.AppendLine(LatestRevision + "投目の庭にも、" + (IsSeed ? "この種は土の中に残っている。" : "この株は残っている。"));
            else if (Returned) sb.AppendLine("この株は、土へ還った。");
            else if (IsSeed && ChildCount > 0) sb.AppendLine("この種から育った株が、記録に残っている。");
            else sb.AppendLine("この" + (IsSeed ? "種" : "株") + "が残っていた最後の記録は、" + LastSeenRevision + "投目。");
            if (!IsSeed && SeedCount > 0) sb.AppendLine("この株の種が土に残った記録は、" + SeedCount + "粒。");
            if (!IsSeed && ChildCount > 0) sb.AppendLine("親として記録された子株は、" + ChildCount + "株。");
            if (Next.Count > 0)
            {
                sb.AppendLine("\n記録の続き");
                foreach (var next in Next) sb.AppendLine("・" + next.Revision + "投目の" + next.Label + (next.Present ? "（いまの庭にも残っている）" : ""));
            }
            return sb.ToString();
        }
    }
    /// <summary>Exact plant/seed identities from one verified run. Queried on demand; no world state is retained or changed.</summary>
    public static class GardenMemories
    {
        public const int MaxSteps = 8, MaxNext = 8;
        private sealed class Snapshot
        {
            public Dictionary<string, object> State, Payload;
            public string RoundId, Recipe;
            public int Revision;
        }
        private sealed class Step
        {
            public string Kind, Text;
            public int Revision, Cell, Tick;
            public Snapshot Snapshot;
        }
        private static string Text(IDictionary<string, object> d, string key) => d != null && d.TryGetValue(key, out object v) ? v as string : null;
        private static int Number(IDictionary<string, object> d, string key, int fallback = 0) => d != null && d.TryGetValue(key, out object v) ? WorldStateSerializer.ToInt(v) : fallback;
        private static IEnumerable<IDictionary<string, object>> Rows(IDictionary<string, object> d, string key)
        {
            if (d != null && d.TryGetValue(key, out object value) && value is IEnumerable list)
                foreach (var item in list) if (item is IDictionary<string, object> row) yield return row;
        }
        private static IDictionary<string, object> Succession(Snapshot s)
        {
            if (s.State.TryGetValue("ecosystem", out object e) && e is IDictionary<string, object> eco && eco.TryGetValue("succession", out object m)) return m as IDictionary<string, object>;
            return null;
        }
        private static Snapshot At(List<Snapshot> snapshots, int revision)
        { foreach (var s in snapshots) if (s.Revision == revision) return s; return null; }
        private static GardenMoment Moment(Step step, string species, string key)
        {
            string habitat = null;
            if (step.Snapshot != null && step.Snapshot.Revision == step.Revision && step.Cell >= 0 && step.Cell < GardenMapData.CellCount)
                habitat = LivingEcosystem.Habitat(WorldStateSerializer.FromPayload(step.Snapshot.State), step.Cell);
            return new GardenMoment { RoundId = step.Snapshot?.RoundId, RecipeId = step.Snapshot?.Recipe, Revision = step.Revision,
                Kind = step.Kind, CellId = step.Cell, FromCell = step.Cell, Species = species, Habitat = habitat, Headline = step.Text,
                MemoryKey = key };
        }
        public static GardenMemory Project(IList<JournalEntry> entries, string runId, string key)
        {
            if (key == null || key.Length <= 2 || (key.Substring(0, 2) != "p:" && key.Substring(0, 2) != "s:")) return null;
            bool seedQuery = key[0] == 's'; string id = key.Substring(2);
            var recipes = new Dictionary<string, string>(); var snapshots = new List<Snapshot>(); var seen = new HashSet<string>();
            foreach (var e in entries)
            {
                if (e.RunId != runId) continue;
                if (e.Kind == "OutcomeCommitted" && e.RoundId != null) recipes[e.RoundId] = Text(e.Payload, "recipeId");
                if (e.Kind == "RunStarted" && e.Payload.TryGetValue("initialState", out object initial) && initial is Dictionary<string, object> init)
                    snapshots.Add(new Snapshot { State = init, Revision = Number(init, "worldRevision") });
                if (e.Kind == "Resolved" && e.RoundId != null && seen.Add(e.RoundId))
                {
                    var state = (Dictionary<string, object>)e.Payload["postState"];
                    recipes.TryGetValue(e.RoundId, out string recipe);
                    snapshots.Add(new Snapshot { State = state, Payload = e.Payload, RoundId = e.RoundId, Revision = Number(state, "worldRevision"), Recipe = recipe });
                }
            }
            if (snapshots.Count == 0) return null;
            var memory = new GardenMemory { Key = key, RunId = runId, IsSeed = seedQuery, CellId = -1, LatestRevision = snapshots[snapshots.Count - 1].Revision };
            bool found = false; string parent = null, sourceSeed = null, carrier = null, originKind = null;
            int born = -1; Snapshot first = null; Step origin = null; bool? rested = null;
            var steps = new List<Step>(); var stepKeys = new HashSet<string>();
            void AddStep(Step step)
            {
                if (!stepKeys.Add(step.Revision + ":" + step.Kind)) return;
                steps.Add(step);
                steps.Sort((a, b) => a.Revision != b.Revision ? a.Revision.CompareTo(b.Revision) : a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : string.CompareOrdinal(a.Kind, b.Kind));
                if (steps.Count > MaxSteps) steps.RemoveAt(0);
            }
            foreach (var s in snapshots)
            {
                IDictionary<string, object> subject = null;
                foreach (var row in Rows(seedQuery ? Succession(s) : s.State, seedQuery ? "seedBank" : "plants"))
                    if (Text(row, seedQuery ? "seedId" : "plantId") == id) { subject = row; break; }
                memory.Present = subject != null;
                if (subject != null)
                {
                    found = true; first = first ?? s; memory.CellId = Number(subject, "cellId", -1); memory.LastSeenRevision = s.Revision;
                    memory.Species = Text(subject, "species") ?? memory.Species;
                    if (seedQuery)
                    {
                        parent = Text(subject, "parentPlantId"); carrier = Text(subject, "carrier"); born = Number(subject, "bornRevision", -1);
                        bool now = subject.TryGetValue("rested", out object r) && r is bool b && b;
                        if (rested == false && now) AddStep(new Step { Kind = "seed-rested", Revision = s.Revision, Cell = memory.CellId, Snapshot = s, Text = "休息の頃を越えた種として、土に残っていた。" });
                        rested = now;
                    }
                    else
                    { originKind = Text(subject, "originKind") ?? originKind; }
                }
                if (!seedQuery)
                {
                    var events = Rows(s.Payload, "events");
                    bool returnedHere = false;
                    foreach (var ev in events)
                        if (GardenMoments.IsPlantReturn(Text(ev, "kind")) && Number(ev, "cellId", -1) == memory.CellId)
                        { returnedHere = true; break; }
                    var ecology = s.Payload != null && s.Payload.TryGetValue("ecology", out object eco)
                        ? eco as IDictionary<string, object> : null;
                    int starGrowth = 0;
                    if (subject != null && !returnedHere && s.State.ContainsKey("ecosystem") && ecology != null)
                        foreach (var effect in GardenMoments.ReadStarEffects(ecology))
                            if (effect.CellId == memory.CellId && effect.Amount > 0) starGrowth = effect.Amount;
                    int finalGrowth = Number(subject, "growth");

                    foreach (var a in Rows(Succession(s), "ancestry"))
                        if (Text(a, "plantId") == id)
                        { parent = Text(a, "parentPlantId"); sourceSeed = Text(a, "seedId"); carrier = Text(a, "carrier"); born = Number(a, "bornRevision", -1); }
                    foreach (var ev in events)
                    {
                        if (Text(ev, "plantId") != id) continue;
                        string kind = Text(ev, "kind"); int cell = Number(ev, "cellId", -1);
                        if (kind == "SeedPlaced") { origin = new Step { Kind = kind, Revision = s.Revision, Cell = cell, Snapshot = s, Text = "ダイスが届けた種が、この土に入った。" }; continue; }
                        bool returned = GardenMoments.IsPlantReturn(kind);
                        string text = kind == "Germinated" ? "芽を出した。" : kind == "LeafOpened" ? "葉を開いた。" : kind == "BudFormed" ? "蕾ができた。"
                            : kind == "Bloomed" ? "花を開いた。" : returned ? "この株が、土へ還った。" : null;
                        if (text == null) continue;
                        found = true; first = first ?? s;
                        if (memory.CellId < 0) memory.CellId = cell;
                        if (returned) memory.Returned = true;
                        int tick = Number(ev, "tick");
                        if (cell == memory.CellId && GardenMoments.StarCrossedStage(kind, finalGrowth, starGrowth, tick))
                            text = "星の恵みで、" + text;
                        AddStep(new Step { Kind = kind, Revision = s.Revision, Cell = cell, Snapshot = s, Tick = tick, Text = text });
                    }
                    // Relations have no individual ID. Bind only to the recorded individual in this snapshot,
                    // and skip the retiring/replacement roll where cell and species alone are ambiguous.
                    if (subject != null && !returnedHere && ecology != null)
                        foreach (var link in Rows(ecology, "relations"))
                        {
                            if (Number(link, "toCell", -1) != memory.CellId || Text(link, "species") != memory.Species || Number(link, "amount") <= 0) continue;
                            string kind = Text(link, "kind"), actor = EcologyObservations.ResidentLabel(Text(link, "actor")) ?? "小さな命";
                            string text = kind == "care-nectar" ? "この投の光を受け、蜜を補った。" : kind == "pollinated" ? actor + "が、この株に花粉を届けた。"
                                : kind == "fruited" ? "次の種が実った。" : kind == "recovered" ? "弱りから持ち直した。" : kind == "flower-rest" ? "花を閉じ、葉の姿に戻った。" : null;
                            if (text != null) AddStep(new Step { Kind = kind, Revision = s.Revision, Cell = memory.CellId, Snapshot = s, Tick = 40, Text = text });
                        }
                }
            }
            if (!found) return null;
            bool parentKnown = false, seedKnown = false;
            var seedRefs = new Dictionary<string, GardenMemoryLink>(); var childRefs = new Dictionary<string, GardenMemoryLink>(); var childSeeds = new Dictionary<string, string>();
            var childIds = new HashSet<string>(); var germinationSteps = new List<Step>();
            foreach (var s in snapshots)
            {
                var plants = new Dictionary<string, IDictionary<string, object>>();
                foreach (var p in Rows(s.State, "plants")) { string pid = Text(p, "plantId"); if (pid != null) plants[pid] = p; if (pid == parent) parentKnown = true; }
                foreach (var seed in Rows(Succession(s), "seedBank"))
                {
                    string sid = Text(seed, "seedId"); if (sid == sourceSeed) seedKnown = true;
                    if (seedQuery || Text(seed, "parentPlantId") != id || sid == null) continue;
                    seedRefs["s:" + sid] = new GardenMemoryLink { Key = "s:" + sid, CellId = Number(seed, "cellId", -1), Revision = Number(seed, "bornRevision"),
                        Label = (Flora.Get(Text(seed, "species"))?.LabelJa ?? "草花") + "の種" };
                }
                foreach (var a in Rows(Succession(s), "ancestry"))
                {
                    bool child = seedQuery ? Text(a, "seedId") == id : Text(a, "parentPlantId") == id;
                    string childId = Text(a, "plantId"); if (!child || childId == null || !plants.TryGetValue(childId, out var p)) continue;
                    if (seedQuery && Number(p, "cellId", -1) != memory.CellId) continue;
                    if (!childIds.Add(childId)) continue;
                    int revision = Number(a, "bornRevision", s.Revision), cell = Number(p, "cellId", -1);
                    string label = Flora.Get(Text(p, "species"))?.LabelJa ?? "草花";
                    childRefs["p:" + childId] = new GardenMemoryLink { Key = "p:" + childId, CellId = cell, Revision = revision, Label = label + "の株" };
                    childSeeds["p:" + childId] = Text(a, "seedId");
                    if (seedQuery) germinationSteps.Add(new Step { Kind = "seed-child", Revision = revision, Cell = cell, Snapshot = At(snapshots, revision), Text = "この種から、" + label + "の芽が育った。" });
                }
            }
            memory.ParentKey = !seedQuery && sourceSeed != null && seedKnown ? "s:" + sourceSeed : parent != null && parent != id && parentKnown ? "p:" + parent : null;
            memory.ParentLabel = memory.ParentKey != null && memory.ParentKey.StartsWith("s:", StringComparison.Ordinal) ? "種の記録へ" : "親株の記録へ";
            memory.SeedCount = seedRefs.Count; memory.ChildCount = childIds.Count;
            var next = new List<GardenMemoryLink>(seedRefs.Values);
            foreach (var pair in childRefs)
                if (seedQuery || !childSeeds.TryGetValue(pair.Key, out string sid) || sid == null || !seedRefs.ContainsKey("s:" + sid)) next.Add(pair.Value);
            var present = new HashSet<string>(); var last = snapshots[snapshots.Count - 1];
            foreach (var p in Rows(last.State, "plants")) present.Add("p:" + Text(p, "plantId"));
            foreach (var s in Rows(Succession(last), "seedBank")) present.Add("s:" + Text(s, "seedId"));
            foreach (var item in next) item.Present = present.Contains(item.Key);
            next.Sort((a, b) => a.Present != b.Present ? b.Present.CompareTo(a.Present) : a.Revision != b.Revision ? b.Revision.CompareTo(a.Revision) : string.CompareOrdinal(a.Key, b.Key));
            memory.Next = next.GetRange(0, Math.Min(MaxNext, next.Count));
            if (seedQuery)
            {
                string text = carrier == Ecology.Bird ? "小鳥が運んだ種が、この土に残った。" : carrier == Ecology.Fluff ? "風に運ばれた種が、この土に残った。" : "この種が、土に残った。";
                origin = new Step { Kind = "seed-arrival", Revision = born >= 0 ? born : first.Revision, Cell = memory.CellId, Snapshot = At(snapshots, born), Text = text };
                germinationSteps.Sort((a, b) => a.Revision.CompareTo(b.Revision)); foreach (var step in germinationSteps) AddStep(step);
            }
            else if (origin == null)
            {
                bool knownBirth = born >= 0;
                string text = carrier == "rhizome" ? "親株の地下茎から、この株が根づいた。" : carrier == "stolon" ? "親株のつるから、この株が根づいた。"
                    : sourceSeed != null ? "土に残った種から、この株が芽を出した。" : first.Revision == 0 && originKind == "initialMap" ? "はじめの庭に、この株があった。" : "この株がある庭として、記録された。";
                origin = new Step { Kind = "plant-origin", Revision = knownBirth ? born : first.Revision, Cell = memory.CellId, Snapshot = knownBirth ? At(snapshots, born) : first, Text = text };
            }
            memory.Origin = Moment(origin, memory.Species, key);
            steps.Sort((a, b) => a.Revision != b.Revision ? a.Revision.CompareTo(b.Revision) : a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : string.CompareOrdinal(a.Kind, b.Kind));
            foreach (var step in steps) memory.Steps.Add(Moment(step, memory.Species, key));
            return memory;
        }
    }
}
