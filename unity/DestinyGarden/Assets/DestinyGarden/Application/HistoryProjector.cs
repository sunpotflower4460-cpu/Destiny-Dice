using System;
using System.Collections;
using System.Collections.Generic;
using DestinyGarden.Domain;

namespace DestinyGarden.Application
{
    public sealed class RoundSummary
    {
        public string RoundId;
        public int WorldRevision;      // after resolve; 0 = initial garden point
        public string WeatherId;
        public string SiteId;
        public string IntentKind;
        public string IntentValue;
        public int? FaceId;
        public string RecipeId;
        public string CardId;
        public string MessageText;
        public long TotalWater;
        public long GrowthSum;
        public bool Resolved;
        public int MatchingFaces;
        public bool IntentMatched;
    }

    /// <summary>Projects verified journal entries of one run into display summaries.</summary>
    public static class HistoryProjector
    {
        public static List<RoundSummary> Project(IList<JournalEntry> entries, string runId)
        {
            var rounds = new List<RoundSummary>();
            var byRound = new Dictionary<string, RoundSummary>();
            RoundSummary initial = null;

            RoundSummary Get(string roundId)
            {
                if (!byRound.TryGetValue(roundId, out var s))
                {
                    s = new RoundSummary { RoundId = roundId };
                    byRound[roundId] = s;
                    rounds.Add(s);
                }
                return s;
            }

            foreach (var e in entries)
            {
                if (e.RunId != runId) continue;
                switch (e.Kind)
                {
                    case "RunStarted":
                        initial = new RoundSummary { WorldRevision = 0, Resolved = true };
                        Totals((Dictionary<string, object>)e.Payload["initialState"], initial);
                        break;
                    case "Prepared":
                        {
                            var s = Get(e.RoundId);
                            s.WeatherId = e.Payload["weatherId"] as string;
                            s.SiteId = e.Payload["resolvedSiteId"] as string;
                            break;
                        }
                    case "IntentCommitted":
                        {
                            var s = Get(e.RoundId);
                            s.IntentKind = e.Payload["kind"] as string;
                            s.IntentValue = e.Payload["value"] as string;
                            if (e.Payload.TryGetValue("matchingFaces", out object mf))
                                s.MatchingFaces = WorldStateSerializer.ToInt(mf);
                            break;
                        }
                    case "OutcomeCommitted":
                        {
                            var s = Get(e.RoundId);
                            s.FaceId = WorldStateSerializer.ToInt(e.Payload["faceId"]);
                            s.RecipeId = e.Payload["recipeId"] as string;
                            break;
                        }
                    case "Resolved":
                        {
                            var s = Get(e.RoundId);
                            s.Resolved = true;
                            s.WorldRevision = WorldStateSerializer.ToInt(e.Payload["worldRevision"]);
                            if (e.Payload.TryGetValue("weatherId", out object w)) s.WeatherId = w as string;
                            if (e.Payload.TryGetValue("selectedMessage", out object sm) &&
                                sm is Dictionary<string, object> smd)
                            {
                                s.MessageText = smd["text"] as string;
                                s.CardId = smd["cardId"] as string;
                            }
                            Totals((Dictionary<string, object>)e.Payload["postState"], s);
                            if (s.IntentKind == "family" && s.RecipeId != null)
                                s.IntentMatched = DestinyGarden.Domain.RecipeCatalog.ElementOf(s.RecipeId)
                                    .ToString().ToLowerInvariant() == s.IntentValue;
                            if (s.IntentKind == "face" && s.FaceId.HasValue && s.IntentValue != null)
                                s.IntentMatched = s.FaceId.Value.ToString() == s.IntentValue;
                            break;
                        }
                }
            }
            var result = new List<RoundSummary>();
            if (initial != null) result.Add(initial);
            result.AddRange(rounds);
            return result;
        }

        private static void Totals(Dictionary<string, object> state, RoundSummary s)
        {
            long water = 0;
            foreach (var v in (IEnumerable)state["soilWater"]) water += WorldStateSerializer.ToInt(v);
            foreach (var v in (IEnumerable)state["surfaceWater"]) water += WorldStateSerializer.ToInt(v);
            long growth = 0;
            foreach (var p in (IEnumerable)state["plants"])
                growth += WorldStateSerializer.ToInt(((IDictionary<string, object>)p)["growth"]);
            s.TotalWater = water;
            s.GrowthSum = growth;
        }
    }
}
