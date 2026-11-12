using System.Collections;
using System.Collections.Generic;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// The garden's small lives (ECOLOGY.md), drawn from Ecology.Presence between rolls and
    /// from the saved EcologyResult right after a roll. Presentation only: every resident
    /// shown here was decided by the domain step and saved in the journal.
    /// </summary>
    public sealed class GardenLife : MonoBehaviour
    {
        public GardenView View;
        public bool ReducedMotion;
        private Shader _lit, _sprite;
        private Texture2D _glow;

        private sealed class Flier
        {
            public Transform Root, L, R;
            public Vector3 Target, Velocity;
            public float Phase, Retarget, FlapSpeed, FlapAngle, Speed;
        }

        private EcosystemResidents _ecosystemResidents;
        private readonly List<Flier> _hoverflies = new List<Flier>();
        private readonly List<int> _hoverflyCells = new List<int>();
        private readonly List<Flier> _bees = new List<Flier>();
        private readonly List<Flier> _dragonflies = new List<Flier>();
        private readonly List<int> _beeCells = new List<int>();
        private readonly List<int> _puddleCells = new List<int>();
        private readonly List<GameObject> _mossGlows = new List<GameObject>();
        private readonly List<GameObject> _mushrooms = new List<GameObject>();
        private readonly List<Transform> _fish = new List<Transform>();
        private ParticleSystem _fireflies, _soil, _fluff, _petals, _dew, _seedGlints;
        private Transform _bird;
        private Vector3 _perch;
        private bool _birdBusy;
        private float _fishUntil;

        public void Build()
        {
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            _sprite = Shader.Find("Sprites/Default");
            _glow = ProceduralArt.GlowSprite();

            var bodyBee = Emissive(new Color(0.95f, 0.75f, 0.20f), new Color(0.35f, 0.24f, 0.04f));
            var beeWing = SpriteMat(ProceduralArt.ButterflyWing(new Color(0.92f, 0.97f, 1f), new Color(0.75f, 0.88f, 1f)), 0.75f, 3160);
            for (int i = 0; i < 3; i++) _bees.Add(MakeFlier("Bee" + i, bodyBee, beeWing, 0.55f, 38f, 40f, 1.4f, i));
            var bodyFly = Emissive(new Color(0.20f, 0.75f, 0.85f), new Color(0.05f, 0.45f, 0.55f));
            var flyWing = SpriteMat(ProceduralArt.ButterflyWing(new Color(0.85f, 0.97f, 1f), new Color(0.55f, 0.85f, 0.95f)), 0.55f, 3160);
            for (int i = 0; i < 2; i++) _dragonflies.Add(MakeFlier("Dragonfly" + i, bodyFly, flyWing, 0.95f, 30f, 25f, 2.4f, i + 5));

            _fireflies = Particles("Fireflies", 60, new Color(0.75f, 1f, 0.45f, 0.95f), 0.09f, 2.5f, 3330);
            var ffNoise = _fireflies.noise; ffNoise.enabled = true; ffNoise.strength = 0.5f; ffNoise.frequency = 0.6f;
            _soil = Particles("SoilLife", 60, new Color(0.85f, 0.70f, 0.45f, 0.8f), 0.05f, 1.2f, 3330);
            _fluff = Particles("Fluff", 50, new Color(1f, 1f, 1f, 0.85f), 0.08f, 6f, 3330);
            var fluffForce = _fluff.forceOverLifetime; fluffForce.enabled = true; fluffForce.x = 0.45f; fluffForce.y = 0.08f;
            _petals = Particles("Petals", 80, new Color(1f, 0.70f, 0.82f, 0.95f), 0.09f, 4.5f, 3330);
            var petalForce = _petals.forceOverLifetime; petalForce.enabled = true; petalForce.x = 0.6f; petalForce.y = -0.15f;
            _dew = Particles("Dew", 80, new Color(0.85f, 0.97f, 1f, 0.9f), 0.05f, 1.6f, 3330);
            _seedGlints = Particles("SeedGlints", 60, new Color(1f, 0.92f, 0.60f, 1f), 0.08f, 1.4f, 3340);

            var residents = new GameObject("EcosystemResidents");
            residents.transform.SetParent(transform, false);
            _ecosystemResidents = residents.AddComponent<EcosystemResidents>();
            for (int i = 0; i < 2; i++) _hoverflies.Add(MakeFlier("Hoverfly" + i, bodyBee, beeWing, 0.38f, 48f, 35f, 1.2f, i + 9));
            BuildBird();
            BuildFish();
        }

        private Material Emissive(Color baseColor, Color emission)
        {
            var m = new Material(_lit) { color = baseColor };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            return m;
        }

        private Material SpriteMat(Texture2D tex, float alpha, int queue)
        {
            var m = new Material(_sprite);
            m.SetTexture("_MainTex", tex);
            m.color = new Color(1f, 1f, 1f, alpha);
            m.renderQueue = queue;
            return m;
        }

        private Flier MakeFlier(string name, Material body, Material wing, float size, float flapSpeed, float flapAngle,
            float speed, int seed)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform);
            var b = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(root, false);
            b.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            b.transform.localScale = name.StartsWith("Dragon") ? new Vector3(0.03f, 0.16f, 0.03f) : new Vector3(0.06f, 0.06f, 0.06f);
            b.GetComponent<MeshRenderer>().material = body;
            b.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var mesh = WingMesh();
            Transform Wing(int side)
            {
                var w = new GameObject(side < 0 ? "L" : "R").transform;
                w.SetParent(root, false);
                w.localScale = new Vector3(side * 0.22f, 1f, name.StartsWith("Dragon") ? 0.07f : 0.14f);
                w.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = w.gameObject.AddComponent<MeshRenderer>();
                r.material = wing;
                r.shadowCastingMode = ShadowCastingMode.Off;
                return w;
            }
            root.localScale = Vector3.one * size;
            var f = new Flier { Root = root, L = Wing(-1), R = Wing(1), Phase = seed * 1.7f, FlapSpeed = flapSpeed, FlapAngle = flapAngle, Speed = speed };
            root.gameObject.SetActive(false);
            return f;
        }

        private static Mesh WingMesh()
        {
            var m = new Mesh { name = "LifeWing" };
            m.vertices = new[] { new Vector3(0, 0, -0.5f), new Vector3(1, 0, -0.5f), new Vector3(1, 0, 0.5f), new Vector3(0, 0, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            return m;
        }

        private ParticleSystem Particles(string name, int max, Color color, float size, float life, int queue)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform);
            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.3f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life * 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.25f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.35f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            obj.GetComponent<ParticleSystemRenderer>().material = SpriteMat(_glow, 1f, queue);
            return ps;
        }

        private void BuildBird()
        {
            _perch = View != null ? View.PerchPoint : new Vector3(7.2f, 2.95f, 16.4f);
            _bird = new GameObject("SmallBird").transform;
            _bird.SetParent(transform);
            _bird.position = _perch;
            var feather = new Material(_lit) { color = new Color(0.30f, 0.52f, 0.78f) };
            var breast = new Material(_lit) { color = new Color(0.98f, 0.78f, 0.55f) };
            var beak = Emissive(new Color(1f, 0.65f, 0.15f), new Color(0.4f, 0.2f, 0f));
            GameObject Part(PrimitiveType t, Material m, Vector3 p, Vector3 s)
            {
                var g = GameObject.CreatePrimitive(t);
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(_bird, false);
                g.transform.localPosition = p; g.transform.localScale = s;
                g.GetComponent<MeshRenderer>().material = m;
                g.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
                return g;
            }
            Part(PrimitiveType.Sphere, feather, Vector3.zero, new Vector3(0.24f, 0.22f, 0.32f));
            Part(PrimitiveType.Sphere, breast, new Vector3(0f, -0.02f, 0.07f), new Vector3(0.20f, 0.17f, 0.20f));
            Part(PrimitiveType.Sphere, feather, new Vector3(0f, 0.13f, 0.12f), Vector3.one * 0.15f);
            Part(PrimitiveType.Cube, beak, new Vector3(0f, 0.12f, 0.22f), new Vector3(0.04f, 0.035f, 0.08f));
            Part(PrimitiveType.Cube, feather, new Vector3(0f, 0.03f, -0.20f), new Vector3(0.10f, 0.03f, 0.16f));
            _bird.rotation = Quaternion.Euler(0f, 180f, 0f);
            _bird.gameObject.SetActive(false);
        }

        private void BuildFish()
        {
            var tex = FishTexture();
            for (int i = 0; i < 7; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "FishShadow";
                q.transform.SetParent(transform);
                q.transform.localScale = new Vector3(0.9f, 0.38f, 1f);
                q.GetComponent<MeshRenderer>().material = SpriteMat(tex, 0f, 2996);
                q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                _fish.Add(q.transform);
            }
        }

        private static Texture2D FishTexture()
        {
            const int w = 128, h = 64;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Fish", wrapMode = TextureWrapMode.Clamp };
            var c = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    float body = 1f - Mathf.Pow((u - 0.45f) / 0.36f, 2) - Mathf.Pow((v - 0.5f) / 0.26f, 2);
                    float tail = u > 0.78f ? 1f - Mathf.Abs(v - 0.5f) / (0.06f + (u - 0.78f) * 1.6f) : -1f;
                    float a = Mathf.Clamp01(Mathf.Max(body, tail) * 3f);
                    c[y * w + x] = new Color(0.55f, 0.95f, 1f, a * 0.85f);
                }
            t.SetPixels(c); t.Apply();
            return t;
        }

        /// <summary>Residents living in the garden now (from Ecology.Presence).</summary>
        public void ShowPresence(List<EcologyVisit> visits, WorldState state)
        {
            EcologyVisit Get(string k) => visits?.Find(v => v.Kind == k);
            _ecosystemResidents.Show(visits, state, View);
            _hoverflyCells.Clear();
            var hoverfly = Get(LivingEcosystem.Hoverfly);
            if (hoverfly != null) _hoverflyCells.AddRange(hoverfly.Cells);
            SetFliers(_hoverflies, hoverfly != null);
            _beeCells.Clear();
            var bee = Get(Ecology.Bee);
            if (bee != null) _beeCells.AddRange(bee.Cells);
            SetFliers(_bees, bee != null);
            _puddleCells.Clear();
            var df = Get(Ecology.Dragonfly);
            if (df != null) _puddleCells.AddRange(df.Cells);
            SetFliers(_dragonflies, df != null);

            foreach (var g in _mossGlows) Destroy(g);
            _mossGlows.Clear();
            var moss = Get(Ecology.Moss);
            if (moss != null)
                foreach (int c in moss.Cells)
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(q.GetComponent<Collider>());
                    q.transform.SetParent(transform);
                    q.transform.position = View.CellTop(c) + Vector3.up * 0.09f;
                    q.transform.rotation = Quaternion.Euler(90f, c * 41f, 0f);
                    q.transform.localScale = Vector3.one * 1.25f;
                    q.GetComponent<MeshRenderer>().material = SpriteMat(_glow, 0.55f, 3005);
                    q.GetComponent<MeshRenderer>().material.color = new Color(0.45f, 1f, 0.75f, 0.5f);
                    _mossGlows.Add(q);
                }

            foreach (var g in _mushrooms) Destroy(g);
            _mushrooms.Clear();
            var mush = Get(Ecology.Mushroom);
            if (mush != null)
                foreach (int c in mush.Cells) _mushrooms.Add(Mushroom(View.CellTop(c), c));

            var ff = Get(Ecology.Firefly);
            SetEmitter(_fireflies, ff != null, ff != null ? ff.Cells : null, 6f);
            var worm = Get(Ecology.Worm);
            SetEmitter(_soil, worm != null, worm != null ? worm.Cells : null, 2.5f);
            var fluff = Get(Ecology.Fluff);
            SetEmitter(_fluff, fluff != null, fluff != null ? fluff.Cells : null, 1.2f);
            var dew = Get(Ecology.Dew);
            var leafy = new List<int>();
            if (dew != null && state != null) foreach (var p in state.Plants) if (p.Stage >= PlantStage.Leaf) leafy.Add(p.CellId);
            SetEmitter(_dew, dew != null && leafy.Count > 0, leafy, 3f);

            bool bird = Get(Ecology.Bird) != null;
            if (!_birdBusy) _bird.gameObject.SetActive(bird);
        }

        private GameObject Mushroom(Vector3 top, int seed)
        {
            var root = new GameObject("Mushroom");
            root.transform.SetParent(transform);
            root.transform.position = top + new Vector3(((seed * 37) % 10 - 5) * 0.04f, 0f, ((seed * 53) % 10 - 5) * 0.04f);
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(stem.GetComponent<Collider>());
            stem.transform.SetParent(root.transform, false);
            stem.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            stem.transform.localScale = new Vector3(0.05f, 0.08f, 0.05f);
            stem.GetComponent<MeshRenderer>().material = new Material(_lit) { color = new Color(0.95f, 0.92f, 0.85f) };
            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(cap.GetComponent<Collider>());
            cap.transform.SetParent(root.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.17f, 0f);
            cap.transform.localScale = new Vector3(0.20f, 0.09f, 0.20f);
            cap.GetComponent<MeshRenderer>().material = Emissive(new Color(0.55f, 0.85f, 1f), new Color(0.25f, 0.75f, 1.1f));
            return root;
        }

        private void SetFliers(List<Flier> fliers, bool on)
        {
            foreach (var f in fliers)
            {
                if (on && !f.Root.gameObject.activeSelf)
                {
                    f.Root.position = new Vector3(7.5f + Random.Range(-3f, 3f), 2.5f, 7.5f + Random.Range(-3f, 3f));
                    f.Retarget = 0f;
                }
                f.Root.gameObject.SetActive(on);
            }
        }

        private void SetEmitter(ParticleSystem ps, bool on, List<int> cells, float rate)
        {
            var e = ps.emission;
            e.rateOverTime = 0f;
            _emitters[ps] = on && cells != null && cells.Count > 0 ? new KeyValuePair<List<int>, float>(new List<int>(cells), rate) : default;
        }

        private readonly Dictionary<ParticleSystem, KeyValuePair<List<int>, float>> _emitters =
            new Dictionary<ParticleSystem, KeyValuePair<List<int>, float>>();

        /// <summary>Right after a roll: seeds carried, nourishment glints, fish, petals.</summary>
        /// <summary>
        /// The moments the garden grows: each flower that opened in this roll bursts in its own colour and
        /// rings its own note; a seed that sprouted glints softly. Only events that were saved are shown,
        /// a few at a time so a busy roll stays quiet.
        /// </summary>
        public IEnumerator PlayGrowthMoments(RollOutcome o, GardenAudio audio)
        {
            if (o == null || o.Simulation == null) yield break;
            var events = new List<GardenEvent>(o.Simulation.Events);
            if (o.Ecology != null) events.AddRange(o.Ecology.Events);
            var post = o.Ecology != null ? o.Ecology.PostState : o.Simulation.PostState;
            int blooms = 0, sprouts = 0;
            foreach (var e in events)
            {
                if (e.Kind == "Bloomed" && blooms < 4)
                {
                    blooms++;
                    var plant = post.PlantAt(e.CellId);
                    string sp = plant != null ? plant.Species : null;
                    int idx = Mathf.Max(0, DestinyGarden.Domain.Flora.IndexOf(sp));
                    var c = PlantForms.BloomColor(sp);
                    if (audio != null) audio.PlayBloom(idx);
                    var at = View.CellTop(e.CellId) + Vector3.up * 0.5f;
                    _seedGlints.transform.position = at;
                    _seedGlints.Emit(new ParticleSystem.EmitParams { startColor = new Color(c.r, c.g, c.b, 1f) }, 22);
                    _petals.transform.position = at;
                    _petals.Emit(new ParticleSystem.EmitParams { startColor = new Color(c.r, c.g, c.b, 0.95f) }, 9);
                    yield return new WaitForSeconds(ReducedMotion ? 0.05f : 0.24f);
                }
                else if (e.Kind == "Germinated" && sprouts < 2)
                {
                    sprouts++;
                    var plant = post.PlantAt(e.CellId);
                    int idx = Mathf.Max(0, DestinyGarden.Domain.Flora.IndexOf(plant != null ? plant.Species : null));
                    if (audio != null && blooms == 0) audio.PlaySprout(idx);
                    _seedGlints.transform.position = View.CellTop(e.CellId) + Vector3.up * 0.2f;
                    _seedGlints.Emit(new ParticleSystem.EmitParams { startColor = new Color(0.8f, 1f, 0.7f, 1f) }, 7);
                }
            }
        }

        public IEnumerator PlayRollEffects(EcologyResult eco)
        {
            if (eco == null) yield break;
            foreach (var e in eco.Effects)
            {
                var top = View.CellTop(e.CellId);
                if (e.Seed)
                {
                    if (e.Kind == Ecology.Bird) StartCoroutine(BirdDrop(top));
                    else StartCoroutine(SeedFall(top, e.Kind));
                }
                else if (e.Amount > 0 && e.Kind != Ecology.Spirit)
                {
                    _seedGlints.transform.position = top + Vector3.up * 0.4f;
                    _seedGlints.Emit(new ParticleSystem.EmitParams
                    {
                        startColor = e.Kind == Ecology.Firefly ? new Color(0.75f, 1f, 0.45f)
                            : e.Kind == Ecology.Mushroom ? new Color(0.55f, 0.9f, 1f) : new Color(0.95f, 0.8f, 0.5f)
                    }, 6);
                }
            }
            if (eco.Has(Ecology.Fish)) _fishUntil = Time.time + 14f;
            if (eco.Has(Ecology.Petals) && View != null)
            {
                foreach (var v in eco.Visits)
                    if (v.Kind == Ecology.Bee)
                        foreach (int c in v.Cells)
                        {
                            _petals.transform.position = View.CellTop(c) + Vector3.up * 1.0f;
                            _petals.Emit(10);
                        }
            }
            yield return null;
        }

        /// <summary>The die sows: each seed glides from the die to its cell and lands with a glint.</summary>
        public IEnumerator PlaySowing(Vector3 from, List<Vector3> tops, System.Action onLand)
        {
            for (int i = 0; i < tops.Count; i++)
            {
                StartCoroutine(Sow(from, tops[i], onLand));
                yield return new WaitForSeconds(0.25f);
            }
        }

        private IEnumerator Sow(Vector3 from, Vector3 top, System.Action onLand)
        {
            float time = ReducedMotion ? 0.25f : 1.1f;
            float lift = 1.6f + Vector3.Distance(from, top) * 0.12f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / time);
                _seedGlints.transform.position = Vector3.Lerp(from, top, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * lift;
                _seedGlints.Emit(new ParticleSystem.EmitParams { startColor = new Color(1f, 0.90f, 0.62f, 0.95f) }, 1);
                yield return null;
            }
            _seedGlints.transform.position = top + Vector3.up * 0.12f;
            _seedGlints.Emit(new ParticleSystem.EmitParams { startColor = new Color(1f, 0.95f, 0.75f, 1f) }, 16);
            onLand?.Invoke();
        }

        private IEnumerator SeedFall(Vector3 top, string carrier)
        {
            Vector3 start = top + new Vector3(carrier == Ecology.Fluff ? -3f : Random.Range(-1f, 1f), 2.4f, Random.Range(-0.6f, 0.6f));
            float time = ReducedMotion ? 0.3f : 1.8f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                _seedGlints.transform.position = Vector3.Lerp(start, top, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.6f;
                _seedGlints.Emit(1);
                yield return new WaitForSeconds(0.05f);
            }
            _seedGlints.transform.position = top + Vector3.up * 0.15f;
            _seedGlints.Emit(10);
        }

        private IEnumerator BirdDrop(Vector3 top)
        {
            _birdBusy = true;
            _bird.gameObject.SetActive(true);
            Vector3 from = _bird.position, hover = top + Vector3.up * 1.4f;
            float fly = ReducedMotion ? 0.3f : 1.6f;
            for (float t = 0f; t < fly; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / fly);
                _bird.position = Vector3.Lerp(from, hover, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.2f;
                _bird.rotation = Quaternion.LookRotation(new Vector3(hover.x - from.x, 0f, hover.z - from.z).normalized + Vector3.forward * 0.001f);
                yield return null;
            }
            StartCoroutine(SeedFall(top, Ecology.Bird));
            yield return new WaitForSeconds(0.4f);
            for (float t = 0f; t < fly; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / fly);
                _bird.position = Vector3.Lerp(hover, _perch, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.2f;
                yield return null;
            }
            _bird.position = _perch;
            _bird.rotation = Quaternion.Euler(0f, 180f, 0f);
            _birdBusy = false;
        }

        private void Update()
        {
            float t = Time.time, dt = Time.deltaTime;
            if (!ReducedMotion)
            {
                UpdateFliers(_bees, _beeCells, 0.55f, t, dt, true);
                UpdateFliers(_hoverflies, _hoverflyCells, 0.5f, t, dt, false);
                UpdateFliers(_dragonflies, _puddleCells, 0.9f, t, dt, false);
            }
            foreach (var kv in _emitters)
            {
                if (kv.Value.Key == null || kv.Value.Key.Count == 0) continue;
                if (Random.value < kv.Value.Value * dt)
                {
                    int c = kv.Value.Key[Random.Range(0, kv.Value.Key.Count)];
                    kv.Key.transform.position = View.CellTop(c) + Vector3.up * (kv.Key == _fireflies ? Random.Range(0.4f, 1.4f) : 0.25f);
                    kv.Key.Emit(1);
                }
            }
            for (int i = 0; i < _mossGlows.Count; i++)
            {
                var m = _mossGlows[i].GetComponent<MeshRenderer>().material;
                m.color = new Color(0.45f, 1f, 0.75f, 0.35f + 0.2f * Mathf.Sin(t * 1.3f + i));
            }
            if (_bird.gameObject.activeSelf && !_birdBusy && !ReducedMotion)
                _bird.position = _perch + Vector3.up * Mathf.Abs(Mathf.Sin(t * 2.4f)) * (Mathf.Repeat(t, 5f) < 0.8f ? 0.08f : 0f);
            float fishAlpha = Mathf.Clamp01((_fishUntil - t) / 3f) * 0.55f;
            for (int i = 0; i < _fish.Count; i++)
            {
                float a = t * (0.10f + i * 0.012f) + i * 0.9f;
                float r = 10.8f + (i % 3) * 1.4f;
                var pos = new Vector3(7.5f + Mathf.Cos(a) * r, -0.77f, 7.5f + Mathf.Sin(a) * r);
                _fish[i].position = pos;
                _fish[i].rotation = Quaternion.Euler(90f, -a * Mathf.Rad2Deg, 0f);
                _fish[i].GetComponent<MeshRenderer>().material.color = new Color(1f, 1f, 1f, fishAlpha);
            }
        }

        private void UpdateFliers(List<Flier> fliers, List<int> cells, float height, float t, float dt, bool landOnTop)
        {
            foreach (var f in fliers)
            {
                if (!f.Root.gameObject.activeSelf || cells.Count == 0) continue;
                if (t > f.Retarget)
                {
                    f.Retarget = t + Random.Range(1.2f, 3.0f);
                    f.Target = View.CellTop(cells[Random.Range(0, cells.Count)]) +
                               new Vector3(Random.Range(-0.3f, 0.3f), height + (landOnTop ? 0.5f : 0f), Random.Range(-0.3f, 0.3f));
                }
                Vector3 to = f.Target - f.Root.position;
                Vector3 wobble = new Vector3(Mathf.Sin(t * 5f + f.Phase), Mathf.Sin(t * 7f + f.Phase) * 0.4f, Mathf.Cos(t * 4.3f + f.Phase)) * 0.5f;
                Vector3 desired = to.normalized * Mathf.Min(f.Speed, to.magnitude * 2f) + wobble;
                f.Velocity = Vector3.Lerp(f.Velocity, desired, dt * 3f);
                f.Root.position += f.Velocity * dt;
                var flat = new Vector3(f.Velocity.x, 0f, f.Velocity.z);
                if (flat.sqrMagnitude > 0.0004f) f.Root.rotation = Quaternion.Slerp(f.Root.rotation, Quaternion.LookRotation(flat), dt * 6f);
                float flap = Mathf.Sin(t * f.FlapSpeed + f.Phase) * f.FlapAngle;
                f.L.localRotation = Quaternion.Euler(0f, 0f, flap);
                f.R.localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
        }
    }
}
