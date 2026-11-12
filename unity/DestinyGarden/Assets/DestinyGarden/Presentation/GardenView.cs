using System.Collections;
using System.Collections.Generic;
using DestinyGarden.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// Renders the saved WorldState as a floating garden board in teal mist.
    /// The view never advances the model; it only displays committed states
    /// and plays short effects bound to the resolved outcome's evidence.
    /// </summary>
    public sealed class GardenView : MonoBehaviour
    {
        private static readonly Color DrySoil = new Color(0.62f, 0.48f, 0.30f);
        private static readonly Color WetSoil = new Color(0.16f, 0.11f, 0.07f);
        private static readonly Color MossGreen = new Color(0.26f, 0.44f, 0.22f);
        private static readonly Color StoneGround = new Color(0.24f, 0.30f, 0.26f);
        private static readonly Color RockGray = new Color(0.55f, 0.58f, 0.60f);
        private static readonly Color WaterGlow = new Color(0.20f, 0.75f, 0.90f);
        private static readonly Color Mist = new Color(0.05f, 0.16f, 0.20f);

        private readonly Renderer[] _cells = new Renderer[256];
        private readonly GameObject[] _waterDiscs = new GameObject[256];
        private readonly GameObject[] _plantViews = new GameObject[256];
        private readonly int[] _plantStageShown = new int[256];
        private Material _baseMat;
        private Material _waterMat;
        private Material _leafMat;
        private Material _stemMat;
        private Material _petalMat;
        private Material _budMat;
        private Light _sun;
        private GameObject _dice;
        private GameObject _focusRing;
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

        // Visual height only: model heights (0..1140) are normalized to gentle relief.
        private static float CellHeightOf(int cellId) =>
            GardenMapData.Solid[cellId] == 1 ? 1.2f : 0.30f + GardenMapData.CellHeight[cellId] / 1140f * 0.85f;

        public void Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _baseMat = new Material(shader);
            _waterMat = new Material(shader)
            {
                color = new Color(WaterGlow.r, WaterGlow.g, WaterGlow.b, 0.75f)
            };
            _waterMat.EnableKeyword("_EMISSION");
            _waterMat.SetColor("_EmissionColor", WaterGlow * 0.45f);
            _waterMat.SetFloat("_Surface", 1f);
            _waterMat.SetFloat("_Blend", 0f);
            _waterMat.renderQueue = 3000;
            _leafMat = new Material(shader) { color = new Color(0.35f, 0.62f, 0.30f) };
            _stemMat = new Material(shader) { color = new Color(0.25f, 0.42f, 0.22f) };
            _budMat = new Material(shader) { color = new Color(0.75f, 0.55f, 0.62f) };
            _petalMat = new Material(shader) { color = new Color(0.96f, 0.80f, 0.86f) };
            _petalMat.EnableKeyword("_EMISSION");
            _petalMat.SetColor("_EmissionColor", new Color(0.45f, 0.22f, 0.28f));

            RenderSettings.fog = true;
            RenderSettings.fogColor = Mist;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 44f;
            RenderSettings.ambientLight = new Color(0.30f, 0.42f, 0.48f);

            var camObj = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(7.5f, 15.5f, -8.5f);
            cam.transform.LookAt(new Vector3(7.5f, 0f, 8.2f));
            cam.backgroundColor = Mist;
            cam.fieldOfView = 40f;
            var camData = camObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            var volumeObj = new GameObject("PostVolume");
            var volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.value = 0.55f;
            bloom.threshold.value = 0.72f;
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.32f;
            volume.profile = profile;

            var sunObj = new GameObject("Sun");
            _sun = sunObj.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.color = new Color(1.0f, 0.88f, 0.70f);
            _sun.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
            _sun.intensity = 1.25f;
            _sun.shadows = LightShadows.Soft;

            // Floating board base.
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "BoardBase";
            board.transform.SetParent(transform);
            board.transform.position = new Vector3(7.5f, -0.55f, 7.5f);
            board.transform.localScale = new Vector3(17.4f, 0.7f, 17.4f);
            board.GetComponent<Renderer>().material = new Material(shader)
            { color = new Color(0.16f, 0.20f, 0.18f) };

            var root = new GameObject("Cells");
            root.transform.SetParent(transform);
            for (int id = 0; id < 256; id++)
            {
                int x = id % 16, y = id / 16;
                bool solid = GardenMapData.Solid[id] == 1;
                GameObject cell;
                if (solid)
                {
                    cell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    cell.transform.position = new Vector3(x, 0.55f, y);
                    cell.transform.localScale = new Vector3(0.95f, 1.9f, 0.9f);
                    cell.transform.rotation = Quaternion.Euler(0, (id * 37) % 40 - 20, 0);
                }
                else
                {
                    cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    float h = CellHeightOf(id);
                    cell.transform.position = new Vector3(x, h / 2f, y);
                    cell.transform.localScale = new Vector3(0.93f, h, 0.93f);
                }
                cell.transform.SetParent(root.transform);
                var renderer = cell.GetComponent<Renderer>();
                renderer.material = _baseMat;
                renderer.shadowCastingMode = solid ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                _cells[id] = renderer;
                _plantStageShown[id] = -1;
            }
            BuildDice(shader);
            BuildParticles(shader);
        }

        private void BuildDice(Shader shader)
        {
            _dice = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _dice.name = "Dice";
            _dice.transform.SetParent(transform);
            _dice.transform.position = CellTop(15) + Vector3.up * 0.72f;
            _dice.transform.localScale = Vector3.one * 1.35f;
            _dice.transform.rotation = Quaternion.Euler(-18f, 32f, 8f);
            var mat = new Material(shader) { color = new Color(0.93f, 0.92f, 0.88f) };
            _dice.GetComponent<Renderer>().material = mat;
            _dice.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            // Glowing teal pips on the three visible faces (top/front/right).
            var pipMat = new Material(shader) { color = WaterGlow };
            pipMat.EnableKeyword("_EMISSION");
            pipMat.SetColor("_EmissionColor", WaterGlow * 1.6f);
            AddPips(pipMat, Vector3.up, 5);
            AddPips(pipMat, Vector3.forward, 3);
            AddPips(pipMat, Vector3.right, 1);

            _focusRing = new GameObject("FocusRing");
            _focusRing.transform.SetParent(transform);
            var ringMat = new Material(shader) { color = new Color(1.0f, 0.82f, 0.45f) };
            ringMat.EnableKeyword("_EMISSION");
            ringMat.SetColor("_EmissionColor", new Color(1.2f, 0.85f, 0.35f));
            var line = _focusRing.AddComponent<LineRenderer>();
            line.material = ringMat;
            line.loop = true;
            line.useWorldSpace = false;
            line.widthMultiplier = 0.045f;
            const int segments = 40;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.55f, 0, Mathf.Sin(a) * 0.55f));
            }
            _focusRing.SetActive(false);
        }

        private void AddPips(Material pipMat, Vector3 normal, int count)
        {
            Vector2[] layouts = count == 1 ? new[] { new Vector2(0, 0) }
                : count == 3 ? new[] { new Vector2(-0.25f, -0.25f), new Vector2(0, 0), new Vector2(0.25f, 0.25f) }
                : new[] { new Vector2(-0.25f, -0.25f), new Vector2(0.25f, -0.25f), new Vector2(0, 0), new Vector2(-0.25f, 0.25f), new Vector2(0.25f, 0.25f) };
            Vector3 right = normal == Vector3.up ? Vector3.right : Vector3.Cross(Vector3.up, normal).normalized;
            Vector3 up = normal == Vector3.up ? Vector3.forward : Vector3.up;
            foreach (var p in layouts)
            {
                var pip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pip.transform.SetParent(_dice.transform, false);
                pip.transform.localPosition = normal * 0.5f + right * p.x + up * p.y;
                pip.transform.localScale = Vector3.one * 0.13f;
                pip.GetComponent<Renderer>().material = pipMat;
                Destroy(pip.GetComponent<Collider>());
            }
        }

        private void BuildParticles(Shader shader)
        {
            _pollen = MakeParticles("Pollen", 90, new Color(1f, 0.9f, 0.6f, 0.5f), 0.05f, 7f, shader);
            var pollenMain = _pollen.main;
            pollenMain.startSpeed = 0.25f;
            _pollen.transform.position = new Vector3(7.5f, 2.2f, 7.5f);
            var shape = _pollen.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(17f, 2.5f, 17f);

            _rain = MakeParticles("Rain", 700, new Color(0.6f, 0.85f, 1f, 0.55f), 0.04f, 1.1f, shader);
            var rainMain = _rain.main;
            rainMain.startSpeed = 6.5f;
            rainMain.gravityModifier = 0.4f;
            _rain.transform.position = new Vector3(7.5f, 9f, 7.5f);
            _rain.transform.rotation = Quaternion.Euler(90f, 0, 0);
            var rShape = _rain.shape;
            rShape.shapeType = ParticleSystemShapeType.Box;
            rShape.scale = new Vector3(17f, 17f, 0.5f);
            _rain.Stop();

            _burst = MakeParticles("Burst", 60, WaterGlow, 0.07f, 0.9f, shader);
            var burstMain = _burst.main;
            burstMain.startSpeed = 1.6f;
            burstMain.gravityModifier = -0.15f;
            var emission = _burst.emission;
            emission.rateOverTime = 0;
            _burst.Stop();
        }

        private ParticleSystem MakeParticles(string name, int max, Color color, float size, float lifetime, Shader shader)
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
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(shader) { color = color };
            return ps;
        }

        public void Show(WorldState state, WeatherDefinition weather)
        {
            for (int id = 0; id < 256; id++)
            {
                Color color;
                if (GardenMapData.Solid[id] == 1) color = RockGray;
                else if (GardenMapData.SoilCapacity[id] > 0)
                {
                    float moisture = (float)state.SoilWater[id] / GardenMapData.SoilCapacity[id];
                    color = Color.Lerp(DrySoil, WetSoil, Mathf.Clamp01(moisture * 1.15f));
                    if (moisture > 0.45f && GardenMapData.Plantable[id] == 1)
                        color = Color.Lerp(color, MossGreen, 0.25f);
                }
                else color = StoneGround;
                // Deterministic per-cell tint so the board does not read as flat tiling.
                float tint = 0.90f + ((id * 2654435761u) % 100u) / 100f * 0.18f;
                color = new Color(color.r * tint, color.g * tint, color.b * tint);
                _block.SetColor("_BaseColor", color);
                _cells[id].SetPropertyBlock(_block);

                bool wet = state.SurfaceWater[id] > 0 && GardenMapData.Solid[id] == 0;
                if (wet && _waterDiscs[id] == null)
                {
                    var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    disc.transform.SetParent(transform);
                    disc.transform.position = CellTop(id) + Vector3.up * 0.02f;
                    disc.transform.localScale = new Vector3(0.70f, 0.02f, 0.70f);
                    disc.GetComponent<Renderer>().material = _waterMat;
                    Destroy(disc.GetComponent<Collider>());
                    _waterDiscs[id] = disc;
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
                    if (_plantViews[plant.CellId] != null) Destroy(_plantViews[plant.CellId]);
                    _plantViews[plant.CellId] = BuildPlant(plant);
                    _plantStageShown[plant.CellId] = (int)plant.Stage;
                }
            }
            for (int id = 0; id < 256; id++)
            {
                if (!occupied.Contains(id) && _plantViews[id] != null)
                {
                    Destroy(_plantViews[id]);
                    _plantViews[id] = null;
                    _plantStageShown[id] = -1;
                }
            }

            if (weather != null)
            {
                _sun.intensity = weather.Id == WeatherId.Clear ? 1.25f
                    : weather.Id == WeatherId.Cloudy ? 0.85f : 0.6f;
                _sun.color = weather.Id == WeatherId.Drizzle
                    ? new Color(0.80f, 0.88f, 1.0f) : new Color(1.0f, 0.88f, 0.70f);
                if (weather.Id == WeatherId.Drizzle) { if (!_rain.isPlaying) _rain.Play(); }
                else if (_rain.isPlaying) _rain.Stop();
            }
        }

        private GameObject BuildPlant(PlantState plant)
        {
            var root = new GameObject("Plant_" + plant.PlantId);
            root.transform.SetParent(transform);
            root.transform.position = CellTop(plant.CellId);
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

            if (plant.Stage == PlantStage.Seed)
            {
                Part(PrimitiveType.Sphere, _budMat, new Vector3(0, 0.06f, 0), Vector3.one * 0.12f, Vector3.zero);
                return root;
            }
            float stemH = 0.25f + g * 0.55f;
            Part(PrimitiveType.Cylinder, _stemMat, new Vector3(0, stemH / 2f, 0), new Vector3(0.05f, stemH / 2f, 0.05f), Vector3.zero);
            int leafCount = plant.Stage == PlantStage.Sprout ? 2 : 4;
            for (int i = 0; i < leafCount; i++)
            {
                float angle = i * (360f / leafCount) + 20f;
                var leaf = Part(PrimitiveType.Sphere, _leafMat,
                    new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 0.13f, stemH * (0.55f + 0.1f * i), Mathf.Sin(angle * Mathf.Deg2Rad) * 0.13f),
                    new Vector3(0.22f, 0.06f, 0.13f), new Vector3(0, -angle, 18f));
                leaf.transform.localScale *= (0.7f + g * 0.6f);
            }
            if (plant.Stage == PlantStage.Bud)
            {
                Part(PrimitiveType.Sphere, _budMat, new Vector3(0, stemH + 0.10f, 0), new Vector3(0.16f, 0.22f, 0.16f), Vector3.zero);
            }
            else if (plant.Stage == PlantStage.Flower)
            {
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * 72f;
                    Part(PrimitiveType.Sphere, _petalMat,
                        new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 0.14f, stemH + 0.12f, Mathf.Sin(angle * Mathf.Deg2Rad) * 0.14f),
                        new Vector3(0.20f, 0.05f, 0.13f), new Vector3(0, -angle, 12f));
                }
                Part(PrimitiveType.Sphere, _petalMat, new Vector3(0, stemH + 0.14f, 0), Vector3.one * 0.10f, Vector3.zero);
            }
            return root;
        }

        /// <summary>Short outcome presentation bound to saved facts. Skipped cues are discarded.</summary>
        public IEnumerator PlayOutcome(SelectedMessage message, string element, SimulationResult sim)
        {
            if (ReducedMotion) yield break;
            // Dice tumble (cosmetic; the logical result is already committed).
            float tumble = 0.85f;
            var startRot = _dice.transform.rotation;
            for (float t = 0; t < tumble; t += Time.deltaTime)
            {
                _dice.transform.Rotate(new Vector3(1.7f, 2.3f, 0.9f) * (240f * Time.deltaTime * (1f - t / tumble)));
                yield return null;
            }
            _dice.transform.rotation = startRot;

            if (sim != null && sim.Evidence.DiceWaterCells.Count > 0 && element == "water")
            {
                foreach (int cell in sim.Evidence.DiceWaterCells)
                {
                    _burst.transform.position = CellTop(cell) + Vector3.up * 0.25f;
                    _burst.Emit(14);
                }
            }
            else if (element == "seed" && sim != null)
            {
                foreach (var e in sim.Events)
                    if (e.Kind == "SeedPlaced")
                    {
                        _burst.transform.position = CellTop(e.CellId) + Vector3.up * 0.2f;
                        _burst.Emit(10);
                    }
            }

            if (message != null && message.FocusCellId >= 0)
            {
                _focusRing.transform.position = CellTop(message.FocusCellId) + Vector3.up * 0.08f;
                _focusRing.SetActive(true);
                for (float t = 0; t < 1.6f; t += Time.deltaTime)
                {
                    float s = 1f + Mathf.Sin(t * 5f) * 0.08f;
                    _focusRing.transform.localScale = new Vector3(s, 1f, s);
                    yield return null;
                }
                _focusRing.SetActive(false);
            }
        }
    }
}
