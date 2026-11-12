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
        private static readonly Color DrySoil = new Color(0.56f, 0.45f, 0.34f);
        private static readonly Color MoistSoil = new Color(0.43f, 0.32f, 0.23f);
        private static readonly Color WetSoil = new Color(0.30f, 0.22f, 0.17f);
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
            // Open ground follows the island surface (equal to the model height inside the garden).
            return new Vector3(x, GardenMapData.Solid[cellId] == 1 ? CellHeightOf(cellId) : IslandTerrain.Height(x, y), y);
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
            _waterMat.color = new Color(0.62f, 0.92f, 1f, 0.55f);
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

            BuildEnvironment(shader, unlit);
            BuildCells(shader);
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
            }

            BuildTrees(shader);

            // Distant silhouette rocks in the mist.
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f + 0.35f;
                float r = 31f + (i % 3) * 3.5f;
                var rock = new GameObject("FarRock");
                rock.transform.SetParent(transform);
                rock.transform.position = new Vector3(7.5f + Mathf.Cos(a) * r, -1.5f, 7.5f + Mathf.Sin(a) * r);
                rock.transform.localScale = new Vector3(4.5f, 6.5f + (i % 4) * 1.6f, 4.5f);
                rock.transform.rotation = Quaternion.Euler(0, i * 47f, 0);
                rock.AddComponent<MeshFilter>().sharedMesh = _rockMeshes[i % 3];
                rock.AddComponent<MeshRenderer>().material = _rockMat;
            }

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
                (70f, 8.4f, 1.15f, 1), (95f, 7.6f, 0.95f, 0), (118f, 8.6f, 1.25f, 2), (140f, 7.9f, 0.85f, 0),
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
                root.localScale = Vector3.one * spots[i].size;
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
                    var rr = rockCell.AddComponent<MeshRenderer>();
                    rr.material = _rockMat;
                    rr.shadowCastingMode = ShadowCastingMode.On;
                    rr.receiveShadows = true;
                }
                // Soil beds are painted into the island surface (IslandTerrain.PaintBeds), not built as meshes.
                _plantStageShown[id] = -1;
            }

            // Meadow detail scattered over the whole island (not on the grid): grass, tiny wildflowers, pebbles.
            var flowerColors = new[]
            {
                new Color(1f, 0.95f, 0.85f), new Color(0.98f, 0.86f, 0.40f), new Color(0.80f, 0.70f, 1f), new Color(1f, 0.72f, 0.80f)
            };
            var dotMats = new Material[flowerColors.Length];
            for (int i = 0; i < dotMats.Length; i++)
            {
                dotMats[i] = Lit(shader, null, 0.3f);
                dotMats[i].color = flowerColors[i];
                dotMats[i].EnableKeyword("_EMISSION");
                dotMats[i].SetColor("_EmissionColor", flowerColors[i] * 0.25f);
            }
            var rng = new System.Random(4207);
            int placed = 0;
            for (int tries = 0; tries < 4000 && placed < 340; tries++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * 10.2f;
                float x = 7.5f + Mathf.Cos(a) * r, z = 7.5f + Mathf.Sin(a) * r;
                int cx = Mathf.RoundToInt(x), cz = Mathf.RoundToInt(z);
                if (cx >= 0 && cx < 16 && cz >= 0 && cz < 16)
                {
                    int c = cz * 16 + cx;
                    if (GardenMapData.SoilCapacity[c] > 0 || GardenMapData.Solid[c] == 1) continue;
                }
                if (IslandTerrain.DistanceToRiver(x, z) < 0.75f) continue;
                if (r > 9.6f) continue;
                // Grow in natural clumps: mostly where the meadow noise is high.
                float clump = ProceduralArt.Fbm(x * 0.45f + 11f, z * 0.45f + 5f, 515, 3);
                if (clump < 0.50f && rng.NextDouble() > 0.15) continue;
                var pos = new Vector3(x, IslandTerrain.Height(x, z), z);
                double kind = rng.NextDouble();
                if (kind < 0.70)
                {
                    var tuft = new GameObject("Tuft");
                    tuft.transform.SetParent(root.transform);
                    tuft.transform.position = pos;
                    tuft.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    for (int bl = 0; bl < 3; bl++)
                    {
                        var blade = new GameObject("Blade");
                        blade.transform.SetParent(tuft.transform, false);
                        blade.transform.localPosition = new Vector3(bl * 0.08f - 0.08f, 0f, (bl % 2) * 0.07f);
                        blade.transform.localScale = Vector3.one * (0.6f + (float)rng.NextDouble() * 0.4f);
                        blade.transform.localRotation = Quaternion.Euler(-40f, bl * 120f, 0);
                        blade.AddComponent<MeshFilter>().sharedMesh = _leafMesh;
                        var br = blade.AddComponent<MeshRenderer>();
                        br.material = _leafMat;
                        br.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
                else if (kind < 0.93)
                {
                    var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(dot.GetComponent<Collider>());
                    dot.name = "Wildflower";
                    dot.transform.SetParent(root.transform);
                    dot.transform.position = pos + Vector3.up * 0.06f;
                    dot.transform.localScale = new Vector3(0.07f, 0.035f, 0.07f);
                    var dr = dot.GetComponent<MeshRenderer>();
                    dr.material = dotMats[rng.Next(dotMats.Length)];
                    dr.shadowCastingMode = ShadowCastingMode.Off;
                }
                else
                {
                    var pebble = new GameObject("Pebble");
                    pebble.transform.SetParent(root.transform);
                    pebble.transform.position = pos + Vector3.up * 0.02f;
                    pebble.transform.localScale = new Vector3(0.26f, 0.22f, 0.24f) * (0.7f + (float)rng.NextDouble() * 0.6f);
                    pebble.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    pebble.AddComponent<MeshFilter>().sharedMesh = _rockMeshes[rng.Next(3)];
                    pebble.AddComponent<MeshRenderer>().material = _rockMat;
                }
                placed++;
            }
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
                _waterMat.color = new Color(0.62f, 0.92f, 1f, 0.55f * pulse);
            }
        }

        public void Show(WorldState state, WeatherDefinition weather)
        {
            IslandTerrain.PaintBeds(state, DrySoil, MoistSoil, WetSoil);
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
                    float ws = Mathf.Lerp(0.90f, 1.95f, Mathf.Sqrt(Mathf.Clamp01(state.SurfaceWater[id] / 240f)));
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
                    float ws = Mathf.Lerp(0.90f, 1.95f, Mathf.Sqrt(Mathf.Clamp01(state.SurfaceWater[id] / 240f)));
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
                if (_plantStageShown[plant.CellId] != (int)plant.Stage || _plantViews[plant.CellId] == null)
                {
                    if (_plantViews[plant.CellId] != null)
                    {
                        _swaying.RemoveAll(s => s.Key == _plantViews[plant.CellId].transform);
                        Destroy(_plantViews[plant.CellId]);
                    }
                    _plantViews[plant.CellId] = BuildPlant(plant);
                    _plantStageShown[plant.CellId] = (int)plant.Stage;
                    _swaying.Add(new KeyValuePair<Transform, float>(
                        _plantViews[plant.CellId].transform, plant.CellId * 0.37f));
                }
            }
            for (int id = 0; id < 256; id++)
            {
                if (!occupied.Contains(id) && _plantViews[id] != null)
                {
                    _swaying.RemoveAll(s => s.Key == _plantViews[id].transform);
                    Destroy(_plantViews[id]);
                    _plantViews[id] = null;
                    _plantStageShown[id] = -1;
                }
            }

            if (weather != null)
            {
                _sun.intensity = weather.Id == WeatherId.Clear ? 2.40f
                    : weather.Id == WeatherId.Cloudy ? 1.95f : 1.70f;
                _sun.color = weather.Id == WeatherId.Drizzle
                    ? new Color(0.78f, 0.84f, 0.92f) : new Color(1.0f, 0.86f, 0.66f);
                _fill.intensity = weather.Id == WeatherId.Clear ? 0.45f
                    : weather.Id == WeatherId.Cloudy ? 0.55f : 0.65f;
                _lightShaft.SetActive(weather.Id == WeatherId.Clear);
                if (weather.Id == WeatherId.Drizzle) { if (!_rain.isPlaying) _rain.Play(); }
                else if (_rain.isPlaying) _rain.Stop();
            }
        }

        private GameObject BuildPlant(PlantState plant)
        {
            var root = new GameObject("Plant_" + plant.PlantId);
            root.transform.SetParent(transform);
            root.transform.position = CellTop(plant.CellId) + Vector3.up * 0.06f + IslandTerrain.PlantJitter(plant.CellId);
            root.transform.rotation = Quaternion.Euler((Hash01(plant.CellId, 1) - 0.5f) * 10f, Hash01(plant.CellId, 2) * 360f,
                (Hash01(plant.CellId, 3) - 0.5f) * 10f);
            float g = plant.Growth / 2500f;
            int species = SpeciesOf(plant);
            float tall = 0.82f + Hash01(plant.CellId, 4) * 0.36f;
            var petal = _speciesPetal[species];

            GameObject Part(PrimitiveType type, Material mat, Vector3 pos, Vector3 scale, Vector3 rot)
            {
                var part = GameObject.CreatePrimitive(type);
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = pos;
                part.transform.localScale = scale;
                part.transform.localRotation = Quaternion.Euler(rot);
                part.GetComponent<Renderer>().material = mat;
                part.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Destroy(part.GetComponent<Collider>());
                return part;
            }
            void Leaf(Vector3 pos, float yaw, float pitch, float scale)
            {
                var leaf = new GameObject("Leaf");
                leaf.transform.SetParent(root.transform, false);
                leaf.transform.localPosition = pos;
                leaf.transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
                leaf.transform.localScale = Vector3.one * scale;
                leaf.AddComponent<MeshFilter>().sharedMesh = _leafMesh;
                var r = leaf.AddComponent<MeshRenderer>();
                r.material = _leafMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (plant.Stage == PlantStage.Seed)
            {
                Part(PrimitiveType.Sphere, _seedMat, new Vector3(0, 0.05f, 0), new Vector3(0.17f, 0.13f, 0.17f), Vector3.zero);
                return root;
            }
            float stemH = (plant.Stage == PlantStage.Sprout ? 0.34f : 0.58f + g * 0.44f) * tall;
            if (species == 4) stemH *= 1.15f; // lavender stands taller
            // Slightly bent three-segment stem.
            Vector3 prev = Vector3.zero;
            for (int sIdx = 0; sIdx < 3; sIdx++)
            {
                float segH = stemH / 3f;
                Vector3 next = prev + new Vector3(0.018f * sIdx, segH, 0.012f * sIdx);
                var seg = Part(PrimitiveType.Cylinder, _stemMat, (prev + next) / 2f,
                    new Vector3(0.045f - sIdx * 0.008f, segH / 2f, 0.045f - sIdx * 0.008f), Vector3.zero);
                seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (next - prev).normalized);
                prev = next;
            }
            int leafCount = plant.Stage == PlantStage.Sprout ? 2 : 5;
            for (int i = 0; i < leafCount; i++)
            {
                float yaw = i * (360f / leafCount) + plant.CellId * 13f;
                Leaf(new Vector3(0.02f * i, stemH * (0.45f + 0.11f * i), 0.01f * i),
                    yaw, -52f + i * 4f, plant.Stage == PlantStage.Sprout ? 1.15f : 1.25f + g * 0.55f);
            }
            if (plant.Stage == PlantStage.Bud)
            {
                var bud = Part(PrimitiveType.Sphere, _budMat, prev + new Vector3(0, 0.09f, 0), new Vector3(0.14f, 0.22f, 0.14f), Vector3.zero);
                if (species != 0) bud.GetComponent<Renderer>().material = petal;
            }
            else if (plant.Stage == PlantStage.Flower)
            {
                BuildBloom(species, petal, prev, Part);
            }
            return root;
        }

        private void BuildBloom(int species, Material petal, Vector3 top,
            System.Func<PrimitiveType, Material, Vector3, Vector3, Vector3, GameObject> part)
        {
            switch (species)
            {
                case 1: // daisy: many thin white rays, a large golden centre
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i * (360f / 14f);
                        part(PrimitiveType.Sphere, petal,
                            top + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.13f, 0.10f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.13f),
                            new Vector3(0.20f, 0.025f, 0.055f), new Vector3(0, -a, 6f));
                    }
                    part(PrimitiveType.Sphere, _centerMat, top + new Vector3(0, 0.12f, 0), new Vector3(0.11f, 0.06f, 0.11f), Vector3.zero);
                    break;
                case 2: // bellflower: nodding bells along a curved tip
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * 120f + 20f;
                        var p = top + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.10f, 0.02f - i * 0.05f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.10f);
                        part(PrimitiveType.Sphere, petal, p, new Vector3(0.11f, 0.15f, 0.11f), new Vector3(20f, -a, 0f));
                        part(PrimitiveType.Sphere, _centerMat, p + new Vector3(0, -0.07f, 0), Vector3.one * 0.03f, Vector3.zero);
                    }
                    break;
                case 3: // buttercup: five glossy cupped petals
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f;
                        part(PrimitiveType.Sphere, petal,
                            top + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.08f, 0.11f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.08f),
                            new Vector3(0.13f, 0.035f, 0.12f), new Vector3(0, -a, 38f));
                    }
                    part(PrimitiveType.Sphere, _centerMat, top + new Vector3(0, 0.11f, 0), Vector3.one * 0.06f, Vector3.zero);
                    break;
                case 4: // lavender: a spike of tiny florets
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i * 137f;
                        part(PrimitiveType.Sphere, petal,
                            top + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.03f, 0.05f + i * 0.045f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.03f),
                            Vector3.one * (0.065f - i * 0.003f), Vector3.zero);
                    }
                    break;
                case 5: // wild rose: three rings of rounded petals, deep centre
                    for (int ring = 0; ring < 3; ring++)
                    {
                        int n = 6 - ring;
                        for (int i = 0; i < n; i++)
                        {
                            float a = i * (360f / n) + ring * 25f;
                            float r = 0.13f - ring * 0.04f;
                            part(PrimitiveType.Sphere, petal,
                                top + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * r, 0.09f + ring * 0.035f, Mathf.Sin(a * Mathf.Deg2Rad) * r),
                                new Vector3(0.15f - ring * 0.03f, 0.05f, 0.12f - ring * 0.02f), new Vector3(0, -a, 22f + ring * 14f));
                        }
                    }
                    break;
                default: // cosmos: two layers of soft pink petals
                    for (int layer = 0; layer < 2; layer++)
                    {
                        int petals = layer == 0 ? 6 : 5;
                        for (int i = 0; i < petals; i++)
                        {
                            float angle = i * (360f / petals) + layer * 30f;
                            float radius = layer == 0 ? 0.16f : 0.10f;
                            part(PrimitiveType.Sphere, petal,
                                top + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * radius, 0.10f + layer * 0.045f, Mathf.Sin(angle * Mathf.Deg2Rad) * radius),
                                new Vector3(0.22f - layer * 0.05f, 0.05f, 0.14f - layer * 0.03f), new Vector3(0, -angle, 14f - layer * 8f));
                        }
                    }
                    part(PrimitiveType.Sphere, _centerMat, top + new Vector3(0, 0.15f, 0), Vector3.one * 0.10f, Vector3.zero);
                    break;
            }
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
