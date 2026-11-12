using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// Presentation-only surroundings of the sacred garden: a twilight sea with a lagoon
    /// glow and drifting glints, shoreline light around the island, a slow ring of seven
    /// lights on the water, and spirit motes rising from the garden. Never reads or
    /// changes the model.
    /// </summary>
    public sealed class SacredAtmosphere : MonoBehaviour
    {
        private const float SeaLevel = -0.80f;
        private static readonly Vector3 Center = new Vector3(7.5f, 0f, 7.5f);
        private Material _seaMat;
        private Material _sparkleMat;
        private Material _shoreMat;
        private Transform _ring;
        private readonly List<Transform> _orbs = new List<Transform>();
        private readonly List<Material> _pillarMats = new List<Material>();
        public bool ReducedMotion;

        public void Build()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var sprite = Shader.Find("Sprites/Default");

            // Sea: lit for the sun's specular, self-lit so the lagoon glows at twilight.
            var seaTex = ProceduralArt.SeaTexture();
            _seaMat = new Material(lit);
            _seaMat.SetTexture("_BaseMap", seaTex);
            _seaMat.SetColor("_BaseColor", Color.white);
            _seaMat.SetFloat("_Smoothness", 0.90f);
            _seaMat.SetFloat("_Metallic", 0.0f);
            _seaMat.EnableKeyword("_EMISSION");
            _seaMat.SetTexture("_EmissionMap", seaTex);
            _seaMat.SetColor("_EmissionColor", new Color(0.55f, 0.75f, 0.85f));
            var sea = MakeDisc("Sea", 110f, SeaLevel, _seaMat);
            sea.GetComponent<MeshRenderer>().receiveShadows = true;

            // Drifting glints on the water.
            _sparkleMat = Transparent(sprite, ProceduralArt.SparkleTexture(), new Color(1f, 0.92f, 0.78f, 0.75f), 2990);
            _sparkleMat.mainTextureScale = new Vector2(16f, 16f);
            MakeDisc("SeaGlints", 46f, SeaLevel + 0.02f, _sparkleMat);

            // Light hugging the shoreline of the island.
            _shoreMat = Transparent(sprite, ProceduralArt.ShoreGlow(), new Color(0.75f, 0.95f, 1f, 0.85f), 2995);
            // Round shoreline: the island meets the sea at about 11 units from the centre.
            _shoreMat.mainTexture = ProceduralArt.RingGlow(11.0f / 13f, 0.035f);
            MakeDisc("ShoreGlow", 13f, SeaLevel + 0.03f, _shoreMat);

            BuildSevenLights(lit, sprite);
            BuildMotes(sprite);
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

            var gold = new Material(lit) { color = new Color(1f, 0.80f, 0.42f) };
            gold.EnableKeyword("_EMISSION");
            gold.SetColor("_EmissionColor", new Color(2.6f, 1.7f, 0.55f));
            var line = ringObj.AddComponent<LineRenderer>();
            line.material = gold;
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.07f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            const int segments = 120;
            const float radius = 13.4f;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }

            var orbMat = new Material(lit) { color = new Color(1f, 0.95f, 0.85f) };
            orbMat.EnableKeyword("_EMISSION");
            orbMat.SetColor("_EmissionColor", new Color(3.4f, 2.8f, 1.6f));
            var glowTex = ProceduralArt.GlowSprite();
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + 0.2f;
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = "Light" + (i + 1);
                orb.transform.SetParent(_ring, false);
                orb.transform.localPosition = new Vector3(Mathf.Cos(a) * radius, 0.9f, Mathf.Sin(a) * radius);
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
                pillar.transform.localPosition = new Vector3(Mathf.Cos(a) * radius, 1.6f, Mathf.Sin(a) * radius);
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
            main.maxParticles = 120;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.gravityModifier = -0.015f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.86f, 0.50f, 0.85f), new Color(0.60f, 0.92f, 1f, 0.75f));
            var emission = ps.emission;
            emission.rateOverTime = 12f;
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
            ps.Play();
        }

        private void Update()
        {
            float t = Time.time;
            if (_sparkleMat != null)
            {
                _sparkleMat.mainTextureOffset = ReducedMotion ? Vector2.zero : new Vector2(t * 0.006f, t * 0.004f);
                var c = _sparkleMat.color;
                c.a = 0.55f + 0.25f * Mathf.Sin(t * 0.9f);
                _sparkleMat.color = c;
            }
            if (_seaMat != null && !ReducedMotion)
                _seaMat.SetTextureOffset("_BaseMap", new Vector2(Mathf.Sin(t * 0.05f) * 0.004f, Mathf.Cos(t * 0.04f) * 0.004f));
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
            if (_shoreMat != null)
            {
                var c = _shoreMat.color;
                c.a = 0.72f + 0.14f * Mathf.Sin(t * 0.6f);
                _shoreMat.color = c;
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
