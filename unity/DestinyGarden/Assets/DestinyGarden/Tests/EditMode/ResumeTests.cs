using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    /// <summary>
    /// Resuming a round cut off at each saved stage (DATA_CONTRACT §3): the wish is kept, saved material and a
    /// saved outcome are interpreted without drawing again, and the run's history counts each roll once.
    /// </summary>
    public class ResumeTests
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

        private static (Memory journal, RollOutcome last, string kind, string value) PlayWithWishes(int rolls)
        {
            var journal = new Memory();
            var coord = new GardenCoordinator(journal, new FixedEntropy("resume"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            RollOutcome last = null; string kind = null, value = null;
            for (int i = 0; i < rolls; i++)
            {
                kind = "family";
                value = RecipeCatalog.ElementOf(coord.CurrentFaces.Faces[0]).ToString().ToLowerInvariant();
                coord.CommitIntent(kind, value);
                last = coord.Roll();
                if (i + 1 < rolls) coord.Prepare();
            }
            return (journal, last, kind, value);
        }

        /// <summary>The journal up to and including the last entry of the given kind in the final round.</summary>
        private static JournalReadResult CutAfter(Memory journal, string kind)
        {
            int idx = journal.Entries.FindLastIndex(e => e.Kind == kind);
            Assert.GreaterOrEqual(idx, 0, kind);
            return new JournalReadResult { ChainValid = true, Entries = journal.Entries.Take(idx + 1).ToList() };
        }

        private static string Garden(WorldState s) =>
            string.Join(";", s.Plants.OrderBy(p => p.CellId).Select(p => $"{p.CellId}:{p.Species}:{p.Growth}:{(int)p.Stage}:{p.BloomRolls}:{p.Pollinated}:{p.IdleRolls}"));

        [Test]
        public void AfterIntent_TheWishIsKept()
        {
            var (journal, _, kind, value) = PlayWithWishes(12);
            var resumed = new GardenCoordinator(new Memory(), new FixedEntropy("other"), new SequentialIds());
            resumed.Restore(CutAfter(journal, "IntentCommitted"));
            Assert.AreEqual(CoordinatorPhase.IntentCommitted, resumed.Phase);
            Assert.AreEqual(kind, resumed.LastIntentKind);
            Assert.AreEqual(value, resumed.LastIntentValue);
            var o = resumed.Roll();
            Assert.AreEqual(LuckySeven.IntentMatched(kind, value, o.FaceId, o.RecipeId), o.IntentMatched, "the resumed roll judges the wish that was made");
        }

        [TestCase("DrawEntropyCommitted")]
        [TestCase("OutcomeCommitted")]
        public void AfterSavedMaterial_TheSameRollIsResolved_WithoutDrawingAgain(string cut)
        {
            var (journal, original, _, _) = PlayWithWishes(12);
            var writer = new Memory();
            var resumed = new GardenCoordinator(writer, new FixedEntropy("must-not-be-used"), new SequentialIds());
            resumed.Restore(CutAfter(journal, cut));
            int historyBefore = resumed.RolledRecipes.Count;
            var o = resumed.Roll();
            Assert.NotNull(o);
            Assert.AreEqual(original.FaceId, o.FaceId);
            Assert.AreEqual(original.RecipeId, o.RecipeId);
            Assert.AreEqual(original.CardIndex, o.CardIndex);
            Assert.AreEqual(original.IntentMatched, o.IntentMatched);
            Assert.AreEqual(original.Ecology.PrayerStar.Points, o.Ecology.PrayerStar.Points);
            Assert.AreEqual(Garden(original.Ecology.PostState), Garden(o.Ecology.PostState), "same garden from the same saved material");
            Assert.IsFalse(writer.Entries.Any(e => e.Kind == "AcquisitionStarted" || e.Kind == "DrawEntropyCommitted"), "no new draw");
            Assert.AreEqual(cut == "OutcomeCommitted" ? 0 : 1, writer.Entries.Count(e => e.Kind == "OutcomeCommitted"));
            Assert.AreEqual(1, writer.Entries.Count(e => e.Kind == "Resolved"));
            Assert.AreEqual(historyBefore + 1, resumed.RolledRecipes.Count, "the roll joins the history once");
        }

        [Test]
        public void AfterAcquisitionStartedOnly_TheRoundStaysUnresolved()
        {
            var (journal, _, _, _) = PlayWithWishes(6);
            var writer = new Memory();
            var resumed = new GardenCoordinator(writer, new FixedEntropy("other"), new SequentialIds());
            resumed.Restore(CutAfter(journal, "AcquisitionStarted"));
            Assert.IsNull(resumed.Roll());
            Assert.AreEqual("InterruptedUnresolved", writer.Entries.Last().Kind);
            Assert.AreEqual(CoordinatorPhase.Ready, resumed.Phase);
            Assert.NotNull(resumed.Prepare(), "the next round prepares as usual");
        }

        [Test]
        public void NewVisitor_StartsWithoutThePreviousWish()
        {
            var journal = new Memory();
            var coord = new GardenCoordinator(journal, new FixedEntropy("visitor"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            coord.CommitIntent("face", "3");
            coord.Roll();
            coord.Prepare();
            coord.SwitchToNewVisitor();
            Assert.AreEqual("none", coord.LastIntentKind, "live switch");
            coord.Prepare();
            var resumed = new GardenCoordinator(new Memory(), new FixedEntropy("other"), new SequentialIds());
            resumed.Restore(new JournalReadResult { ChainValid = true, Entries = journal.Entries });
            Assert.AreEqual(coord.RunId, resumed.RunId);
            Assert.AreEqual("none", resumed.LastIntentKind, "restored into the new visitor's run");
            Assert.IsNull(resumed.LastIntentValue);
        }

        [Test]
        public void NewVisitor_AfterASavedDraw_ResolvesThatRoundFirst()
        {
            var (journal, _, _, _) = PlayWithWishes(5);
            var writer = new Memory();
            var resumed = new GardenCoordinator(writer, new FixedEntropy("other"), new SequentialIds());
            resumed.Restore(CutAfter(journal, "DrawEntropyCommitted"));
            resumed.SwitchToNewVisitor();
            var kinds = writer.Entries.Select(e => e.Kind).ToList();
            int resolved = kinds.IndexOf("Resolved"), closed = kinds.IndexOf("RunClosed");
            Assert.GreaterOrEqual(resolved, 0, "the saved draw is resolved");
            Assert.Greater(closed, resolved, "before the run closes");
            Assert.IsFalse(kinds.Take(closed).Contains("DrawEntropyCommitted"), "without drawing again");
        }

        [Test]
        public void FullJournal_HistoryCountsEachResolvedRollOnce()
        {
            var (journal, _, _, _) = PlayWithWishes(10);
            var resumed = new GardenCoordinator(new Memory(), new FixedEntropy("other"), new SequentialIds());
            resumed.Restore(new JournalReadResult { ChainValid = true, Entries = journal.Entries });
            Assert.AreEqual(journal.Entries.Count(e => e.Kind == "Resolved"), resumed.RolledRecipes.Count);
        }
    }
}
