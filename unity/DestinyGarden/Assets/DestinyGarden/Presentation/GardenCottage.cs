using System.Collections.Generic;
using DestinyGarden.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace DestinyGarden.Presentation
{
    internal sealed class GardenCottage : MonoBehaviour
    {
        private const float CabinWidth = 1.48f;
        private const float CabinDepth = 1.23f;
        private const float Eave = 0.97f;
        private const float Ridge = 1.52f;

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private Mesh _box, _stone, _gable;
        private Material _plaster, _stonework, _wood, _trim, _door, _roof, _roofShade, _glow;
        private Material _glass, _soil, _herbs, _cloth, _water;
        private Shader _shader;
        private Light _lamp, _daylight;
        private Transform _porch, _wing, _finish;
        private Transform _chime, _doorstep;
        private CottageStage _stage;
        private ElementId _extension;
        private bool _hasProgress;
        private float _weatherMotion;

        internal CottagePlot Plot { get; private set; }
        internal string Label { get; private set; } = "小さな家";
        internal Vector3 LookAt => transform.position + transform.right * (_stage >= CottageStage.Extension ? 0.50f : 0.12f)
            + transform.up * 0.58f - transform.forward * 0.49f;

        internal void Build(CottagePlot plot, Shader shader, Mesh roundedBox, Mesh stone, int seed)
        {
            Plot = plot;
            _shader = shader;
            transform.SetPositionAndRotation(new Vector3(plot.Center.x, plot.Floor, plot.Center.y),
                Quaternion.LookRotation(plot.Forward, Vector3.up));
            _box = roundedBox;
            _stone = stone;
            _gable = CottageModel.CreateGable();
            _meshes.Add(_gable);

            var woodGrain = ProceduralArt.WoodTexture();
            var stoneGrain = ProceduralArt.StoneTexture();
            var plasterGrain = ProceduralArt.PlasterTexture();
            var waterTexture = ProceduralArt.RiverTexture();
            _textures.Add(woodGrain);
            _textures.Add(stoneGrain);
            _textures.Add(plasterGrain);
            _textures.Add(waterTexture);
            _plaster = Lit("Lime plaster", new Color(1f, 0.97f, 0.90f), 0.19f, plasterGrain);
            _stonework = Lit("Foundation stone", new Color(0.78f, 0.80f, 0.79f), 0.15f, stoneGrain);
            _wood = Lit("Cedar boards", new Color(1.20f, 1.05f, 0.88f), 0.23f, woodGrain);
            _trim = Lit("Warm oak", new Color(0.40f, 0.247f, 0.145f), 0.27f);
            _door = Lit("Painted door", new Color(0.19f, 0.39f, 0.37f), 0.29f);
            var tile = new[]
            {
                new Color(0.51f, 0.26f, 0.20f),
                new Color(0.24f, 0.38f, 0.40f),
                new Color(0.41f, 0.33f, 0.24f)
            }[(seed & int.MaxValue) % 3];
            _roof = Lit("Roof tiles", tile, 0.25f);
            _roofShade = Lit("Weathered roof tiles", tile * 0.82f, 0.20f);
            _glow = Lit("Window glass", new Color(0.96f, 0.77f, 0.47f), 0.42f);
            _glow.EnableKeyword("_EMISSION");

            _soil = Lit("Planter soil", new Color(0.25f, 0.16f, 0.09f), 0.10f);
            _cloth = Lit("Porch linen", new Color(0.79f, 0.65f, 0.40f), 0.16f);
            _water = Lit("Rainwater", new Color(0.46f, 0.71f, 0.74f), 0.92f, waterTexture);
            var foliage = Shader.Find("DestinyGarden/Foliage");
            _herbs = new Material(foliage) { name = "House flowers" };
            _herbs.SetFloat("_WindStrength", 0f);
            _materials.Add(_herbs);
            _glass = Lit("Greenhouse panes", new Color(0.59f, 0.83f, 0.77f, 0.25f), 0.85f);
            _glass.SetOverrideTag("RenderType", "Transparent");
            _glass.SetFloat("_Surface", 1f);
            _glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            _glass.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            _glass.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            _glass.SetFloat("_ZWrite", 0f);
            _glass.SetFloat("_Cull", (float)CullMode.Back);
            _glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _glass.renderQueue = (int)RenderQueue.Transparent;

            var cabin = NewPart("Cottage");
            AddCabin().Build(cabin, _meshes);
            var plotDetails = NewPart("Lantern and path");
            AddPath().Build(plotDetails, _meshes);
            _doorstep = NewPart("Stone doorstep");
            var step = Model();
            Stairs(step, -0.23f, -0.65f, 0.49f, 0f, 0.35f, _stonework);
            step.Build(_doorstep, _meshes);

            _lamp = new GameObject("Porch lantern").AddComponent<Light>();
            _lamp.transform.SetParent(transform, false);
            _lamp.transform.localPosition = new Vector3(0.34f, 0.77f, -0.80f);
            _lamp.type = LightType.Point;
            _lamp.color = new Color(1f, 0.76f, 0.47f);
            _lamp.range = 2.75f;
            _daylight = new GameObject("Bounced daylight").AddComponent<Light>();
            _daylight.transform.SetParent(transform, false);
            _daylight.transform.localPosition = new Vector3(0.62f, 2.12f, -2.65f);
            _daylight.type = LightType.Point;
            _daylight.color = new Color(0.96f, 0.87f, 0.74f);
            _daylight.range = 5.6f;
            SetLight(0f);
        }

        internal void Show(CottageGrowth growth, bool reducedMotion)
        {
            if (_hasProgress && growth.Stage == _stage && growth.Extension == _extension) return;
            _stage = growth.Stage;
            _extension = growth.Extension;
            Label = growth.Label;

            if (_stage >= CottageStage.Porch && _porch == null)
            {
                _porch = NewPart("Front porch");
                AddPorch().Build(_porch, _meshes);
                _doorstep.gameObject.SetActive(false);
                Reveal(_porch, reducedMotion);
            }
            if (_stage >= CottageStage.Extension && _wing == null)
            {
                _wing = NewPart(growth.Extension.ToString() + " extension");
                BuildExtension(growth.Extension).Build(_wing, _meshes);
                Reveal(_wing, reducedMotion);
            }
            if (_stage == CottageStage.Finished && _finish == null)
            {
                _finish = NewPart("Finished details");
                AddFinish(growth.Extension, _finish).Build(_finish, _meshes);
                Reveal(_finish, reducedMotion);
            }
            _hasProgress = true;
        }

        private readonly List<Transform> _revealing = new List<Transform>();
        private float _revealTime;

        private void Reveal(Transform part, bool reducedMotion)
        {
            if (!_hasProgress || reducedMotion) return;
            part.localScale = new Vector3(1f, 0.04f, 1f);
            _revealing.Add(part);
            _revealTime = 0f;
        }

        internal void UpdateAppearance(float night, bool reducedMotion, float deltaTime)
        {
            SetLight(night);
            if (_revealing.Count > 0)
            {
                _revealTime = reducedMotion ? 1f : _revealTime + deltaTime / 1.05f;
                float grow = Mathf.SmoothStep(0.04f, 1f, Mathf.Clamp01(_revealTime));
                foreach (var part in _revealing) part.localScale = new Vector3(1f, grow, 1f);
                if (_revealTime >= 1f) _revealing.Clear();
            }
            if (!reducedMotion) _weatherMotion += deltaTime;
            if (_chime != null)
                _chime.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_weatherMotion * 2.3f) * 9f);
        }

        private void SetLight(float night)
        {
            _lamp.intensity = Mathf.Lerp(0.22f, 1.65f, night);
            _daylight.intensity = Mathf.Lerp(1.45f, 0.03f, night);
            _glow.SetColor("_EmissionColor", new Color(2.2f, 1.30f, 0.48f) * Mathf.Lerp(0.18f, 0.92f, night));
        }

        private Material Lit(string name, Color color, float smoothness, Texture2D map = null)
        {
            var material = new Material(_shader) { name = name, color = color };
            if (map != null) material.SetTexture("_BaseMap", map);
            material.SetFloat("_Smoothness", smoothness);
            _materials.Add(material);
            return material;
        }

        private Transform NewPart(string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(transform, false);
            return child;
        }

        private CottageModel Model() => new CottageModel(_box, _stone, _gable);

        private CottageModel AddCabin()
        {
            var model = Model();
            const float front = -CabinDepth * 0.5f;
            Foundation(model, 0f, 0f, CabinWidth + 0.07f, CabinDepth + 0.06f, 0f);
            model.Box(_plaster, new Vector3(0f, (Eave + 0.04f) * 0.5f, 0f), new Vector3(CabinWidth, Eave + 0.04f, CabinDepth));
            model.Gable(_plaster, new Vector3(0f, Eave - 0.009f, 0f),
                new Vector3(CabinWidth - 0.027f, Ridge - Eave + 0.013f, CabinDepth - 0.012f));
            Roof(model, 0f, 0f, CabinWidth + 0.29f, CabinDepth + 0.26f, Eave, Ridge);
            for (int side = -1; side <= 1; side += 2)
                for (int end = -1; end <= 1; end += 2)
                    model.Box(_trim, new Vector3(side * 0.706f, 0.43f, end * 0.593f), new Vector3(0.085f, 0.92f, 0.085f));

            var door = new Vector3(-0.23f, 0.32f, front - 0.028f);
            model.Box(_trim, door, new Vector3(0.44f, 0.70f, 0.085f));
            model.Box(_door, door - Vector3.forward * 0.047f, new Vector3(0.32f, 0.60f, 0.044f));
            model.Box(_wood, door + new Vector3(0.10f, -0.015f, -0.081f), new Vector3(0.038f, 0.037f, 0.025f));
            Window(model, new Vector3(0.32f, 0.52f, front), 0.41f, 0.32f);
            Window(model, new Vector3(-CabinWidth * 0.5f, 0.53f, -0.09f), 0.42f, 0.30f, 90f);
            Window(model, new Vector3(0.19f, 0.52f, -front), 0.38f, 0.30f, 180f);
            model.Box(_trim, new Vector3(0f, 1.14f, front - 0.018f), new Vector3(0.20f, 0.15f, 0.065f));
            model.Box(_glow, new Vector3(0f, 1.14f, front - 0.057f), new Vector3(0.13f, 0.085f, 0.019f));

            model.Box(_stonework, new Vector3(-0.47f, 1.37f, 0.29f), new Vector3(0.23f, 0.61f, 0.24f));
            model.Box(_roofShade, new Vector3(-0.47f, 1.69f, 0.29f), new Vector3(0.30f, 0.09f, 0.31f));
            return model;
        }

        private CottageModel AddPath()
        {
            var model = Model();
            for (int i = 0; i < 5; i++)
            {
                float x = -0.23f + Mathf.Sin(i * 1.8f) * 0.077f;
                float z = -1.12f - i * 0.29f;
                float y = Plot.Ground(x, z) - Plot.Floor;
                model.Stone(_stonework, new Vector3(x, y + 0.012f, z), new Vector3(0.43f, 0.095f, 0.30f), 29f * i);
            }
            model.Box(_trim, new Vector3(0.35f, 0.78f, -0.69f), new Vector3(0.12f, 0.19f, 0.12f));
            model.Box(_glow, new Vector3(0.35f, 0.77f, -0.716f), new Vector3(0.082f, 0.105f, 0.087f));
            return model;
        }

        private CottageModel AddPorch()
        {
            var model = Model();
            Deck(model, 0f, -0.98f, 1.71f, 0.68f, -0.06f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.78f;
                model.Box(_trim, new Vector3(x, 0.36f, -1.26f), new Vector3(0.078f, 0.83f, 0.078f));
                model.Post(_wood, new Vector3(x, 0.67f, -1.27f), new Vector3(x * 0.77f, 0.83f, -1.27f), 0.052f);
            }
            model.Box(_roofShade, new Vector3(0f, 0.88f, -1.01f), new Vector3(1.87f, 0.069f, 0.78f), -13f);
            model.Box(_trim, new Vector3(0f, 0.80f, -1.40f), new Vector3(1.87f, 0.078f, 0.076f));
            Bench(model, 0.40f, -0.84f, -0.015f);
            Stairs(model, -0.23f, -1.30f, 0.55f, -0.024f, 0.62f, _wood);
            return model;
        }

        private CottageModel BuildExtension(ElementId element)
        {
            var model = Model();
            switch (element)
            {
                case ElementId.Water:
                    WaterTerrace(model);
                    break;
                case ElementId.Wind:
                    WindLoft(model);
                    break;
                case ElementId.Seed:
                    FlowerWorkshop(model);
                    break;
                default:
                    Greenhouse(model);
                    break;
            }
            return model;
        }

        private void WaterTerrace(CottageModel model)
        {
            const float x = 1.35f, z = -0.23f;
            Deck(model, x, z, 1.53f, 1.94f, -0.04f);
            for (int i = 0; i < 4; i++)
                model.Box(_trim, new Vector3(2.09f, 0.22f, -1.08f + i * 0.57f), new Vector3(0.071f, 0.51f, 0.071f));
            model.Box(_wood, new Vector3(2.09f, 0.45f, z), new Vector3(0.08f, 0.06f, 1.80f));
            model.Box(_wood, new Vector3(2.09f, 0.17f, z), new Vector3(0.052f, 0.048f, 1.80f));
            for (int i = 0; i < 3; i++)
                model.Box(_trim, new Vector3(0.77f + i * 0.66f, 0.43f, 0.65f), new Vector3(0.068f, 0.95f, 0.070f));
            model.Box(_roofShade, new Vector3(x, 0.92f, 0.24f), new Vector3(1.58f, 0.076f, 0.90f), -10f);
            model.Box(_trim, new Vector3(x, 0.86f, -0.18f), new Vector3(1.58f, 0.082f, 0.081f));
            Bench(model, x + 0.09f, 0.47f, -0.015f);
            const float basinX = 1.38f, basinZ = -0.81f;
            model.Box(_stonework, new Vector3(basinX, 0.053f, basinZ), new Vector3(0.52f, 0.21f, 0.45f));
            model.Box(_water, new Vector3(basinX, 0.158f, basinZ), new Vector3(0.43f, 0.013f, 0.36f));
            for (int side = -1; side <= 1; side += 2)
            {
                model.Box(_stonework, new Vector3(basinX + side * 0.235f, 0.171f, basinZ), new Vector3(0.057f, 0.061f, 0.45f));
                model.Box(_stonework, new Vector3(basinX, 0.171f, basinZ + side * 0.196f), new Vector3(0.48f, 0.061f, 0.057f));
            }
        }

        private void WindLoft(CottageModel model)
        {
            const float x = 1.25f, z = 0.10f;
            Foundation(model, x, z, 0.98f, 0.96f, 0f);
            model.Box(_plaster, new Vector3(x, 0.58f, z), new Vector3(0.96f, 1.15f, 0.89f));
            model.Box(_wood, new Vector3(x, 1.36f, z), new Vector3(1.04f, 0.43f, 0.94f));
            model.Gable(_wood, new Vector3(x, 1.57f, z), new Vector3(1.04f, 0.43f, 0.94f));
            Roof(model, x, z, 1.28f, 1.18f, 1.57f, 2f);
            Window(model, new Vector3(x, 0.56f, -0.355f), 0.40f, 0.35f);
            Window(model, new Vector3(x, 1.37f, -0.39f), 0.45f, 0.25f);
            model.Box(_wood, new Vector3(x, 1.16f, -0.52f), new Vector3(0.91f, 0.08f, 0.29f));
            for (int side = -1; side <= 1; side += 2)
                model.Box(_trim, new Vector3(x + side * 0.40f, 1.29f, -0.64f), new Vector3(0.055f, 0.28f, 0.056f));
            model.Box(_trim, new Vector3(x, 1.43f, -0.64f), new Vector3(0.88f, 0.056f, 0.062f));
        }

        private void Greenhouse(CottageModel model)
        {
            const float x = 1.46f, z = 0f, width = 1.39f, depth = 1.36f, eave = 1.00f, ridge = 1.37f;
            Foundation(model, x, z, width, depth, 0f);
            model.Box(_stonework, new Vector3(x, 0.09f, z), new Vector3(width, 0.18f, depth));
            for (int side = -1; side <= 1; side += 2)
            {
                float xx = x + side * width * 0.5f;
                for (int end = -1; end <= 1; end += 2)
                {
                    float zz = z + end * depth * 0.5f;
                    model.Box(_door, new Vector3(xx, 0.56f, zz), new Vector3(0.073f, 0.94f, 0.073f));
                    model.Post(_door, new Vector3(xx, eave, zz), new Vector3(x, ridge, zz), 0.055f);
                }
                model.Box(_door, new Vector3(xx, 0.57f, z), new Vector3(0.055f, 0.90f, 0.055f));
                model.Box(_door, new Vector3(xx, eave, z), new Vector3(0.068f, 0.069f, depth + 0.06f));
                model.Box(_door, new Vector3(xx, 0.22f, z), new Vector3(0.068f, 0.064f, depth));
                model.Box(_glass, new Vector3(xx, 0.56f, z), new Vector3(0.014f, 0.81f, depth - 0.04f));
                float span = Mathf.Sqrt(width * width * 0.25f + (ridge - eave) * (ridge - eave));
                RoofPane(model, new Vector3(x + side * width * 0.25f, (ridge + eave) * 0.5f, z),
                    span, depth, -side * Mathf.Atan2(ridge - eave, width * 0.5f) * Mathf.Rad2Deg);
            }
            model.Box(_door, new Vector3(x, ridge, z), new Vector3(0.066f, 0.07f, depth + 0.10f));
            model.Box(_glass, new Vector3(x, 0.55f, -depth * 0.5f), new Vector3(width - 0.055f, 0.82f, 0.016f));
            model.Box(_glass, new Vector3(x, 0.55f, depth * 0.5f), new Vector3(width - 0.055f, 0.82f, 0.016f));
            model.Box(_door, new Vector3(x, 0.55f, -depth * 0.5f - 0.019f), new Vector3(0.060f, 0.88f, 0.056f));
            model.Box(_door, new Vector3(x, 0.98f, -depth * 0.5f), new Vector3(width, 0.069f, 0.058f));
            Planter(model, new Vector3(1.58f, 0.24f, 0.10f), 0.82f, true);
            Planter(model, new Vector3(1.51f, 0.28f, -0.35f), 0.72f, false);
        }

        private void RoofPane(CottageModel model, Vector3 center, float span, float depth, float roll)
        {
            // The frame divides the long panes and keeps the plants visible underneath.
            for (int i = -1; i <= 1; i++)
            {
                var p = center + Vector3.forward * (i * depth * 0.49f);
                model.Post(_door, p + Quaternion.Euler(0f, 0f, roll) * Vector3.left * span * 0.50f,
                    p + Quaternion.Euler(0f, 0f, roll) * Vector3.right * span * 0.50f, 0.055f);
            }
            // A roof panel's thickness is perpendicular to its slope.
            var rotation = Quaternion.Euler(0f, 0f, roll);
            model.Board(_glass, center, new Vector3(span, 0.014f, depth), rotation);
        }

        private void FlowerWorkshop(CottageModel model)
        {
            const float x = 1.42f, z = -0.05f, width = 1.35f, depth = 1.11f;
            Foundation(model, x, z, width, depth, 0f);
            model.Box(_wood, new Vector3(x, 0.40f, z + 0.30f), new Vector3(width, 0.80f, 0.48f));
            model.Box(_plaster, new Vector3(x + 0.64f, 0.40f, z), new Vector3(0.12f, 0.80f, depth));
            model.Box(_roofShade, new Vector3(x, 0.98f, z), new Vector3(width + 0.23f, 0.092f, depth + 0.36f), -14f);
            model.Box(_trim, new Vector3(x + 0.60f, 0.39f, -0.65f), new Vector3(0.08f, 0.85f, 0.08f));
            model.Box(_trim, new Vector3(x - 0.58f, 0.39f, -0.65f), new Vector3(0.08f, 0.85f, 0.08f));
            Deck(model, x, -0.53f, width, 0.99f, -0.04f);
            model.Box(_trim, new Vector3(x, 0.32f, 0.03f), new Vector3(1.05f, 0.09f, 0.40f));
            model.Box(_wood, new Vector3(x, 0.53f, 0.23f), new Vector3(0.98f, 0.07f, 0.26f));
            for (int side = -1; side <= 1; side += 2)
                model.Box(_trim, new Vector3(x + side * 0.41f, 0.16f, 0.02f), new Vector3(0.063f, 0.33f, 0.25f));
            Planter(model, new Vector3(x, 0.40f, 0.02f), 0.94f, true);
            Planter(model, new Vector3(x + 0.51f, 0.02f, -0.82f), 0.40f, true);
            model.Box(_cloth, new Vector3(x, 0.74f, -0.69f), new Vector3(1.19f, 0.13f, 0.045f));
            for (int i = 0; i < 3; i++)
                model.Box(_door, new Vector3(x - 0.40f + i * 0.40f, 0.74f, -0.716f), new Vector3(0.10f, 0.13f, 0.021f));
        }

        private CottageModel AddFinish(ElementId element, Transform part)
        {
            var model = Model();
            Planter(model, new Vector3(0.68f, 0.0f, -1.63f), 0.42f, true);
            switch (element)
            {
                case ElementId.Water:
                    Planter(model, new Vector3(1.99f, 0.03f, 0.39f), 0.37f, false);
                    model.Box(_glow, new Vector3(0.89f, 0.74f, -0.06f), new Vector3(0.084f, 0.13f, 0.082f));
                    break;
                case ElementId.Wind:
                    _chime = new GameObject("Wind chimes").transform;
                    _chime.SetParent(part, false);
                    _chime.localPosition = new Vector3(1.43f, 1.46f, -0.65f);
                    var chime = Model();
                    for (int i = 0; i < 3; i++)
                    {
                        float x = (i - 1) * 0.086f;
                        chime.Box(_trim, new Vector3(x, -0.081f, 0f), new Vector3(0.015f, 0.16f, 0.014f));
                        chime.Box(_cloth, new Vector3(x, -0.165f - i * 0.02f, 0f), new Vector3(0.031f, 0.13f + i * 0.04f, 0.031f));
                    }
                    chime.Build(_chime, _meshes);
                    break;
                case ElementId.Seed:
                    Planter(model, new Vector3(1.34f, 0.57f, 0.25f), 0.69f, true);
                    Planter(model, new Vector3(1.46f, Plot.Ground(1.46f, -1.50f) - Plot.Floor + 0.022f, -1.50f), 0.51f, true);
                    break;
                default:
                    Planter(model, new Vector3(1.58f, 0.04f, -0.95f), 0.73f, true);
                    model.Box(_glow, new Vector3(1.46f, 0.86f, -0.49f), new Vector3(0.073f, 0.13f, 0.073f));
                    break;
            }
            return model;
        }

        private void Foundation(CottageModel model, float x, float z, float width, float depth, float floor)
        {
            float bottom = floor - 0.19f;
            for (int side = -1; side <= 1; side += 2)
                for (int end = -1; end <= 1; end += 2)
                    bottom = Mathf.Min(bottom, Plot.Ground(x + side * width * 0.5f, z + end * depth * 0.5f) - Plot.Floor - 0.08f);
            model.Box(_stonework, new Vector3(x, (bottom + floor) * 0.5f, z), new Vector3(width, floor - bottom, depth));
        }

        private void Deck(CottageModel model, float x, float z, float width, float depth, float floor)
        {
            const int boards = 8;
            float plank = width / boards;
            for (int i = 0; i < boards; i++)
                model.Box(_wood, new Vector3(x - width * 0.5f + (i + 0.5f) * plank, floor, z),
                    new Vector3(plank - 0.011f, 0.072f, depth));
            for (int side = -1; side <= 1; side += 2)
                for (int end = -1; end <= 1; end += 2)
                {
                    float xx = x + side * width * 0.40f, zz = z + end * depth * 0.39f;
                    float foot = Mathf.Min(floor - 0.10f, Plot.Ground(xx, zz) - Plot.Floor - 0.065f);
                    model.Box(_trim, new Vector3(xx, (foot + floor) * 0.5f, zz), new Vector3(0.10f, floor - foot, 0.10f));
                }
        }

        private void Stairs(CottageModel model, float x, float entrance, float width, float top, float run, Material material)
        {
            float ground = Plot.Ground(x, entrance - run) - Plot.Floor;
            int steps = Mathf.Clamp(Mathf.CeilToInt((top - ground) / 0.17f), 1, 5);
            float tread = run / steps;
            for (int i = 0; i < steps; i++)
            {
                float z = entrance - (i + 0.5f) * tread;
                float bottom = Mathf.Min(ground, Plot.Ground(x, z) - Plot.Floor) - 0.065f;
                float height = Mathf.Max(0.08f, Mathf.Lerp(top, ground, i / (float)steps) - bottom);
                model.Box(material, new Vector3(x, bottom + height * 0.5f, z),
                    new Vector3(width, height, tread + 0.021f));
            }
        }

        private void Roof(CottageModel model, float x, float z, float width, float depth, float eave, float ridge)
        {
            const int courses = 5, tiles = 7;
            float halfWidth = width * 0.5f;
            float rise = ridge - eave;
            float slant = Mathf.Sqrt(halfWidth * halfWidth + rise * rise);
            float roll = Mathf.Atan2(rise, halfWidth) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                var rotation = Quaternion.Euler(0f, 0f, -side * roll);
                var middle = new Vector3(x + side * halfWidth * 0.50f, (eave + ridge) * 0.5f, z);
                model.Board(_roofShade, middle, new Vector3(slant + 0.025f, 0.073f, depth), rotation);
                for (int course = 0; course < courses; course++)
                {
                    float t = (course + 0.5f) / courses;
                    for (int i = 0; i < tiles; i++)
                    {
                        float stagger = course % 2 == 0 || i == 0 || i == tiles - 1 ? 0f : 0.22f;
                        float tileZ = z + (i + 0.5f - tiles * 0.5f + stagger) * depth / tiles;
                        float tileY = Mathf.Lerp(ridge, eave, t) + 0.045f;
                        var material = (i * 5 + course * 3 + side) % 6 == 0 ? _roofShade : _roof;
                        model.Board(material, new Vector3(x + side * halfWidth * t, tileY, tileZ),
                            new Vector3(slant / courses * 1.17f, 0.057f, depth / tiles * 0.945f), rotation);
                    }
                }
            }
            model.Box(_roof, new Vector3(x, ridge + 0.043f, z), new Vector3(0.19f, 0.125f, depth + 0.035f));
        }

        private void Window(CottageModel model, Vector3 center, float width, float height, float yaw = 0f)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            Place(_trim, Vector3.zero, new Vector3(width, height, 0.10f));
            Place(_glow, new Vector3(0f, 0f, -0.051f), new Vector3(width - 0.10f, height - 0.092f, 0.028f));
            Place(_wood, new Vector3(0f, 0f, -0.069f), new Vector3(0.031f, height - 0.033f, 0.026f));
            Place(_wood, new Vector3(0f, 0.006f, -0.069f), new Vector3(width - 0.035f, 0.030f, 0.026f));
            Place(_wood, new Vector3(0f, -height * 0.50f, -0.029f), new Vector3(width + 0.11f, 0.06f, 0.19f));

            void Place(Material material, Vector3 offset, Vector3 scale) =>
                model.Box(material, center + rotation * offset, scale, yaw: yaw);
        }

        private void Bench(CottageModel model, float x, float z, float floor)
        {
            model.Box(_wood, new Vector3(x, floor + 0.20f, z), new Vector3(0.51f, 0.062f, 0.25f));
            model.Box(_trim, new Vector3(x, floor + 0.35f, z + 0.11f), new Vector3(0.51f, 0.10f, 0.046f));
            for (int side = -1; side <= 1; side += 2)
                model.Box(_trim, new Vector3(x + side * 0.19f, floor + 0.12f, z), new Vector3(0.056f, 0.25f, 0.18f));
        }

        private void Planter(CottageModel model, Vector3 at, float width, bool flower)
        {
            at.y = Mathf.Max(at.y, Plot.Ground(at.x, at.z) - Plot.Floor + 0.012f);
            model.Box(_roofShade, at + Vector3.up * 0.10f, new Vector3(width, 0.20f, 0.25f));
            model.Box(_soil, at + Vector3.up * 0.197f, new Vector3(width * 0.9f, 0.031f, 0.21f));
            var plants = new FoliageMesh();
            plants.Root = new Vector4(0f, 0f, 0f, -1f);
            for (int i = 0; i < 3; i++)
            {
                var species = i == 1 ? PlantForms.Species.ForgetMeNot : PlantForms.Species.Daisy;
                var ornamental = new PlantState
                {
                    PlantId = "cottage-" + i,
                    Stage = flower ? PlantStage.Flower : PlantStage.Leaf,
                    Growth = 2200
                };
                Vector3 root = new Vector3((i - 1) * width * 0.30f / 0.48f, 0f, i % 2 * 0.055f);
                PlantForms.Build(plants, ornamental, root, species);
            }
            var mesh = plants.ToMesh("Potted plants");
            _meshes.Add(mesh);
            model.Plants(_herbs, mesh, at + Vector3.up * 0.212f, 0.48f);
        }

        private void OnDestroy()
        {
            foreach (var mesh in _meshes) Destroy(mesh);
            foreach (var material in _materials) Destroy(material);
            foreach (var texture in _textures) Destroy(texture);
        }
    }
}
