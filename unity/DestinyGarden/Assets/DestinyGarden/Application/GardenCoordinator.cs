using System;
using System.Collections;
using System.Collections.Generic;
using DestinyGarden.Domain;

namespace DestinyGarden.Application
{
    public enum CoordinatorPhase { NoRun, Ready, Prepared, IntentCommitted, Resolved }

    public sealed class RollOutcome
    {
        public int FaceId; // 1..6
        public string RecipeId;
        public int CardIndex; // 0..5, -1 when unavailable
        public SimulationResult Simulation;
        public string StateHashAfter;
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
                ["mappingBytesConsumed"] = faces.MappingBytesConsumed
            });
            Phase = CoordinatorPhase.Prepared;
            return faces;
        }

        public void CommitIntent(string kind, string value)
        {
            if (Phase != CoordinatorPhase.Prepared)
                throw new InvalidOperationException("Intent requires Prepared phase.");
            _writer.Append("IntentCommitted", RunId, CurrentRoundId, new Dictionary<string, object>
            {
                ["kind"] = kind ?? "none",
                ["value"] = value
            });
            Phase = CoordinatorPhase.IntentCommitted;
        }

        public RollOutcome Roll()
        {
            if (Phase != CoordinatorPhase.IntentCommitted)
                throw new InvalidOperationException("Roll requires a committed intent (use kind=none).");
            string roundId = CurrentRoundId;
            _writer.Append("AcquisitionStarted", RunId, roundId, new Dictionary<string, object>
            {
                ["entropySource"] = _entropy.SourceName
            });

            byte[] outcomeBytes = _entropy.GetBytes(32);
            byte[] oracleBytes = _entropy.GetBytes(32);
            _writer.Append("DrawEntropyCommitted", RunId, roundId, new Dictionary<string, object>
            {
                ["outcomeBase64"] = Convert.ToBase64String(outcomeBytes),
                ["oracleBase64"] = Convert.ToBase64String(oracleBytes),
                ["source"] = _entropy.SourceName
            });

            int faceId = EntropyBuffer.RollD6(outcomeBytes, out _, out _);
            if (faceId < 0)
            {
                _writer.Append("InterruptedUnresolved", RunId, roundId, new Dictionary<string, object>
                {
                    ["reason"] = "outcome_entropy_exhausted"
                });
                Phase = CoordinatorPhase.Ready;
                CurrentRoundId = null; CurrentFaces = null;
                return null;
            }
            int cardRoll = EntropyBuffer.RollD6(oracleBytes, out _, out _);
            int cardIndex = cardRoll < 0 ? -1 : cardRoll - 1;
            string recipeId = CurrentFaces.Faces[faceId - 1];

            _writer.Append("OutcomeCommitted", RunId, roundId, new Dictionary<string, object>
            {
                ["faceId"] = faceId,
                ["recipeId"] = recipeId,
                ["cardIndex"] = cardIndex,
                ["cardStatus"] = cardIndex < 0 ? "unavailable_entropy_exhausted" : "drawn"
            });

            int anchor = Sites.ById(CurrentFaces.ResolvedSiteId).CenterCellId;
            var resolvedRecipe = RecipeResolver.Resolve(recipeId, anchor, State);
            int seedCounter = 0;
            var sim = Simulation.Resolve(State, resolvedRecipe, CurrentWeather,
                n => _ids.NewId("plant") + "-" + (seedCounter++));
            if (sim.WaterBalanceResidual != 0)
                throw new InvalidOperationException("Water balance residual must be 0, was " + sim.WaterBalanceResidual);

            var events = new List<object>();
            foreach (var e in sim.Events)
            {
                events.Add(new Dictionary<string, object>
                {
                    ["kind"] = e.Kind, ["plantId"] = e.PlantId, ["cellId"] = e.CellId, ["tick"] = e.Tick
                });
            }
            State = sim.PostState;
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
                ["evaporated"] = sim.Evaporated
            });

            Phase = CoordinatorPhase.Resolved;
            return new RollOutcome
            {
                FaceId = faceId, RecipeId = recipeId, CardIndex = cardIndex,
                Simulation = sim, StateHashAfter = afterHash
            };
        }

        /// <summary>Rebuilds coordinator state from verified journal entries (same-run resume).</summary>
        public void Restore(JournalReadResult read)
        {
            if (!read.ChainValid) throw new InvalidOperationException("Journal chain invalid: " + read.Error);
            RunId = null; State = null; CurrentFaces = null; CurrentRoundId = null;
            Phase = CoordinatorPhase.NoRun;
            _weatherEntropyByPeriod.Clear();

            foreach (var e in read.Entries)
            {
                switch (e.Kind)
                {
                    case "RunStarted":
                        RunId = e.RunId;
                        State = WorldStateSerializer.FromPayload(
                            (Dictionary<string, object>)e.Payload["initialState"]);
                        PreferenceMode = "auto"; PreferenceSiteId = null;
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
                            Phase = CoordinatorPhase.Prepared;
                            break;
                        }
                    case "IntentCommitted":
                        if (Phase == CoordinatorPhase.Prepared) Phase = CoordinatorPhase.IntentCommitted;
                        break;
                    case "Resolved":
                        State = WorldStateSerializer.FromPayload(
                            (Dictionary<string, object>)e.Payload["postState"]);
                        CurrentFaces = null; CurrentRoundId = null;
                        Phase = CoordinatorPhase.Resolved;
                        break;
                    case "PreparationInterrupted":
                    case "InterruptedUnresolved":
                        CurrentFaces = null; CurrentRoundId = null;
                        Phase = CoordinatorPhase.Ready;
                        break;
                }
            }
        }
    }
}
