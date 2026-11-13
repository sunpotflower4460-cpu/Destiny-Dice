using System;
using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    /// <summary>
    /// Long play: does the garden keep changing, and do the dice keep mattering, after many rolls?
    /// Reports per 20-roll block: plants, flowers, empty plantable cells, seed faces published,
    /// germinations, growth added and species turnover. Report only.
    /// </summary>
    public class LongPlayTests
    {
        private sealed class Memory : IJournalWriter
        {
            public string HeadHash => "";
            public long NextSeq => 1;
            public JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload) => new JournalEntry();
        }

        [Test]
        public void Measure_LongPlay()
        {
            const int runs = 6, rolls = 200, block = 20;
            int blocks = rolls / block;
            var plants = new double[blocks]; var flowers = new double[blocks]; var empty = new double[blocks];
            var seedFaces = new double[blocks]; var germ = new double[blocks]; var growth = new double[blocks];
            var withered = new double[blocks]; var speciesSeen = new double[blocks];
            int plantable = Enumerable.Range(0, GardenMapData.CellCount).Count(c => GardenMapData.Plantable[c] == 1);
            for (int run = 0; run < runs; run++)
            {
                var coord = new GardenCoordinator(new Memory(), new FixedEntropy("long-" + run), new SequentialIds());
                coord.CreateRun();
                coord.Prepare();
                for (int i = 0; i < rolls; i++)
                {
                    int b = i / block;
                    seedFaces[b] += coord.CurrentFaces.Faces.Count(f => RecipeCatalog.ElementOf(f) == ElementId.Seed);
                    coord.CommitIntent("none", null);
                    var before = coord.State.Clone();
                    var o = coord.Roll();
                    coord.Prepare();
                    if (o == null) continue;
                    var s = coord.State;
                    germ[b] += o.Ecology.Effects.Count(e => e.Kind == Ecology.Germinate);
                    growth[b] += s.Plants.Sum(p => (long)p.Growth) - before.Plants.Sum(p => (long)p.Growth);
                    withered[b] += before.Plants.Count(p => s.Plants.All(q => q.PlantId != p.PlantId));
                    if (i % block == block - 1)
                    {
                        plants[b] += s.Plants.Count;
                        flowers[b] += s.Plants.Count(p => p.Stage == PlantStage.Flower);
                        empty[b] += plantable - s.Plants.Count;
                        speciesSeen[b] += s.Plants.Where(p => p.Species != null).Select(p => p.Species).Distinct().Count();
                    }
                }
            }
            var lines = new List<string>();
            for (int b = 0; b < blocks; b++)
                lines.Add($"rolls {(b + 1) * block}: plants {plants[b] / runs:0.0}, flowers {flowers[b] / runs:0.0}, empty {empty[b] / runs:0.0}, " +
                          $"species {speciesSeen[b] / runs:0.0}, seed faces/roll {seedFaces[b] / (runs * block):0.00}, " +
                          $"germinations/roll {germ[b] / (runs * block):0.00}, growth/roll {growth[b] / (runs * block):0}, withered/roll {withered[b] / (runs * block):0.00}");
            string report = string.Join(" | ", lines);
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log("DG-LONG " + report);
            Assert.Greater(plants[blocks - 1], 0);
        }
    }
}
