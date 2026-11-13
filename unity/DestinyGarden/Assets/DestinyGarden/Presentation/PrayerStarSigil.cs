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
        private Text _labelTitle, _labelSub;
        private Coroutine _labelRoutine;

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
            main.maxParticles = 600;
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
                    var grad = new Gradient();
                    var keys = new GradientColorKey[8];
                    for (int k = 0; k < 8; k++) keys[k] = new GradientColorKey(RainbowColors[k], k / 7f);
                    grad.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                    line.colorGradient = grad;
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
                    // Keep the gradient's colours; only the material carries the alpha.
                    line.startColor = new Color(1f, 1f, 1f, alpha);
                    line.endColor = new Color(1f, 1f, 1f, alpha);
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
            SetHint(LastFace(history));
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

        /// <summary>
        /// Reveals this roll's star: the face's note, the turn into the new star, its chord, the lucky
        /// number label, and light falling on the plants that received the gift.
        /// </summary>
        public IEnumerator PlayReveal(RollOutcome outcome, WorldState state)
        {
            var star = outcome?.Ecology?.PrayerStar;
            if (star == null) yield break;
            int previousFaceId = star.FaceSum > 0 ? star.FaceSum - outcome.FaceId : 0;
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
                    if (e.Kind != Lifecycle.ReturnedEvent) continue;
                    var at = View.CellTop(e.CellId);
                    for (int i = 0; i < 18; i++)
                        _glints.Emit(new ParticleSystem.EmitParams
                        {
                            position = at + new Vector3(UnityEngine.Random.Range(-0.25f, 0.25f), UnityEngine.Random.Range(0f, 0.5f), UnityEngine.Random.Range(-0.25f, 0.25f)),
                            velocity = ReducedMotion ? Vector3.zero : Vector3.up * UnityEngine.Random.Range(0.15f, 0.45f),
                            startColor = new Color(1f, 0.86f, 0.62f, 0.75f), startLifetime = UnityEngine.Random.Range(1.4f, 2.4f)
                        }, 1);
                }
            yield return PlayRevealCore(star.Points, outcome.FaceId, previousFaceId, tops);
        }

        private IEnumerator PlayRevealCore(int points, int faceId, int previousFaceId, List<Vector3> tops)
        {
            _hintTarget = 0f; // the old hint fades while the die speaks
            if (Audio != null) Audio.PlayFaceNote(faceId);
            // The die's answer runs down to the star as a thread of light.
            yield return Thread(points, 0.55f);
            Change(points);
            yield return new WaitForSeconds(0.55f);
            _flash = points >= 7 ? 1f : points == 6 ? 0.5f : 0.25f;
            if (Audio != null) Audio.PlayStarChord(points);
            EmitBurst(points);
            if (points >= 7)
            {
                string sub = points >= 8 ? "ラッキーナンバーに、陰陽と島の調和が重なった"
                    : faceId == LuckySeven.FaceId ? "七の目　・　偶然なら 1/7"
                    : $"前の目 {previousFaceId} と今の目 {faceId} で七　・　偶然なら 1/6";
                ShowLabel(points, sub, false);
            }
            yield return new WaitForSeconds(0.5f);
            int perPlant = points >= 8 ? 14 : points == 7 ? 10 : points == 6 ? 5 : 3;
            for (int i = 0; i < tops.Count; i++)
            {
                Shower(tops[i], points, perPlant);
                if (!ReducedMotion) yield return new WaitForSeconds(points >= 8 ? 0.05f : 0.09f);
            }
            yield return new WaitForSeconds(0.4f);
            SetHint(faceId); // lights for the face that would complete seven next time
        }

        private IEnumerator QaRollThenReveal(int points, int face, int prev, List<Vector3> tops)
        {
            // The die tumbles as in a roll (the star whirls with it), then the answer is revealed.
            yield return View.PlayOutcome(null, "light", null);
            yield return PlayRevealCore(points, face, prev, tops);
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
                var grad = new Gradient();
                grad.SetKeys(new[] { new GradientColorKey(col, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0.15f * fade, 0f), new GradientAlphaKey(0.9f * fade, 1f) });
                _thread.colorGradient = grad;
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
            if (total < 3f) return yaw * Gear;                     // resting: the slow turn about the vertical
            if (total > 40f) return 0f;                            // the snap back to rest
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
                else if (child.name == "Lantern reflection") _lanternRefl.Add(child);
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
                r.name = "Lantern reflection (prayer star)";
                _lanternRefl.Add(r.transform);
            }
            foreach (var l in lights)
            {
                _lanterns.Add(l);
                _lanternScale.Add(l.localScale);
                _lanternDeg.Add(Mathf.Atan2(l.localPosition.z, l.localPosition.x) * Mathf.Rad2Deg);
                _lanternShow.Add(1f);
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
                l.localScale = _lanternScale[j] * show;
                if (j < _lanternRefl.Count)
                {
                    var r = _lanternRefl[j];
                    r.localPosition = new Vector3(Mathf.Cos(rad) * _lanternRadius, r.localPosition.y, Mathf.Sin(rad) * _lanternRadius);
                    r.localRotation = Quaternion.Euler(90f, 0f, -_lanternDeg[j]);
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
            if (_qaCameraCanvas && _labelCanvas != null && _labelCanvas.renderMode == RenderMode.ScreenSpaceOverlay && Camera.main != null)
            {
                // QA capture renders the camera into a texture, which an overlay canvas skips.
                _labelCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                _labelCanvas.worldCamera = Camera.main;
                _labelCanvas.planeDistance = 1.2f;
                if (_qaStar >= 7)
                    ShowLabel(_qaStar, _qaStar == 7 ? "前の目 3 と今の目 4 で七　・　偶然なら 1/6"
                        : "ラッキーナンバーに、陰陽と島の調和が重なった", true);
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
            _current.Scale = Mathf.Lerp(1.28f, 1f, inK);
            _current.Alpha = inK;
            _current.Root.localRotation = Quaternion.identity;
            Paint(_current, (strength + 0.35f * _flash) * breathe * _current.Alpha);

            if (_leaving != null)
            {
                float outK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.7f, _sinceChange));
                _leaving.Scale = Mathf.Lerp(1f, 0.55f, outK);
                _leaving.Root.localRotation = Quaternion.Euler(0f, 0f, _sinceChange * 120f);
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
