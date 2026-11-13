using System;
using System.Collections;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    public static class HabitatSerializer
    {
        public static Dictionary<string, object> ToPayload(HabitatState h)
        {
            var reserves = new List<object>(); var ids = new List<string>(h.RootReserves.Keys); ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids) reserves.Add(new Dictionary<string, object> { ["plantId"] = id, ["amount"] = h.RootReserves[id] });
            return new Dictionary<string, object> { ["humus"] = (int[])h.Humus.Clone(), ["lastHabitat"] = (int[])h.LastHabitat.Clone(),
                ["stableAge"] = (int[])h.StableAge.Clone(), ["rootReserves"] = reserves };
        }
        public static HabitatState FromPayload(IDictionary<string, object> data)
        {
            var h = new HabitatState(); Copy(data["humus"], h.Humus); Copy(data["lastHabitat"], h.LastHabitat); Copy(data["stableAge"], h.StableAge);
            foreach (IDictionary<string, object> p in (IEnumerable)data["rootReserves"])
                h.RootReserves.Add((string)p["plantId"], WorldStateSerializer.ToInt(p["amount"]));
            return h;
        }
        private static void Copy(object source, int[] target)
        {
            int i = 0;
            foreach (var v in (IEnumerable)source)
            { if (i == target.Length) throw new ArgumentException("Habitat array too long."); target[i++] = WorldStateSerializer.ToInt(v); }
            if (i != target.Length) throw new ArgumentException("Habitat array has wrong length.");
        }
    }
}
