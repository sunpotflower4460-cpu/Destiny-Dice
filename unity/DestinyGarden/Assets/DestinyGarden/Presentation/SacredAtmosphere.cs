using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// Sky and sea lighting, the bottle, and the seven lights around the island.
    /// </summary>
    public sealed class SacredAtmosphere : MonoBehaviour
    {
        private const float SeaLevel = IslandTerrain.SeaLevel;
        private static readonly Vector3 Center = new Vector3(7.5f, 0f, 7.5f);
        private Material _seaMat;
        private Transform _ring;
        private readonly List<Transform> _orbs = new List<Transform>();
        public GardenView View;
        private LineRenderer _star;
        private int _starPoints;
        private float _starAlpha, _starTarget;
        private const float RingRadius = 13.4f;
        private Material _farGlass, _nearGlass, _lipGlass;
        private MeshRenderer _bottle;
        private MeshRenderer _cork;
        private Material _corkMat;
        private Texture2D _corkTexture;
        private Mesh _corkMesh;
        private Material _orbMat;
        private ParticleSystem _motes;
        private Material _starsMat, _skyMat, _haloMat, _wakeMat, _ringMat, _harmonyMat, _motesMat;
        private Texture2D _cloudNoise, _starlight, _coastMap, _wakeTexture;
        private Mesh _starMesh, _seaMesh, _bottleMesh;
        private float _motionTime;
        private static readonly Color Warmth = new Color(0.42f, 0.16f, 0.12f);
        private static readonly int HorizonId = Shader.PropertyToID("_DGSkyHorizon");
        private static readonly int ZenithId = Shader.PropertyToID("_DGSkyZenith");
        private static readonly int WarmthId = Shader.PropertyToID("_DGSkyWarmth");
        private static readonly int SunId = Shader.PropertyToID("_DGSkySun");
        private static readonly int MoonId = Shader.PropertyToID("_DGSkyMoon");
        private static readonly int WeatherId = Shader.PropertyToID("_DGSkyWeather");
        private static readonly int WaterTintId = Shader.PropertyToID("_WaterTint");
        private static readonly int GlassVisibilityId = Shader.PropertyToID("_Visibility");
        private static readonly int BottleWaterlineId = Shader.PropertyToID("_BottleWaterline");

        /// <summary>The sea, the lights and the sky follow the saved hour and weather.</summary>
        private void SetSky(SkyState sky, bool reducedMotion)
        {
            _seaMat.SetVector(WaterTintId, sky.SeaTint);
            Shader.SetGlobalVector(HorizonId, sky.Horizon);
            Shader.SetGlobalVector(ZenithId, sky.Zenith);
            Shader.SetGlobalVector(WarmthId, new Vector4(Warmth.r, Warmth.g, Warmth.b, sky.Twilight));
            Shader.SetGlobalVector(SunId, new Vector4(sky.SunDirection.x, sky.SunDirection.y, sky.SunDirection.z, sky.SunVisibility));
            Shader.SetGlobalVector(MoonId, new Vector4(sky.MoonDirection.x, sky.MoonDirection.y, sky.MoonDirection.z, sky.MoonVisibility));

            _orbMat.SetColor("_EmissionColor", new Color(1.88f, 1.19f, 0.54f) * Mathf.Lerp(0.77f, 1.31f, sky.Night));
            _haloMat.color = new Color(1f, 0.69f, 0.39f, Mathf.Lerp(0.27f, 0.55f, sky.Night));
            _wakeMat.color = new Color(1f, 0.69f, 0.42f, Mathf.Lerp(0.14f, 0.32f, sky.Night));
            _ringMat.color = new Color(0.89f, 0.68f, 0.43f, Mathf.Lerp(0.21f, 0.32f, sky.Night));
            var emission = _motes.emission;
            emission.rateOverTime = reducedMotion ? 0f : Mathf.Lerp(1.1f, 3.0f, sky.Night);
            float dark = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.49f, 0.96f, sky.Night));
            _starsMat.color = new Color(1f, 1f, 1f, dark * (1f - 0.46f * sky.Moisture));
        }

        private void BuildSky()
        {
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Sky dome";
            dome.transform.SetParent(transform, false);
            dome.transform.position = Center;
            dome.transform.localScale = Vector3.one * 190f;
            Destroy(dome.GetComponent<Collider>());
            _cloudNoise = ProceduralArt.CloudNoise();
            _skyMat = new Material(Shader.Find("DestinyGarden/Sky"));
            _skyMat.SetTexture("_CloudNoise", _cloudNoise);
            var paintedSky = Resources.Load<Texture2D>("StorybookCloudbanks");
            Shader.SetGlobalTexture("_DGPaintedSky", paintedSky != null ? paintedSky : Texture2D.grayTexture);
            Shader.SetGlobalFloat("_DGPaintedSkyWeight", paintedSky != null ? 1f : 0f);
            var renderer = dome.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _skyMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // One sparse, static star field extends around the full orbit of the camera.
            const int count = 128;
            var vertices = new Vector3[count * 4];
            var colors = new Color[count * 4];
            var uv = new Vector2[count * 4];
            var triangles = new int[count * 6];
            var rng = new System.Random(2026);
            for (int i = 0; i < count; i++)
            {
                float height = (float)rng.NextDouble();
                float y = Mathf.Lerp(0.025f, 0.97f, height * height);
                float azimuth = (i * 2.399963f + (float)rng.NextDouble() * 0.16f) % (Mathf.PI * 2f);
                float horizontal = Mathf.Sqrt(1f - y * y);
                var normal = new Vector3(Mathf.Cos(azimuth) * horizontal, y, Mathf.Sin(azimuth) * horizontal);
                var point = normal * 72f;
                float size = i % 16 == 0 ? 0.24f : Mathf.Lerp(0.075f, 0.15f, (float)rng.NextDouble());
                var right = Vector3.Cross(Vector3.up, normal).normalized * size;
                var up = Vector3.Cross(normal, right);
                var color = Color.Lerp(new Color(0.63f, 0.78f, 1f), new Color(1f, 0.88f, 0.72f), (float)rng.NextDouble());
                color.a = Mathf.Lerp(0.26f, 0.74f, (float)rng.NextDouble()) * Mathf.SmoothStep(0.35f, 1f, y * 5.3f);
                int v = i * 4, t = i * 6;
                vertices[v] = point - right - up; vertices[v + 1] = point + right - up;
                vertices[v + 2] = point + right + up; vertices[v + 3] = point - right + up;
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = color;
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.right; uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            _starMesh = new Mesh { name = "Distant stars", vertices = vertices, colors = colors, uv = uv, triangles = triangles };
            var stars = new GameObject("Distant stars");
            stars.transform.SetParent(transform, false);
            stars.transform.position = Center;
            stars.AddComponent<MeshFilter>().sharedMesh = _starMesh;
            _starlight = ProceduralArt.GlowSprite();
            _starsMat = IslandHarmony.GlowMaterial(_starlight, new Color(1f, 1f, 1f, 0f), 2980);
            _starsMat.SetFloat("_Intensity", 1.65f);
            var starRenderer = stars.AddComponent<MeshRenderer>();
            starRenderer.sharedMaterial = _starsMat;
            starRenderer.shadowCastingMode = ShadowCastingMode.Off;
            starRenderer.receiveShadows = false;
        }

        public void Build()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var sprite = Shader.Find("Sprites/Default");

            _seaMat = new Material(Shader.Find("DestinyGarden/Lagoon"));
            _coastMap = IslandTerrain.BakeCoastMap();
            _seaMat.SetTexture("_CoastMap", _coastMap);
            _seaMat.SetVector("_CoastParams", new Vector4(Center.x, Center.z,
                1f / IslandTerrain.CoastMapWidth, IslandTerrain.CoastDistanceRange));
            // Keep water under the frame when a tall opening view pulls the camera back.
            var sea = MakeDisc("Sea", 270f, SeaLevel, _seaMat);
            sea.GetComponent<MeshRenderer>().receiveShadows = true;

            BuildSky();
            BuildSevenLights(lit);
            BuildMotes(sprite);
            BuildStar();
            BuildBottle(lit);
            SetSky(View.Sky, View.ReducedMotion);
        }

        private GameObject MakeDisc(string name, float radius, float y, Material mat)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform);
            obj.transform.position = Center + new Vector3(0f, y, 0f);
            _seaMesh = ProceduralArt.DiscMesh(radius, 96);
            obj.AddComponent<MeshFilter>().sharedMesh = _seaMesh;
            var r = obj.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return obj;
        }

        /// <summary>A golden circle on the sea with seven floating lights: the garden's lucky number.</summary>
        private void BuildSevenLights(Shader lit)
        {
            var ringObj = new GameObject("SevenLights");
            ringObj.transform.SetParent(transform);
            ringObj.transform.position = Center + new Vector3(0f, SeaLevel + 0.06f, 0f);
            _ring = ringObj.transform;

            var line = ringObj.AddComponent<LineRenderer>();
            _ringMat = IslandHarmony.GlowMaterial(null, Color.white, 2999);
            line.sharedMaterial = _ringMat;
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.028f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            const int segments = 120;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * RingRadius, 0f, Mathf.Sin(a) * RingRadius));
            }

            _orbMat = new Material(lit) { color = new Color(1f, 0.95f, 0.85f) };
            _orbMat.EnableKeyword("_EMISSION");
            _haloMat = IslandHarmony.GlowMaterial(_starlight, Color.white, 3010);
            _wakeTexture = ProceduralArt.LanternReflection();
            _wakeMat = IslandHarmony.GlowMaterial(_wakeTexture, Color.white, 2997);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + 0.2f;
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = "Light" + (i + 1);
                orb.transform.SetParent(_ring, false);
                orb.transform.localPosition = new Vector3(Mathf.Cos(a) * RingRadius, 0.9f, Mathf.Sin(a) * RingRadius);
                orb.transform.localScale = Vector3.one * 0.22f;
                orb.GetComponent<MeshRenderer>().sharedMaterial = _orbMat;
                orb.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                Destroy(orb.GetComponent<Collider>());
                _orbs.Add(orb.transform);

                var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                halo.name = "Halo";
                halo.transform.SetParent(orb.transform, false);
                halo.transform.localScale = Vector3.one * 8.1f;
                Destroy(halo.GetComponent<Collider>());
                var haloRenderer = halo.GetComponent<MeshRenderer>();
                haloRenderer.sharedMaterial = _haloMat;
                haloRenderer.shadowCastingMode = ShadowCastingMode.Off;
                halo.AddComponent<Billboard>();

                var reflection = GameObject.CreatePrimitive(PrimitiveType.Quad);
                reflection.name = "Lantern reflection";
                reflection.transform.SetParent(_ring, false);
                reflection.transform.localPosition = new Vector3(Mathf.Cos(a) * RingRadius, -0.046f, Mathf.Sin(a) * RingRadius);
                reflection.transform.localRotation = Quaternion.Euler(90f, 0f, -a * Mathf.Rad2Deg);
                reflection.transform.localScale = new Vector3(0.78f, 1.88f, 1f);
                Destroy(reflection.GetComponent<Collider>());
                var reflected = reflection.GetComponent<MeshRenderer>();
                reflected.sharedMaterial = _wakeMat;
                reflected.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private void BuildMotes(Shader sprite)
        {
            var obj = new GameObject("SpiritMotes");
            obj.transform.SetParent(transform);
            obj.transform.position = Center + new Vector3(0f, 0.6f, 0f);
            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = 48;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            main.gravityModifier = -0.015f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.86f, 0.58f, 0.55f), new Color(0.67f, 0.90f, 1f, 0.48f));
            var emission = ps.emission;
            emission.rateOverTime = 2f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, 0.4f, 16f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            _motesMat = new Material(sprite);
            _motesMat.SetTexture("_MainTex", _starlight);
            _motesMat.renderQueue = 3200;
            obj.GetComponent<ParticleSystemRenderer>().sharedMaterial = _motesMat;
            _motes = ps;
            ps.Play();
        }

        /// <summary>A faint star of light on the sea, joined through the ring (harmony 5, 7 or 8 points).</summary>
        private void BuildStar()
        {
            var obj = new GameObject("HarmonyStar");
            obj.transform.SetParent(_ring, false);
            obj.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            _star = obj.AddComponent<LineRenderer>();
            _harmonyMat = IslandHarmony.GlowMaterial(null, Color.white, 3001);
            _star.sharedMaterial = _harmonyMat;
            _star.useWorldSpace = false;
            _star.loop = true;
            _star.widthMultiplier = 0.035f;
            _star.shadowCastingMode = ShadowCastingMode.Off;
            _star.positionCount = 0;
        }

        /// <summary>0 hides the star; 5, 7 or 8 draws {5/2}, {7/3} or {8/3}.</summary>
        public void SetStar(int points)
        {
            _starTarget = points > 0 ? 1f : 0f;
            if (points <= 0 || points == _starPoints) return;
            _starPoints = points;
            int step = points == 5 ? 2 : 3;
            _star.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float a = (i * step % points) / (float)points * Mathf.PI * 2f + 0.2f;
                _star.SetPosition(i, new Vector3(Mathf.Cos(a) * RingRadius, 0f, Mathf.Sin(a) * RingRadius));
            }
        }

        /// <summary>A thin glass bottle around the island and its water, floating on the sea.</summary>
        private void BuildBottle(Shader lit)
        {
            var glass = Shader.Find("DestinyGarden/Glass");
            if (glass == null) return;

            _bottleMesh = BottleMesh.Create();
            _farGlass = GlassMaterial("Far glass", CullMode.Front, (int)RenderQueue.Transparent - 11);
            _nearGlass = GlassMaterial("Near glass", CullMode.Back, 3050);
            _nearGlass.SetFloat("_NearWall", 1f);
            _lipGlass = GlassMaterial("Rolled glass lip", CullMode.Back, 3051);
            _lipGlass.SetFloat("_Lip", 1f);
            var obj = new GameObject("Bottle");
            obj.transform.SetParent(transform, false);
            obj.transform.position = Center + new Vector3(0f, SeaLevel, 0f);
            obj.AddComponent<MeshFilter>().sharedMesh = _bottleMesh;
            _bottle = obj.AddComponent<MeshRenderer>();
            _bottle.sharedMaterials = new[] { _farGlass, _nearGlass, _lipGlass };
            _bottle.shadowCastingMode = ShadowCastingMode.Off;
            _bottle.receiveShadows = false;

            var stopper = new GameObject("Cork stopper");
            stopper.transform.SetParent(obj.transform, false);
            _corkMesh = BottleCork.CreateMesh();
            _corkTexture = BottleCork.CreateTexture();
            _corkMat = BottleCork.CreateMaterial(lit, _corkTexture);
            stopper.AddComponent<MeshFilter>().sharedMesh = _corkMesh;
            _cork = stopper.AddComponent<MeshRenderer>();
            _cork.sharedMaterial = _corkMat;
            _cork.shadowCastingMode = ShadowCastingMode.Off;
            _cork.receiveShadows = false;

            Material GlassMaterial(string name, CullMode cull, int queue)
            {
                var material = new Material(glass) { name = name, renderQueue = queue };
                material.SetFloat("_Cull", (float)cull);
                return material;
            }
        }

        private void LateUpdate()
        {
            if (View == null || _seaMat == null) return;
            var sky = View.Sky;
            bool reducedMotion = View.ReducedMotion;
            SetSky(sky, reducedMotion);
            if (!reducedMotion) _motionTime += Time.deltaTime;
            float t = _motionTime;
            Shader.SetGlobalVector(WeatherId, new Vector4(sky.CloudCover, sky.Night, t, sky.Moisture));
            var camera = View.ViewCamera;
            if (_bottle != null && camera != null)
            {
                Vector3 offset = camera.transform.position - Center;
                float horizontal = new Vector2(offset.x, offset.z).magnitude;
                float outside = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(15f, 25f, horizontal));
                float wide = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(25f, 39f, offset.magnitude));
                float low = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.70f, -camera.transform.forward.y));
                float visible = outside * low;
                float detail = wide * visible;
                _bottle.enabled = visible > 0f;
                _farGlass.SetFloat(GlassVisibilityId, visible * Mathf.Lerp(0.20f, 1f, wide));
                _nearGlass.SetFloat(GlassVisibilityId, detail);
                _lipGlass.SetFloat(GlassVisibilityId, detail);
                _cork.enabled = detail > 0.003f;
                if (_cork.enabled) _corkMat.color = new Color(1f, 1f, 1f, detail);
                _seaMat.SetVector(BottleWaterlineId, new Vector4(BottleMesh.WaterlineRadius, detail, 0f, 0f));
            }
            if (_star != null)
            {
                _starAlpha = Mathf.MoveTowards(_starAlpha, _starTarget, Time.deltaTime * 0.4f);
                float breathe = 0.86f + 0.14f * Mathf.Sin(t * 0.8f);
                var sc = new Color(1f, 0.86f, 0.55f, 0.20f * _starAlpha * breathe);
                _star.startColor = sc; _star.endColor = sc;
            }
            if (!reducedMotion)
            {
                _ring.rotation = Quaternion.Euler(0f, t * 2.2f, 0f);
                for (int i = 0; i < _orbs.Count; i++)
                {
                    var p = _orbs[i].localPosition;
                    p.y = 0.9f + Mathf.Sin(t * 0.8f + i * 0.9f) * 0.18f;
                    _orbs[i].localPosition = p;
                }
            }
        }

        private void OnDestroy()
        {
            Destroy(_skyMat);
            Destroy(_cloudNoise);
            Destroy(_seaMat);
            Destroy(_coastMap);
            Destroy(_seaMesh);
            Destroy(_starMesh);
            Destroy(_starsMat);
            Destroy(_starlight);
            Destroy(_farGlass);
            Destroy(_nearGlass);
            Destroy(_lipGlass);
            Destroy(_bottleMesh);
            Destroy(_corkMat);
            Destroy(_corkMesh);
            Destroy(_corkTexture);
            Destroy(_orbMat);
            Destroy(_haloMat);
            Destroy(_ringMat);
            Destroy(_harmonyMat);
            Destroy(_motesMat);
            Destroy(_wakeMat);
            Destroy(_wakeTexture);
        }
    }

    /// <summary>Keeps a quad facing the main camera.</summary>
    public sealed class Billboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
