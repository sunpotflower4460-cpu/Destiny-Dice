using System;
using System.Collections;
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
            _audio = gameObject.AddComponent<GardenAudio>();
            _ui = gameObject.AddComponent<GardenUi>();
            _ui.Audio = _audio;
            _ui.View = _view;
            _ui.OnRoll = () => StartCoroutine(RollFlow());
            _ui.OnIntentSelected = (kind, value) =>
            {
                _intentKind = kind; _intentValue = value;
                _ui.ShowIntentSelection(kind, value);
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
                _ui.ShowMessage(null, null);
                RefreshAll();
                _ui.ShowStatus("新しい庭を用意しました。");
            };
            _ui.OnExport = ExportNow;
            _ui.Build();
            _ui.ShowIntentSelection(_intentKind, _intentValue);
            _ui.ShowSiteSelection(_coordinator.PreferenceMode, _coordinator.PreferenceSiteId);
            RefreshAll();
            _ui.ShowStatus("公開された6面を見て、サイコロを振ってください。");

            // QA hook (explicit launch arguments only; never active in normal play):
            // -dgAutoRolls N performs N honest rolls through the coordinator,
            // -dgCapture PATH saves a screenshot and quits.
            string[] args = Environment.GetCommandLineArgs();
            string capturePath = null;
            int autoRolls = 0;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-dgCapture") capturePath = args[i + 1];
                if (args[i] == "-dgAutoRolls") int.TryParse(args[i + 1], out autoRolls);
            }
            if (capturePath != null) StartCoroutine(CaptureRoutine(capturePath, autoRolls));
        }

        private IEnumerator CaptureRoutine(string path, int autoRolls)
        {
            _view.ReducedMotion = true;
            RollOutcome last = null;
            for (int i = 0; i < autoRolls; i++)
            {
                if (_coordinator.Phase == CoordinatorPhase.Prepared)
                    _coordinator.CommitIntent("none", null);
                last = _coordinator.Roll();
                if (last == null) break;
                _coordinator.Prepare();
            }
            if (last != null) _ui.ShowMessage(last.Message, last);
            RefreshAll(last != null ? last.FaceId : 0);
            yield return new WaitForSeconds(1.2f);
            yield return new WaitForEndOfFrame();
            // RenderTexture capture works headless where ScreenCapture does not.
            var cam = Camera.main;
            var canvas = FindFirstObjectByType<UnityEngine.Canvas>();
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
            _ui.ShowFaces(_coordinator.CurrentFaces != null ? _coordinator.CurrentFaces.Faces : null, rolledFaceId);
            var read = _journal.ReadAll();
            _ui.RefreshHistory(HistoryProjector.Project(read.Entries, _coordinator.RunId));
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
            _ui.ShowMessage(outcome.Message, outcome);
            RefreshAll(outcome.FaceId);
            _coordinator.Prepare();
            // Publish the next faces only after the end state is on screen.
            _ui.ShowFaces(_coordinator.CurrentFaces.Faces, 0);
            string weather = _coordinator.CurrentWeather.LabelJa;
            string site = Sites.ById(_coordinator.CurrentFaces.ResolvedSiteId).LabelJa;
            _ui.ShowHeader(weather, site, _coordinator.State.WorldRevision);
            _view.Show(_coordinator.State, _coordinator.CurrentWeather);
            _busy = false;
            _ui.SetRollEnabled(true);
        }

        private void ExportNow()
        {
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
        }

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
    }
}
