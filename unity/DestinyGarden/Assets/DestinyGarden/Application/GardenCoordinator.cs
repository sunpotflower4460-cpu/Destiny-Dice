using System;
using System.Collections;
using System.Collections.Generic;
using DestinyGarden.Domain;

namespace DestinyGarden.Application
{
    public enum CoordinatorPhase { NoRun, Ready, Prepared, IntentCommitted, Resolved }

    public sealed class RollOutcome
    {
        public int FaceId; // 1..6, or 7 for the Lucky Seven
        public int FaceCount = 6;
        public bool LuckySeven; // the published 7th face was rolled
        public bool IntentMatched;
        public EcologyResult Ecology; // residents and what they did after the natural simulation
        public string RecipeId;
        public int CardIndex; // 0..5, -1 when unavailable
        public SimulationResult Simulation;
        public string StateHashAfter;
        public SelectedMessage Message;
        public WeatherDefinition Weather;
        public string SiteId;
        public string SeasonGoalId;     // season.v1: what the garden asks for this season
        public bool SeasonMet;          // met at some roll of this season
        public bool SeasonMetNow;       // met by this roll (brings the seventh face to the next round)
        public int SeasonRollsLeft;     // rolls left in the season after this one
        public string IntentKind = "none";   // the wish of this roll (none / face / family)
        public string IntentValue;
        public string PrayerText;
        public GardenMoment Moment;
        public int MatchingFaces;            // faces that would have answered it (its chance: MatchingFaces / FaceCount)
        public bool EarnedSeventh;           // lucky-seven.v2: this roll earned the seventh face (a long-shot wish or the season's wish)
        public int SeasonsMetCount;          // this run so far
        public int RainbowCount;             // this run so far
        public bool YearEnded;               // the fourth season ended with this roll (a year of 48 rolls)
    }

    /// <summary>
    /// Thin G1/G2 coordinator. Enforces the saved order:
    /// PreparationStarted -&gt; WeatherEntropyCommitted (once per period) -&gt;
    /// MappingEntropyCommitted -&gt; Prepared -&gt; IntentCommitted -&gt;
    /// AcquisitionStarted -&gt; DrawEntropyCommitted -&gt; OutcomeCommitted -&gt; Resolved.
    /// Entropy is always committed before it is interpreted; a prepared round is
    /// never regenerated after restart.
    /// </summary>
    public sealed class GardenCoordinator
    {
        private readonly IJournalWriter _writer;
        private readonly IEntropyProvider _entropy;
        private readonly IIdSource _ids;

        public string RunId { get; private set; }
        public WorldState State { get; private set; }
        public CoordinatorPhase Phase { get; private set; } = CoordinatorPhase.NoRun;
        public string CurrentRoundId { get; private set; }
        public WeatherDefinition CurrentWeather { get; private set; }
        public PreparedFaces CurrentFaces { get; private set; }
        public string PreferenceMode { get; private set; } = "auto";
        public string PreferenceSiteId { get; private set; }

        private readonly Dictionary<int, byte[]> _weatherEntropyByPeriod = new Dictionary<int, byte[]>();
        private readonly List<string> _recentMessageIds = new List<string>(); // newest last, max 3 kept for novelty
        public string LastIntentKind { get; private set; } = "none";
        public string LastIntentValue { get; private set; }
        public string LastPrayerText { get; private set; }

        // Lucky Seven: earned by a roll whose wish came true, offered on the next prepared round only.
        // lucky-seven.v2: seventh faces earned (a long-shot wish come true, a season's wish met), one offered per
        // round, at most two kept, so two earned in the same roll are not lost.
        private int _luckyBank;

        // A round whose preparation started but was never published (process ended): DATA_CONTRACT §3.
        private string _openRoundId, _openCareMode, _openCareSite;
        private int _openPeriod;
        private bool _openConnectedHabitats;
        private byte[] _openMapping;
        private void ClearOpenRound() { _openRoundId = null; _openMapping = null; _openCareMode = null; _openCareSite = null; _openConnectedHabitats = false; }
        private bool _pendingWishEarn;
        public int SeasonsMetCount { get; private set; }   // season.v2: seasons whose wish came true, this run
        public int RainbowCount { get; private set; }      // eight-pointed stars (rainbows) this run
        private string _restoreIntentKind, _restoreIntentValue;
        private readonly List<string> _rolledRecipes = new List<string>(); // oldest first, this run
        private readonly List<int> _rolledFaces = new List<int>();          // oldest first, this run (prayer star)
        private bool _prayerStarEnabled;
        // plant-ids.v1: new plants are named from their saved round (the same names when a roll is resumed). Runs begun
        // before it keep the id source, so their gardens continue exactly as they were (golden hashes).
        public const string PlantIdPolicyVersion = "plant-ids.v1";
        private bool _roundScopedIds;
        private bool _useConnectedHabitats;
        public IReadOnlyList<string> RolledRecipes => _rolledRecipes;
        public IReadOnlyList<int> RolledFaces => _rolledFaces;
        /// <summary>The run reads the prayer star (lucky numbers and signs count in this garden).</summary>
        public bool UsesPrayerStar => _prayerStarEnabled;
        public int MatchingFaces { get; private set; }  // faces that would answer the committed wish

        // season.v1: the season's goal, chosen at its first roll and saved with every Resolved.
        private int _seasonIndex = -1;
        public string SeasonGoalId { get; private set; }
        public bool SeasonMet { get; private set; }

        private void ResetSeason() { _seasonIndex = -1; SeasonGoalId = null; SeasonMet = false; }

        // A round restored after its draw or outcome was saved is resolved from what was saved (DATA_CONTRACT §3):
        // no new entropy, the same face. Faces and recipes join the run's history only once the round is Resolved.
        private bool _pendingAcquisition;
        private byte[] _pendingOutcomeBytes, _pendingOracleBytes, _pendingEcologyBytes;
        private bool _hasPendingOutcome;
        private int _pendingFaceId, _pendingCardIndex;
        private string _pendingRecipeId;

        /// <summary>A new run (a new visitor) starts without the previous visitor's wish.</summary>
        private void ResetIntent()
        {
            LastIntentKind = "none"; LastIntentValue = null; LastPrayerText = null; MatchingFaces = 0;
            _restoreIntentKind = null; _restoreIntentValue = null;
        }

        private void ClearPending()
        {
            _pendingAcquisition = false;
            _pendingOutcomeBytes = _pendingOracleBytes = _pendingEcologyBytes = null;
            _hasPendingOutcome = false;
            _pendingFaceId = 0; _pendingCardIndex = 0; _pendingRecipeId = null;
            _pendingWishEarn = false;
        }

        /// <summary>
        /// season.v2: the season of the next roll and its goal, readable before that roll (the same choice the roll
        /// will make: the goal of a new season is chosen from the garden as it is now).
        /// </summary>
        public (int index, string goalId) UpcomingSeason()
        {
            if (State == null) return (0, null);
            int idx = Seasons.IndexOf(State.WorldRevision + 1);
            if (idx == _seasonIndex && SeasonGoalId != null) return (idx, SeasonGoalId);
            return (idx, Seasons.Pick(idx, State, _rolledRecipes, CurrentWeather, UsesConnectedHabitats).Id);
        }

        /// <summary>Whether the upcoming season's goal is already met in this season.</summary>
        public bool UpcomingSeasonMet => State != null && Seasons.IndexOf(State.WorldRevision + 1) == _seasonIndex && SeasonMet;
        public bool LuckySevenOffered { get; private set; }
        public bool LuckySevenAdded => CurrentFaces != null && CurrentFaces.Faces.Length == LuckySeven.FaceId;
        public bool UsesConnectedHabitats => CurrentFaces?.ConnectedHabitats ??
            (_openRoundId != null ? _openConnectedHabitats : _useConnectedHabitats);

        private static bool HasConnectedHabitats(IDictionary<string, object> payload) =>
            payload.TryGetValue("ecologyPolicyVersion", out object version) &&
            version as string == LivingEcosystem.ConnectedHabitatsVersion;

        public GardenCoordinator(IJournalWriter writer, IEntropyProvider entropy, IIdSource ids)
        {
            _writer = writer; _entropy = entropy; _ids = ids;
        }

        public string CreateRun()
        {
            RunId = _ids.NewId("run");
            State = WorldState.CreateInitial();
            State.Ecosystem = EcosystemState.CreateCurrent();
            State.Ecosystem.BlessingFlora = true;
            _useConnectedHabitats = true;
            _prayerStarEnabled = true;
            _roundScopedIds = true;
            PreferenceMode = "auto"; PreferenceSiteId = null;
            _weatherEntropyByPeriod.Clear();
            _luckyBank = 0; LuckySevenOffered = false; SeasonsMetCount = 0; RainbowCount = 0;
            _rolledRecipes.Clear();
            _rolledFaces.Clear();
            ClearPending();
            ClearOpenRound();
            ResetIntent();
            ResetSeason();
            _writer.Append("RunStarted", RunId, null, new Dictionary<string, object>
            {
                ["gameId"] = "destiny-garden",
                ["ecologyPolicyVersion"] = LivingEcosystem.ConnectedHabitatsVersion,
                ["floraPolicyVersion"] = Flora.BlessingRulesVersion,
                ["prayerStarPolicyVersion"] = PrayerStar.RulesVersion,
                ["plantIdPolicyVersion"] = PlantIdPolicyVersion,
                ["runLifecycleVersion"] = RuleVersions.RunLifecycle,
                ["simulationProfile"] = RuleVersions.SimulationProfile,
                ["mapVersion"] = RuleVersions.Map,
                ["mapSha256"] = GardenMapData.SourceMapSha256,
                ["weatherPolicyVersion"] = RuleVersions.WeatherPolicy,
                ["targetPolicyVersion"] = CareRules.TargetVersion,
                ["mappingPolicyVersion"] = CareRules.MappingVersion,
                ["entropySource"] = _entropy.SourceName,
                ["defaultCareFocusMode"] = "auto",
                ["initialState"] = WorldStateSerializer.ToPayload(State),
                ["initialStateHash"] = Hashing.StateHash(State)
            });
            Phase = CoordinatorPhase.Ready;
            return RunId;
        }

        /// <summary>Records a next-round site preference. Never changes a published Prepared round.</summary>
        public void SetCareFocus(string mode, string siteId)
        {
            if (RunId == null) throw new InvalidOperationException("No run.");
            if (mode == "fixed") Sites.ById(siteId);
            else if (mode == "habitat")
            {
                if (!CareRules.Enabled(State) || Array.IndexOf(HabitatRules.Labels, siteId) < 0) throw new ArgumentException("Habitat focus is unavailable.");
            }
            else if (mode != "auto" || siteId != null) throw new ArgumentException("Unknown care focus.");
            if (mode == PreferenceMode && siteId == PreferenceSiteId) return;
            PreferenceMode = mode; PreferenceSiteId = siteId;
            _writer.Append("CareFocusChanged", RunId, null, new Dictionary<string, object>
            {
                ["mode"] = mode,
                ["siteId"] = siteId,
                ["targetPolicyVersion"] = CareRules.Enabled(State) ? CareRules.TargetVersion : RuleVersions.TargetPolicy
            });
        }

        public PreparedFaces Prepare()
        {
            if (Phase != CoordinatorPhase.Ready && Phase != CoordinatorPhase.Resolved)
                throw new InvalidOperationException("Prepare requires Ready/Resolved phase, was " + Phase);
            ClearPending();
            if (_openRoundId != null)
            {
                // Resumed: a preparation whose mapping material was saved continues from it (no new entropy);
                // one without it is closed as interrupted and a new round begins (DATA_CONTRACT §3).
                string open = _openRoundId, openMode = _openCareMode, openSite = _openCareSite;
                int openPeriod = _openPeriod;
                bool openConnectedHabitats = _openConnectedHabitats;
                byte[] openMapping = _openMapping;
                ClearOpenRound();
                if (openMapping != null && _weatherEntropyByPeriod.TryGetValue(openPeriod, out byte[] openWeather))
                {
                    CurrentRoundId = open;
                    CurrentWeather = Weathers.All[(int)WeatherSelection.Select(openWeather, openPeriod)];
                    return PublishFaces(open, openPeriod, Hashing.StateHash(State), openMapping, openMode, openSite, openConnectedHabitats);
                }
                _writer.Append("PreparationInterrupted", RunId, open, new Dictionary<string, object>
                {
                    ["reason"] = "process_ended_before_mapping"
                });
            }
            string roundId = _ids.NewId("round");
            CurrentRoundId = roundId;
            int periodIndex = State.WorldRevision / Weathers.RoundsPerEpisode;
            string beforeHash = Hashing.StateHash(State);
            bool connectedHabitats = _useConnectedHabitats;

            var preparation = new Dictionary<string, object>
            {
                ["beforeRevision"] = State.WorldRevision,
                ["beforeStateHash"] = beforeHash,
                ["weatherPeriodIndex"] = periodIndex,
                ["careFocusMode"] = PreferenceMode,
                ["careFocusSiteId"] = PreferenceSiteId
            };
            if (connectedHabitats) preparation["ecologyPolicyVersion"] = LivingEcosystem.ConnectedHabitatsVersion;
            _writer.Append("PreparationStarted", RunId, roundId, preparation);

            if (!_weatherEntropyByPeriod.TryGetValue(periodIndex, out byte[] weatherBytes))
            {
                weatherBytes = _entropy.GetBytes(32);
                _weatherEntropyByPeriod[periodIndex] = weatherBytes;
                _writer.Append("WeatherEntropyCommitted", RunId, null, new Dictionary<string, object>
                {
                    ["weatherPeriodIndex"] = periodIndex,
                    ["bytesBase64"] = Convert.ToBase64String(weatherBytes),
                    ["source"] = _entropy.SourceName,
                    ["initiatedByRoundId"] = roundId
                });
            }
            CurrentWeather = Weathers.All[(int)WeatherSelection.Select(weatherBytes, periodIndex)];

            byte[] mappingBytes = _entropy.GetBytes(256);
            _writer.Append("MappingEntropyCommitted", RunId, roundId, new Dictionary<string, object>
            {
                ["bytesBase64"] = Convert.ToBase64String(mappingBytes),
                ["source"] = _entropy.SourceName
            });
            return PublishFaces(roundId, periodIndex, beforeHash, mappingBytes, PreferenceMode, PreferenceSiteId, connectedHabitats);
        }

        /// <summary>Generates the faces from saved mapping material and publishes them (Prepared).</summary>
        private PreparedFaces PublishFaces(string roundId, int periodIndex, string beforeHash, byte[] mappingBytes,
            string careMode, string careSite, bool connectedHabitats)
        {
            var buffer = new EntropyBuffer(mappingBytes);
            var faces = FaceGenerator.Generate(State, CurrentWeather, careMode, careSite, buffer);
            if (faces == null)
            {
                _writer.Append("PreparationInterrupted", RunId, roundId, new Dictionary<string, object>
                {
                    ["reason"] = "mapping_entropy_exhausted"
                });
                CurrentRoundId = null;
                Phase = CoordinatorPhase.Ready;
                return null;
            }
            faces.ConnectedHabitats = connectedHabitats;
            CurrentFaces = faces;
            LuckySevenOffered = _luckyBank > 0;
            if (LuckySevenOffered) _luckyBank--;

            var faceList = new List<object>();
            foreach (string f in faces.Faces) faceList.Add(f);
            var preparedPayload = new Dictionary<string, object>
            {
                ["beforeRevision"] = State.WorldRevision,
                ["beforeStateHash"] = beforeHash,
                ["weatherId"] = CurrentWeather.Id.ToString(),
                ["weatherPeriodIndex"] = periodIndex,
                ["drawnSiteId"] = faces.DrawnSiteId,
                ["resolvedSiteId"] = faces.ResolvedSiteId,
                ["faces"] = faceList,
                ["mappingBytesConsumed"] = faces.MappingBytesConsumed,
                ["luckySevenOffered"] = LuckySevenOffered
            };
            if (CareRules.Enabled(State))
            {
                preparedPayload["anchorCellId"] = faces.AnchorCellId;
                if (faces.TargetNote != null) preparedPayload["targetNote"] = faces.TargetNote;
            }
            if (connectedHabitats) preparedPayload["ecologyPolicyVersion"] = LivingEcosystem.ConnectedHabitatsVersion;
            _writer.Append("Prepared", RunId, roundId, preparedPayload);
            Phase = CoordinatorPhase.Prepared;
            return faces;
        }

        /// <summary>
        /// Publishes the 7th face for this prepared round (before the intent and the roll).
        /// Returns the added recipe, or null when the chance is not available.
        /// </summary>
        public string AddLuckySeven()
        {
            if (Phase != CoordinatorPhase.Prepared || !LuckySevenOffered || LuckySevenAdded) return null;
            string recipeId = LuckySeven.PickRecipe(State, CurrentWeather, CareTargets.Anchor(CurrentFaces));
            if (recipeId == null) return null;
            _writer.Append("LuckySevenAdded", RunId, CurrentRoundId, new Dictionary<string, object>
            {
                ["faceId"] = LuckySeven.FaceId,
                ["recipeId"] = recipeId,
                ["amplification"] = LuckySeven.Amplification,
                ["rulesVersion"] = LuckySeven.RulesVersion
            });
            var faces = new List<string>(CurrentFaces.Faces) { recipeId };
            CurrentFaces.Faces = faces.ToArray();
            return recipeId;
        }

        public void CommitIntent(string kind, string value) => CommitIntent(kind, value, null);

        public void CommitIntent(string kind, string value, string prayerText)
        {
            if (Phase != CoordinatorPhase.Prepared)
                throw new InvalidOperationException("Intent requires Prepared phase.");
            kind = kind ?? "none";
            prayerText = RollRecord.NormalizePrayer(prayerText);
            int matchFaces = 0;
            if (kind == "family" && value != null)
                foreach (string f in CurrentFaces.Faces)
                    if (RecipeCatalog.ElementOf(f).ToString().ToLowerInvariant() == value) matchFaces++;
            // A face wish counts only for a face on this die (a 7 kept from a seven-face round cannot come true now).
            if (kind == "face" && value != null &&
                int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int wishFace) &&
                wishFace >= 1 && wishFace <= CurrentFaces.Faces.Length)
                matchFaces = 1;
            var payload = new Dictionary<string, object>
            {
                ["kind"] = kind,
                ["value"] = value,
                ["matchingFaces"] = matchFaces,
                ["baselineNumerator"] = matchFaces, // probability = matchingFaces / faceCount
                ["faceCount"] = CurrentFaces.Faces.Length
            };
            if (prayerText != null) payload["prayerText"] = prayerText;
            _writer.Append("IntentCommitted", RunId, CurrentRoundId, payload);
            LastIntentKind = kind;
            LastIntentValue = value;
            LastPrayerText = prayerText;
            MatchingFaces = matchFaces;
            Phase = CoordinatorPhase.IntentCommitted;
        }

        public RollOutcome Roll()
        {
            if (Phase != CoordinatorPhase.IntentCommitted)
                throw new InvalidOperationException("Roll requires a committed intent (use kind=none).");
            string roundId = CurrentRoundId;
            byte[] outcomeBytes, oracleBytes, ecologyBytes;
            if (_pendingOutcomeBytes != null)
            {
                // Resumed after the draw was saved: interpret the saved material, never draw again.
                outcomeBytes = _pendingOutcomeBytes; oracleBytes = _pendingOracleBytes; ecologyBytes = _pendingEcologyBytes;
            }
            else if (_pendingAcquisition)
            {
                // The process ended after acquisition started and before its material was saved.
                _writer.Append("InterruptedUnresolved", RunId, roundId, new Dictionary<string, object>
                {
                    ["reason"] = "acquisition_interrupted_before_material_saved"
                });
                ClearPending();
                Phase = CoordinatorPhase.Ready;
                CurrentRoundId = null; CurrentFaces = null;
                return null;
            }
            else
            {
                _writer.Append("AcquisitionStarted", RunId, roundId, new Dictionary<string, object>
                {
                    ["entropySource"] = _entropy.SourceName
                });
                _pendingAcquisition = true;
                outcomeBytes = _entropy.GetBytes(32);
                oracleBytes = _entropy.GetBytes(32);
                ecologyBytes = _entropy.GetBytes(64);
                _writer.Append("DrawEntropyCommitted", RunId, roundId, new Dictionary<string, object>
                {
                    ["outcomeBase64"] = Convert.ToBase64String(outcomeBytes),
                    ["oracleBase64"] = Convert.ToBase64String(oracleBytes),
                    ["ecologyBase64"] = Convert.ToBase64String(ecologyBytes),
                    ["source"] = _entropy.SourceName
                });
                // Saved: if anything below fails, a retry interprets this material instead of drawing again.
                _pendingOutcomeBytes = outcomeBytes; _pendingOracleBytes = oracleBytes; _pendingEcologyBytes = ecologyBytes;
            }

            int faceCount = CurrentFaces.Faces.Length;
            int faceId = faceCount == LuckySeven.FaceId
                ? LuckySeven.RollD7(outcomeBytes)
                : EntropyBuffer.RollD6(outcomeBytes, out _, out _);
            if (faceId < 0)
            {
                _writer.Append("InterruptedUnresolved", RunId, roundId, new Dictionary<string, object>
                {
                    ["reason"] = "outcome_entropy_exhausted"
                });
                ClearPending();
                Phase = CoordinatorPhase.Ready;
                CurrentRoundId = null; CurrentFaces = null;
                return null;
            }
            int cardRoll = EntropyBuffer.RollD6(oracleBytes, out _, out _);
            int cardIndex = cardRoll < 0 ? -1 : cardRoll - 1;
            if (_hasPendingOutcome)
            {
                // The saved outcome is the record; the same bytes give the same face, which is checked here.
                if (_pendingFaceId != faceId || _pendingRecipeId != CurrentFaces.Faces[faceId - 1])
                    throw new InvalidOperationException("Saved outcome does not match its saved material.");
                cardIndex = _pendingCardIndex;
            }
            string recipeId = CurrentFaces.Faces[faceId - 1];
            bool luckyRolled = faceCount == LuckySeven.FaceId && faceId == LuckySeven.FaceId;

            if (!_hasPendingOutcome)
            {
                _writer.Append("OutcomeCommitted", RunId, roundId, new Dictionary<string, object>
                {
                    ["faceId"] = faceId,
                    ["recipeId"] = recipeId,
                    ["cardIndex"] = cardIndex,
                    ["cardStatus"] = cardIndex < 0 ? "unavailable_entropy_exhausted" : "drawn",
                    ["faceCount"] = faceCount,
                    ["luckySeven"] = luckyRolled
                });
                _hasPendingOutcome = true; _pendingFaceId = faceId; _pendingRecipeId = recipeId; _pendingCardIndex = cardIndex;
            }

            int anchor = CareTargets.Anchor(CurrentFaces);
            var resolvedRecipe = luckyRolled
                ? LuckySeven.ResolveAmplified(recipeId, anchor, State)
                : RecipeResolver.Resolve(recipeId, anchor, State);
            int seedCounter = 0;
            var sim = Simulation.Resolve(State, resolvedRecipe, CurrentWeather,
                n => _roundScopedIds ? roundId + "-s" + (seedCounter++) : _ids.NewId("plant") + "-" + (seedCounter++), roundId);
            if (sim.WaterBalanceResidual != 0)
                throw new InvalidOperationException("Water balance residual must be 0, was " + sim.WaterBalanceResidual);

            string cardId = cardIndex >= 0 ? OracleCatalogData.CardOrder[cardIndex] : null;
            var message = Oracle.Select(roundId, sim, resolvedRecipe, cardId, RecentThree());

            // Ecology step (ECOLOGY.md): after the natural simulation, from saved bytes only.
            // The run's history takes this roll only after Resolved is saved (below).
            bool matched = LuckySeven.IntentMatched(LastIntentKind, LastIntentValue, faceId, recipeId);
            var recent = new List<string>(_rolledRecipes) { recipeId };
            int previousFace = _rolledFaces.Count > 0 ? _rolledFaces[_rolledFaces.Count - 1] : 0;
            var eco = DestinyGarden.Domain.Ecology.Apply(sim.PostState, new EcologyContext
            {
                RoundId = roundId,
                CurrentRecipeId = recipeId,
                RecentRecipeIds = recent,
                PrayerAnswered = matched,
                Sim = sim,
                Dice = resolvedRecipe,
                Weather = CurrentWeather,
                ConnectedHabitats = CurrentFaces.ConnectedHabitats,
                FaceId = _prayerStarEnabled ? faceId : 0,
                PreviousFaceId = previousFace,
                AnchorCellId = anchor,
                LuckyCountBefore = PrayerStar.CountLuckies(_rolledFaces),
                LuckyStreakBefore = PrayerStar.TrailingStreak(_rolledFaces),
                SameRunBefore = PrayerStar.SameRun(_rolledFaces),
                FaceTotalBefore = PrayerStar.Total(_rolledFaces),
                NoWish = LastIntentKind == null || LastIntentKind == "none" || LastIntentValue == null,
                SpiritCount = matched ? Math.Min(PrayerSpirits.MaxSpirits,
                    (faceCount + Math.Max(1, MatchingFaces) - 1) / Math.Max(1, MatchingFaces)) : 0
            }, ecologyBytes, n => _roundScopedIds ? roundId + "-e" + n : _ids.NewId("plant") + "-eco" + n);

            // season.v1: a new season's goal is chosen from the garden before its first roll; meeting it in the
            // season (once) brings the seventh face to the next round.
            // (Computed here, committed to the coordinator only after Resolved is saved.)
            int revision = eco.PostState.WorldRevision;
            int season = Seasons.IndexOf(revision);
            bool newSeason = season != _seasonIndex;
            string seasonGoal = newSeason ? Seasons.Pick(season, State, _rolledRecipes, CurrentWeather, UsesConnectedHabitats).Id : SeasonGoalId;
            bool metBefore = !newSeason && SeasonMet;
            // The communities of season.v2's goals are read from the garden itself, so they count in gardens with
            // the living ecosystem too (which has its own community rules and leaves eco.Communities empty).
            var seasonCommunities = DestinyGarden.Domain.Communities.Find(eco.PostState);
            bool butterflies = (eco.FinalPresence ?? eco.Visits).Exists(visit => visit.Kind == DestinyGarden.Domain.Ecology.Butterfly);
            bool seasonMetNow = !metBefore && Seasons.IsMet(seasonGoal, eco.PostState, seasonCommunities,
                eco.Harmony != null ? eco.Harmony.Value : 0, butterflies);
            bool seasonMet = metBefore || seasonMetNow;
            var ecologyPayload = DestinyGarden.Domain.Ecology.ToPayload(eco);
            ecologyPayload["season"] = new Dictionary<string, object>
            {
                ["rulesVersion"] = Seasons.RulesVersion,
                ["index"] = (long)season,
                ["goal"] = seasonGoal,
                ["met"] = seasonMet,
                ["metNow"] = seasonMetNow
            };

            var events = new List<object>();
            foreach (var e in sim.Events)
            {
                events.Add(new Dictionary<string, object>
                {
                    ["kind"] = e.Kind, ["plantId"] = e.PlantId, ["cellId"] = e.CellId, ["tick"] = e.Tick
                });
            }
            foreach (var e in eco.Events)
            {
                events.Add(new Dictionary<string, object>
                {
                    ["kind"] = e.Kind, ["plantId"] = e.PlantId, ["cellId"] = e.CellId, ["tick"] = e.Tick
                });
            }
            var moment = GardenMoments.First(GardenMoments.FromResolved(sim, eco, recipeId, roundId));
            var post = eco.PostState;
            string afterHash = Hashing.StateHash(post);
            _writer.Append("Resolved", RunId, roundId, new Dictionary<string, object>
            {
                ["worldRevision"] = post.WorldRevision,
                ["postState"] = WorldStateSerializer.ToPayload(post),
                ["postStateHash"] = afterHash,
                ["events"] = events,
                ["diceWaterInjected"] = sim.DiceWaterInjected,
                ["weatherWaterInjected"] = sim.WeatherWaterInjected,
                ["discharged"] = sim.Discharged,
                ["evaporated"] = sim.Evaporated,
                ["weatherId"] = CurrentWeather.Id.ToString(),
                ["ecology"] = ecologyPayload,
                ["momentHeadline"] = moment?.Headline,
                ["momentCellId"] = moment?.CellId ?? -1,
                ["selectedMessage"] = new Dictionary<string, object>
                {
                    ["templateId"] = message.TemplateId,
                    ["text"] = message.Text,
                    ["cardId"] = message.CardId,
                    ["focusGroupId"] = message.FocusGroupId,
                    ["secondaryGroupId"] = message.SecondaryGroupId,
                    ["focusCellId"] = message.FocusCellId,
                    ["fallbackReason"] = message.FallbackReason
                }
            });

            // Saved: only now does the coordinator move on.
            State = post;
            ClearPending();
            _seasonIndex = season; SeasonGoalId = seasonGoal; SeasonMet = seasonMet;
            _rolledRecipes.Add(recipeId);
            _rolledFaces.Add(faceId);
            _recentMessageIds.Add(message.TemplateId);
            while (_recentMessageIds.Count > 3) _recentMessageIds.RemoveAt(0);
            Phase = CoordinatorPhase.Resolved;
            bool wishEarned = LuckySeven.WishEarnsSeventh(matched, MatchingFaces, faceCount) &&
                              (eco.PrayerStar == null || LuckySeven.FamilyWishEarns(PrayerStar.RulesVersion) || LastIntentKind == "face");
            if (wishEarned) _luckyBank++;
            if (seasonMetNow) { _luckyBank++; SeasonsMetCount++; }
            bool signEarned = eco.PrayerStar != null && eco.PrayerStar.EarnsSeventh; // prayer-star.v5: 三つぞろい, total 77
            if (signEarned) _luckyBank++;
            _luckyBank = Math.Min(LuckySeven.MaxBanked, _luckyBank);
            if (eco.PrayerStar != null && eco.PrayerStar.Points >= 8) RainbowCount++;
            return new RollOutcome
            {
                FaceId = faceId, FaceCount = faceCount, LuckySeven = luckyRolled, IntentMatched = matched,
                SeasonGoalId = SeasonGoalId, SeasonMet = SeasonMet, SeasonMetNow = seasonMetNow,
                SeasonRollsLeft = Seasons.RollsLeft(revision),
                IntentKind = LastIntentKind, IntentValue = LastIntentValue, PrayerText = LastPrayerText,
                MatchingFaces = MatchingFaces, Moment = moment,
                EarnedSeventh = wishEarned || seasonMetNow || signEarned,
                SeasonsMetCount = SeasonsMetCount, RainbowCount = RainbowCount,
                YearEnded = Seasons.RollsLeft(revision) == 0 && (season + 1) % 4 == 0,
                Ecology = eco,
                RecipeId = recipeId, CardIndex = cardIndex,
                Simulation = sim, StateHashAfter = afterHash,
                Message = message, Weather = CurrentWeather, SiteId = CurrentFaces.ResolvedSiteId
            };
        }

        private List<string> RecentThree() => new List<string>(_recentMessageIds);

        /// <summary>Closes the current run and starts a fresh garden for the next visitor.</summary>
        public string SwitchToNewVisitor()
        {
            // A round whose draw was already started or saved is finished first (DATA_CONTRACT §3): resolved from
            // the saved material, or recorded as unresolved. A known outcome is never dropped for the switch.
            if (Phase == CoordinatorPhase.IntentCommitted && (_pendingOutcomeBytes != null || _pendingAcquisition))
                Roll();
            // A preparation resumed from an earlier session but never published is closed as interrupted.
            if (RunId != null && _openRoundId != null)
            {
                _writer.Append("PreparationInterrupted", RunId, _openRoundId, new Dictionary<string, object>
                {
                    ["reason"] = "visitor_switch"
                });
                ClearOpenRound();
            }
            // A published round that never began acquisition is closed as abandoned (DATA_CONTRACT §3).
            if (RunId != null && CurrentRoundId != null &&
                (Phase == CoordinatorPhase.Prepared || Phase == CoordinatorPhase.IntentCommitted))
                _writer.Append("AbandonedBeforeAcquisition", RunId, CurrentRoundId, new Dictionary<string, object>
                {
                    ["reason"] = "visitor_switch"
                });
            if (RunId != null)
            {
                _writer.Append("RunClosed", RunId, null, new Dictionary<string, object>
                {
                    ["reason"] = "visitor_switch",
                    ["finalWorldRevision"] = State != null ? State.WorldRevision : 0
                });
            }
            _recentMessageIds.Clear();
            return CreateRun();
        }

        /// <summary>Rebuilds coordinator state from verified journal entries (same-run resume).</summary>
        public void Restore(JournalReadResult read)
        {
            if (!read.ChainValid) throw new InvalidOperationException("Journal chain invalid: " + read.Error);
            RunId = null; State = null; CurrentFaces = null; CurrentRoundId = null;
            Phase = CoordinatorPhase.NoRun;
            _prayerStarEnabled = false;
            _roundScopedIds = false;
            _useConnectedHabitats = false;
            _luckyBank = 0; LuckySevenOffered = false; SeasonsMetCount = 0; RainbowCount = 0;
            _rolledRecipes.Clear();
            _rolledFaces.Clear();
            ClearPending();
            ClearOpenRound();
            ResetSeason();
            _weatherEntropyByPeriod.Clear();
            _recentMessageIds.Clear();

            // Only the last saved garden is the garden now: it is rebuilt once, after the loop (rebuilding every
            // roll's saved garden made resuming a long run slow).
            Dictionary<string, object> lastPost = null;
            foreach (var e in read.Entries)
            {
                switch (e.Kind)
                {
                    case "RunStarted":
                        lastPost = null;
                        _weatherEntropyByPeriod.Clear();
                        _recentMessageIds.Clear();
                        RunId = e.RunId;
                        State = WorldStateSerializer.FromPayload(
                            (Dictionary<string, object>)e.Payload["initialState"]);
                        // v8 gardens adopt the new ecology at their next preparation.
                        // Restored in-progress rounds read the policy recorded with their own preparation.
                        _useConnectedHabitats = HasConnectedHabitats(e.Payload) || CareRules.Enabled(State) &&
                            e.Payload.TryGetValue("ecologyPolicyVersion", out object ecologyPolicy) &&
                            ecologyPolicy as string == CareRules.RulesVersion;
                        // A run begun with any version of the prayer star keeps it (it continues under the current rules).
                        _prayerStarEnabled = e.Payload.TryGetValue("prayerStarPolicyVersion", out object starPolicy) &&
                            starPolicy is string sp && sp.StartsWith("prayer-star.", StringComparison.Ordinal);
                        _roundScopedIds = e.Payload.TryGetValue("plantIdPolicyVersion", out object pidp) && pidp as string == PlantIdPolicyVersion;
                        PreferenceMode = "auto"; PreferenceSiteId = null;
                        _luckyBank = 0; LuckySevenOffered = false; SeasonsMetCount = 0; RainbowCount = 0;
                        _rolledRecipes.Clear();
                        _rolledFaces.Clear();
                        ClearPending();
                        ClearOpenRound();
                        ResetIntent();
                        ResetSeason();
                        Phase = CoordinatorPhase.Ready;
                        break;
                    case "PreparationStarted":
                        _openRoundId = e.RoundId;
                        _openMapping = null;
                        _openCareMode = e.Payload.TryGetValue("careFocusMode", out object ocm) ? ocm as string : "auto";
                        _openCareSite = e.Payload.TryGetValue("careFocusSiteId", out object ocs) ? ocs as string : null;
                        _openPeriod = e.Payload.TryGetValue("weatherPeriodIndex", out object opi) ? WorldStateSerializer.ToInt(opi) : 0;
                        _openConnectedHabitats = HasConnectedHabitats(e.Payload);
                        break;
                    case "MappingEntropyCommitted":
                        if (e.RoundId != null && e.RoundId == _openRoundId)
                            _openMapping = Convert.FromBase64String((string)e.Payload["bytesBase64"]);
                        break;
                    case "RunClosed":
                        // The visitor's run is over: nothing of it continues (a new run follows, or will be created).
                        ClearPending(); ClearOpenRound(); ResetIntent(); ResetSeason();
                        lastPost = null;
                        RunId = null; State = null; CurrentFaces = null; CurrentRoundId = null;
                        _useConnectedHabitats = false;
                        Phase = CoordinatorPhase.NoRun;
                        break;
                    case "CareFocusChanged":
                        PreferenceMode = (string)e.Payload["mode"];
                        PreferenceSiteId = e.Payload["siteId"] as string;
                        break;
                    case "WeatherEntropyCommitted":
                        _weatherEntropyByPeriod[WorldStateSerializer.ToInt(e.Payload["weatherPeriodIndex"])] =
                            Convert.FromBase64String((string)e.Payload["bytesBase64"]);
                        break;
                    case "Prepared":
                        {
                            CurrentRoundId = e.RoundId;
                            int period = WorldStateSerializer.ToInt(e.Payload["weatherPeriodIndex"]);
                            if (_weatherEntropyByPeriod.TryGetValue(period, out byte[] wb))
                                CurrentWeather = Weathers.All[(int)WeatherSelection.Select(wb, period)];
                            var faces = new List<string>();
                            foreach (var f in (IEnumerable)e.Payload["faces"]) faces.Add((string)f);
                            CurrentFaces = new PreparedFaces
                            {
                                DrawnSiteId = (string)e.Payload["drawnSiteId"],
                                ResolvedSiteId = (string)e.Payload["resolvedSiteId"],
                                AnchorCellId = e.Payload.TryGetValue("anchorCellId", out object ac) ? WorldStateSerializer.ToInt(ac) : -1,
                                TargetNote = e.Payload.TryGetValue("targetNote", out object tn) ? tn as string : null,
                                ConnectedHabitats = HasConnectedHabitats(e.Payload),
                                Faces = faces.ToArray()
                            };
                            LuckySevenOffered = e.Payload.TryGetValue("luckySevenOffered", out object lo) && lo is bool lb && lb;
                            if (LuckySevenOffered && _luckyBank > 0) _luckyBank--;
                            ClearOpenRound();
                            ClearPending();
                            Phase = CoordinatorPhase.Prepared;
                            break;
                        }
                    case "LuckySevenAdded":
                        if (CurrentFaces != null && CurrentFaces.Faces.Length == 6)
                        {
                            var faces7 = new List<string>(CurrentFaces.Faces) { (string)e.Payload["recipeId"] };
                            CurrentFaces.Faces = faces7.ToArray();
                        }
                        break;
                    case "IntentCommitted":
                        _restoreIntentKind = e.Payload["kind"] as string;
                        _restoreIntentValue = e.Payload["value"] as string;
                        // The wish belongs to the round: a resumed roll must judge it as it was made.
                        LastIntentKind = _restoreIntentKind ?? "none";
                        LastIntentValue = _restoreIntentValue;
                        LastPrayerText = e.Payload.TryGetValue("prayerText", out object prayer) ? prayer as string : null;
                        MatchingFaces = e.Payload.TryGetValue("matchingFaces", out object mfv) ? WorldStateSerializer.ToInt(mfv) : 0;
                        if (Phase == CoordinatorPhase.Prepared) Phase = CoordinatorPhase.IntentCommitted;
                        break;
                    case "AcquisitionStarted":
                        _pendingAcquisition = true;
                        break;
                    case "DrawEntropyCommitted":
                        _pendingOutcomeBytes = Convert.FromBase64String((string)e.Payload["outcomeBase64"]);
                        _pendingOracleBytes = Convert.FromBase64String((string)e.Payload["oracleBase64"]);
                        _pendingEcologyBytes = e.Payload.TryGetValue("ecologyBase64", out object eb) && eb is string ebs
                            ? Convert.FromBase64String(ebs) : new byte[0];
                        break;
                    case "OutcomeCommitted":
                        _hasPendingOutcome = true;
                        _pendingFaceId = WorldStateSerializer.ToInt(e.Payload["faceId"]);
                        _pendingRecipeId = e.Payload["recipeId"] as string;
                        _pendingCardIndex = e.Payload.TryGetValue("cardIndex", out object ci) ? WorldStateSerializer.ToInt(ci) : -1;
                        _pendingWishEarn = LuckySeven.WishEarnsSeventh(
                            LuckySeven.IntentMatched(_restoreIntentKind, _restoreIntentValue,
                                WorldStateSerializer.ToInt(e.Payload["faceId"]), e.Payload["recipeId"] as string),
                            MatchingFaces, e.Payload.TryGetValue("faceCount", out object fcv) ? WorldStateSerializer.ToInt(fcv) : 6);
                        break;
                    case "Resolved":
                        if (_hasPendingOutcome && _pendingRecipeId != null)
                        {
                            _rolledRecipes.Add(_pendingRecipeId);
                            _rolledFaces.Add(_pendingFaceId);
                        }
                        // lucky-seven.v3: in a prayer-star.v6 roll only a face wish earns the seventh face.
                        string starVersion = e.Payload.TryGetValue("ecology", out object ecoRec0) && ecoRec0 is IDictionary<string, object> ecoRec0D &&
                            ecoRec0D.TryGetValue("prayerStar", out object psRec0) && psRec0 is IDictionary<string, object> psd0
                            ? psd0["rulesVersion"] as string : null;
                        if (_pendingWishEarn && (starVersion == null || LuckySeven.FamilyWishEarns(starVersion) || _restoreIntentKind == "face"))
                            _luckyBank++;
                        _pendingWishEarn = false;
                        ClearPending();
                        lastPost = (Dictionary<string, object>)e.Payload["postState"];
                        if (e.Payload.TryGetValue("ecology", out object ecoRec) && ecoRec is IDictionary<string, object> ecoRecD &&
                            ecoRecD.TryGetValue("season", out object seasonRec) && seasonRec is IDictionary<string, object> sd)
                        {
                            _seasonIndex = WorldStateSerializer.ToInt(sd["index"]);
                            SeasonGoalId = sd["goal"] as string;
                            SeasonMet = sd["met"] is bool smb && smb;
                            if (sd["metNow"] is bool mnb && mnb) { _luckyBank++; SeasonsMetCount++; }
                        }
                        _luckyBank = Math.Min(LuckySeven.MaxBanked, _luckyBank);
                        if (e.Payload.TryGetValue("ecology", out object ecoRec2) && ecoRec2 is IDictionary<string, object> ecoRec2D &&
                            ecoRec2D.TryGetValue("prayerStar", out object psRec) && psRec is IDictionary<string, object> psd &&
                            WorldStateSerializer.ToInt(psd["points"]) >= 8)
                            RainbowCount++;
                        if (e.Payload.TryGetValue("ecology", out object ecoRec3) && ecoRec3 is IDictionary<string, object> ecoRec3D &&
                            ecoRec3D.TryGetValue("prayerStar", out object psRec3) && psRec3 is IDictionary<string, object> psd3 &&
                            ((psd3.TryGetValue("triple", out object tr) && tr is bool trb && trb) ||
                             (PrayerStar.VersionOf(psd3["rulesVersion"] as string) < 6 &&
                              psd3.TryGetValue("total77", out object t77) && t77 is bool t77b && t77b)))
                            _luckyBank = Math.Min(LuckySeven.MaxBanked, _luckyBank + 1); // prayer-star.v5 (v6: the triple only)
                        // The earlier prayer-star branch recorded its policy with each outcome.
                        if (!lastPost.ContainsKey("ecosystem") && e.Payload.TryGetValue("ecology", out object ecology) &&
                            ecology is IDictionary<string, object> observed && observed.ContainsKey("prayerStar"))
                            _prayerStarEnabled = true;
                        if (e.Payload.TryGetValue("selectedMessage", out object sm) &&
                            sm is Dictionary<string, object> smd && smd["templateId"] is string tid)
                        {
                            _recentMessageIds.Add(tid);
                            while (_recentMessageIds.Count > 3) _recentMessageIds.RemoveAt(0);
                        }
                        CurrentFaces = null; CurrentRoundId = null;
                        Phase = CoordinatorPhase.Resolved;
                        break;
                    case "PreparationInterrupted":
                    case "InterruptedUnresolved":
                    case "AbandonedBeforeAcquisition":
                        ClearPending();
                        if (e.RoundId == _openRoundId) ClearOpenRound();
                        CurrentFaces = null; CurrentRoundId = null;
                        Phase = CoordinatorPhase.Ready;
                        break;
                }
            }
            if (lastPost != null) State = WorldStateSerializer.FromPayload(lastPost);
        }
    }
}
