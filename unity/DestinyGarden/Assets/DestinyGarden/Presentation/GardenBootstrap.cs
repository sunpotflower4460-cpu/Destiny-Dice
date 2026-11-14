using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using DestinyGarden.Infrastructure;
using UnityEngine;

namespace DestinyGarden.Presentation
{
    /// <summary>Composition root: journal + coordinator + view + UI + audio.</summary>
    public sealed class GardenBootstrap : MonoBehaviour
    {
        private GardenCoordinator _coordinator;
        private FileJournal _journal;
        private GardenView _view;
        private GardenUi _ui;
        private GardenAudio _audio;
        private SacredAtmosphere _atmosphere;
        private GardenWonders _wonders;
        private GardenLife _life;
        private IslandHarmony _islandHarmony;
        private PrayerStarSigil _sigil; // prayer star on the sea, geared to the die (prayer-star.v1)
        private int _harmonyNow = -1, _lastStar = -1;
        private readonly QualityMonitor _quality = new QualityMonitor();
        private float _vitalityNow;
        private bool _birdsNow;
        private Vector3 _homePos;
        private Quaternion _homeRot;
        private static bool s_skipTitle; // set when a new visitor reloads the scene: start straight on the new island
        private float? _qaYaw, _qaPitch, _qaDist;
        private bool _captureNoUi, _captureTitle;
        private int _captureW = 1600, _captureH = 900;
        private string _intentKind = "none";
        private string _intentValue;
        private bool _busy;

        private void Start()
        {
            string journalPath = Path.Combine(UnityEngine.Application.persistentDataPath,
                "destiny-garden", "journal.v1.bin");
            _journal = new FileJournal(journalPath, new SystemClock());
            if (_journal.Recovery != null) Debug.LogWarning("Destiny Garden journal recovered: " + _journal.Recovery);
            _coordinator = new GardenCoordinator(_journal, new LocalEntropy(), new GuidIdSource());

            var read = _journal.ReadAll();
            bool restored = false;
            if (read.ChainValid && read.Entries.Count > 0)
            {
                try
                {
                    _coordinator.Restore(read);
                    restored = _coordinator.State != null;
                }
                catch (Exception ex)
                {
                    // A record this version cannot rebuild never stops the game: the journal is kept as it is
                    // and a new garden begins after it.
                    Debug.LogException(ex);
                    _coordinator = new GardenCoordinator(_journal, new LocalEntropy(), new GuidIdSource());
                }
            }
            if (!restored) _coordinator.CreateRun();
            if (_coordinator.Phase == CoordinatorPhase.Ready || _coordinator.Phase == CoordinatorPhase.Resolved)
                _coordinator.Prepare();
            _intentKind = _coordinator.LastIntentKind ?? "none";
            _intentValue = _coordinator.LastIntentValue;

            // QA arguments read first (explicit launch arguments only): -dgSeed N fixes the island,
            // -dgYaw/-dgPitch/-dgDist set the view of a capture.
            int? seedOverride = null;
            string[] early = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < early.Length; i++)
            {
                if (early[i] == "-dgSeed" && int.TryParse(early[i + 1], out int sv)) seedOverride = sv;
                if (early[i] == "-dgYaw" && float.TryParse(early[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float yv)) _qaYaw = yv;
                if (early[i] == "-dgPitch" && float.TryParse(early[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float pv)) _qaPitch = pv;
                if (early[i] == "-dgDist" && float.TryParse(early[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dv)) _qaDist = dv;
            }
            // The island is a function of the saved run id: the same garden is always the same island.
            var islandSeed = IslandSeed.FromRunId(_coordinator.RunId, seedOverride);
            SkyClock.StartPhase = islandSeed.StartPhase;
            _view = gameObject.AddComponent<GardenView>();
            _view.Seed = islandSeed;
            _view.Build();
            if (UnityEngine.Application.isMobilePlatform)
            {
                // Phones (also in a mobile browser): the lighter picture from the start, and patience before stepping down.
                _view.Mobile = true;
                _quality.MinimumFps = 24f;
                _view.ApplyQuality(0);
            }
            _atmosphere = gameObject.AddComponent<SacredAtmosphere>();
            _atmosphere.View = _view;
            _atmosphere.Build();
            _wonders = gameObject.AddComponent<GardenWonders>();
            _wonders.View = _view;
            _wonders.Build();
            _life = gameObject.AddComponent<GardenLife>();
            _life.View = _view;
            _life.Build();
            _islandHarmony = gameObject.AddComponent<IslandHarmony>();
            _islandHarmony.View = _view;
            _islandHarmony.Build();
            _audio = gameObject.AddComponent<GardenAudio>();
            _sigil = gameObject.AddComponent<PrayerStarSigil>();
            _sigil.View = _view;
            _sigil.Audio = _audio;
            _sigil.Coordinator = _coordinator;
            _sigil.Build();
            _sigil.ShowSaved(HistoryProjector.Project(_journal.ReadAll().Entries, _coordinator.RunId));
            _ui = gameObject.AddComponent<GardenUi>();
            _ui.Audio = _audio;
            _ui.View = _view;
            _ui.OnRoll = () =>
            {
                // Rolling from the house (or a turned view) glides back to the garden, where the roll is seen.
                if (_view != null && _view.Rig != null && _view.Rig.IsHouseFocused) _view.Rig.ResetView();
                StartCoroutine(RollFlow());
            };
            _ui.OnIntentSelected = (kind, value) =>
            {
                if (_coordinator.Phase == CoordinatorPhase.IntentCommitted)
                {
                    _ui.ShowStatus("この一投の願いは記録済みです。");
                    return;
                }
                // A wish made (not withdrawn) is heard as a quiet prayer tone.
                if (kind != "none" && (kind != _intentKind || value != _intentValue) && _audio != null) _audio.PlayPrayer();
                _intentKind = kind; _intentValue = value;
                _ui.ShowIntentSelection(kind, value);
                ShowPreparedFaces();
                _ui.ShowStatus(null);
            };
            _ui.OnSiteSelected = (mode, siteId) =>
            {
                if (_busy || !TrySave(() => _coordinator.SetCareFocus(mode, siteId))) return;
                _ui.ShowSiteSelection(mode, siteId);
                ShowMagicDestination();
            };
            _ui.OnSwitchVisitor = () =>
            {
                if (_busy) return;
                // A new visitor gets a new garden on a different island: save it, then start the scene again.
                _coordinator.SwitchToNewVisitor();
                _coordinator.Prepare();
                s_skipTitle = true;
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            };
            _ui.OnResetView = () => { if (_view != null && _view.Rig != null) _view.Rig.ResetView(); };
            _ui.OnObserveCell = cell => { if (!_busy) _view.FocusObservedCell(cell); };
            _ui.OnReadMemory = key => GardenMemories.Project(_journal.ReadAll().Entries, _coordinator.RunId, key);
            _ui.OnViewHouse = () => _view.LookAtHouse();
            _ui.OnExport = ExportNow;
            _ui.OnAddLuckySeven = () =>
            {
                if (_busy) return;
                if (_coordinator.AddLuckySeven() == null) return;
                _audio.PlayLuckySeven();
                ShowPreparedFaces();
            };
            _ui.OnHideUi = () => StartCoroutine(HideUiTemporarily());
            _ui.OnOpenKeepsake = () => ShowKeepsakeCard(false);
            _ui.Build();
            _ui.ShowOpeningProgress(_coordinator.State.WorldRevision);
            _ui.ShowIntentSelection(_intentKind, _intentValue);
            _ui.SetPrayerDraft(_coordinator.Phase == CoordinatorPhase.IntentCommitted ? _coordinator.LastPrayerText : null);
            _ui.SetWishEditable(_coordinator.Phase != CoordinatorPhase.IntentCommitted);
            _ui.ShowSiteSelection(_coordinator.PreferenceMode, _coordinator.PreferenceSiteId);
            RefreshAll();
            _view.Flora.Snap();
            _view.SnapClock();
            // Begin seeing the whole bottle; glide in to the garden when the visitor begins.
            var cam = _view.ViewCamera;
            var rig = _view.Rig;
            var homePos = cam.transform.position;
            var homeRot = cam.transform.rotation;
            _homePos = homePos; _homeRot = homeRot;
            rig.Lock();
            if (s_skipTitle)
            {
                s_skipTitle = false;
                FrameOpening(cam, withTitle: false);
                _ui.ShowTitleCard(false);
                StartCoroutine(GlideCamera(rig, cam, homePos, homeRot, 3.2f));
            }
            else
            {
                FrameOpening(cam);
                _ui.ShowTitleCard(true, opening: true);
                _ui.OnTitleClosed = () =>
                {
                    _ui.OnTitleClosed = null;
                    StartCoroutine(GlideCamera(rig, cam, homePos, homeRot, 3.2f));
                };
            }

            // QA hook (explicit launch arguments only; never active in normal play):
            // -dgAutoRolls N performs N honest rolls through the coordinator,
            // -dgCapture PATH saves a screenshot and quits,
            // -dgWonders (with -dgCapture) previews butterflies, halo and the rainbow eight
            // for screenshots only; it never writes to the journal.
            string[] args = Environment.GetCommandLineArgs();
            string capturePath = null;
            int autoRolls = 0;
            bool wonders = Array.IndexOf(args, "-dgWonders") >= 0;
            _captureNoUi = Array.IndexOf(args, "-dgNoUi") >= 0; // QA capture only: hide the canvas
            _captureTitle = Array.IndexOf(args, "-dgTitle") >= 0;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-dgCapture") capturePath = args[i + 1];
                if (args[i] == "-dgAutoRolls") int.TryParse(args[i + 1], out autoRolls);
                // QA capture only: -dgCaptureSize WxH (default 1600x900), -dgTitle keeps the opening title card.
                if (args[i] == "-dgCaptureSize")
                {
                    var wh = args[i + 1].Split('x');
                    if (wh.Length == 2 && int.TryParse(wh[0], out int cw) && int.TryParse(wh[1], out int ch)) { _captureW = cw; _captureH = ch; }
                }
            }
            if (capturePath != null) StartCoroutine(CaptureRoutine(capturePath, autoRolls, wonders));
        }

        private static void FrameOpening(Camera cam, bool withTitle = true)
        {
            bool side = GardenUi.UseSideTitle(cam.aspect);
            bool stacked = withTitle && !side;
            var center = IslandTerrain.Center;
            float aspect = cam.aspect;
            float pullback = Mathf.Max(1f, 1.25f / aspect);
            var position = stacked
                ? new Vector3(center.x, Mathf.Lerp(12.6f, 22f, Mathf.InverseLerp(1.0f, 0.55f, aspect)),
                    center.y - 63f * pullback)
                : new Vector3(center.x - 2.7f, withTitle ? 12.1f : 19.25f, center.y - (withTitle ? 50f : 46f) * pullback);
            float shift = withTitle && side ? 12f * aspect / (16f / 9f) : 0f;
            float aimHeight = stacked ? 1.8f - Mathf.Max(0f, 1.15f - aspect) * 16f : withTitle ? 7.9f : 5.05f;
            var aim = new Vector3(center.x - shift, aimHeight, center.y);
            cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(aim - position, Vector3.up));
        }

        private static IEnumerator GlideCamera(CameraRig rig, Camera cam, Vector3 p1, Quaternion r1, float seconds)
        {
            Vector3 p0 = cam.transform.position;
            Quaternion r0 = cam.transform.rotation;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(p0, p1, k), Quaternion.Slerp(r0, r1, k));
                yield return null;
            }
            cam.transform.SetPositionAndRotation(p1, r1);
            rig.Unlock();
        }

        private void Update()
        {
            if (_view == null) return;
            if (_ui.IsOpening) FrameOpening(_view.ViewCamera);
            if (_audio != null) _audio.SetScene(_vitalityNow, _harmonyNow, _birdsNow, _view.Sky.Night);
            if (_quality.Feed(Time.unscaledDeltaTime)) _view.ApplyQuality(_quality.Level);
            if (_wonders != null) _wonders.ReducedMotion = _view.ReducedMotion;
            if (_life != null) _life.ReducedMotion = _view.ReducedMotion;
            if (_view.Flora != null) _view.Flora.ReducedMotion = _view.ReducedMotion;
            if (_islandHarmony != null) _islandHarmony.ReducedMotion = _view.ReducedMotion;
            if (_sigil != null) _sigil.ReducedMotion = _view.ReducedMotion;
        }

        /// <summary>Publishes the prepared faces, the Lucky Seven offer and the die's lucky state.</summary>
        private void ShowPreparedFaces(int rolledFaceId = 0)
        {
            bool prepared = _coordinator.Phase == CoordinatorPhase.Prepared;
            bool offered = prepared && _coordinator.LuckySevenOffered && !_coordinator.LuckySevenAdded;
            _ui.ShowLuckyOffer(offered);
            int wished = 0;
            if (_intentKind == "face" && int.TryParse(_intentValue, out int wf)) wished = wf;
            // The faces that would make the lucky number or a ゾロ目 on this roll (prayer-star gardens, before the roll).
            int last = _coordinator.RolledFaces.Count > 0 ? _coordinator.RolledFaces[_coordinator.RolledFaces.Count - 1] : 0;
            bool marks = _coordinator.UsesPrayerStar && rolledFaceId == 0 &&
                         (prepared || _coordinator.Phase == CoordinatorPhase.IntentCommitted);
            int lucky = marks ? PrayerStar.FaceCompletingSeven(last) : 0;
            int pair = marks && last >= 1 && last <= 6 ? last : 0;
            _ui.ShowFaces(_coordinator.CurrentFaces != null ? _coordinator.CurrentFaces.Faces : null, rolledFaceId, wished, lucky, pair,
                marks && _coordinator.LuckySevenAdded);
            _ui.ShowLuckyClue(_coordinator.RolledFaces.Count <= 4 ? lucky : 0);
            bool activePlan = prepared || _coordinator.Phase == CoordinatorPhase.IntentCommitted;
            _ui.ShowCarePlan(_coordinator.State, activePlan ? _coordinator.CurrentFaces : null, _coordinator.CurrentWeather);
            _view.ShowCareTarget(CareRules.Enabled(_coordinator.State) && activePlan && _coordinator.CurrentFaces != null ? CareTargets.Anchor(_coordinator.CurrentFaces) : -1);
            ShowMagicDestination();
            bool sevenUp = prepared && _coordinator.LuckySevenAdded;
            _view.SetLuckyDice(sevenUp);
            _wonders.SetLuckyHalo(sevenUp || offered);
        }

        private void ShowMagicDestination()
        {
            bool enabled = CareRules.Enabled(_coordinator.State);
            bool hasPlot = CareTargets.TryCell(_coordinator.PreferenceSiteId, out int cell);
            bool hasPublishedRound = _coordinator.Phase == CoordinatorPhase.Prepared || _coordinator.Phase == CoordinatorPhase.IntentCommitted;
            bool applied = hasPublishedRound && _coordinator.CurrentFaces?.ResolvedSiteId == _coordinator.PreferenceSiteId;
            _view.ShowQueuedPlot(enabled && hasPlot && !applied ? cell : -1);
            _ui.ShowMagicDestination(enabled, hasPlot ? cell : -1, applied);
        }

        /// <summary>
        /// How alive the island looks (0..1), derived only from the saved garden: rolls so far and
        /// flowers in bloom. The island's ground cover grows toward it.
        /// </summary>
        private float Vitality()
        {
            var state = _coordinator.State;
            int flowers = 0;
            foreach (var p in state.Plants) if (p.Stage == PlantStage.Flower) flowers++;
            // From bottle, soil and water to a living island: rolls (prayers), flowers and harmony.
            return 0.09f + 0.38f * Mathf.Min(1f, state.WorldRevision / 24f) + 0.30f * Mathf.Min(1f, flowers / 10f)
                   + 0.26f * Mathf.Max(0, _harmonyNow) / 8f;
        }

        /// <summary>
        /// Harmony as recorded with the last roll (ecology.v2); before any roll, read live with the same
        /// domain function. Drives the star on the sea, the light's yin/yang tint and the network.
        /// </summary>
        private void UpdateHarmony(List<RoundSummary> history, int residentKinds)
        {
            var rolled = new List<string>(_coordinator.RolledRecipes);
            int flowers = 0;
            foreach (var p in _coordinator.State.Plants) if (p.Stage == PlantStage.Flower) flowers++;
            var live = Harmony.Read(rolled, residentKinds, Flora.BloomingSpecies(_coordinator.State));
            int value = live.Value, star = live.Star;
            for (int i = history.Count - 1; i >= 0; i--)
                if (history[i].WorldRevision > 0 && history[i].Resolved)
                {
                    if (history[i].Harmony >= 0) { value = history[i].Harmony; star = history[i].Star; }
                    break;
                }
            _harmonyNow = value;
            var blooming = new List<string>();
            foreach (var t in Flora.All)
                foreach (var p in _coordinator.State.Plants)
                    if (p.Stage == PlantStage.Flower && p.Species == t.Id) { blooming.Add(t.LabelJa); break; }
            _ui.ShowComposition(blooming, value);
            _atmosphere.SetStar(0); // the star on the sea is now the prayer star, geared to the die (PrayerStarSigil)
            _view.SetBalance(live.Yin, live.Yang);
            _islandHarmony.ShowNetwork(_coordinator.State, value);
            _lastStar = star;
        }

        /// <summary>Residents follow the domain's Ecology.Presence over the saved state and rolled history.</summary>
        private void UpdateButterflies(List<RoundSummary> history)
        {
            string lastRecipe = _coordinator.RolledRecipes.Count > 0 ? _coordinator.RolledRecipes[_coordinator.RolledRecipes.Count - 1] : null;
            var presence = Ecology.Presence(_coordinator.State, _coordinator.CurrentWeather, new List<string>(_coordinator.RolledRecipes), lastRecipe);
            _life.ShowPresence(presence, _coordinator.State);
            var kinds = new List<string>();
            foreach (var v in presence) kinds.Add(v.Kind);
            _birdsNow = kinds.Contains(Ecology.Bird);
            UpdateHarmony(history, _coordinator.UsesConnectedHabitats ? CommunityRules.ResidentKinds(presence) : kinds.Count);
            var butterfly = presence.Find(v => v.Kind == Ecology.Butterfly);
            var tops = new List<Vector3>();
            if (butterfly != null)
                foreach (int cell in butterfly.Cells)
                {
                    var plant = _coordinator.State.PlantAt(cell);
                    if (plant != null) tops.Add(_view.PlantTip(plant));
                }
            _wonders.SetButterflies(butterfly != null, tops);
        }

        private IEnumerator CaptureRoutine(string path, int autoRolls, bool wonders)
        {
            _view.ReducedMotion = true;
            _ui.OnTitleClosed = null;
            if (!_captureTitle)
            {
                _ui.ShowTitleCard(false);
                _view.ViewCamera.transform.SetPositionAndRotation(_homePos, _homeRot);
                _view.Rig.Unlock();
            }
            if (_qaYaw.HasValue || _qaPitch.HasValue || _qaDist.HasValue)
                _view.Rig.SetView(_qaYaw ?? _view.Rig.HomeYaw, _qaPitch ?? _view.Rig.HomePitch, _qaDist ?? _view.Rig.HomeDist);
            RollOutcome last = null;
            for (int i = 0; i < autoRolls; i++)
            {
                if (_coordinator.Phase == CoordinatorPhase.Prepared)
                    _coordinator.CommitIntent("none", null);
                last = _coordinator.Roll();
                if (last == null) break;
                if (i + 1 < autoRolls) _coordinator.Prepare();
            }
            if (last != null) { _view.SetDiceFace(last.FaceId); _ui.ShowMessage(last.Message, last); }
            RefreshAll(last != null ? last.FaceId : 0);
            _view.Flora.Snap();
            _view.SnapClock();
            if (wonders)
            {
                var tops = new List<Vector3>();
                foreach (var plant in _coordinator.State.Plants)
                    tops.Add(_view.CellTop(plant.CellId) + Vector3.up * 0.9f);
                _wonders.SetButterflies(true, tops);
                _wonders.SetLuckyHalo(true);
                _view.SetLuckyDice(true);
                _view.ReducedMotion = false;
                StartCoroutine(_wonders.PlayRainbowEight());
                yield return new WaitForSeconds(4.2f);
            }
            yield return new WaitForSeconds(1.2f);
            yield return new WaitForEndOfFrame();
            // RenderTexture capture works headless where ScreenCapture does not.
            var cam = Camera.main;
            // The main UI's own canvas (the prayer star has canvases of its own; "the first canvas" could be one of them).
            var canvas = _ui != null ? _ui.GetComponentInChildren<UnityEngine.Canvas>(true) : FindFirstObjectByType<UnityEngine.Canvas>();
            if (canvas != null && _captureNoUi) canvas.enabled = false;
            if (canvas != null)
            {
                canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 5f;
            }
            yield return null;
            var rt = new RenderTexture(_captureW, _captureH, 24);
            cam.aspect = _captureW / (float)_captureH;
            if (_captureTitle) FrameOpening(cam);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;
            RenderTexture.active = rt;
            var tex = new Texture2D(_captureW, _captureH, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, _captureW, _captureH), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            yield return new WaitForSeconds(0.3f);
            UnityEngine.Application.Quit();
        }

        private void RefreshAll(int rolledFaceId = 0)
        {
            _view.Show(_coordinator.State, _coordinator.CurrentWeather);
            ShowPreparedFaces(rolledFaceId);
            var read = _journal.ReadAll();
            var history = HistoryProjector.Project(read.Entries, _coordinator.RunId);
            _history = history;
            var moments = GardenMoments.Recent(read.Entries, _coordinator.RunId, 12, includeMilestones: true);
            _ui.RefreshHistory(history, moments);
            var latest = history.FindLast(round => round.Resolved && round.WorldRevision > 0 && round.FaceId.HasValue);
            if (rolledFaceId == 0 && latest != null)
            {
                _view.SetDiceFace(latest.FaceId.Value);
                _ui.ShowSavedRoll(latest, moments.Find(moment => moment.RoundId == latest.RoundId));
            }
            _ui.ShowMomentReview(moments, latest?.RoundId, latest?.FortuneReason);
            var house = CottageGrowth.Project(_coordinator.State.WorldRevision, history);
            _view.ShowHouse(house);
            _ui.ShowHouse(house.Label);
            var discovered = new HashSet<string>();
            foreach (var r in history) foreach (var g in r.Germinated) discovered.Add(g);
            foreach (var p in _coordinator.State.Plants) if (p.Species != null) discovered.Add(p.Species);
            var residents = new HashSet<string>();
            foreach (var r in history) foreach (string kind in r.EcologyKinds) residents.Add(kind);
            _ui.ShowGuide(discovered, _coordinator.State, residents);
            _ui.ShowObservation(_coordinator.State);
            UpdateButterflies(history);
            float vitality = Vitality();
            _vitalityNow = vitality;
            _view.SetClock(_coordinator.State.WorldRevision, false);
            _ui.SetRollPulse(_coordinator.State.WorldRevision == 0);
            _view.Flora.SetVitality(vitality);
            _view.SetGroundVitality(vitality);
        }

        private IEnumerator RollFlow()
        {
            if (_busy || _ui.IsPanelOpen || _ui.IsOpening) yield break;
            // A preparation or wish that could not be saved last time is tried again here (nothing is drawn twice:
            // the coordinator resumes from what was saved).
            if (!TrySave(() =>
                {
                    if (_coordinator.Phase == CoordinatorPhase.Ready || _coordinator.Phase == CoordinatorPhase.Resolved)
                        _coordinator.Prepare();
                    if (_coordinator.Phase == CoordinatorPhase.Prepared)
                        _coordinator.CommitIntent(_intentKind, _intentValue, _ui.PrayerDraft);
                }))
            {
                ShowPreparedFaces();
                yield break;
            }
            if (_coordinator.Phase != CoordinatorPhase.IntentCommitted) yield break;
            _busy = true;
            _ui.SetWishEditable(false);
            _ui.SetRollEnabled(false);
            _ui.ShowStatus(null); // Clear feedback from the previous roll.
            _sigil.BeginRoll();

            RollOutcome outcome;
            try { outcome = _coordinator.Roll(); }
            catch (Exception ex)
            {
                // Nothing is drawn again: what was saved stays, and the next roll continues from it.
                Debug.LogException(ex);
                _ui.ShowStatus("記録を書けませんでした。もう一度振ると、保存済みの結果から続きます。");
                _busy = false;
                _ui.SetRollEnabled(true);
                yield break;
            }
            if (outcome == null)
            {
                _ui.ShowStatus("この回は成立しませんでした。記録は欠測のまま残ります。");
                _ui.SetPrayerDraft(null);
                TrySave(() => _coordinator.Prepare());
                RefreshAll();
                _ui.SetWishEditable(true);
                _busy = false;
                _ui.SetRollEnabled(true);
                yield break;
            }

            _ui.SetPrayerDraft(null);
            var moment = outcome.Moment;
            _audio.PlayBell();
            string element = RecipeCatalog.ElementOf(outcome.RecipeId).ToString().ToLowerInvariant();
            _audio.PlayNature(element);
            if (element == "wind") _view.Flora.Gust();
            // Land the saved face, then show the actual change in the garden.
            yield return _view.PlayOutcome(outcome.Message, element, outcome.Simulation, -1);
            _view.SetDiceFace(outcome.FaceId);
            _ui.ShowMessage(outcome.Message, outcome, moment);
            RefreshAll(outcome.FaceId);
            _view.SetLuckyDice(outcome.LuckySeven);
            StartCoroutine(_islandHarmony.PlayBlessing(outcome));
            StartCoroutine(_life.PlayGrowthMoments(outcome, _audio, moment));
            StartCoroutine(_life.PlayRollEffects(outcome.Ecology));
            StartCoroutine(_islandHarmony.PlayPulses(outcome));
            var sown = new List<Vector3>();
            foreach (var ev in outcome.Simulation.Events)
                if (ev.Kind == "SeedPlaced") sown.Add(_view.CellTop(ev.CellId) + Vector3.up * 0.05f);
            if (sown.Count > 0 && _view.DiceTransform != null)
                StartCoroutine(_life.PlaySowing(_view.DiceTransform.position + Vector3.up * 0.4f, sown, null));
            var spirit = outcome.Ecology != null ? outcome.Ecology.Visit(Ecology.Spirit) : null;
            if (spirit != null)
            {
                // A wish took shape: spirits carry nourishment to the plants the ecology step fed
                // (or, if all were in flower, to the plants the blessing reached).
                var cells = spirit.Cells.Count > 0 ? spirit.Cells : PrayerSpirits.ReachedPlantCells(outcome.Simulation);
                var tops = new List<Vector3>();
                foreach (int cell in cells)
                {
                    var plant = _coordinator.State.PlantAt(cell);
                    var tip = plant != null ? _view.PlantTip(plant) : _view.CellTop(cell);
                    tops.Add(tip + Vector3.up * 0.09f);
                }
                StartCoroutine(_wonders.PlayPrayerSpirits(tops));
            }
            if (moment != null && moment.CellId >= 0 && moment.CellId < GardenMapData.CellCount)
            {
                _view.FocusObservedCell(moment.CellId);
                if (_view.ReducedMotion) yield return new WaitForSecondsRealtime(1.65f);
                else yield return _view.Rig.FocusOnRoll(_view.CellTop(moment.CellId), false);
            }
            _sigil.Reveal(outcome, _coordinator.State);
            if (PrayerStar.IsRainbow(outcome.LuckySeven, outcome.Simulation, outcome.Ecology))
            {
                StartCoroutine(_wonders.PlayRainbowEight());
                _audio.PlayRainbow();
            }
            // The next round is prepared now; if it cannot be saved, the die stays usable and the next press retries.
            TrySave(() => _coordinator.Prepare());
            // Publish the next faces only after the end state is on screen.
            ShowPreparedFaces();
            if (outcome.LuckySeven) _view.SetLuckyDice(true);
            _view.Show(_coordinator.State, _coordinator.CurrentWeather);
            _ui.SetWishEditable(true);
            _busy = false;
            _ui.SetRollEnabled(true);
            if (GardenKeepsake.CardDue(_coordinator.State.WorldRevision)) StartCoroutine(KeepsakeAfterRoll());
        }

        private List<RoundSummary> _history;

        /// <summary>Runs a step that writes the journal; on failure the garden says so and play can go on.</summary>
        private bool TrySave(Action step)
        {
            try { step(); return true; }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _ui.ShowStatus("記録を書けませんでした。もう一度振ると、保存済みのところから続きます。");
                return false;
            }
        }

        /// <summary>The 48th roll (and every 48 after): the keepsake card follows once the roll has been seen.</summary>
        private IEnumerator KeepsakeAfterRoll()
        {
            int revision = _coordinator.State.WorldRevision;
            yield return new WaitForSecondsRealtime(3.4f);
            if (_busy || _ui.IsPanelOpen || _ui.IsOpening || _coordinator.State.WorldRevision != revision) yield break;
            ShowKeepsakeCard(true);
        }

        private void ShowKeepsakeCard(bool milestone)
        {
            var state = _coordinator.State;
            if (state == null) return;
            var history = _history ?? HistoryProjector.Project(_journal.ReadAll().Entries, _coordinator.RunId);
            var card = GardenKeepsake.BuildCard(PrayerStarSigil.CurrentName(_coordinator), state, history,
                CottageGrowth.Project(state.WorldRevision, history).Label, PrayerStarSigil.LuckyPlants(state));
            if (!_ui.ShowKeepsake(card) || !milestone || _audio == null) return;
            // The lucky scale, then the eight's chord: both measured sounds at their own levels.
            _audio.PlayLuckyScale(8);
            StartCoroutine(Later(0.9f, () => _audio.PlayStarChord(8)));
        }

        private static IEnumerator Later(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }

        private IEnumerator HideUiTemporarily()
        {
            // Every overlay (the main UI and the prayer star's words), and only those that were showing.
            var hidden = new List<Canvas>();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.enabled && c.isRootCanvas) { c.enabled = false; hidden.Add(c); }
            if (hidden.Count == 0) yield break;
            yield return new WaitForSeconds(4f);
            foreach (var c in hidden) if (c != null) c.enabled = true;
        }

        private void ExportNow()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // The browser sandbox gives the player no way to reach persistentDataPath files.
            _ui.ShowStatus("Web版は書き出し非対応です（記録はブラウザ内に保存）。");
            return;
#else
            try
            {
                string dir = Path.Combine(UnityEngine.Application.persistentDataPath, "destiny-garden", "exports");
                Directory.CreateDirectory(dir);
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                var read = _journal.ReadAll();
                var history = HistoryProjector.Project(read.Entries, _coordinator.RunId);

                var csv = new StringBuilder();
                csv.AppendLine("worldRevision,weatherId,siteId,faceId,recipeId,cardId,intentKind,intentValue,matchingFaces,intentMatched,totalWater,growthSum,message");
                foreach (var r in history)
                {
                    if (!r.Resolved || r.WorldRevision == 0) continue;
                    csv.AppendLine(string.Join(",", r.WorldRevision, Csv(r.WeatherId), Csv(r.SiteId),
                        r.FaceId.HasValue ? r.FaceId.Value.ToString() : "", Csv(r.RecipeId), Csv(r.CardId),
                        Csv(r.IntentKind), Csv(r.IntentValue), r.MatchingFaces, r.IntentMatched,
                        r.TotalWater, r.GrowthSum, Csv(r.MessageText)));
                }
                File.WriteAllText(Path.Combine(dir, $"garden-{stamp}.csv"), csv.ToString(), Encoding.UTF8);

                var json = new StringBuilder();
                foreach (var e in read.Entries)
                {
                    json.AppendLine(CanonicalJson.Write(e.ToHashPayload()));
                }
                File.WriteAllText(Path.Combine(dir, $"garden-journal-{stamp}.jsonl"), json.ToString(), Encoding.UTF8);
                _ui.ShowStatus("書き出しました: " + dir);
            }
            catch (Exception ex)
            {
                _ui.ShowStatus("書き出しに失敗しました: " + ex.Message);
            }
#endif
        }

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
    }
}
