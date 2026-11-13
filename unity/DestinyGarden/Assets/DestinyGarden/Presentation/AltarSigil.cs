using System;
using System.Collections;
using System.Collections.Generic;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// The prayer star on the altar beneath the die (presentation of saved facts only, prayer-star.v1).
    /// It turns slowly between rolls. When a roll is revealed the old star spins away and the new one
    /// (five, six, seven or eight points) settles in its place; a seven shows the lucky number, an eight
    /// is drawn in the colours of the rainbow. Light then falls on the plants that really received the
    /// star's gift (the saved ecology effects). Nothing here changes the garden.
    /// </summary>
    public sealed class AltarSigil : MonoBehaviour
    {
        public GardenView View;
        public GardenAudio Audio;
        public Font Font;
        public bool ReducedMotion;

        private const float StarRadius = 0.90f;   // a floating circle of light around the altar, at its top
        private const float LineWidth = 0.034f;
        private const float BaseSpin = 9f;           // degrees per second between rolls

        private static readonly Color[] RainbowColors =
        {
            new Color(1f, 0.45f, 0.45f), new Color(1f, 0.68f, 0.38f), new Color(1f, 0.92f, 0.45f),
            new Color(0.55f, 1f, 0.55f), new Color(0.45f, 0.85f, 1f), new Color(0.55f, 0.6f, 1f),
            new Color(0.82f, 0.55f, 1f), new Color(1f, 0.6f, 0.9f)
        };

        private sealed class Layer
        {
            public Transform Root;
            public readonly List<LineRenderer> Lines = new List<LineRenderer>();
            public int Points;
            public float Alpha, Scale = 1f;
        }

        private Material _lineMat;
        private Transform _root;
        private Layer _current, _leaving;
        private LineRenderer _ring;
        private ParticleSystem _glints;
        private float _angle, _boost, _sinceChange = 99f, _flash;
        private int _points;
        private int _qaStar; // QA capture only (-dgStar N)
        private Canvas _labelCanvas;

        private CanvasGroup _labelGroup;
        private Text _labelTitle, _labelSub;
        private Coroutine _labelRoutine;

        /// <summary>The star the last resolved roll left (5 before any roll or for older records).</summary>
        public static int LastStar(List<RoundSummary> history)
        {
            if (history != null)
                for (int i = history.Count - 1; i >= 0; i--)
                    if (history[i].Resolved && history[i].WorldRevision > 0)
                        return history[i].PrayerStar >= 5 ? history[i].PrayerStar : 5;
            return 5;
        }

        public static Color StarColor(int points) =>
            points >= 8 ? new Color(1f, 0.96f, 0.90f)
            : points == 7 ? new Color(1f, 0.80f, 0.40f)
            : points == 6 ? new Color(0.72f, 0.88f, 1f)
            : new Color(1f, 0.91f, 0.70f);

        /// <summary>Unit-radius outline of {n/k} (k = 2 for 5 and 6, 3 for 7 and 8). Six is two triangles.</summary>
        public static List<Vector2[]> StarLoops(int points)
        {
            var loops = new List<Vector2[]>();
            if (points < 5) return loops;
            Vector2 P(int i, int n) { float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / n; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
            if (points == 6)
            {
                loops.Add(new[] { P(0, 6), P(2, 6), P(4, 6) });
                loops.Add(new[] { P(1, 6), P(3, 6), P(5, 6) });
                return loops;
            }
            int step = points == 5 ? 2 : 3;
            var loop = new Vector2[points];
            for (int i = 0; i < points; i++) loop[i] = P(i * step % points, points);
            loops.Add(loop);
            return loops;
        }

        public void Build()
        {
            _lineMat = IslandHarmony.GlowMaterial(null, Color.white, 3006);
            var center = View != null && View.DiceTransform != null
                ? View.DiceTransform.position - Vector3.up * 0.605f  // altar top (die floats 0.92 above the ground, altar top 0.30)
                : Vector3.zero;
            _root = new GameObject("AltarSigil").transform;
            _root.SetParent(transform, false);
            _root.position = center;
            // Lines are drawn in the local XY plane and face the root's Z axis, which points up.
            _root.rotation = Quaternion.Euler(90f, 0f, 0f);

            _ring = NewLine(_root, "SigilRing", 72, true);
            for (int i = 0; i < 72; i++)
            {
                float a = i / 72f * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (StarRadius * 1.08f));
            }

            var gObj = new GameObject("SigilGlints");
            gObj.transform.SetParent(transform, false);
            gObj.transform.position = center;
            _glints = gObj.AddComponent<ParticleSystem>();
            var main = _glints.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 600;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 2.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.10f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = _glints.emission; em.enabled = false;
            var shape = _glints.shape; shape.enabled = false;
            var col = _glints.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            gObj.GetComponent<ParticleSystemRenderer>().material =
                IslandHarmony.GlowMaterial(ProceduralArt.GlowSprite(), Color.white, 3330);

            BuildLabel();
            ShowSaved(5);

            // QA preview only (explicit launch argument, never in normal play): -dgStar N shows star N
            // with its label for a capture. It never writes to the journal.
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-dgStar" && int.TryParse(args[i + 1], out int qa) && qa >= 5 && qa <= 8)
                {
                    _qaStar = qa;
                    ShowSaved(qa);
                }
        }

        private LineRenderer NewLine(Transform parent, string name, int count, bool loop)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var line = obj.AddComponent<LineRenderer>();
            line.material = _lineMat;
            line.useWorldSpace = false;
            line.loop = loop;
            line.alignment = LineAlignment.TransformZ;
            line.widthMultiplier = LineWidth;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = count;
            return line;
        }

        private Layer MakeLayer(int points)
        {
            var layer = new Layer { Points = points };
            layer.Root = new GameObject("Star" + points).transform;
            layer.Root.SetParent(_root, false);
            foreach (var loop in StarLoops(points))
            {
                var line = NewLine(layer.Root, "Loop", loop.Length, true);
                for (int i = 0; i < loop.Length; i++) line.SetPosition(i, (Vector3)(loop[i] * StarRadius));
                if (points >= 8)
                {
                    // The eight carries the rainbow along its outline.
                    var grad = new Gradient();
                    var keys = new GradientColorKey[8];
                    for (int k = 0; k < 8; k++) keys[k] = new GradientColorKey(RainbowColors[k], k / 7f);
                    grad.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                    line.colorGradient = grad;
                }
                layer.Lines.Add(line);
            }
            return layer;
        }

        private void Paint(Layer layer, float alpha)
        {
            if (layer == null) return;
            layer.Root.localScale = Vector3.one * layer.Scale;
            var c = StarColor(layer.Points);
            foreach (var line in layer.Lines)
            {
                if (layer.Points >= 8)
                {
                    // Keep the gradient's colours; only the material carries the alpha.
                    line.startColor = new Color(1f, 1f, 1f, alpha);
                    line.endColor = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    line.startColor = new Color(c.r, c.g, c.b, alpha);
                    line.endColor = new Color(c.r, c.g, c.b, alpha);
                }
            }
        }

        /// <summary>Shows a saved star at once (start of play, restore). No sound, no label.</summary>
        public void ShowSaved(int points)
        {
            points = Mathf.Clamp(_qaStar > 0 ? _qaStar : points, 5, 8);
            if (_leaving != null) { Destroy(_leaving.Root.gameObject); _leaving = null; }
            if (_current != null) Destroy(_current.Root.gameObject);
            _current = MakeLayer(points);
            _current.Alpha = 1f;
            _points = points;
            _sinceChange = 99f;
            _boost = 0f;
        }

        private void Change(int points)
        {
            if (_leaving != null) Destroy(_leaving.Root.gameObject);
            _leaving = _current;
            _current = MakeLayer(points);
            _current.Alpha = 0f;
            _points = points;
            _sinceChange = 0f;
            _boost = ReducedMotion ? 0f : 420f;
        }

        /// <summary>
        /// Reveals this roll's star: the face's note, the turn into the new star, its chord, the lucky
        /// number label, and light falling on the plants that received the gift.
        /// </summary>
        public IEnumerator PlayReveal(RollOutcome outcome, WorldState state)
        {
            var star = outcome?.Ecology?.PrayerStar;
            if (star == null) yield break;
            int previousFaceId = star.FaceSum > 0 ? star.FaceSum - outcome.FaceId : 0;
            if (Audio != null) Audio.PlayFaceNote(outcome.FaceId);
            yield return new WaitForSeconds(0.45f);
            Change(star.Points);
            yield return new WaitForSeconds(0.55f);
            _flash = star.Points >= 7 ? 1f : star.Points == 6 ? 0.5f : 0.25f;
            if (Audio != null) Audio.PlayStarChord(star.Points);
            EmitBurst(star.Points);
            if (star.Points >= 7)
            {
                string sub = star.Points >= 8 ? "ラッキーナンバーに、陰陽と島の調和が重なった"
                    : outcome.FaceId == LuckySeven.FaceId ? "七の目　・　偶然なら 1/7"
                    : $"前の目 {previousFaceId} と今の目 {outcome.FaceId} で七　・　偶然なら 1/6";
                ShowLabel(star.Points, sub, false);
            }
            yield return new WaitForSeconds(0.5f);
            // The gift: light falls on each plant that the star actually nourished.
            var tops = new List<Vector3>();
            foreach (var e in outcome.Ecology.Effects)
            {
                if (e.Kind != PrayerStar.GiftKind || e.Amount <= 0) continue;
                var plant = state != null ? state.PlantAt(e.CellId) : null;
                float h = plant != null ? 0.12f + 0.75f * plant.Growth / 2500f : 0.15f;
                tops.Add(View.CellTop(e.CellId) + Vector3.up * h);
            }
            int perPlant = star.Points >= 8 ? 14 : star.Points == 7 ? 10 : star.Points == 6 ? 5 : 3;
            for (int i = 0; i < tops.Count; i++)
            {
                Shower(tops[i], star.Points, perPlant);
                if (!ReducedMotion) yield return new WaitForSeconds(star.Points >= 8 ? 0.05f : 0.09f);
            }
        }

        private Color GlintColor(int points) =>
            points >= 8 ? RainbowColors[UnityEngine.Random.Range(0, RainbowColors.Length)] : StarColor(points);

        private void EmitBurst(int points)
        {
            if (_glints == null) return;
            int n = points >= 8 ? 90 : points == 7 ? 60 : points == 6 ? 22 : 10;
            var c = _root.position;
            for (int i = 0; i < n; i++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                float r = StarRadius * UnityEngine.Random.Range(0.3f, 1.1f);
                var pos = c + new Vector3(Mathf.Cos(a) * r, 0.02f, Mathf.Sin(a) * r);
                var vel = ReducedMotion ? Vector3.zero
                    : new Vector3(Mathf.Cos(a) * 0.12f, UnityEngine.Random.Range(0.25f, points >= 7 ? 0.9f : 0.45f), Mathf.Sin(a) * 0.12f);
                var col = GlintColor(points);
                _glints.Emit(new ParticleSystem.EmitParams
                {
                    position = pos, velocity = vel, startColor = new Color(col.r, col.g, col.b, 0.9f),
                    startLifetime = UnityEngine.Random.Range(1.0f, 2.2f)
                }, 1);
            }
        }

        private void Shower(Vector3 top, int points, int count)
        {
            if (_glints == null) return;
            for (int i = 0; i < count; i++)
            {
                var from = top + new Vector3(UnityEngine.Random.Range(-0.18f, 0.18f), UnityEngine.Random.Range(0.9f, 1.6f),
                    UnityEngine.Random.Range(-0.18f, 0.18f));
                float life = UnityEngine.Random.Range(1.1f, 1.6f);
                var vel = ReducedMotion ? Vector3.zero : (top - from) / life;
                var col = GlintColor(points);
                _glints.Emit(new ParticleSystem.EmitParams
                {
                    position = ReducedMotion ? top + Vector3.up * 0.1f : from, velocity = vel,
                    startColor = new Color(col.r, col.g, col.b, 0.85f), startLifetime = life
                }, 1);
            }
        }

        // ------------------------------------------------------------------ label

        private void BuildLabel()
        {
            var canvasObj = new GameObject("SigilLabelCanvas");
            canvasObj.transform.SetParent(transform, false);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            _labelCanvas = canvas;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            _labelGroup = canvasObj.AddComponent<CanvasGroup>();
            _labelGroup.alpha = 0f;
            _labelGroup.blocksRaycasts = false;
            _labelGroup.interactable = false;
            _labelTitle = LabelText(canvasObj.transform, new Vector2(0f, 120f), 44);
            _labelSub = LabelText(canvasObj.transform, new Vector2(0f, 72f), 19);
        }

        private Text LabelText(Transform parent, Vector2 pos, int size)
        {
            var obj = new GameObject("SigilLabel");
            obj.transform.SetParent(parent, false);
            var t = obj.AddComponent<Text>();
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = size;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = obj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.02f, 0.04f, 0.06f, 0.65f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(1000f, 60f);
            return t;
        }

        private void ShowLabel(int points, string sub, bool hold)
        {
            if (_labelGroup == null) return;
            var font = Font;
            if (font == null)
            {
                var ui = FindFirstObjectByType<GardenUi>();
                if (ui != null) font = ui.UiFont;
            }
            if (font != null) { _labelTitle.font = font; _labelSub.font = font; }
            _labelTitle.text = points >= 8 ? "八芒星　・　八の虹" : "ラッキーナンバー　７";
            _labelTitle.color = points >= 8 ? new Color(1f, 0.96f, 0.88f) : new Color(1f, 0.86f, 0.52f);
            _labelSub.text = sub ?? "";
            _labelSub.color = new Color(0.92f, 0.95f, 0.93f, 0.9f);
            if (_labelRoutine != null) StopCoroutine(_labelRoutine);
            _labelRoutine = StartCoroutine(FadeLabel(hold));
        }

        private IEnumerator FadeLabel(bool hold)
        {
            for (float t = 0f; t < 0.8f; t += Time.deltaTime) { _labelGroup.alpha = t / 0.8f; yield return null; }
            _labelGroup.alpha = 1f;
            if (hold) yield break;
            yield return new WaitForSeconds(3.2f);
            for (float t = 0f; t < 1.4f; t += Time.deltaTime) { _labelGroup.alpha = 1f - t / 1.4f; yield return null; }
            _labelGroup.alpha = 0f;
        }

        // ------------------------------------------------------------------ motion

        private void Update()
        {
            if (_root == null || _current == null) return;
            if (_qaStar > 0 && _labelCanvas != null && _labelCanvas.renderMode == RenderMode.ScreenSpaceOverlay && Camera.main != null)
            {
                // QA capture renders the camera into a texture, which an overlay canvas skips.
                _labelCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                _labelCanvas.worldCamera = Camera.main;
                _labelCanvas.planeDistance = 4f;
                if (_qaStar >= 7)
                    ShowLabel(_qaStar, _qaStar == 7 ? "前の目 3 と今の目 4 で七　・　偶然なら 1/6"
                        : "ラッキーナンバーに、陰陽と島の調和が重なった", true);
            }
            float dt = Time.deltaTime;
            _sinceChange += dt;
            _boost *= Mathf.Exp(-dt * 1.8f);
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 0.45f);
            float spin = ReducedMotion ? 0f : BaseSpin + _boost;
            _angle = (_angle + spin * dt) % 360f;

            float breathe = 0.82f + 0.18f * Mathf.Sin(Time.time * 1.2f);
            float strength = _points >= 7 ? 0.75f : 0.55f;

            // The new star settles in, larger at first, and turns with the old one leaving.
            float inK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 1.1f, _sinceChange));
            _current.Scale = Mathf.Lerp(1.28f, 1f, inK);
            _current.Alpha = inK;
            _current.Root.localRotation = Quaternion.Euler(0f, 0f, _angle);
            Paint(_current, (strength + 0.35f * _flash) * breathe * _current.Alpha);

            if (_leaving != null)
            {
                float outK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.7f, _sinceChange));
                _leaving.Scale = Mathf.Lerp(1f, 0.55f, outK);
                _leaving.Root.localRotation = Quaternion.Euler(0f, 0f, -_angle * 1.6f);
                Paint(_leaving, strength * breathe * (1f - outK));
                if (outK >= 1f) { Destroy(_leaving.Root.gameObject); _leaving = null; }
            }

            var rc = StarColor(_points);
            float ringA = (0.22f + 0.30f * _flash) * breathe;
            _ring.startColor = _ring.endColor = new Color(rc.r, rc.g, rc.b, ringA);
        }
    }
}
