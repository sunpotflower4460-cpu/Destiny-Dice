using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    /// <summary>
    /// The 48-roll experience, measured from saved facts only (no presentation): does each roll have a visible
    /// answer near where its blessing went, how often is a roll quiet, does the headline speak of this roll, how the
    /// flowers come, how often luck shows, and whether praying makes a difference. Six gardens × 48 rolls × 3 ways.
    /// </summary>
    public class ExperienceMeasureTests
    {
        private sealed class Memory : IJournalWriter
        {
            public readonly List<JournalEntry> Entries = new List<JournalEntry>();
            public string HeadHash => "";
            public long NextSeq => Entries.Count + 1;
            public JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload)
            {
                var e = new JournalEntry { Seq = Entries.Count + 1, Kind = kind, RunId = runId, RoundId = roundId, Payload = payload };
                Entries.Add(e);
                return e;
            }
        }

        private static readonly HashSet<string> StageEvents = new HashSet<string> { "Germinated", "LeafOpened", "BudFormed", "Bloomed" };
        public const int Rolls = 48, Gardens = 6;

        private sealed class Window { public int Rolls, Answered, Quiet, Related, Signs, Big, SeventhOffered; }

        public sealed class Report
        {
            public string Text;
            public double AnsweredShare, QuietShare, RelatedShare, SignShare, SeventhShare, BigPerRolls, RainbowGardens;
            public double Flowers12, Flowers24, Flowers48, FirstBloom;
        }

        /// <summary>Plays the gardens one way ("none", "family", "lucky") and reports. Also used by later checks.</summary>
        public static Report Measure(string way, int gardens = Gardens, int rolls = Rolls, string seedPrefix = "experience")
        {
            var windows = new[] { new Window(), new Window(), new Window() };
            int rainbowGardens = 0;
            double f12 = 0, f24 = 0, f48 = 0, firstBloomSum = 0;
            var headlineKinds = new Dictionary<string, int>();
            int earnWish = 0, earnSeason = 0, earnSign = 0, rainbowSeven = 0, rainbowWish = 0;
            for (int g = 0; g < gardens; g++)
            {
                var coord = new GardenCoordinator(new Memory(), new FixedEntropy(seedPrefix + "-" + way + "-" + g), new SequentialIds());
                coord.CreateRun();
                coord.Prepare();
                bool rainbow = false;
                int firstBloom = rolls + 1;
                for (int i = 1; i <= rolls; i++)
                {
                    var w = windows[i <= 12 ? 0 : i <= 24 ? 1 : 2];
                    w.Rolls++;
                    if (coord.LuckySevenOffered) { w.SeventhOffered++; coord.AddLuckySeven(); }
                    var faces = coord.CurrentFaces.Faces;
                    int last = coord.RolledFaces.Count > 0 ? coord.RolledFaces[coord.RolledFaces.Count - 1] : 0;
                    int luckyFace = PrayerStar.FaceCompletingSeven(last);
                    string family = faces.GroupBy(f => RecipeCatalog.ElementOf(f)).OrderByDescending(x => x.Count()).ThenBy(x => (int)x.Key).First().Key.ToString().ToLowerInvariant();
                    if (way == "family" || (way == "lucky" && (luckyFace < 1 || luckyFace > faces.Length))) coord.CommitIntent("family", family);
                    else if (way == "lucky") coord.CommitIntent("face", luckyFace.ToString());
                    else coord.CommitIntent("none", null);
                    int anchor = CareTargets.Anchor(coord.CurrentFaces);
                    string roundId = coord.CurrentRoundId;
                    var o = coord.Roll();
                    coord.Prepare();

                    var eco = o.Ecology;
                    var star = eco.PrayerStar;
                    var fed = new HashSet<int>(eco.Effects.Where(e => (e.Kind == PrayerStar.GiftKind || e.Kind == Ecology.Spirit) && e.Amount > 0).Select(e => e.CellId));
                    var events = o.Simulation.Events.Concat(eco.Events).Where(e => StageEvents.Contains(e.Kind)).ToList();
                    bool answered = events.Any(e => PrayerStar.Dist(e.CellId, anchor) <= 3 || fed.Contains(e.CellId));
                    bool sign = star != null && (star.LuckyNumber || star.Zorome || star.Total77 || star.Points >= 8);
                    bool big = star != null && (star.Points >= 8 || star.Triple || star.Total77 || star.SevenLuckies);
                    if (answered) w.Answered++;
                    if (events.Count == 0 && !sign) w.Quiet++;
                    if (sign) w.Signs++;
                    if (big) w.Big++;
                    if (star != null && star.Points >= 8) { rainbow = true; if (star.SevenLuckies) rainbowSeven++; else rainbowWish++; }
                    if (LuckySeven.WishEarnsSeventh(o.IntentMatched, o.MatchingFaces, o.FaceCount)) earnWish++;
                    if (o.SeasonMetNow) earnSeason++;
                    if (star != null && star.EarnsSeventh) earnSign++;
                    var moment = GardenMoments.First(GardenMoments.FromOutcome(o, roundId));
                    if (moment != null)
                    {
                        headlineKinds.TryGetValue(moment.Kind, out int k); headlineKinds[moment.Kind] = k + 1;
                        if (PrayerStar.Dist(moment.CellId, anchor) <= 3 || fed.Contains(moment.CellId)) w.Related++;
                    }
                    int flowers = eco.PostState.Plants.Count(p => p.Stage == PlantStage.Flower);
                    if (flowers > 0 && firstBloom > rolls) firstBloom = i;
                    if (i == 12) f12 += flowers;
                    if (i == 24) f24 += flowers;
                    if (i == rolls) f48 += flowers;
                }
                if (rainbow) rainbowGardens++;
                firstBloomSum += firstBloom;
            }
            int all = windows.Sum(x => x.Rolls);
            var r = new Report
            {
                AnsweredShare = windows.Sum(x => x.Answered) / (double)all,
                QuietShare = windows.Sum(x => x.Quiet) / (double)all,
                RelatedShare = windows.Sum(x => x.Related) / (double)all,
                SignShare = windows.Sum(x => x.Signs) / (double)all,
                SeventhShare = windows.Sum(x => x.SeventhOffered) / (double)all,
                BigPerRolls = windows.Sum(x => x.Big) > 0 ? all / (double)windows.Sum(x => x.Big) : double.PositiveInfinity,
                RainbowGardens = rainbowGardens / (double)gardens,
                Flowers12 = f12 / gardens, Flowers24 = f24 / gardens, Flowers48 = f48 / gardens, FirstBloom = firstBloomSum / gardens
            };
            string Pct(int a, int b) => b == 0 ? "-" : (100.0 * a / b).ToString("0") + "%";
            var names = new[] { "1-12", "13-24", "25-48" };
            var parts = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var w = windows[i];
                parts.Add($"{names[i]}: answered {Pct(w.Answered, w.Rolls)} quiet {Pct(w.Quiet, w.Rolls)} headline-here {Pct(w.Related, w.Rolls)} signs {Pct(w.Signs, w.Rolls)} seventh {Pct(w.SeventhOffered, w.Rolls)}");
            }
            r.Text = $"[{way}] " + string.Join(" | ", parts) +
                     $" || all: answered {r.AnsweredShare:P0} quiet {r.QuietShare:P0} headline-here {r.RelatedShare:P0} signs {r.SignShare:P0} seventh {r.SeventhShare:P0}" +
                     $" big 1/{r.BigPerRolls:0.0} rainbow-gardens {r.RainbowGardens:P0} flowers@12/24/48 {r.Flowers12:0.0}/{r.Flowers24:0.0}/{r.Flowers48:0.0} first-bloom {r.FirstBloom:0.0}" +
 $" || seventh from wish {earnWish} season {earnSeason} sign {earnSign}; rainbows by seven-luckies {rainbowSeven} by-wish {rainbowWish}" +
                     " || headlines: " + string.Join(", ", headlineKinds.OrderByDescending(kv => kv.Value).Take(8).Select(kv => kv.Key + " " + kv.Value));
            return r;
        }

        [Test]
        public void Measure_The48RollExperience()
        {
            var lines = new List<string>();
            foreach (string way in new[] { "none", "family", "lucky" }) lines.Add(Measure(way).Text);
            string report = string.Join("\n", lines);
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log("DG-EXPERIENCE\n" + report);
        }
    }
}
