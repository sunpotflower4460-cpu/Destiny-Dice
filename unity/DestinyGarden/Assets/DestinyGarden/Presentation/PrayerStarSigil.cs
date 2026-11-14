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
    /// The prayer star on the sea beneath the island (presentation of saved facts only, prayer-star.v1).
    /// It is geared to the die on the altar: it turns as the die turns, whirls while the die tumbles,
    /// and each answer runs from the die down to the star as a thread of light. When a roll is revealed the old star spins away and the new one
    /// (five, six, seven or eight points) settles in its place; a seven shows the lucky number, an eight
    /// is drawn in the colours of the rainbow. Light then falls on the plants that really received the
    /// star's gift (the saved ecology effects). Nothing here changes the garden.
    /// </summary>
    public sealed class PrayerStarSigil : MonoBehaviour
    {
        public GardenView View;
        public GardenAudio Audio;
        public GardenCoordinator Coordinator;   // read only: the standing line (season's wish, lucky count, next lucky face)
        private Text _standingText;
        private Image _standingBacking;
        private CanvasGroup _standingGroup;
        private GardenUi _ui;
        private string _standingKey;
        public Font Font;
        public bool ReducedMotion;

        // The star lies on the sea under the island, on the circle of the seven lights (SacredAtmosphere).
        private static readonly Vector3 IslandCenter = new Vector3(7.5f, 0f, 7.5f);
        private const float SeaLevel = -0.80f;
        public const float StarRadius = 13.4f;
        private const float LineWidth = 0.075f;
        public const float Gear = 0.35f;             // star turn per die turn (yaw), between rolls
        private const float TumbleSpin = 0.6f;       // star turn per degree of die tumble during a roll

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
            public Material NodeMat;   // the points of light on the star's vertices
            public int Points;
            public float Alpha, Scale = 1f;
        }

        private Material _lineMat;
        private Texture2D _nodeTex;
        private Transform _root;
        private Layer _current, _leaving;
        private LineRenderer _ring;
        private ParticleSystem _glints;
        private float _angle, _boost, _sinceChange = 99f, _flash;
        private Quaternion _lastDiceRot = Quaternion.identity;
        private bool _haveDiceRot;
        private LineRenderer _thread;

        // The seven lanterns on the sea (SacredAtmosphere's "SevenLights") come to rest on the star's points.
        private Transform _lanternRing;
        private readonly List<Transform> _lanterns = new List<Transform>();
        private readonly List<Transform> _lanternRefl = new List<Transform>();
        private readonly List<Vector3> _lanternScale = new List<Vector3>(), _reflScale = new List<Vector3>();
        private readonly List<float> _lanternDeg = new List<float>(), _lanternShow = new List<float>();
        private int[] _lanternVertex = new int[0];
        private int _assignedPoints = -1;
        private float _lanternRadius = StarRadius;
        private bool _lanternsSnapped;
        private int _points;
        private int _qaStar; // QA capture only (-dgStar N)
        private bool _qaCameraCanvas; // QA only: render the label canvas through the camera
        private readonly List<Transform> _hintOrbs = new List<Transform>();
        private Material _hintMat;
        private Transform _hintRoot;
        private float _hintAlpha, _hintTarget;
        private Canvas _labelCanvas;

        private CanvasGroup _labelGroup;
        private Text _labelTitle, _labelSub, _labelNumeral, _labelCounter;
        private RectTransform _labelRoot;
        private Image _labelBacking;
        private readonly List<float> _lanternFlare = new List<float>();
        public const string Gold = "#FFD27A";      // the colour of the words ラッキーナンバー
        private const float LuckyStep = 0.14f;      // one lantern (and one note) after another
        private CanvasGroup _causeGroup;
        private Canvas _causeCanvas;
        private readonly List<Vector3> _communityTops = new List<Vector3>();
        private Image _causeBacking;
        private Text _causeText;
        private Coroutine _causeRoutine;
        private Coroutine _labelRoutine;
        // The reveal in progress and what it set going: a new roll's reveal replaces them (never two at once).
        private readonly List<Coroutine> _revealRoutines = new List<Coroutine>();
        private int _standingRev = -1, _standingCount = -1;
        private CoordinatorPhase _standingPhase;
        private GradientColorKey[] _rainbowKeys;
        private readonly GradientAlphaKey[] _alphaKeys = { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
        private readonly Gradient _paintGradient = new Gradient();
        private readonly Gradient _threadGradient = new Gradient();
        private const int GlintBudget = 420; // glints a reveal may send to the plants (the system holds 1200)

        /// <summary>The face of the last resolved roll (0 before any roll).</summary>
        public static int LastFace(List<RoundSummary> history)
        {
            if (history != null)
                for (int i = history.Count - 1; i >= 0; i--)
                    if (history[i].Resolved && history[i].WorldRevision > 0)
                        return history[i].FaceId ?? 0;
            return 0;
        }

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
            _nodeTex = ProceduralArt.GlowSprite();
            // Just above the sea, where the island's harmony star used to be drawn.
            var center = IslandCenter + new Vector3(0f, SeaLevel + 0.05f, 0f);
            _root = new GameObject("PrayerStarSigil").transform;
            _root.SetParent(transform, false);
            _root.position = center;
            // Lines are drawn in the local XY plane and face the root's Z axis, which points up.
            _root.rotation = Quaternion.Euler(90f, 0f, 0f);

            _ring = NewLine(_root, "SigilRing", 72, true);
            for (int i = 0; i < 72; i++)
            {
                float a = i / 72f * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * StarRadius);
            }

            var gObj = new GameObject("SigilGlints");
            gObj.transform.SetParent(transform, false);
            gObj.transform.position = center;
            _glints = gObj.AddComponent<ParticleSystem>();
            var main = _glints.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 1200;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 2.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
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

            _hintRoot = new GameObject("SevenHint").transform;
            _hintRoot.SetParent(transform, false);
            _hintRoot.position = center + Vector3.up * 0.02f;

            _thread = NewLine(transform, "DieToStarThread", 24, false);
            _thread.alignment = LineAlignment.View;
            _thread.useWorldSpace = true;
            _thread.widthMultiplier = 0.05f;
            _thread.enabled = false;
            _hintMat = IslandHarmony.GlowMaterial(ProceduralArt.GlowSprite(), new Color(1f, 0.9f, 0.62f, 0f), 3007);

            BuildLabel();
            ShowSaved(5);

            // QA preview only (explicit launch argument, never in normal play): -dgStar N shows star N
            // with its label for a capture. It never writes to the journal.
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-dgStar" && int.TryParse(args[i + 1], out int qa) && qa >= 5 && qa <= 8)
                {
                    _qaStar = qa;
                    _qaCameraCanvas = true;
                    ShowSaved(qa);
                }
                if (args[i] == "-dgRevealFrames") StartCoroutine(QaFilmstrip(args[i + 1], args));
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
                    if (_rainbowKeys == null)
                    {
                        _rainbowKeys = new GradientColorKey[8];
                        for (int k = 0; k < 8; k++) _rainbowKeys[k] = new GradientColorKey(RainbowColors[k], k / 7f);
                    }
                    _paintGradient.SetKeys(_rainbowKeys, _alphaKeys);
                    line.colorGradient = _paintGradient;
                }
                layer.Lines.Add(line);
            }
            // A point of light on every vertex, on the water (the lanterns come to rest above them).
            layer.NodeMat = IslandHarmony.GlowMaterial(_nodeTex, Color.white, 3007);
            for (int v = 0; v < points; v++)
            {
                float a = Mathf.PI * 0.5f + v * Mathf.PI * 2f / points;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "StarPoint";
                q.transform.SetParent(layer.Root, false);
                q.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * StarRadius;
                q.transform.localScale = Vector3.one * 0.85f;
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = layer.NodeMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return layer;
        }

        private void Paint(Layer layer, float alpha)
        {
            if (layer == null) return;
            layer.Root.localScale = Vector3.one * layer.Scale;
            var c = StarColor(layer.Points);
            if (layer.NodeMat != null)
            {
                var nc = layer.Points >= 8 ? new Color(1f, 0.95f, 0.9f) : layer.Points == 6 ? new Color(0.85f, 0.88f, 0.95f) : c;
                layer.NodeMat.color = new Color(nc.r, nc.g, nc.b, Mathf.Clamp01(alpha * 1.6f));
            }
            foreach (var line in layer.Lines)
            {
                if (layer.Points >= 8)
                {
                    // Keep the rainbow's colours and fade only its alpha (startColor/endColor would replace the
                    // gradient with a plain white line).
                    _alphaKeys[0].alpha = alpha; _alphaKeys[1].alpha = alpha;
                    _paintGradient.SetKeys(_rainbowKeys, _alphaKeys);
                    line.colorGradient = _paintGradient;
                }
                else
                {
                    // The hexagram is yin and yang joined: one triangle of water-blue, one of sun-gold.
                    if (layer.Points == 6 && layer.Lines.Count == 2)
                        c = line == layer.Lines[0] ? YinColor : YangColor;
                    line.startColor = new Color(c.r, c.g, c.b, alpha);
                    line.endColor = new Color(c.r, c.g, c.b, alpha);
                }
            }
        }

        private static readonly Color YinColor = new Color(0.60f, 0.82f, 1f);
        private static readonly Color YangColor = new Color(1f, 0.82f, 0.50f);

        /// <summary>
        /// Small lights circle the die, as many as the face that would complete seven with the last face
        /// (none after a seven). A wordless hint; the chance of that face stays the same.
        /// </summary>
        public void SetHint(int lastFaceId)
        {
            int k = PrayerStar.FaceCompletingSeven(lastFaceId);
            foreach (var o in _hintOrbs) if (o != null) Destroy(o.gameObject);
            _hintOrbs.Clear();
            for (int i = 0; i < k; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "HintLight";
                q.transform.SetParent(_hintRoot, false);
                q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                q.transform.localScale = Vector3.one * 0.24f;
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = _hintMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                _hintOrbs.Add(q.transform);
            }
            _hintAlpha = 0f;
            _hintTarget = k > 0 ? 1f : 0f;
        }

        /// <summary>Shows the star and the hint the last saved roll left (start of play, restore).</summary>
        public void ShowSaved(List<RoundSummary> history)
        {
            ShowSaved(LastStar(history));
            SetHint(history != null && history.Exists(round => round.Resolved && round.PrayerStar >= 5)
                ? LastFace(history) : 0);
            _hintAlpha = _hintTarget;
        }

        /// <summary>Shows a saved star at once (start of play, restore). No sound, no label.</summary>
        public void ShowSaved(int points)
        {
            points = Mathf.Clamp(_qaStar > 0 ? _qaStar : points, 5, 8);
            if (_leaving != null) { DestroyLayer(_leaving); _leaving = null; }
            if (_current != null) DestroyLayer(_current);
            _current = MakeLayer(points);
            _current.Alpha = 1f;
            _points = points;
            _sinceChange = 99f;
            _boost = 0f;
        }

        private void DestroyLayer(Layer layer)
        {
            if (layer == null) return;
            if (layer.NodeMat != null) Destroy(layer.NodeMat);
            Destroy(layer.Root.gameObject);
        }

        private void Change(int points)
        {
            if (_leaving != null) DestroyLayer(_leaving);
            _leaving = _current;
            _current = MakeLayer(points);
            _current.Alpha = 0f;
            _points = points;
            _sinceChange = 0f;
            _boost = ReducedMotion ? 0f : 160f;
        }

        /// <summary>Starts this roll's reveal, ending any reveal still playing (its sounds and words included).</summary>
        public void Reveal(RollOutcome outcome, WorldState state)
        {
            StopReveal();
            Track(StartCoroutine(PlayReveal(outcome, state)));
        }

        private Coroutine Track(Coroutine c) { if (c != null) _revealRoutines.Add(c); return c; }

        /// <summary>A new roll begins: the last roll's words step aside (they never stand for the new roll).</summary>
        public void BeginRoll()
        {
            if (_causeGroup == null || _causeGroup.alpha <= 0f) return;
            if (_causeRoutine != null) StopCoroutine(_causeRoutine);
            _causeRoutine = StartCoroutine(FadeOutCause());
        }

        private IEnumerator FadeOutCause()
        {
            float from = _causeGroup.alpha;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime) { _causeGroup.alpha = from * (1f - t / 0.3f); yield return null; }
            _causeGroup.alpha = 0f;
        }

        private void StopReveal()
        {
            BeginRoll();
            foreach (var c in _revealRoutines) if (c != null) StopCoroutine(c);
            _revealRoutines.Clear();
            if (_thread != null) _thread.enabled = false;
            for (int j = 0; j < _lanternFlare.Count; j++) _lanternFlare[j] = 0f;
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
            // community.v1: the plants an answering community fed (saved effects).
            _communityTops.Clear();
            foreach (var e in outcome.Ecology.Effects)
            {
                if (e.Kind != Communities.AnswerKind || e.Amount <= 0) continue;
                var cp = state != null ? state.PlantAt(e.CellId) : null;
                _communityTops.Add(View.CellTop(e.CellId) + Vector3.up * (cp != null ? 0.12f + 0.75f * cp.Growth / 2500f : 0.15f));
            }
            // The gift: light falls on each plant that the star actually nourished.
            var tops = new List<Vector3>();
            foreach (var e in outcome.Ecology.Effects)
            {
                if (e.Kind != PrayerStar.GiftKind || e.Amount <= 0) continue;
                var plant = state != null ? state.PlantAt(e.CellId) : null;
                float h = plant != null ? 0.12f + 0.75f * plant.Growth / 2500f : 0.15f;
                tops.Add(View.CellTop(e.CellId) + Vector3.up * h);
            }
            // Flowers that returned to the soil this roll (lifecycle.v1): a soft rise of light where they stood.
            if (_glints != null && View != null)
                foreach (var e in outcome.Ecology.Events)
                {
                    bool withered = e.Kind == Lifecycle.WitheredEvent;
                    if (e.Kind != Lifecycle.ReturnedEvent && e.Kind != "ReturnedToSoil" && !withered) continue;
                    var at = View.CellTop(e.CellId);
                    for (int i = 0; i < 18; i++)
                        _glints.Emit(new ParticleSystem.EmitParams
                        {
                            position = at + new Vector3(UnityEngine.Random.Range(-0.25f, 0.25f), UnityEngine.Random.Range(0f, 0.5f), UnityEngine.Random.Range(-0.25f, 0.25f)),
                            velocity = ReducedMotion ? Vector3.zero : Vector3.up * UnityEngine.Random.Range(0.15f, 0.45f),
                            // A withered seedling leaves a few dull motes; a returned flower a warm rise of light.
                            startColor = withered ? new Color(0.72f, 0.66f, 0.55f, 0.45f) : new Color(1f, 0.86f, 0.62f, 0.75f),
                            startLifetime = UnityEngine.Random.Range(1.4f, 2.4f)
                        }, 1);
                }
            var lines = new List<string>();
            foreach (var l in new[] { CauseLine(outcome.Ecology), WishLine(outcome), HintLine(outcome.FaceId, star.LuckyCount),
                         SeasonLine(outcome), YearLine(outcome, star.LuckyCount), FirstTimeTip(outcome) })
                if (!string.IsNullOrEmpty(l)) lines.Add(l);
            Track(StartCoroutine(ShowCauseLater(lines.Count > 0 ? string.Join("\n", lines) : null)));
            Track(StartCoroutine(CycleToneLater(CycleToneFor(outcome.Ecology))));
            yield return PlayRevealCore(star.Points, outcome.FaceId, previousFaceId, tops, outcome.FaceCount, star);
        }

        /// <summary>Which cycle tone this roll rings (one at most): seed set, else a return, else a withering.</summary>
        public static string CycleToneFor(EcologyResult eco)
        {
            if (eco == null) return null;
            bool fruit = false, ret = false, wither = false;
            foreach (var e in eco.Effects)
            {
                if (e.Kind == Lifecycle.FruitKind) fruit = true;
                else if (e.Kind == Lifecycle.ReturnKind) ret = true;
                else if (e.Kind == Lifecycle.WitherKind) wither = true;
            }
            return fruit ? "fruit" : ret ? "return" : wither ? "wither" : null;
        }

        private IEnumerator CycleToneLater(string kind)
        {
            if (kind == null || Audio == null) yield break;
            yield return new WaitForSeconds(2.4f); // after the star's chord has bloomed
            Audio.PlayCycleTone(kind);
        }

        private IEnumerator ShowCauseLater(string line)
        {
            yield return new WaitForSeconds(1.6f); // after the star settles
            ShowCause(line);
        }

        private IEnumerator PlayRevealCore(int points, int faceId, int previousFaceId, List<Vector3> tops, int faceCount = 6,
            PrayerStarReading star = null)
        {
            _hintTarget = 0f; // the old hint fades while the die speaks
            if (Audio != null) Audio.PlayFaceNote(faceId);
            // The die's answer runs down to the star as a thread of light.
            yield return Thread(points, 0.55f);
            Change(points);
            if (star != null && star.LuckyNumber)
            {
                // lucky.v1: a lucky number lights the lanterns one after another, climbing do..si (and do).
                Track(StartCoroutine(LanternSequence(points)));
                if (Audio != null) Audio.PlayLuckyScale(points);
                yield return new WaitForSeconds(LuckyStep * points + 0.15f);
            }
            else yield return new WaitForSeconds(0.55f);
            _flash = points >= 7 ? 1f : points == 6 ? 0.5f : 0.25f;
            if (Audio != null) Audio.PlayStarChord(points);
            EmitBurst(points);
            if (points >= 7) ShowLabel(points, LuckyLabelLines(star, points, faceId, previousFaceId, faceCount), false);
            yield return new WaitForSeconds(0.5f);
            int perPlant = points >= 8 ? 14 : points == 7 ? 10 : points == 6 ? 5 : 3;
            // Many plants share a fixed number of glints, so every one of them still shows its light.
            if (tops.Count > 0) perPlant = Mathf.Clamp(GlintBudget / tops.Count, 2, perPlant);
            foreach (var c in _communityTops) Shower(c, 6, 3); // community.v1: the answering community glints too
            _communityTops.Clear();
            for (int i = 0; i < tops.Count; i++)
            {
                Shower(tops[i], points, perPlant);
                if (!ReducedMotion) yield return new WaitForSeconds(points >= 8 ? 0.05f : 0.09f);
            }
            yield return new WaitForSeconds(0.4f);
            SetHint(faceId); // lights for the face that would complete seven next time
        }

        /// <summary>QA only: a reading for the filmstrip (-dgLuckyCount N lucky numbers before, -dgLuckyStreak S in a row).</summary>
        private static PrayerStarReading QaReading(int face, int prev)
        {
            int before = 2, streak = 0;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-dgLuckyCount") int.TryParse(args[i + 1], out before);
                if (args[i] == "-dgLuckyStreak") int.TryParse(args[i + 1], out streak);
            }
            // Same polarity and low harmony: a plain lucky number shows seven, unless it is the garden's seventh.
            return PrayerStar.Read(face, prev, "water_drop", "seed_single", false, 4, -1, before, streak);
        }

        private IEnumerator QaRollThenReveal(int points, int face, int prev, List<Vector3> tops)
        {
            // The die tumbles as in a roll (the star whirls with it), then the answer is revealed.
            yield return View.PlayOutcome(null, "light", null);
            var star = QaReading(face, prev);
            yield return PlayRevealCore(star.LuckyNumber ? star.Points : points, face, prev, tops, 6, star);
        }

        /// <summary>
        /// QA only (explicit -dgRevealFrames DIR, never in normal play): plays one reveal close to the altar
        /// (-dgStarReveal N, -dgFace F, -dgPrevFace P) and saves a frame every 0.2 s, then quits.
        /// Nothing is rolled and nothing is written to the journal.
        /// </summary>
        private IEnumerator QaFilmstrip(string dir, string[] args)
        {
            int points = 7, face = 4, prev = 3;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-dgStarReveal") int.TryParse(args[i + 1], out points);
                if (args[i] == "-dgFace") int.TryParse(args[i + 1], out face);
                if (args[i] == "-dgPrevFace") int.TryParse(args[i + 1], out prev);
            }
            System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(1.0f);
            _qaCameraCanvas = true;
            ShowSaved(5);
            SetHint(prev);
            _hintAlpha = 1f;
            var cam = Camera.main;
            var c = _root.position;
            var camPos = c + new Vector3(0f, 21f, -23f);
            cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(c + Vector3.up * 1.5f - camPos, Vector3.up));
            var tops = new List<Vector3>();
            for (int i = 0; i < 7; i++)
            {
                float a = i * 0.9f;
                var p = c + new Vector3(Mathf.Cos(a) * 4f, 0f, Mathf.Sin(a) * 4f);
                p.y = IslandTerrain.Height(p.x, p.z) + 0.2f;
                tops.Add(p);
            }
            yield return new WaitForSeconds(0.6f);
            StartCoroutine(QaRollThenReveal(points, face, prev, tops));
            StartCoroutine(ShowCauseLater("星の恵み 7株　水の恵み → 乾いた土の株へ　・　受粉した花が土に還り、実りを落とした"));
            var rt = new RenderTexture(960, 540, 24);
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            for (int f = 0; f < 30; f++)
            {
                yield return new WaitForEndOfFrame();
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"frame{f:00}.png"), tex.EncodeToPNG());
                yield return new WaitForSeconds(0.2f);
            }
            UnityEngine.Application.Quit();
        }

        /// <summary>A thread of light from the die down to the nearest point of the star's circle.</summary>
        private IEnumerator Thread(int points, float seconds)
        {
            if (_thread == null || View == null || View.DiceTransform == null) yield break;
            Vector3 from = View.DiceTransform.position;
            var flat = new Vector3(from.x - _root.position.x, 0f, from.z - _root.position.z);
            Vector3 to = _root.position + (flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward) * StarRadius;
            var col = StarColor(points);
            int n = _thread.positionCount;
            _thread.enabled = true;
            for (float t = 0f; t < seconds + 0.6f; t += Time.deltaTime)
            {
                float head = ReducedMotion ? 1f : Mathf.Clamp01(t / seconds);
                float fade = 1f - Mathf.Clamp01((t - seconds) / 0.6f);
                for (int i = 0; i < n; i++)
                {
                    float u = i / (float)(n - 1) * head;
                    // A gentle arc: out from the die, then down to the sea.
                    var p = Vector3.Lerp(from, to, u) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * 1.2f);
                    _thread.SetPosition(i, p);
                }
                _threadGradient.SetKeys(new[] { new GradientColorKey(col, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0.15f * fade, 0f), new GradientAlphaKey(0.9f * fade, 1f) });
                _thread.colorGradient = _threadGradient;
                yield return null;
            }
            _thread.enabled = false;
            if (_glints != null)
                for (int i = 0; i < 24; i++)
                    _glints.Emit(new ParticleSystem.EmitParams
                    {
                        position = to + UnityEngine.Random.insideUnitSphere * 0.4f, velocity = Vector3.up * UnityEngine.Random.Range(0.2f, 0.7f),
                        startColor = new Color(col.r, col.g, col.b, 0.9f), startLifetime = UnityEngine.Random.Range(1f, 1.8f)
                    }, 1);
        }

        /// <summary>
        /// The lucky number and its honest chance: one face out of the faces on the die this round
        /// (1/6, or 1/7 when the seventh face was added).
        /// </summary>
        public static string LuckyOddsLine(int faceId, int previousFaceId, int faceCount)
        {
            // Honest odds of a lucky number this round: the face that makes seven with the previous one, and on a
            // seven-face round the 7 as well (2/7). (Previously each face's own 1/n was shown, understating 2/7.)
            int n = Mathf.Max(1, faceCount);
            int k = PrayerStar.FaceCompletingSeven(previousFaceId);
            bool seven = faceCount >= LuckySeven.FaceId;
            int lucky = (k > 0 ? 1 : 0) + (seven ? 1 : 0);
            string which = k > 0 && seven ? $"{k} と 7 の目" : k > 0 ? $"{k} の目" : "7 の目";
            string odds = $"偶然なら {Mathf.Max(1, lucky)}/{n}（{which}）";
            return faceId == LuckySeven.FaceId ? $"七の目　・　{odds}" : $"前の目 {previousFaceId} と今の目 {faceId} で七　・　{odds}";
        }

        private Color GlintColor(int points) =>
            points >= 8 ? RainbowColors[UnityEngine.Random.Range(0, RainbowColors.Length)] : StarColor(points);

        private void EmitBurst(int points)
        {
            if (_glints == null) return;
            int n = points >= 8 ? 160 : points == 7 ? 112 : points == 6 ? 48 : 25;
            var c = _root.position;
            for (int i = 0; i < n; i++)
            {
                // Glints rise from the star's points on the sea.
                int k = i % Mathf.Max(5, points);
                float a = -_angle * Mathf.Deg2Rad + Mathf.PI * 0.5f + k * Mathf.PI * 2f / Mathf.Max(5, points);
                float r = StarRadius * UnityEngine.Random.Range(0.92f, 1.02f);
                var pos = c + new Vector3(Mathf.Cos(a) * r, 0.05f, Mathf.Sin(a) * r);
                var vel = ReducedMotion ? Vector3.zero
                    : new Vector3(0f, UnityEngine.Random.Range(0.4f, points >= 7 ? 1.6f : 0.8f), 0f);
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
            // Below the main UI's canvas (order 0): its title card and panels cover the star's words.
            canvas.sortingOrder = -1;
            _labelCanvas = canvas;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            _labelGroup = canvasObj.AddComponent<CanvasGroup>();
            _labelGroup.alpha = 0f;
            _labelGroup.blocksRaycasts = false;
            _labelGroup.interactable = false;
            // The lucky label: a large numeral, the title, the honest odds and the garden's count, popping in together.
            var rootObj = new GameObject("LuckyLabelRoot");
            rootObj.transform.SetParent(canvasObj.transform, false);
            _labelRoot = rootObj.AddComponent<RectTransform>();
            _labelRoot.anchorMin = Vector2.zero; _labelRoot.anchorMax = Vector2.one;
            _labelRoot.offsetMin = Vector2.zero; _labelRoot.offsetMax = Vector2.zero;
            // A soft dark panel behind the two small lines (odds and count) keeps them readable over bright ground.
            var luckyBackObj = new GameObject("LuckyLabelBacking");
            luckyBackObj.transform.SetParent(_labelRoot, false);
            _labelBacking = luckyBackObj.AddComponent<Image>();
            _labelBacking.color = new Color(0.02f, 0.05f, 0.07f, 0.48f);
            _labelBacking.raycastTarget = false;
            var lbRect = luckyBackObj.GetComponent<RectTransform>();
            lbRect.anchorMin = lbRect.anchorMax = new Vector2(0.5f, 0.5f);
            lbRect.anchoredPosition = new Vector2(0f, 89f);
            _labelNumeral = LabelText(_labelRoot, new Vector2(0f, 222f), 112);
            _labelNumeral.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 140f);
            _labelTitle = LabelText(_labelRoot, new Vector2(0f, 146f), 42);
            _labelSub = LabelText(_labelRoot, new Vector2(0f, 104f), 20);
            _labelCounter = LabelText(_labelRoot, new Vector2(0f, 74f), 18);

            // A quiet line of cause and effect for every roll, under the lucky label's place. Its own canvas: the
            // lucky label's canvas group is transparent on most rolls and would hide it too.
            var causeObj = new GameObject("SigilCauseCanvas");
            causeObj.transform.SetParent(transform, false);
            _causeCanvas = causeObj.AddComponent<Canvas>();
            _causeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _causeCanvas.sortingOrder = -2;
            var causeScaler = causeObj.AddComponent<CanvasScaler>();
            causeScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            causeScaler.referenceResolution = new Vector2(1600, 900);
            _causeGroup = causeObj.AddComponent<CanvasGroup>();
            _causeGroup.alpha = 0f;
            _causeGroup.blocksRaycasts = false;
            // A soft dark panel behind the line keeps it readable over bright ground (sized to the text when shown).
            var backObj = new GameObject("SigilCauseBacking");
            backObj.transform.SetParent(causeObj.transform, false);
            _causeBacking = backObj.AddComponent<Image>();
            _causeBacking.color = new Color(0.02f, 0.05f, 0.07f, 0.48f);
            _causeBacking.raycastTarget = false;
            var backRect = backObj.GetComponent<RectTransform>();
            backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0.5f);
            backRect.pivot = new Vector2(0.5f, 1f);
            backRect.anchoredPosition = new Vector2(0f, 0f);
            // The lines wrap within the screen and grow downward, away from the lucky label above.
            _causeText = LabelText(causeObj.transform, new Vector2(0f, -9f), 20);
            _causeText.lineSpacing = 1.15f;
            _causeText.alignment = TextAnchor.UpperCenter;
            _causeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var causeRect = _causeText.GetComponent<RectTransform>();
            causeRect.pivot = new Vector2(0.5f, 1f);
            causeRect.sizeDelta = new Vector2(1320f, 80f);

            // The standing line, top left under the header: the season's wish with its progress, the garden's lucky
            // count and the face that would make the next lucky number. Always shown (between rolls too).
            var standCanvasObj = new GameObject("SigilStandingCanvas");
            standCanvasObj.transform.SetParent(transform, false);
            var standCanvas = standCanvasObj.AddComponent<Canvas>();
            standCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            standCanvas.sortingOrder = -3;
            var standScaler = standCanvasObj.AddComponent<CanvasScaler>();
            standScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            standScaler.referenceResolution = new Vector2(1600, 900);
            // Hidden while the title card is up (the title no longer covers the whole screen).
            _standingGroup = standCanvasObj.AddComponent<CanvasGroup>();
            _standingGroup.blocksRaycasts = false;
            _standingGroup.interactable = false;
            _standingGroup.alpha = 0f; // fades in once the garden is entered
            var sbObj = new GameObject("SigilStandingBacking");
            sbObj.transform.SetParent(standCanvasObj.transform, false);
            _standingBacking = sbObj.AddComponent<Image>();
            _standingBacking.color = new Color(0.02f, 0.05f, 0.07f, 0.42f);
            _standingBacking.raycastTarget = false;
            var sbRect = sbObj.GetComponent<RectTransform>();
            sbRect.anchorMin = sbRect.anchorMax = sbRect.pivot = new Vector2(0f, 1f);
            sbRect.anchoredPosition = new Vector2(30f, -70f);
            var stObj = new GameObject("SigilStanding");
            stObj.transform.SetParent(standCanvasObj.transform, false);
            _standingText = stObj.AddComponent<Text>();
            _standingText.alignment = TextAnchor.UpperLeft;
            _standingText.fontSize = 15;
            _standingText.lineSpacing = 1.1f;
            _standingText.raycastTarget = false;
            _standingText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _standingText.verticalOverflow = VerticalWrapMode.Overflow;
            _standingText.color = new Color(0.93f, 0.95f, 0.92f, 0.95f);
            var stShadow = stObj.AddComponent<Shadow>();
            stShadow.effectColor = new Color(0.02f, 0.04f, 0.06f, 0.6f);
            var stRect = stObj.GetComponent<RectTransform>();
            stRect.anchorMin = stRect.anchorMax = stRect.pivot = new Vector2(0f, 1f);
            stRect.anchoredPosition = new Vector2(40f, -74f);
            stRect.sizeDelta = new Vector2(300f, 44f);
            _causeText.color = new Color(0.95f, 0.93f, 0.84f, 0.92f);
        }

        /// <summary>
        /// One line of what this roll set going, from the saved effects only: where the star's gift went
        /// (by the rolled blessing) and the cycle of the garden (a flower returned; a pollinated one let its seed fall).
        /// </summary>
        public static string CauseLine(EcologyResult eco)
        {
            if (eco == null || eco.PrayerStar == null) return null;
            int gifts = 0, fruit = 0, returned = 0, plain = 0, withered = 0;
            foreach (var e in eco.Effects)
            {
                if (e.Kind == PrayerStar.GiftKind && e.Amount > 0) gifts++;
                if (e.Kind == Lifecycle.FruitKind) fruit++;
                if (e.Kind == Lifecycle.ReturnKind) returned++;
                if (e.Kind == Lifecycle.WitherKind) withered++;
            }
            plain = returned - fruit;
            var parts = new List<string>();
            if (gifts > 0)
            {
                var star = eco.PrayerStar;
                string what = star.GiftGrowth > 0 ? $"星の恵み {gifts}株に ＋{star.GiftGrowth}"
                    : star.Points == 7 && star.LuckyStreak >= 2 ? $"<color={Gold}>ダブル ラッキーナンバー</color>で {gifts}株が一段育った"
                    : star.Points == 7 ? $"<color={Gold}>ラッキーナンバー</color>で {gifts}株が半段育った"
                    : $"星の恵みで {gifts}株が一段育った";
                string site = null;
                if (star.Points < 8)
                    foreach (var st in Sites.All) if (st.CenterCellId == star.AnchorCellId) site = st.LabelJa;
                string where = !star.HasElement ? "若い芽へ"
                    : star.Element == ElementId.Water ? "水の恵み → 乾いた土の株へ"
                    : star.Element == ElementId.Light ? "光の恵み → 日向を好む株へ"
                    : star.Element == ElementId.Wind ? "風の恵み → 蜂や蝶に好かれる株へ"
                    : "種の恵み → いちばん若い芽へ";
                parts.Add(what + (site != null ? $"（{site}のあたりから）" : "") + "　" + where);
            }
            foreach (var c in eco.Answered)
            {
                string blessing = c.Element == ElementId.Water ? "水" : c.Element == ElementId.Light ? "光" : c.Element == ElementId.Wind ? "風" : "種";
                parts.Add($"{c.LabelJa}（{c.Cells.Count}株）が{blessing}の恵みに応えた");
            }
            if (eco.PrayerStar.Points >= 8 && eco.PostState != null &&
                eco.PostState.Plants.Exists(p => p.Stage == PlantStage.Flower && p.Pollinated))
                parts.Add("八の虹の下で、咲いている花がみな受粉した");
            if (fruit > 0) parts.Add("受粉した花が土に還り、実りを落とした");
            else if (plain > 0) parts.Add("花がひとつ土に還り、となりを養った");
            else if (eco.Relations.Exists(link => link.Kind == "fruited"))
                parts.Add("花に、次の種が実った");
            else if (eco.Relations.Exists(link => link.Kind == "returned-to-soil"))
                parts.Add("花の株が土へ還った");
            foreach (var e in eco.Effects)
                if (e.Kind == Lifecycle.WitherKind) parts.Add(WitherLine(e.Reason));
            return parts.Count > 0 ? string.Join("　・　", parts) : null;
        }

        /// <summary>The standing line's two rows (season's wish with progress; lucky count and the next lucky face).</summary>
        public static string StandingLines(GardenCoordinator c)
        {
            if (c == null || c.State == null) return null;
            var (index, goalId) = c.UpcomingSeason();
            var g = Seasons.Get(goalId);
            var rows = new List<string>();
            if (g != null)
            {
                var recent = new List<string>(c.RolledRecipes);
                var presence = Ecology.Presence(c.State, c.CurrentWeather, recent, null);
                var kinds = new HashSet<string>();
                foreach (var v in presence) kinds.Add(v.Kind);
                int harmony = Harmony.Read(recent, kinds.Count, Flora.BloomingSpecies(c.State)).Value;
                var (have, need) = Seasons.Progress(goalId, c.State, harmony, kinds.Contains(Ecology.Butterfly));
                int left = Seasons.Length - ((c.State.WorldRevision) % Seasons.Length);
                rows.Add(c.UpcomingSeasonMet ? $"季節の願い：{g.LabelJa}（叶った）" : $"季節の願い：{g.LabelJa} {Math.Min(have, need)}/{need}（あと {left} 投）");
            }
            var faces = c.RolledFaces;
            int count = PrayerStar.CountLuckies(new List<int>(faces));
            int last = faces.Count > 0 ? faces[faces.Count - 1] : 0;
            int k = PrayerStar.FaceCompletingSeven(last);
            string next = k > 0 ? $"・次は <color={Gold}>{k}</color> の目" : "";
            rows.Add($"<color={Gold}>ラッキーナンバー</color> {count % PrayerStar.LuckiesForRainbow}/{PrayerStar.LuckiesForRainbow}{next}");
            return string.Join("\n", rows);
        }

        /// <summary>Refreshes the standing line when the garden's revision or phase changed (not every frame).</summary>
        private void RefreshStanding()
        {
            if (_standingText == null || Coordinator == null || Coordinator.State == null) return;
            if (_ui == null) _ui = FindFirstObjectByType<GardenUi>();
            if (_standingGroup != null)
                _standingGroup.alpha = Mathf.MoveTowards(_standingGroup.alpha, _ui != null && _ui.TitleCardOpen ? 0f : 1f, Time.deltaTime * 2.5f);
            int rev = Coordinator.State.WorldRevision, count = Coordinator.RolledFaces.Count;
            var phase = Coordinator.Phase;
            if (_standingKey != null && rev == _standingRev && count == _standingCount && phase == _standingPhase) return;
            _standingKey = "shown"; _standingRev = rev; _standingCount = count; _standingPhase = phase;
            string text = StandingLines(Coordinator);
            if (_standingText.font == null)
            {
                var font = Font;
                if (font == null) { var ui = FindFirstObjectByType<GardenUi>(); if (ui != null) font = ui.UiFont; }
                if (font != null) _standingText.font = font; else { _standingKey = null; return; }
            }
            _standingText.text = text ?? "";
            if (_standingBacking != null)
                _standingBacking.rectTransform.sizeDelta = new Vector2(_standingText.preferredWidth + 24f, _standingText.preferredHeight + 10f);
        }

        /// <summary>
        /// A one-time tip the first time something happens on this device (PlayerPrefs; presentation only): the first
        /// lucky number, the first season's wish, the first seventh face earned, the first flower returning to the soil.
        /// </summary>
        private static string FirstTimeTip(RollOutcome o)
        {
            var star = o?.Ecology?.PrayerStar;
            if (star == null) return null;
            string Once(string key, string text)
            {
                try
                {
                    if (PlayerPrefs.GetInt(key, 0) == 1) return null;
                    PlayerPrefs.SetInt(key, 1);
                    PlayerPrefs.Save();
                }
                catch (Exception) { return null; }
                return text;
            }
            if (star.LuckyNumber)
                return Once("dg.tip.lucky", $"ヒント：前の目と今の目を足して 7 になると <color={Gold}>ラッキーナンバー</color>。七つ集めると八の虹。");
            if (o.EarnedSeventh)
                return Once("dg.tip.seventh", "ヒント：七つめの面を足すと、7 の目もラッキーナンバーになる。どの面も 1/7。");
            if (o.Ecology.Effects.Exists(e => e.Kind == Lifecycle.ReturnKind))
                return Once("dg.tip.cycle", "ヒント：花は咲き終えると土に還る。蜂や蝶が訪れた花は、実りを残すことがある。");
            if (o.SeasonGoalId != null)
                return Once("dg.tip.season", "ヒント：12投ごとに、庭が一つ願う（左上）。叶えると七つめの面を足せる。");
            return null;
        }

        /// <summary>Why a seedling withered, and what would help (lifecycle.v4, from the saved reason).</summary>
        public static string WitherLine(string reason) =>
            reason == "dry" ? "乾きすぎた土で、芽がひとつ枯れた（水の恵みが助けになる）"
            : reason == "wet" ? "湿りすぎた土で、芽がひとつ枯れた"
            : reason == "hard" ? "硬くなった土で、芽がひとつ枯れた"
            : reason == "drain" ? "水はけが合わず、芽がひとつ枯れた"
            : reason == "light" ? "日の当たりが合わず、芽がひとつ枯れた"
            : "合わなくなった土で、芽がひとつ枯れた";

        /// <summary>The wish of this roll, said plainly with its honest chance, and whether it earned the seventh face.</summary>
        public static string WishLine(RollOutcome o)
        {
            if (o == null || o.IntentKind == null || o.IntentKind == "none" || o.MatchingFaces <= 0) return null;
            string odds = $"偶然なら {o.MatchingFaces}/{o.FaceCount}";
            if (!o.IntentMatched) return $"願い：叶わなかった（{odds}）";
            bool longShot = LuckySeven.WishEarnsSeventh(true, o.MatchingFaces, o.FaceCount);
            return longShot ? $"願い：叶った（{odds}）　・　次の回に七つめの面を足せる"
                : $"願い：叶った（{odds}）";
        }

        /// <summary>Every fourth season: a look back over the year (48 rolls).</summary>
        public static string YearLine(RollOutcome o, int luckyCount) =>
            o != null && o.YearEnded
                ? $"一年がめぐった：叶った季節 {o.SeasonsMetCount}・八の虹 {o.RainbowCount}・<color={Gold}>ラッキーナンバー</color> {luckyCount}"
                : null;

        /// <summary>The season's wish (season.v1): what the garden asks, how long is left, or that it came true.</summary>
        public static string SeasonLine(RollOutcome o)
        {
            var g = o != null ? Seasons.Get(o.SeasonGoalId) : null;
            if (g == null) return null;
            if (o.SeasonMetNow) return $"季節の願いが叶った：{g.LabelJa}　・　次の回に七つめの面を足せる（<color={Gold}>7</color> も<color={Gold}>ラッキーナンバー</color>になる）";
            if (o.SeasonMet) return $"季節の願い：{g.LabelJa}（叶った）";
            return o.SeasonRollsLeft > 0 ? $"季節の願い：{g.LabelJa}（あと {o.SeasonRollsLeft} 投）" : $"季節の願い：{g.LabelJa}（今季はここまで）";
        }

        /// <summary>
        /// The words for the hint: which face would make the lucky number next, and how many the garden has toward
        /// the seven that bring the rainbow (no change in any face's chance).
        /// </summary>
        public static string HintLine(int lastFaceId, int luckyCount = 0)
        {
            int k = PrayerStar.FaceCompletingSeven(lastFaceId);
            int have = luckyCount % PrayerStar.LuckiesForRainbow;
            return k > 0
                ? $"次に <color={Gold}>{k}</color> の目（七つめの面があれば <color={Gold}>7</color> も）が出れば <color={Gold}>ラッキーナンバー</color>（いま {have}/{PrayerStar.LuckiesForRainbow}・どの目も同じ確率）"
                : null;
        }

        private void ShowCause(string line)
        {
            if (_causeGroup == null || string.IsNullOrEmpty(line)) return;
            var font = Font;
            if (font == null)
            {
                var ui = FindFirstObjectByType<GardenUi>();
                if (ui != null) font = ui.UiFont;
            }
            if (font != null) _causeText.font = font;
            _causeText.text = line;
            if (_causeBacking != null)
                _causeBacking.rectTransform.sizeDelta = new Vector2(Mathf.Min(1320f, _causeText.preferredWidth) + 48f,
                    _causeText.preferredHeight + 18f);
            if (_causeRoutine != null) StopCoroutine(_causeRoutine);
            _causeRoutine = StartCoroutine(FadeCause());
        }

        private IEnumerator FadeCause()
        {
            for (float t = 0f; t < 0.8f; t += Time.deltaTime) { _causeGroup.alpha = t / 0.8f; yield return null; }
            _causeGroup.alpha = 1f;
            yield return new WaitForSeconds(4.0f);
            for (float t = 0f; t < 1.4f; t += Time.deltaTime) { _causeGroup.alpha = 1f - t / 1.4f; yield return null; }
            _causeGroup.alpha = 0f;
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
            // A dark outline and a soft shadow keep the light text readable over bright grass and sea.
            var outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.05f, 0.06f, 0.75f);
            outline.effectDistance = new Vector2(1.3f, -1.3f);
            var shadow = obj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.02f, 0.04f, 0.06f, 0.55f);
            shadow.effectDistance = new Vector2(2f, -2f);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(1000f, 60f);
            return t;
        }

        /// <summary>
        /// The lucky label's four lines [numeral, title, odds, the garden's count] (lucky.v1), from the saved reading.
        /// Seven: "ラッキーナンバー" (or "ダブル ラッキーナンバー" for two in a row) with the honest odds and k/7.
        /// Eight: "七つの幸運・八の虹" when it is the seventh lucky number, otherwise "八芒星・八の虹".
        /// </summary>
        public static string[] LuckyLabelLines(PrayerStarReading star, int points, int faceId, int previousFaceId, int faceCount)
        {
            int count = star != null ? star.LuckyCount : 0;
            int k = count <= 0 ? 0 : (count % PrayerStar.LuckiesForRainbow == 0 ? PrayerStar.LuckiesForRainbow : count % PrayerStar.LuckiesForRainbow);
            string progress = $"ラッキーナンバー {k}/{PrayerStar.LuckiesForRainbow}　・　七つそろうと八の虹";
            if (points >= 8)
            {
                if (star != null && star.SevenLuckies)
                    return new[] { "8", "七つの幸運　・　八の虹", "ラッキーナンバーが七つそろった", $"この庭のラッキーナンバー {count} 回目" };
                string why = star != null && star.WishAnswered && !star.YinYangUnion ? "叶った願いと島の調和" : "陰陽の結びと島の調和";
                return new[] { "8", "八芒星　・　八の虹", $"ラッキーナンバーに、{why}が重なった", progress };
            }
            bool dbl = star != null && star.LuckyStreak >= 2;
            return new[]
            {
                "7", dbl ? "ダブル　ラッキーナンバー" : "ラッキーナンバー", LuckyOddsLine(faceId, previousFaceId, faceCount),
                dbl ? progress + "　・　ふたつ続いて、七株が一段育つ" : progress
            };
        }

        private void ShowLabel(int points, string[] lines, bool hold)
        {
            if (_labelGroup == null || lines == null) return;
            var font = Font;
            if (font == null)
            {
                var ui = FindFirstObjectByType<GardenUi>();
                if (ui != null) font = ui.UiFont;
            }
            if (font != null) foreach (var t in new[] { _labelNumeral, _labelTitle, _labelSub, _labelCounter }) t.font = font;
            _labelNumeral.text = lines[0];
            _labelTitle.text = lines[1];
            _labelSub.text = lines[2];
            _labelCounter.text = lines[3];
            _labelTitle.color = points >= 8 ? new Color(1f, 0.96f, 0.88f) : new Color(1f, 0.86f, 0.52f);
            _labelSub.color = new Color(0.92f, 0.95f, 0.93f, 0.9f);
            _labelCounter.color = new Color(1f, 0.88f, 0.62f, 0.92f);
            if (_labelBacking != null)
                _labelBacking.rectTransform.sizeDelta = new Vector2(
                    Mathf.Min(1440f, Mathf.Max(_labelSub.preferredWidth, _labelCounter.preferredWidth) + 56f), 66f);
            if (_labelRoutine != null) StopCoroutine(_labelRoutine);
            _labelRoutine = StartCoroutine(FadeLabel(points, hold));
        }

        /// <summary>The label pops in (a little larger, then settling), its numeral shimmering gold and white.</summary>
        private IEnumerator FadeLabel(int points, bool hold)
        {
            var gold = new Color(1f, 0.82f, 0.42f);
            var white = new Color(1f, 0.98f, 0.92f);
            void Shimmer(float t)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 5f);
                var c = Color.Lerp(gold, white, k);
                if (points >= 8) c = Color.HSVToRGB((t * 0.25f) % 1f, 0.35f, 1f); // the eight shimmers through the rainbow
                _labelNumeral.color = c;
            }
            float time = 0f;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime, time += Time.deltaTime)
            {
                _labelGroup.alpha = t / 0.8f;
                float s = ReducedMotion ? 1f : t < 0.25f ? Mathf.Lerp(0.6f, 1.12f, t / 0.25f) : Mathf.Lerp(1.12f, 1f, Mathf.Clamp01((t - 0.25f) / 0.3f));
                _labelRoot.localScale = Vector3.one * s;
                Shimmer(time);
                yield return null;
            }
            _labelGroup.alpha = 1f;
            _labelRoot.localScale = Vector3.one;
            for (float t = 0f; hold || t < 3.4f; t += Time.deltaTime, time += Time.deltaTime) { Shimmer(time); yield return null; }
            for (float t = 0f; t < 1.4f; t += Time.deltaTime, time += Time.deltaTime)
            {
                _labelGroup.alpha = 1f - t / 1.4f;
                Shimmer(time);
                yield return null;
            }
            _labelGroup.alpha = 0f;
        }

        /// <summary>
        /// lucky.v1: the lanterns on the star's points flare one after another, with glints rising at each point
        /// (a note of the lucky scale for each, played by GardenAudio.PlayLuckyScale in the same rhythm).
        /// </summary>
        private IEnumerator LanternSequence(int points)
        {
            yield return null; // let the lanterns take the new star's points first (LateUpdate)
            for (int v = 0; v < points; v++)
            {
                for (int j = 0; j < _lanterns.Count && j < _lanternFlare.Count; j++)
                    if (_lanternVertex.Length > j && _lanternVertex[j] == v) _lanternFlare[j] = 1f;
                if (_glints != null && _root != null)
                {
                    float a = Mathf.PI * 0.5f + v * Mathf.PI * 2f / points;
                    var at = _root.TransformPoint(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * StarRadius);
                    var col = points >= 8 ? RainbowColors[v % RainbowColors.Length] : new Color(1f, 0.84f, 0.45f);
                    for (int i = 0; i < 10; i++)
                        _glints.Emit(new ParticleSystem.EmitParams
                        {
                            position = at + UnityEngine.Random.insideUnitSphere * 0.35f,
                            velocity = ReducedMotion ? Vector3.zero : Vector3.up * UnityEngine.Random.Range(0.6f, 1.6f),
                            startColor = new Color(col.r, col.g, col.b, 0.95f), startLifetime = UnityEngine.Random.Range(1.2f, 2.2f)
                        }, 1);
                }
                if (!ReducedMotion) yield return new WaitForSeconds(LuckyStep);
            }
        }

        // ------------------------------------------------------------------ motion

        /// <summary>
        /// How far the star turns this frame, geared to the die: between rolls it follows the die's slow
        /// turn (Gear), while the die tumbles it spins with it. The die's snap back to rest is ignored.
        /// </summary>
        private float DieTurn()
        {
            if (View == null || View.DiceTransform == null) return 0f;
            var rot = View.DiceTransform.rotation;
            if (!_haveDiceRot) { _lastDiceRot = rot; _haveDiceRot = true; return 0f; }
            float total = Quaternion.Angle(_lastDiceRot, rot);
            float yaw = Mathf.DeltaAngle(Yaw(_lastDiceRot), Yaw(rot));
            _lastDiceRot = rot;
            float dt = Time.deltaTime;
            if (dt <= 0f) return 0f;
            // By angular speed, so the same at 20 fps and 144 fps: the die's rest turn is ~9°/s, its tumble up
            // to ~780°/s, and the snap back to rest a jump within one frame.
            float speed = total / dt;
            if (speed < 60f) return yaw * Gear;                    // resting: the slow turn about the vertical
            if (speed > 1500f && total > 20f) return 0f;           // the snap back to rest
            return total * TumbleSpin;                             // tumbling: the star whirls along
        }

        // ------------------------------------------------------------------ lanterns on the star's points

        private void FindLanterns()
        {
            _lanternRing = transform.Find("SevenLights");
            if (_lanternRing == null) return;
            var lights = new List<Transform>();
            foreach (Transform child in _lanternRing)
            {
                if (child.name.StartsWith("Light")) lights.Add(child);
                else if (child.name == "Lantern reflection" || child.name == "Pillar") _lanternRefl.Add(child); // each lantern's companion
            }
            lights.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (lights.Count == 0) { _lanternRing = null; return; }
            var p0 = lights[0].localPosition;
            _lanternRadius = new Vector2(p0.x, p0.z).magnitude;
            // An eighth lantern for the eight-pointed star, made like the others.
            var extra = Instantiate(lights[0].gameObject, _lanternRing);
            extra.name = "Light8 (prayer star)";
            lights.Add(extra.transform);
            if (_lanternRefl.Count > 0)
            {
                var r = Instantiate(_lanternRefl[0].gameObject, _lanternRing);
                r.name = _lanternRefl[0].name + " (prayer star)";
                _lanternRefl.Add(r.transform);
            }
            foreach (var l in lights)
            {
                _lanterns.Add(l);
                _lanternScale.Add(l.localScale);
                _lanternDeg.Add(Mathf.Atan2(l.localPosition.z, l.localPosition.x) * Mathf.Rad2Deg);
                _lanternShow.Add(1f);
                _lanternFlare.Add(0f);
            }
            foreach (var r in _lanternRefl) _reflScale.Add(r.localScale);
        }

        /// <summary>Ring-local angle (degrees) of the star's vertex v, as the star lies now.</summary>
        private float VertexDeg(int v, int n)
        {
            float a = Mathf.PI * 0.5f + v * Mathf.PI * 2f / n;
            var world = _root.TransformPoint(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * _lanternRadius);
            var local = _lanternRing.InverseTransformPoint(world);
            return Mathf.Atan2(local.z, local.x) * Mathf.Rad2Deg;
        }

        /// <summary>Each point of the star takes the nearest free lantern; spare lanterns rest unseen.</summary>
        private void AssignLanterns(int n)
        {
            _assignedPoints = n;
            _lanternVertex = new int[_lanterns.Count];
            for (int j = 0; j < _lanternVertex.Length; j++) _lanternVertex[j] = -1;
            int usable = n >= 8 ? _lanterns.Count : Mathf.Min(7, _lanterns.Count);
            for (int v = 0; v < n; v++)
            {
                float target = VertexDeg(v, n);
                int best = -1; float bestD = float.MaxValue;
                for (int j = 0; j < usable; j++)
                {
                    if (_lanternVertex[j] >= 0) continue;
                    float d = Mathf.Abs(Mathf.DeltaAngle(_lanternDeg[j], target));
                    if (d < bestD) { bestD = d; best = j; }
                }
                if (best >= 0) _lanternVertex[best] = v;
            }
        }

        private void LateUpdate()
        {
            if (_root == null) return;
            if (_lanternRing == null && _lanterns.Count == 0) FindLanterns();
            if (_lanternRing == null) return;
            if (_points != _assignedPoints) AssignLanterns(_points);
            float dt = Time.deltaTime;
            bool snap = ReducedMotion || !_lanternsSnapped;
            // While the star whirls the lanterns trail behind; once it rests they sit on its points.
            float follow = snap ? 1f : 1f - Mathf.Exp(-dt * 2.4f);
            for (int j = 0; j < _lanterns.Count; j++)
            {
                int v = _lanternVertex.Length > j ? _lanternVertex[j] : -1;
                if (v >= 0) _lanternDeg[j] += Mathf.DeltaAngle(_lanternDeg[j], VertexDeg(v, _points)) * follow;
                _lanternShow[j] = snap ? (v >= 0 ? 1f : 0f) : Mathf.MoveTowards(_lanternShow[j], v >= 0 ? 1f : 0f, dt * 0.9f);
                float rad = _lanternDeg[j] * Mathf.Deg2Rad;
                var l = _lanterns[j];
                var p = l.localPosition;
                if (j >= 7) p.y = 0.9f + (ReducedMotion ? 0f : Mathf.Sin(Time.time * 0.8f + j * 0.9f) * 0.18f); // our own lantern bobs like the rest
                l.localPosition = new Vector3(Mathf.Cos(rad) * _lanternRadius, p.y, Mathf.Sin(rad) * _lanternRadius);
                float show = Mathf.SmoothStep(0f, 1f, _lanternShow[j]);
                if (j < _lanternFlare.Count) _lanternFlare[j] = Mathf.MoveTowards(_lanternFlare[j], 0f, dt * 1.1f);
                float flare = j < _lanternFlare.Count ? _lanternFlare[j] : 0f;
                l.localScale = _lanternScale[j] * show * (1f + 1.1f * flare); // a lucky number makes each lantern flare in turn
                if (j < _lanternRefl.Count)
                {
                    var r = _lanternRefl[j];
                    r.localPosition = new Vector3(Mathf.Cos(rad) * _lanternRadius, r.localPosition.y, Mathf.Sin(rad) * _lanternRadius);
                    if (r.name.StartsWith("Lantern reflection")) r.localRotation = Quaternion.Euler(90f, 0f, -_lanternDeg[j]);
                    r.localScale = _reflScale[j] * show;
                }
            }
            _lanternsSnapped = true;
        }

        private static float Yaw(Quaternion q)
        {
            var f = q * Vector3.forward;
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        private void Update()
        {
            if (_root == null || _current == null) return;
            RefreshStanding();
            if (_qaCameraCanvas && _labelCanvas != null && _labelCanvas.renderMode == RenderMode.ScreenSpaceOverlay && Camera.main != null)
            {
                // QA capture renders the camera into a texture, which an overlay canvas skips.
                _labelCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                _labelCanvas.worldCamera = Camera.main;
                _labelCanvas.planeDistance = 1.2f;
                if (_causeCanvas != null)
                {
                    _causeCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    _causeCanvas.worldCamera = Camera.main;
                    _causeCanvas.planeDistance = 1.25f;
                }
                if (_qaStar >= 7)
                    ShowLabel(_qaStar, LuckyLabelLines(QaReading(4, 3), _qaStar, 4, 3, 6), true);
            }
            float dt = Time.deltaTime;
            _sinceChange += dt;
            _boost *= Mathf.Exp(-dt * 1.8f);
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 0.45f);
            _angle = (_angle + DieTurn() + (ReducedMotion ? 0f : _boost * dt)) % 360f;
            _root.rotation = Quaternion.Euler(0f, -_angle, 0f) * Quaternion.Euler(90f, 0f, 0f);

            float breathe = 0.82f + 0.18f * Mathf.Sin(Time.time * 1.2f);
            float strength = _points >= 7 ? 0.62f : 0.42f;

            // The new star settles in, larger at first, and turns with the old one leaving.
            float inK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 1.1f, _sinceChange));
            _current.Scale = ReducedMotion ? 1f : Mathf.Lerp(1.28f, 1f, inK);
            _current.Alpha = inK;
            _current.Root.localRotation = Quaternion.identity;
            Paint(_current, (strength + 0.35f * _flash) * breathe * _current.Alpha);

            if (_leaving != null)
            {
                float outK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.7f, _sinceChange));
                _leaving.Scale = ReducedMotion ? 1f : Mathf.Lerp(1f, 0.55f, outK);
                _leaving.Root.localRotation = Quaternion.Euler(0f, 0f, ReducedMotion ? 0f : _sinceChange * 120f);
                Paint(_leaving, strength * breathe * (1f - outK));
                if (outK >= 1f) { DestroyLayer(_leaving); _leaving = null; }
            }

            var rc = StarColor(_points);
            float ringA = (0.06f + 0.30f * _flash) * breathe; // the gold circle of the seven lights is already there
            _ring.startColor = _ring.endColor = new Color(rc.r, rc.g, rc.b, ringA);

            // Hint lights travel slowly round the outside of the ring.
            _hintAlpha = Mathf.MoveTowards(_hintAlpha, _hintTarget, dt * 0.8f);
            if (_hintMat != null)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 2.1f);
                _hintMat.color = new Color(1f, 0.93f, 0.70f, _hintAlpha * pulse);
                int k = _hintOrbs.Count;
                if (View != null && View.DiceTransform != null)
                    _hintRoot.position = View.DiceTransform.position + Vector3.down * 0.32f;
                float baseA = ReducedMotion ? 0f : Time.time * 0.9f;
                for (int i = 0; i < k; i++)
                {
                    float a = baseA + i * Mathf.PI * 2f / k;
                    // They float a little above the grass, rising and falling one after another.
                    float bob = ReducedMotion ? 0f : 0.03f * Mathf.Sin(Time.time * 1.6f + i * 1.3f);
                    _hintOrbs[i].localPosition = new Vector3(Mathf.Cos(a) * 0.62f, bob, Mathf.Sin(a) * 0.62f);
                }
            }
        }
    }
}
