using System;
using System.Collections.Generic;
using System.Text;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace DestinyGarden.Presentation
{
    /// <summary>All UI is built in code. The UI only displays committed facts and
    /// forwards intents; it never draws dice or changes a published round.</summary>
    public sealed class GardenUi : MonoBehaviour
    {
        public Action OnRoll;
        public Action<string, string> OnIntentSelected; // kind, value
        public Action<string, string> OnSiteSelected;   // mode, siteId
        public Action OnSwitchVisitor;
        public Action OnExport;
        public Action OnHideUi;

        private Font _font;
        private Text _header;
        private Text _messageCard;
        private Text _status;
        private Button _rollButton;
        private readonly Button[] _faceChips = new Button[6];
        private readonly Dictionary<string, Button> _intentChips = new Dictionary<string, Button>();
        private readonly Dictionary<string, Button> _siteChips = new Dictionary<string, Button>();
        private GameObject _historyPanel;
        private Text _historyText;
        private GameObject _graphPanel;
        private Transform _graphBars;
        private Text _graphCaption;
        private GameObject _settingsPanel;
        public GardenAudio Audio;
        public GardenView View;

        private static readonly Color PanelColor = new Color(0.015f, 0.05f, 0.07f, 0.92f);
        private static readonly Color ChipColor = new Color(0.03f, 0.10f, 0.14f, 0.96f);
        private static readonly Color ChipActive = new Color(0.75f, 0.45f, 0.12f, 1.0f);
        private static readonly Color GoldText = new Color(0.018f, 0.010f, 0.002f);
        private static readonly Color TextColor = new Color(0.888f, 0.871f, 0.791f);

        public Font UiFont
        {
            get
            {
#if UNITY_WEBGL
                // Browsers expose no OS fonts to WebGL; use the bundled Japanese font (SIL OFL 1.1).
                if (_font == null) _font = Resources.Load<Font>("Fonts/NotoSerifJP-Regular");
#endif
                if (_font == null)
                {
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Noto Sans CJK JP", "Yu Gothic", "Arial" }, 24);
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }

        public void Build()
        {
            var canvasObj = new GameObject("Canvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            canvasObj.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            _header = MakeText(canvasObj.transform, new Vector2(0f, 1f), new Vector2(36f, -42f),
                new Vector2(640f, 44f), 25, TextAnchor.UpperLeft);
            AddBacking(_header, new Vector2(10f, 5f));
            _messageCard = MakeText(canvasObj.transform, new Vector2(0.5f, 1f), new Vector2(0f, -76f),
                new Vector2(940f, 64f), 28, TextAnchor.MiddleCenter);
            AddBacking(_messageCard, new Vector2(18f, 6f));
            _status = MakeText(canvasObj.transform, new Vector2(0.5f, 0f), new Vector2(0f, 144f),
                new Vector2(560f, 34f), 19, TextAnchor.MiddleCenter);
            AddBacking(_status, new Vector2(12f, 4f));

            // Face chips (tap = wish for that face number).
            for (int i = 0; i < 6; i++)
            {
                int face = i + 1;
                _faceChips[i] = MakeChip(canvasObj.transform, new Vector2(0f, 0f),
                    new Vector2(24f + i * 148f, 180f), new Vector2(140f, 52f), "", 19,
                    () => OnIntentSelected?.Invoke("face", face.ToString()));
            }
            MakeLabel(canvasObj.transform, new Vector2(24f, 236f), "今回の6面（押すとその目を願う。同じ面はその分出やすい）", 16);

            // Intent family chips.
            string[] intentKeys = { "none", "water", "wind", "light", "seed" };
            string[] intentLabels = { "意図なし", "水", "風", "光", "種" };
            for (int i = 0; i < intentKeys.Length; i++)
            {
                string key = intentKeys[i];
                _intentChips[key] = MakeChip(canvasObj.transform, new Vector2(0f, 0f),
                    new Vector2(24f + i * 118f, 100f), new Vector2(110f, 46f), intentLabels[i], 19,
                    () => OnIntentSelected?.Invoke(key == "none" ? "none" : "family", key == "none" ? null : key));
            }
            MakeLabel(canvasObj.transform, new Vector2(24f, 150f), "願う系統", 17);

            // Site chips (apply from the next preparation).
            string[] siteKeys = { "auto", "inlet", "shade", "meadow" };
            string[] siteLabels = { "おまかせ", "入口", "岩陰", "ひらけた土" };
            for (int i = 0; i < siteKeys.Length; i++)
            {
                string key = siteKeys[i];
                _siteChips[key] = MakeChip(canvasObj.transform, new Vector2(0f, 0f),
                    new Vector2(24f + i * 148f, 20f), new Vector2(140f, 46f), siteLabels[i], 19,
                    () => OnSiteSelected?.Invoke(key == "auto" ? "auto" : "fixed", key == "auto" ? null : key));
            }
            MakeLabel(canvasObj.transform, new Vector2(24f, 70f), "次からの届け先", 17);

            _rollButton = MakeChip(canvasObj.transform, new Vector2(0.5f, 0f),
                new Vector2(0f, 62f), new Vector2(300f, 78f), "サイコロを振る", 27,
                () => OnRoll?.Invoke());
            _rollButton.GetComponent<Image>().color = new Color(0.694f, 0.392f, 0.076f, 1.0f);
            _rollButton.GetComponentInChildren<Text>().color = GoldText;

            // Right-side menu.
            MakeChip(canvasObj.transform, new Vector2(1f, 1f), new Vector2(-96f, -60f), new Vector2(150f, 48f),
                "記録", 20, () => TogglePanel(_historyPanel));
            MakeChip(canvasObj.transform, new Vector2(1f, 1f), new Vector2(-96f, -118f), new Vector2(150f, 48f),
                "庭の歩み", 20, () => TogglePanel(_graphPanel));
            MakeChip(canvasObj.transform, new Vector2(1f, 1f), new Vector2(-96f, -176f), new Vector2(150f, 48f),
                "書き出し", 20, () => OnExport?.Invoke());
            MakeChip(canvasObj.transform, new Vector2(1f, 1f), new Vector2(-96f, -234f), new Vector2(150f, 48f),
                "次の来場者", 20, () => OnSwitchVisitor?.Invoke());
            MakeChip(canvasObj.transform, new Vector2(1f, 1f), new Vector2(-96f, -292f), new Vector2(150f, 48f),
                "設定", 20, () => TogglePanel(_settingsPanel));

            BuildPanels(canvasObj.transform);
        }

        private void AddBacking(Text text, Vector2 pad)
        {
            var bg = new GameObject("Backing");
            var parent = text.transform.parent;
            var siblingIndex = text.transform.GetSiblingIndex();
            bg.transform.SetParent(parent, false);
            bg.transform.SetSiblingIndex(siblingIndex);
            var img = bg.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.62f);
            var src = text.GetComponent<RectTransform>();
            var rect = bg.GetComponent<RectTransform>();
            rect.anchorMin = src.anchorMin; rect.anchorMax = src.anchorMax;
            rect.pivot = src.pivot;
            rect.anchoredPosition = src.anchoredPosition + new Vector2(
                (0.5f - src.pivot.x) * pad.x, (0.5f - src.pivot.y) * pad.y);
            rect.sizeDelta = src.sizeDelta + pad * 2f;
        }

        /// <summary>Hides all UI except a small restore chip (photo moment).</summary>
        public void SetUiVisible(bool visible, string restoreLabel = null)
        {
            foreach (Transform child in transform) child.gameObject.SetActive(visible);
        }

        private void BuildPanels(Transform canvas)
        {
            _historyPanel = MakePanel(canvas, new Vector2(880f, 560f));
            _historyText = MakeText(_historyPanel.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(820f, 500f), 19, TextAnchor.UpperLeft);
            _historyPanel.SetActive(false);

            _graphPanel = MakePanel(canvas, new Vector2(920f, 480f));
            var barsObj = new GameObject("Bars");
            barsObj.transform.SetParent(_graphPanel.transform, false);
            var barsRect = barsObj.AddComponent<RectTransform>();
            barsRect.anchorMin = barsRect.anchorMax = new Vector2(0.5f, 0.5f);
            barsRect.sizeDelta = new Vector2(840f, 300f);
            _graphBars = barsObj.transform;
            _graphCaption = MakeText(_graphPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -200f),
                new Vector2(840f, 60f), 18, TextAnchor.UpperCenter);
            _graphPanel.SetActive(false);

            _settingsPanel = MakePanel(canvas, new Vector2(520f, 340f));
            MakeLabel(_settingsPanel.transform, new Vector2(-220f, 110f), "音量", 20);
            var sliderObj = new GameObject("Volume");
            sliderObj.transform.SetParent(_settingsPanel.transform, false);
            var sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.anchoredPosition = new Vector2(30f, 110f);
            sliderRect.sizeDelta = new Vector2(260f, 28f);
            var slider = sliderObj.AddComponent<Slider>();
            var bg = new GameObject("BG"); bg.transform.SetParent(sliderObj.transform, false);
            var bgImg = bg.AddComponent<Image>(); bgImg.color = ChipColor;
            bg.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 12f);
            var fillArea = new GameObject("Fill"); fillArea.transform.SetParent(sliderObj.transform, false);
            var fillImg = fillArea.AddComponent<Image>(); fillImg.color = ChipActive;
            fillArea.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 12f);
            slider.fillRect = fillArea.GetComponent<RectTransform>();
            slider.minValue = 0; slider.maxValue = 1; slider.value = 0.8f;
            slider.onValueChanged.AddListener(v => { if (Audio != null) Audio.Volume = v; });

            MakeChip(_settingsPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(-110f, 30f),
                new Vector2(180f, 48f), "消音 切替", 19, () => { if (Audio != null) Audio.Muted = !Audio.Muted; });
            MakeChip(_settingsPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(110f, 30f),
                new Vector2(180f, 48f), "動きを抑える", 19, () => { if (View != null) View.ReducedMotion = !View.ReducedMotion; });
            MakeChip(_settingsPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(-110f, -70f),
                new Vector2(180f, 48f), "UIを隠す(4秒)", 19, () => OnHideUi?.Invoke());
            MakeChip(_settingsPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(110f, -70f),
                new Vector2(160f, 48f), "閉じる", 19, () => _settingsPanel.SetActive(false));
            _settingsPanel.SetActive(false);
        }

        private Sprite _rounded;
        private Sprite Rounded => _rounded ?? (_rounded = ProceduralArt.RoundedSprite());

        private GameObject MakePanel(Transform parent, Vector2 size)
        {
            var obj = new GameObject("Panel");
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = PanelColor;
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return obj;
        }

        private void TogglePanel(GameObject panel)
        {
            bool show = !panel.activeSelf;
            _historyPanel.SetActive(false);
            _graphPanel.SetActive(false);
            _settingsPanel.SetActive(false);
            panel.SetActive(show);
        }

        public void SetRollEnabled(bool enabled) => _rollButton.interactable = enabled;

        public void ShowHeader(string weatherLabel, string siteLabel, int revision)
        {
            _header.text = $"空: {weatherLabel}　届け先: {siteLabel}　{revision}投目まで記録";
        }

        public void ShowFaces(string[] faces, int rolledFaceId, int wishedFaceId = 0)
        {
            for (int i = 0; i < 6; i++)
            {
                var label = _faceChips[i].GetComponentInChildren<Text>();
                label.text = faces != null ? $"{i + 1} {RecipeCatalog.LabelJa(faces[i])}" : "";
                bool rolled = rolledFaceId == i + 1;
                bool wished = wishedFaceId == i + 1 && !rolled;
                _faceChips[i].GetComponent<Image>().color =
                    rolled ? ChipActive : wished ? new Color(0.05f, 0.35f, 0.38f, 0.98f) : ChipColor;
                label.color = rolled ? GoldText : TextColor;
            }
        }

        public void ShowIntentSelection(string kind, string value)
        {
            foreach (var kv in _intentChips)
            {
                bool active = (kind == "none" && kv.Key == "none") || (kind == "family" && kv.Key == value);
                kv.Value.GetComponent<Image>().color = active ? ChipActive : ChipColor;
                kv.Value.GetComponentInChildren<Text>().color = active ? GoldText : TextColor;
            }
        }

        public void ShowSiteSelection(string mode, string siteId)
        {
            foreach (var kv in _siteChips)
            {
                bool active = (mode == "auto" && kv.Key == "auto") || (mode == "fixed" && kv.Key == siteId);
                kv.Value.GetComponent<Image>().color = active ? ChipActive : ChipColor;
                kv.Value.GetComponentInChildren<Text>().color = active ? GoldText : TextColor;
            }
        }

        public void ShowMessage(SelectedMessage message, RollOutcome outcome)
        {
            if (message == null) { _messageCard.text = ""; return; }
            string card = "";
            if (message.CardId != null)
            {
                int idx = Array.IndexOf(OracleCatalogData.CardOrder, message.CardId);
                if (idx >= 0) card = "【" + OracleCatalogData.CardLabelsJa[idx] + "】";
            }
            _messageCard.text = outcome != null
                ? $"{outcome.FaceId}の目「{RecipeCatalog.LabelJa(outcome.RecipeId)}」　{card}{message.Text}"
                : card + message.Text;
        }

        public void ShowStatus(string text) => _status.text = text ?? "";

        public void RefreshHistory(List<RoundSummary> history)
        {
            var sb = new StringBuilder();
            sb.AppendLine("記録（新しい順）");
            for (int i = history.Count - 1; i >= 1 && i > history.Count - 16; i--)
            {
                var r = history[i];
                sb.AppendLine($"{r.WorldRevision}投目 {WeatherLabel(r.WeatherId)} {SiteLabel(r.SiteId)} " +
                    $"{(r.FaceId.HasValue ? r.FaceId.Value + "の目" : "")} {RecipeLabel(r.RecipeId)}");
                if (!string.IsNullOrEmpty(r.MessageText)) sb.AppendLine("　" + r.MessageText);
            }
            if (history.Count <= 1) sb.AppendLine("まだ記録がありません。");
            _historyText.text = sb.ToString();
            RefreshGraph(history);
        }

        private void RefreshGraph(List<RoundSummary> history)
        {
            foreach (Transform child in _graphBars) Destroy(child.gameObject);
            if (history.Count == 0) { _graphCaption.text = ""; return; }
            long maxWater = 1, maxGrowth = 1;
            foreach (var r in history)
            {
                if (r.TotalWater > maxWater) maxWater = r.TotalWater;
                if (r.GrowthSum > maxGrowth) maxGrowth = r.GrowthSum;
            }
            int n = Math.Min(history.Count, 42);
            float width = 840f / n;
            for (int i = 0; i < n; i++)
            {
                var r = history[history.Count - n + i];
                MakeBar(i * width - 420f, width * 0.38f, (float)r.TotalWater / maxWater * 260f,
                    new Color(0.25f, 0.70f, 0.90f));
                MakeBar(i * width - 420f + width * 0.44f, width * 0.38f, (float)r.GrowthSum / maxGrowth * 260f,
                    new Color(0.45f, 0.75f, 0.40f));
            }
            // Honest summary: actual matches next to the chance baseline.
            int compared = 0, matched = 0, baselineNum = 0;
            foreach (var r in history)
            {
                if (!r.Resolved || r.IntentKind == null || r.IntentKind == "none") continue;
                compared++;
                baselineNum += r.MatchingFaces;
                if (r.IntentMatched) matched++;
            }
            _graphCaption.text = "青: 庭の水の総量　緑: 成長の合計" +
                (compared > 0
                    ? $"\n意図の一致 {matched}/{compared}回（偶然なら平均 {(baselineNum / 6f):0.0}回）"
                    : "\n意図を添えた記録はまだありません。");
        }

        private void MakeBar(float x, float width, float height, Color color)
        {
            var obj = new GameObject("Bar");
            obj.transform.SetParent(_graphBars, false);
            var img = obj.AddComponent<Image>();
            img.color = color;
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(x, -150f);
            rect.sizeDelta = new Vector2(Mathf.Max(2f, width), Mathf.Max(1f, height));
        }

        private static string WeatherLabel(string id) =>
            id == "Cloudy" ? "薄曇り" : id == "Drizzle" ? "小雨" : "晴れ";

        private static string SiteLabel(string id) =>
            id == "shade" ? "岩陰" : id == "meadow" ? "ひらけた土" : "入口";

        private static string RecipeLabel(string id) => id == null ? "" : RecipeCatalog.LabelJa(id);

        private Text MakeText(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
        {
            var obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            text.font = UiFont;
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = TextColor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return text;
        }

        private void MakeLabel(Transform parent, Vector2 posFromBottomLeft, string label, int size)
        {
            var text = MakeText(parent, new Vector2(0f, 0f), posFromBottomLeft + new Vector2(0f, 0f),
                new Vector2(500f, 30f), size, TextAnchor.LowerLeft);
            text.text = label;
            text.color = new Color(0.80f, 0.85f, 0.82f, 0.9f);
        }

        private Button MakeChip(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string label,
            int fontSize, Action onClick)
        {
            var obj = new GameObject("Chip_" + label);
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = ChipColor;
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var button = obj.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick?.Invoke());
            var text = MakeText(obj.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size, fontSize, TextAnchor.MiddleCenter);
            text.text = label;
            return button;
        }
    }
}
