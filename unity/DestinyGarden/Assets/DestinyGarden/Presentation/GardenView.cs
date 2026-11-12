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
        private static readonly Color DrySoil = new Color(0.85f, 0.72f, 0.55f);
        private static readonly Color MoistSoil = new Color(0.62f, 0.45f, 0.30f);
        private static readonly Color WetSoil = new Color(0.30f, 0.22f, 0.16f);
        private static readonly Color WaterGlow = new Color(0.20f, 0.78f, 0.95f);
        private static readonly Color Mist = new Color(0.003f, 0.020f, 0.030f);

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
        private Material _mossMat;
        private Mesh[] _rockMeshes;
        private Mesh[] _discMeshes;
        private Mesh _leafMesh;
        private Light _sun;
        private Light _fill;
        private GameObject _dice;
        private readonly List<GameObject> _topPips = new List<GameObject>();
        private static readonly int[][] PipLayouts = { new[]{4}, new[]{0,8}, new[]{0,4,8}, new[]{0,2,6,8}, new[]{0,2,4,6,8}, new[]{0,2,3,5,6,8} };
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
            return new Vector3(x, CellHeightOf(cellId), y);
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
            _seedMat = Lit(shader, null, 0.3f);
            _seedMat.color = new Color(0.687f, 0.479f, 0.254f);
            _mossMat = Lit(shader, ProceduralArt.MossTexture(), 0.15f);

            _waterMat = Lit(shader, ProceduralArt.GlowSprite(), 0.65f);
            _waterMat.SetColor("_BaseColor", new Color(0.02f, 0.45f, 0.70f));
            _waterMat.EnableKeyword("_EMISSION");
            _waterMat.SetColor("_EmissionColor", new Color(0.08f, 1.55f, 1.85f));

            _rockMeshes = new[] { ProceduralArt.RockMesh(0), ProceduralArt.RockMesh(1), ProceduralArt.RockMesh(2) };
            _discMeshes = new[] { ProceduralArt.IrregularDiscMesh(0), ProceduralArt.IrregularDiscMesh(1),
                ProceduralArt.IrregularDiscMesh(2), ProceduralArt.IrregularDiscMesh(3) };
            _leafMesh = ProceduralArt.LeafMesh();

            RenderSettings.fog = true;
            RenderSettings.fogColor = Mist;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 20f;
            RenderSettings.fogEndDistance = 46f;
            RenderSettings.ambientLight = new Color(0.34f, 0.46f, 0.52f);

            var camObj = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camObj.AddComponent<Camera>();
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
            bloom.intensity.value = 0.55f;
            bloom.threshold.value = 0.85f;
            bloom.scatter.value = 0.65f;
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.30f;
            vignette.smoothness.value = 0.55f;
            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.value = TonemappingMode.ACES;
            var colors = profile.Add<ColorAdjustments>(true);
            colors.saturation.value = 9f;
            colors.postExposure.value = 0.22f;
            colors.contrast.value = 12f;
            volume.profile = profile;

            var sunObj = new GameObject("Sun");
            _sun = sunObj.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.color = new Color(1.0f, 0.694f, 0.352f);
            _sun.transform.rotation = Quaternion.Euler(50f, -42f, 0f);
            _sun.intensity = 2.00f;
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

            // Board base and wooden frame, as in the reference diorama.
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "BoardBase";
            board.transform.SetParent(transform);
            board.transform.position = new Vector3(7.5f, -0.62f, 7.5f);
            board.transform.localScale = new Vector3(18.6f, 0.75f, 18.6f);
            var woodMat = Lit(shader, ProceduralArt.WoodTexture(), 0.15f);
            board.GetComponent<Renderer>().material = woodMat;
            board.GetComponent<Renderer>().receiveShadows = true;

            for (int i = 0; i < 4; i++)
            {
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.name = "FrameBeam";
                beam.transform.SetParent(transform);
                float a = i * 90f * Mathf.Deg2Rad;
                beam.transform.position = new Vector3(7.5f + Mathf.Sin(a) * 8.85f, 0.02f, 7.5f + Mathf.Cos(a) * 8.85f);
                beam.transform.rotation = Quaternion.Euler(0, i * 90f, 0);
                beam.transform.localScale = new Vector3(18.2f, 0.55f, 0.55f);
                beam.GetComponent<Renderer>().material = woodMat;
                beam.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            }

            // Decorative rocks on the frame (presentation dressing, outside cells).
            var decor = new (float x, float z, float h, float w)[]
            {
                (-0.6f, 15.8f, 3.2f, 1.05f), (7.2f, 16.4f, 3.4f, 1.05f), (15.9f, 15.5f, 2.9f, 1.0f),
                (-0.9f, 4.5f, 2.2f, 0.95f), (-0.5f, 10.5f, 2.0f, 0.9f),
                (16.2f, 6.0f, 2.3f, 0.95f), (15.8f, 11.5f, 2.1f, 0.9f),
                (0.2f, -0.9f, 0.9f, 1.25f), (14.8f, -0.8f, 1.0f, 1.2f)
            };
            for (int i = 0; i < decor.Length; i++)
            {
                var rock = new GameObject("DecorRock");
                rock.transform.SetParent(transform);
                rock.transform.position = new Vector3(decor[i].x, decor[i].h / 2f - 0.55f, decor[i].z);
                rock.transform.localScale = new Vector3(decor[i].w, decor[i].h, decor[i].w);
                rock.transform.rotation = Quaternion.Euler(0, i * 67f, 0);
                rock.AddComponent<MeshFilter>().sharedMesh = _rockMeshes[i % 3];
                var rr = rock.AddComponent<MeshRenderer>();
                rr.material = _rockMat;
                rr.shadowCastingMode = ShadowCastingMode.On;
                rr.receiveShadows = true;
            }

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
            mistMat.color = new Color(0.8f, 0.92f, 0.96f, 0.55f);
            mistMat.SetColor("_BaseColor", new Color(0.8f, 0.92f, 0.96f, 0.55f));
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
                plane.transform.position = new Vector3(7.5f + Mathf.Cos(a) * 13.5f, 0.45f + (i % 2) * 0.3f, 7.5f + Mathf.Sin(a) * 13.5f);
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

                GameObject cell;
                Renderer renderer;
                if (solid)
                {
                    cell = new GameObject("Rock_" + id);
                    cell.transform.SetParent(root.transform);
                    cell.transform.position = new Vector3(x, 0.62f, y);
                    cell.transform.localScale = new Vector3(1.02f, 1.85f + jitter * 0.45f, 1.0f);
                    cell.transform.rotation = Quaternion.Euler(0, (id * 53) % 360, 0);
                    cell.AddComponent<MeshFilter>().sharedMesh = _rockMeshes[id % 3];
                    renderer = cell.AddComponent<MeshRenderer>();
                    renderer.material = _rockMat;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }
                else
                {
                    cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cell.transform.SetParent(root.transform);
                    float h = CellHeightOf(id);
                    cell.transform.position = new Vector3(x, h / 2f - 0.12f, y);
                    cell.transform.localScale = new Vector3(soil ? 0.98f : 0.94f, h, soil ? 0.98f : 0.94f);
                    cell.transform.rotation = Quaternion.Euler(0, (jitter - 0.5f) * 2.2f, 0);
                    renderer = cell.GetComponent<Renderer>();
                    renderer.material = soil ? _mossMat : _stoneMat;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    Destroy(cell.GetComponent<Collider>());

                    if (soil)
                    {
                        var dome = new GameObject("SoilDome");
                        dome.transform.SetParent(root.transform);
                        dome.transform.position = new Vector3(x, h - 0.10f, y);
                        dome.transform.localScale = new Vector3(0.96f, 0.16f, 0.96f);
                        dome.AddComponent<MeshFilter>().sharedMesh = domeMesh;
                        var domeRenderer = dome.AddComponent<MeshRenderer>();
                        domeRenderer.material = _soilMat;
                        domeRenderer.shadowCastingMode = ShadowCastingMode.On;
                        domeRenderer.receiveShadows = true;
                        _domes[id] = domeRenderer;
                    }
                    else if (jitter > 0.87f)
                    {
                        // Sparse grass tufts and pebbles on stone ground.
                        var tuft = new GameObject("Tuft");
                        tuft.transform.SetParent(root.transform);
                        tuft.transform.position = CellTop(id) + new Vector3((jitter - 0.85f) * 1.6f, 0.05f, (jitter - 0.8f) * 1.4f);
                        for (int b = 0; b < 3; b++)
                        {
                            var blade = new GameObject("Blade");
                            blade.transform.SetParent(tuft.transform, false);
                            blade.transform.localPosition = new Vector3(b * 0.09f - 0.09f, 0f, (b % 2) * 0.08f);
                            blade.transform.localScale = Vector3.one * 0.8f;
                            blade.transform.localRotation = Quaternion.Euler(-38f, b * 120f, 0);
                            blade.AddComponent<MeshFilter>().sharedMesh = _leafMesh;
                            blade.AddComponent<MeshRenderer>().material = _leafMat;
                        }
                    }
                    else if (jitter < 0.10f)
                    {
                        var pebble = new GameObject("Pebble");
                        pebble.transform.SetParent(root.transform);
                        pebble.transform.position = CellTop(id) + new Vector3(0.18f, 0.02f, -0.14f);
                        pebble.transform.localScale = new Vector3(0.30f, 0.36f, 0.28f);
                        pebble.AddComponent<MeshFilter>().sharedMesh = _rockMeshes[(id + 1) % 3];
                        pebble.AddComponent<MeshRenderer>().material = _rockMat;
                    }
                }
                renderer.receiveShadows = true;
                _cells[id] = renderer;
                _plantStageShown[id] = -1;
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
            pipMat.SetColor("_EmissionColor", new Color(0.10f, 2.80f, 2.35f));
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
            if (faceId < 1 || faceId > 6) return;
            var on = new HashSet<int>(PipLayouts[faceId - 1]);
            for (int i = 0; i < _topPips.Count; i++) _topPips[i].SetActive(on.Contains(i));
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
            if (_waterMat != null)
            {
                float pulse = 1.0f + Mathf.Sin(t * 1.8f) * 0.08f;
                _waterMat.SetColor("_EmissionColor", new Color(0.08f, 1.55f, 1.85f) * pulse);
            }
        }

        public void Show(WorldState state, WeatherDefinition weather)
        {
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
                    _cells[id].SetPropertyBlock(_block);
                    if (_domes[id] != null) _domes[id].SetPropertyBlock(_block);
                }
                else if (GardenMapData.Solid[id] == 0)
                {
                    float jitter = 0.88f + ((id * 2654435761u) % 100u) / 100f * 0.22f;
                    _block.SetColor("_BaseColor", new Color(jitter, jitter, jitter));
                    _cells[id].SetPropertyBlock(_block);
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
                _sun.intensity = weather.Id == WeatherId.Clear ? 2.00f
                    : weather.Id == WeatherId.Cloudy ? 1.50f : 0.90f;
                _sun.color = weather.Id == WeatherId.Drizzle
                    ? new Color(0.552f, 0.687f, 0.807f) : new Color(1.0f, 0.694f, 0.352f);
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
            root.transform.position = CellTop(plant.CellId) + Vector3.up * 0.06f;
            float g = plant.Growth / 2500f;

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
            float stemH = plant.Stage == PlantStage.Sprout ? 0.34f : 0.58f + g * 0.44f;
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
                Part(PrimitiveType.Sphere, _budMat, prev + new Vector3(0, 0.09f, 0), new Vector3(0.15f, 0.23f, 0.15f), Vector3.zero);
            }
            else if (plant.Stage == PlantStage.Flower)
            {
                for (int layer = 0; layer < 2; layer++)
                {
                    int petals = layer == 0 ? 6 : 5;
                    for (int i = 0; i < petals; i++)
                    {
                        float angle = i * (360f / petals) + layer * 30f;
                        float radius = layer == 0 ? 0.16f : 0.10f;
                        Part(PrimitiveType.Sphere, _petalMat,
                            prev + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * radius, 0.10f + layer * 0.045f, Mathf.Sin(angle * Mathf.Deg2Rad) * radius),
                            new Vector3(0.22f - layer * 0.05f, 0.05f, 0.14f - layer * 0.03f), new Vector3(0, -angle, 14f - layer * 8f));
                    }
                }
                var center = Part(PrimitiveType.Sphere, _petalMat, prev + new Vector3(0, 0.15f, 0), Vector3.one * 0.10f, Vector3.zero);
                center.GetComponent<Renderer>().material = _centerMat;
            }
            return root;
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
