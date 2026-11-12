using System.IO;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using DestinyGarden.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// Minimal G1/G2 presentation: renders the saved WorldState as a 16x16 garden,
    /// one fixed camera, one roll button. Presentation never draws its own dice or
    /// nature results; everything shown comes from the coordinator's saved state.
    /// </summary>
    public sealed class GardenBootstrap : MonoBehaviour
    {
        private GardenCoordinator _coordinator;
        private readonly GameObject[] _cellViews = new GameObject[256];
        private readonly GameObject[] _plantViews = new GameObject[256];
        private Material _cellMaterialProto;
        private Text _statusText;
        private Text _facesText;
        private Light _sun;

        private void Start()
        {
            string journalPath = Path.Combine(UnityEngine.Application.persistentDataPath, "destiny-garden", "journal.v1.bin");
            var clock = new SystemClock();
            var journal = new FileJournal(journalPath, clock);
            _coordinator = new GardenCoordinator(journal, new LocalEntropy(), new GuidIdSource());

            var read = journal.ReadAll();
            if (read.ChainValid && read.Entries.Count > 0) _coordinator.Restore(read);
            if (_coordinator.State == null)
            {
                _coordinator.CreateRun();
                _coordinator.Prepare();
            }
            else if (_coordinator.Phase == CoordinatorPhase.Ready || _coordinator.Phase == CoordinatorPhase.Resolved)
            {
                _coordinator.Prepare();
            }

            BuildScene();
            RefreshView("公開された6面を見て、サイコロを振ってください。");
        }

        private void BuildScene()
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.55f, 0.62f, 0.72f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 34f;

            var camObj = new GameObject("Main Camera");
            var cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
            cam.transform.position = new Vector3(8f, 13f, -4f);
            cam.transform.LookAt(new Vector3(8f, 0f, 8f));
            cam.backgroundColor = new Color(0.62f, 0.70f, 0.82f);

            var sunObj = new GameObject("Sun");
            _sun = sunObj.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            _sun.intensity = 1.1f;

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Sprites/Default");
            _cellMaterialProto = new Material(shader);
            var gardenRoot = new GameObject("Garden");
            for (int id = 0; id < 256; id++)
            {
                int x = id % 16, y = id / 16;
                var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cell.transform.SetParent(gardenRoot.transform);
                float h = 0.25f + GardenMapData.CellHeight[id] * 0.004f;
                if (GardenMapData.Solid[id] == 1) h = 1.4f;
                cell.transform.position = new Vector3(x, h / 2f, y);
                cell.transform.localScale = new Vector3(0.94f, h, 0.94f);
                cell.GetComponent<Renderer>().material = new Material(_cellMaterialProto);
                _cellViews[id] = cell;
            }
            BuildUi();
        }

        private void BuildUi()
        {
            var canvasObj = new GameObject("Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            _statusText = MakeText(canvasObj.transform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), 26);
            _facesText = MakeText(canvasObj.transform, new Vector2(0.5f, 1f), new Vector2(0f, -118f), 20);

            var buttonObj = new GameObject("RollButton");
            buttonObj.transform.SetParent(canvasObj.transform, false);
            var img = buttonObj.AddComponent<Image>();
            img.color = new Color(0.16f, 0.22f, 0.34f, 0.92f);
            var rect = buttonObj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 64f);
            rect.sizeDelta = new Vector2(280f, 72f);
            var button = buttonObj.AddComponent<Button>();
            var label = MakeText(buttonObj.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 26);
            label.text = "サイコロを振る";
            button.onClick.AddListener(OnRollPressed);
        }

        private Text MakeText(Transform parent, Vector2 anchor, Vector2 pos, int size)
        {
            var obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(1100f, 48f);
            return text;
        }

        private void OnRollPressed()
        {
            if (_coordinator.Phase == CoordinatorPhase.Prepared)
                _coordinator.CommitIntent("none", null);
            if (_coordinator.Phase != CoordinatorPhase.IntentCommitted) return;
            var outcome = _coordinator.Roll();
            if (outcome == null) { RefreshView("この回は成立しませんでした。"); return; }
            string msg = $"{outcome.FaceId}の目「{RecipeCatalog.LabelJa(outcome.RecipeId)}」が庭へ届きました。";
            _coordinator.Prepare();
            RefreshView(msg);
        }

        private void RefreshView(string message)
        {
            var state = _coordinator.State;
            for (int id = 0; id < 256; id++)
            {
                var renderer = _cellViews[id].GetComponent<Renderer>();
                Color color;
                if (GardenMapData.Solid[id] == 1) color = new Color(0.45f, 0.46f, 0.50f);
                else if (state.SurfaceWater[id] > 0) color = new Color(0.25f, 0.45f, 0.75f);
                else if (GardenMapData.SoilCapacity[id] > 0)
                {
                    float moisture = (float)state.SoilWater[id] / GardenMapData.SoilCapacity[id];
                    color = Color.Lerp(new Color(0.72f, 0.60f, 0.42f), new Color(0.30f, 0.42f, 0.26f), moisture);
                }
                else color = new Color(0.62f, 0.60f, 0.56f);
                renderer.material.color = color;
            }
            for (int id = 0; id < 256; id++)
            {
                if (_plantViews[id] != null) { Destroy(_plantViews[id]); _plantViews[id] = null; }
            }
            foreach (var plant in state.Plants)
            {
                var view = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                int x = plant.CellId % 16, y = plant.CellId / 16;
                float scale = 0.16f + plant.Growth / 2500f * 0.5f;
                view.transform.position = new Vector3(x, 0.75f, y);
                view.transform.localScale = Vector3.one * scale;
                var mat = new Material(_cellMaterialProto);
                mat.color = plant.Stage == PlantStage.Flower
                    ? new Color(0.95f, 0.75f, 0.85f) : new Color(0.35f, 0.65f, 0.35f);
                if (plant.Stage == PlantStage.Flower)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.5f, 0.3f, 0.35f));
                }
                view.GetComponent<Renderer>().material = mat;
                _plantViews[plant.CellId] = view;
            }
            if (_coordinator.CurrentWeather != null)
            {
                _sun.intensity = _coordinator.CurrentWeather.Id == WeatherId.Clear ? 1.25f
                    : _coordinator.CurrentWeather.Id == WeatherId.Cloudy ? 0.85f : 0.6f;
            }
            string weather = _coordinator.CurrentWeather != null ? _coordinator.CurrentWeather.LabelJa : "";
            string site = _coordinator.CurrentFaces != null
                ? Sites.ById(_coordinator.CurrentFaces.ResolvedSiteId).LabelJa : "";
            _statusText.text = $"空: {weather}　届け先: {site}　{message}";
            if (_coordinator.CurrentFaces != null)
            {
                var parts = new string[6];
                for (int i = 0; i < 6; i++)
                    parts[i] = $"{i + 1}:{RecipeCatalog.LabelJa(_coordinator.CurrentFaces.Faces[i])}";
                _facesText.text = "今回の6面　" + string.Join("　", parts);
            }
        }
    }
}
