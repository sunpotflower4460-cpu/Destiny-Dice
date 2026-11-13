using System;
using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    /// <summary>
    /// Why seeds become dandelions: over real play (coordinator), records for every germination which
    /// conditions the place met, and for plantable cells how moist, hard and well drained they are.
    /// Report only.
    /// </summary>
    public class SoilDiagnosisTests
    {
        private sealed class Memory : IJournalWriter
        {
            public string HeadHash => "";
            public long NextSeq => 1;
            public JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload) => new JournalEntry();
        }

        [Test]
        public void Measure_WhyNothingFits()
        {
            int germ = 0, noneFit = 0, chosenDandelion = 0, fitWhenDandelion = 0;
            var byCarrier = new Dictionary<string, int>();
            var failCount = new Dictionary<string, int> { ["hard"] = 0, ["drain"] = 0, ["dry"] = 0, ["wet"] = 0, ["light"] = 0 };
            var onlyFail = new Dictionary<string, int> { ["hard"] = 0, ["drain"] = 0, ["dry"] = 0, ["wet"] = 0, ["light"] = 0 };
            long moistSum = 0, hardSum = 0, drainSum = 0; int samples = 0;
            var moistHist = new int[11];
            var hardHist = new int[11];
            long diceWater = 0, weatherWater = 0, discharged = 0, evaporated = 0; int rolls = 0;
            for (int run = 0; run < 6; run++)
            {
                var coord = new GardenCoordinator(new Memory(), new FixedEntropy("soil-" + run), new SequentialIds());
                coord.CreateRun();
                coord.Prepare();
                for (int i = 0; i < 60; i++)
                {
                    coord.CommitIntent("none", null);
                    var before = coord.State;
                    var o = coord.Roll();
                    coord.Prepare();
                    if (o == null) continue;
                    rolls++;
                    diceWater += o.Simulation.DiceWaterInjected; weatherWater += o.Simulation.WeatherWaterInjected;
                    discharged += o.Simulation.Discharged; evaporated += o.Simulation.Evaporated;
                    var sim = o.Simulation.PostState;
                    foreach (var g in o.Ecology.Effects.Where(e => e.Kind == Ecology.Germinate))
                    {
                        germ++;
                        int c = g.CellId;
                        if (g.Species == "dandelion") chosenDandelion++;
                        var carried = o.Ecology.Effects.FirstOrDefault(e => e.Seed && e.CellId == c);
                        string how = carried != null ? carried.Kind : o.Simulation.Events.Any(e => e.Kind == "SeedPlaced" && e.CellId == c) ? "die" : "other";
                        string key = how + (g.Species == "dandelion" ? ":dandelion" : ":other");
                        byCarrier[key] = byCarrier.TryGetValue(key, out int bc) ? bc + 1 : 1;
                        if (g.Species == "dandelion") fitWhenDandelion += Flora.FittingAt(sim, c).Count;
                        if (Flora.FittingAt(sim, c).Count == 0) noneFit++;
                        // For each species (except the pioneer), which conditions fail here?
                        foreach (var t in Flora.All)
                        {
                            if (t.Id == "dandelion") continue;
                            var fails = new List<string>();
                            if (Flora.Hardness(sim, c) > t.MaxHardness) fails.Add("hard");
                            int d = Flora.Drainage(sim, c);
                            if (d < t.DrainMin || d > t.DrainMax) fails.Add("drain");
                            int m = Flora.Moisture(sim, c);
                            if (m < t.MoistMin) fails.Add("dry");
                            if (m > t.MoistMax) fails.Add("wet");
                            bool shade = Flora.IsRockShade(c);
                            if ((t.Light == 0 && shade) || (t.Light == 2 && !shade)) fails.Add("light");
                            foreach (var f in fails) failCount[f]++;
                            if (fails.Count == 1) onlyFail[fails[0]]++;
                        }
                    }
                    if (i % 10 == 9)
                        for (int c = 0; c < GardenMapData.CellCount; c++)
                        {
                            if (GardenMapData.Plantable[c] != 1) continue;
                            int m = Flora.Moisture(coord.State, c), h = Flora.Hardness(coord.State, c);
                            moistSum += m; hardSum += h; drainSum += Flora.Drainage(coord.State, c); samples++;
                            moistHist[Math.Min(10, m / 100)]++;
                            hardHist[Math.Min(10, h / 100)]++;
                        }
                }
            }
            string H(int[] h) => string.Join(" ", h.Select((v, i) => $"{i * 100}:{100.0 * v / Math.Max(1, samples):0}%"));
            string report = $"rolls={rolls} germinations={germ} noneFit={noneFit} ({100.0 * noneFit / Math.Max(1, germ):0}%) " +
                            $"becameDandelion={chosenDandelion} ({100.0 * chosenDandelion / Math.Max(1, germ):0}%)" +
                            " | failing conditions (species x germination): " + string.Join(", ", failCount.Select(k => $"{k.Key} {k.Value}")) +
                            " | the only failing condition: " + string.Join(", ", onlyFail.Select(k => $"{k.Key} {k.Value}")) +
                            $" | plantable cells: moisture mean {moistSum / (double)Math.Max(1, samples):0}‰, hardness mean {hardSum / (double)Math.Max(1, samples):0}, drainage mean {drainSum / (double)Math.Max(1, samples):0}" +
                            " | moisture hist " + H(moistHist) + " | hardness hist " + H(hardHist) +
                            $" | water per roll: dice {diceWater / (double)rolls:0}, weather {weatherWater / (double)rolls:0}, discharged {discharged / (double)rolls:0}, evaporated {evaporated / (double)rolls:0}" +
                            $" | plantable cells {Enumerable.Range(0, GardenMapData.CellCount).Count(c => GardenMapData.Plantable[c] == 1)}, with soil capacity {Enumerable.Range(0, GardenMapData.CellCount).Count(c => GardenMapData.Plantable[c] == 1 && GardenMapData.SoilCapacity[c] > 0)}, rock shade {Enumerable.Range(0, GardenMapData.CellCount).Count(c => GardenMapData.Plantable[c] == 1 && Flora.IsRockShade(c))}";
            report += " | by carrier: " + string.Join(", ", byCarrier.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}")) +
                      $" | fitting species when dandelion chosen: {fitWhenDandelion / (double)Math.Max(1, chosenDandelion):0.0}";
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log("DG-SOIL " + report);
            Assert.Greater(rolls, 0);
        }
    }
}
