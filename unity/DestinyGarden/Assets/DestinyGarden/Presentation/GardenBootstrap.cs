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
        private bool _butterfliesShown;
        private bool _captureNoUi;
        private string _intentKind = "none";
        private string _intentValue;
        private bool _busy;

        private void Start()
        {
            string journalPath = Path.Combine(UnityEngine.Application.persistentDataPath,
                "destiny-garden", "journal.v1.bin");
            _journal = new FileJournal(journalPath, new SystemClock());
            _coordinator = new GardenCoordinator(_journal, new LocalEntropy(), new GuidIdSource());

            var read = _journal.ReadAll();
            bool restored = false;
            if (read.ChainValid && read.Entries.Count > 0)
            {
                _coordinator.Restore(read);
                restored = _coordinator.State != null;
            }
            if (!restored) _coordinator.CreateRun();
            if (_coordinator.Phase == CoordinatorPhase.Ready || _coordinator.Phase == CoordinatorPhase.Resolved)
                _coordinator.Prepare();
            _intentKind = _coordinator.LastIntentKind ?? "none";
            _intentValue = _coordinator.LastIntentValue;

            _view = gameObject.AddComponent<GardenView>();
            _view.Build();
            _atmosphere = gameObject.AddComponent<SacredAtmosphere>();
            _atmosphere.Build();
            _wonders = gameObject.AddComponent<GardenWonders>();
            _wonders.View = _view;
            _wonders.Build();
            _life = gameObject.AddComponent<GardenLife>();
            _life.View = _view;
            _life.Build();
            _audio = gameObject.AddComponent<GardenAudio>();
            _ui = gameObject.AddComponent<GardenUi>();
            _ui.Audio = _audio;
            _ui.View = _view;
            _ui.OnRoll = () => StartCoroutine(RollFlow());
            _ui.OnIntentSelected = (kind, value) =>
            {
                _intentKind = kind; _intentValue = value;
                _ui.ShowIntentSelection(kind, value);
                ShowPreparedFaces();
                _ui.ShowStatus(kind == "none" ? "意図を添えずに振ります。" : "願いを添えました。配面は変わりません。");
            };
            _ui.OnSiteSelected = (mode, siteId) =>
            {
                _coordinator.SetCareFocus(mode, siteId);
                _ui.ShowSiteSelection(mode, siteId);
                _ui.ShowStatus("届け先は、次の一投から適用されます。");
            };
            _ui.OnSwitchVisitor = () =>
            {
                if (_busy) return;
                _coordinator.SwitchToNewVisitor();
                _coordinator.Prepare();
                _intentKind = "none"; _intentValue = null;
                _ui.ShowIntentSelection(_intentKind, _intentValue);
                _ui.ShowMessage(null, null);
                RefreshAll();
                _ui.ShowStatus("新しい庭を用意しました。");
            };
            _ui.OnExport = ExportNow;
            _ui.OnAddLuckySeven = () =>
            {
                if (_busy) return;
                if (_coordinator.AddLuckySeven() == null) return;
                ShowPreparedFaces();
                _ui.ShowStatus("七つめの面が加わった。どの面も 1/7。");
            };
            _ui.OnHideUi = () => StartCoroutine(HideUiTemporarily());
            _ui.Build();
            _ui.ShowIntentSelection(_intentKind, _intentValue);
            _ui.ShowSiteSelection(_coordinator.PreferenceMode, _coordinator.PreferenceSiteId);
            RefreshAll();
            _ui.ShowStatus(_coordinator.LuckySevenOffered && !_coordinator.LuckySevenAdded
                ? "七つめの面が、そっと現れている。"
                : "公開された面を見て、サイコロを振ってください。");
            _ui.ShowTitleCard(true);

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
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-dgCapture") capturePath = args[i + 1];
                if (args[i] == "-dgAutoRolls") int.TryParse(args[i + 1], out autoRolls);
            }
            if (capturePath != null) StartCoroutine(CaptureRoutine(capturePath, autoRolls, wonders));
        }

        private void Update()
        {
            if (_view == null) return;
            if (_atmosphere != null) _atmosphere.ReducedMotion = _view.ReducedMotion;
            if (_wonders != null) _wonders.ReducedMotion = _view.ReducedMotion;
            if (_life != null) _life.ReducedMotion = _view.ReducedMotion;
        }

        /// <summary>Publishes the prepared faces, the Lucky Seven offer and the die's lucky state.</summary>
        private void ShowPreparedFaces(int rolledFaceId = 0)
        {
            bool prepared = _coordinator.Phase == CoordinatorPhase.Prepared;
            bool offered = prepared && _coordinator.LuckySevenOffered && !_coordinator.LuckySevenAdded;
            _ui.ShowLuckyOffer(offered);
            int wished = 0;
            if (_intentKind == "face" && int.TryParse(_intentValue, out int wf)) wished = wf;
            _ui.ShowFaces(_coordinator.CurrentFaces != null ? _coordinator.CurrentFaces.Faces : null, rolledFaceId, wished);
            bool sevenUp = prepared && _coordinator.LuckySevenAdded;
            _view.SetLuckyDice(sevenUp);
            _wonders.SetLuckyHalo(sevenUp || offered);
        }

        /// <summary>One quiet line about what the garden's residents did; never explains the rules.</summary>
        private static string EcologyLine(RollOutcome outcome)
        {
            var eco = outcome.Ecology;
            if (eco == null) return null;
            bool Seeded(string kind) => eco.Effects.Exists(e => e.Seed && e.Kind == kind);
            bool Fed(string kind) => eco.Effects.Exists(e => !e.Seed && e.Kind == kind && e.Amount > 0);
            if (Fed(Ecology.Spirit)) return "……かすかな光が、花のほうへ降りていった。";
            if (Seeded(Ecology.Bird)) return "小鳥が、どこからか種を落としていった。";
            if (Seeded(Ecology.Butterfly)) return "蝶の運んだ花粉が、離れた土に落ちた。";
            if (Seeded(Ecology.Bee)) return "蜂の通ったあとに、小さな種がこぼれた。";
            if (Seeded(Ecology.Fluff)) return "綿毛が風に乗って、東の土へ。";
            if (Fed(Ecology.Mushroom)) return "岩陰の湿りに、きのこが顔を出した。";
            if (Fed(Ecology.Firefly)) return "苔の灯りに、蛍が寄ってくる。";
            if (eco.Has(Ecology.Fish)) return "あふれた水が海へ。魚の影が寄ってくる。";
            if (Fed(Ecology.Worm)) return "土の下で、何かが静かに働いている。";
            if (eco.Has(Ecology.Dragonfly)) return "水たまりの上を、トンボが渡る。";
            return null;
        }

        /// <summary>Residents follow the domain's Ecology.Presence over the saved state and rolled history.</summary>
        private void UpdateButterflies(List<RoundSummary> history)
        {
            var presence = Ecology.Presence(_coordinator.State, _coordinator.CurrentWeather, new List<string>(_coordinator.RolledRecipes), null);
            _life.ShowPresence(presence, _coordinator.State);
            var kinds = new List<string>();
            foreach (var v in presence) kinds.Add(v.Kind);
            _ui.ShowResidents(kinds);
            bool active = presence.Exists(v => v.Kind == Ecology.Butterfly);
            var tops = new List<Vector3>();
            foreach (var plant in _coordinator.State.Plants)
                if (plant.Stage == PlantStage.Flower)
                    tops.Add(_view.CellTop(plant.CellId) + Vector3.up * (0.80f + plant.Growth / 2500f * 0.44f));
            _wonders.SetButterflies(active, tops);
            if (active && !_butterfliesShown && !_busy) _ui.ShowStatus("どこからか、蝶が。");
            _butterfliesShown = active;
        }

        private IEnumerator CaptureRoutine(string path, int autoRolls, bool wonders)
        {
            _view.ReducedMotion = true;
            _ui.ShowTitleCard(false);
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
            var canvas = FindFirstObjectByType<UnityEngine.Canvas>();
            if (canvas != null && _captureNoUi) canvas.enabled = false;
            if (canvas != null)
            {
                canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 5f;
            }
            yield return null;
            var rt = new RenderTexture(1600, 900, 24);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            yield return new WaitForSeconds(0.3f);
            UnityEngine.Application.Quit();
        }

        private void RefreshAll(int rolledFaceId = 0)
        {
            _view.Show(_coordinator.State, _coordinator.CurrentWeather);
            string weather = _coordinator.CurrentWeather != null ? _coordinator.CurrentWeather.LabelJa : "";
            string site = _coordinator.CurrentFaces != null
                ? Sites.ById(_coordinator.CurrentFaces.ResolvedSiteId).LabelJa : "";
            _ui.ShowHeader(weather, site, _coordinator.State.WorldRevision);
            ShowPreparedFaces(rolledFaceId);
            var read = _journal.ReadAll();
            var history = HistoryProjector.Project(read.Entries, _coordinator.RunId);
            _ui.RefreshHistory(history);
            UpdateButterflies(history);
        }

        private IEnumerator RollFlow()
        {
            if (_busy) yield break;
            if (_coordinator.Phase == CoordinatorPhase.Prepared)
                _coordinator.CommitIntent(_intentKind, _intentValue);
            if (_coordinator.Phase != CoordinatorPhase.IntentCommitted) yield break;
            _busy = true;
            _ui.SetRollEnabled(false);

            var outcome = _coordinator.Roll();
            if (outcome == null)
            {
                _ui.ShowStatus("この回は成立しませんでした。記録は欠測のまま残ります。");
                _coordinator.Prepare();
                RefreshAll();
                _busy = false;
                _ui.SetRollEnabled(true);
                yield break;
            }

            _audio.PlayBell();
            string element = RecipeCatalog.ElementOf(outcome.RecipeId).ToString().ToLowerInvariant();
            _audio.PlayNature(element);
            // Presentation replays the saved outcome; the saved end state is shown after.
            yield return _view.PlayOutcome(outcome.Message, element, outcome.Simulation);
            foreach (var e in outcome.Simulation.Events)
            {
                if (e.Kind == "Bloomed" || e.Kind == "BudFormed" || e.Kind == "Germinated")
                {
                    _audio.PlayGrowthAccent();
                    break;
                }
            }
            _view.SetDiceFace(outcome.FaceId);
            _ui.ShowMessage(outcome.Message, outcome);
            RefreshAll(outcome.FaceId);
            _view.SetLuckyDice(outcome.LuckySeven);
            bool rainbow = LuckySeven.IsRainbow(outcome.LuckySeven, outcome.Simulation);
            if (rainbow) StartCoroutine(_wonders.PlayRainbowEight());
            StartCoroutine(_life.PlayRollEffects(outcome.Ecology));
            var spirit = outcome.Ecology != null ? outcome.Ecology.Visit(Ecology.Spirit) : null;
            if (spirit != null)
            {
                // A wish took shape: spirits carry nourishment to the plants the ecology step fed
                // (or, if all were in flower, to the plants the blessing reached).
                var cells = spirit.Cells.Count > 0 ? spirit.Cells : PrayerSpirits.ReachedPlantCells(outcome.Simulation);
                var tops = new List<Vector3>();
                foreach (int cell in cells) tops.Add(_view.CellTop(cell) + Vector3.up * 0.75f);
                StartCoroutine(_wonders.PlayPrayerSpirits(tops));
            }
            _coordinator.Prepare();
            // Publish the next faces only after the end state is on screen.
            ShowPreparedFaces();
            if (outcome.LuckySeven) _view.SetLuckyDice(true);
            string luckStatus = rainbow ? "空に、虹の八がかかった。"
                : outcome.LuckySeven ? "七の目。恵みが重なって届いた。"
                : EcologyLine(outcome);
            if (luckStatus != null) _ui.ShowStatus(luckStatus);
            else if (_coordinator.LuckySevenOffered) _ui.ShowStatus("七つめの面が、そっと現れている。");
            string weather = _coordinator.CurrentWeather.LabelJa;
            string site = Sites.ById(_coordinator.CurrentFaces.ResolvedSiteId).LabelJa;
            _ui.ShowHeader(weather, site, _coordinator.State.WorldRevision);
            _view.Show(_coordinator.State, _coordinator.CurrentWeather);
            _busy = false;
            _ui.SetRollEnabled(true);
        }

        private IEnumerator HideUiTemporarily()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) yield break;
            canvas.enabled = false;
            yield return new WaitForSeconds(4f);
            canvas.enabled = true;
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
