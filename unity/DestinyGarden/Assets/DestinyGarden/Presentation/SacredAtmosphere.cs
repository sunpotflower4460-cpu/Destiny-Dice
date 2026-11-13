using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// Presentation-only surroundings of the sacred garden: a twilight sea with a shallow
    /// shelf and coastal surf, a ring of seven lights, and spirit motes above the garden.
    /// </summary>
    public sealed class SacredAtmosphere : MonoBehaviour
    {
        private const float SeaLevel = IslandTerrain.SeaLevel;
        private static readonly Vector3 Center = new Vector3(7.5f, 0f, 7.5f);
        private Material _seaMat;
        private Transform _ring;
        private readonly List<Transform> _orbs = new List<Transform>();
        private readonly List<Material> _pillarMats = new List<Material>();
        public bool ReducedMotion;
        private LineRenderer _star;
        private int _starPoints;
        private float _starAlpha, _starTarget;
        private const float RingRadius = 13.4f;
        private Material _bottleMat;
        private Material _orbMat;
        private ParticleSystem _motes;
        private Material _starsMat, _sunMat, _moonMat;
        private Transform _sun, _moon;
        private float _night;
        private Vector3 _orbBase = new Vector3(3.4f, 2.8f, 1.6f);

        /// <summary>The sea, the lights and the sky follow the hour (SkyClock).</summary>
        public void SetSky(SkyState sky, float phase)
        {
            _night = sky.Night;
            if (_seaMat != null)
            {
                _seaMat.SetVector("_WaterTint", sky.SeaTint);
                _seaMat.SetVector("_SkyTint", sky.Haze);
            }
            if (_orbMat != null) _orbMat.SetColor("_EmissionColor", (Vector4)_orbBase * (0.85f + 0.9f * sky.Night));
            if (_motes != null) { var e = _motes.emission; e.rateOverTime = ReducedMotion ? 0f : 2f + 6f * sky.Night; }
            if (_starsMat != null) _starsMat.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.9f, sky.Night)) * 0.9f);
            PlaceBody(_sun, _sunMat, SkyClock.BodyPosition(phase, false), sky.Sun, 1f - sky.Night);
            PlaceBody(_moon, _moonMat, SkyClock.BodyPosition(phase, true), new Color(0.86f, 0.92f, 1f), sky.Night);
        }

        private static void PlaceBody(Transform t, Material m, Vector3 pos, Color color, float strength)
        {
            if (t == null) return;
            t.position = pos;
            float up = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.0f, 7f, pos.y));
            m.color = new Color(color.r, color.g, color.b, 0.85f * up * Mathf.Clamp01(strength * 1.4f));
        }

        /// <summary>Stars, a sun and a moon in the sky behind the island (seen when the camera draws back).</summary>
        private void BuildCelestial()
        {
            var glow = ProceduralArt.GlowSprite();
            var starsObj = new GameObject("Stars");
            starsObj.transform.SetParent(transform, false);
            var ps = starsObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.maxParticles = 220;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f; main.startLifetime = 1e7f;
            var em = ps.emission; em.enabled = false;
            var rng = new System.Random(2026);
            for (int i = 0; i < 200; i++)
            {
                float az = (float)rng.NextDouble() * Mathf.PI, el = 0.18f + (float)rng.NextDouble() * 1.1f;
                var dir = new Vector3(Mathf.Cos(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(az) * Mathf.Cos(el));
                ps.Emit(new ParticleSystem.EmitParams
                {
                    position = Center + dir * 38f, velocity = Vector3.zero, startLifetime = 1e7f,
                    startSize = 0.5f + (float)rng.NextDouble() * 0.9f,
                    startColor = new Color(1f, 0.95f, 0.9f, 0.35f + (float)rng.NextDouble() * 0.65f)
                }, 1);
            }
            _starsMat = IslandHarmony.GlowMaterial(glow, new Color(1f, 1f, 1f, 0f), 2980);
            starsObj.GetComponent<ParticleSystemRenderer>().material = _starsMat;

            Transform Disc(string name, float size, out Material mat, int queue)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = name;
                q.transform.SetParent(transform, false);
                q.transform.localScale = Vector3.one * size;
                mat = IslandHarmony.GlowMaterial(glow, new Color(1f, 1f, 1f, 0f), queue);
                q.GetComponent<MeshRenderer>().material = mat;
                q.AddComponent<Billboard>();
                return q.transform;
            }
            _sun = Disc("Sun", 11f, out _sunMat, 2985);
            _moon = Disc("Moon", 5f, out _moonMat, 2986);
        }

        public void Build()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var sprite = Shader.Find("Sprites/Default");

            _seaMat = new Material(Shader.Find("DestinyGarden/Lagoon"));
            _seaMat.SetTexture("_CoastMap", IslandTerrain.BakeCoastMap());
            _seaMat.SetVector("_CoastParams", new Vector4(Center.x, Center.z,
                1f / IslandTerrain.CoastMapWidth, IslandTerrain.CoastDistanceRange));
            var sea = MakeDisc("Sea", 45.5f, SeaLevel, _seaMat);
            sea.GetComponent<MeshRenderer>().receiveShadows = true;

            BuildSevenLights(lit, sprite);
            BuildMotes(sprite);
            BuildStar();
            BuildBottle();
            BuildCelestial();
        }

        private GameObject MakeDisc(string name, float radius, float y, Material mat)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform);
            obj.transform.position = Center + new Vector3(0f, y, 0f);
            obj.AddComponent<MeshFilter>().sharedMesh = ProceduralArt.DiscMesh(radius, 96);
            var r = obj.AddComponent<MeshRenderer>();
            r.material = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return obj;
        }

        private static Material Transparent(Shader shader, Texture2D tex, Color color, int queue)
        {
            var m = new Material(shader);
            m.SetTexture("_MainTex", tex);
            m.color = color;
            m.renderQueue = queue;
            return m;
        }

        /// <summary>A golden circle on the sea with seven floating lights: the garden's lucky number.</summary>
        private void BuildSevenLights(Shader lit, Shader sprite)
        {
            var ringObj = new GameObject("SevenLights");
            ringObj.transform.SetParent(transform);
            ringObj.transform.position = Center + new Vector3(0f, SeaLevel + 0.06f, 0f);
            _ring = ringObj.transform;

            var line = ringObj.AddComponent<LineRenderer>();
            line.material = IslandHarmony.GlowMaterial(null, new Color(0.86f, 0.66f, 0.31f, 0.35f), 2999);
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.038f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            const int segments = 120;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * RingRadius, 0f, Mathf.Sin(a) * RingRadius));
            }

            var orbMat = new Material(lit) { color = new Color(1f, 0.95f, 0.85f) };
            orbMat.EnableKeyword("_EMISSION");
            orbMat.SetColor("_EmissionColor", new Color(3.4f, 2.8f, 1.6f));
            _orbMat = orbMat;
            var glowTex = ProceduralArt.GlowSprite();
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + 0.2f;
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = "Light" + (i + 1);
                orb.transform.SetParent(_ring, false);
                orb.transform.localPosition = new Vector3(Mathf.Cos(a) * RingRadius, 0.9f, Mathf.Sin(a) * RingRadius);
                orb.transform.localScale = Vector3.one * 0.32f;
                orb.GetComponent<MeshRenderer>().material = orbMat;
                orb.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                Destroy(orb.GetComponent<Collider>());
                _orbs.Add(orb.transform);

                // Soft halo and a faint pillar of light down to the water.
                var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                halo.name = "Halo";
                halo.transform.SetParent(orb.transform, false);
                halo.transform.localScale = Vector3.one * 7f;
                Destroy(halo.GetComponent<Collider>());
                halo.GetComponent<MeshRenderer>().material =
                    Transparent(sprite, glowTex, new Color(1f, 0.84f, 0.52f, 0.55f), 3010);
                halo.AddComponent<Billboard>();

                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "Pillar";
                pillar.transform.SetParent(_ring, false);
                pillar.transform.localPosition = new Vector3(Mathf.Cos(a) * RingRadius, 1.6f, Mathf.Sin(a) * RingRadius);
                pillar.transform.localScale = new Vector3(0.55f, 1.6f, 0.55f);
                Destroy(pillar.GetComponent<Collider>());
                var pm = Transparent(sprite, ProceduralArt.MistSprite(), new Color(1f, 0.86f, 0.55f, 0.16f), 3005);
                _pillarMats.Add(pm);
                pillar.GetComponent<MeshRenderer>().material = pm;
                pillar.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
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
            var mat = new Material(sprite);
            mat.SetTexture("_MainTex", ProceduralArt.GlowSprite());
            mat.renderQueue = 3200;
            obj.GetComponent<ParticleSystemRenderer>().material = mat;
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
            _star.material = IslandHarmony.GlowMaterial(null, Color.white, 3001);
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
        private void BuildBottle()
        {
            var glass = Shader.Find("DestinyGarden/Glass");
            if (glass == null) return; // without the glass shader a bottle would look opaque; skip it
            var profile = new[]
            {
                new Vector2(14.2f, -1.1f), new Vector2(14.45f, 1.5f), new Vector2(14.35f, 4.5f), new Vector2(13.6f, 7.4f),
                new Vector2(11.4f, 10.0f), new Vector2(7.6f, 12.1f), new Vector2(4.2f, 13.3f), new Vector2(3.3f, 14.3f),
                new Vector2(3.2f, 15.6f), new Vector2(3.6f, 15.9f)
            };
            const int seg = 96;
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int i = 0; i < profile.Length; i++)
            {
                Vector2 prev = profile[Mathf.Max(0, i - 1)], next = profile[Mathf.Min(profile.Length - 1, i + 1)];
                Vector2 tan = (next - prev).normalized;
                Vector2 n2 = new Vector2(tan.y, -tan.x);
                for (int j = 0; j <= seg; j++)
                {
                    float a = j / (float)seg * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    verts.Add(new Vector3(c * profile[i].x, profile[i].y, sn * profile[i].x));
                    norms.Add(new Vector3(c * n2.x, n2.y, sn * n2.x).normalized);
                    uvs.Add(new Vector2(j / (float)seg, i / (float)(profile.Length - 1)));
                }
            }
            for (int i = 0; i + 1 < profile.Length; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * (seg + 1) + j, b = a + 1, c2 = a + seg + 1, d = c2 + 1;
                    tris.Add(a); tris.Add(c2); tris.Add(b); tris.Add(b); tris.Add(c2); tris.Add(d);
                }
            var mesh = new Mesh { name = "Bottle" };
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var obj = new GameObject("Bottle");
            obj.transform.SetParent(transform, false);
            obj.transform.position = Center + new Vector3(0f, SeaLevel, 0f);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = obj.AddComponent<MeshRenderer>();
            _bottleMat = new Material(glass);
            r.material = _bottleMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private void Update()
        {
            float t = Time.time;
            var camera = Camera.main;
            if (_bottleMat != null && camera != null)
            {
                float distance = Vector3.Distance(camera.transform.position, Center);
                float wideShot = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 38f, distance));
                float lowAngle = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.57f, -camera.transform.forward.y));
                float outside = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(18f, 24f, distance));
                float glass = Mathf.Max(wideShot, lowAngle * outside);
                float overhead = 1f - 0.85f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.60f, 0.90f, -camera.transform.forward.y));
                _bottleMat.SetFloat("_RimStrength", Mathf.Lerp(0.042f, 0.27f, glass) * overhead);
                _bottleMat.SetFloat("_SpecStrength", Mathf.Lerp(0.05f, 0.34f, glass) * overhead);
            }
            if (_star != null)
            {
                _starAlpha = Mathf.MoveTowards(_starAlpha, _starTarget, Time.deltaTime * 0.4f);
                float breathe = 0.75f + 0.25f * Mathf.Sin(t * 0.8f);
                var sc = new Color(1f, 0.86f, 0.55f, 0.20f * _starAlpha * breathe);
                _star.startColor = sc; _star.endColor = sc;
            }
            if (_seaMat != null) _seaMat.SetFloat("_WaterTime", ReducedMotion ? 0f : Time.timeSinceLevelLoad);
            if (_ring != null && !ReducedMotion)
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
