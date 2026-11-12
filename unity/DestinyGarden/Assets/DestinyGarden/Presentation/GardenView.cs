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
        private static readonly Color Mist = new Color(0.30f, 0.40f, 0.52f); // pale luminous haze over the sea

        private readonly Renderer[] _cells = new Renderer[256];
        private readonly Renderer[] _domes = new Renderer[256];
        private readonly GameObject[] _waterDiscs = new GameObject[256];
        private readonly GameObject[] _plantViews = new GameObject[256];
        private readonly int[] _plantStageShown = new int[256];
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
        public Vector3 PerchPoint { get; private set; } = new Vector3(7.2f, 2.95f, 16.4f);
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
        public Camera ViewCamera { get; private set; }
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
            _sun.color = Color.Lerp(_sunBase, toward, Mathf.Abs(_balance) * 0.35f);
        }
        private GameObject _focusRing;
        private GameObject _lightShaft;
        private ParticleSystem _rain;
        private ParticleSystem _pollen;
        private ParticleSystem _burst;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
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

        public void Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? shader;

            _soilMat = Lit(shader, ProceduralArt.SoilTexture(), 0.08f);
            _stoneMat = Lit(shader, ProceduralArt.StoneTexture(), 0.18f);
            _rockMat = Lit(shader, ProceduralArt.StoneTexture(), 0.12f);
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
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 58f;
            RenderSettings.ambientLight = new Color(0.58f, 0.62f, 0.72f);

            var camObj = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camObj.AddComponent<Camera>();
            ViewCamera = cam;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(7.5f, 14.8f, -7.6f);
            cam.transform.LookAt(new Vector3(7.5f, 0.2f, 8.4f));
            cam.backgroundColor = Mist;
            cam.fieldOfView = 41f;
            var camData = camObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;

            var volumeObj = new GameObject("PostVolume");
            var volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.value = 0.85f;
            bloom.threshold.value = 0.78f;
            bloom.scatter.value = 0.72f;
            bloom.tint.value = new Color(1.0f, 0.93f, 0.86f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.16f;
            vignette.smoothness.value = 0.55f;
            var split = profile.Add<SplitToning>(true);
            split.shadows.value = new Color(0.36f, 0.52f, 0.66f);
            split.highlights.value = new Color(0.86f, 0.70f, 0.50f);
            split.balance.value = -12f;
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.value = DepthOfFieldMode.Gaussian;
            dof.gaussianStart.value = 25f;
            dof.gaussianEnd.value = 48f;
            dof.gaussianMaxRadius.value = 1.0f;
            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.value = TonemappingMode.ACES;
            var colors = profile.Add<ColorAdjustments>(true);
            colors.saturation.value = 4f;
            colors.postExposure.value = 0.50f;
            colors.contrast.value = 12f;
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
            pool.intensity = 2.2f;
            pool.shadows = LightShadows.None;

            IslandTerrain.ShadeCasters.Clear();
            BuildEnvironment(shader, unlit);
            BuildCells(shader);
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
            skyMat.SetTexture("_BaseMap", ProceduralArt.SkyGradient());
            skyMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);
            sky.GetComponent<Renderer>().material = skyMat;
            sky.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Destroy(sky.GetComponent<Collider>());

            // The round sacred island: one continuous terrain (IslandTerrain) instead of a board.
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
            // Glowing water drawn unlit with soft banks; scrolls downstream in Update.
            _riverMat = new Material(Shader.Find("Sprites/Default") ?? unlit);
            _riverMat.SetTexture("_MainTex", ProceduralArt.RiverTexture());
            _riverMat.color = new Color(0.85f, 0.97f, 1f, 0.95f);
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
            springLight.color = new Color(0.55f, 0.95f, 1f);
            springLight.range = 4.5f;
            springLight.intensity = 1.6f;
            springLight.shadows = LightShadows.None;

            // Stones along the shore and a few standing stones on the hills.
            var decor = new (float angle, float radius, float h, float w)[]
            {
                (100f, 9.4f, 3.4f, 1.05f), (128f, 9.9f, 2.6f, 1.0f), (62f, 9.6f, 2.8f, 1.0f), (20f, 10.3f, 1.6f, 1.1f),
                (330f, 10.4f, 1.2f, 1.2f), (290f, 10.6f, 0.9f, 1.3f), (250f, 10.5f, 1.0f, 1.25f), (205f, 10.2f, 1.5f, 1.1f),
                (168f, 10.0f, 2.2f, 1.0f), (148f, 10.8f, 0.8f, 1.2f), (40f, 10.9f, 0.7f, 1.3f)
            };
            for (int i = 0; i < decor.Length; i++)
            {
                float ang = decor[i].angle * Mathf.Deg2Rad;
                float x = 7.5f + Mathf.Cos(ang) * decor[i].radius, z = 7.5f + Mathf.Sin(ang) * decor[i].radius;
                float baseY = IslandTerrain.Height(x, z);
                var rock = new GameObject("DecorRock");
                rock.transform.SetParent(transform);
                rock.transform.position = new Vector3(x, baseY + decor[i].h / 2f - 0.35f, z);
                rock.transform.localScale = new Vector3(decor[i].w, decor[i].h, decor[i].w);
                rock.transform.rotation = Quaternion.Euler(0, i * 67f, (i % 3 - 1) * 4f);
                rock.AddComponent<MeshFilter>().sharedMesh = _rockMeshes[i % 3];
                var rr = rock.AddComponent<MeshRenderer>();
                rr.material = _rockMat;
                rr.shadowCastingMode = ShadowCastingMode.On;
                rr.receiveShadows = true;
                if (i == 0) PerchPoint = new Vector3(x, baseY + decor[i].h - 0.32f, z);
                IslandTerrain.AddShade(x, z, decor[i].w * 1.3f);
            }

            BuildTrees(shader);
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
        private void BuildTrees(Shader shader)
        {
            var bark = Lit(shader, ProceduralArt.WoodTexture(), 0.1f);
            bark.color = new Color(0.52f, 0.42f, 0.34f);
            var leafA = Lit(shader, ProceduralArt.MossTexture(), 0.18f);
            leafA.color = new Color(0.62f, 0.80f, 0.56f);
            var leafB = Lit(shader, ProceduralArt.MossTexture(), 0.18f);
            leafB.color = new Color(0.50f, 0.70f, 0.52f);
            var blossom = Lit(shader, ProceduralArt.MossTexture(), 0.25f);
            blossom.color = new Color(1f, 0.86f, 0.90f);
            blossom.EnableKeyword("_EMISSION");
            blossom.SetColor("_EmissionColor", new Color(0.25f, 0.16f, 0.18f));
            var spots = new (float angle, float radius, float size, int kind)[]
            {
                (70f, 8.4f, 1.15f, 1), (86f, 7.6f, 0.95f, 0), (124f, 8.6f, 1.25f, 2), (142f, 7.9f, 0.85f, 0),
                (35f, 8.3f, 0.9f, 2), (165f, 8.2f, 1.0f, 1), (12f, 8.0f, 0.7f, 3), (188f, 7.6f, 0.65f, 3),
                (55f, 7.2f, 0.6f, 3), (128f, 7.0f, 0.55f, 3)
            };
            for (int i = 0; i < spots.Length; i++)
            {
                float a = spots[i].angle * Mathf.Deg2Rad;
                float x = 7.5f + Mathf.Cos(a) * spots[i].radius, z = 7.5f + Mathf.Sin(a) * spots[i].radius;
                if (IslandTerrain.DistanceToRiver(x, z) < 1.1f) { x += 0.9f; z -= 0.6f; }
                var root = new GameObject(spots[i].kind == 3 ? "Shrub" : "Tree").transform;
                root.SetParent(transform);
                root.position = new Vector3(x, IslandTerrain.Height(x, z) - 0.04f, z);
                root.localScale = Vector3.zero;
                // Grows in with the island: shrubs first, then trees, the blossom tree last.
                float thr = spots[i].kind == 3 ? 0.40f + (i % 4) * 0.03f
                    : spots[i].kind == 2 ? 0.78f + (i % 2) * 0.04f : 0.54f + (i % 5) * 0.035f;
                _growers.Add(new Grower { T = root, Size = spots[i].size, Threshold = thr });
                IslandTerrain.AddShade(x, z, (spots[i].kind == 3 ? 0.9f : 1.7f) * spots[i].size);
                GameObject Ball(Material m, Vector3 p, float r)
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(g.GetComponent<Collider>());
                    g.transform.SetParent(root, false);
                    g.transform.localPosition = p;
                    g.transform.localScale = new Vector3(r, r * 0.86f, r);
                    g.GetComponent<MeshRenderer>().material = m;
                    return g;
                }
                if (spots[i].kind == 3)
                {
                    Ball(leafB, new Vector3(0f, 0.22f, 0f), 0.62f);
                    Ball(leafA, new Vector3(0.24f, 0.30f, 0.10f), 0.44f);
                    Ball(leafA, new Vector3(-0.20f, 0.28f, -0.12f), 0.40f);
                    continue;
                }
                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(trunk.GetComponent<Collider>());
                trunk.transform.SetParent(root, false);
                trunk.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                trunk.transform.localScale = new Vector3(0.14f, 0.7f, 0.14f);
                trunk.transform.localRotation = Quaternion.Euler((i % 3 - 1) * 5f, 0f, (i % 2) * 6f - 3f);
                trunk.GetComponent<MeshRenderer>().material = bark;
                var crownMat = spots[i].kind == 2 ? blossom : (spots[i].kind == 1 ? leafA : leafB);
                Ball(crownMat, new Vector3(0f, 1.62f, 0f), 1.05f);
                Ball(crownMat, new Vector3(0.42f, 1.42f, 0.18f), 0.72f);
                Ball(crownMat, new Vector3(-0.38f, 1.48f, -0.16f), 0.70f);
                Ball(spots[i].kind == 2 ? leafA : crownMat, new Vector3(0.05f, 1.98f, -0.10f), 0.62f);
                _swaying.Add(new KeyValuePair<Transform, float>(root, i * 0.8f));
            }
        }

        private sealed class Grower { public Transform T; public float Size; public float Threshold; }
        private readonly List<Grower> _growers = new List<Grower>();

        /// <summary>A small cottage by the spring that appears when the garden has matured.</summary>
        private void BuildCottage(Shader shader)
        {
            float a = 104f * Mathf.Deg2Rad, r = 8.2f;
            float x = 7.5f + Mathf.Cos(a) * r, z = 7.5f + Mathf.Sin(a) * r;
            var root = new GameObject("Cottage").transform;
            root.SetParent(transform);
            root.position = new Vector3(x, IslandTerrain.Height(x, z) - 0.03f, z);
            root.rotation = Quaternion.Euler(0f, 200f, 0f);
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
                    rockCell.transform.position = new Vector3(x, IslandTerrain.Height(x, y) + 0.45f, y);
                    rockCell.transform.localScale = new Vector3(1.02f, 1.85f + jitter * 0.45f, 1.0f);
                    rockCell.transform.rotation = Quaternion.Euler((jitter - 0.5f) * 8f, (id * 53) % 360, 0);
                    rockCell.AddComponent<MeshFilter>().sharedMesh = _rockMeshes[id % 3];
                    IslandTerrain.AddShade(x, y, 1.1f);
                    var rr = rockCell.AddComponent<MeshRenderer>();
                    rr.material = _rockMat;
                    rr.shadowCastingMode = ShadowCastingMode.On;
                    rr.receiveShadows = true;
                }
                // Soil beds are painted into the island surface (IslandTerrain.PaintBeds), not built as meshes.
                _plantStageShown[id] = -1;
            }

            // Ground cover lives in IslandFlora (habitat-driven colonies), built after the cells.
        }

        private void BuildDice(Shader shader)
        {
            _dice = new GameObject("Dice");
            _dice.transform.SetParent(transform);
            _dice.transform.position = CellTop(44) + new Vector3(0.35f, 0.62f, -0.05f);
            _dice.transform.localScale = Vector3.one * 1.10f;
            _dice.transform.rotation = Quaternion.Euler(-16f, 30f, 6f);
            _dice.AddComponent<MeshFilter>().sharedMesh = ProceduralArt.RoundedCubeMesh(0.30f);
            var diceRenderer = _dice.AddComponent<MeshRenderer>();
            var diceMat = Lit(shader, ProceduralArt.StoneTexture(), 0.35f);
            diceMat.color = new Color(1.60f, 1.55f, 1.40f);
            diceRenderer.material = diceMat;
            diceRenderer.shadowCastingMode = ShadowCastingMode.On;
            diceRenderer.receiveShadows = true;

            var pipMat = new Material(shader) { color = WaterGlow };
            pipMat.EnableKeyword("_EMISSION");
            pipMat.SetColor("_EmissionColor", PipEmission);
            _pipMat = pipMat;
            BuildTopPips(pipMat);
            AddPips(pipMat, Vector3.forward, 1);
            AddPips(pipMat, Vector3.right, 1);
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

        private void BuildTopPips(Material pipMat)
        {
            for (int i = 0; i < 9; i++)
            {
                var pip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pip.transform.SetParent(_dice.transform, false);
                float x = (i % 3 - 1) * 0.24f, z = (i / 3 - 1) * 0.24f;
                pip.transform.localPosition = new Vector3(x, 0.485f, z);
                pip.transform.localScale = Vector3.one * 0.115f;
                pip.GetComponent<Renderer>().material = pipMat;
                Destroy(pip.GetComponent<Collider>());
                _topPips.Add(pip);
            }
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

        private void AddPips(Material pipMat, Vector3 normal, int count)
        {
            Vector2[] layouts = count == 1 ? new[] { new Vector2(0, 0) }
                : count == 3 ? new[] { new Vector2(-0.24f, -0.24f), new Vector2(0, 0), new Vector2(0.24f, 0.24f) }
                : new[] { new Vector2(-0.24f, -0.24f), new Vector2(0.24f, -0.24f), new Vector2(0, 0), new Vector2(-0.24f, 0.24f), new Vector2(0.24f, 0.24f) };
            Vector3 right = normal == Vector3.up ? Vector3.right : Vector3.Cross(Vector3.up, normal).normalized;
            Vector3 up = normal == Vector3.up ? Vector3.forward : Vector3.up;
            foreach (var p in layouts)
            {
                var pip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pip.transform.SetParent(_dice.transform, false);
                pip.transform.localPosition = normal * 0.485f + right * p.x + up * p.y;
                pip.transform.localScale = Vector3.one * 0.115f;
                pip.GetComponent<Renderer>().material = pipMat;
                Destroy(pip.GetComponent<Collider>());
            }
        }

        private void BuildParticles(Shader unlit)
        {
            var glowTex = ProceduralArt.GlowSprite();
            _pollen = MakeParticles("Pollen", 150, new Color(1f, 0.92f, 0.62f, 0.75f), 0.055f, 8f, unlit, glowTex);
            var pollenMain = _pollen.main; pollenMain.startSpeed = 0.22f;
            _pollen.transform.position = new Vector3(7.5f, 2.0f, 7.5f);
            var shape = _pollen.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(17f, 2.6f, 17f);

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

        private void LateUpdate() => UpdateGrowers();

        private void Update()
        {
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
                occupied.Add(plant.CellId);
                // Rebuild when the stage changes or the plant has grown visibly (every 250 growth).
                int shownKey = (int)plant.Stage * 100 + plant.Growth / 250;
                if (_plantStageShown[plant.CellId] != shownKey || _plantViews[plant.CellId] == null)
                {
                    DestroyPlantView(_plantViews[plant.CellId]);
                    _plantViews[plant.CellId] = BuildPlant(plant);
                    _plantStageShown[plant.CellId] = shownKey;
                }
            }
            for (int id = 0; id < 256; id++)
            {
                if (!occupied.Contains(id) && _plantViews[id] != null)
                {
                    DestroyPlantView(_plantViews[id]);
                    _plantViews[id] = null;
                    _plantStageShown[id] = -1;
                }
            }

            if (weather != null)
            {
                _sun.intensity = weather.Id == WeatherId.Clear ? 2.40f
                    : weather.Id == WeatherId.Cloudy ? 1.95f : 1.70f;
                _sunBase = weather.Id == WeatherId.Drizzle
                    ? new Color(0.78f, 0.84f, 0.92f) : new Color(1.0f, 0.86f, 0.66f);
                ApplyBalance();
                _fill.intensity = weather.Id == WeatherId.Clear ? 0.45f
                    : weather.Id == WeatherId.Cloudy ? 0.55f : 0.65f;
                _lightShaft.SetActive(weather.Id == WeatherId.Clear);
                if (weather.Id == WeatherId.Drizzle) { if (!_rain.isPlaying) _rain.Play(); }
                else if (_rain.isPlaying) _rain.Stop();
            }
        }

        private Material _plantMat;

        /// <summary>
        /// One swaying mesh per plant (PlantForms, DestinyGarden/Foliage), built in world space at the
        /// plant's organic position. Species follows habitat and who sowed it.
        /// </summary>
        private GameObject BuildPlant(PlantState plant)
        {
            if (_plantMat == null)
            {
                var fol = Shader.Find("DestinyGarden/Foliage");
                _plantMat = new Material(fol != null ? fol : Shader.Find("Sprites/Default"));
            }
            var at = CellTop(plant.CellId) + IslandTerrain.PlantJitter(plant.CellId) * 0.5f;
            at.y = IslandTerrain.Height(at.x, at.z) + 0.01f;
            var b = new FoliageMesh { Root = new Vector4(at.x, at.y, at.z, -1f) };
            PlantForms.Build(b, plant, at, PlantForms.Choose(plant, at));
            // Read clearly from the camera: scale the whole plant around its root.
            for (int i = 0; i < b.V.Count; i++) b.V[i] = at + (b.V[i] - at) * 1.75f;
            var root = new GameObject("Plant_" + plant.PlantId);
            root.transform.SetParent(transform, false);
            root.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Plant_" + plant.PlantId);
            var r = root.AddComponent<MeshRenderer>();
            r.material = _plantMat;
            r.shadowCastingMode = ShadowCastingMode.On;
            return root;
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
