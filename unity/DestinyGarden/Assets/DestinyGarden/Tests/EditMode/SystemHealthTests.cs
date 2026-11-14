using System;
using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    /// <summary>
    /// What each mechanism contributes over real play (coordinator), and how much the player's choices move
    /// the garden. Report only: growth by source, where new plants come from, how often each event happens,
    /// and three ways of praying compared.
    /// </summary>
    public class SystemHealthTests
    {
        private sealed class Memory : IJournalWriter
        {
            public string HeadHash => "";
            public long NextSeq => 1;
            public JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload) => new JournalEntry();
        }

        private sealed class Tally
        {
            public int Rolls, Matched, LuckyRolled, Rainbows, Lucky7Stars;
            public int AnsweredGerm, AnsweredSuit, OtherGerm, OtherSuit;
            public long SimGrowth;
            public readonly Dictionary<string, long> EcoGrowth = new Dictionary<string, long>();
            public readonly Dictionary<string, int> NewPlants = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Visits = new Dictionary<string, int>();
            public readonly Dictionary<int, int> Stars = new Dictionary<int, int>();
            public double FinalFlowers, FinalPlants, FinalSpecies, FinalGrowth, HarmonySum;
            public void Add<TK>(Dictionary<TK, int> d, TK k, int v = 1) => d[k] = d.TryGetValue(k, out int c) ? c + v : v;
            public void AddL(Dictionary<string, long> d, string k, long v) => d[k] = d.TryGetValue(k, out long c) ? c + v : v;
        }

        /// <summary>policy: "none" (no wish), "family" (wish the commonest element), "hint" (wish the face that completes seven).</summary>
        private static Tally Play(string policy, int runs, int rolls)
        {
            var t = new Tally();
            for (int run = 0; run < runs; run++)
            {
                var coord = new GardenCoordinator(new Memory(), new FixedEntropy("health-" + run), new SequentialIds());
                coord.CreateRun();
                coord.Prepare();
                int lastFace = 0;
                for (int i = 0; i < rolls; i++)
                {
                    if (coord.LuckySevenOffered) coord.AddLuckySeven();
                    if (policy == "family")
                    {
                        string best = coord.CurrentFaces.Faces.GroupBy(f => RecipeCatalog.ElementOf(f).ToString().ToLowerInvariant())
                            .OrderByDescending(g => g.Count()).First().Key;
                        coord.CommitIntent("family", best);
                    }
                    else if (policy == "hint" && PrayerStar.FaceCompletingSeven(lastFace) > 0)
                        coord.CommitIntent("face", PrayerStar.FaceCompletingSeven(lastFace).ToString());
                    else coord.CommitIntent("none", null);
                    var before = coord.State.Clone();
                    var o = coord.Roll();
                    coord.Prepare();
                    if (o == null) continue;
                    lastFace = o.FaceId;
                    t.Rolls++;
                    if (o.IntentMatched) t.Matched++;
                    if (o.LuckySeven) t.LuckyRolled++;
                    var sim = o.Simulation.PostState;
                    foreach (var p in sim.Plants)
                    {
                        var b = before.PlantAt(p.CellId);
                        t.SimGrowth += p.Growth - (b != null && b.PlantId == p.PlantId ? b.Growth : 0);
                    }
                    var element = RecipeCatalog.ElementOf(o.RecipeId);
                    foreach (var g in o.Ecology.Effects.Where(e => e.Kind == Ecology.Germinate))
                    {
                        bool suits = Flora.Suits(Flora.Get(g.Species), element);
                        if (o.IntentMatched) { t.AnsweredGerm++; if (suits) t.AnsweredSuit++; }
                        else { t.OtherGerm++; if (suits) t.OtherSuit++; }
                    }
                    foreach (var e in o.Ecology.Effects)
                    {
                        if (e.Amount > 0) t.AddL(t.EcoGrowth, e.Kind, e.Amount);
                        if (e.Seed) t.Add(t.NewPlants, e.Kind);
                    }
                    foreach (var ev in o.Simulation.Events) if (ev.Kind == "SeedPlaced") t.Add(t.NewPlants, "die");
                    foreach (var v in o.Ecology.Visits) t.Add(t.Visits, v.Kind);
                    t.Add(t.Stars, o.Ecology.PrayerStar.Points);
                    if (PrayerStar.IsRainbow(o.LuckySeven, o.Simulation, o.Ecology)) t.Rainbows++;
                    t.HarmonySum += o.Ecology.Harmony.Value;
                }
                var s = coord.State;
                t.FinalPlants += s.Plants.Count;
                t.FinalFlowers += s.Plants.Count(p => p.Stage == PlantStage.Flower);
                t.FinalSpecies += s.Plants.Where(p => p.Species != null).Select(p => p.Species).Distinct().Count();
                t.FinalGrowth += s.Plants.Sum(p => (long)p.Growth);
            }
            t.FinalPlants /= runs; t.FinalFlowers /= runs; t.FinalSpecies /= runs; t.FinalGrowth /= runs;
            return t;
        }

        private static string Report(string name, Tally t, int runs)
        {
            long eco = t.EcoGrowth.Values.Sum();
            double all = t.SimGrowth + eco;
            string growth = $"growth/roll {all / t.Rolls:0} (simulation {100.0 * t.SimGrowth / all:0}%, " +
                            string.Join(", ", t.EcoGrowth.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {100.0 * k.Value / all:0.0}%")) + ")";
            int newAll = t.NewPlants.Values.Sum();
            string births = $"new plants/roll {newAll / (double)t.Rolls:0.00} (" +
                            string.Join(", ", t.NewPlants.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {100.0 * k.Value / Math.Max(1, newAll):0}%")) + ")";
            string visits = "residents % of rolls: " + string.Join(", ", t.Visits.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {100.0 * k.Value / t.Rolls:0}"));
            string stars = "stars " + string.Join(" ", t.Stars.OrderBy(k => k.Key).Select(k => $"{k.Key}:{100.0 * k.Value / t.Rolls:0.0}%"));
            return $"[{name}] rolls {t.Rolls}, wish matched {100.0 * t.Matched / t.Rolls:0}%, lucky seven rolled {t.LuckyRolled}, " +
                   $"rainbows 1 per {(t.Rainbows > 0 ? t.Rolls / (double)t.Rainbows : double.PositiveInfinity):0}, harmony {t.HarmonySum / t.Rolls:0.00}, {stars} | {growth} | {births} | {visits} | " +
                   $"seeds suiting the rolled blessing: wish answered {100.0 * t.AnsweredSuit / Math.Max(1, t.AnsweredGerm):0}% of {t.AnsweredGerm}, otherwise {100.0 * t.OtherSuit / Math.Max(1, t.OtherGerm):0}% of {t.OtherGerm} | " +
                   $"end: plants {t.FinalPlants:0.0}, flowers {t.FinalFlowers:0.0}, species {t.FinalSpecies:0.0}, total growth {t.FinalGrowth:0}";
        }

        [Test]
        public void Measure_SystemHealth()
        {
            const int runs = 6, rolls = 60;
            var lines = new List<string>();
            foreach (var policy in new[] { "none", "family", "hint" })
                lines.Add(Report(policy, Play(policy, runs, rolls), runs));
            string report = string.Join(" || ", lines);
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log("DG-HEALTH " + report);
            Assert.Greater(lines.Count, 0);
        }
    }
}
