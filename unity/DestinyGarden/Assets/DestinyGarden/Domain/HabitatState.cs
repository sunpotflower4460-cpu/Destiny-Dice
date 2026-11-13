using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>Optional v7 memory of soil development, habitat continuity and living root reserves.</summary>
    public sealed class HabitatState
    {
        public bool LinkedCare; // opt-in ecology.v8; omitted from saved v7 gardens
        public int[] Humus = new int[GardenMapData.CellCount];
        public int[] LastHabitat = new int[GardenMapData.CellCount];
        public int[] StableAge = new int[GardenMapData.CellCount];
        public Dictionary<string, int> RootReserves = new Dictionary<string, int>(StringComparer.Ordinal);
        public HabitatState() { for (int c = 0; c < LastHabitat.Length; c++) LastHabitat[c] = -1; }
        public HabitatState Clone()
        {
            var h = new HabitatState { LinkedCare = LinkedCare, Humus = (int[])Humus.Clone(), LastHabitat = (int[])LastHabitat.Clone(), StableAge = (int[])StableAge.Clone() };
            foreach (var pair in RootReserves) h.RootReserves.Add(pair.Key, pair.Value);
            return h;
        }
    }
}
