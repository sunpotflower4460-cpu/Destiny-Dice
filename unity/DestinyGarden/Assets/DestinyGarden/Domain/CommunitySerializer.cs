using System;
using System.Collections;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    public static class CommunitySerializer
    {
        public static Dictionary<string, object> ToPayload(CommunityState c)
        {
            var families = new List<object>(); var ordered = new List<ResidentFamily>(c.Families);
            ordered.Sort((a, b) => string.CompareOrdinal(a.FamilyId, b.FamilyId));
            foreach (var f in ordered)
                families.Add(new Dictionary<string, object>
                {
                    ["familyId"] = f.FamilyId, ["kind"] = f.Kind, ["cellId"] = f.CellId,
                    ["adults"] = f.Adults, ["brood"] = f.Brood, ["broodAge"] = f.BroodAge,
                    ["adultHunger"] = f.AdultHunger, ["nurseryHunger"] = f.NurseryHunger,
                    ["badHabitat"] = f.BadHabitat, ["age"] = f.Age, ["cooldown"] = f.Cooldown
                });
            var prospects = new List<object>(); var sites = new List<SettlementSite>(c.Prospects);
            sites.Sort((a, b) => string.CompareOrdinal(a.Kind, b.Kind) != 0 ? string.CompareOrdinal(a.Kind, b.Kind) : a.CellId.CompareTo(b.CellId));
            foreach (var p in sites) prospects.Add(new Dictionary<string, object> { ["kind"] = p.Kind, ["cellId"] = p.CellId, ["streak"] = p.Streak });
            return new Dictionary<string, object> { ["algae"] = (int[])c.Algae.Clone(), ["aquaticPrey"] = (int[])c.AquaticPrey.Clone(),
                ["families"] = families, ["prospects"] = prospects };
        }
        public static CommunityState FromPayload(IDictionary<string, object> d)
        {
            var c = new CommunityState(); Copy(d["algae"], c.Algae); Copy(d["aquaticPrey"], c.AquaticPrey);
            foreach (IDictionary<string, object> f in (IEnumerable)d["families"])
                c.Families.Add(new ResidentFamily { FamilyId = (string)f["familyId"], Kind = (string)f["kind"], CellId = Int(f, "cellId"),
                    Adults = Int(f, "adults"), Brood = Int(f, "brood"), BroodAge = Int(f, "broodAge"), AdultHunger = Int(f, "adultHunger"),
                    NurseryHunger = Int(f, "nurseryHunger"), BadHabitat = Int(f, "badHabitat"), Age = Int(f, "age"), Cooldown = Int(f, "cooldown") });
            foreach (IDictionary<string, object> p in (IEnumerable)d["prospects"])
                c.Prospects.Add(new SettlementSite { Kind = (string)p["kind"], CellId = Int(p, "cellId"), Streak = Int(p, "streak") });
            return c;
        }
        private static int Int(IDictionary<string, object> d, string key) => WorldStateSerializer.ToInt(d[key]);
        private static void Copy(object data, int[] target)
        {
            int i = 0;
            foreach (var item in (IEnumerable)data)
            { if (i == target.Length) throw new ArgumentException("Pond array too long."); target[i++] = WorldStateSerializer.ToInt(item); }
            if (i != target.Length) throw new ArgumentException("Pond array has wrong length.");
        }
    }
}
