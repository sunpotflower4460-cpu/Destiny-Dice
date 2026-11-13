using System.Collections;
using System.Collections.Generic;
using DestinyGarden.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// Product-look rendering of the saved WorldState: a floating garden board
    /// in teal mist with procedural textures, rocks, wooden frame, glowing water
    /// and staged plants. The view never advances the model; sway and mist are
    /// presentation-only motion over committed states.
    /// </summary>
    public sealed class GardenView : MonoBehaviour
    {
        // Calm, dark garden earth (the earlier orange read as clay tiles).
        private static readonly Color DrySoil = new Color(0.55f, 0.46f, 0.36f);
        private static readonly Color MoistSoil = new Color(0.46f, 0.37f, 0.28f);
        private static readonly Color WetSoil = new Color(0.38f, 0.30f, 0.23f);
        private static readonly Color WaterGlow = new Color(0.20f, 0.78f, 0.95f);
        private static readonly Color Mist = new Color(0.18f, 0.30f, 0.36f);

        private readonly Renderer[] _cells = new Renderer[256];
        private readonly Renderer[] _domes = new Renderer[256];
        private readonly GameObject[] _waterDiscs = new GameObject[256];
        private readonly GameObject[] _plantViews = new GameObject[256];
        private readonly (string Id, PlantForms.Species Species, PlantStage Stage, int GrowthBand, bool Suited)[] _shownPlants =
            new (string, PlantForms.Species, PlantStage, int, bool)[GardenMapData.CellCount];
        private readonly List<KeyValuePair<Transform, float>> _swaying = new List<KeyValuePair<Transform, float>>();
        private readonly List<Transform> _mistPlanes = new List<Transform>();

        private Material _soilMat;
        private Material _stoneMat;
        private Material _rockMat;
        private Material _waterMat;
        private Material _leafMat;
        private Material _stemMat;
        private Material _petalMat;
        private Material _budMat;
        private Material _centerMat;
        private Material _seedMat;
        private Material[] _speciesPetal;

        private static Material Glow(Shader shader, Color baseColor, Color emission)
        {
            var m = new Material(shader) { color = baseColor };
            m.SetFloat("_Smoothness", 0.4f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            return m;
        }

        /// <summary>
        /// Species (visual only): plants from the die and the first garden are cosmos, daisy or
        /// bellflower; plants the small lives seeded (OriginKind "ecology") are buttercup,
        /// lavender or wild rose, so the ecosystem's own growth is visible.
        /// </summary>
        private static int SpeciesOf(PlantState p)
        {
            int h = 17;
            foreach (char ch in p.PlantId ?? "") h = unchecked(h * 31 + ch);
            h = h & 0x7fffffff;
            return p.OriginKind == "ecology" ? 3 + h % 3 : h % 3;
        }

        private static float Hash01(int cellId, int salt)
        {
            uint h = (uint)(cellId * 2654435761u + (uint)salt * 40503u);
            h ^= h >> 13; h *= 1274126177u;
            return (h & 0xFFFF) / 65535f;
        }
        private Material _mossMat;
        private Material _groundMat;
        private Material _riverMat;
        private Material _fallMat;
        public Vector3 PerchPoint { get; private set; } = new Vector3(7.2f, 2.95f, 16.4f);
        internal Vector3 PerchHeading { get; private set; } = Vector3.back;
        private Mesh[] _rockMeshes;
        private Mesh[] _discMeshes;
        private Mesh _leafMesh;
        private Light _sun;
        private Light _fill;
        private GameObject _dice;
        private readonly List<GameObject> _topPips = new List<GameObject>();
        private static readonly int[][] PipLayouts = { new[]{4}, new[]{0,8}, new[]{0,4,8}, new[]{0,2,6,8}, new[]{0,2,4,6,8}, new[]{0,2,3,5,6,8}, new[]{0,2,3,4,5,6,8} };
        private Material _pipMat;
        private static readonly Color PipEmission = new Color(0.10f, 2.80f, 2.35f);
        private static readonly Color LuckyPipEmission = new Color(3.2f, 2.2f, 0.6f);
        public Transform DiceTransform => _dice != null ? _dice.transform : null;
        private GameObject _altarGlow;
        private Material _altarGlowMat;
        private Vector3 _diceHomePos;
        private Quaternion _diceHomeRot;
        private bool _diceRolling;
        public Camera ViewCamera { get; private set; }
        public CameraRig Rig { get; private set; }
        public IslandFlora Flora { get; private set; }
        private float _groundVitality = 0.02f;
        private WorldState _lastState;

        /// <summary>The ground greens with the island's vitality (repainted only when it changes).</summary>
        public void SetGroundVitality(float vitality)
        {
            if (Mathf.Abs(vitality - _groundVitality) < 0.005f) return;
            _groundVitality = vitality;
            if (_lastState != null) IslandTerrain.PaintBeds(_lastState, DrySoil, MoistSoil, WetSoil, _groundVitality);
        }
        private Color _sunBase = new Color(1.0f, 0.86f, 0.66f);
        private float _balance;

        // ---- time of day (SkyClock), weather and quality
        private SkyState _sky = SkyClock.Evaluate(SkyClock.PhaseFor(0f));
        private float _clockRolls, _clockTarget;
        private WeatherId _weatherId = WeatherId.Clear;
        private Material _skyMat;
        private Bloom _bloom;
        private DepthOfField _dof;
        private bool _rainbowFocus;
        private UniversalAdditionalCameraData _camData;
        private Light _cottageLamp;
        public SkyState Sky => _sky;
        public void SnapClock() { _clockRolls = _clockTarget; ApplySky(); }
        public float Phase => SkyClock.PhaseFor(_clockRolls);

        /// <summary>The island's hour follows the saved rolls; transitions glide, or snap on load.</summary>
        public void SetClock(float rolls, bool snap)
        {
            _clockTarget = rolls;
            if (snap) { _clockRolls = rolls; ApplySky(); }
        }

        private void ApplySky()
        {
            if (_sun == null) return;
            _sky = SkyClock.Evaluate(SkyClock.PhaseFor(_clockRolls));
            float grey = _weatherId == WeatherId.Drizzle ? 0.6f : _weatherId == WeatherId.Cloudy ? 0.25f : 0f;
            float wi = _weatherId == WeatherId.Clear ? 1f : _weatherId == WeatherId.Cloudy ? 0.8f : 0.7f;
            _sunBase = Color.Lerp(_sky.Sun, new Color(0.78f, 0.84f, 0.92f), grey);
            _sun.intensity = _sky.SunIntensity * wi * 0.92f;
            _sun.transform.rotation = Quaternion.Euler(_sky.Elevation, _sky.Azimuth, 0f);
            _fill.color = _sky.Fill;
            _fill.intensity = _sky.FillIntensity * (_weatherId == WeatherId.Clear ? 1f : 1.25f);
            RenderSettings.ambientLight = _sky.Ambient * (_weatherId == WeatherId.Drizzle ? 0.94f : 1f);
            var haze = _sky.Haze * 0.62f;
            RenderSettings.fogColor = haze;
            if (ViewCamera != null) ViewCamera.backgroundColor = haze;
            if (_skyMat != null) _skyMat.SetColor("_BaseColor", haze);
            if (_bloom != null) _bloom.intensity.value = 0.58f * _sky.Bloom;
            if (_lightShaft != null) _lightShaft.SetActive(_weatherId == WeatherId.Clear && _sky.Night < 0.5f);
            if (_cottageLamp != null) _cottageLamp.intensity = 0.3f + 1.7f * _sky.Night;
            ApplyBalance();
        }

        // ---- quality steps for slow GPUs (QualityMonitor decides; see GardenBootstrap)
        private int _qualityLevel;

        public void ApplyQuality(int level)
        {
            _qualityLevel = level;
            if (_dof != null) _dof.active = level < 1 && !_rainbowFocus;
            if (_camData != null) _camData.antialiasing = level < 1 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
            if (_sun != null) _sun.shadows = level < 2 ? LightShadows.Soft : LightShadows.None;
            if (_bloom != null) _bloom.active = level < 3;
            var asset = UniversalRenderPipeline.asset;
            if (asset != null) asset.renderScale = level >= 3 ? 0.7f : 1f;
        }

        internal void SetRainbowFocus(bool active)
        {
            _rainbowFocus = active;
            if (_dof != null) _dof.active = _qualityLevel < 1 && !active;
        }

        /// <summary>Yin and yang of the recent rolls tint the light: yin cooler, yang warmer (subtle).</summary>
        public void SetBalance(int yin, int yang)
        {
            _balance = Mathf.Clamp((yang - yin) / 8f, -1f, 1f);
            ApplyBalance();
        }

        private void ApplyBalance()
        {
            if (_sun == null) return;
            var toward = _balance >= 0f ? new Color(1f, 0.80f, 0.55f) : new Color(0.70f, 0.82f, 1f);
            _sun.color = Color.Lerp(_sunBase, toward, Mathf.Abs(_balance) * 0.15f);
        }
        private GameObject _focusRing;
        private GameObject _lightShaft;
        private ParticleSystem _rain;
        private ParticleSystem _burst;
        private MaterialPropertyBlock _block;
        public bool ReducedMotion;

        public Vector3 CellTop(int cellId)
        {
            int x = cellId % 16, y = cellId / 16;
            if (GardenMapData.Solid[cellId] == 1) return new Vector3(x, CellHeightOf(cellId), y);
            // Open ground: an organic display position (warp field) on the island surface,
            // so nothing that happens on the model grid lines up as a grid on screen.
            var w = IslandTerrain.Warp(cellId);
            return new Vector3(w.x, IslandTerrain.Height(w.x, w.y), w.y);
        }

        private static float CellHeightOf(int cellId) =>
            GardenMapData.Solid[cellId] == 1 ? 1.0f : 0.30f + GardenMapData.CellHeight[cellId] / 1140f * 0.85f;

        private static Material Lit(Shader shader, Texture2D map, float smoothness)
        {
            var m = new Material(shader);
            if (map != null) m.SetTexture("_BaseMap", map);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        /// <summary>This garden's island (set by the bootstrap from the saved run id before Build).</summary>
        public IslandSeed Seed { get; set; }

        public void Build()
        {
            _block = new MaterialPropertyBlock();
            IslandTerrain.Configure(Seed ?? IslandSeed.FromValue(1));
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? shader;

            _soilMat = Lit(shader, ProceduralArt.SoilTexture(), 0.08f);
            _stoneMat = Lit(shader, ProceduralArt.StoneTexture(), 0.18f);
            _rockMat = Lit(shader, ProceduralArt.StoneTexture(true), 0.12f);
            _rockMat.color = new Color(1.10f, 1.15f, 1.12f);
            _leafMat = Lit(shader, ProceduralArt.MossTexture(), 0.25f);
            _leafMat.color = new Color(0.42f, 0.95f, 0.38f);
            _leafMat.SetFloat("_Cull", 0f); // leaves visible from both sides
            _stemMat = Lit(shader, null, 0.2f);
            _stemMat.color = new Color(0.048f, 0.162f, 0.040f);
            _budMat = Lit(shader, null, 0.35f);
            _budMat.color = new Color(0.694f, 0.254f, 0.392f);
            _budMat.EnableKeyword("_EMISSION");
            _budMat.SetColor("_EmissionColor", new Color(0.28f, 0.08f, 0.14f));
            _petalMat = Lit(shader, null, 0.4f);
            _petalMat.color = new Color(0.922f, 0.584f, 0.694f);
            _petalMat.EnableKeyword("_EMISSION");
            _petalMat.SetColor("_EmissionColor", new Color(0.85f, 0.38f, 0.48f));
            _centerMat = Lit(shader, null, 0.4f);
            _centerMat.color = new Color(0.9f, 0.85f, 0.6f);
            _centerMat.EnableKeyword("_EMISSION");
            _centerMat.SetColor("_EmissionColor", new Color(1.60f, 1.25f, 0.55f));
            _speciesPetal = new[]
            {
                _petalMat,                                                              // cosmos (pink)
                Glow(shader, new Color(0.97f, 0.96f, 0.92f), new Color(0.30f, 0.30f, 0.26f)),  // daisy (white)
                Glow(shader, new Color(0.56f, 0.62f, 0.98f), new Color(0.18f, 0.22f, 0.55f)),  // bellflower (blue)
                Glow(shader, new Color(1.00f, 0.84f, 0.26f), new Color(0.55f, 0.40f, 0.05f)),  // buttercup (yellow)
                Glow(shader, new Color(0.68f, 0.52f, 0.96f), new Color(0.30f, 0.18f, 0.50f)),  // lavender (purple)
                Glow(shader, new Color(1.00f, 0.56f, 0.50f), new Color(0.55f, 0.18f, 0.14f))   // wild rose (coral)
            };
            _seedMat = Lit(shader, null, 0.3f);
            _seedMat.color = new Color(0.687f, 0.479f, 0.254f);
            _mossMat = Lit(shader, ProceduralArt.MossTexture(), 0.15f);
            // Soft moss-and-grass ground instead of grey paving: the garden, not a board.
            _groundMat = Lit(shader, ProceduralArt.MossTexture(), 0.10f);
            _groundMat.color = new Color(0.46f, 0.54f, 0.42f);

            // Puddles: soft translucent glints of water, not opaque discs.
            _waterMat = new Material(Shader.Find("Sprites/Default") ?? unlit);
            _waterMat.SetTexture("_MainTex", ProceduralArt.GlowSprite());
            _waterMat.color = new Color(0.58f, 0.90f, 1f, 0.30f);
            _waterMat.renderQueue = 2995;

            _rockMeshes = new[] { ProceduralArt.RockMesh(0), ProceduralArt.RockMesh(1), ProceduralArt.RockMesh(2) };
            _discMeshes = new[] { ProceduralArt.IrregularDiscMesh(0), ProceduralArt.IrregularDiscMesh(1),
                ProceduralArt.IrregularDiscMesh(2), ProceduralArt.IrregularDiscMesh(3) };
            _leafMesh = ProceduralArt.LeafMesh();

            RenderSettings.fog = true;
            RenderSettings.fogColor = Mist;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 26f;
            RenderSettings.fogEndDistance = 59f;
            RenderSettings.ambientLight = new Color(0.58f, 0.62f, 0.72f);

            var camObj = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camObj.AddComponent<Camera>();
            ViewCamera = cam;
            Rig = camObj.AddComponent<CameraRig>();
            Rig.Cam = cam;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(5.9f, 19.0f, -14.8f);
            cam.transform.LookAt(Rig.Target);
            cam.backgroundColor = Mist;
            cam.fieldOfView = 41f;
            var camData = camObj.AddComponent<UniversalAdditionalCameraData>();
            _camData = camData;
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;

            var volumeObj = new GameObject("PostVolume");
            var volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            _bloom = bloom;
            bloom.intensity.value = 0.58f;
            bloom.threshold.value = 0.94f;
            bloom.scatter.value = 0.64f;
            bloom.tint.value = new Color(1.0f, 0.93f, 0.86f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.22f;
            vignette.smoothness.value = 0.55f;
            var split = profile.Add<SplitToning>(true);
            split.shadows.value = new Color(0.42f, 0.53f, 0.58f);
            split.highlights.value = new Color(0.70f, 0.59f, 0.49f);
            split.balance.value = -4f;
            var dof = profile.Add<DepthOfField>(true);
            _dof = dof;
            dof.mode.value = DepthOfFieldMode.Gaussian;
            dof.gaussianStart.value = 37f;
            dof.gaussianEnd.value = 54f;
            dof.gaussianMaxRadius.value = 0.82f;
            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.value = TonemappingMode.ACES;
            var colors = profile.Add<ColorAdjustments>(true);
            colors.saturation.value = 1f;
            colors.postExposure.value = 0.18f;
            colors.contrast.value = 10f;
            volume.profile = profile;

            var sunObj = new GameObject("Sun");
            _sun = sunObj.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.color = new Color(1.0f, 0.86f, 0.66f);
            _sun.transform.rotation = Quaternion.Euler(50f, -42f, 0f);
            _sun.intensity = 2.40f;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.85f;
            _sun.shadowBias = 0.04f;
            _sun.shadowNormalBias = 0.06f;

            var fillObj = new GameObject("Fill");
            _fill = fillObj.AddComponent<Light>();
            _fill.type = LightType.Directional;
            _fill.color = new Color(0.212f, 0.445f, 0.604f);
            _fill.transform.rotation = Quaternion.Euler(38f, 130f, 0f);
            _fill.intensity = 0.45f;
            _fill.shadows = LightShadows.None;

            // A warm pool of light over the heart of the garden: a quiet, sacred focus.
            var poolObj = new GameObject("GardenGlow");
            poolObj.transform.SetParent(transform);
            poolObj.transform.position = new Vector3(7.5f, 4.2f, 7.8f);
            var pool = poolObj.AddComponent<Light>();
            pool.type = LightType.Point;
            pool.color = new Color(1f, 0.82f, 0.58f);
            pool.range = 11f;
            pool.intensity = 1.65f;
            pool.shadows = LightShadows.None;

            IslandTerrain.ShadeCasters.Clear();
            BuildEnvironment(shader, unlit);
            BuildCells(shader);
            Rig.CaptureHome();
            Flora = gameObject.AddComponent<IslandFlora>();
            Flora.Build();
            BuildDice(shader);
            BuildParticles(unlit);
        }

        private void BuildEnvironment(Shader shader, Shader unlit)
        {
            // Gradient sky dome (inside-out sphere).
            var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sky.name = "SkyDome";
            sky.transform.SetParent(transform);
            sky.transform.position = new Vector3(7.5f, 0, 7.5f);
            sky.transform.localScale = Vector3.one * 90f;
            var skyMat = new Material(unlit);
            _skyMat = skyMat;
            skyMat.SetTexture("_BaseMap", ProceduralArt.SkyTint());
            skyMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);
            sky.GetComponent<Renderer>().material = skyMat;
            sky.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Destroy(sky.GetComponent<Collider>());

            // One continuous terrain for the garden and its shore.
            var islandObj = new GameObject("Island");
            islandObj.transform.SetParent(transform);
            islandObj.AddComponent<MeshFilter>().sharedMesh = IslandTerrain.BuildMesh();
            var islandRenderer = islandObj.AddComponent<MeshRenderer>();
            var islandMat = Lit(shader, IslandTerrain.BakeColor(), 0.12f);
            islandRenderer.material = islandMat;
            islandRenderer.receiveShadows = true;
            islandRenderer.shadowCastingMode = ShadowCastingMode.On;

            // The river: scenery from a spring behind the garden to the sea (not the water model).
            var riverObj = new GameObject("River");
            riverObj.transform.SetParent(transform);
            riverObj.AddComponent<MeshFilter>().sharedMesh = IslandTerrain.BuildRiverMesh();
            // Translucent stream with soft banks and downstream glints.
            _riverMat = new Material(Shader.Find("Sprites/Default") ?? unlit);
            var riverTexture = ProceduralArt.RiverTexture();
            _riverMat.SetTexture("_MainTex", riverTexture);
            _riverMat.color = new Color(0.95f, 1f, 0.98f, 0.97f);
            _riverMat.renderQueue = 2990;
            var riverRenderer = riverObj.AddComponent<MeshRenderer>();
            riverRenderer.material = _riverMat;
            riverRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var spring = IslandTerrain.RiverPath[0];
            var springLightObj = new GameObject("SpringGlow");
            springLightObj.transform.SetParent(transform);
            springLightObj.transform.position = new Vector3(spring.x, IslandTerrain.Height(spring.x, spring.y) + 0.8f, spring.y);
            var springLight = springLightObj.AddComponent<Light>();
            springLight.type = LightType.Point;
            springLight.color = new Color(0.60f, 0.93f, 0.88f);
            springLight.range = 3.6f;
            springLight.intensity = 0.68f;
            springLight.shadows = LightShadows.None;

            // The spring: a glowing pool under a small cascade falling from a rock set toward the highlands.
            {
                float sy = IslandTerrain.Height(spring.x, spring.y);
                var outward = (spring - new Vector2(7.5f, 7.5f)).normalized;
                var pool = new GameObject("SpringPool");
                pool.transform.SetParent(transform);
                pool.transform.position = new Vector3(spring.x, sy + 0.08f, spring.y);
                pool.AddComponent<MeshFilter>().sharedMesh = ProceduralArt.DiscMesh(0.95f, 32);
                var poolMat = new Material(Shader.Find("Sprites/Default") ?? unlit);
                poolMat.SetTexture("_MainTex", ProceduralArt.GlowSprite());
                poolMat.color = new Color(0.52f, 0.89f, 0.80f, 0.55f);
                poolMat.renderQueue = 2992;
                var pr = pool.AddComponent<MeshRenderer>();
                pr.material = poolMat;
                pr.shadowCastingMode = ShadowCastingMode.Off;
                var cliff = new GameObject("SpringRock");
                cliff.transform.SetParent(transform);
                cliff.transform.position = new Vector3(spring.x + outward.x * 1.0f, sy + 0.62f, spring.y + outward.y * 1.0f);
                cliff.transform.localScale = new Vector3(1.5f, 1.7f, 1.2f);
                cliff.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
                cliff.AddComponent<MeshFilter>().sharedMesh = _rockMeshes[1];
                var cr = cliff.AddComponent<MeshRenderer>();
                cr.material = _rockMat;
                cr.shadowCastingMode = ShadowCastingMode.On;
                PerchPoint = RockTop(_rockMeshes[1], cliff.transform.localToWorldMatrix);
                PerchHeading = new Vector3(-outward.x, 0f, -outward.y);
                IslandTerrain.AddShade(spring.x + outward.x, spring.y + outward.y, 1.2f);
                _fallMat = new Material(Shader.Find("Sprites/Default") ?? unlit);
                _fallMat.SetTexture("_MainTex", riverTexture);
                _fallMat.color = new Color(0.90f, 1f, 0.98f, 0.84f);
                _fallMat.renderQueue = 2993;
                float yaw = Mathf.Atan2(outward.x, outward.y) * Mathf.Rad2Deg;
                for (int k = 0; k < 2; k++)
                {
                    // Two crossed quads so the cascade reads from every side of the orbit.
                    var fall = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(fall.GetComponent<Collider>());
                    fall.name = "SpringFall";
                    fall.transform.SetParent(transform);
                    fall.transform.position = new Vector3(spring.x + outward.x * 0.26f, sy + 0.62f, spring.y + outward.y * 0.26f);
                    fall.transform.rotation = Quaternion.Euler(0f, yaw + 180f + (k == 0 ? 0f : 90f), 0f);
                    fall.transform.localScale = new Vector3(0.45f, 1.1f, 1f);
                    fall.GetComponent<MeshRenderer>().material = _fallMat;
                }
                var mistObj = new GameObject("SpringMist");
                mistObj.transform.SetParent(transform);
                mistObj.transform.position = new Vector3(spring.x + outward.x * 0.2f, sy + 0.2f, spring.y + outward.y * 0.2f);
                var mist = mistObj.AddComponent<ParticleSystem>();
                var mm = mist.main;
                mm.maxParticles = 30; mm.startLifetime = 1.8f; mm.startSpeed = 0.18f; mm.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                mm.startColor = new Color(0.9f, 0.98f, 1f, 0.22f);
                mm.simulationSpace = ParticleSystemSimulationSpace.World;
                var me = mist.emission; me.rateOverTime = 7f;
                var ms = mist.shape; ms.shapeType = ParticleSystemShapeType.Sphere; ms.radius = 0.3f;
                var mmat = new Material(Shader.Find("Sprites/Default") ?? unlit);
                mmat.SetTexture("_MainTex", ProceduralArt.MistSprite());
                mmat.renderQueue = 3120;
                mistObj.GetComponent<ParticleSystemRenderer>().material = mmat;
            }

            BuildCoastalRocks();
            BuildTrees();
            BuildCottage(shader);

            // Drifting mist sheets around the board.
            var mistShader = Shader.Find("Sprites/Default") ?? unlit;
            var mistMat = new Material(mistShader);
            var mistTex = ProceduralArt.MistSprite();
            mistMat.SetTexture("_MainTex", mistTex);
            mistMat.SetTexture("_BaseMap", mistTex);
            mistMat.color = new Color(0.90f, 0.94f, 1.0f, 0.14f);
            mistMat.SetColor("_BaseColor", new Color(0.90f, 0.94f, 1.0f, 0.14f));
            mistMat.SetFloat("_Surface", 1f);
            mistMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mistMat.SetFloat("_ZWrite", 0f);
            mistMat.renderQueue = 3100;
            mistMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            for (int i = 0; i < 6; i++)
            {
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = "Mist";
                plane.transform.SetParent(transform);
                float a = i / 6f * Mathf.PI * 2f + 0.5f;
                plane.transform.position = new Vector3(7.5f + Mathf.Cos(a) * 15.5f, -0.55f + (i % 2) * 0.12f, 7.5f + Mathf.Sin(a) * 15.5f);
                plane.transform.rotation = Quaternion.Euler(0, i * 61f, 0);
                plane.transform.localScale = new Vector3(0.85f, 1f, 0.5f);
                plane.GetComponent<Renderer>().material = mistMat;
                plane.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Destroy(plane.GetComponent<Collider>());
                _mistPlanes.Add(plane.transform);
            }

            // Warm light shaft from the upper left (clear-weather accent).
            _lightShaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _lightShaft.name = "LightShaft";
            _lightShaft.transform.SetParent(transform);
            _lightShaft.transform.position = new Vector3(3.2f, 7.5f, 4.2f);
            _lightShaft.transform.rotation = Quaternion.Euler(24f, 0, 34f);
            _lightShaft.transform.localScale = new Vector3(4.6f, 8.5f, 4.6f);
            var shaftMat = new Material(Shader.Find("Sprites/Default") ?? unlit);
            var shaftTex = ProceduralArt.MistSprite();
            shaftMat.SetTexture("_MainTex", shaftTex);
            shaftMat.SetTexture("_BaseMap", shaftTex);
            shaftMat.color = new Color(1.0f, 0.85f, 0.55f, 0.08f);
            shaftMat.SetColor("_BaseColor", new Color(1.0f, 0.85f, 0.55f, 0.08f));
            shaftMat.SetFloat("_Surface", 1f);
            shaftMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            shaftMat.SetFloat("_ZWrite", 0f);
            shaftMat.renderQueue = 3050;
            shaftMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _lightShaft.GetComponent<Renderer>().material = shaftMat;
            _lightShaft.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Destroy(_lightShaft.GetComponent<Collider>());
        }

        /// <summary>A few small trees and shrubs on the back and side hills (never in front of the garden).</summary>
        private void BuildCoastalRocks()
        {
            var hillside = new List<CombineInstance>();
            var shoreline = new List<CombineInstance>();
            var rng = new System.Random(IslandTerrain.Seed.Value ^ 87153);
            float Range(float min, float max) => Mathf.Lerp(min, max, (float)rng.NextDouble());

            float bestPerch = float.NegativeInfinity;
            var spots = IslandTerrain.Seed.Stones;
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                float rockiness = IslandTerrain.ShoreRockiness(spot.Angle);
                if (rockiness < 0.29f) continue;
                var p = IslandTerrain.ShelteredLand(spot.Angle, spot.Radius, 1.35f, 1.15f);
                float height = 0.45f + spot.Height * 0.46f;
                float y = IslandTerrain.Height(p.x, p.y);
                var rock = AddRock(hillside, p, y + height * 0.07f, new Vector3(spot.Width * 1.36f, height, spot.Width),
                    Quaternion.Euler(Range(-13f, 13f), spot.Angle * Mathf.Rad2Deg + 40f, Range(-16f, 16f)));
                var crown = RockTop(rock.mesh, rock.transform);
                var inward = IslandTerrain.Center - p;
                float score = height + crown.y * 0.45f + Mathf.Clamp(inward.y * 0.17f, -1.1f, 1.1f);
                if (score > bestPerch && IslandTerrain.DistanceToRiver(p.x, p.y) > 1.3f)
                {
                    bestPerch = score;
                    PerchPoint = crown;
                    PerchHeading = new Vector3(inward.x * 0.68f, 0f, -2f - Mathf.Abs(inward.y) * 0.45f);
                }
                IslandTerrain.AddShade(p.x, p.y, spot.Width);
            }

            // Group the slabs around exposed headlands, leaving the sandy bays open.
            const float sectorWidth = 2f * Mathf.PI / 3f;
            for (int sector = 0; sector < 3; sector++)
            {
                float middle = IslandTerrain.NearBayAngle + (sector + 0.5f) * sectorWidth;
                float angle = middle;
                float best = 0.35f;
                for (int i = -9; i <= 9; i++)
                {
                    float a = middle + i * 0.091f;
                    float exposure = IslandTerrain.ShoreRockiness(a);
                    if (exposure <= best) continue;
                    best = exposure;
                    angle = a;
                }
                if (best < 0.48f) continue;

                for (int i = 0; i < 8; i++)
                {
                    float a = angle + Range(-0.24f, 0.24f);
                    if (IslandTerrain.ShoreRockiness(a) < 0.40f) continue;
                    float r = IslandTerrain.ShoreRadius(a);
                    bool tidal = i >= 4;
                    var p = IslandSeed.At(a, r + (tidal ? Range(-0.08f, 0.93f) : Range(-1.42f, -0.18f)));
                    float width = Range(0.57f, 1.42f) * (tidal ? 1f : 1.6f);
                    float height = width * Range(0.44f, 0.90f);
                    float y = tidal ? IslandTerrain.SeaLevel - Range(0.02f, 0.17f) : IslandTerrain.Height(p.x, p.y) - height * 0.17f;
                    var rotation = Quaternion.Euler(Range(-18f, 18f), 100f - a * Mathf.Rad2Deg + Range(-25f, 25f), Range(-18f, 18f));
                    AddRock(shoreline, p, y, new Vector3(width, height, width * Range(0.70f, 1.22f)), rotation);
                    if (!tidal) IslandTerrain.AddShade(p.x, p.y, width * 0.51f);
                }
            }

            AddRockGroup("Mossy boulders", hillside, _rockMat);
            AddRockGroup("Coastal outcrops", shoreline,
                new Material(_stoneMat) { color = new Color(0.98f, 0.99f, 1.02f) });

            CombineInstance AddRock(List<CombineInstance> rocks, Vector2 p, float y, Vector3 scale, Quaternion rotation)
            {
                var rock = new CombineInstance
                {
                    mesh = _rockMeshes[rng.Next(_rockMeshes.Length)],
                    transform = Matrix4x4.TRS(new Vector3(p.x, y, p.y), rotation, scale)
                };
                rocks.Add(rock);
                return rock;
            }

            void AddRockGroup(string name, List<CombineInstance> rocks, Material material)
            {
                if (rocks.Count == 0) return;
                var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(rocks.ToArray());
                var root = new GameObject(name);
                root.transform.SetParent(transform);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        private static Vector3 RockTop(Mesh mesh, Matrix4x4 transform)
        {
            var top = new Vector3(0f, float.NegativeInfinity, 0f);
            foreach (var vertex in mesh.vertices)
            {
                var point = transform.MultiplyPoint3x4(vertex);
                if (point.y > top.y) top = point;
            }
            return top;
        }

        private void BuildTrees()
        {
            var fol = Shader.Find("DestinyGarden/Foliage");
            var treeMat = new Material(fol != null ? fol : Shader.Find("Sprites/Default"));
            treeMat.SetFloat("_CanopyLighting", 1f);
            treeMat.SetFloat("_WindStrength", 0.11f);
            treeMat.SetFloat("_WindSpeed", 0.89f);
            // Trees stand where this island's seed puts them (never across the default front view).
            var spots = IslandTerrain.Seed.Trees;
            Material petalMat = null;
            var ground = AltarGround();
            var altar = new Vector2(ground.x, ground.z);
            for (int i = 0; i < spots.Length; i++)
            {
                var kind = spots[i].Kind;
                var p = IslandTerrain.ShelteredLand(spots[i].Angle, spots[i].Radius, 1.26f,
                    kind == TreeForms.Kind.Willow ? 0.48f : 0.96f, altar, 0.83f + spots[i].Size * 1.04f);
                float x = p.x, z = p.y;
                var root = new GameObject(kind.ToString()).transform;
                root.SetParent(transform);
                root.position = new Vector3(x, IslandTerrain.Height(x, z) - 0.04f, z);
                root.rotation = Quaternion.Euler(0f, i * 47f, 0f);
                root.localScale = Vector3.zero;
                var body = new GameObject("Form");
                body.transform.SetParent(root, false);
                body.AddComponent<MeshFilter>().sharedMesh = TreeForms.Build(kind, IslandTerrain.Seed.Value % 9973 + i * 37).ToMesh(kind + "_" + i);
                var r = body.AddComponent<MeshRenderer>();
                r.sharedMaterial = treeMat;
                r.shadowCastingMode = ShadowCastingMode.On;
                // Grows in with the island: shrubs first, then trees, the blossom trees last.
                float thr = kind == TreeForms.Kind.Shrub ? 0.40f + (i % 4) * 0.03f
                    : kind == TreeForms.Kind.Blossom ? 0.78f + (i % 2) * 0.04f
                    : kind == TreeForms.Kind.Willow ? 0.56f + (i % 3) * 0.03f : 0.54f + (i % 5) * 0.035f;
                _growers.Add(new Grower { T = root, Size = spots[i].Size * 1.3f, Threshold = thr });
                IslandTerrain.AddShade(x, z, (kind == TreeForms.Kind.Shrub ? 0.9f : 1.7f) * spots[i].Size);
                if (kind == TreeForms.Kind.Blossom)
                {
                    if (petalMat == null)
                    {
                        petalMat = new Material(Shader.Find("Sprites/Default"));
                        petalMat.SetTexture("_MainTex", ProceduralArt.BlossomPetalSprite());
                        petalMat.renderQueue = 3150;
                    }
                    AddPetalFall(root, petalMat);
                }
            }
        }

        private readonly List<ParticleSystem> _fallingPetals = new List<ParticleSystem>();

        /// <summary>A slow drift of petals from a blossom tree.</summary>
        private void AddPetalFall(Transform tree, Material material)
        {
            var obj = new GameObject("PetalFall");
            obj.transform.SetParent(tree, false);
            obj.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = 40;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.085f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.gravityModifier = 0.015f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.80f, 0.86f, 0.9f), new Color(1f, 0.94f, 0.96f, 0.9f));
            var emission = ps.emission; emission.rateOverTime = 1.4f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.7f;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.15f; noise.frequency = 0.4f;
            var spin = ps.rotationOverLifetime; spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            obj.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            _fallingPetals.Add(ps);
        }

        private sealed class Grower { public Transform T; public float Size; public float Threshold; }
        private readonly List<Grower> _growers = new List<Grower>();

        /// <summary>A small cottage by the spring that appears when the garden has matured.</summary>
        private void BuildCottage(Shader shader)
        {
            var p = IslandTerrain.ShelteredLand(IslandTerrain.Seed.CottageAngle, IslandTerrain.Seed.CottageRadius, 2.02f, 1.45f);
            float x = p.x, z = p.y;
            var root = new GameObject("Cottage").transform;
            root.SetParent(transform);
            root.position = new Vector3(x, IslandTerrain.Height(x, z) - 0.03f, z);
            // The door faces the garden (local -z is the door side).
            root.rotation = Quaternion.LookRotation(new Vector3(x - 7.5f, 0f, z - 7.5f), Vector3.up);
            root.localScale = Vector3.zero;
            var wall = Lit(shader, ProceduralArt.StoneTexture(), 0.12f);
            wall.color = new Color(0.92f, 0.88f, 0.80f);
            var roof = Lit(shader, ProceduralArt.MossTexture(), 0.1f);
            roof.color = new Color(0.46f, 0.60f, 0.44f);
            var wood = Lit(shader, ProceduralArt.WoodTexture(), 0.1f);
            wood.color = new Color(0.55f, 0.40f, 0.30f);
            var glow = Lit(shader, null, 0.3f);
            glow.color = new Color(1f, 0.82f, 0.50f);
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(2.4f, 1.6f, 0.7f));
            GameObject Box(Material m, Vector3 p, Vector3 sc, Vector3 rot)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(root, false);
                g.transform.localPosition = p; g.transform.localScale = sc; g.transform.localRotation = Quaternion.Euler(rot);
                g.GetComponent<MeshRenderer>().material = m;
                return g;
            }
            Box(wall, new Vector3(0f, 0.28f, 0f), new Vector3(0.95f, 0.56f, 0.72f), Vector3.zero);
            Box(roof, new Vector3(0f, 0.70f, 0.20f), new Vector3(1.10f, 0.06f, 0.56f), new Vector3(38f, 0f, 0f));
            Box(roof, new Vector3(0f, 0.70f, -0.20f), new Vector3(1.10f, 0.06f, 0.56f), new Vector3(-38f, 0f, 0f));
            Box(wood, new Vector3(0.18f, 0.20f, -0.365f), new Vector3(0.22f, 0.38f, 0.02f), Vector3.zero);
            Box(glow, new Vector3(-0.22f, 0.32f, -0.365f), new Vector3(0.18f, 0.16f, 0.02f), Vector3.zero);
            Box(wall, new Vector3(0.32f, 0.86f, 0.10f), new Vector3(0.12f, 0.30f, 0.12f), Vector3.zero);
            var lampObj = new GameObject("CottageLight");
            lampObj.transform.SetParent(root, false);
            lampObj.transform.localPosition = new Vector3(-0.22f, 0.4f, -0.7f);
            var lamp = lampObj.AddComponent<Light>();
            lamp.type = LightType.Point; lamp.color = new Color(1f, 0.78f, 0.45f); lamp.range = 2.2f; lamp.intensity = 1.2f;
            _cottageLamp = lamp;
            _growers.Add(new Grower { T = root, Size = 1.0f, Threshold = 0.86f });
            IslandTerrain.AddShade(x, z, 1.0f);
        }

        private void UpdateGrowers()
        {
            if (Flora == null) return;
            float v = Flora.Vitality;
            foreach (var g in _growers)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((v - g.Threshold) / 0.16f));
                g.T.localScale = Vector3.one * (g.Size * k);
                g.T.gameObject.SetActive(k > 0.001f);
            }
        }

        private void BuildCells(Shader shader)
        {
            var root = new GameObject("Cells");
            root.transform.SetParent(transform);
            var domeProto = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var domeMesh = domeProto.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(domeProto);

            for (int id = 0; id < 256; id++)
            {
                int x = id % 16, y = id / 16;
                bool solid = GardenMapData.Solid[id] == 1;
                bool soil = GardenMapData.SoilCapacity[id] > 0;
                float jitter = ((id * 2654435761u) % 100u) / 100f;
                if (solid)
                {
                    var rockCell = new GameObject("Rock_" + id);
                    rockCell.transform.SetParent(root.transform);
                    float height = 1.85f + jitter * 0.45f;
                    rockCell.transform.position = new Vector3(x, IslandTerrain.Height(x, y) + height * 0.18f - 0.06f, y);
                    rockCell.transform.localScale = new Vector3(1.02f, height, 1.0f);
                    rockCell.transform.rotation = Quaternion.Euler((jitter - 0.5f) * 8f, (id * 53) % 360, 0);
                    rockCell.AddComponent<MeshFilter>().sharedMesh = _rockMeshes[id % 3];
                    IslandTerrain.AddShade(x, y, 1.1f);
                    var rr = rockCell.AddComponent<MeshRenderer>();
                    rr.material = _rockMat;
                    rr.shadowCastingMode = ShadowCastingMode.On;
                    rr.receiveShadows = true;
                }
                // Soil beds are painted into the island surface (IslandTerrain.PaintBeds), not built as meshes.
            }

            // Ground cover lives in IslandFlora (habitat-driven colonies), built after the cells.
        }

        private void BuildDice(Shader shader)
        {
            // A small stone altar; the die floats above it, slowly turning and breathing.
            var ground = AltarGround();
            var altar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(altar.GetComponent<Collider>());
            altar.name = "Altar";
            altar.transform.SetParent(transform);
            altar.transform.position = ground + Vector3.up * 0.14f;
            altar.transform.localScale = new Vector3(0.95f, 0.16f, 0.95f);
            var altarMat = Lit(shader, ProceduralArt.StoneTexture(), 0.2f);
            altarMat.color = new Color(1.10f, 1.08f, 1.02f);
            altar.GetComponent<MeshRenderer>().material = altarMat;
            _altarGlow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(_altarGlow.GetComponent<Collider>());
            _altarGlow.name = "AltarGlow";
            _altarGlow.transform.SetParent(transform);
            _altarGlow.transform.position = ground + Vector3.up * 0.31f;
            _altarGlow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _altarGlow.transform.localScale = Vector3.one * 1.5f;
            _altarGlowMat = IslandHarmony.GlowMaterial(ProceduralArt.GlowSprite(), new Color(0.55f, 0.95f, 1f, 0.55f), 3005);
            _altarGlow.GetComponent<MeshRenderer>().material = _altarGlowMat;
            _diceHomePos = ground + Vector3.up * 0.98f;
            _diceHomeRot = Quaternion.Euler(-16f, 30f, 6f);

            _dice = new GameObject("Dice");
            _dice.transform.SetParent(transform);
            _dice.transform.position = _diceHomePos;
            _dice.transform.localScale = Vector3.one * 0.86f;
            _dice.transform.rotation = _diceHomeRot;
            _dice.AddComponent<MeshFilter>().sharedMesh = ProceduralArt.RoundedCubeMesh();
            var diceRenderer = _dice.AddComponent<MeshRenderer>();
            var diceMat = Lit(shader, ProceduralArt.IvoryTexture(), 0.43f);
            diceMat.EnableKeyword("_EMISSION");
            diceMat.SetColor("_EmissionColor", new Color(0.042f, 0.033f, 0.022f));
            diceRenderer.material = diceMat;
            diceRenderer.shadowCastingMode = ShadowCastingMode.On;
            diceRenderer.receiveShadows = true;

            var pipMat = new Material(shader) { color = WaterGlow };
            pipMat.EnableKeyword("_EMISSION");
            pipMat.SetColor("_EmissionColor", PipEmission);
            _pipMat = pipMat;
            var socketMat = Lit(shader, null, 0.22f);
            socketMat.color = new Color(0.08f, 0.18f, 0.18f);
            for (int i = 0; i < 9; i++)
            {
                float x = (i % 3 - 1) * 0.24f, z = (i / 3 - 1) * 0.24f;
                _topPips.Add(BuildPip(Vector3.up, new Vector3(x, 0f, z), pipMat, socketMat));
            }
            BuildPip(Vector3.forward, Vector3.zero, pipMat, socketMat);
            BuildPip(Vector3.right, Vector3.zero, pipMat, socketMat);
            SetDiceFace(5);

            _focusRing = new GameObject("FocusRing");
            _focusRing.transform.SetParent(transform);
            var ringMat = new Material(shader) { color = new Color(1.0f, 0.82f, 0.45f) };
            ringMat.EnableKeyword("_EMISSION");
            ringMat.SetColor("_EmissionColor", new Color(2.40f, 1.42f, 0.38f));
            var line = _focusRing.AddComponent<LineRenderer>();
            line.material = ringMat;
            line.loop = true;
            line.useWorldSpace = false;
            line.widthMultiplier = 0.05f;
            const int segments = 44;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.58f, 0, Mathf.Sin(a) * 0.58f));
            }
            _focusRing.SetActive(false);
        }

        private Vector3 AltarGround()
        {
            var ground = CellTop(44) + new Vector3(0.35f, 0f, -0.05f);
            ground.y = IslandTerrain.Height(ground.x, ground.z);
            return ground;
        }

        private GameObject BuildPip(Vector3 normal, Vector3 offset, Material pipMat, Material socketMat)
        {
            var pip = new GameObject("Pip");
            pip.transform.SetParent(_dice.transform, false);
            pip.transform.localPosition = normal * 0.5f + offset;
            pip.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal);

            void Inlay(string name, float diameter, float depth, float lift, Material material)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                part.name = name;
                part.transform.SetParent(pip.transform, false);
                part.transform.localPosition = Vector3.up * lift;
                part.transform.localScale = new Vector3(diameter, depth, diameter);
                var renderer = part.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                Destroy(part.GetComponent<Collider>());
            }

            Inlay("Socket", 0.143f, 0.024f, 0.002f, socketMat);
            Inlay("Gem", 0.103f, 0.036f, 0.009f, pipMat);
            return pip;
        }

        /// <summary>Shows the rolled face number as glowing pips on the dice top.</summary>
        public void SetDiceFace(int faceId)
        {
            if (faceId < 1 || faceId > PipLayouts.Length) return;
            var on = new HashSet<int>(PipLayouts[faceId - 1]);
            for (int i = 0; i < _topPips.Count; i++) _topPips[i].SetActive(on.Contains(i));
        }

        /// <summary>Golden pips while the Lucky Seven face is published or rolled.</summary>
        public void SetLuckyDice(bool lucky)
        {
            if (_pipMat == null) return;
            _pipMat.color = lucky ? new Color(1f, 0.82f, 0.35f) : WaterGlow;
            _pipMat.SetColor("_EmissionColor", lucky ? LuckyPipEmission : PipEmission);
        }

        private void BuildParticles(Shader unlit)
        {
            var glowTex = ProceduralArt.GlowSprite();
            _rain = MakeParticles("Rain", 1100, new Color(0.72f, 0.90f, 1f, 0.65f), 0.045f, 1.0f, unlit, glowTex);
            var rainMain = _rain.main; rainMain.startSpeed = 7.5f; rainMain.gravityModifier = 0.35f;
            _rain.transform.position = new Vector3(7.5f, 9.5f, 7.5f);
            _rain.transform.rotation = Quaternion.Euler(90f, 0, 0);
            var rShape = _rain.shape;
            rShape.shapeType = ParticleSystemShapeType.Box;
            rShape.scale = new Vector3(17f, 17f, 0.5f);
            var rainRenderer = _rain.GetComponent<ParticleSystemRenderer>();
            rainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            rainRenderer.lengthScale = 2.6f;
            _rain.Stop();

            _burst = MakeParticles("Burst", 80, WaterGlow, 0.06f, 0.9f, unlit, glowTex);
            var burstMain = _burst.main; burstMain.startSpeed = 1.7f; burstMain.gravityModifier = -0.12f;
            var burstEmission = _burst.emission; burstEmission.rateOverTime = 0;
            _burst.Stop();
        }

        private ParticleSystem MakeParticles(string name, int max, Color color, float size, float lifetime,
            Shader unlit, Texture2D sprite)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform);
            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = max;
            main.startSize = size;
            main.startLifetime = lifetime;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var mat = new Material(Shader.Find("Sprites/Default") ?? unlit);
            mat.SetTexture("_BaseMap", sprite);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", sprite);
            mat.color = color;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = 3200;
            obj.GetComponent<ParticleSystemRenderer>().material = mat;
            return ps;
        }

        private void LateUpdate()
        {
            UpdateGrowers();
            foreach (var petals in _fallingPetals)
            {
                if (!petals.gameObject.activeInHierarchy) continue;
                if (ReducedMotion && !petals.isStopped) petals.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                else if (!ReducedMotion && petals.isStopped) petals.Play(false);
            }
            if (_dice != null && !_diceRolling)
            {
                float t = Time.time;
                _dice.transform.position = _diceHomePos + Vector3.up * (ReducedMotion ? 0f : Mathf.Sin(t * 1.1f) * 0.07f);
                _dice.transform.rotation = Quaternion.Euler(0f, ReducedMotion ? 0f : t * 9f, 0f) * _diceHomeRot;
            }
            if (_altarGlowMat != null)
                _altarGlowMat.color = new Color(0.55f, 0.95f, 1f, 0.40f + 0.15f * Mathf.Sin(Time.time * 1.1f));
        }

        private void Update()
        {
            if (!Mathf.Approximately(_clockRolls, _clockTarget))
            {
                _clockRolls = Mathf.MoveTowards(_clockRolls, _clockTarget, Time.deltaTime * 0.45f);
                ApplySky();
            }
            if (ReducedMotion) return;
            float t = Time.time;
            for (int i = 0; i < _swaying.Count; i++)
            {
                var entry = _swaying[i];
                if (entry.Key == null) continue;
                entry.Key.localRotation = Quaternion.Euler(
                    Mathf.Sin(t * 1.25f + entry.Value) * 2.6f, 0, Mathf.Cos(t * 1.05f + entry.Value) * 2.2f);
            }
            for (int i = 0; i < _mistPlanes.Count; i++)
            {
                var m = _mistPlanes[i];
                m.position += new Vector3(Mathf.Sin(t * 0.07f + i * 1.7f), 0, Mathf.Cos(t * 0.05f + i)) * (0.12f * Time.deltaTime);
            }
            if (_fallMat != null) _fallMat.mainTextureOffset = new Vector2(0f, Time.time * 0.7f);
            if (_riverMat != null)
            {
                _riverMat.mainTextureOffset = new Vector2(0f, -t * 0.18f);
            }
            if (_waterMat != null)
            {
                float pulse = 1.0f + Mathf.Sin(t * 1.8f) * 0.08f;
                _waterMat.color = new Color(0.58f, 0.90f, 1f, 0.30f * pulse);
            }
        }

        public void Show(WorldState state, WeatherDefinition weather)
        {
            _lastState = state;
            IslandTerrain.PaintBeds(state, DrySoil, MoistSoil, WetSoil, _groundVitality);
            for (int id = 0; id < 256; id++)
            {
                if (GardenMapData.SoilCapacity[id] > 0)
                {
                    float moisture = (float)state.SoilWater[id] / GardenMapData.SoilCapacity[id];
                    Color tint = moisture < 0.45f
                        ? Color.Lerp(DrySoil, MoistSoil, moisture / 0.45f)
                        : Color.Lerp(MoistSoil, WetSoil, Mathf.Clamp01((moisture - 0.45f) / 0.40f));
                    _block.SetColor("_BaseColor", tint);
                    _block.SetFloat("_Smoothness", Mathf.Lerp(0.05f, 0.55f, moisture));
                    if (_cells[id] != null) _cells[id].SetPropertyBlock(_block);
                }

                bool wet = state.SurfaceWater[id] > 0 && GardenMapData.Solid[id] == 0;
                if (wet && _waterDiscs[id] == null)
                {
                    var disc = new GameObject("Water");
                    disc.transform.SetParent(transform);
                    disc.transform.position = CellTop(id) + Vector3.up * 0.045f;
                    float ws = Mathf.Lerp(0.55f, 1.35f, Mathf.Sqrt(Mathf.Clamp01(state.SurfaceWater[id] / 240f)));
                    disc.transform.localScale = new Vector3(ws, 1f, ws);
                    disc.transform.rotation = Quaternion.Euler(0, (id * 37) % 180, 0);
                    disc.AddComponent<MeshFilter>().sharedMesh = _discMeshes[id % 4];
                    var r = disc.AddComponent<MeshRenderer>();
                    r.material = _waterMat;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    _waterDiscs[id] = disc;
                }
                else if (wet && _waterDiscs[id] != null)
                {
                    float ws = Mathf.Lerp(0.55f, 1.35f, Mathf.Sqrt(Mathf.Clamp01(state.SurfaceWater[id] / 240f)));
                    _waterDiscs[id].transform.localScale = new Vector3(ws, 1f, ws);
                }
                else if (!wet && _waterDiscs[id] != null)
                {
                    Destroy(_waterDiscs[id]);
                    _waterDiscs[id] = null;
                }
            }

            var occupied = new HashSet<int>();
            foreach (var plant in state.Plants)
            {
                int cell = plant.CellId;
                occupied.Add(cell);
                // Rebuild when the stage changes or the plant has grown visibly (every 250 growth).
                bool suited = PlantSuited(plant, state);
                var at = PlantRoot(cell);
                var species = PlantForms.Choose(plant, at);
                var appearance = (plant.PlantId, species, plant.Stage, plant.Growth / 250, suited);
                if (_shownPlants[cell] != appearance || _plantViews[cell] == null)
                {
                    DestroyPlantView(_plantViews[cell]);
                    _plantViews[cell] = BuildPlant(plant, at, species, suited);
                    _shownPlants[cell] = appearance;
                }
            }
            for (int id = 0; id < 256; id++)
            {
                if (!occupied.Contains(id) && _plantViews[id] != null)
                {
                    DestroyPlantView(_plantViews[id]);
                    _plantViews[id] = null;
                    _shownPlants[id] = default;
                }
            }

            if (weather != null)
            {
                _weatherId = weather.Id;
                ApplySky();
                if (weather.Id == WeatherId.Drizzle) { if (!_rain.isPlaying) _rain.Play(); }
                else if (_rain.isPlaying) _rain.Stop();
            }
        }

        private Material _plantMat;

        private Vector3 PlantRoot(int cell)
        {
            var at = CellTop(cell) + IslandTerrain.PlantJitter(cell) * 0.5f;
            at.y = IslandTerrain.Height(at.x, at.z) + 0.01f;
            return at;
        }

        internal Vector3 PlantTip(PlantState plant)
        {
            var at = PlantRoot(plant.CellId);
            return PlantForms.TipPosition(plant, at, PlantForms.Choose(plant, at));
        }

        /// <summary>
        /// One swaying mesh per plant (PlantForms, DestinyGarden/Foliage), built in world space at the
        /// plant's organic position. Species follows habitat and who sowed it.
        /// </summary>
        private GameObject BuildPlant(PlantState plant, Vector3 at, PlantForms.Species species, bool suited)
        {
            if (_plantMat == null)
            {
                var fol = Shader.Find("DestinyGarden/Foliage");
                _plantMat = new Material(fol != null ? fol : Shader.Find("Sprites/Default"));
                _plantMat.SetFloat("_CanopyLighting", 1f);
                _plantMat.SetFloat("_Translucency", 0.58f);
                _plantMat.SetFloat("_WindStrength", 0.055f);
            }
            var b = new FoliageMesh { Root = new Vector4(at.x, at.y, at.z, -1f) };
            PlantForms.Build(b, plant, at, species);
            // Read clearly from the camera: scale the whole plant around its root.
            for (int i = 0; i < b.V.Count; i++) b.V[i] = at + (b.V[i] - at) * PlantForms.DisplayScale;
            // A plant whose place no longer suits it (Flora.Fits) looks pale and tired.
            if (!suited)
                for (int i = 0; i < b.C.Count; i++)
                {
                    var c = b.C[i];
                    b.C[i] = new Color(Mathf.Lerp(c.r, 0.57f, 0.24f), Mathf.Lerp(c.g, 0.53f, 0.24f), Mathf.Lerp(c.b, 0.36f, 0.24f), c.a);
                }
            var root = new GameObject("Plant_" + plant.PlantId);
            root.transform.SetParent(transform, false);
            root.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Plant_" + plant.PlantId);
            var r = root.AddComponent<MeshRenderer>();
            r.sharedMaterial = _plantMat;
            r.shadowCastingMode = plant.Stage >= PlantStage.Leaf ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return root;
        }

        private static bool PlantSuited(PlantState plant, WorldState state)
        {
            var t = DestinyGarden.Domain.Flora.Get(plant.Species);
            return t == null || state == null || plant.Stage == PlantStage.Seed || DestinyGarden.Domain.Flora.Fits(t, state, plant.CellId);
        }

        private static void DestroyPlantView(GameObject view)
        {
            if (view == null) return;
            var mf = view.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
            Destroy(view);
        }

        /// <summary>Short outcome presentation bound to saved facts. Skipped cues are discarded.</summary>
        public IEnumerator PlayOutcome(SelectedMessage message, string element, SimulationResult sim)
        {
            if (ReducedMotion) yield break;
            float tumble = 0.9f;
            _diceRolling = true;
            var startRot = _dice.transform.rotation;
            var startPos = _dice.transform.position;
            for (float t = 0; t < tumble; t += Time.deltaTime)
            {
                float k = 1f - t / tumble;
                _dice.transform.Rotate(new Vector3(1.7f, 2.3f, 0.9f) * (260f * Time.deltaTime * k));
                _dice.transform.position = startPos + Vector3.up * (Mathf.Sin(t / tumble * Mathf.PI) * 0.35f);
                yield return null;
            }
            _dice.transform.rotation = startRot;
            _dice.transform.position = startPos;
            _diceRolling = false;

            if (sim != null && element == "water")
            {
                foreach (int cell in sim.Evidence.DiceWaterCells)
                {
                    _burst.transform.position = CellTop(cell) + Vector3.up * 0.3f;
                    _burst.Emit(16);
                }
            }
            else if (element == "seed" && sim != null)
            {
                foreach (var e in sim.Events)
                    if (e.Kind == "SeedPlaced")
                    {
                        _burst.transform.position = CellTop(e.CellId) + Vector3.up * 0.25f;
                        _burst.Emit(12);
                    }
            }

            if (message != null && message.FocusCellId >= 0)
            {
                _focusRing.transform.position = CellTop(message.FocusCellId) + Vector3.up * 0.10f;
                _focusRing.SetActive(true);
                for (float t = 0; t < 1.7f; t += Time.deltaTime)
                {
                    float s = 1f + Mathf.Sin(t * 5f) * 0.07f;
                    _focusRing.transform.localScale = new Vector3(s, 1f, s);
                    yield return null;
                }
                _focusRing.SetActive(false);
            }
        }
    }
}
