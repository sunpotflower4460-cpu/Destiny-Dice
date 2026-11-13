using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>Optional v6 memory: small, bounded families and a finite pond food web.</summary>
    public sealed class CommunityState
    {
        public int[] Algae = new int[GardenMapData.CellCount];
        public int[] AquaticPrey = new int[GardenMapData.CellCount];
        public List<ResidentFamily> Families = new List<ResidentFamily>();
        public List<SettlementSite> Prospects = new List<SettlementSite>();
        public CommunityState Clone()
        {
            var c = new CommunityState { Algae = (int[])Algae.Clone(), AquaticPrey = (int[])AquaticPrey.Clone() };
            foreach (var f in Families) c.Families.Add(f.Clone());
            foreach (var p in Prospects) c.Prospects.Add(p.Clone());
            return c;
        }
    }
    public sealed class ResidentFamily
    {
        public string FamilyId, Kind;
        public int CellId, Adults = 1, Brood, BroodAge, AdultHunger, NurseryHunger, BadHabitat, Age, Cooldown;
        public ResidentFamily Clone() => (ResidentFamily)MemberwiseClone();
    }
    public sealed class SettlementSite
    {
        public string Kind;
        public int CellId, Streak;
        public SettlementSite Clone() => (SettlementSite)MemberwiseClone();
    }
}
