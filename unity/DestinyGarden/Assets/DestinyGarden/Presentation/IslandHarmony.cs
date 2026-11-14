using System.Collections;
using System.Collections.Generic;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// The island as one living whole (presentation of saved facts only):
    /// - faint mycorrhizal threads between plants that the ecology step treats as one network,
    /// - after a roll, the blessing travels across the island to where it actually landed
    ///   (water from the spring down the river and over the land, light from the sky, wind across
    ///   the meadow), then light pulses through the network to the plants it really fed.
    /// </summary>
    public sealed class IslandHarmony : MonoBehaviour
    {
        public GardenView View;
        public bool ReducedMotion;
        private Material _threadMat;
        private Texture2D _glow;
        private readonly List<LineRenderer> _threads = new List<LineRenderer>();
        private readonly List<(int a, int b)> _edges = new List<(int a, int b)>();
        private ParticleSystem _spark;
        private float _threadAlpha = 0.18f;

        public static Material GlowMaterial(Texture2D tex, Color color, int queue = 3200)
        {
            var sh = Shader.Find("DestinyGarden/Glow");
            var m = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
            if (tex != null) m.mainTexture = tex;
            m.color = color;
            m.renderQueue = queue;
            return m;
        }

        public void Build()
        {
            _glow = ProceduralArt.GlowSprite();
            _threadMat = GlowMaterial(null, Color.white, 2998);
            var obj = new GameObject("HarmonySparks");
            obj.transform.SetParent(transform, false);
            _spark = obj.AddComponent<ParticleSystem>();
            var main = _spark.main;
            main.playOnAwake = false;
            main.maxParticles = 900;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = _spark.emission; emission.rateOverTime = 0f;
            var shape = _spark.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.06f;
            var col = _spark.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            obj.GetComponent<ParticleSystemRenderer>().material = GlowMaterial(_glow, Color.white, 3340);
        }

        private Vector3 PlantPoint(int cell) => View.CellTop(cell) + Vector3.up * 0.03f;

        /// <summary>Threads follow Ecology.Networks: a minimum spanning tree inside each network.</summary>
        public void ShowNetwork(WorldState state, int harmony)
        {
            foreach (var t in _threads) if (t != null) Destroy(t.gameObject);
            _threads.Clear();
            _edges.Clear();
            _threadAlpha = 0.035f + harmony * 0.012f;
            foreach (var net in Ecology.Networks(state))
            {
                if (net.Count < 2) continue;
                var inTree = new List<int> { net[0] };
                var rest = new List<int>(net); rest.RemoveAt(0);
                while (rest.Count > 0)
                {
                    float best = float.MaxValue; int bi = -1, bj = -1;
                    for (int i = 0; i < inTree.Count; i++)
                        for (int j = 0; j < rest.Count; j++)
                        {
                            float d = Vector3.SqrMagnitude(PlantPoint(inTree[i]) - PlantPoint(rest[j]));
                            if (d < best) { best = d; bi = i; bj = j; }
                        }
                    _edges.Add((inTree[bi], rest[bj]));
                    inTree.Add(rest[bj]);
                    rest.RemoveAt(bj);
                }
            }
            // The network stays underground: its threads are not drawn (only what it feeds is shown).
        }

        private LineRenderer Thread(Vector3 a, Vector3 b, int seed)
        {
            var obj = new GameObject("Mycorrhiza");
            obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>();
            line.material = _threadMat;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.022f;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            const int n = 10;
            line.positionCount = n;
            var side = Vector3.Cross(Vector3.up, (b - a).normalized);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                var p = Vector3.Lerp(a, b, t) + side * (Mathf.Sin(t * Mathf.PI * 2f + seed) * 0.08f * Mathf.Sin(t * Mathf.PI));
                p.y = IslandTerrain.Height(p.x, p.z) + 0.03f;
                line.SetPosition(i, p);
            }
            var c = new Color(1f, 0.80f, 0.45f, _threadAlpha);
            line.startColor = c;
            line.endColor = new Color(0.70f, 1f, 0.80f, _threadAlpha);
            return line;
        }

        /// <summary>The blessing crosses the island to where it landed (from saved evidence).</summary>
        public IEnumerator PlayBlessing(RollOutcome o)
        {
            if (o == null || o.Simulation == null || ReducedMotion) yield break;
            var ev = o.Simulation.Evidence;
            var element = RecipeCatalog.ElementOf(o.RecipeId);
            var targets = new List<Vector3>();
            IEnumerable<int> cells = element == ElementId.Water ? ev.DiceWaterCells
                : element == ElementId.Light ? ev.LightReceivedCells
                : element == ElementId.Wind ? ev.WindReceivedCells : null;
            if (cells != null) foreach (int c in cells) targets.Add(View.CellTop(c));
            if (element == ElementId.Seed)
                foreach (var e in o.Simulation.Events) if (e.Kind == "SeedPlaced") targets.Add(View.CellTop(e.CellId));
            if (targets.Count == 0) yield break;
            var centre = Vector3.zero;
            foreach (var t in targets) centre += t;
            centre /= targets.Count;

            switch (element)
            {
                case ElementId.Water: yield return WaterFromSpring(centre, targets); break;
                case ElementId.Light: yield return LightFromSky(centre, targets); break;
                case ElementId.Wind: yield return WindAcross(centre, targets); break;
                default: yield break; // seeds are sown from the die (GardenLife.PlaySowing)
            }
        }

        private void Emit(Vector3 at, Color c, int n)
        {
            _spark.transform.position = at;
            _spark.Emit(new ParticleSystem.EmitParams { startColor = c }, n);
        }

        private IEnumerator WaterFromSpring(Vector3 centre, List<Vector3> targets)
        {
            var path = new List<Vector3>();
            var river = IslandTerrain.RiverPath;
            int nearest = 0; float best = float.MaxValue;
            for (int i = 0; i < river.Length; i++)
            {
                float d = Vector2.Distance(river[i], new Vector2(centre.x, centre.z));
                if (d < best) { best = d; nearest = i; }
            }
            for (int i = 0; i <= nearest; i++)
                path.Add(new Vector3(river[i].x, IslandTerrain.Height(river[i].x, river[i].y) + 0.12f, river[i].y));
            var from = path[path.Count - 1];
            for (int k = 1; k <= 6; k++)
            {
                var p = Vector3.Lerp(from, centre, k / 6f);
                p.y = IslandTerrain.Height(p.x, p.z) + 0.10f;
                path.Add(p);
            }
            var water = new Color(0.55f, 0.92f, 1f, 1f);
            float speed = 7.5f;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                float len = Vector3.Distance(path[i], path[i + 1]);
                for (float t = 0f; t < len; t += speed * Time.deltaTime)
                {
                    Emit(Vector3.Lerp(path[i], path[i + 1], t / len), water, 3);
                    yield return null;
                }
            }
            foreach (var t in targets) Emit(t + Vector3.up * 0.1f, water, 10);
        }

        private IEnumerator LightFromSky(Vector3 centre, List<Vector3> targets)
        {
            var gold = new Color(1f, 0.90f, 0.62f, 1f);
            var top = centre + Vector3.up * 7f;
            for (float t = 0f; t < 1.0f; t += Time.deltaTime)
            {
                Emit(Vector3.Lerp(top, centre, t) + Random.insideUnitSphere * 0.15f, gold, 4);
                yield return null;
            }
            for (int ring = 0; ring < 3; ring++)
            {
                foreach (var tg in targets) Emit(tg + Vector3.up * 0.15f, gold, 4);
                yield return new WaitForSeconds(0.12f);
            }
        }

        private IEnumerator WindAcross(Vector3 centre, List<Vector3> targets)
        {
            View.Flora?.Gust();
            var pale = new Color(0.90f, 1f, 0.92f, 1f);
            var start = new Vector3(-2f, 1.2f, centre.z - 2f);
            for (float t = 0f; t < 1.4f; t += Time.deltaTime)
            {
                var p = Vector3.Lerp(start, centre + Vector3.up * 0.6f, t / 1.4f);
                for (int k = 0; k < 3; k++) Emit(p + new Vector3(0f, Random.Range(-0.5f, 0.5f), Random.Range(-1.5f, 1.5f)), pale, 1);
                yield return null;
            }
            foreach (var tg in targets) Emit(tg + Vector3.up * 0.3f, pale, 3);
        }

        /// <summary>Light runs through the network from the plants that grew to the plants it fed.</summary>
        public IEnumerator PlayPulses(RollOutcome o)
        {
            if (o == null || o.Ecology == null || ReducedMotion) yield break;
            // Underground and unseen: only a soft glint where the network actually fed a plant.
            var fedOnly = new List<int>();
            foreach (var e in o.Ecology.Effects) if (e.Kind == Ecology.Mycorrhiza && e.Amount > 0) fedOnly.Add(e.CellId);
            foreach (var link in o.Ecology.Relations)
                if (link.Kind == "nutrient-shared" && !fedOnly.Contains(link.ToCell)) fedOnly.Add(link.ToCell);
            for (int i = 0; i < fedOnly.Count; i++)
            {
                yield return new WaitForSeconds(0.18f);
                Emit(PlantPoint(fedOnly[i]) + Vector3.up * 0.35f, new Color(1f, 0.95f, 0.78f, 0.9f), 8);
            }
            yield break;
#pragma warning disable CS0162
            var fed = new HashSet<int>();
            foreach (var e in o.Ecology.Effects) if (e.Kind == Ecology.Mycorrhiza && e.Amount > 0) fed.Add(e.CellId);
            if (fed.Count == 0) yield break;
            var grew = new HashSet<int>();
            var before = o.Simulation.Evidence.GrowthBeforeByPlant;
            foreach (var p in o.Ecology.PostState.Plants)
                if (before.TryGetValue(p.PlantId, out int g0) && p.Growth > g0 && !fed.Contains(p.CellId)) grew.Add(p.CellId);
            // Breadth-first along the drawn threads, starting from plants that grew.
            var adj = new Dictionary<int, List<int>>();
            foreach (var e in _edges)
            {
                if (!adj.ContainsKey(e.a)) adj[e.a] = new List<int>();
                if (!adj.ContainsKey(e.b)) adj[e.b] = new List<int>();
                adj[e.a].Add(e.b); adj[e.b].Add(e.a);
            }
            var frontier = new List<int>(grew);
            var seen = new HashSet<int>(grew);
            var glowC = new Color(1f, 0.92f, 0.66f, 1f);
            for (int depth = 0; depth < 12 && frontier.Count > 0; depth++)
            {
                var next = new List<int>();
                foreach (int c in frontier)
                {
                    if (!adj.TryGetValue(c, out var ns)) continue;
                    foreach (int nb in ns)
                    {
                        if (seen.Contains(nb)) continue;
                        seen.Add(nb); next.Add(nb);
                        StartCoroutine(Pulse(PlantPoint(c), PlantPoint(nb), glowC, fed.Contains(nb)));
                    }
                }
                frontier = next;
                yield return new WaitForSeconds(0.32f);
            }
        }

        private IEnumerator Pulse(Vector3 a, Vector3 b, Color c, bool fedHere)
        {
            for (float t = 0f; t < 0.32f; t += Time.deltaTime)
            {
                var p = Vector3.Lerp(a, b, t / 0.32f);
                p.y = IslandTerrain.Height(p.x, p.z) + 0.05f;
                Emit(p, c, 1);
                yield return null;
            }
            if (fedHere) Emit(b + Vector3.up * 0.25f, new Color(1f, 0.98f, 0.80f, 1f), 14);
        }
    }
}
