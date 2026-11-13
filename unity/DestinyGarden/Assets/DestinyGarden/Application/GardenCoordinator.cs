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

        // Lucky Seven: earned by a roll whose wish came true, offered on the next prepared round only.
        private bool _luckyEarned;
        private string _restoreIntentKind, _restoreIntentValue;
        private readonly List<string> _rolledRecipes = new List<string>(); // oldest first, this run
        private readonly List<int> _rolledFaces = new List<int>();          // oldest first, this run (prayer star)
        public IReadOnlyList<string> RolledRecipes => _rolledRecipes;
        public int MatchingFaces { get; private set; }  // faces that would answer the committed wish

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
            LastIntentKind = "none"; LastIntentValue = null; MatchingFaces = 0;
            _restoreIntentKind = null; _restoreIntentValue = null;
        }

        private void ClearPending()
        {
            _pendingAcquisition = false;
            _pendingOutcomeBytes = _pendingOracleBytes = _pendingEcologyBytes = null;
            _hasPendingOutcome = false;
            _pendingFaceId = 0; _pendingCardIndex = 0; _pendingRecipeId = null;
        }
        public bool LuckySevenOffered { get; private set; }
        public bool LuckySevenAdded => CurrentFaces != null && CurrentFaces.Faces.Length == LuckySeven.FaceId;

        public GardenCoordinator(IJournalWriter writer, IEntropyProvider entropy, IIdSource ids)
        {
            _writer = writer; _entropy = entropy; _ids = ids;
        }

        public string CreateRun()
        {
            RunId = _ids.NewId("run");
            State = WorldState.CreateInitial();
            PreferenceMode = "auto"; PreferenceSiteId = null;
            _weatherEntropyByPeriod.Clear();
            _luckyEarned = false; LuckySevenOffered = false;
            _rolledRecipes.Clear();
            _rolledFaces.Clear();
            ClearPending();
            ResetIntent();
            _writer.Append("RunStarted", RunId, null, new Dictionary<string, object>
            {
                ["gameId"] = "destiny-garden",
                ["runLifecycleVersion"] = RuleVersions.RunLifecycle,
                ["simulationProfile"] = RuleVersions.SimulationProfile,
                ["mapVersion"] = RuleVersions.Map,
                ["mapSha256"] = GardenMapData.SourceMapSha256,
                ["weatherPolicyVersion"] = RuleVersions.WeatherPolicy,
                ["targetPolicyVersion"] = RuleVersions.TargetPolicy,
                ["mappingPolicyVersion"] = RuleVersions.MappingPolicy,
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
            if (mode == PreferenceMode && siteId == PreferenceSiteId) return;
            PreferenceMode = mode; PreferenceSiteId = siteId;
            _writer.Append("CareFocusChanged", RunId, null, new Dictionary<string, object>
            {
                ["mode"] = mode,
                ["siteId"] = siteId,
                ["targetPolicyVersion"] = RuleVersions.TargetPolicy
            });
        }

        public PreparedFaces Prepare()
        {
            if (Phase != CoordinatorPhase.Ready && Phase != CoordinatorPhase.Resolved)
                throw new InvalidOperationException("Prepare requires Ready/Resolved phase, was " + Phase);
            ClearPending();
            string roundId = _ids.NewId("round");
            CurrentRoundId = roundId;
            int periodIndex = State.WorldRevision / Weathers.RoundsPerEpisode;
            string beforeHash = Hashing.StateHash(State);

            _writer.Append("PreparationStarted", RunId, roundId, new Dictionary<string, object>
            {
                ["beforeRevision"] = State.WorldRevision,
                ["beforeStateHash"] = beforeHash,
                ["weatherPeriodIndex"] = periodIndex,
                ["careFocusMode"] = PreferenceMode,
                ["careFocusSiteId"] = PreferenceSiteId
            });

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

            var buffer = new EntropyBuffer(mappingBytes);
            var faces = FaceGenerator.Generate(State, CurrentWeather, PreferenceMode, PreferenceSiteId, buffer);
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
            CurrentFaces = faces;
            LuckySevenOffered = _luckyEarned;
            _luckyEarned = false;

            var faceList = new List<object>();
            foreach (string f in faces.Faces) faceList.Add(f);
            _writer.Append("Prepared", RunId, roundId, new Dictionary<string, object>
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
            });
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
            string recipeId = LuckySeven.PickRecipe(State, CurrentWeather, CurrentFaces.ResolvedSiteId);
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

        public void CommitIntent(string kind, string value)
        {
            if (Phase != CoordinatorPhase.Prepared)
                throw new InvalidOperationException("Intent requires Prepared phase.");
            LastIntentKind = kind ?? "none";
            LastIntentValue = value;
            int matchFaces = 0;
            if (LastIntentKind == "family" && value != null)
                foreach (string f in CurrentFaces.Faces)
                    if (RecipeCatalog.ElementOf(f).ToString().ToLowerInvariant() == value) matchFaces++;
            if (LastIntentKind == "face" && value != null) matchFaces = 1;
            MatchingFaces = matchFaces;
            _writer.Append("IntentCommitted", RunId, CurrentRoundId, new Dictionary<string, object>
            {
                ["kind"] = LastIntentKind,
                ["value"] = value,
                ["matchingFaces"] = matchFaces,
                ["baselineNumerator"] = matchFaces, // probability = matchingFaces / faceCount
                ["faceCount"] = CurrentFaces.Faces.Length
            });
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
                _writer.Append("OutcomeCommitted", RunId, roundId, new Dictionary<string, object>
                {
                    ["faceId"] = faceId,
                    ["recipeId"] = recipeId,
                    ["cardIndex"] = cardIndex,
                    ["cardStatus"] = cardIndex < 0 ? "unavailable_entropy_exhausted" : "drawn",
                    ["faceCount"] = faceCount,
                    ["luckySeven"] = luckyRolled
                });
            ClearPending();

            int anchor = Sites.ById(CurrentFaces.ResolvedSiteId).CenterCellId;
            var resolvedRecipe = luckyRolled
                ? LuckySeven.ResolveAmplified(recipeId, anchor, State)
                : RecipeResolver.Resolve(recipeId, anchor, State);
            int seedCounter = 0;
            var sim = Simulation.Resolve(State, resolvedRecipe, CurrentWeather,
                n => _ids.NewId("plant") + "-" + (seedCounter++), roundId);
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
                Weather = CurrentWeather,
                FaceId = faceId,
                PreviousFaceId = previousFace,
                AnchorCellId = anchor,
                SpiritCount = matched ? Math.Min(PrayerSpirits.MaxSpirits,
                    (faceCount + Math.Max(1, MatchingFaces) - 1) / Math.Max(1, MatchingFaces)) : 0
            }, ecologyBytes, n => _ids.NewId("plant") + "-eco" + n);

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
            State = eco.PostState;
            string afterHash = Hashing.StateHash(State);
            _writer.Append("Resolved", RunId, roundId, new Dictionary<string, object>
            {
                ["worldRevision"] = State.WorldRevision,
                ["postState"] = WorldStateSerializer.ToPayload(State),
                ["postStateHash"] = afterHash,
                ["events"] = events,
                ["diceWaterInjected"] = sim.DiceWaterInjected,
                ["weatherWaterInjected"] = sim.WeatherWaterInjected,
                ["discharged"] = sim.Discharged,
                ["evaporated"] = sim.Evaporated,
                ["weatherId"] = CurrentWeather.Id.ToString(),
                ["ecology"] = DestinyGarden.Domain.Ecology.ToPayload(eco),
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

            _rolledRecipes.Add(recipeId);
            _rolledFaces.Add(faceId);
            _recentMessageIds.Add(message.TemplateId);
            while (_recentMessageIds.Count > 3) _recentMessageIds.RemoveAt(0);
            Phase = CoordinatorPhase.Resolved;
            _luckyEarned = matched;
            return new RollOutcome
            {
                FaceId = faceId, FaceCount = faceCount, LuckySeven = luckyRolled, IntentMatched = matched,
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
            _luckyEarned = false; LuckySevenOffered = false;
            _rolledRecipes.Clear();
            _rolledFaces.Clear();
            ClearPending();
            _weatherEntropyByPeriod.Clear();
            _recentMessageIds.Clear();

            foreach (var e in read.Entries)
            {
                switch (e.Kind)
                {
                    case "RunStarted":
                        _weatherEntropyByPeriod.Clear();
                        _recentMessageIds.Clear();
                        RunId = e.RunId;
                        State = WorldStateSerializer.FromPayload(
                            (Dictionary<string, object>)e.Payload["initialState"]);
                        PreferenceMode = "auto"; PreferenceSiteId = null;
                        _luckyEarned = false; LuckySevenOffered = false;
                        _rolledRecipes.Clear();
                        _rolledFaces.Clear();
                        ClearPending();
                        ResetIntent();
                        Phase = CoordinatorPhase.Ready;
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
                                Faces = faces.ToArray()
                            };
                            LuckySevenOffered = e.Payload.TryGetValue("luckySevenOffered", out object lo) && lo is bool lb && lb;
                            _luckyEarned = false;
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
                        _luckyEarned = LuckySeven.IntentMatched(_restoreIntentKind, _restoreIntentValue,
                            WorldStateSerializer.ToInt(e.Payload["faceId"]), e.Payload["recipeId"] as string);
                        break;
                    case "Resolved":
                        if (_hasPendingOutcome && _pendingRecipeId != null)
                        {
                            _rolledRecipes.Add(_pendingRecipeId);
                            _rolledFaces.Add(_pendingFaceId);
                        }
                        ClearPending();
                        State = WorldStateSerializer.FromPayload(
                            (Dictionary<string, object>)e.Payload["postState"]);
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
                        ClearPending();
                        CurrentFaces = null; CurrentRoundId = null;
                        Phase = CoordinatorPhase.Ready;
                        break;
                }
            }
        }
    }
}
