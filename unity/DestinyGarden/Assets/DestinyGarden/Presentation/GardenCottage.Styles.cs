using UnityEngine;

namespace DestinyGarden.Presentation
{
    internal sealed partial class GardenCottage
    {
        private Material _rainCopper;
        private Transform _millSails;

        private void BuildStyle(CottageStyle style, Transform shell)
        {
            var model = Model();
            switch (style)
            {
                case CottageStyle.RainHouse:
                    RainHouse(model);
                    break;
                case CottageStyle.Windmill:
                    Windmill(model, shell);
                    break;
                case CottageStyle.Observatory:
                    Observatory(model);
                    break;
                case CottageStyle.GreenRoof:
                    GreenRoof(model);
                    break;
            }
            model.Build(shell, _meshes);
        }

        private void RainHouse(CottageModel model)
        {
            const float width = 1.79f, depth = 1.56f, rise = 0.64f;
            _rainCopper = Lit("Hammered copper", new Color(0.62f, 0.40f, 0.27f),
                Mathf.Lerp(0.32f, 0.63f, _roofWetness), _roof.GetTexture("_BaseMap") as Texture2D);
            _rainCopper.SetFloat("_Metallic", 0.24f);
            var vault = KeepStyleMesh(CottageModel.Revolve("Curved copper roof",
                new[] { new Vector2(0.5f, 0f), new Vector2(0.5f, 1f) }, sweep: 180f));
            model.PlaceMesh(_rainCopper, vault, new Vector3(0f, Eave, depth * 0.5f),
                new Vector3(width, depth, rise * 2f), Quaternion.Euler(-90f, 0f, 0f));
            var gable = KeepStyleMesh(CottageModel.CreateBarrelGable(24));
            model.PlaceMesh(_plaster, gable, new Vector3(0f, Eave - 0.006f, 0f),
                new Vector3(width - 0.054f, rise - 0.018f, CabinDepth));
            model.Box(_wood, new Vector3(0f, Eave + 0.002f, 0f), new Vector3(width - 0.035f, 0.078f, CabinDepth + 0.065f));
            RoundWindow(model, new Vector3(0f, 1.23f, -CabinDepth * 0.5f), 0.28f);

            for (int seam = 0; seam <= 12; seam++)
            {
                float angle = Mathf.PI * seam / 12f;
                float x = Mathf.Cos(angle) * width * 0.5f;
                float y = Eave + Mathf.Sin(angle) * rise;
                float roll = Mathf.Atan2(-rise * Mathf.Cos(angle), width * 0.5f * Mathf.Sin(angle)) * Mathf.Rad2Deg;
                model.Board(_copper, new Vector3(x, y, 0f), new Vector3(0.024f, 0.035f, depth + 0.04f),
                    Quaternion.Euler(0f, 0f, roll));
                if (seam == 0) continue;
                float prev = angle - Mathf.PI / 12f;
                for (int end = -1; end <= 1; end += 2)
                {
                    float z = end * depth * 0.50f;
                    model.Post(_rainCopper, new Vector3(Mathf.Cos(prev) * width * 0.5f, Eave + Mathf.Sin(prev) * rise, z),
                        new Vector3(x, y, z), 0.043f);
                }
            }

            const float chainX = -1.00f, chainZ = -1.28f;
            float ground = Plot.Ground(chainX, chainZ) - Plot.Floor;
            model.Box(_copper, new Vector3(-0.895f, Eave - 0.021f, 0f), new Vector3(0.074f, 0.057f, depth));
            model.Post(_copper, new Vector3(-0.895f, Eave - 0.022f, -0.72f), new Vector3(chainX, 0.89f, chainZ), 0.066f);
            var basin = new Vector3(chainX, ground, chainZ);
            model.PlaceMesh(_basinStone, _basin, basin, new Vector3(0.34f, 0.19f, 0.34f));
            model.PlaceMesh(_water, _disc, basin + Vector3.up * 0.153f, new Vector3(0.240f, 1f, 0.240f));
            model.Post(_brass, basin + Vector3.up * 0.15f, new Vector3(chainX, 0.89f, chainZ), 0.016f);
            for (int cup = 0; cup < 5; cup++)
            {
                float y = Mathf.Lerp(0.81f, ground + 0.25f, cup / 4f);
                model.PlaceMesh(_rainCopper, _pot, new Vector3(chainX, y, chainZ), new Vector3(0.074f, 0.081f, 0.074f));
            }
        }

        private void Windmill(CottageModel model, Transform shell)
        {
            const float towerZ = -0.11f;
            var tower = KeepStyleMesh(CottageModel.Revolve("Tapered mill walls", new[]
            {
                new Vector2(0.65f, 0f), new Vector2(0.59f, 0.12f), new Vector2(0.48f, 0.85f)
            }));
            model.Box(_wood, new Vector3(0f, Eave + 0.018f, 0f), new Vector3(1.53f, 0.115f, 1.29f));
            model.PlaceMesh(_plaster, tower, new Vector3(0f, Eave, towerZ), Vector3.one);
            var cap = KeepStyleMesh(CottageModel.Revolve("Mill roof", new[]
            {
                new Vector2(0.001f, 0f), new Vector2(0.58f, 0f), new Vector2(0.58f, 0.052f),
                new Vector2(0.43f, 0.18f), new Vector2(0.24f, 0.33f), new Vector2(0.075f, 0.43f), new Vector2(0.001f, 0.43f)
            }));
            model.PlaceMesh(_roof, cap, new Vector3(0f, Eave + 0.82f, towerZ), Vector3.one);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 2f * Mathf.PI / 12f;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var foot = new Vector3(0f, Eave + 0.05f, towerZ) + radial * 0.625f;
                var beam = new Vector3(0f, Eave + 0.79f, towerZ) + radial * 0.49f;
                model.Post(_trim, foot, beam, 0.042f);
            }
            const float capBase = Eave + 0.82f;
            for (int seam = 0; seam < 12; seam++)
            {
                float angle = seam * 2f * Mathf.PI / 12f;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var knee = Vector3.up * (capBase + 0.18f) + radial * 0.44f + Vector3.forward * towerZ;
                var shoulder = Vector3.up * (capBase + 0.33f) + radial * 0.25f + Vector3.forward * towerZ;
                model.Post(_roofShade, Vector3.up * (capBase + 0.052f) + radial * 0.588f + Vector3.forward * towerZ,
                    knee, 0.024f);
                model.Post(_roofShade, knee, shoulder, 0.024f);
                model.Post(_roofShade, shoulder,
                    Vector3.up * (capBase + 0.43f) + radial * 0.08f + Vector3.forward * towerZ, 0.024f);
            }
            Window(model, new Vector3(0.29f, 1.26f, -0.58f), 0.24f, 0.29f, -30f);
            model.Post(_trim, new Vector3(0f, 1.72f, -0.53f), new Vector3(0f, 1.72f, -0.90f), 0.12f);

            _millSails = new GameObject("Canvas windmill sails").transform;
            _millSails.SetParent(shell, false);
            _millSails.localPosition = new Vector3(0f, 1.72f, -0.89f);
            _millSails.localRotation = Quaternion.Euler(0f, 0f, 22f);
            var sails = Model();
            var canvas = Lit("Natural sailcloth", new Color(0.89f, 0.79f, 0.62f), 0.19f);
            for (int blade = 0; blade < 4; blade++)
            {
                var turn = Quaternion.Euler(0f, 0f, blade * 90f);
                sails.Post(_trim, turn * new Vector3(0f, 0.04f, 0f), turn * new Vector3(0f, 0.738f, 0f), 0.044f);
                sails.Board(canvas, turn * new Vector3(0.119f, 0.486f, 0.015f), new Vector3(0.237f, 0.455f, 0.037f), turn);
                sails.Board(_wood, turn * new Vector3(0.232f, 0.49f, -0.012f), new Vector3(0.024f, 0.467f, 0.029f), turn);
                for (int batten = 0; batten < 4; batten++)
                    sails.Board(_wood, turn * new Vector3(0.116f, 0.267f + batten * 0.145f, -0.015f),
                        new Vector3(0.235f, 0.021f, 0.024f), turn);
            }
            sails.Box(_copper, Vector3.zero, new Vector3(0.18f, 0.18f, 0.12f));
            sails.Box(_brass, new Vector3(0f, 0f, -0.068f), new Vector3(0.10f, 0.10f, 0.066f));
            sails.Build(_millSails, _meshes);
        }

        private void Observatory(CottageModel model)
        {
            const float domeBase = 1.31f;
            var drum = KeepStyleMesh(CottageModel.Revolve("Observatory drum", new[]
            {
                new Vector2(0.68f, 0f), new Vector2(0.68f, 0.10f), new Vector2(0.63f, 0.13f),
                new Vector2(0.63f, 0.29f), new Vector2(0.70f, 0.29f), new Vector2(0.70f, 0.34f)
            }));
            model.Box(_wood, new Vector3(0f, Eave + 0.03f, 0f), new Vector3(1.54f, 0.10f, 1.30f));
            model.PlaceMesh(_trim, drum, new Vector3(0f, Eave, 0f), Vector3.one);
            RoundWindow(model, new Vector3(-0.12f, 1.155f, -0.625f), 0.235f);

            var domeColor = Lit("Indigo dome", new Color(0.25f, 0.33f, 0.47f), 0.39f,
                _roof.GetTexture("_BaseMap") as Texture2D);
            var profile = new[]
            {
                new Vector2(0.72f, 0f), new Vector2(0.70f, 0.15f), new Vector2(0.60f, 0.33f),
                new Vector2(0.46f, 0.47f), new Vector2(0.25f, 0.56f), new Vector2(0.033f, 0.58f)
            };
            const float slitEdge = -44f, sweep = 320f;
            var dome = KeepStyleMesh(CottageModel.Revolve("Slotted dome", profile, slitEdge, sweep));
            model.PlaceMesh(domeColor, dome, Vector3.up * domeBase, Vector3.one);
            for (int seam = 0; seam <= 5; seam++)
            {
                float angle = (slitEdge + sweep * seam / 5f) * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                for (int i = 1; i < profile.Length; i++)
                    model.Post(seam == 0 || seam == 5 ? _brass : _copper,
                        radial * profile[i - 1].x + Vector3.up * (domeBase + profile[i - 1].y),
                        radial * profile[i].x + Vector3.up * (domeBase + profile[i].y), 0.028f);
            }

            var direction = new Vector3(0.43f, 0.57f, -0.70f).normalized;
            var rotation = Quaternion.FromToRotation(Vector3.up, direction);
            var pivot = new Vector3(0.17f, 1.47f, -0.26f);
            var tube = KeepStyleMesh(CottageModel.Revolve("Telescope barrel", new[]
            {
                new Vector2(0.001f, 0f), new Vector2(0.37f, 0f), new Vector2(0.39f, 0.23f),
                new Vector2(0.50f, 0.42f), new Vector2(0.50f, 1f), new Vector2(0.42f, 1f),
                new Vector2(0.42f, 0.92f), new Vector2(0.001f, 0.92f)
            }));
            model.Post(_wood, new Vector3(0.16f, domeBase, -0.27f), pivot, 0.10f);
            model.PlaceMesh(_brass, tube, pivot, new Vector3(0.19f, 0.76f, 0.19f), rotation);
            model.PlaceMesh(domeColor, tube, pivot + direction * 0.23f, new Vector3(0.192f, 0.34f, 0.192f), rotation);
            var lens = Lit("Blue telescope glass", new Color(0.18f, 0.43f, 0.55f), 0.92f);
            model.PlaceMesh(lens, _disc, pivot + direction * 0.708f, new Vector3(0.156f, 1f, 0.156f), rotation);
        }

        private void GreenRoof(CottageModel model)
        {
            const float halfWidth = 0.93f, rise = 0.42f, depth = 1.55f;
            var moss = ProceduralArt.MossTexture();
            _textures.Add(moss);
            var turf = Lit("Living roof turf", new Color(0.92f, 1f, 0.77f), 0.12f, moss);
            model.Gable(_wood, new Vector3(0f, Eave, 0f), new Vector3(CabinWidth, rise, CabinDepth));
            for (int side = -1; side <= 1; side += 2)
            {
                var rotation = Quaternion.Euler(0f, 0f, -side * Mathf.Atan2(rise, halfWidth) * Mathf.Rad2Deg);
                var center = new Vector3(side * halfWidth * 0.5f, Eave + rise * 0.5f, 0f);
                float span = new Vector2(halfWidth, rise).magnitude;
                model.Board(_trim, center, new Vector3(span + 0.06f, 0.082f, depth + 0.033f), rotation);
                model.Board(turf, center + Vector3.up * 0.067f, new Vector3(span + 0.03f, 0.095f, depth), rotation);
                model.Box(_wood, new Vector3(side * halfWidth, Eave + 0.027f, 0f),
                    new Vector3(0.064f, 0.12f, depth + 0.075f));
            }
            model.Box(turf, new Vector3(0f, Eave + rise + 0.046f, 0f), new Vector3(0.15f, 0.125f, depth - 0.015f));

            const float dormerX = 0.24f, dormerZ = -0.50f, dormerDepth = 0.57f;
            model.Box(_plaster, new Vector3(dormerX, 1.27f, dormerZ), new Vector3(0.58f, 0.48f, dormerDepth));
            model.Gable(_plaster, new Vector3(dormerX, 1.50f, dormerZ), new Vector3(0.58f, 0.30f, dormerDepth));
            Roof(model, dormerX, dormerZ, 0.70f, 0.67f, 1.50f, 1.80f);
            RoundWindow(model, new Vector3(dormerX, 1.46f, dormerZ - dormerDepth * 0.5f), 0.27f);

            var plants = new FoliageMesh { Root = new Vector4(0f, 0f, 0f, -1f) };
            var random = new System.Random(1287);
            for (int i = 0; i < 34; i++)
            {
                float side = i < 22 ? -1f : 1f;
                float x = side * (0.53f + 0.28f * (float)random.NextDouble());
                float z = -0.64f + 0.49f * (float)random.NextDouble() + (side > 0f ? 0.87f : 0f);
                var at = RoofGardenPoint(x, z);
                plants.Blade(at, new Vector3(side * 0.35f, 0f, 0.32f), 0.055f + 0.085f * (float)random.NextDouble(),
                    0.016f, new Color(0.22f, 0.37f, 0.13f), new Color(0.48f, 0.65f, 0.25f), i * 1.75f);
            }
            AddPlants(model, plants, Vector3.zero, 1f);
            RoofFlower(-0.56f, -0.42f, PlantForms.Species.Daisy, 0.32f);
            RoofFlower(-0.77f, -0.49f, PlantForms.Species.ForgetMeNot, 0.38f);
            RoofFlower(-0.72f, -0.31f, PlantForms.Species.Buttercup, 0.29f);
            RoofFlower(0.70f, 0.37f, PlantForms.Species.Bellflower, 0.29f);

            Vector3 RoofGardenPoint(float x, float z) =>
                new Vector3(x, Eave + rise * (1f - Mathf.Abs(x) / halfWidth) + 0.109f, z);

            void RoofFlower(float x, float z, PlantForms.Species species, float scale)
            {
                var bloom = new FoliageMesh { Root = new Vector4(0f, 0f, 0f, -1f) };
                PlantForms.Build(bloom, Ornament("roof-" + species, true), Vector3.zero, species);
                AddPlants(model, bloom, RoofGardenPoint(x, z), scale);
            }
        }

        private void RoundWindow(CottageModel model, Vector3 center, float diameter)
        {
            var face = Quaternion.Euler(-90f, 0f, 0f);
            model.PlaceMesh(_trim, _disc, center + Vector3.back * 0.034f, new Vector3(diameter, 1f, diameter), face);
            model.PlaceMesh(_glow, _disc, center + Vector3.back * 0.044f,
                new Vector3(diameter * 0.77f, 1f, diameter * 0.77f), face);
            model.Box(_wood, center + Vector3.back * 0.053f, new Vector3(diameter * 0.77f, 0.023f, 0.021f));
            model.Box(_wood, center + Vector3.back * 0.053f, new Vector3(0.023f, diameter * 0.77f, 0.021f));
        }

        private Mesh KeepStyleMesh(Mesh mesh)
        {
            _meshes.Add(mesh);
            return mesh;
        }
    }
}
