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
        public Action OnAddLuckySeven;
        public Action OnResetView;

        private Font _font;
        private Text _header;
        private Text _messageCard;
        private Text _status;
        private Button _rollButton;
        private readonly Button[] _faceChips = new Button[7];
        private string _luckyChipMode = "hidden"; // hidden | offer | face
        private Button _wishToggle;
        private GameObject _drawer;
        private static float FaceWidth(int i) => i == 6 ? 204f : 128f;

        /// <summary>Centres the visible face pills as one row (6, or 7 with the Lucky Seven).</summary>
        private void LayoutFaces(int count)
        {
            const float gap = 10f;
            float total = 0f;
            for (int i = 0; i < count; i++) total += FaceWidth(i) + (i > 0 ? gap : 0f);
            float x = -total / 2f;
            for (int i = 0; i < 7; i++)
            {
                var rect = _faceChips[i].GetComponent<RectTransform>();
                if (i < count)
                {
                    rect.anchoredPosition = new Vector2(x + FaceWidth(i) / 2f, 148f);
                    x += FaceWidth(i) + gap;
                }
            }
        }
        private Text _facesLabel;
        private GameObject _titleCard;
        private readonly Dictionary<string, Button> _intentChips = new Dictionary<string, Button>();
        private readonly Dictionary<string, Button> _siteChips = new Dictionary<string, Button>();
        private GameObject _observationPanel;
        private Text _observationText;
        private WorldState _observationState;
        private string _observationHabitat;
        private readonly Dictionary<string, Button> _observationChips = new Dictionary<string, Button>();
        private GameObject _historyPanel;
        private Text _historyText;
        private GameObject _graphPanel;
        private Transform _graphBars;
        private Text _graphCaption;
        private GameObject _settingsPanel;
        public GardenAudio Audio;
        public GardenView View;

        // Glass palette: translucent deep water, pale gold for what is chosen.
        private static readonly Color PanelColor = new Color(0.012f, 0.035f, 0.055f, 0.66f);
        private static readonly Color ChipColor = new Color(0.02f, 0.05f, 0.075f, 0.46f);
        private static readonly Color ChipActive = new Color(0.86f, 0.70f, 0.40f, 0.92f);
        private static readonly Color GoldText = new Color(0.07f, 0.045f, 0.01f);
        private static readonly Color Hairline = new Color(1f, 0.93f, 0.78f, 0.16f);
        private static readonly Color GoldLine = new Color(1f, 0.82f, 0.48f, 0.85f);
        private static readonly Color TextColor = new Color(0.888f, 0.871f, 0.791f);
        private static readonly Color WishOutline = new Color(0.45f, 1f, 0.95f, 1f);
        private static readonly Color LuckyGold = new Color(0.95f, 0.80f, 0.48f, 0.88f);

        /// <summary>One colour per blessing, shared by face chips and wish chips.</summary>
        public static Color ElementColor(string element)
        {
            switch (element)
            {
                case "water": return new Color(0.05f, 0.16f, 0.29f, 0.72f);
                case "wind": return new Color(0.05f, 0.19f, 0.16f, 0.72f);
                case "light": return new Color(0.26f, 0.21f, 0.07f, 0.72f);
                case "seed": return new Color(0.21f, 0.13f, 0.06f, 0.72f);
                default: return ChipColor;
            }
        }

        /// <summary>Bright hairline for a blessing, so colour reads as an edge of light, not a block.</summary>
        public static Color ElementLine(string element)
        {
            switch (element)
            {
                case "water": return new Color(0.45f, 0.78f, 1f, 0.55f);
                case "wind": return new Color(0.50f, 0.95f, 0.78f, 0.55f);
                case "light": return new Color(1f, 0.88f, 0.45f, 0.55f);
                case "seed": return new Color(0.95f, 0.70f, 0.45f, 0.55f);
                default: return Hairline;
            }
        }

        private static void SetLine(Button chip, Color color, float width)
        {
            var o = chip.GetComponent<Outline>();
            if (o == null) return;
            o.effectColor = color;
            o.effectDistance = new Vector2(width, -width);
        }

        private static string ElementOfRecipe(string recipeId) =>
            RecipeCatalog.ElementOf(recipeId).ToString().ToLowerInvariant();

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

            // Glass vessel edge: drawn first so every control sits above it; never takes clicks.
            var glass = new GameObject("GlassEdge");
            glass.transform.SetParent(canvasObj.transform, false);
            var glassImg = glass.AddComponent<RawImage>();
            glassImg.texture = ProceduralArt.GlassEdge();
            glassImg.raycastTarget = false;
            var glassRect = glass.GetComponent<RectTransform>();
            glassRect.anchorMin = Vector2.zero; glassRect.anchorMax = Vector2.one;
            glassRect.offsetMin = Vector2.zero; glassRect.offsetMax = Vector2.zero;
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            _header = MakeText(canvasObj.transform, new Vector2(0f, 1f), new Vector2(36f, -42f),
                new Vector2(640f, 44f), 25, TextAnchor.UpperLeft);
            AddBacking(_header, new Vector2(10f, 5f));
            _messageCard = MakeText(canvasObj.transform, new Vector2(0.5f, 1f), new Vector2(0f, -104f),
                new Vector2(940f, 76f), 27, TextAnchor.MiddleCenter);
            AddBacking(_messageCard, new Vector2(18f, 6f));
            _status = MakeText(canvasObj.transform, new Vector2(0.5f, 0f), new Vector2(0f, 214f),
                new Vector2(720f, 30f), 18, TextAnchor.MiddleCenter);
            AddBacking(_status, new Vector2(12f, 4f));

            // Published faces: a slim centred row (tap = wish for that face). Laid out in ShowFaces.
            for (int i = 0; i < 7; i++)
            {
                int face = i + 1;
                Action click = () => OnIntentSelected?.Invoke("face", face.ToString());
                if (face == 7) click = () =>
                {
                    if (_luckyChipMode == "offer") OnAddLuckySeven?.Invoke();
                    else if (_luckyChipMode == "face") OnIntentSelected?.Invoke("face", "7");
                };
                _faceChips[i] = MakeChip(canvasObj.transform, new Vector2(0.5f, 0f),
                    new Vector2(0f, 148f), new Vector2(FaceWidth(i), 46f), "", 16, click);
            }
            _faceChips[6].gameObject.SetActive(false);
            _facesLabel = MakeText(canvasObj.transform, new Vector2(0.5f, 0f), new Vector2(0f, 118f),
                new Vector2(900f, 24f), 14, TextAnchor.MiddleCenter);
            _facesLabel.color = new Color(0.86f, 0.92f, 0.90f, 0.85f);

            _rollButton = MakeChip(canvasObj.transform, new Vector2(0.5f, 0f),
                new Vector2(0f, 36f), new Vector2(300f, 72f), "サイコロを振る", 26,
                () => OnRoll?.Invoke());
            _rollButton.GetComponent<Image>().color = new Color(0.035f, 0.06f, 0.08f, 0.62f);
            _rollButton.GetComponentInChildren<Text>().color = new Color(1f, 0.89f, 0.64f);
            SetLine(_rollButton, GoldLine, 2f);

            // Wishes and destination live in a drawer, out of sight until wanted.
            _wishToggle = MakeChip(canvasObj.transform, new Vector2(0.5f, 0f), new Vector2(-268f, 50f),
                new Vector2(196f, 44f), "願いをかける", 17, () => _drawer.SetActive(!_drawer.activeSelf));
            _drawer = new GameObject("WishDrawer");
            _drawer.transform.SetParent(canvasObj.transform, false);
            var drawerImg = _drawer.AddComponent<Image>();
            drawerImg.sprite = Rounded;
            drawerImg.type = Image.Type.Sliced;
            drawerImg.color = new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.66f);
            var drawerRect = _drawer.GetComponent<RectTransform>();
            drawerRect.anchorMin = drawerRect.anchorMax = new Vector2(0f, 0f);
            drawerRect.pivot = new Vector2(0f, 0f);
            drawerRect.anchoredPosition = new Vector2(20f, 236f);
            drawerRect.sizeDelta = new Vector2(660f, 208f);
            var drawer = _drawer.transform;

            string[] intentKeys = { "none", "water", "wind", "light", "seed" };
            string[] intentLabels = { "意図なし", "水", "風", "光", "種" };
            for (int i = 0; i < intentKeys.Length; i++)
            {
                string key = intentKeys[i];
                _intentChips[key] = MakeChip(drawer, new Vector2(0f, 0f),
                    new Vector2(16f + i * 126f, 112f), new Vector2(118f, 44f), intentLabels[i], 18,
                    () => OnIntentSelected?.Invoke(key == "none" ? "none" : "family", key == "none" ? null : key));
            }
            MakeLabel(drawer, new Vector2(16f, 166f), "願う恵み（面の色と同じ）", 16);

            string[] siteKeys = { "auto", "inlet", "shade", "meadow" };
            string[] siteLabels = { "おまかせ", "入口", "岩陰", "ひらけた土" };
            for (int i = 0; i < siteKeys.Length; i++)
            {
                string key = siteKeys[i];
                _siteChips[key] = MakeChip(drawer, new Vector2(0f, 0f),
                    new Vector2(16f + i * 158f, 14f), new Vector2(150f, 44f), siteLabels[i], 18,
                    () => OnSiteSelected?.Invoke(key == "auto" ? "auto" : "fixed", key == "auto" ? null : key));
            }
            MakeLabel(drawer, new Vector2(16f, 66f), "次からの届け先", 16);
            _drawer.SetActive(false);

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
            MakeChip(canvasObj.transform, new Vector2(1f, 1f), new Vector2(-96f, -350f), new Vector2(150f, 48f),
                "遊び方", 20, () => ShowTitleCard(true));
            MakeChip(canvasObj.transform, new Vector2(1f, 1f), new Vector2(-96f, -408f), new Vector2(150f, 48f),
                "図鑑", 20, () => TogglePanel(_guidePanel));
            MakeChip(canvasObj.transform, new Vector2(1f, 1f), new Vector2(-96f, -466f), new Vector2(150f, 48f),
                "庭を観察", 18, () => TogglePanel(_observationPanel));
            MakeChip(canvasObj.transform, new Vector2(1f, 1f), new Vector2(-96f, -524f), new Vector2(150f, 48f),
                "視点を戻す", 18, () => OnResetView?.Invoke());

            BuildGoals(canvasObj.transform);
            BuildPanels(canvasObj.transform);
            BuildTitleCard(canvasObj.transform);
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
            img.color = new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.34f);
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
            _historyText = MakeScrollableText(_historyPanel.transform, new Vector2(820f, 500f), 19);
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
            _composition = MakeText(_graphPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 205f),
                new Vector2(860f, 56f), 17, TextAnchor.UpperCenter);
            _composition.color = new Color(1f, 0.92f, 0.76f);
            _graphPanel.SetActive(false);

            _guidePanel = MakePanel(canvas, new Vector2(1000f, 600f));
            _guideText = MakeScrollableText(_guidePanel.transform, new Vector2(940f, 560f), 18);
            _guideText.supportRichText = true;
            _guidePanel.SetActive(false);

            _observationPanel = MakePanel(canvas, new Vector2(1000f, 600f));
            var labels = new List<string> { "全体" }; labels.AddRange(EcologyInspection.Habitats);
            for (int i = 0; i < labels.Count; i++)
            {
                string label = labels[i];
                _observationChips[label] = MakeChip(_observationPanel.transform, new Vector2(0.5f, 1f),
                    new Vector2(-375f + 150f * i, -36f), new Vector2(140f, 40f), label, 17, () =>
                    { _observationHabitat = label == "全体" ? null : label; RenderObservation(); });
            }
            _observationText = MakeScrollableText(_observationPanel.transform, new Vector2(940f, 492f), 18);
            _observationText.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -24f);
            _observationPanel.SetActive(false);

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

        private Text MakeScrollableText(Transform parent, Vector2 size, int fontSize)
        {
            var viewport = new GameObject("ScrollableText", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(parent, false);
            var rect = viewport.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            viewport.GetComponent<Image>().color = Color.clear;
            var text = MakeText(viewport.transform, new Vector2(0.5f, 1f), Vector2.zero, size, fontSize, TextAnchor.UpperLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            text.rectTransform.sizeDelta = new Vector2(0f, size.y);
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.15f;
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = rect; scroll.content = text.rectTransform;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;
            return text;
        }

        private static void SetScrollableText(Text text, string value)
        {
            text.text = value;
            var scroll = text.GetComponentInParent<ScrollRect>(true);
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(scroll.viewport.rect.height, text.preferredHeight + 20f));
            scroll.verticalNormalizedPosition = 1f;
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
            if (_guidePanel != null) _guidePanel.SetActive(false);
            if (_observationPanel != null) _observationPanel.SetActive(false);
            panel.SetActive(show);
        }

        public void SetRollEnabled(bool enabled) => _rollButton.interactable = enabled;

        private bool _rollPulse = true;
        /// <summary>A gentle breathing of the roll button until the first roll (so a new visitor knows where to begin).</summary>
        public void SetRollPulse(bool on)
        {
            _rollPulse = on;
            if (!on && _rollButton != null) _rollButton.transform.localScale = Vector3.one;
        }

        public void ShowHeader(string weatherLabel, string siteLabel, int revision)
        {
            _header.text = $"空: {weatherLabel}　届け先: {siteLabel}　{revision}投目まで記録";
        }

        public void ShowFaces(string[] faces, int rolledFaceId, int wishedFaceId = 0)
        {
            int n = faces != null ? faces.Length : 6;
            LayoutFaces(n == 7 || _luckyChipMode == "offer" ? 7 : 6);
            _facesLabel.text = n == 7
                ? "7面目は「幸運の七」（その恵みが2倍）。どの面も 1/7"
                : "押した面に願いをかける・同じ恵みの面が多いほど出やすい";
            for (int i = 0; i < 7; i++)
            {
                var chip = _faceChips[i];
                var label = chip.GetComponentInChildren<Text>();
                bool present = faces != null && i < faces.Length;
                if (i == 6)
                {
                    if (present) _luckyChipMode = "face";
                    chip.gameObject.SetActive(_luckyChipMode != "hidden");
                    if (_luckyChipMode == "offer")
                    {
                        label.text = "★ 七つめの面を加える";
                        chip.GetComponent<Image>().color = LuckyGold;
                        label.color = GoldText;
                        SetLine(chip, GoldLine, 2f);
                        continue;
                    }
                    if (!present) continue;
                }
                label.text = present
                    ? (i == 6 ? $"7 ★{RecipeCatalog.LabelJa(faces[i])}×2" : $"{i + 1} {RecipeCatalog.LabelJa(faces[i])}")
                    : "";
                bool rolled = rolledFaceId == i + 1;
                bool wished = wishedFaceId == i + 1 && !rolled;
                string el = present ? ElementOfRecipe(faces[i]) : null;
                chip.GetComponent<Image>().color = rolled ? ChipActive : present ? ElementColor(el) : ChipColor;
                if (wished) SetLine(chip, WishOutline, 2.5f);
                else if (i == 6 && present) SetLine(chip, GoldLine, 1.5f);
                else SetLine(chip, present ? ElementLine(el) : Hairline, 1f);
                label.color = rolled ? GoldText : i == 6 ? new Color(1f, 0.88f, 0.55f) : TextColor;
            }
        }

        /// <summary>Shows or hides the Lucky Seven offer chip (only before the seventh face is added).</summary>
        public void ShowLuckyOffer(bool offered)
        {
            _luckyChipMode = offered ? "offer" : "hidden";
            _faceChips[6].gameObject.SetActive(offered);
            if (offered)
            {
                _faceChips[6].GetComponentInChildren<Text>().text = "★ 七つめの面を加える";
                _faceChips[6].GetComponent<Image>().color = LuckyGold;
                _faceChips[6].GetComponentInChildren<Text>().color = GoldText;
                SetLine(_faceChips[6], GoldLine, 2f);
            }
        }

        private void Update()
        {
            if (_rollPulse && _rollButton != null)
                _rollButton.transform.localScale = Vector3.one * (1f + 0.035f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f)));
            if (_luckyChipMode == "offer" && _faceChips[6] != null)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                _faceChips[6].GetComponent<Image>().color = Color.Lerp(LuckyGold, new Color(1f, 0.93f, 0.75f, 0.95f), k);
            }
        }

        private Text _residents;

        /// <summary>The garden's presence: residents living in the garden right now (no explanations).</summary>
        private void BuildGoals(Transform canvas)
        {
            _residents = MakeText(canvas, new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(500f, 64f), 17,
                TextAnchor.UpperLeft);
            _residents.supportRichText = true;
            AddBacking(_residents, new Vector2(14f, 10f));
            ShowResidents(null);
        }

        public static string ResidentLabelJa(string kind) => EcologyObservations.ResidentLabel(kind);

        private int _harmony = -1;
        private GameObject _guidePanel;
        private Text _guideText;

        /// <summary>Field guide: the species this garden has met, described from their rule data.</summary>
        public void ShowGuide(ICollection<string> discovered, WorldState state = null, ICollection<string> residents = null)
        {
            if (_guideText == null) return;
            var sb = new StringBuilder();
            bool living = state != null && state.Ecosystem != null;
            int total = living ? Flora.All.Length : Flora.LegacySpeciesCount;
            sb.AppendLine($"<b>庭の図鑑</b>　この庭に現れた花 {discovered.Count}/{total}　<color=#9aa8a4>上下にスクロール</color>");
            if (living)
            {
                sb.AppendLine("<color=#a8e6c8>" + EcologyObservations.HabitatSummary(state) + "</color>\n");
                sb.AppendLine("<b>つながりを育てる</b>\n花に昆虫が訪れると受粉し、数投かけて実ります。種は親と同じ種類を受け継ぎ、合う土へ。\n花は休み、寿命を終えた株は落ち葉になり、分解者が次の芽の養分へ戻します。\n水辺は湿りが続くと、草木の陰は背の高い株が育つと生まれます。投数で巡る四つの時期も、結実の量に影響します。\n");
                if (state.Ecosystem.Succession != null)
                    sb.AppendLine("土の中に残った種は、空き地や湿り、光、養分を待ちます。サクラソウ・スミレ・アネモネは休息の頃を経てから芽吹きます。\n蜂と蝶は花蜜を使い、受粉を待つ花と、長く訪れていない花へ巡ります。『庭を観察』から株や種の今を眺められます。\n");
                if (state.Ecosystem.Succession?.Community != null)
                    sb.AppendLine("蝶・テントウムシ・ハナアブ・トンボ・カエルは、条件が続いた住み場所に定着します。卵や幼生には食べものが必要で、成体とは役割が違う命もいます。住み場所が変わってもすぐには消えず、移動や次の世代につながります。\n");
                if (state.Ecosystem.Habitat != null)
                    sb.AppendLine("落ち葉の一部は腐植となって土に残り、後の根を支えます。野イチゴ・タイムはつる、ハッカ・スミレは地下茎でも同種の株を広げます。親の蓄えと養分を使い、合う隣の土へ根づきます。育つ条件の中にも、よく合う場所と条件の端の場所があります。\n");
                sb.AppendLine("<b>この庭で出会った小さな命</b>");
                foreach (string kind in new[] { Ecology.Bee, Ecology.Butterfly, LivingEcosystem.Hoverfly, Ecology.Bird,
                    Ecology.Mushroom, Ecology.Worm, LivingEcosystem.Springtail, LivingEcosystem.Snail,
                    LivingEcosystem.Aphid, LivingEcosystem.Ladybird, LivingEcosystem.Spider, LivingEcosystem.Frog,
                    Ecology.Dragonfly, Ecology.Moss, Ecology.Firefly, CommunityRules.Caterpillar, CommunityRules.Chrysalis,
                    CommunityRules.LadybirdLarva, CommunityRules.HoverflyLarva, CommunityRules.Nymph, CommunityRules.Tadpole,
                    CommunityRules.Algae, CommunityRules.PondPrey })
                    if (residents != null && residents.Contains(kind))
                        sb.AppendLine("<color=#ffe2a6>" + ResidentLabelJa(kind) + "</color>　" + EcologyObservations.ResidentGuide(kind));
                sb.AppendLine("\n<b>花の条件と役割</b>　庭の中の決まりを記したもの");
            }
            for (int i = 0; i < total; i++)
            {
                var t = Flora.All[i];
                if (!discovered.Contains(t.Id)) { sb.AppendLine("<color=#7d8a87>？？？</color>"); continue; }
                var parts = new List<string>();
                parts.Add(t.MaxHardness >= 900 ? "硬い土でも育つ" : t.MaxHardness >= 700 ? "ふつうの土" : t.MaxHardness >= 500 ? "やわらかめの土" : "とてもやわらかい土");
                if (t.DrainMin >= 450) parts.Add("水捌けのよい所");
                if (t.DrainMax <= 600) parts.Add("水の残る所");
                if (t.MoistMin >= 450) parts.Add("湿った土");
                if (t.MoistMax <= 700) parts.Add("乾きぎみ");
                if (t.Light == 0) parts.Add("日向"); else if (t.Light == 2) parts.Add("日陰");
                if (t.Butterfly >= 2) parts.Add("蝶が大好き"); else if (t.Butterfly == 1) parts.Add("蝶が好む"); else if (t.Butterfly < 0) parts.Add("<color=#ffb3a6>蝶が避ける</color>");
                if (t.Bee >= 2) parts.Add("蜂が大好き");
                var works = new List<string>();
                if (t.Softens > 0) works.Add("土をやわらげる");
                if (t.Drains > 0) works.Add("水捌けをよくする");
                if (t.Drains < 0) works.Add("水を抱える");
                if (t.Berries) works.Add("実を小鳥が運ぶ");
                if (t.WindSeed) works.Add("綿毛で旅する");
                sb.AppendLine($"<color=#ffe2a6>{t.LabelJa}</color>　{string.Join("・", parts)}" +
                              (works.Count > 0 ? $"　<color=#a8e6c8>→ {string.Join("・", works)}</color>" : ""));
                if (living)
                {
                    string roots = state.Ecosystem.Habitat != null && HabitatRules.RootRoute(t.Id) != null
                        ? (HabitatRules.RootRoute(t.Id) == "stolon" ? "・つるでも根づく" : "・地下茎でも増える") : "";
                    string dormancy = state.Ecosystem.Succession != null && SeedStrategies.Get(t.Id).NeedsRestSeason ? "・種は休息の頃を経てから芽吹く" : "";
                    sb.AppendLine("　<color=#b7cdc3>" + PlantStrategies.Describe(t.Id) + dormancy + roots + "</color>\n");
                }
            }
            SetScrollableText(_guideText, sb.ToString());
        }
        public void ShowObservation(WorldState state)
        {
            _observationState = state;
            RenderObservation();
        }
        private void RenderObservation()
        {
            if (_observationText == null || _observationState == null) return;
            SetScrollableText(_observationText, EcologyInspection.Describe(_observationState, _observationHabitat));
            foreach (var pair in _observationChips)
            {
                bool active = pair.Key == (_observationHabitat ?? "全体");
                pair.Value.GetComponent<Image>().color = active ? ChipActive : ChipColor;
                pair.Value.GetComponentInChildren<Text>().color = active ? GoldText : TextColor;
            }
        }

        private Text _composition;

        /// <summary>What this garden has become: flowers in bloom now and its harmony.</summary>
        public void ShowComposition(IList<string> bloomingJa, int harmony)
        {
            if (_composition == null) return;
            _composition.text = (bloomingJa.Count > 0 ? "いま咲いている花: " + string.Join("・", bloomingJa) : "まだ花は咲いていない") +
                                (harmony >= 0 ? $"\n☯ 調和 {harmony}/8" : "");
        }

        public void ShowHarmony(int harmony)
        {
            _harmony = harmony;
            ShowResidents(_lastKinds);
        }

        private List<string> _lastKinds = new List<string>();

        public void ShowResidents(IEnumerable<string> kinds)
        {
            if (kinds != null && !ReferenceEquals(kinds, _lastKinds)) _lastKinds = new List<string>(kinds);
            var names = new List<string>();
            if (kinds != null)
                foreach (var k in kinds)
                {
                    string n = ResidentLabelJa(k);
                    if (n != null && !names.Contains(n)) names.Add(n);
                }
            _residents.text = "<color=#cfe6dc>庭の気配</color>" +
                (_harmony >= 0 ? $"　<color=#ffe2a6>☯ 調和 {_harmony}/8</color>" : "") + "\n" +
                (names.Count > 0 ? string.Join("・", names.GetRange(0, Math.Min(6, names.Count))) +
                    (names.Count > 6 ? $" ほか{names.Count - 6}種" : "") : "<color=#9aa8a4>まだ静か</color>");
        }

        private void BuildTitleCard(Transform canvas)
        {
            _titleCard = new GameObject("TitleCard");
            _titleCard.transform.SetParent(canvas, false);
            var dim = _titleCard.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.02f, 0.05f, 0.62f);
            var dimRect = _titleCard.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero; dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero; dimRect.offsetMax = Vector2.zero;
            var close = _titleCard.AddComponent<Button>();
            close.targetGraphic = dim;
            close.onClick.AddListener(() => ShowTitleCard(false));

            var panel = MakePanel(_titleCard.transform, new Vector2(1060f, 500f));
            panel.SetActive(true);
            var title = MakeText(panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(980f, 80f), 60,
                TextAnchor.MiddleCenter);
            title.text = "Destiny Garden";
            title.color = new Color(1f, 0.90f, 0.66f);
            var sub = MakeText(panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(980f, 40f), 24,
                TextAnchor.MiddleCenter);
            sub.text = "運命の庭 — 瓶の中の土と水から、あなただけの庭へ";
            sub.color = new Color(0.80f, 0.92f, 0.95f);
            var body = MakeText(panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(960f, 160f), 20,
                TextAnchor.UpperLeft);
            body.supportRichText = true;
            body.text =
                "・瓶の中の土と水から、あなただけの庭が育っていきます。\n" +
                "・空と庭の状態が、サイコロの<b>6つの面</b>（水・風・光・種の恵み）を毎回決めます。\n" +
                "・面を選んで<b>願い</b>をかけ、<b>サイコロを振る</b>。その一投が祈りになります。\n" +
                "・植物は生きられる場所に根づき、生き物が種を運び、島はひとつの命のように育ちます。\n" +
                "・ドラッグで島を見まわし、ホイールで寄り引き。庭ごとに島が変わります。\n" +
                "・<color=#ffd36b>ときおり、願いが形になることがあります。</color>";
            var en = MakeText(panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -352f), new Vector2(940f, 70f), 17,
                TextAnchor.UpperLeft);
            en.text = "From earth and water in a sealed bottle, a garden of your own grows. Each roll is a prayer:\n" +
                      "plants take root only where they can live, small lives carry seeds, and the island grows as one.";
            en.color = new Color(0.78f, 0.84f, 0.86f);
            var tap = MakeText(panel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(600f, 36f), 20,
                TextAnchor.MiddleCenter);
            tap.text = "クリック／タップで始める　·　Click to begin";
            tap.color = new Color(1f, 0.82f, 0.45f);
            // Clicks on the panel also close the card.
            var panelButton = panel.AddComponent<Button>();
            panelButton.targetGraphic = panel.GetComponent<Image>();
            panelButton.onClick.AddListener(() => ShowTitleCard(false));
        }

        public Action OnTitleClosed;

        public void ShowTitleCard(bool show)
        {
            if (_titleCard == null) return;
            bool wasOpen = _titleCard.activeSelf;
            _titleCard.SetActive(show);
            if (wasOpen && !show) OnTitleClosed?.Invoke();
        }

        public void ShowIntentSelection(string kind, string value)
        {
            if (_wishToggle != null)
            {
                string label = kind == "face" && value != null ? $"願い：{value}の目"
                    : kind == "family" && value != null ? "願い：" + (value == "water" ? "水" : value == "wind" ? "風" : value == "light" ? "光" : "種")
                    : "願いをかける";
                _wishToggle.GetComponentInChildren<Text>().text = label;
                bool wishing = kind == "face" || kind == "family";
                SetLine(_wishToggle, wishing ? WishOutline : Hairline, wishing ? 1.5f : 1f);
            }
            foreach (var kv in _intentChips)
            {
                bool active = (kind == "none" && kv.Key == "none") || (kind == "family" && kv.Key == value);
                kv.Value.GetComponent<Image>().color = active ? ChipActive : ElementColor(kv.Key);
                SetLine(kv.Value, active ? GoldLine : ElementLine(kv.Key), active ? 1.5f : 1f);
                kv.Value.GetComponentInChildren<Text>().color = active ? GoldText : TextColor;
            }
        }

        public void ShowSiteSelection(string mode, string siteId)
        {
            foreach (var kv in _siteChips)
            {
                bool active = (mode == "auto" && kv.Key == "auto") || (mode == "fixed" && kv.Key == siteId);
                kv.Value.GetComponent<Image>().color = active ? ChipActive : ChipColor;
                SetLine(kv.Value, active ? GoldLine : Hairline, active ? 1.5f : 1f);
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
            _messageCard.text = outcome == null ? card + message.Text
                : outcome.LuckySeven
                    ? $"★幸運の七「{RecipeCatalog.LabelJa(outcome.RecipeId)}×2」　{card}{message.Text}"
                    : $"{outcome.FaceId}の目「{RecipeCatalog.LabelJa(outcome.RecipeId)}」　{card}{message.Text}";
        }

        public void ShowStatus(string text) => _status.text = text ?? "";

        public void RefreshHistory(List<RoundSummary> history)
        {
            var sb = new StringBuilder();
            sb.AppendLine("記録（新しい順・上下にスクロール）");
            for (int i = history.Count - 1; i >= 1; i--) // scroll through every saved roll
            {
                var r = history[i];
                sb.AppendLine($"{r.WorldRevision}投目 {WeatherLabel(r.WeatherId)} {SiteLabel(r.SiteId)} " +
                    $"{(r.FaceId.HasValue ? r.FaceId.Value + "の目" : "")} {RecipeLabel(r.RecipeId)}" +
                    (r.LuckySeven ? " ★幸運の七" : r.FaceCount == 7 ? "（7面）" : "") +
                    (r.LuckySeven && r.Bloomed ? " ∞虹" : ""));
                var lives = new List<string>();
                foreach (var k in r.EcologyKinds) { string n = ResidentLabelJa(k); if (n != null && !lives.Contains(n)) lives.Add(n); }
                if (lives.Count > 0 || r.EcologySeeds > 0 || r.SeedBankDeposits > 0 || r.Harmony >= 0)
                    sb.AppendLine("　〔" + string.Join("・", lives) + "〕" +
                                  (r.SeedBankDeposits > 0 ? $" 土に残った種 ×{r.SeedBankDeposits}" : "") +
                                  (r.SeedBankSprouts > 0 ? $" 眠った種が芽吹いた ×{r.SeedBankSprouts}" : r.EcologySeeds > 0 ? $" 種がこぼれた ×{r.EcologySeeds}" : "") +
                                  (r.Harmony >= 0 ? $"　☯{r.Harmony}" : ""));
                string relationship = EcologyObservations.Highlight(r.Relations);
                if (relationship != null) sb.AppendLine("　<color=#a8e6c8>" + relationship + "</color>");
                if (!string.IsNullOrEmpty(r.MessageText)) sb.AppendLine("　" + r.MessageText);
            }
            if (history.Count <= 1) sb.AppendLine("まだ記録がありません。");
            SetScrollableText(_historyText, sb.ToString());
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
            int compared = 0, matched = 0;
            float baseline = 0f;
            foreach (var r in history)
            {
                if (!r.Resolved || r.IntentKind == null || r.IntentKind == "none") continue;
                compared++;
                baseline += r.MatchingFaces / (float)Math.Max(1, r.FaceCount);
                if (r.IntentMatched) matched++;
            }
            _graphCaption.text = "青: 庭の水の総量　緑: 成長の合計" +
                (compared > 0
                    ? $"\n意図の一致 {matched}/{compared}回（偶然なら平均 {baseline:0.0}回）"
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
            rect.anchoredPosition = new Vector2(x, 0f);
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
            var shadow = obj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(1.2f, -1.2f);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return text;
        }

        private Text MakeLabel(Transform parent, Vector2 posFromBottomLeft, string label, int size)
        {
            var text = MakeText(parent, new Vector2(0f, 0f), posFromBottomLeft + new Vector2(0f, 0f),
                new Vector2(900f, 30f), size, TextAnchor.LowerLeft);
            text.text = label;
            text.color = new Color(0.80f, 0.85f, 0.82f, 0.9f);
            return text;
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
            var hair = obj.AddComponent<Outline>();
            hair.effectColor = Hairline;
            hair.effectDistance = new Vector2(1f, -1f);
            var button = obj.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick?.Invoke());
            var text = MakeText(obj.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size, fontSize, TextAnchor.MiddleCenter);
            text.text = label;
            return button;
        }
    }
}
