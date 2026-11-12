using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// Rare, rule-bound presentation: butterflies while nature is in balance
    /// (NatureBalance), the rainbow figure-eight and rising angels when the Lucky Seven
    /// makes a flower bloom (LuckySeven.IsRainbow), and a halo of seven colours around
    /// the die while the seventh face is published. Decisions come from domain rules;
    /// this class only draws them.
    /// </summary>
    public sealed class GardenWonders : MonoBehaviour
    {
        public static readonly Color[] Rainbow =
        {
            new Color(1.00f, 0.25f, 0.30f), new Color(1.00f, 0.58f, 0.18f), new Color(1.00f, 0.92f, 0.30f),
            new Color(0.35f, 0.95f, 0.45f), new Color(0.25f, 0.80f, 1.00f), new Color(0.35f, 0.45f, 1.00f),
            new Color(0.78f, 0.42f, 1.00f)
        };

        public GardenView View;
        public bool ReducedMotion;
        private Shader _lit, _sprite;
        private readonly List<Butterfly> _butterflies = new List<Butterfly>();
        private readonly List<Vector3> _flowerSpots = new List<Vector3>();
        private bool _butterfliesActive;
        private ParticleSystem _angels;
        private ParticleSystem _sparkles;
        private Transform _diceHalo;
        private readonly List<Transform> _diceHaloOrbs = new List<Transform>();

        public void Build()
        {
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            _sprite = Shader.Find("Sprites/Default");
            BuildButterflies();
            BuildAngels();
            BuildDiceHalo();
        }

        // ---------------- Butterflies ----------------

        private sealed class Butterfly
        {
            public Transform Root, LeftWing, RightWing;
            public Vector3 Target, Velocity;
            public float Phase, Retarget, RestUntil;
            public bool Leaving;
        }

        private void BuildButterflies()
        {
            var palettes = new[]
            {
                (new Color(0.30f, 0.85f, 1.00f), new Color(0.12f, 0.25f, 0.85f)),
                (new Color(1.00f, 0.90f, 0.40f), new Color(0.95f, 0.45f, 0.12f)),
                (new Color(1.00f, 0.70f, 0.88f), new Color(0.62f, 0.30f, 0.85f)),
                (new Color(0.92f, 1.00f, 0.96f), new Color(0.35f, 0.85f, 0.70f)),
                (new Color(1.00f, 0.82f, 0.60f), new Color(0.85f, 0.25f, 0.35f))
            };
            var wingMesh = WingMesh();
            var bodyMat = new Material(_lit) { color = new Color(0.10f, 0.07f, 0.12f) };
            for (int i = 0; i < palettes.Length; i++)
            {
                var mat = new Material(_sprite);
                mat.SetTexture("_MainTex", ProceduralArt.ButterflyWing(palettes[i].Item1, palettes[i].Item2));
                mat.renderQueue = 3150;
                var root = new GameObject("Butterfly" + i).transform;
                root.SetParent(transform);
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.transform.SetParent(root, false);
                body.transform.localScale = new Vector3(0.035f, 0.07f, 0.035f);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body.GetComponent<MeshRenderer>().material = bodyMat;
                body.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                Destroy(body.GetComponent<Collider>());
                var b = new Butterfly
                {
                    Root = root,
                    LeftWing = Wing(root, wingMesh, mat, -1),
                    RightWing = Wing(root, wingMesh, mat, 1),
                    Phase = i * 1.3f
                };
                root.localScale = Vector3.one * 1.25f;
                root.gameObject.SetActive(false);
                _butterflies.Add(b);
            }
        }

        private static Transform Wing(Transform root, Mesh mesh, Material mat, int side)
        {
            var w = new GameObject(side < 0 ? "WingL" : "WingR");
            w.transform.SetParent(root, false);
            w.transform.localScale = new Vector3(side * 0.30f, 1f, 0.30f);
            w.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = w.AddComponent<MeshRenderer>();
            r.material = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return w.transform;
        }

        /// <summary>Unit wing quad hinged at x=0 (the body), spanning x 0..1, z -0.5..0.5.</summary>
        private static Mesh WingMesh()
        {
            var m = new Mesh { name = "Wing" };
            m.vertices = new[] { new Vector3(0, 0, -0.5f), new Vector3(1, 0, -0.5f), new Vector3(1, 0, 0.5f), new Vector3(0, 0, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Called after every refresh with the domain's NatureBalance verdict.</summary>
        public void SetButterflies(bool active, IEnumerable<Vector3> flowerTops)
        {
            _flowerSpots.Clear();
            if (flowerTops != null) _flowerSpots.AddRange(flowerTops);
            if (active == _butterfliesActive) return;
            _butterfliesActive = active;
            for (int i = 0; i < _butterflies.Count; i++)
            {
                var b = _butterflies[i];
                if (active)
                {
                    float a = i / (float)_butterflies.Count * Mathf.PI * 2f + 0.7f;
                    b.Root.position = new Vector3(7.5f + Mathf.Cos(a) * 15f, 3.5f + i * 0.3f, 7.5f + Mathf.Sin(a) * 15f);
                    b.Velocity = Vector3.zero;
                    b.Leaving = false;
                    b.Retarget = 0f;
                    b.RestUntil = 0f;
                    b.Root.gameObject.SetActive(true);
                }
                else
                {
                    b.Leaving = true;
                }
            }
        }

        public bool ButterfliesActive => _butterfliesActive;

        private void UpdateButterflies(float t, float dt)
        {
            foreach (var b in _butterflies)
            {
                if (!b.Root.gameObject.activeSelf) continue;
                if (b.Leaving)
                {
                    Vector3 away = b.Root.position - new Vector3(7.5f, 0f, 7.5f);
                    away.y = 0f;
                    b.Target = b.Root.position + away.normalized * 6f + Vector3.up * 1.5f;
                    if (away.magnitude > 20f) { b.Root.gameObject.SetActive(false); continue; }
                }
                else if (t > b.Retarget)
                {
                    b.Retarget = t + Random.Range(2.5f, 5f);
                    if (_flowerSpots.Count > 0 && Random.value < 0.7f)
                    {
                        b.Target = _flowerSpots[Random.Range(0, _flowerSpots.Count)] + Vector3.up * 0.12f;
                        b.RestUntil = t + Random.Range(1.5f, 3f);
                    }
                    else
                    {
                        b.Target = new Vector3(Random.Range(2f, 13f), Random.Range(1.2f, 2.8f), Random.Range(2f, 13f));
                        b.RestUntil = 0f;
                    }
                }
                Vector3 to = b.Target - b.Root.position;
                bool resting = !b.Leaving && to.magnitude < 0.15f && t < b.RestUntil;
                Vector3 wobble = new Vector3(Mathf.Sin(t * 3.1f + b.Phase), Mathf.Sin(t * 4.3f + b.Phase * 2f) * 0.6f,
                    Mathf.Cos(t * 2.7f + b.Phase)) * (resting ? 0f : 0.9f);
                Vector3 desired = to.normalized * Mathf.Min(1.6f, to.magnitude * 1.4f) + wobble;
                b.Velocity = Vector3.Lerp(b.Velocity, resting ? Vector3.zero : desired, dt * 2.2f);
                b.Root.position += b.Velocity * dt;
                Vector3 flat = new Vector3(b.Velocity.x, 0f, b.Velocity.z);
                if (flat.sqrMagnitude > 0.0004f)
                    b.Root.rotation = Quaternion.Slerp(b.Root.rotation, Quaternion.LookRotation(flat), dt * 5f);
                float flap = resting ? 18f + Mathf.Sin(t * 2.2f + b.Phase) * 14f
                                     : Mathf.Sin(t * 17f + b.Phase) * 62f;
                b.LeftWing.localRotation = Quaternion.Euler(0f, 0f, flap);
                b.RightWing.localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
        }

        // ---------------- Rainbow figure-eight and angels ----------------

        private void BuildAngels()
        {
            var obj = new GameObject("Angels");
            obj.transform.SetParent(transform);
            _angels = obj.AddComponent<ParticleSystem>();
            var main = _angels.main;
            main.playOnAwake = false;
            main.maxParticles = 80;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5.0f, 7.0f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.30f, 0.52f);
            main.gravityModifier = -0.035f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1f, 0.97f, 0.90f, 0.42f);
            var emission = _angels.emission;
            emission.rateOverTime = 0f;
            var sheet = _angels.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 1;
            sheet.cycleCount = 14;
            var fade = _angels.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.9f, 0.7f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var noise = _angels.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.35f;
            var mat = new Material(_sprite);
            mat.SetTexture("_MainTex", ProceduralArt.AngelSheet());
            mat.renderQueue = 3300;
            obj.GetComponent<ParticleSystemRenderer>().material = mat;

            var sObj = new GameObject("RainbowSparkles");
            sObj.transform.SetParent(transform);
            _sparkles = sObj.AddComponent<ParticleSystem>();
            var sMain = _sparkles.main;
            sMain.playOnAwake = false;
            sMain.maxParticles = 400;
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            sMain.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var sEmission = _sparkles.emission;
            sEmission.rateOverTime = 0f;
            var sMat = new Material(_sprite);
            sMat.SetTexture("_MainTex", ProceduralArt.GlowSprite());
            sMat.renderQueue = 3250;
            sObj.GetComponent<ParticleSystemRenderer>().material = sMat;
        }

        /// <summary>Upright lemniscate (an "8"): t in [0, 2pi).</summary>
        private static Vector2 Eight(float t, float a)
        {
            float s = Mathf.Sin(t), c = Mathf.Cos(t);
            float d = 1f + s * s;
            return new Vector2(a * s * c / d, a * c / d);
        }

        /// <summary>Soft prismatic band texture: transparent edges across the width, faint shimmer along it.</summary>
        private static Texture2D _bandTex;
        private static Texture2D BandTexture()
        {
            if (_bandTex != null) return _bandTex;
            const int w = 64, h = 32;
            _bandTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Band", wrapMode = TextureWrapMode.Repeat };
            var c = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float across = Mathf.Sin(y / (float)(h - 1) * Mathf.PI);
                    float shimmer = 0.80f + 0.20f * Mathf.Sin(x / (float)w * Mathf.PI * 6f);
                    c[y * w + x] = new Color(1f, 1f, 1f, across * across * shimmer);
                }
            _bandTex.SetPixels(c);
            _bandTex.Apply();
            return _bandTex;
        }

        /// <summary>Pale spectral tint: the rainbow as light seen through mist, not paint.</summary>
        private static Color Pale(Color c, float alpha) =>
            new Color(Mathf.Lerp(c.r, 1f, 0.45f), Mathf.Lerp(c.g, 1f, 0.45f), Mathf.Lerp(c.b, 1f, 0.45f), alpha);

        /// <summary>
        /// The figure-eight of light. It does not draw itself like a stroke: it condenses out of
        /// the air as a faint, breathing prism of light, angels half-seen within it, then rises
        /// and dissolves into motes.
        /// </summary>
        public IEnumerator PlayRainbowEight()
        {
            var cam = View != null ? View.ViewCamera : Camera.main;
            // High in the air behind the garden, about as wide as the island.
            Vector3 center = new Vector3(7.5f, 7.6f, 12.0f);
            var root = new GameObject("RainbowEight").transform;
            root.SetParent(transform);
            root.position = center;
            // The camera draws back and rises a little to give the eight the sky; it returns afterwards.
            Vector3 camPos0 = cam.transform.position;
            Quaternion camRot0 = cam.transform.rotation;
            Vector3 camPos1 = new Vector3(7.5f, 13.2f, -12.5f);
            Quaternion camRot1 = Quaternion.LookRotation(new Vector3(7.5f, 6.4f, 9.0f) - camPos1, Vector3.up);
            if (!ReducedMotion) StartCoroutine(MoveCamera(cam, camPos0, camRot0, camPos1, camRot1, 2.6f));
            root.rotation = Quaternion.LookRotation((center - camPos1).normalized, Vector3.up);

            const int points = 180;
            const float baseA = 4.6f;
            var tex = BandTexture();
            var lines = new List<LineRenderer>();
            LineRenderer Band(string name, float width, int queue, float z)
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(root, false);
                obj.transform.localPosition = new Vector3(0f, 0f, z);
                var line = obj.AddComponent<LineRenderer>();
                var m = IslandHarmony.GlowMaterial(tex, Color.white, queue);
                line.material = m;
                line.textureMode = LineTextureMode.Tile;
                line.useWorldSpace = false;
                line.loop = true;
                line.widthMultiplier = width;
                line.numCapVertices = 0;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.positionCount = points;
                return line;
            }
            Vector3 PointOn(float a, int i)
            {
                float t = i / (float)points * Mathf.PI * 2f;
                var p = Eight(t, a);
                return new Vector3(p.x, p.y, 0f);
            }
            // A wide veil of light first, then seven pale overlapping bands.
            var veil = Band("Veil", 2.6f, 3290, 0.06f);
            for (int i = 0; i < points; i++) veil.SetPosition(i, PointOn(baseA, i));
            for (int band = 0; band < Rainbow.Length; band++)
            {
                var line = Band("Band" + band, 0.46f, 3300 + band, -band * 0.004f);
                for (int i = 0; i < points; i++) line.SetPosition(i, PointOn(baseA + (3 - band) * 0.15f, i));
                lines.Add(line);
            }

            void SetAlpha(float k, float time)
            {
                veil.startColor = veil.endColor = new Color(1f, 0.97f, 0.92f, 0.09f * k);
                for (int band = 0; band < lines.Count; band++)
                {
                    float breathe = 0.85f + 0.15f * Mathf.Sin(time * 1.4f + band * 0.7f);
                    lines[band].startColor = lines[band].endColor = Pale(Rainbow[band], 0.30f * k * breathe);
                }
            }

            // Condense out of the air.
            float appear = ReducedMotion ? 0.01f : 3.2f;
            for (float time = 0f; time < appear; time += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, time / appear);
                SetAlpha(k, time);
                root.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, k);
                if (Random.value < 0.5f)
                {
                    _sparkles.transform.position = root.TransformPoint(PointOn(baseA, Random.Range(0, points)));
                    _sparkles.Emit(new ParticleSystem.EmitParams { startColor = Pale(Rainbow[Random.Range(0, 7)], 0.8f) }, 1);
                }
                yield return null;
            }

            // Angels are glimpsed within the light, a few at a time.
            for (int n = 0; n < 24; n++)
            {
                float t = Random.Range(0f, Mathf.PI * 2f);
                var local = Eight(t, baseA);
                var pos = root.TransformPoint(new Vector3(local.x, local.y, 0f));
                var vel = (root.TransformDirection(new Vector3(local.x, 0f, 0f)).normalized * Random.Range(0.12f, 0.35f))
                          + Vector3.up * Random.Range(0.25f, 0.5f);
                _angels.Emit(new ParticleSystem.EmitParams { position = pos, velocity = vel, applyShapeToPosition = false }, 1);
                yield return new WaitForSeconds(0.14f);
            }

            // Breathe, then rise and dissolve.
            float hold = ReducedMotion ? 2f : 3.5f;
            float elapsed = appear;
            for (float time = 0f; time < hold; time += Time.deltaTime)
            {
                elapsed += Time.deltaTime;
                SetAlpha(1f, elapsed);
                yield return null;
            }
            float fade = 3.0f;
            Vector3 start = root.position;
            for (float time = 0f; time < fade; time += Time.deltaTime)
            {
                elapsed += Time.deltaTime;
                float k = 1f - Mathf.SmoothStep(0f, 1f, time / fade);
                SetAlpha(k, elapsed);
                root.position = start + Vector3.up * (time / fade) * 0.8f;
                if (Random.value < 0.7f)
                {
                    _sparkles.transform.position = root.TransformPoint(PointOn(baseA, Random.Range(0, points)));
                    _sparkles.Emit(new ParticleSystem.EmitParams { startColor = new Color(1f, 0.96f, 0.88f, 0.7f) }, 1);
                }
                yield return null;
            }
            Destroy(root.gameObject);
            if (!ReducedMotion) yield return MoveCamera(cam, cam.transform.position, cam.transform.rotation, camPos0, camRot0, 2.4f);
            cam.transform.SetPositionAndRotation(camPos0, camRot0);
        }

        private static IEnumerator MoveCamera(Camera cam, Vector3 p0, Quaternion r0, Vector3 p1, Quaternion r1, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(p0, p1, k), Quaternion.Slerp(r0, r1, k));
                yield return null;
            }
            cam.transform.SetPositionAndRotation(p1, r1);
        }

        // ---------------- Answered prayer: spirits carry the blessing ----------------

        private Texture2D _angelSheet;

        /// <summary>
        /// Small winged lights descend and dissolve into the plants this roll's blessing reached.
        /// Quiet by design: a few at a time, staggered, fading in and out.
        /// </summary>
        public IEnumerator PlayPrayerSpirits(List<Vector3> plantTops)
        {
            if (plantTops == null || plantTops.Count == 0) yield break;
            if (_angelSheet == null) _angelSheet = ProceduralArt.AngelSheet();
            for (int i = 0; i < plantTops.Count; i++)
            {
                StartCoroutine(Spirit(plantTops[i], i * 0.45f));
            }
            yield return new WaitForSeconds(plantTops.Count * 0.45f + 3.2f);
        }

        private IEnumerator Spirit(Vector3 target, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var root = new GameObject("PrayerSpirit").transform;
            root.SetParent(transform);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);
            quad.transform.localScale = Vector3.one * 0.38f;
            quad.AddComponent<Billboard>();
            var mat = new Material(_sprite);
            mat.SetTexture("_MainTex", _angelSheet);
            mat.mainTextureScale = new Vector2(0.5f, 1f);
            mat.renderQueue = 3320;
            var qr = quad.GetComponent<MeshRenderer>();
            qr.material = mat;
            qr.shadowCastingMode = ShadowCastingMode.Off;

            var trail = root.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.9f;
            trail.widthMultiplier = 0.06f;
            trail.material = new Material(_sprite);
            var tg = new Gradient();
            tg.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.88f, 0.55f), 0f), new GradientColorKey(new Color(0.7f, 0.9f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = tg;
            trail.shadowCastingMode = ShadowCastingMode.Off;

            Vector3 start = target + new Vector3(Random.Range(-1.6f, 1.6f), 6.0f + Random.Range(0f, 1.2f), Random.Range(-2.2f, -0.6f));
            Vector3 control = Vector3.Lerp(start, target, 0.5f) + new Vector3(Random.Range(-1.5f, 1.5f), 1.2f, Random.Range(-1f, 1f));
            float flight = ReducedMotion ? 0.6f : Random.Range(2.0f, 2.6f);
            for (float t = 0f; t < flight; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / flight);
                Vector3 a = Vector3.Lerp(start, control, k), b = Vector3.Lerp(control, target, k);
                root.position = Vector3.Lerp(a, b, k);
                float alpha = Mathf.Clamp01(t / 0.5f) * (k > 0.85f ? Mathf.Clamp01((1f - k) / 0.15f) * 0.6f + 0.4f : 1f);
                mat.color = new Color(1f, 1f, 1f, alpha * 0.9f);
                mat.mainTextureOffset = new Vector2(Mathf.Repeat(t * 6f, 1f) < 0.5f ? 0f : 0.5f, 0f);
                yield return null;
            }
            root.position = target;
            _sparkles.transform.position = target;
            _sparkles.Emit(new ParticleSystem.EmitParams { startColor = new Color(1f, 0.92f, 0.65f, 0.9f) }, 14);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                mat.color = new Color(1f, 1f, 1f, (1f - t / 0.6f) * 0.9f);
                quad.transform.localScale = Vector3.one * 0.38f * (1f - t / 0.6f * 0.6f);
                yield return null;
            }
            Destroy(root.gameObject, trail.time);
            quad.SetActive(false);
        }

        // ---------------- Lucky Seven halo around the die ----------------

        private void BuildDiceHalo()
        {
            _diceHalo = new GameObject("LuckyHalo").transform;
            _diceHalo.SetParent(transform);
            var glow = ProceduralArt.GlowSprite();
            for (int i = 0; i < 7; i++)
            {
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.transform.SetParent(_diceHalo, false);
                orb.transform.localScale = Vector3.one * 0.12f;
                Destroy(orb.GetComponent<Collider>());
                var m = new Material(_lit) { color = Rainbow[i] };
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Rainbow[i] * 3.2f);
                orb.GetComponent<MeshRenderer>().material = m;
                orb.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                _diceHaloOrbs.Add(orb.transform);
            }
            _diceHalo.gameObject.SetActive(false);
        }

        public void SetLuckyHalo(bool on)
        {
            if (_diceHalo != null) _diceHalo.gameObject.SetActive(on);
        }

        private void Update()
        {
            float t = Time.time, dt = Time.deltaTime;
            if (!ReducedMotion) UpdateButterflies(t, dt);
            if (_diceHalo != null && _diceHalo.gameObject.activeSelf && View != null && View.DiceTransform != null)
            {
                _diceHalo.position = View.DiceTransform.position;
                for (int i = 0; i < _diceHaloOrbs.Count; i++)
                {
                    float a = i / 7f * Mathf.PI * 2f + t * 1.3f;
                    _diceHaloOrbs[i].localPosition = new Vector3(Mathf.Cos(a) * 0.95f,
                        0.15f + Mathf.Sin(t * 2f + i) * 0.12f, Mathf.Sin(a) * 0.95f);
                }
            }
        }
    }
}
