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
            CollectionAssert.AreEqual(original.Ecology.FinalPresence.Select(v => v.Kind + ":" + string.Join(",", v.Cells)),
                o.Ecology.FinalPresence.Select(v => v.Kind + ":" + string.Join(",", v.Cells)));
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
        public void AfterPreparationStartedOnly_TheRoundIsClosedAsInterrupted()
        {
            var (journal, _, _, _) = PlayWithWishes(4);
            // Real (unique) ids: the open round and the next one must be told apart.
            var coord = new GardenCoordinator(journal, new FixedEntropy("resume"), new DestinyGarden.Infrastructure.GuidIdSource());
            coord.Restore(new JournalReadResult { ChainValid = true, Entries = journal.Entries.ToList() });
            int cut = journal.Entries.Count;
            coord.Prepare(); // the next round's preparation, then the process ends before its mapping is saved
            var entries = journal.Entries.Take(journal.Entries.FindIndex(cut, e => e.Kind == "PreparationStarted") + 1).ToList();
            string openRound = entries.Last().RoundId;
            var writer = new Memory();
            var resumed = new GardenCoordinator(writer, new FixedEntropy("other"), new DestinyGarden.Infrastructure.GuidIdSource());
            resumed.Restore(new JournalReadResult { ChainValid = true, Entries = entries });
            Assert.NotNull(resumed.Prepare());
            Assert.AreEqual("PreparationInterrupted", writer.Entries[0].Kind);
            Assert.AreEqual(openRound, writer.Entries[0].RoundId, "the open round is closed, not left hanging");
            Assert.AreEqual("PreparationStarted", writer.Entries[1].Kind);
            Assert.AreNotEqual(openRound, resumed.CurrentRoundId);
        }

        [Test]
        public void AfterMappingSaved_ThePreparationContinues_WithTheSameFaces()
        {
            var (journal, _, _, _) = PlayWithWishes(5);
            var full = journal.Entries;
            int mapping = full.FindLastIndex(e => e.Kind == "MappingEntropyCommitted");
            var prepared = full.First(e => e.Kind == "Prepared" && e.RoundId == full[mapping].RoundId);
            var writer = new Memory();
            var resumed = new GardenCoordinator(writer, new FixedEntropy("must-not-be-used"), new SequentialIds());
            resumed.Restore(new JournalReadResult { ChainValid = true, Entries = full.Take(mapping + 1).ToList() });
            var faces = resumed.Prepare();
            Assert.NotNull(faces);
            Assert.AreEqual(prepared.RoundId, resumed.CurrentRoundId, "the same round continues");
            CollectionAssert.AreEqual(((IEnumerable<object>)prepared.Payload["faces"]).Cast<string>().ToArray(), faces.Faces,
                "the same faces from the saved mapping material");
            Assert.AreEqual(new[] { "Prepared" }, writer.Entries.Select(e => e.Kind).ToArray(), "no new entropy, no new round");
        }

        [Test]
        public void RunClosed_LeavesNothingOfThatRun()
        {
            var journal = new Memory();
            var coord = new GardenCoordinator(journal, new FixedEntropy("closed"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            coord.CommitIntent("none", null);
            coord.Roll();
            coord.Prepare();
            coord.SwitchToNewVisitor();
            int closed = journal.Entries.FindIndex(e => e.Kind == "RunClosed");
            Assert.AreEqual("AbandonedBeforeAcquisition", journal.Entries[closed - 1].Kind, "the published round is closed first");
            var resumed = new GardenCoordinator(new Memory(), new FixedEntropy("other"), new SequentialIds());
            resumed.Restore(new JournalReadResult { ChainValid = true, Entries = journal.Entries.Take(closed + 1).ToList() });
            Assert.IsNull(resumed.RunId, "the closed run does not continue");
            Assert.IsNull(resumed.State);
            Assert.AreEqual(CoordinatorPhase.NoRun, resumed.Phase);
        }

        [Test]
        public void FaceWish_CountsOnlyForAFaceOnThisDie()
        {
            foreach (var (value, expected) in new[] { ("3", 1), ("6", 1), ("7", 0), ("0", 0), ("３", 0), ("-1", 0), ("x", 0) })
            {
                var coord = new GardenCoordinator(new Memory(), new FixedEntropy("wish"), new SequentialIds());
                coord.CreateRun();
                coord.Prepare();
                coord.CommitIntent("face", value);
                Assert.AreEqual(expected, coord.MatchingFaces, value);
            }
        }

        private sealed class FailOnce : IJournalWriter
        {
            public readonly List<JournalEntry> Entries = new List<JournalEntry>();
            public string FailKind;
            public string HeadHash => "";
            public long NextSeq => Entries.Count + 1;
            public JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload)
            {
                if (kind == FailKind) { FailKind = null; throw new System.IO.IOException("disk full (test)"); }
                var e = new JournalEntry { Seq = Entries.Count + 1, Kind = kind, RunId = runId, RoundId = roundId, Payload = payload };
                Entries.Add(e);
                return e;
            }
        }

        [TestCase("OutcomeCommitted")]
        [TestCase("Resolved")]
        public void AFailedSave_ChangesNothing_AndTheRetryUsesTheSavedDraw(string failing)
        {
            var writer = new FailOnce();
            var coord = new GardenCoordinator(writer, new FixedEntropy("fail"), new SequentialIds());
            coord.CreateRun();
            for (int i = 0; i < 5; i++) { coord.Prepare(); coord.CommitIntent("none", null); coord.Roll(); }
            coord.Prepare();
            coord.CommitIntent("none", null);
            string before = Hashing.StateHash(coord.State);
            int history = coord.RolledRecipes.Count;
            writer.FailKind = failing;
            Assert.Throws<System.IO.IOException>(() => coord.Roll());
            Assert.AreEqual(CoordinatorPhase.IntentCommitted, coord.Phase, "the round is still open");
            Assert.AreEqual(before, Hashing.StateHash(coord.State), "the garden did not move");
            Assert.AreEqual(history, coord.RolledRecipes.Count);
            var o = coord.Roll();
            Assert.NotNull(o);
            string round = coord.RolledRecipes.Count > 0 ? writer.Entries.Last(e => e.Kind == "Resolved").RoundId : null;
            Assert.AreEqual(1, writer.Entries.Count(e => e.RoundId == round && e.Kind == "DrawEntropyCommitted"), "drawn once");
            Assert.AreEqual(1, writer.Entries.Count(e => e.RoundId == round && e.Kind == "OutcomeCommitted"));
            Assert.AreEqual(1, writer.Entries.Count(e => e.RoundId == round && e.Kind == "Resolved"));
            Assert.AreEqual(history + 1, coord.RolledRecipes.Count);
        }

        [Test]
        public void ResumedRoll_GivesThePlantsTheSameIds()
        {
            var (journal, original, _, _) = PlayWithWishes(12);
            var resumed = new GardenCoordinator(new Memory(), new FixedEntropy("other"), new SequentialIds());
            resumed.Restore(CutAfter(journal, "DrawEntropyCommitted"));
            var o = resumed.Roll();
            string Ids(WorldState s) => string.Join(",", s.Plants.OrderBy(p => p.CellId).Select(p => p.PlantId));
            Assert.AreEqual(Ids(original.Ecology.PostState), Ids(o.Ecology.PostState), "ids come from the saved round, not a new id source");
        }

        [TestCase("MappingEntropyCommitted")]
        [TestCase("Prepared")]
        [TestCase("DrawEntropyCommitted")]
        public void OldPreparedRoundsFinishWithTheirOwnEcologyBeforeTheNextRoundUpgrades(string cut)
        {
            var (journal, _, _, _) = PlayWithWishes(2);
            foreach (var entry in journal.Entries)
            {
                if (entry.Kind == "RunStarted") entry.Payload["ecologyPolicyVersion"] = CareRules.RulesVersion;
                else if (entry.Kind == "PreparationStarted" || entry.Kind == "Prepared")
                    entry.Payload.Remove("ecologyPolicyVersion");
            }
            var writer = new Memory();
            var resumed = new GardenCoordinator(writer, new FixedEntropy("old-prepared"), new SequentialIds());
            resumed.Restore(CutAfter(journal, cut));
            Assert.IsFalse(resumed.UsesConnectedHabitats);
            if (cut == "MappingEntropyCommitted") resumed.Prepare();
            if (resumed.Phase == CoordinatorPhase.Prepared) resumed.CommitIntent("none", null);
            var legacy = resumed.Roll();
            Assert.IsNull(legacy.Ecology.FinalPresence);
            Assert.AreEqual(CareRules.RulesVersion, Ecology.ToPayload(legacy.Ecology)["rulesVersion"]);

            resumed.Prepare();
            Assert.IsTrue(resumed.UsesConnectedHabitats);
            Assert.AreEqual(LivingEcosystem.ConnectedHabitatsVersion, writer.Entries.Last().Payload["ecologyPolicyVersion"]);
            resumed.CommitIntent("none", null);
            var next = resumed.Roll();
            Assert.IsNotNull(next.Ecology.FinalPresence);
            Assert.AreEqual(LivingEcosystem.ConnectedHabitatsVersion, Ecology.ToPayload(next.Ecology)["rulesVersion"]);
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
