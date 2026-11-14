using System.Collections.Generic;
using DestinyGarden.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace DestinyGarden.Presentation
{
    internal sealed partial class GardenCottage : MonoBehaviour
    {
        private const float CabinWidth = 1.48f;
        private const float CabinDepth = 1.23f;
        private const float Eave = 0.97f;
        private const float Ridge = 1.52f;

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private Mesh _box, _stone, _gable, _pot, _basin, _disc;
        private Material _plaster, _stonework, _wood, _trim, _door, _roof, _roofShade, _glow;
        private Material _glass, _soil, _herbs, _cloth, _water, _clay, _slate, _copper, _brass, _fabric, _basinStone;
        private Material _shadeLinen, _pathStone;
        private Shader _shader;
        private Light _lamp, _daylight, _greenhouseLamp;
        private Transform _cabinRoof, _porch, _wing, _finish, _styledRoof;
        private Transform _chime, _doorstep, _windTail;
        private CottageStage _stage;
        private ElementId _extension;
        private bool _hasProgress;
        private float _weatherMotion;
        private float _roofWetness = -1f;

        internal CottagePlot Plot { get; private set; }
        internal string Label { get; private set; } = "小さな家";
        internal Vector3 LookAt => transform.position + transform.right * (_stage >= CottageStage.Extension ? 0.50f : 0.12f)
            + transform.up * (_stage == CottageStage.Styled ? 0.80f : 0.58f) - transform.forward * 0.49f;

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
            _pot = CottagePottery.Pot();
            _basin = CottagePottery.Basin();
            _disc = ProceduralArt.DiscMesh(0.5f, 24);
            _meshes.Add(_pot);
            _meshes.Add(_basin);
            _meshes.Add(_disc);

            var woodGrain = ProceduralArt.WoodTexture();
            var stoneGrain = ProceduralArt.StoneTexture();
            var plasterGrain = ProceduralArt.PlasterTexture();
            var waterTexture = ProceduralArt.RiverTexture();
            var tileTexture = ProceduralArt.CottageShingleTexture();
            _textures.Add(woodGrain);
            _textures.Add(stoneGrain);
            _textures.Add(plasterGrain);
            _textures.Add(waterTexture);
            _textures.Add(tileTexture);
            _plaster = Lit("Lime plaster", new Color(1f, 0.94f, 0.85f), 0.19f, plasterGrain);
            _stonework = Lit("Foundation stone", new Color(0.80f, 0.78f, 0.82f), 0.15f, stoneGrain);
            _pathStone = Lit("Worn grey stepping stone", new Color(0.61f, 0.62f, 0.56f), 0.19f, plasterGrain);
            _wood = Lit("Cedar boards", new Color(1.20f, 1.05f, 0.88f), 0.23f, woodGrain);
            _trim = Lit("Warm oak", new Color(0.40f, 0.247f, 0.145f), 0.27f);
            _door = Lit("Painted door", new Color(0.21f, 0.43f, 0.37f), 0.29f);
            var tile = new[]
            {
                new Color(0.65f, 0.31f, 0.32f),
                new Color(0.24f, 0.48f, 0.50f),
                new Color(0.51f, 0.34f, 0.47f)
            }[(seed & int.MaxValue) % 3];
            _roof = Lit("Roof tiles", tile, 0.25f, tileTexture);
            _roofShade = Lit("Weathered roof tiles", tile * 0.77f, 0.20f, tileTexture);
            _slate = Lit("Terrace slate", new Color(0.26f, 0.42f, 0.46f), 0.28f, tileTexture);
            _copper = Lit("Patinated copper", new Color(0.40f, 0.53f, 0.44f), 0.42f);
            _brass = Lit("Aged brass", new Color(0.58f, 0.36f, 0.17f), 0.42f);
            _brass.SetFloat("_Metallic", 0.28f);
            _clay = Lit("Terracotta", new Color(0.67f, 0.33f, 0.22f), 0.31f, tileTexture);
            _basinStone = Lit("Basin limestone", new Color(0.78f, 0.75f, 0.66f), 0.37f, plasterGrain);
            _glow = Lit("Window glass", new Color(0.96f, 0.77f, 0.47f), 0.42f);
            _glow.EnableKeyword("_EMISSION");

            _soil = Lit("Planter soil", new Color(0.25f, 0.16f, 0.09f), 0.10f);
            _cloth = Lit("Warm linen", new Color(0.79f, 0.64f, 0.39f), 0.17f);
            _shadeLinen = Lit("Rust linen", new Color(0.53f, 0.24f, 0.20f), 0.21f, tileTexture);
            _fabric = Lit("Woven cushion", new Color(0.47f, 0.23f, 0.18f), 0.18f, tileTexture);
            _water = Lit("Rainwater", new Color(0.64f, 0.85f, 0.81f), 0.86f, waterTexture);
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
            _cabinRoof = NewPart("Cottage roof");
            AddCabinRoof().Build(_cabinRoof, _meshes);
            var plotDetails = NewPart("Lantern and path");
            AddPath().Build(plotDetails, _meshes);
            _doorstep = NewPart("Stone doorstep");
            var step = Model();
            Stairs(step, CottagePlot.DoorX, -0.65f, 0.49f, 0f, 0.35f, _stonework);
            step.Build(_doorstep, _meshes);

            _lamp = new GameObject("Porch lantern").AddComponent<Light>();
            _lamp.transform.SetParent(transform, false);
            _lamp.transform.localPosition = new Vector3(0.34f, 0.77f, -0.80f);
            _lamp.type = LightType.Point;
            _lamp.color = new Color(1f, 0.71f, 0.39f);
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
            if (_stage >= CottageStage.Finished && _finish == null)
            {
                _finish = NewPart("Finished details");
                AddFinish(growth.Extension, _finish).Build(_finish, _meshes);
                Reveal(_finish, reducedMotion);
            }
            if (_stage == CottageStage.Styled && _styledRoof == null)
            {
                // Grow from the eaves, where the new roof meets the original walls.
                _styledRoof = NewPart(growth.Style.ToString());
                _styledRoof.localPosition = Vector3.up * Eave;
                var shell = new GameObject("Roof and fittings").transform;
                shell.SetParent(_styledRoof, false);
                shell.localPosition = Vector3.down * Eave;
                BuildStyle(growth.Style, shell);
                _cabinRoof.gameObject.SetActive(false);
                Reveal(_styledRoof, reducedMotion);
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

        internal void UpdateAppearance(SkyState sky, bool reducedMotion, float deltaTime)
        {
            SetLight(sky.Night);
            if (_revealing.Count > 0)
            {
                _revealTime = reducedMotion ? 1f : _revealTime + deltaTime / 1.05f;
                float grow = Mathf.SmoothStep(0.04f, 1f, Mathf.Clamp01(_revealTime));
                foreach (var part in _revealing) part.localScale = new Vector3(1f, grow, 1f);
                if (_revealTime >= 1f) _revealing.Clear();
            }
            if (!reducedMotion) _weatherMotion += deltaTime;
            if (_chime != null)
                _chime.localRotation = Quaternion.Euler(Mathf.Sin(_weatherMotion * 1.7f) * 4f, 0f,
                    Mathf.Sin(_weatherMotion * 2.3f) * 11f);
            if (_windTail != null)
                _windTail.localRotation = Quaternion.Euler(0f, Mathf.Sin(_weatherMotion * 0.78f) * 13f, 0f);
            if (_millSails != null)
                _millSails.localRotation = Quaternion.Euler(0f, 0f, 22f - _weatherMotion * 18f);
            if ((_extension == ElementId.Water && _wing != null) || _rainCopper != null)
            {
                var drift = new Vector2(_weatherMotion * 0.018f, _weatherMotion * -0.023f);
                _water.SetTextureOffset("_BaseMap", drift);
            }
            if (!Mathf.Approximately(_roofWetness, sky.Moisture))
            {
                _roofWetness = sky.Moisture;
                _roof.SetFloat("_Smoothness", Mathf.Lerp(0.25f, 0.47f, _roofWetness));
                _roofShade.SetFloat("_Smoothness", Mathf.Lerp(0.20f, 0.41f, _roofWetness));
                _slate.SetFloat("_Smoothness", Mathf.Lerp(0.28f, 0.46f, _roofWetness));
                if (_rainCopper != null)
                    _rainCopper.SetFloat("_Smoothness", Mathf.Lerp(0.32f, 0.63f, _roofWetness));
            }
        }

        private void SetLight(float night)
        {
            _lamp.intensity = Mathf.Lerp(0.26f, 1.76f, night);
            _daylight.intensity = Mathf.Lerp(1.45f, 0.03f, night);
            _glow.SetColor("_EmissionColor", new Color(2.2f, 1.24f, 0.43f) * Mathf.Lerp(0.22f, 1f, night));
            if (_greenhouseLamp != null) _greenhouseLamp.intensity = Mathf.Lerp(0.03f, 0.68f, night);
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
            for (int side = -1; side <= 1; side += 2)
                for (int end = -1; end <= 1; end += 2)
                    model.Box(_trim, new Vector3(side * 0.706f, 0.43f, end * 0.593f), new Vector3(0.085f, 0.92f, 0.085f));

            var door = new Vector3(CottagePlot.DoorX, 0.32f, front - 0.028f);
            model.Box(_trim, door, new Vector3(0.44f, 0.70f, 0.085f));
            for (int i = 0; i < 4; i++)
                model.Box(_door, door + new Vector3((i - 1.5f) * 0.081f, 0f, -0.047f),
                    new Vector3(0.079f, 0.60f, 0.042f));
            for (int sign = -1; sign <= 1; sign += 2)
                model.Box(_wood, door + new Vector3(0f, sign * 0.187f, -0.069f), new Vector3(0.325f, 0.040f, 0.026f));
            model.Box(_brass, door + new Vector3(0.10f, -0.015f, -0.081f), new Vector3(0.045f, 0.035f, 0.032f));
            var frontWindow = new Vector3(0.32f, 0.52f, front);
            Window(model, frontWindow, 0.41f, 0.32f);
            Shutters(model, frontWindow, 0.41f, 0.32f);
            Window(model, new Vector3(-CabinWidth * 0.5f, 0.53f, -0.09f), 0.42f, 0.30f, 90f);
            Window(model, new Vector3(0.19f, 0.52f, -front), 0.38f, 0.30f, 180f);
            return model;
        }

        private CottageModel AddCabinRoof()
        {
            var model = Model();
            const float front = -CabinDepth * 0.5f;
            model.Gable(_plaster, new Vector3(0f, Eave - 0.009f, 0f),
                new Vector3(CabinWidth - 0.027f, Ridge - Eave + 0.013f, CabinDepth - 0.012f));
            Roof(model, 0f, 0f, CabinWidth + 0.29f, CabinDepth + 0.26f, Eave, Ridge);
            RoundWindow(model, new Vector3(0f, 1.15f, front), 0.21f);
            model.Box(_wood, new Vector3(0f, Eave + 0.012f, front - 0.016f), new Vector3(CabinWidth - 0.07f, 0.055f, 0.052f));
            for (int side = -1; side <= 1; side += 2)
                model.Post(_trim, new Vector3(side * 0.65f, Eave + 0.04f, front - 0.016f),
                    new Vector3(0f, Ridge - 0.02f, front - 0.016f), 0.039f);

            model.Box(_stonework, new Vector3(-0.47f, 1.37f, 0.29f), new Vector3(0.23f, 0.61f, 0.24f));
            for (int i = 0; i < 3; i++)
            {
                float y = 1.37f + i * 0.096f;
                model.Box(_stonework, new Vector3(-0.47f, y, 0.29f), new Vector3(0.244f, 0.023f, 0.255f));
            }
            model.Box(_roofShade, new Vector3(-0.47f, 1.69f, 0.29f), new Vector3(0.30f, 0.09f, 0.31f));
            return model;
        }

        private CottageModel AddPath()
        {
            var model = Model();
            for (int i = 0; i < 7; i++)
            {
                float z = -1.12f - i * 0.246f;
                float x = Plot.PathX(z) + Mathf.Sin(i * 1.8f) * 0.047f;
                if (!Plot.AllowsDressing(x, z)) continue;
                float y = Plot.Ground(x, z) - Plot.Floor;
                float width = 0.36f + 0.045f * Mathf.Cos(i * 1.43f);
                model.Stone(i % 3 == 2 ? _pathStone : _stonework, new Vector3(x, y + 0.022f, z),
                    new Vector3(width, 0.115f, 0.28f), i * 41f);
            }
            Lantern(model, new Vector3(0.33f, 0.74f, -0.746f), 1f);
            AddDoorstepGarden(model);
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
            LeanToRoof(model, new Vector3(0f, 0.88f, -1.01f), 1.87f, 0.78f, -13f, _roof, _roofShade);
            Bench(model, 0.40f, -0.84f, -0.015f);
            model.Box(_fabric, new Vector3(0.40f, 0.22f, -0.86f), new Vector3(0.42f, 0.055f, 0.18f));
            model.Box(_cloth, new Vector3(0.53f, 0.25f, -0.86f), new Vector3(0.036f, 0.013f, 0.175f));
            Stairs(model, CottagePlot.DoorX, -1.30f, 0.55f, -0.024f, 0.62f, _wood);
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
                model.Post(_trim, new Vector3(2.09f, 0.17f, -1.04f + i * 0.57f),
                    new Vector3(2.09f, 0.43f, -0.56f + i * 0.57f), 0.034f);
            for (int i = 0; i < 3; i++)
                model.Box(_trim, new Vector3(0.77f + i * 0.66f, 0.43f, 0.65f), new Vector3(0.068f, 0.95f, 0.070f));
            LeanToRoof(model, new Vector3(x, 0.92f, 0.24f), 1.58f, 0.90f, -10f, _slate, _roofShade);
            Bench(model, x + 0.09f, 0.47f, -0.015f);
            var basin = new Vector3(1.61f, -0.020f, -0.73f);
            model.PlaceMesh(_basinStone, _basin, basin, new Vector3(0.62f, 0.34f, 0.56f));
            model.PlaceMesh(_water, _disc, basin + Vector3.up * 0.278f, new Vector3(0.452f, 1f, 0.406f));
            model.Post(_copper, basin + new Vector3(0.22f, 0.17f, 0.09f),
                basin + new Vector3(0.22f, 0.48f, 0.09f), 0.050f);
            model.Post(_copper, basin + new Vector3(0.22f, 0.48f, 0.09f),
                basin + new Vector3(0.058f, 0.47f, 0.09f), 0.047f);
            model.Box(_brass, basin + new Vector3(0.22f, 0.519f, 0.09f), new Vector3(0.099f, 0.019f, 0.032f));
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
            for (int i = 1; i <= 4; i++)
            {
                float railX = x - 0.40f + i * 0.16f;
                model.Box(_wood, new Vector3(railX, 1.30f, -0.64f), new Vector3(0.031f, 0.23f, 0.038f));
            }
            model.Box(_brass, new Vector3(x + 0.20f, 2.08f, 0.17f), new Vector3(0.045f, 0.35f, 0.045f));
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
            Worktable(model, new Vector3(1.45f, 0.19f, 0.23f), 1.02f, 0.35f);
            PottedPlant(model, new Vector3(1.10f, 0.59f, 0.23f), 0.19f, PlantForms.Species.Bellflower, true);
            PottedPlant(model, new Vector3(1.45f, 0.59f, 0.26f), 0.17f, PlantForms.Species.Strawberry, true);
            PottedPlant(model, new Vector3(1.78f, 0.59f, 0.24f), 0.18f, PlantForms.Species.Foxglove, false);
            model.Box(_wood, new Vector3(1.46f, 0.31f, -0.39f), new Vector3(0.88f, 0.08f, 0.34f));
            PottedPlant(model, new Vector3(1.19f, 0.35f, -0.38f), 0.17f, PlantForms.Species.Daisy, false);
            PottedPlant(model, new Vector3(1.56f, 0.35f, -0.39f), 0.21f, PlantForms.Species.ForgetMeNot, true);
            Lantern(model, new Vector3(1.48f, 0.89f, 0.28f), 0.63f);
            _greenhouseLamp = new GameObject("Warm greenhouse light").AddComponent<Light>();
            _greenhouseLamp.transform.SetParent(_wing, false);
            _greenhouseLamp.transform.localPosition = new Vector3(1.45f, 0.91f, 0.20f);
            _greenhouseLamp.type = LightType.Point;
            _greenhouseLamp.color = new Color(1f, 0.74f, 0.42f);
            _greenhouseLamp.range = 1.72f;
            _greenhouseLamp.intensity = 0f;
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
            LeanToRoof(model, new Vector3(x, 1.01f, z + 0.21f), width + 0.23f, 0.99f, -11f, _roof, _roofShade);
            model.Box(_trim, new Vector3(x + 0.60f, 0.39f, -0.65f), new Vector3(0.08f, 0.85f, 0.08f));
            model.Box(_trim, new Vector3(x - 0.58f, 0.39f, -0.65f), new Vector3(0.08f, 0.85f, 0.08f));
            Deck(model, x, -0.53f, width, 0.99f, -0.04f);
            Worktable(model, new Vector3(x, -0.005f, 0.02f), 1.04f, 0.37f);
            PottedPlant(model, new Vector3(x - 0.27f, 0.395f, -0.02f), 0.19f, PlantForms.Species.Daisy, true);
            PottedPlant(model, new Vector3(x + 0.27f, 0.395f, 0.04f), 0.23f, PlantForms.Species.Poppy, true);
            PottedPlant(model, new Vector3(x + 0.44f, 0.00f, -0.82f), 0.27f, PlantForms.Species.Lavender, true);
            model.Box(_clay, new Vector3(x, 0.42f, -0.05f), new Vector3(0.18f, 0.052f, 0.13f));
            model.Box(_soil, new Vector3(x, 0.451f, -0.05f), new Vector3(0.15f, 0.023f, 0.10f));
            WorkshopAwning(model, x);
        }

        private CottageModel AddFinish(ElementId element, Transform part)
        {
            var model = Model();
            GroundPot(model, 0.63f, -1.67f, 0.30f, PlantForms.Species.Daisy);
            switch (element)
            {
                case ElementId.Water:
                    PottedPlant(model, new Vector3(1.91f, 0.0f, 0.30f), 0.27f, PlantForms.Species.Iris, true);
                    Lantern(model, new Vector3(0.89f, 0.74f, -0.06f), 0.73f);
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
                        chime.Box(_brass, new Vector3(x, -0.165f - i * 0.02f, 0f), new Vector3(0.031f, 0.13f + i * 0.04f, 0.031f));
                    }
                    chime.Build(_chime, _meshes);
                    _windTail = new GameObject("Copper weather vane").transform;
                    _windTail.SetParent(part, false);
                    _windTail.localPosition = new Vector3(1.45f, 2.27f, 0.17f);
                    var vane = Model();
                    vane.Box(_copper, Vector3.zero, new Vector3(0.43f, 0.026f, 0.026f));
                    vane.Box(_copper, new Vector3(-0.16f, 0.035f, 0f), new Vector3(0.115f, 0.092f, 0.025f));
                    vane.PlaceMesh(_brass, _gable, new Vector3(0.19f, -0.022f, 0f), new Vector3(0.081f, 0.089f, 0.025f),
                        Quaternion.Euler(0f, 0f, -90f));
                    vane.Build(_windTail, _meshes);
                    break;
                case ElementId.Seed:
                    GroundPot(model, 1.45f, -1.48f, 0.34f, PlantForms.Species.Cosmos);
                    GroundPot(model, 1.78f, -1.66f, 0.24f, PlantForms.Species.ForgetMeNot);
                    break;
                default:
                    GroundPot(model, 1.36f, -0.99f, 0.30f, PlantForms.Species.Lavender);
                    GroundPot(model, 1.70f, -0.93f, 0.27f, PlantForms.Species.WildRose);
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

        private void Shutters(CottageModel model, Vector3 center, float width, float height)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var shutter = center + new Vector3(side * (width * 0.5f + 0.056f), 0.017f, -0.035f);
                model.Box(_door, shutter, new Vector3(0.104f, height + 0.022f, 0.043f), yaw: side * -17f);
                for (int louver = 0; louver < 4; louver++)
                    model.Box(_copper, shutter + new Vector3(0f, (louver - 1.5f) * 0.052f, -0.024f),
                        new Vector3(0.083f, 0.014f, 0.024f), yaw: side * -17f);
            }
        }

        private void Lantern(CottageModel model, Vector3 center, float size)
        {
            model.Box(_glow, center, new Vector3(0.094f, 0.130f, 0.095f) * size);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                model.Box(_brass, center + Vector3.up * (sign * 0.068f * size),
                    new Vector3(0.117f, 0.025f, 0.116f) * size);
                for (int side = -1; side <= 1; side += 2)
                    model.Box(_brass, center + new Vector3(sign * 0.049f, 0f, side * 0.049f) * size,
                        new Vector3(0.012f, 0.144f, 0.012f) * size);
            }
            model.Box(_trim, center + new Vector3(0f, 0.091f, 0.052f) * size,
                new Vector3(0.025f, 0.024f, 0.112f) * size);
        }

        private void LeanToRoof(CottageModel model, Vector3 center, float width, float depth, float pitch,
            Material tile, Material dark)
        {
            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            model.Board(dark, center, new Vector3(width, 0.063f, depth), rotation);
            const int columns = 8, rows = 3;
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                {
                    var offset = new Vector3((col + 0.5f - columns * 0.5f) * width / columns, 0.039f,
                        (row + 0.5f - rows * 0.5f) * depth / rows);
                    model.Board((row * 3 + col) % 7 == 1 ? dark : tile, center + rotation * offset,
                        new Vector3(width / columns * 0.95f, 0.038f, depth / rows * 1.06f), rotation);
                }
            model.Board(_trim, center + rotation * new Vector3(0f, -0.006f, -depth * 0.50f),
                new Vector3(width + 0.038f, 0.077f, 0.048f), rotation);
        }

        private void Bench(CottageModel model, float x, float z, float floor)
        {
            model.Box(_wood, new Vector3(x, floor + 0.20f, z), new Vector3(0.51f, 0.062f, 0.25f));
            model.Box(_trim, new Vector3(x, floor + 0.35f, z + 0.11f), new Vector3(0.51f, 0.10f, 0.046f));
            for (int side = -1; side <= 1; side += 2)
                model.Box(_trim, new Vector3(x + side * 0.19f, floor + 0.12f, z), new Vector3(0.056f, 0.25f, 0.18f));
        }

        private void AddDoorstepGarden(CottageModel model)
        {
            GroundPot(model, -0.91f, -0.68f, 0.29f, PlantForms.Species.Bellflower);
            GroundPot(model, -0.94f, -0.98f, 0.20f, PlantForms.Species.ForgetMeNot);

            var plants = new FoliageMesh();
            plants.Root = new Vector4(0f, 0f, 0f, -1f);
            for (int i = 0; i < 6; i++)
            {
                float z = -1.37f - i * 0.217f;
                float x = Plot.PathX(z) - 0.43f - Mathf.Sin(i * 1.26f) * 0.13f;
                if (!Plot.AllowsDressing(x, z)) continue;
                float y = Plot.Ground(x, z) - Plot.Floor;
                model.Stone(_stonework, new Vector3(x, y + 0.005f, z),
                    new Vector3(0.14f + (i % 2) * 0.07f, 0.085f, 0.18f), i * 49f);
                Vector3 root = new Vector3(x - 0.086f, y, z + 0.027f) / 0.46f;
                PlantForms.Build(plants, Ornament("path-" + i, true), root,
                    i % 3 == 1 ? PlantForms.Species.ForgetMeNot : PlantForms.Species.Daisy);
            }
            if (plants.V.Count > 0) AddPlants(model, plants, Vector3.zero, 0.46f);
        }

        private void GroundPot(CottageModel model, float x, float z, float width, PlantForms.Species species)
        {
            if (Plot.AllowsDressing(x, z))
                PottedPlant(model, new Vector3(x, Plot.Ground(x, z) - Plot.Floor + 0.006f, z), width, species, true);
        }

        private void PottedPlant(CottageModel model, Vector3 at, float width, PlantForms.Species species, bool flower)
        {
            float height = width * 0.69f;
            model.PlaceMesh(_clay, _pot, at, new Vector3(width, height, width));
            model.PlaceMesh(_soil, _disc, at + Vector3.up * (height * 0.93f), new Vector3(width * 0.83f, 1f, width * 0.83f));
            var plants = new FoliageMesh { Root = new Vector4(0f, 0f, 0f, -1f) };
            PlantForms.Build(plants, Ornament(species.ToString(), flower), Vector3.zero, species);
            AddPlants(model, plants, at + Vector3.up * (height * 0.91f), Mathf.Min(0.55f, width * 1.76f));
        }

        private static PlantState Ornament(string id, bool flower) => new PlantState
        {
            PlantId = "cottage-" + id,
            Stage = flower ? PlantStage.Flower : PlantStage.Leaf,
            Growth = 2200
        };

        private void AddPlants(CottageModel model, FoliageMesh plants, Vector3 at, float scale)
        {
            var mesh = plants.ToMesh("House plants");
            _meshes.Add(mesh);
            model.Plants(_herbs, mesh, at, scale);
        }

        private void Worktable(CottageModel model, Vector3 at, float width, float depth)
        {
            model.Box(_wood, at + Vector3.up * 0.36f, new Vector3(width, 0.072f, depth));
            model.Box(_trim, at + Vector3.up * 0.16f, new Vector3(width * 0.92f, 0.044f, depth * 0.83f));
            for (int side = -1; side <= 1; side += 2)
                for (int end = -1; end <= 1; end += 2)
                    model.Box(_trim, at + new Vector3(side * width * 0.41f, 0.17f, end * depth * 0.37f),
                        new Vector3(0.046f, 0.34f, 0.048f));
        }

        private void WorkshopAwning(CottageModel model, float x)
        {
            const int stripes = 9;
            const float width = 1.50f, run = 0.61f;
            for (int i = 0; i < stripes; i++)
            {
                float xx = x + (i + 0.5f - stripes * 0.5f) * width / stripes;
                Material cloth = i % 2 == 0 ? _shadeLinen : _cloth;
                model.Box(cloth, new Vector3(xx, 0.908f, -0.55f), new Vector3(width / stripes + 0.006f, 0.027f, run), -13f);
                model.Box(cloth, new Vector3(xx, 0.793f, -0.85f), new Vector3(width / stripes + 0.004f, 0.125f, 0.030f));
            }
            model.Box(_trim, new Vector3(x, 0.834f, -0.85f), new Vector3(width + 0.06f, 0.038f, 0.037f));
            for (int side = -1; side <= 1; side += 2)
                model.Post(_wood, new Vector3(x + side * 0.69f, 0.66f, -0.53f),
                    new Vector3(x + side * 0.69f, 0.837f, -0.85f), 0.032f);
        }

        private void OnDestroy()
        {
            foreach (var mesh in _meshes) Destroy(mesh);
            foreach (var material in _materials) Destroy(material);
            foreach (var texture in _textures) Destroy(texture);
        }
    }
}
