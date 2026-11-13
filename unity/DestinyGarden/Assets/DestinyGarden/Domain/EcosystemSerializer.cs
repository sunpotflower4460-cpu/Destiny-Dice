using System.Collections;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    public static class EcosystemSerializer
    {
        public static Dictionary<string, object> ToPayload(EcosystemState e)
        {
            var cycles = new List<object>();
            var ids = new List<string>(e.Cycles.Keys);
            ids.Sort(System.StringComparer.Ordinal);
            foreach (string id in ids)
            {
                var p = e.Cycles[id];
                cycles.Add(new Dictionary<string, object>
                {
                    ["plantId"] = id, ["age"] = p.Age, ["stress"] = p.Stress, ["bloomAge"] = p.BloomAge,
                    ["pollen"] = p.Pollen, ["seeds"] = p.Seeds, ["fruited"] = p.Fruited
                });
            }
            var payload = new Dictionary<string, object>
            {
                ["litter"] = (int[])e.Litter.Clone(), ["nutrients"] = (int[])e.Nutrients.Clone(), ["wetAge"] = (int[])e.WetAge.Clone(),
                ["coverAge"] = (int[])e.CoverAge.Clone(), ["aphids"] = (int[])e.Aphids.Clone(), ["cycles"] = cycles
            };
            if (e.Succession != null) payload["succession"] = SuccessionSerializer.ToPayload(e.Succession);
            if (e.Habitat != null) payload["habitat"] = HabitatSerializer.ToPayload(e.Habitat);
            return payload;
        }

        public static EcosystemState FromPayload(IDictionary<string, object> d)
        {
            var e = new EcosystemState();
            Copy(d["litter"], e.Litter); Copy(d["nutrients"], e.Nutrients);
            Copy(d["wetAge"], e.WetAge); Copy(d["coverAge"], e.CoverAge); Copy(d["aphids"], e.Aphids);
            foreach (IDictionary<string, object> p in (IEnumerable)d["cycles"])
                e.Cycles.Add((string)p["plantId"], new PlantCycle
                {
                    Age = Int(p, "age"), Stress = Int(p, "stress"), BloomAge = Int(p, "bloomAge"),
                    Pollen = Int(p, "pollen"), Seeds = Int(p, "seeds"), Fruited = (bool)p["fruited"]
                });
            if (d.TryGetValue("succession", out object succession) && succession is IDictionary<string, object> sd)
                e.Succession = SuccessionSerializer.FromPayload(sd);
            if (d.TryGetValue("habitat", out object habitat) && habitat is IDictionary<string, object> hd)
                e.Habitat = HabitatSerializer.FromPayload(hd);
            return e;
        }

        private static int Int(IDictionary<string, object> d, string key) => WorldStateSerializer.ToInt(d[key]);
        private static void Copy(object src, int[] dst)
        {
            int i = 0;
            foreach (var v in (IEnumerable)src)
            {
                if (i >= dst.Length) throw new System.ArgumentException("Ecology array too long.");
                dst[i++] = WorldStateSerializer.ToInt(v);
            }
            if (i != dst.Length) throw new System.ArgumentException("Ecology array has wrong length.");
        }
    }
}
