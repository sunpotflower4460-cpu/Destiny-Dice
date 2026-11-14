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
            public LifeModels.Winged Rig;
            public Renderer Lamp, Aura;
            public Vector3 Velocity;
            public float Phase;
            public Transform Root => Rig.Root;
        }

        private EcosystemResidents _ecosystemResidents;
        private readonly List<Flier> _hoverflies = new List<Flier>();
        private readonly List<Vector3> _hoverflySpots = new List<Vector3>();
        private LifeModels _models;
        private readonly List<Flier> _bees = new List<Flier>();
        private readonly List<Flier> _dragonflies = new List<Flier>();
        private readonly List<Flier> _fireflies = new List<Flier>();
        private readonly List<Vector3> _beeSpots = new List<Vector3>();
        private readonly List<Vector3> _puddleSpots = new List<Vector3>();
        private readonly List<Vector3> _mossSpots = new List<Vector3>();
        private readonly Transform[] _worms = new Transform[4];
        private readonly Vector3[] _wormAnchors = new Vector3[4];
        private readonly List<LifeModels.Fish> _fish = new List<LifeModels.Fish>();
        private MaterialPropertyBlock _tint;
        private static readonly int TintId = Shader.PropertyToID("_Color");
        private Mesh _mossSurface;
        private Material _mossMaterial;
        private Material _mushroomStem, _mushroomCap;
        private readonly List<GameObject> _mushrooms = new List<GameObject>();
        private ParticleSystem _fluff, _petals, _dew, _seedGlints;
        private LifeModels.Bird _bird;
        private Vector3 _perch;
        private float _perchYaw;
        private bool _birdPresent, _birdBusy;
        private float _fishSince = float.NegativeInfinity;

        public void Build()
        {
            _tint = new MaterialPropertyBlock();
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            _sprite = Shader.Find("Sprites/Default");
            _models = new LifeModels();
            _glow = _models.Keep(ProceduralArt.GlowSprite());

            var moss = new GameObject("Glowing Moss");
            moss.transform.SetParent(transform, false);
            _mossSurface = _models.Keep(new Mesh { name = "Glowing Moss" });
            moss.AddComponent<MeshFilter>().sharedMesh = _mossSurface;
            _mossMaterial = _models.Keep(IslandHarmony.GlowMaterial(null, new Color(0.30f, 0.65f, 0.40f, 0.14f), 3005));
            var mossRenderer = moss.AddComponent<MeshRenderer>();
            mossRenderer.sharedMaterial = _mossMaterial;
            mossRenderer.shadowCastingMode = ShadowCastingMode.Off;
            mossRenderer.receiveShadows = false;

            MakeFliers(_bees, LifeModels.Insect.Bee, 3);
            MakeFliers(_dragonflies, LifeModels.Insect.Dragonfly, 2);
            MakeFliers(_fireflies, LifeModels.Insect.Firefly, 5);
            for (int i = 0; i < _worms.Length; i++) _worms[i] = _models.MakeWorm(transform, i);

            _fluff = Particles("Fluff", 50, new Color(1f, 1f, 1f, 0.85f), 0.08f, 6f, 3330);
            var fluffForce = _fluff.forceOverLifetime; fluffForce.enabled = true; fluffForce.x = 0.45f; fluffForce.y = 0.08f;
            _petals = Particles("Petals", 80, new Color(1f, 0.70f, 0.82f, 0.95f), 0.09f, 4.5f, 3330);
            var petalForce = _petals.forceOverLifetime; petalForce.enabled = true; petalForce.x = 0.6f; petalForce.y = -0.15f;
            _dew = Particles("Dew", 80, new Color(0.85f, 0.97f, 1f, 0.9f), 0.05f, 1.6f, 3330);
            _seedGlints = Particles("SeedGlints", 60, new Color(1f, 0.92f, 0.60f, 1f), 0.08f, 1.4f, 3340);

            var residents = new GameObject("EcosystemResidents");
            residents.transform.SetParent(transform, false);
            _ecosystemResidents = residents.AddComponent<EcosystemResidents>();
            MakeFliers(_hoverflies, LifeModels.Insect.Hoverfly, 2);
            _perch = View.PerchPoint + Vector3.up * 0.006f;
            _perchYaw = Quaternion.LookRotation(View.PerchHeading).eulerAngles.y;
            _bird = _models.MakeBird(transform);
            SetBirdOnPerch(0f);
            for (int i = 0; i < 4; i++) _fish.Add(_models.MakeFish(transform, i));
        }

        private Material Emissive(Color baseColor, Color emission)
        {
            var m = _models.Keep(new Material(_lit) { color = baseColor });
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            return m;
        }

        private Material SpriteMat(Texture2D tex, float alpha, int queue)
        {
            var m = _models.Keep(new Material(_sprite));
            m.SetTexture("_MainTex", tex);
            m.color = new Color(1f, 1f, 1f, alpha);
            m.renderQueue = queue;
            return m;
        }

        private void MakeFliers(List<Flier> fliers, LifeModels.Insect insect, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var rig = _models.MakeInsect(transform, insect, i);
                rig.Root.localScale = Vector3.one * (0.72f + 0.05f * (i % 3));
                if (insect == LifeModels.Insect.Hoverfly) rig.Root.localScale *= 0.64f;
                var flier = new Flier { Rig = rig, Phase = i * 2.39996f + (int)insect * 0.7f };
                if (insect == LifeModels.Insect.Firefly)
                    (flier.Lamp, flier.Aura) = _models.AddLantern(rig.Root);
                fliers.Add(flier);
            }
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
            obj.GetComponent<ParticleSystemRenderer>().sharedMaterial = SpriteMat(_glow, 1f, queue);
            return ps;
        }

        /// <summary>Residents living in the garden now (from Ecology.Presence).</summary>
        public void ShowPresence(List<EcologyVisit> visits, WorldState state)
        {
            EcologyVisit Get(string k) => visits?.Find(v => v.Kind == k);
            _ecosystemResidents.Show(visits, state, View);
            SetFliers(_hoverflies, _hoverflySpots, Get(LivingEcosystem.Hoverfly), 0.21f, state);
            SetFliers(_bees, _beeSpots, Get(Ecology.Bee), 0.15f, state);
            SetFliers(_dragonflies, _puddleSpots, Get(Ecology.Dragonfly), 0.78f);
            SetFliers(_fireflies, _mossSpots, Get(Ecology.Firefly), 0.40f);
            SetWorms(Get(Ecology.Worm));

            ShowGlowingMoss(Get(Ecology.Moss));

            foreach (var g in _mushrooms) Destroy(g);
            _mushrooms.Clear();
            var mush = Get(Ecology.Mushroom);
            if (mush != null)
                foreach (int c in mush.Cells) _mushrooms.Add(Mushroom(View.CellTop(c), c));

            var fluff = Get(Ecology.Fluff);
            SetEmitter(_fluff, fluff != null, fluff != null ? fluff.Cells : null, 1.2f);
            var dew = Get(Ecology.Dew);
            var leafy = new List<int>();
            if (dew != null && state != null) foreach (var p in state.Plants) if (p.Stage >= PlantStage.Leaf) leafy.Add(p.CellId);
            SetEmitter(_dew, dew != null && leafy.Count > 0, leafy, 3f);

            _birdPresent = Get(Ecology.Bird) != null;
            if (!_birdBusy) _bird.Root.gameObject.SetActive(_birdPresent);
        }

        private void ShowGlowingMoss(EcologyVisit visit)
        {
            var mesh = _mossSurface;
            mesh.Clear();
            if (visit == null || visit.Cells.Count == 0) return;

            var sites = new List<Vector2>(visit.Cells.Count);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (int cell in visit.Cells)
            {
                Vector3 top = View.CellTop(cell);
                var p = new Vector2(top.x, top.z);
                sites.Add(p);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            // Blend nearby patches once so overlapping habitats do not create brighter spots.
            const float step = 0.20f, spread = 0.92f;
            min -= Vector2.one * spread;
            max += Vector2.one * spread;
            int columns = Mathf.CeilToInt((max.x - min.x) / step) + 1;
            int rows = Mathf.CeilToInt((max.y - min.y) / step) + 1;
            var vertices = new List<Vector3>(columns * rows);
            var colors = new List<Color>(columns * rows);
            var indices = new List<int>((columns - 1) * (rows - 1) * 6);
            for (int z = 0; z < rows; z++)
                for (int x = 0; x < columns; x++)
                {
                    var p = min + new Vector2(x, z) * step;
                    float coverage = 0f;
                    foreach (var site in sites)
                        coverage += 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, spread, Vector2.Distance(site, p)));
                    float grain = ProceduralArt.Fbm(p.x * 3.8f, p.y * 5.2f, 470, 2);
                    float alpha = Mathf.Clamp01(coverage * (0.55f + grain * 0.32f));
                    vertices.Add(new Vector3(p.x, IslandTerrain.Height(p.x, p.y) + 0.06f, p.y));
                    colors.Add(new Color(1f, 1f, 1f, alpha));
                    if (x == 0 || z == 0) continue;
                    int d = vertices.Count - 1;
                    int c = d - 1, b = d - columns, a = b - 1;
                    indices.Add(a); indices.Add(c); indices.Add(b);
                    indices.Add(b); indices.Add(c); indices.Add(d);
                }
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
        }

        private void OnDestroy()
        {
            _models?.Dispose();
        }

        private GameObject Mushroom(Vector3 top, int seed)
        {
            if (_mushroomStem == null)
            {
                _mushroomStem = _models.Keep(new Material(_lit) { color = new Color(0.95f, 0.92f, 0.85f) });
                _mushroomCap = Emissive(new Color(0.55f, 0.85f, 1f), new Color(0.25f, 0.75f, 1.1f));
            }
            var root = new GameObject("Mushroom");
            root.transform.SetParent(transform);
            root.transform.position = top + new Vector3(((seed * 37) % 10 - 5) * 0.04f, 0f, ((seed * 53) % 10 - 5) * 0.04f);
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(stem.GetComponent<Collider>());
            stem.transform.SetParent(root.transform, false);
            stem.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            stem.transform.localScale = new Vector3(0.05f, 0.08f, 0.05f);
            stem.GetComponent<MeshRenderer>().sharedMaterial = _mushroomStem;
            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(cap.GetComponent<Collider>());
            cap.transform.SetParent(root.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.17f, 0f);
            cap.transform.localScale = new Vector3(0.20f, 0.09f, 0.20f);
            cap.GetComponent<MeshRenderer>().sharedMaterial = _mushroomCap;
            return root;
        }

        private void SetFliers(List<Flier> fliers, List<Vector3> spots, EcologyVisit visit, float height, WorldState plants = null)
        {
            spots.Clear();
            if (visit != null)
                foreach (int cell in visit.Cells)
                {
                    var plant = plants?.PlantAt(cell);
                    Vector3 top = plant != null ? View.PlantTip(plant) : View.CellTop(cell);
                    spots.Add(top + Vector3.up * height);
                }

            for (int i = 0; i < fliers.Count; i++)
            {
                var f = fliers[i];
                bool present = i < spots.Count && i < (f.Lamp == null ? 2 : 3);
                if (present && (!f.Root.gameObject.activeSelf || ReducedMotion))
                {
                    f.Root.position = spots[i % spots.Count] + new Vector3(Mathf.Cos(f.Phase) * 0.24f, 0.18f, Mathf.Sin(f.Phase) * 0.24f);
                    f.Root.rotation = Quaternion.Euler(0f, f.Phase * Mathf.Rad2Deg, 0f);
                    f.Velocity = Vector3.zero;
                    f.Rig.Flap(13f, 6f);
                }
                f.Root.gameObject.SetActive(present);
            }
        }

        private void SetWorms(EcologyVisit visit)
        {
            for (int i = 0; i < _worms.Length; i++)
            {
                var worm = _worms[i];
                bool present = visit != null && i < visit.Cells.Count;
                if (present)
                {
                    int cell = visit.Cells[i];
                    float angle = cell * 2.39996f;
                    Vector3 point = View.CellTop(cell) + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.34f;
                    point.y = IslandTerrain.Height(point.x, point.z) + 0.008f;
                    _wormAnchors[i] = point;
                    worm.SetPositionAndRotation(point, Quaternion.Euler(0f, 18f - angle * Mathf.Rad2Deg, 0f));
                }
                worm.gameObject.SetActive(present);
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

        /// <summary>
        /// The moments the garden grows: each flower that opened in this roll bursts in its own colour and
        /// rings its own note; a seed that sprouted glints softly. Only events that were saved are shown,
        /// a few at a time so a busy roll stays quiet.
        /// </summary>
        public IEnumerator PlayGrowthMoments(RollOutcome o, GardenAudio audio)
            => PlayGrowthMoments(o, audio, GardenMoments.First(GardenMoments.FromOutcome(o)));

        internal IEnumerator PlayGrowthMoments(RollOutcome o, GardenAudio audio, GardenMoment focus)
        {
            if (focus?.Kind != "Bloomed" && focus?.Kind != "Germinated" && focus?.Kind != "seed-awakened") yield break;
            var plant = (o?.Ecology?.PostState ?? o?.Simulation?.PostState)?.PlantAt(focus.CellId);
            if (plant == null) yield break;
            int species = Mathf.Max(0, DestinyGarden.Domain.Flora.IndexOf(plant.Species));
            var at = View.PlantTip(plant);
            _seedGlints.transform.position = at;
            if (focus.Kind == "Bloomed")
            {
                audio?.PlayBloom(species);
                if (ReducedMotion) yield break;
                var c = PlantForms.BloomColor(plant.Species);
                _seedGlints.Emit(new ParticleSystem.EmitParams { startColor = new Color(c.r, c.g, c.b, 0.87f) }, 12);
                _petals.transform.position = at;
                _petals.Emit(new ParticleSystem.EmitParams { startColor = new Color(c.r, c.g, c.b, 0.83f) }, 6);
            }
            else
            {
                audio?.PlaySprout(species);
                if (!ReducedMotion) _seedGlints.Emit(new ParticleSystem.EmitParams { startColor = new Color(0.8f, 1f, 0.7f, 1f) }, 7);
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
                    if (e.Kind != SuccessionRules.SeedBank) ShowSeedArrival(top, e.Kind);
                }
                else if (e.Amount > 0 && e.Kind != Ecology.Spirit && e.Kind != PrayerStar.GiftKind)
                {
                    _seedGlints.transform.position = top + Vector3.up * 0.4f;
                    _seedGlints.Emit(new ParticleSystem.EmitParams
                    {
                        startColor = e.Kind == Ecology.Firefly ? new Color(0.75f, 1f, 0.45f)
                            : e.Kind == Ecology.Mushroom ? new Color(0.55f, 0.9f, 1f) : new Color(0.95f, 0.8f, 0.5f)
                    }, 6);
                }
            }
            foreach (var link in eco.Relations)
                if (link.Kind == "clonal-rooted")
                {
                    Vector3 from = View.CellTop(link.FromCell), to = View.CellTop(link.ToCell);
                    for (int k = 0; k < 6; k++)
                    {
                        _seedGlints.transform.position = Vector3.Lerp(from, to, k / 5f) + Vector3.up * 0.08f;
                        _seedGlints.Emit(new ParticleSystem.EmitParams { startColor = new Color(0.60f, 0.88f, 0.55f, 0.75f) }, 1);
                    }
                }
            foreach (var link in eco.Relations)
                if (link.Kind == "resident-emerged" || link.Kind == "resident-settled" || link.Kind == "refuge-moved")
                {
                    _seedGlints.transform.position = View.CellTop(link.ToCell) + Vector3.up * 0.25f;
                    _seedGlints.Emit(new ParticleSystem.EmitParams { startColor = new Color(0.72f, 0.94f, 0.82f, 0.85f) }, 7);
                }
            foreach (var link in eco.Relations)
                if (link.Kind == "seed-buried")
                    ShowSeedArrival(View.CellTop(link.ToCell), link.Actor);
            if (eco.Has(Ecology.Fish)) _fishSince = Time.time;
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

        private IEnumerator SeedFall(Vector3 top, string carrier, Vector3? origin = null)
        {
            Vector3 start = origin ?? top + new Vector3(carrier == Ecology.Fluff ? -3f : Random.Range(-1f, 1f), 2.4f, Random.Range(-0.6f, 0.6f));
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

        private void ShowSeedArrival(Vector3 top, string carrier)
        {
            if (carrier == Ecology.Bird && !_birdBusy && !ReducedMotion) StartCoroutine(BirdDrop(top));
            else StartCoroutine(SeedFall(top, carrier));
        }

        private IEnumerator BirdDrop(Vector3 top)
        {
            _birdBusy = true;
            _bird.Root.gameObject.SetActive(true);
            Vector3 hover = top + Vector3.up * 1.45f;
            yield return BirdFlight(_bird.Root.position, hover, 1.7f, false);
            StartCoroutine(SeedFall(top, Ecology.Bird, _bird.Head.TransformPoint(new Vector3(0f, 0f, 0.17f))));
            for (float t = 0f; t < 0.34f && !ReducedMotion; t += Time.deltaTime)
            {
                _bird.Wings.Flap(Mathf.Sin(t * 39f) * 38f, 0f);
                _bird.Head.localRotation = Quaternion.Euler(18f, 0f, 0f);
                yield return null;
            }
            yield return BirdFlight(hover, _perch, 1.55f, true);
            SetBirdOnPerch(0f);
            _birdBusy = false;
            _bird.Root.gameObject.SetActive(_birdPresent);
        }

        private IEnumerator BirdFlight(Vector3 from, Vector3 to, float duration, bool landing)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            Quaternion facing = Quaternion.LookRotation(direction.normalized + Vector3.forward * 0.001f);
            for (float t = 0f; t < duration && !ReducedMotion; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                float height = Mathf.Sin(k * Mathf.PI) * (landing ? 1.02f : 0.88f);
                _bird.Root.position = Vector3.Lerp(from, to, k) + Vector3.up * height;
                float pitch = Mathf.Cos(k * Mathf.PI) * -17f;
                _bird.Root.rotation = Quaternion.Slerp(_bird.Root.rotation, facing * Quaternion.Euler(pitch, 0f, 0f), Time.deltaTime * 12f);
                float fold = landing ? Mathf.InverseLerp(0.83f, 1f, k) : 1f - Mathf.InverseLerp(0f, 0.13f, k);
                float amplitude = landing ? 30f + 21f * Mathf.Sin(k * Mathf.PI) : 49f;
                _bird.Wings.Flap(Mathf.Lerp(Mathf.Sin(t * 37f) * amplitude, -65f, fold), 0f, fold * 76f);
                _bird.Tail.localRotation = Quaternion.Euler(pitch * -0.8f, 0f, 0f);
                _bird.Head.localRotation = Quaternion.Euler(-pitch * 0.30f, 0f, 0f);
                yield return null;
            }
            _bird.Root.SetPositionAndRotation(to, facing);
        }

        private void SetBirdOnPerch(float time)
        {
            float look = Mathf.Sin(time * 0.86f) * 29f + Mathf.Sin(time * 2.2f) * 8f;
            _bird.Root.SetPositionAndRotation(_perch, Quaternion.Euler(0f, _perchYaw, 0f));
            _bird.Head.localRotation = Quaternion.Euler(Mathf.Sin(time * 1.5f) * 6f, look, Mathf.Sin(time * 0.6f) * 8f);
            _bird.Tail.localRotation = Quaternion.Euler(4f + Mathf.Sin(time * 2.25f) * 5f, 0f, 0f);
            _bird.Wings.Flap(-67f, 0f, 78f);
        }

        private void Update()
        {
            if (_models == null) return;
            float t = Time.time, dt = Time.deltaTime;
            UpdateFliers(_bees, _beeSpots, LifeModels.Insect.Bee, t, dt);
            UpdateFliers(_hoverflies, _hoverflySpots, LifeModels.Insect.Hoverfly, t, dt);
            UpdateFliers(_dragonflies, _puddleSpots, LifeModels.Insect.Dragonfly, t, dt);
            UpdateFliers(_fireflies, _mossSpots, LifeModels.Insect.Firefly, t, dt);
            UpdateWorms(t);
            UpdateFish(t);
            if (!ReducedMotion)
                foreach (var kv in _emitters)
                {
                    if (kv.Value.Key == null || kv.Value.Key.Count == 0 || Random.value >= kv.Value.Value * dt) continue;
                    int cell = kv.Value.Key[Random.Range(0, kv.Value.Key.Count)];
                    kv.Key.transform.position = View.CellTop(cell) + Vector3.up * 0.25f;
                    kv.Key.Emit(1);
                }
            if (_mossMaterial != null)
            {
                float glow = Mathf.Lerp(0.095f, 0.23f, View.Sky.Night);
                _mossMaterial.color = new Color(0.30f, 0.65f, 0.40f,
                    glow * (ReducedMotion ? 1f : 0.94f + 0.06f * Mathf.Sin(t * 0.7f)));
            }
            if (_bird.Root.gameObject.activeSelf && !_birdBusy) SetBirdOnPerch(ReducedMotion ? 0f : t);
        }

        private void Tint(Renderer renderer, Color color)
        {
            _tint.SetColor(TintId, color);
            renderer.SetPropertyBlock(_tint);
        }

        private void UpdateWorms(float t)
        {
            for (int i = 0; i < _worms.Length; i++)
            {
                var worm = _worms[i];
                if (!worm.gameObject.activeSelf) continue;
                float phase = ReducedMotion ? 0f : Mathf.Sin(t * 2.35f + i * 1.98f);
                var pos = _wormAnchors[i] + worm.right * (phase * 0.062f);
                pos.y = IslandTerrain.Height(pos.x, pos.z) + 0.005f;
                worm.position = pos;
                worm.localScale = new Vector3(1f + phase * 0.16f, 1f - phase * 0.14f, 1f - phase * 0.10f);
            }
        }

        private void UpdateFish(float time)
        {
            float age = time - _fishSince;
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 1.1f)) * Mathf.Clamp01((14f - age) / 3f) * 0.58f;
            bool visible = alpha > 0.005f;
            float travel = ReducedMotion ? 0f : Mathf.Min(age, 14f);
            for (int i = 0; i < _fish.Count; i++)
            {
                var fish = _fish[i];
                if (fish.Root.gameObject.activeSelf != visible) fish.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                float a = IslandTerrain.NearBayAngle - 0.75f + 0.21f * i + travel * (0.14f + i * 0.012f);
                float off = 1.15f + (i % 2) * 0.76f + Mathf.Sin(travel * 0.8f + i) * 0.33f;
                Vector2 at = IslandSeed.At(a, IslandTerrain.ShoreRadius(a) + off);
                Vector2 ahead = IslandSeed.At(a + 0.04f, IslandTerrain.ShoreRadius(a + 0.04f) + off);
                float yaw = Mathf.Atan2(ahead.x - at.x, ahead.y - at.y) * Mathf.Rad2Deg;
                float tailBeat = Mathf.Sin(travel * 11f + i * 1.71f);
                // Lay the tinted silhouette just above the opaque lagoon so it reads through the surface.
                fish.Root.SetPositionAndRotation(new Vector3(at.x, IslandTerrain.SeaLevel + 0.032f + 0.006f * i, at.y),
                    Quaternion.Euler(0f, yaw + tailBeat * 3f, 0f));
                fish.Root.localScale = new Vector3(0.92f + i * 0.12f, 0.13f, 0.92f + i * 0.12f);
                fish.Tail.localRotation = Quaternion.Euler(0f, tailBeat * 26f, 0f);
                var water = Color.Lerp(new Color(0.69f, 0.93f, 0.91f, alpha), new Color(0.51f, 0.78f, 0.86f, alpha * 0.72f), View.Sky.Night);
                Tint(fish.BodyPaint, water);
                Tint(fish.TailPaint, water);
            }
        }

        private void UpdateFliers(List<Flier> fliers, List<Vector3> spots, LifeModels.Insect insect, float t, float dt)
        {
            if (spots.Count == 0) return;
            bool bee = insect == LifeModels.Insect.Bee;
            bool dragonfly = insect == LifeModels.Insect.Dragonfly;
            bool hoverfly = insect == LifeModels.Insect.Hoverfly;
            for (int i = 0; i < fliers.Count; i++)
            {
                var f = fliers[i];
                if (!f.Root.gameObject.activeSelf) continue;
                if (!ReducedMotion)
                {
                    float cycle = t * (bee ? 0.24f : dragonfly ? 0.37f : 0.13f) + i * 0.61f;
                    int site = (Mathf.FloorToInt(cycle) + i) % spots.Count;
                    float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Repeat(cycle, 1f) - 0.30f) / 0.62f));
                    float phase = t * (dragonfly ? 4.4f : bee ? 5.4f : 1.7f) + f.Phase;
                    float radius = bee || hoverfly ? 0.17f : dragonfly ? 0.31f : 0.36f;
                    Vector3 hover = new Vector3(Mathf.Sin(phase) * radius, 0.11f + 0.09f * Mathf.Sin(phase * 1.47f), Mathf.Cos(phase * 0.87f) * radius);
                    if (bee) hover.y += Mathf.Sin(progress * Mathf.PI) * 0.42f;
                    Vector3 target = Vector3.Lerp(spots[site], spots[(site + 1) % spots.Count], progress) + hover;
                    target.y = Mathf.Max(target.y, IslandTerrain.Height(target.x, target.z) + 0.23f);
                    Vector3 to = target - f.Root.position;
                    Vector3 desired = Vector3.ClampMagnitude(to * (dragonfly ? 5.3f : 3.1f), dragonfly ? 4.8f : bee ? 2.3f : 1.0f);
                    f.Velocity = Vector3.Lerp(f.Velocity, desired, dt * (dragonfly ? 9f : 5f));
                    f.Root.position += f.Velocity * dt;
                    Vector3 heading = new Vector3(f.Velocity.x, f.Velocity.y * 0.32f, f.Velocity.z);
                    if (heading.sqrMagnitude > 0.021f)
                        f.Root.rotation = Quaternion.Slerp(f.Root.rotation, Quaternion.LookRotation(heading) * Quaternion.Euler(0f, 0f, Mathf.Sin(phase) * 8f), dt * 7f);
                    float beat = t * (bee || hoverfly ? 53f : dragonfly ? 41f : 35f) + f.Phase;
                    f.Rig.Flap(Mathf.Sin(beat) * (bee ? 40f : 23f) + 11f,
                        Mathf.Sin(beat - (dragonfly ? 1.38f : 0.25f)) * 24f + 10f, bee ? 12f : 0f);
                }
                else f.Rig.Flap(18f, 7f, bee ? 13f : 0f);
                if (f.Lamp == null) continue;
                float pulse = Mathf.Repeat(t * 0.38f + i * 0.29f, 1f);
                float glimmer = ReducedMotion ? 0.64f : Mathf.Sin(Mathf.PI * Mathf.Clamp01(pulse * 2.25f));
                float night = Mathf.Lerp(0.50f, 1f, View.Sky.Night);
                Tint(f.Lamp, new Color(0.76f, 0.94f, 0.40f, (0.22f + glimmer * 0.78f) * night));
                Tint(f.Aura, new Color(0.66f, 0.92f, 0.36f, (0.025f + 0.64f * glimmer * glimmer) * night));
            }
        }
    }
}
