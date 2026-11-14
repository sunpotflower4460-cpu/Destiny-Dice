using System.Collections.Generic;
using DestinyGarden.Application;
using DestinyGarden.Domain;

namespace DestinyGarden.Tests
{
    /// <summary>
    /// A prayer-star garden begun before the living ecosystem (no EcosystemState): the gardens whose ecology step is
    /// Ecology.Apply's own (lifecycle, community.v1, withering). New runs use the living ecosystem instead.
    /// </summary>
    internal static class TestGardens
    {
        public static GardenCoordinator LegacyPrayer(IJournalWriter writer, IEntropyProvider entropy, IIdSource ids)
        {
            var coordinator = new GardenCoordinator(writer, entropy, ids);
            coordinator.Restore(new JournalReadResult
            {
                ChainValid = true,
                Entries = new List<JournalEntry>
                {
                    new JournalEntry
                    {
                        Kind = "RunStarted", RunId = ids.NewId("run"),
                        Payload = new Dictionary<string, object>
                        {
                            ["initialState"] = WorldStateSerializer.ToPayload(WorldState.CreateInitial()),
                            ["prayerStarPolicyVersion"] = PrayerStar.RulesVersion,
                            ["plantIdPolicyVersion"] = GardenCoordinator.PlantIdPolicyVersion
                        }
                    }
                }
            });
            return coordinator;
        }
    }
}
