using System;
using System.Collections;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    public static class SuccessionSerializer
    {
        public static Dictionary<string, object> ToPayload(SuccessionState s)
        {
            var seeds = new List<object>();
            var ordered = new List<RestingSeed>(s.SeedBank);
            ordered.Sort((a, b) => string.CompareOrdinal(a.SeedId, b.SeedId));
            foreach (var seed in ordered)
                seeds.Add(new Dictionary<string, object>
                {
                    ["seedId"] = seed.SeedId, ["species"] = seed.Species, ["cellId"] = seed.CellId,
                    ["parentPlantId"] = seed.ParentPlantId, ["parentCellId"] = seed.ParentCellId,
                    ["sourceRoundId"] = seed.SourceRoundId, ["carrier"] = seed.Carrier,
                    ["age"] = seed.Age, ["bornRevision"] = seed.BornRevision, ["rested"] = seed.Rested
                });
            var flowers = new List<object>();
            foreach (string id in Sorted(s.Flowers.Keys))
                flowers.Add(new Dictionary<string, object>
                {
                    ["plantId"] = id, ["nectar"] = s.Flowers[id].Nectar, ["lastForaged"] = s.Flowers[id].LastForaged
                });
            var ancestry = new List<object>();
            foreach (string id in Sorted(s.Ancestry.Keys))
            {
                var p = s.Ancestry[id];
                ancestry.Add(new Dictionary<string, object>
                {
                    ["plantId"] = id, ["parentPlantId"] = p.ParentPlantId, ["seedId"] = p.SeedId,
                    ["carrier"] = p.Carrier, ["bornRevision"] = p.BornRevision
                });
            }
            return new Dictionary<string, object> { ["seedBank"] = seeds, ["flowers"] = flowers, ["ancestry"] = ancestry };
        }

        public static SuccessionState FromPayload(IDictionary<string, object> d)
        {
            var s = new SuccessionState();
            foreach (IDictionary<string, object> seed in (IEnumerable)d["seedBank"])
                s.SeedBank.Add(new RestingSeed
                {
                    SeedId = (string)seed["seedId"], Species = (string)seed["species"], CellId = Int(seed, "cellId"),
                    ParentPlantId = seed["parentPlantId"] as string, ParentCellId = Int(seed, "parentCellId"),
                    SourceRoundId = seed["sourceRoundId"] as string, Carrier = seed["carrier"] as string,
                    Age = Int(seed, "age"), BornRevision = Int(seed, "bornRevision"), Rested = (bool)seed["rested"]
                });
            foreach (IDictionary<string, object> p in (IEnumerable)d["flowers"])
                s.Flowers.Add((string)p["plantId"], new FlowerResource { Nectar = Int(p, "nectar"), LastForaged = Int(p, "lastForaged") });
            foreach (IDictionary<string, object> p in (IEnumerable)d["ancestry"])
                s.Ancestry.Add((string)p["plantId"], new PlantAncestry
                {
                    ParentPlantId = p["parentPlantId"] as string, SeedId = p["seedId"] as string,
                    Carrier = p["carrier"] as string, BornRevision = Int(p, "bornRevision")
                });
            return s;
        }
        private static int Int(IDictionary<string, object> p, string key) => WorldStateSerializer.ToInt(p[key]);
        private static List<string> Sorted(IEnumerable<string> keys)
        {
            var list = new List<string>(keys); list.Sort(StringComparer.Ordinal); return list;
        }
    }
}
