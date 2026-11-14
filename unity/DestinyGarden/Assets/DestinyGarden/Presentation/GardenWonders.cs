using System.Collections;
using System.Collections.Generic;
using DestinyGarden.Domain;
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
        private Shader _lit;
        private LifeModels _models;
        private SpiritModels _spirits;
        private Material _trailPaint;
        private MaterialPropertyBlock _figureTint;
        private static readonly int TintId = Shader.PropertyToID("_Color");
        private readonly List<Butterfly> _butterflies = new List<Butterfly>();
        private readonly List<Vector3> _flowerSpots = new List<Vector3>();
        private bool _butterfliesActive;
        private SpiritModels.Figure[] _angels, _fairies;
        private bool _rainbowPlaying;
        private bool _wasReducedMotion;
        private ParticleSystem _sparkles;
        private Transform _diceHalo;
        private readonly List<Transform> _diceHaloOrbs = new List<Transform>();

        public void Build()
        {
            _figureTint = new MaterialPropertyBlock();
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            _models = new LifeModels();
            _spirits = new SpiritModels(_models);
            BuildButterflies();
            BuildFigures();
            BuildSparkles();
            BuildDiceHalo();
        }

        // ---------------- Butterflies ----------------

        private sealed class Butterfly
        {
            public LifeModels.Winged Rig;
            public Transform Root => Rig.Root;
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
                (new Color(1.00f, 0.70f, 0.88f), new Color(0.62f, 0.30f, 0.85f))
            };
            for (int i = 0; i < palettes.Length; i++)
            {
                var rig = _models.MakeInsect(transform, LifeModels.Insect.Butterfly, i, palettes[i].Item1, palettes[i].Item2);
                rig.Root.localScale = Vector3.one * (0.66f + i * 0.07f);
                rig.Flap(16f, 12f);
                _butterflies.Add(new Butterfly { Rig = rig, Phase = i * 1.3f });
            }
        }

        /// <summary>Called after every refresh with the domain's NatureBalance verdict.</summary>
        public void SetButterflies(bool active, IEnumerable<Vector3> flowerTops)
        {
            _flowerSpots.Clear();
            if (flowerTops != null) _flowerSpots.AddRange(flowerTops);
            if (active == _butterfliesActive)
            {
                if (active && ReducedMotion) PoseButterflies();
                return;
            }
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
                else if (ReducedMotion) b.Root.gameObject.SetActive(false);
                else b.Leaving = true;
            }
            if (active && ReducedMotion) PoseButterflies();
        }

        private void PoseButterflies()
        {
            for (int i = 0; i < _butterflies.Count; i++)
            {
                var butterfly = _butterflies[i];
                if (!_butterfliesActive)
                {
                    butterfly.Root.gameObject.SetActive(false);
                    continue;
                }
                if (_flowerSpots.Count == 0) continue;
                float angle = i * 2.39996f;
                butterfly.Root.SetPositionAndRotation(_flowerSpots[i % _flowerSpots.Count] +
                    new Vector3(Mathf.Cos(angle) * 0.34f, 0.14f, Mathf.Sin(angle) * 0.34f), Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f));
                butterfly.Rig.Flap(29f, 22f, 8f);
                butterfly.Velocity = Vector3.zero;
                butterfly.Retarget = 0f;
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
                    b.Retarget = t + Random.Range(4.0f, 6.5f);
                    if (_flowerSpots.Count > 0 && Random.value < 0.7f)
                    {
                        b.Target = _flowerSpots[Random.Range(0, _flowerSpots.Count)] +
                            new Vector3(Mathf.Cos(b.Phase) * 0.20f, 0.10f, Mathf.Sin(b.Phase) * 0.20f);
                        b.RestUntil = b.Retarget - 0.25f;
                    }
                    else
                    {
                        b.Target = new Vector3(Random.Range(2f, 13f), Random.Range(1.2f, 2.8f), Random.Range(2f, 13f));
                        b.RestUntil = 0f;
                    }
                }
                Vector3 to = b.Target - b.Root.position;
                bool resting = !b.Leaving && to.sqrMagnitude < 0.075f && t < b.RestUntil;
                Vector3 wobble = new Vector3(Mathf.Sin(t * 3.1f + b.Phase), Mathf.Sin(t * 4.3f + b.Phase * 2f) * 0.6f,
                    Mathf.Cos(t * 2.7f + b.Phase)) * (resting ? 0f : 0.42f + Mathf.Clamp01(to.magnitude * 0.4f) * 0.32f);
                Vector3 desired = to.normalized * Mathf.Min(1.6f, to.magnitude * 1.4f) + wobble;
                b.Velocity = Vector3.Lerp(b.Velocity, resting ? Vector3.zero : desired, dt * 2.2f);
                b.Root.position += b.Velocity * dt;
                Vector3 flat = new Vector3(b.Velocity.x, 0f, b.Velocity.z);
                if (flat.sqrMagnitude > 0.0004f)
                    b.Root.rotation = Quaternion.Slerp(b.Root.rotation, Quaternion.LookRotation(flat), dt * 5f);
                float beat = t * 19f + b.Phase;
                float flap = resting ? 29f + Mathf.Sin(t * 2.2f + b.Phase) * 19f : 15f + Mathf.Sin(beat) * 58f;
                float hind = resting ? flap * 0.88f : 12f + Mathf.Sin(beat - 0.35f) * 49f;
                b.Rig.Flap(flap, hind, resting ? 8f : Mathf.Sin(beat * 0.5f) * 7f);
            }
        }

        // ---------------- Rainbow figure-eight and angels ----------------

        private void BuildFigures()
        {
            _angels = new SpiritModels.Figure[3];
            _fairies = new SpiritModels.Figure[PrayerSpirits.MaxSpirits * 2];
            for (int i = 0; i < _angels.Length; i++)
            {
                _angels[i] = _spirits.Make(transform, false);
                _angels[i].Root.gameObject.SetActive(false);
            }
            // Fairy objects are created only as a prayer needs them, then returned to the pool.
            _trailPaint = _models.Keep(IslandHarmony.GlowMaterial(_models.Keep(ProceduralArt.GlowSprite()), Color.white, 3318));
        }

        private void BuildSparkles()
        {
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
            var fade = _sparkles.colorOverLifetime;
            fade.enabled = true;
            var opacity = new Gradient();
            opacity.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            fade.color = opacity;
            sObj.GetComponent<ParticleSystemRenderer>().sharedMaterial = _models.Paint(_models.Keep(ProceduralArt.GlowSprite()), 3250);
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
            if (cam == null) yield break;
            while (_rainbowPlaying) yield return null;
            _rainbowPlaying = true;
            bool motion = !ReducedMotion;
            // Frame the figures against open sky beside the rainbow.
            Vector3 center = new Vector3(7.5f, 7.6f, 12.0f);
            var root = new GameObject("RainbowEight").transform;
            root.SetParent(transform);
            root.position = center;
            // The camera draws back and rises a little to give the eight the sky; it returns afterwards.
            Vector3 camPos0 = cam.transform.position;
            Quaternion camRot0 = cam.transform.rotation;
            Vector3 camPos1 = new Vector3(7.5f, 13.2f, -12.5f);
            Quaternion camRot1 = Quaternion.LookRotation(new Vector3(7.5f, 6.4f, 9.0f) - camPos1, Vector3.up);
            if (View != null && View.Rig != null) View.Rig.Lock();
            if (View != null) View.SetRainbowFocus(true);
            if (motion) StartCoroutine(MoveCamera(cam, camPos0, camRot0, camPos1, camRot1, 2.6f));
            root.rotation = Quaternion.LookRotation((center - camPos1).normalized, Vector3.up);

            const int points = 180;
            const float baseA = 4.6f;
            var tex = BandTexture();
            var lines = new List<LineRenderer>();
            var materials = new List<Material>();
            LineRenderer Band(string name, float width, int queue, float z)
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(root, false);
                obj.transform.localPosition = new Vector3(0f, 0f, z);
                var line = obj.AddComponent<LineRenderer>();
                var m = IslandHarmony.GlowMaterial(tex, Color.white, queue);
                line.sharedMaterial = m;
                materials.Add(m);
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
                    float breathe = motion && !ReducedMotion ? 0.90f + 0.10f * Mathf.Sin(time * 1.4f + band * 0.7f) : 0.95f;
                    lines[band].startColor = lines[band].endColor = Pale(Rainbow[band], 0.30f * k * breathe);
                }
            }

            // Leave breathing room around the eight so the silhouettes and the two loops stay legible.
            float appear = motion ? 2.65f : 0.55f;
            float hold = motion ? 5.6f : 2.6f;
            float fade = motion ? 2.9f : 0.9f;
            float nextSparkle = 0.2f;
            for (float time = 0f; time < appear + hold + fade; time += Time.deltaTime)
            {
                bool animate = motion && !ReducedMotion;
                float inFade = Mathf.SmoothStep(0f, 1f, time / appear);
                float outFade = Mathf.SmoothStep(0f, 1f, (time - appear - hold) / fade);
                SetAlpha(inFade * (1f - outFade), time);
                root.localScale = Vector3.one * (animate ? Mathf.Lerp(0.94f, 1f, inFade) : 1f);
                root.position = center + Vector3.up * (animate ? outFade * 0.68f : 0f);
                for (int i = 0; i < _angels.Length; i++)
                {
                    float life = time - (motion ? 1.75f + i * 0.56f : 0.23f);
                    float alpha = Mathf.SmoothStep(0f, 1f, life / 0.78f) * (1f - outFade);
                    var angel = _angels[i];
                    if (!angel.Root.gameObject.activeSelf && alpha > 0f) angel.Root.gameObject.SetActive(true);
                    if (!angel.Root.gameObject.activeSelf) continue;
                    float rise = animate ? Mathf.Clamp01(life / (hold + 1.4f)) : 0.42f;
                    float x = i == 1 ? 2.96f + rise * 0.62f : (i == 0 ? -3.35f + rise * 0.24f : -2.54f - rise * 0.53f);
                    float y = (i == 0 ? -1.15f : i == 1 ? 0.32f : 1.86f) + rise * (i == 2 ? 0.68f : 1.52f);
                    var at = root.TransformPoint(new Vector3(x, y, -0.28f));
                    angel.Root.position = at;
                    angel.Root.rotation = FacingCamera(cam, at) * Quaternion.Euler(0f, i == 1 ? 13f : -15f,
                        animate ? Mathf.Sin(life * 1.32f + i) * 5.5f : 0f);
                    angel.Root.localScale = Vector3.one * (i == 0 ? 0.97f : i == 1 ? 0.84f : 0.73f);
                    float stroke = animate ? 10f + Mathf.Sin(life * 2.8f + i * 1.37f) * 20f : 12f;
                    angel.Wings.Flap(stroke, 0f, animate ? Mathf.Sin(life * 2.8f) * 9f : 6f);
                    SetFigureOpacity(angel, alpha * (i == 2 ? 0.80f : 0.94f), 0.19f);
                }
                if (animate && time > nextSparkle)
                {
                    nextSparkle = time + 0.22f;
                    _sparkles.transform.position = root.TransformPoint(PointOn(baseA, Random.Range(0, points)));
                    _sparkles.Emit(new ParticleSystem.EmitParams { startColor = Pale(Rainbow[Random.Range(0, Rainbow.Length)], 0.60f) }, 1);
                }
                yield return null;
            }
            foreach (var angel in _angels) angel.Root.gameObject.SetActive(false);
            foreach (var material in materials) Destroy(material);
            Destroy(root.gameObject);
            if (motion) yield return MoveCamera(cam, cam.transform.position, cam.transform.rotation, camPos0, camRot0, 2.4f);
            cam.transform.SetPositionAndRotation(camPos0, camRot0);
            if (View != null) View.SetRainbowFocus(false);
            if (View != null && View.Rig != null) View.Rig.Unlock();
            _rainbowPlaying = false;
        }

        private static Quaternion FacingCamera(Camera cam, Vector3 at) =>
            cam != null ? Quaternion.LookRotation(cam.transform.position - at, Vector3.up) : Quaternion.identity;

        private void SetFigureOpacity(SpiritModels.Figure figure, float alpha, float glow)
        {
            void Tint(Renderer renderer, float opacity)
            {
                if (renderer == null) return;
                _figureTint.SetColor(TintId, new Color(1f, 1f, 1f, opacity));
                renderer.SetPropertyBlock(_figureTint);
            }
            Tint(figure.Body, alpha);
            Tint(figure.Aura, alpha * glow);
            Tint(figure.Wings.Left, alpha);
            Tint(figure.Wings.Right, alpha);
            if (figure.Wings.LeftHind == null) return;
            Tint(figure.Wings.LeftHind, alpha);
            Tint(figure.Wings.RightHind, alpha);
        }

        private IEnumerator MoveCamera(Camera cam, Vector3 p0, Quaternion r0, Vector3 p1, Quaternion r1, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (ReducedMotion) yield break;
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(p0, p1, k), Quaternion.Slerp(r0, r1, k));
                yield return null;
            }
            cam.transform.SetPositionAndRotation(p1, r1);
        }

        // ---------------- Answered prayer: spirits carry the blessing ----------------

        /// <summary>Fairies carry the light down to the plants reached by the saved blessing.</summary>
        public IEnumerator PlayPrayerSpirits(List<Vector3> plantTops)
        {
            if (plantTops == null || plantTops.Count == 0) yield break;
            int count = Mathf.Min(PrayerSpirits.MaxSpirits, plantTops.Count);
            for (int i = 0; i < count; i++)
                StartCoroutine(Spirit(plantTops[i], i * 0.31f, i));
            yield return new WaitForSeconds(count * 0.31f + 3.2f);
        }

        private (SpiritModels.Figure Figure, TrailRenderer Trail) BorrowFairy()
        {
            for (int i = 0; i < _fairies.Length; i++)
            {
                var figure = _fairies[i];
                if (figure != null && figure.Root.gameObject.activeSelf) continue;
                if (figure == null)
                {
                    _fairies[i] = figure = _spirits.Make(transform, true);
                    var newTrail = figure.Root.gameObject.AddComponent<TrailRenderer>();
                    newTrail.time = 0.46f;
                    newTrail.widthMultiplier = 0.042f;
                    newTrail.sharedMaterial = _trailPaint;
                    newTrail.minVertexDistance = 0.075f;
                    newTrail.numCapVertices = 3;
                    newTrail.shadowCastingMode = ShadowCastingMode.Off;
                    var tint = new Gradient();
                    tint.SetKeys(new[] { new GradientColorKey(new Color(0.81f, 0.94f, 0.83f), 0f), new GradientColorKey(new Color(0.75f, 0.67f, 0.92f), 1f) },
                        new[] { new GradientAlphaKey(0.51f, 0f), new GradientAlphaKey(0f, 1f) });
                    newTrail.colorGradient = tint;
                }
                var trail = figure.Root.GetComponent<TrailRenderer>();
                trail.emitting = false;
                trail.Clear();
                figure.Root.gameObject.SetActive(true);
                return (figure, trail);
            }
            return (null, null);
        }

        private IEnumerator Spirit(Vector3 target, float delay, int index)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var (fairy, trail) = BorrowFairy();
            if (fairy == null) yield break;
            var root = fairy.Root;
            var cam = View != null ? View.ViewCamera : Camera.main;
            bool motion = !ReducedMotion;
            float side = index % 2 == 0 ? -1f : 1f;
            Vector3 start = target + new Vector3(side * (0.80f + index * 0.13f), 3.20f + index % 3 * 0.21f, -0.94f);
            Vector3 control = target + new Vector3(-side * 0.99f, 1.35f, -0.58f);
            root.position = motion ? start : target + Vector3.up * 0.27f;
            root.localScale = Vector3.one * (0.88f + index % 3 * 0.065f);
            SetFigureOpacity(fairy, 0f, 0.30f);
            trail.Clear();
            trail.emitting = motion;
            float flight = motion ? 2.18f + index % 3 * 0.11f : 1.45f;
            for (float t = 0f; t < flight; t += Time.deltaTime)
            {
                if (motion && ReducedMotion)
                {
                    motion = false;
                    trail.emitting = false;
                    trail.Clear();
                    root.position = target + Vector3.up * 0.27f;
                }
                float k = Mathf.SmoothStep(0f, 1f, t / flight);
                if (motion)
                {
                    var a = Vector3.Lerp(start, control, k);
                    var b = Vector3.Lerp(control, target, k);
                    root.position = Vector3.Lerp(a, b, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.19f;
                }
                root.rotation = FacingCamera(cam, root.position) * Quaternion.Euler(0f, 17f * side, motion ? -side * Mathf.Sin(k * Mathf.PI) * 15f : 0f);
                float beat = t * 24f + index * 1.71f;
                fairy.Wings.Flap(motion ? 12f + Mathf.Sin(beat) * 31f : 20f,
                    motion ? 8f + Mathf.Sin(beat - 0.68f) * 27f : 11f, 13f);
                SetFigureOpacity(fairy, Mathf.SmoothStep(0f, 1f, t / 0.42f) * 0.96f, 0.32f);
                yield return null;
            }
            trail.emitting = false;
            _sparkles.transform.position = target;
            if (motion) _sparkles.Emit(new ParticleSystem.EmitParams { startColor = new Color(0.92f, 0.94f, 0.73f, 0.75f) }, 4);
            for (float t = 0f; t < 0.55f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.55f);
                SetFigureOpacity(fairy, (1f - k) * 0.96f, 0.32f);
                if (motion) root.localScale = Vector3.one * Mathf.Lerp(0.92f, 0.55f, k);
                yield return null;
            }
            trail.Clear();
            root.gameObject.SetActive(false);
        }

        // ---------------- Lucky Seven halo around the die ----------------

        private void BuildDiceHalo()
        {
            _diceHalo = new GameObject("LuckyHalo").transform;
            _diceHalo.SetParent(transform);
            for (int i = 0; i < 7; i++)
            {
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.transform.SetParent(_diceHalo, false);
                orb.transform.localScale = Vector3.one * 0.12f;
                Destroy(orb.GetComponent<Collider>());
                var m = _models.Keep(new Material(_lit) { color = Rainbow[i] });
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Rainbow[i] * 3.2f);
                orb.GetComponent<MeshRenderer>().sharedMaterial = m;
                orb.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                _diceHaloOrbs.Add(orb.transform);
            }
            _diceHalo.gameObject.SetActive(false);
        }

        public void SetLuckyHalo(bool on)
        {
            if (_diceHalo != null) _diceHalo.gameObject.SetActive(on);
        }

        private void OnDestroy() => _models?.Dispose();

        private void Update()
        {
            if (_models == null) return;
            float t = ReducedMotion ? 0f : Time.time, dt = Time.deltaTime;
            if (ReducedMotion && !_wasReducedMotion) PoseButterflies();
            if (!ReducedMotion) UpdateButterflies(t, dt);
            _wasReducedMotion = ReducedMotion;
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
