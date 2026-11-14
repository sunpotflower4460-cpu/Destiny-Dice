using System;
using System.Collections.Generic;
using System.Text;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using UnityEngine;
using UnityEngine.UI;
using Surface = DestinyGarden.Presentation.GardenUiArt.Surface;
using Symbol = DestinyGarden.Presentation.GardenUiArt.Symbol;

namespace DestinyGarden.Presentation
{
    /// <summary>All UI is built in code. The UI only displays committed facts and
    /// forwards intents; it never draws dice or changes a published round.</summary>
    public sealed partial class GardenUi : MonoBehaviour
    {
        public Action OnRoll;
        public Action<string, string> OnIntentSelected; // kind, value
        public Action<string, string> OnSiteSelected;   // mode, siteId
        public Action OnSwitchVisitor;
        public Action OnExport;
        public Action OnHideUi;
        public Action OnAddLuckySeven;
        public Action OnResetView;
        public Action<int> OnObserveCell;
        public Func<string, GardenMemory> OnReadMemory;
        public Action OnViewHouse;

        private Font _font;
        private Text _status;
        private Image _statusBacking;
        private GardenUiButton _rollButton;
        private bool _rollEnabled = true;
        private Text _houseViewLabel;
        private readonly GardenUiButton[] _faceChips = new GardenUiButton[7];
        private readonly Text[] _faceLabels = new Text[7];
        private readonly Image[] _faceWishBackings = new Image[7];
        private readonly Image[] _faceMarks = new Image[7];   // lucky marks on the face strip (no words)
        private readonly Image[] _faceIcons = new Image[7];   // the blessing's mark of each face
        private int _wishedFaceId;
        private bool _hasSelectedWish;
        private RectTransform _faceStrip;
        private GardenUiButton _luckyOffer;
        private Image _luckyOfferGlow;
        private string _wishedElement;
        private const float FaceSpacing = 138f;
        private const string StartGardenLabel = "小さな庭をひらく";
        private const float OpeningWidth = 545f;
        private static readonly Vector2 OpeningEntrySize = new Vector2(394f, 86f);
        private GameObject _titleCard;
        private RectTransform _openingCard;
        private float _openingScale;
        private Text _openingHeading, _openingTagline;
        private GardenUiButton _enterButton;
        // The title's second way in: a new garden (the current one stays in the record). Two presses, so it is never by accident.
        private GardenUiButton _newGardenButton;
        private bool _newGardenArmed;
        private const string NewGardenLabel = "新しい庭をはじめる", NewGardenConfirm = "もう一度押す・今の庭は残ります";
        private static readonly Vector2 NewGardenSize = new Vector2(372f, 48f);
        private CanvasGroup _gardenControls;
        internal bool IsOpening { get; private set; }
        private readonly Dictionary<string, GardenUiButton> _intentChips = new Dictionary<string, GardenUiButton>();
        private readonly Dictionary<string, GardenUiButton> _siteChips = new Dictionary<string, GardenUiButton>();
        private GameObject _observationPanel;
        private Text _observationText;
        private Text _habitatHeading;
        private Button _careTargetButton;
        private Text _careSelection;
        private Text _targetNote;
        private string _carePlan;
        private CareOutlook _careOutlook;
        private WorldState _outlookState;
        private string[] _outlookFaces;
        private PreparedFaces _outlookPrepared;
        private WeatherDefinition _outlookWeather;
        private List<CareAftercareNote> _aftercareNotes = new List<CareAftercareNote>();
        private List<GardenMoment> _recentMoments = new List<GardenMoment>();
        private List<RoundSummary> _historyData = new List<RoundSummary>();
        private bool _observationDetails;
        private Button _observationModeButton, _momentFocusButton, _habitatButton;
        private Button _momentPrevious, _momentNext, _memoryButton;
        private Text _momentCounter;
        private int _momentIndex;
        private GardenMoment _selectedMoment;
        private GardenMemory _memoryView;
        private bool _memoryDirty;
        private readonly Dictionary<string, int> _memoryNext = new Dictionary<string, int>();
        private GardenMoment _focusedMoment;
        private WorldState _observationState;
        private string _observationHabitat;
        private readonly List<GardenUiButton> _habitatChoices = new List<GardenUiButton>();
        private GameObject _historyPanel;
        private Text _historyText;
        private GameObject _graphPanel;
        private Transform _graphBars;
        private Text _graphCaption;
        private GameObject _settingsPanel;
        public GardenAudio Audio;
        public GardenView View;

        // Glass palette shared by the controls and the garden's information plaques.
        private static readonly Color ChipColor = new Color(0.040f, 0.087f, 0.105f, 0.82f);
        private static readonly Color Hairline = new Color(1f, 0.93f, 0.78f, 0.16f);
        private static readonly Color TextColor = GardenUiArt.Pigment(0xf4eedf);

        /// <summary>One colour per blessing, shared by face chips and wish chips.</summary>
        public static Color ElementColor(string element)
        {
            switch (element)
            {
                case "water": return new Color(0.045f, 0.14f, 0.24f, 0.90f);
                case "wind": return new Color(0.045f, 0.15f, 0.13f, 0.90f);
                case "light": return new Color(0.20f, 0.15f, 0.055f, 0.90f);
                case "seed": return new Color(0.17f, 0.105f, 0.05f, 0.90f);
                default: return ChipColor;
            }
        }

        /// <summary>Bright hairline for a blessing, so colour reads as an edge of light, not a block.</summary>
        /// <summary>What a face is called on the die: a short, concrete name (presentation only).</summary>
        public static string FaceName(string recipeId) => recipeId switch
        {
            "water_drop" => "たっぷり水",
            "water_rain" => "小雨",
            "wind_breeze" => "そよ風",
            "wind_passage" => "つよい風",
            "light_soft" => "やわらかな光",
            "light_beam" => "ひとすじの光",
            "seed_single" => "種ひと粒",
            "seed_scatter" => "種ふた粒",
            _ => RecipeCatalog.LabelJa(recipeId)
        };

        /// <summary>Where and how the face's blessing falls, in a few words (from the recipe's footprint).</summary>
        public static string FaceHint(string recipeId) => recipeId switch
        {
            "water_drop" => "一か所に深く",
            "water_rain" => "まわりに広く",
            "wind_breeze" => "広くやさしく",
            "wind_passage" => "すぐそばを強く",
            "light_soft" => "まわりを照らす",
            "light_beam" => "一点を強く",
            "seed_single" => "すぐそばに",
            "seed_scatter" => "少し離して",
            _ => ""
        };

        /// <summary>"4 つよい風" and, smaller, "すぐそばを強く" (the seventh face is doubled: "×2").</summary>
        public static string FaceTitle(int face, string recipeId, bool seventh = false) =>
            $"{face} {FaceName(recipeId)}" + (seventh ? "×2" : "") + $"\n<size=13><color=#cdbfa8>{FaceHint(recipeId)}</color></size>";

        internal static Symbol ElementSymbol(string element) => element switch
        {
            "water" => Symbol.Water,
            "wind" => Symbol.Wind,
            "seed" => Symbol.Seed,
            _ => Symbol.Star
        };

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

        private static string ElementOfRecipe(string recipeId) =>
            RecipeCatalog.ElementOf(recipeId).ToString().ToLowerInvariant();

        public Font UiFont
        {
            get
            {
                // The bundled Noto Serif JP on every platform (GardenType), so the garden reads the same in the
                // browser and in the app (the app used to fall back to the system's sans-serif font).
                if (_font == null) _font = GardenType.Body;
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
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasObj.AddComponent<GraphicRaycaster>();

            var controls = new GameObject("GardenControls", typeof(RectTransform));
            controls.transform.SetParent(canvasObj.transform, false);
            controls.AddComponent<GardenSafeArea>();
            _gardenControls = controls.AddComponent<CanvasGroup>();
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            var parent = controls.transform;
            BuildMomentCard(parent);
            _status = MakeText(parent, new Vector2(0.5f, 0f), new Vector2(0f, 166f),
                new Vector2(820f, 40f), 17, TextAnchor.MiddleCenter);
            _status.name = "Garden notice";
            _statusBacking = AddBacking(_status, new Vector2(12f, 4f));
            ShowStatus(null);

            BuildRollControls(parent);
            BuildPanels(BuildNavigation(canvasObj.transform));
            BuildTitleCard(canvasObj.transform);
        }

        private Image AddBacking(Text text, Vector2 pad)
        {
            var bg = new GameObject("Backing");
            var parent = text.transform.parent;
            var siblingIndex = text.transform.GetSiblingIndex();
            bg.transform.SetParent(parent, false);
            bg.transform.SetSiblingIndex(siblingIndex);
            var img = bg.AddComponent<Image>();
            SetPlate(img, GardenUiArt.Plaque);
            img.raycastTarget = false;
            var src = text.GetComponent<RectTransform>();
            var rect = bg.GetComponent<RectTransform>();
            rect.anchorMin = src.anchorMin; rect.anchorMax = src.anchorMax;
            rect.pivot = src.pivot;
            rect.anchoredPosition = src.anchoredPosition + Vector2.Scale(src.pivot * 2f - Vector2.one, pad);
            rect.sizeDelta = src.sizeDelta + pad * 2f;
            return img;
        }

        /// <summary>Hides all UI except a small restore chip (photo moment).</summary>
        public void SetUiVisible(bool visible, string restoreLabel = null)
        {
            foreach (Transform child in transform) child.gameObject.SetActive(visible);
        }

        private void BuildPanels(Transform canvas)
        {
            _historyPanel = MakePanel(canvas, new Vector2(880f, 620f));
            AddPanelHeader(_historyPanel, "投ごとの記録");
            _historyText = MakeScrollableText(_historyPanel.transform, new Vector2(800f, 432f), 20);
            _historyText.supportRichText = false;
            _historyText.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -10f);
            BuildHistoryPages(_historyPanel.transform);
            _historyPanel.SetActive(false);

            _graphPanel = MakePanel(canvas, new Vector2(920f, 590f));
            AddPanelHeader(_graphPanel, "庭の記録");
            var barsObj = new GameObject("Bars");
            barsObj.transform.SetParent(_graphPanel.transform, false);
            var barsRect = barsObj.AddComponent<RectTransform>();
            barsRect.anchorMin = barsRect.anchorMax = new Vector2(0.5f, 0.5f);
            barsRect.anchoredPosition = new Vector2(0f, 4f);
            barsRect.sizeDelta = new Vector2(840f, 230f);
            _graphBars = barsObj.transform;
            _graphCaption = MakeText(_graphPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -148f),
                new Vector2(840f, 66f), 20, TextAnchor.MiddleCenter);
            _composition = MakeText(_graphPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 185f),
                new Vector2(840f, 68f), 19, TextAnchor.MiddleCenter);
            _composition.color = new Color(1f, 0.92f, 0.76f);
            MakeChip(_graphPanel.transform, new Vector2(0.5f, 0f), new Vector2(-164f, 24f),
                new Vector2(272f, 50f), "投ごとの記録", 20, () => OpenPanel(_historyPanel));
#if !UNITY_WEBGL || UNITY_EDITOR
            MakeChip(_graphPanel.transform, new Vector2(0.5f, 0f), new Vector2(164f, 24f),
                new Vector2(272f, 50f), "記録を書き出す", 20, () =>
                {
                    ClosePanels();
                    OnExport?.Invoke();
                });
#endif
            _graphPanel.SetActive(false);

            _guidePanel = MakePanel(canvas, new Vector2(1000f, 650f));
            AddPanelHeader(_guidePanel, "図鑑");
            _guideText = MakeScrollableText(_guidePanel.transform, new Vector2(920f, 530f), 20);
            _guideText.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -29f);
            _guideText.supportRichText = true;
            _guidePanel.SetActive(false);

            BuildObservationPanels(canvas);

            _settingsPanel = MakePanel(canvas, new Vector2(640f, 430f));
            AddPanelHeader(_settingsPanel, "設定");
            var settings = _settingsPanel.transform;
            var volume = MakeText(settings, new Vector2(0f, 1f), new Vector2(49f, -105f),
                new Vector2(130f, 44f), 22, TextAnchor.MiddleLeft);
            volume.text = "音量";
            var sliderObj = new GameObject("Volume", typeof(RectTransform), typeof(Image));
            sliderObj.transform.SetParent(settings, false);
            var sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0.5f, 1f);
            sliderRect.anchoredPosition = new Vector2(70f, -127f);
            sliderRect.sizeDelta = new Vector2(370f, 48f);
            sliderObj.GetComponent<Image>().color = Color.clear;
            var slider = sliderObj.AddComponent<Slider>();
            var bg = new GameObject("Track"); bg.transform.SetParent(sliderObj.transform, false);
            var bgImg = bg.AddComponent<Image>();
            SetPlate(bgImg, GardenUiArt.Well);
            bgImg.raycastTarget = false;
            bg.GetComponent<RectTransform>().sizeDelta = new Vector2(370f, 12f);
            var handle = new GameObject("Handle"); handle.transform.SetParent(sliderObj.transform, false);
            var handleImage = handle.AddComponent<Image>();
            SetPlate(handleImage, GardenUiArt.Button(Surface.Brass));
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(24f, 38f);
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handleImage;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = Audio != null ? Audio.Volume : 0.8f;
            slider.onValueChanged.AddListener(v => { if (Audio != null) Audio.Volume = v; });

            _muteButton = MakeChip(settings, new Vector2(0.5f, 1f), new Vector2(-144f, -178f),
                new Vector2(262f, 55f), "音を消す", 21, () =>
                {
                    if (Audio != null) Audio.Muted = !Audio.Muted;
                    RefreshSettings();
                });
            _motionButton = MakeChip(settings, new Vector2(0.5f, 1f), new Vector2(144f, -178f),
                new Vector2(262f, 55f), "動きを抑える", 21, () =>
                {
                    if (View != null) View.ReducedMotion = !View.ReducedMotion;
                    RefreshSettings();
                });
            MakeChip(settings, new Vector2(0.5f, 1f), new Vector2(-144f, -248f),
                new Vector2(262f, 55f), "表示を隠す（4秒）", 20, () =>
                {
                    ClosePanels();
                    OnHideUi?.Invoke();
                });
            MakeChip(settings, new Vector2(0.5f, 1f), new Vector2(144f, -248f),
                new Vector2(262f, 55f), "新しい庭を始める", 20, () => OpenPanel(_newGardenPanel));
            _settingsPanel.SetActive(false);

            _newGardenPanel = MakePanel(canvas, new Vector2(610f, 300f));
            AddPanelHeader(_newGardenPanel, "新しい庭を始める");
            var notice = MakeText(_newGardenPanel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -106f),
                new Vector2(530f, 78f), 22, TextAnchor.UpperCenter);
            notice.text = "今の庭は記録に残ります。\n画面は新しい庭に切り替わります。";
            var start = MakeChip(_newGardenPanel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 34f),
                new Vector2(244f, 55f), "新しい庭を始める", 21, () =>
                {
                    ClosePanels();
                    OnSwitchVisitor?.Invoke();
                });
            StylePrimary(start);
            _newGardenPanel.SetActive(false);
            BuildKeepsakePanel(canvas);
        }

        private void BuildObservationPanels(Transform parent)
        {
            _observationPanel = MakePanel(parent, new Vector2(1000f, 644f));
            AddPanelHeader(_observationPanel, "庭を観察");
            var panel = _observationPanel.transform;
            _habitatButton = MakeChip(panel, new Vector2(0f, 1f), new Vector2(36f, -92f),
                new Vector2(254f, 50f), "場所を選ぶ", 20, () => OpenHabitats(false));
            _observationText = MakeScrollableText(panel, new Vector2(928f, 280f), 21);
            _observationText.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 19f);
            _momentPrevious = MakeChip(panel, new Vector2(0.5f, 0f), new Vector2(-306f, 100f),
                new Vector2(250f, 48f), "新しい出来事", 19, () =>
                {
                    if (_memoryView != null) OpenMemory(_memoryView.ParentKey);
                    else { _momentIndex--; RenderObservation(); }
                });
            _momentNext = MakeChip(panel, new Vector2(0.5f, 0f), new Vector2(-36f, 100f),
                new Vector2(250f, 48f), "前の出来事", 19, () =>
                {
                    if (_memoryView == null) { _momentIndex++; RenderObservation(); return; }
                    _memoryNext.TryGetValue(_memoryView.Key, out int index);
                    if (_memoryView.Next.Count == 0) return;
                    var link = _memoryView.Next[index % _memoryView.Next.Count];
                    _memoryNext[_memoryView.Key] = index + 1;
                    OpenMemory(link.Key);
                });
            _momentCounter = MakeText(panel, new Vector2(0.5f, 1f), new Vector2(10f, -101f),
                new Vector2(310f, 32f), 18, TextAnchor.MiddleCenter);
            _memoryButton = MakeChip(panel, new Vector2(0.5f, 0f), new Vector2(312f, 100f),
                new Vector2(270f, 48f), "この株の歩み", 19, () =>
                {
                    if (_memoryView != null) { _memoryView = null; RenderObservation(); }
                    else OpenMemory(_selectedMoment?.MemoryKey);
                });
            _observationModeButton = MakeChip(panel, new Vector2(0.5f, 0f), new Vector2(-306f, 33f),
                new Vector2(250f, 48f), "庭のようすを詳しく", 19, () =>
                {
                    _observationDetails = !_observationDetails;
                    RenderObservation();
                });
            _momentFocusButton = MakeChip(panel, new Vector2(0.5f, 0f), new Vector2(-36f, 33f),
                new Vector2(250f, 48f), "この場所を見る", 19, () =>
                {
                    if (_focusedMoment == null || _focusedMoment.CellId < 0 || !_rollEnabled) return;
                    ShowMessage(null, null, _focusedMoment);
                    ClosePanels();
                    OnObserveCell?.Invoke(_focusedMoment.CellId);
                });
            _careTargetButton = MakeChip(panel, new Vector2(0.5f, 0f), new Vector2(312f, 33f),
                new Vector2(270f, 48f), "次の届け先にする", 19, () =>
                {
                    if (_observationHabitat == null) return;
                    OnSiteSelected?.Invoke("habitat", _observationHabitat);
                    ClosePanels();
                });
            _observationPanel.SetActive(false);

            _habitatPanel = MakePanel(parent, new Vector2(730f, 358f));
            _habitatHeading = AddPanelHeader(_habitatPanel, "観察する場所を選ぶ");
            var labels = new List<string> { "全体" };
            labels.AddRange(EcologyInspection.Habitats);
            for (int i = 0; i < labels.Count; i++)
            {
                string label = labels[i];
                _habitatChoices.Add(MakeChip(_habitatPanel.transform, new Vector2(0.5f, 1f),
                    new Vector2(-226f + (i % 3) * 226f, -111f - (i / 3) * 82f),
                    new Vector2(202f, 59f), label, 22, () =>
                    {
                        if (_choosingHabitat)
                        {
                            OnSiteSelected?.Invoke("habitat", label);
                            ClosePanels();
                            return;
                        }
                        _observationHabitat = label == "全体" ? null : label;
                        _momentIndex = 0;
                        _memoryView = null;
                        GoBack();
                    }));
            }
            _habitatPanel.SetActive(false);
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

        private GameObject MakePanel(Transform parent, Vector2 size)
        {
            var obj = new GameObject("Panel");
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();
            SetPlate(img, GardenUiArt.Panel);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return obj;
        }

        public void SetRollEnabled(bool enabled)
        {
            _rollEnabled = enabled;
            _rollButton.interactable = enabled;
            _menuButton.interactable = enabled;
            _wishButton.interactable = enabled && _wishEditable;
            _luckyOffer.interactable = enabled;
            if (!enabled) ShowMessage(null, null);
            if (enabled && _activePanel == null && !_titleCard.activeSelf) _rollButton.Select();
        }

        internal void ShowHouse(string label)
        {
            _houseViewLabel.text = "家を見る\n<size=17><color=#f0cb91>" + label + "</color></size>";
        }

        private bool _rollPulse = true;
        /// <summary>A gentle breathing of the roll button until the first roll.</summary>
        public void SetRollPulse(bool on)
        {
            _rollPulse = on;
            if (!on && _rollButton != null) _rollButton.Cap.color = Color.white;
        }

        /// <param name="luckyFaceId">The face that would make the lucky number (gold star), 0 for none.</param>
        /// <param name="pairFaceId">The face that would make a ゾロ目 (two dots), 0 for none.</param>
        public void ShowFaces(string[] faces, int rolledFaceId, int wishedFaceId = 0, int luckyFaceId = 0, int pairFaceId = 0, bool seventhLucky = false)
        {
            _wishedFaceId = wishedFaceId;
            int faceCount = Math.Min(faces?.Length ?? 0, _faceLabels.Length);
            _faceStrip.gameObject.SetActive(faceCount > 0);
            _faceStrip.sizeDelta = new Vector2(faceCount * FaceSpacing + 20f, _faceStrip.sizeDelta.y);
            for (int i = 0; i < _faceLabels.Length; i++)
            {
                var label = _faceLabels[i];
                var accent = _faceAccents[i];
                var chip = _faceChips[i];
                var backing = _faceWishBackings[i];
                bool seventh = i == LuckySeven.FaceId - 1;
                bool present = i < faceCount;
                label.gameObject.SetActive(present);
                chip.gameObject.SetActive(present);
                var mark = _faceMarks[i];
                if (!present)
                {
                    backing.gameObject.SetActive(false);
                    mark.gameObject.SetActive(false);
                    continue;
                }
                // The seventh face is always lucky; on a six-face round the face that adds up to seven, and the face
                // that repeats the last one. Shown only while the round is prepared (the caller passes 0 otherwise).
                bool luckyMark = (luckyFaceId > 0 && i + 1 == luckyFaceId) || (seventh && seventhLucky);
                bool pairMark = !luckyMark && pairFaceId > 0 && i + 1 == pairFaceId;
                mark.gameObject.SetActive(luckyMark || pairMark);
                if (luckyMark || pairMark)
                {
                    mark.sprite = GardenUiArt.Icon(luckyMark ? Symbol.LuckyStar : Symbol.Pair);
                    mark.color = luckyMark ? PaleGold : new Color(PaleGold.r, PaleGold.g, PaleGold.b, 0.85f);
                    mark.rectTransform.sizeDelta = luckyMark ? new Vector2(28f, 28f) : new Vector2(30f, 15f);
                }

                string title = FaceTitle(i + 1, faces[i], seventh);
                int fontSize = seventh ? 14 : 16;
                label.text = title;
                label.fontSize = fontSize;
                _faceIcons[i].sprite = GardenUiArt.Icon(ElementSymbol(ElementOfRecipe(faces[i])));
                var position = new Vector2((i - (faceCount - 1) * 0.5f) * FaceSpacing, 0f);
                label.rectTransform.anchoredPosition = position + new Vector2(13f, 0f);
                string element = ElementOfRecipe(faces[i]);
                bool rolled = rolledFaceId == i + 1;
                bool wished = wishedFaceId == i + 1 || _wishedElement == element;
                backing.rectTransform.anchoredPosition = position;
                backing.gameObject.SetActive(wished);
                label.color = rolled ? PaleGold : wished ? WishInk : TextColor;
                GardenType.Emphasis(label, rolled || wished);
                accent.color = rolled ? PaleGold : wished ? WishInk : ElementLine(element);
                var iconInk = ElementLine(element); iconInk.a = 1f;
                _faceIcons[i].color = rolled ? PaleGold : iconInk;
                SetSurface(chip, wished ? Surface.Brass : BlessingSurface(element));
                chip.Label.text = title;
                chip.Label.fontSize = fontSize;
            }
        }

        public void ShowLuckyOffer(bool offered)
        {
            _luckyOffer.gameObject.SetActive(offered);
        }

        private void Update()
        {
            if (IsOpening) LayoutOpeningCard();
            if (_momentCard != null && !float.IsNegativeInfinity(_momentShownAt)) UpdateMomentCard();
            bool animateButtons = _rollEnabled && _activePanel == null && !IsOpening && View?.ReducedMotion != true;
            if (_rollButton != null)
            {
                float light = _rollPulse && animateButtons ? 0.97f + 0.03f * Mathf.Sin(Time.unscaledTime * 3.2f) : 1f;
                _rollButton.Cap.color = new Color(light, light, light);
            }
            if (_luckyOffer != null && _luckyOffer.gameObject.activeSelf)
            {
                Color glow = PaleGold;
                glow.a = animateButtons ? 0.67f + 0.25f * Mathf.Sin(Time.unscaledTime * 3.2f) : 0.67f;
                _luckyOfferGlow.color = glow;
            }

            if (_resetViewButton != null)
            {
                var rig = View?.Rig;
                bool visible = _rollEnabled && rig != null && rig.CanResetView;
                if (_resetViewButton.gameObject.activeSelf != visible) _resetViewButton.gameObject.SetActive(visible);
                if (visible)
                {
                    // At the house the way back is the one thing to do: it says so and stands out in gold.
                    string label = rig.IsHouseFocused ? "庭にもどる" : "視点を戻す";
                    if (_resetViewLabel.text != label) _resetViewLabel.text = label;
                    if (_resetPrimary != rig.IsHouseFocused)
                    {
                        _resetPrimary = rig.IsHouseFocused;
                        if (_resetPrimary) StylePrimary(_resetChip); else { SetSurface(_resetChip, Surface.Enamel); GardenType.MakeBody(_resetChip.Label); }
                    }
                }
            }

            if (_activePanel != null && UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                GoBack();
        }

        public static string ResidentLabelJa(string kind) => EcologyObservations.ResidentLabel(kind);

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
                if (_recentMoments.Count > 0) sb.AppendLine(GardenMoments.Describe(_recentMoments, max: 2));
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
            _observedDestinationButton.gameObject.SetActive(CareRules.Enabled(state));
            RenderObservation();
        }
        public void ShowCarePlan(WorldState state, PreparedFaces faces, WeatherDefinition weather)
        {
            _carePlan = CareTargets.PreparedPlan(state, faces, weather);
            if (faces == null || !ReferenceEquals(_outlookState, state) || !ReferenceEquals(_outlookFaces, faces.Faces) || !ReferenceEquals(_outlookWeather, weather))
            {
                _careOutlook = null;
                _outlookState = state; _outlookFaces = faces?.Faces; _outlookWeather = weather;
            }
            _outlookPrepared = faces;
            _targetNote.text = faces?.TargetNote ?? "";
            RenderObservation();
        }
        private void RenderObservation()
        {
            if (_observationText == null || _observationState == null || !_observationPanel.activeSelf) return;
            if (_memoryDirty && !_observationDetails)
            {
                if (_memoryView != null) _memoryView = OnReadMemory?.Invoke(_memoryView.Key);
                _memoryDirty = false;
            }
            var moments = new List<GardenMoment>();
            foreach (var moment in _recentMoments) if (_observationHabitat == null || moment.Habitat == _observationHabitat) moments.Add(moment);
            _momentIndex = Math.Max(0, Math.Min(_momentIndex, moments.Count - 1));
            _selectedMoment = moments.Count == 0 ? null : moments[_momentIndex];
            _focusedMoment = _memoryView?.Focus ?? _selectedMoment;
            if (_observationDetails && _careOutlook == null && _outlookPrepared != null)
                _careOutlook = CareOutlook.Build(_outlookState, _outlookPrepared, _outlookWeather);
            string text = _observationDetails ? (_careOutlook?.Describe(_observationHabitat) ?? "") + (_carePlan ?? "") +
                CareAftercare.Describe(_aftercareNotes, _observationHabitat, 3) + "\n" + EcologyInspection.Describe(_observationState, _observationHabitat) +
                CareTargets.Guidance(_observationState, _observationHabitat)
                : _memoryView != null ? _memoryView.Describe()
                : GardenMoments.Describe(_selectedMoment != null ? new[] { _selectedMoment } : new GardenMoment[0], _observationHabitat) +
                    (_selectedMoment?.Revision == _observationState.WorldRevision ? CareAftercare.Describe(_aftercareNotes, _observationHabitat, 1) : "");
            SetScrollableText(_observationText, text);
            _habitatButton.GetComponentInChildren<Text>().text = "場所： " + (_observationHabitat ?? "庭全体");
            _observationModeButton.GetComponentInChildren<Text>().text = _observationDetails ? (_memoryView != null ? "歩みへ戻る" : "出来事へ戻る") : "庭のようすを詳しく";
            _memoryButton.gameObject.SetActive(!_observationDetails && (_memoryView != null || _selectedMoment?.MemoryKey != null));
            _momentPrevious.GetComponentInChildren<Text>().text = _memoryView != null ? _memoryView.ParentLabel : "新しい出来事";
            _momentPrevious.gameObject.SetActive(!_observationDetails && (_memoryView != null ? _memoryView.ParentKey != null : _momentIndex > 0));
            int paths = _memoryView?.Next.Count ?? 0;
            _memoryNext.TryGetValue(_memoryView?.Key ?? "", out int next);
            _momentNext.GetComponentInChildren<Text>().text = _memoryView != null ? (_memoryView.IsSeed ? "芽吹いた株へ" : "残した命へ") + (paths > 1 ? " " + (next % paths + 1) + "/" + paths : "") : "前の出来事";
            _momentNext.gameObject.SetActive(!_observationDetails && (_memoryView != null ? paths > 0 : _momentIndex + 1 < moments.Count));
            _momentCounter.text = _memoryView == null && !_observationDetails && moments.Count > 1
                ? $"出来事 {_momentIndex + 1} / {moments.Count}" : "";
            _memoryButton.GetComponentInChildren<Text>().text = _memoryView != null ? "出来事へ戻る" : _selectedMoment?.MemoryKey?.StartsWith("s:", StringComparison.Ordinal) == true ? "この種の歩み" : "この株の歩み";
            _memoryButton.interactable = _memoryView != null || _selectedMoment?.MemoryKey != null;
            _momentFocusButton.gameObject.SetActive(!_observationDetails && _focusedMoment != null && _focusedMoment.CellId >= 0);
            _careTargetButton.gameObject.SetActive(_observationHabitat != null && _memoryView == null && CareRules.Enabled(_observationState));
        }
        private void OpenMemory(string key)
        {
            if (key == null) return;
            var memory = OnReadMemory?.Invoke(key);
            if (memory == null) { ShowStatus("この命の記録は、まだ見つからなかった。"); return; }
            _memoryView = memory; _memoryDirty = false; RenderObservation();
        }

        private Text _composition;

        /// <summary>What this garden has become: flowers in bloom now and its harmony.</summary>
        public void ShowComposition(IList<string> bloomingJa, int harmony)
        {
            if (_composition == null) return;
            _composition.text = (bloomingJa.Count > 0 ? "いま咲いている花: " + string.Join("・", bloomingJa) : "まだ花は咲いていない") +
                                (harmony >= 0 ? $"\n☯ 調和 {harmony}/8" : "");
        }

        private void BuildTitleCard(Transform canvas)
        {
            _titleCard = new GameObject("TitleCard");
            _titleCard.transform.SetParent(canvas, false);
            var dim = _titleCard.AddComponent<Image>();
            dim.color = new Color(0.035f, 0.016f, 0.055f, 0.07f);
            Stretch(_titleCard.GetComponent<RectTransform>());
            var safeArea = new GameObject("TitleSafeArea", typeof(RectTransform));
            safeArea.transform.SetParent(_titleCard.transform, false);
            safeArea.AddComponent<GardenSafeArea>();

            var opening = new GameObject("Opening", typeof(RectTransform));
            opening.transform.SetParent(safeArea.transform, false);
            _openingCard = opening.GetComponent<RectTransform>();
            DecorateOpening(opening.transform);
            var topLeft = new Vector2(0f, 1f);
            _openingHeading = MakeText(opening.transform, topLeft, Vector2.zero, Vector2.zero, 64, TextAnchor.MiddleLeft);
            _openingHeading.text = "Destiny Garden";
            GardenType.MakeDisplay(_openingHeading);
            _openingHeading.color = GardenUiArt.Pigment(0xffedc6);
            _openingTagline = MakeText(opening.transform, topLeft, Vector2.zero, Vector2.zero, 30, TextAnchor.UpperLeft);
            _openingTagline.text = "ひと振り、瓶の庭に小さな魔法。";
            _openingTagline.color = GardenUiArt.Pigment(0xf8e6c7);
            _enterButton = MakeChip(opening.transform, topLeft, Vector2.zero,
                OpeningEntrySize, StartGardenLabel, 30, () => ShowTitleCard(false));
            DecorateOpeningEntry(_enterButton);
            _newGardenButton = MakeChip(opening.transform, topLeft, Vector2.zero, NewGardenSize, NewGardenLabel, 19, () =>
            {
                if (!_newGardenArmed)
                {
                    _newGardenArmed = true;
                    _newGardenButton.Label.text = NewGardenConfirm;
                    StylePrimary(_newGardenButton);
                    return;
                }
                _newGardenArmed = false;
                OnSwitchVisitor?.Invoke();
            });
            Engrave(_newGardenButton, Symbol.Seed, 24f, 16f);
            _newGardenButton.gameObject.SetActive(false);
            _openingHeading.gameObject.SetActive(_openingLettering == null);
            LayoutOpeningCard();
        }

        public Action OnTitleClosed;

        internal static bool UseSideTitle(float aspect) => aspect >= 1.70f;

        private void LayoutOpeningCard()
        {
            var rect = _titleCard.GetComponent<RectTransform>();
            float aspect = rect.rect.width / Mathf.Max(1f, rect.rect.height);
            bool side = UseSideTitle(aspect);
            bool portrait = aspect < 0.90f;
            var anchor = new Vector2(side ? 0.084f : 0.5f, side ? 0.47f : portrait ? 0.151f : 0.036f);
            // Resize the text and artwork with the layout so the font is rasterized at its displayed size.
            float scale = portrait ? Mathf.Min(2.1f, 1.08f / Mathf.Max(0.01f, aspect)) : 1f;
            if (_openingCard.anchorMin == anchor && Mathf.Approximately(_openingScale, scale)) return;
            _openingScale = scale;
            _openingCard.anchorMin = _openingCard.anchorMax = anchor;
            _openingCard.pivot = new Vector2(side ? 0f : 0.5f, side ? 0.5f : 0f);
            _openingCard.anchoredPosition = Vector2.zero;
            _openingCard.sizeDelta = new Vector2(OpeningWidth, side ? 556f : portrait ? 491f : 403f) * scale;
            _openingHeading.alignment = side ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            _openingTagline.alignment = side ? TextAnchor.UpperLeft : TextAnchor.UpperCenter;
            float portraitOffset = portrait ? 88f : 0f;
            PlaceOpeningText(_openingHeading, 0f, side ? -104f : -58f, OpeningWidth, 76f, 64, scale);
            PlaceOpeningText(_openingTagline, 4f, side ? -326f : -203f - portraitOffset, 537f, 40f, 30, scale);
            var entry = (RectTransform)_enterButton.transform;
            entry.sizeDelta = OpeningEntrySize * scale;
            entry.anchoredPosition = new Vector2(side ? 4f : (OpeningWidth - OpeningEntrySize.x) * 0.5f,
                side ? -449f : -308f - portraitOffset) * scale;
            LayoutOpeningEntry(scale);
            var fresh = (RectTransform)_newGardenButton.transform;
            fresh.sizeDelta = NewGardenSize * scale;
            fresh.anchoredPosition = new Vector2(side ? 4f + (OpeningEntrySize.x - NewGardenSize.x) * 0.5f : (OpeningWidth - NewGardenSize.x) * 0.5f,
                (side ? -449f : -308f - portraitOffset) - OpeningEntrySize.y - 14f) * scale;
            _newGardenButton.Label.fontSize = Mathf.RoundToInt(19 * scale);
            LayoutOpeningDecorations(side, portrait, scale);
        }

        /// <summary>Whether the title card is up (overlays of other components stay hidden under it).</summary>
        public bool TitleCardOpen => _titleCard != null && _titleCard.activeSelf;

        public void ShowOpeningProgress(int revision)
        {
            _enterButton.Label.text = revision >= 2 ? "庭のつづきへ" : StartGardenLabel;
            _newGardenButton.gameObject.SetActive(revision >= 2);
            _newGardenArmed = false;
            _newGardenButton.Label.text = NewGardenLabel;
            SetSurface(_newGardenButton, Surface.Enamel);
            GardenType.MakeBody(_newGardenButton.Label);
        }

        public void ShowTitleCard(bool show, bool opening = false)
        {
            if (_titleCard == null) return;
            if (show && !opening)
            {
                OpenPanel(_helpPanel);
                return;
            }

            bool wasOpen = _titleCard.activeSelf;
            IsOpening = show;
            _titleCard.SetActive(show);
            UpdateGardenControls();
            if (show)
            {
                LayoutOpeningCard();
                _enterButton.Select();
            }
            else if (wasOpen)
            {
                if (_activePanel == null && _rollEnabled) _rollButton.Select();
                OnTitleClosed?.Invoke();
            }
        }

        public void ShowIntentSelection(string kind, string value)
        {
            _wishedElement = kind == "family" ? value : null;
            _hasSelectedWish = value != null && (kind == "face" || kind == "family");
            RefreshWishLight();
            foreach (var kv in _intentChips)
            {
                bool active = (kind == "none" && kv.Key == "none") || (kind == "family" && kv.Key == value);
                SetSurface(kv.Value, active ? Surface.Brass : BlessingSurface(kv.Key));
            }
        }

        public void ShowSiteSelection(string mode, string siteId)
        {
            if (_careSelection != null) _careSelection.text = "次の準備から：" + (mode == "auto" ? "おまかせ" : CareTargets.Name(mode == "habitat" ? "habitat:" + Array.IndexOf(HabitatRules.Labels, siteId) : siteId));
            foreach (var kv in _siteChips)
            {
                bool active = (mode == "auto" && kv.Key == "auto") || (mode == "fixed" && kv.Key == siteId);
                SetSurface(kv.Value, active ? Surface.Brass : Surface.Enamel);
            }
        }

        public void ShowMessage(SelectedMessage message, RollOutcome outcome, GardenMoment moment = null)
        {
            if (moment == null && outcome != null) moment = outcome.Moment ?? GardenMoments.First(GardenMoments.FromOutcome(outcome));
            bool hasMessage = moment != null || message != null || (outcome?.FaceId ?? 0) > 0;
            _momentCard.gameObject.SetActive(hasMessage);
            if (!hasMessage)
            {
                _momentShownAt = float.NegativeInfinity;
                return;
            }
            var star = outcome?.Ecology?.PrayerStar;
            LayoutRollResult(outcome?.FaceId ?? 0, outcome?.RecipeId, outcome?.LuckySeven == true,
                RollRecord.WishComparison(outcome?.IntentKind, outcome?.IntentValue, outcome?.IntentMatched == true),
                outcome?.PrayerText != null, star?.LuckyNumber == true ? RollRecord.LuckyLabel(outcome.FaceId, star.FaceSum - outcome.FaceId) : null);
            if (moment != null)
            {
                PaintMoment(outcome?.RecipeId ?? moment.RecipeId, outcome?.LuckySeven == true, outcome != null,
                    moment.Headline, moment.Habitat);
                return;
            }
            string card = "";
            if (message?.CardId != null)
            {
                int idx = Array.IndexOf(OracleCatalogData.CardOrder, message.CardId);
                if (idx >= 0) card = "【" + OracleCatalogData.CardLabelsJa[idx] + "】";
            }
            PaintMoment(outcome?.RecipeId, outcome?.LuckySeven == true, outcome != null, card + message?.Text, null);
        }

        public void ShowStatus(string text)
        {
            _status.text = text ?? "";
            bool visible = !string.IsNullOrEmpty(text);
            _status.gameObject.SetActive(visible);
            _statusBacking.gameObject.SetActive(visible);
            RefreshLuckyClue();
        }

        public void RefreshHistory(List<RoundSummary> history, IList<GardenMoment> moments = null)
        {
            _historyData = history.FindAll(round => round.Resolved && round.WorldRevision > 0);
            if (moments != null)
            {
                _recentMoments = new List<GardenMoment>(moments);
                if (_momentIndex > 0 && _selectedMoment != null)
                {
                    int index = 0; _momentIndex = 0;
                    foreach (var moment in _recentMoments)
                    {
                        if (_observationHabitat != null && moment.Habitat != _observationHabitat) continue;
                        if (moment.RoundId == _selectedMoment.RoundId && moment.CellId == _selectedMoment.CellId && moment.Kind == _selectedMoment.Kind) { _momentIndex = index; break; }
                        index++;
                    }
                }
                if (_memoryView != null) _memoryDirty = true;
            }
            _aftercareNotes = CareAftercare.Project(history);
            if (_activePanel == _historyPanel) RenderHistory();
            RefreshGraph(history);
        }
        private void RenderHistory()
        {
            var history = _historyData;
            int pageCount = Mathf.Max(1, (history.Count + HistoryPageSize - 1) / HistoryPageSize);
            _historyPage = Mathf.Clamp(_historyPage, 0, pageCount - 1);
            _historyPageLabel.text = $"{_historyPage + 1} / {pageCount}";
            _historyPageLabel.gameObject.SetActive(pageCount > 1);
            _newerRolls.gameObject.SetActive(_historyPage > 0);
            _olderRolls.gameObject.SetActive(_historyPage + 1 < pageCount);

            var sb = new StringBuilder();
            int start = history.Count - 1 - _historyPage * HistoryPageSize;
            int end = Mathf.Max(0, start - HistoryPageSize + 1);
            for (int i = start; i >= end; i--)
            {
                var r = history[i];
                sb.AppendLine($"{r.WorldRevision}投目    {RollRecord.FaceLabel(r.FaceId ?? 0, r.RecipeId, r.LuckySeven)}" +
                    (r.Rainbow ? "    虹" : ""));
                string wish = RollRecord.WishComparison(r.IntentKind, r.IntentValue, r.IntentMatched);
                if (wish != null) sb.AppendLine("　" + wish);
                if (r.PrayerText != null) sb.AppendLine("　祈り「" + r.PrayerText + "」");
                if (r.LuckyNumber) sb.AppendLine("　" + RollRecord.LuckyLabel(r.FaceId ?? 0, r.LuckySumFace));
                if (_observationDetails) sb.AppendLine($"　{WeatherLabel(r.WeatherId)}　{SiteLabel(r.SiteId)}");
                if (_observationDetails)
                {
                    var lives = new List<string>();
                    foreach (var k in r.EcologyKinds) { string n = ResidentLabelJa(k); if (n != null && !lives.Contains(n)) lives.Add(n); }
                    if (lives.Count > 0 || r.EcologySeeds > 0 || r.SeedBankDeposits > 0 || r.Harmony >= 0)
                        sb.AppendLine("　〔" + string.Join("・", lives) + "〕" +
                                      (r.SeedBankDeposits > 0 ? $" 土に残った種 ×{r.SeedBankDeposits}" : "") +
                                      (r.SeedBankSprouts > 0 ? $" 眠った種が芽吹いた ×{r.SeedBankSprouts}" : r.EcologySeeds > 0 ? $" 種がこぼれた ×{r.EcologySeeds}" : "") +
                                      (r.Harmony >= 0 ? $"　☯{r.Harmony}" : ""));
                }
                string relationship = EcologyObservations.Highlight(r.Relations);
                int shown = 0;
                foreach (var moment in _recentMoments)
                    if (moment.RoundId == r.RoundId && shown++ < 2)
                    { sb.AppendLine("　" + moment.Headline); if (moment.Detail != null) sb.AppendLine("　" + moment.Detail); }
                if (_observationDetails && r.Care != null) sb.Append(r.Care.Describe());
                if (shown == 0)
                {
                    string recorded = r.MomentHeadline ?? relationship ?? r.MessageText;
                    if (recorded != null) sb.AppendLine("　" + recorded);
                }
                sb.AppendLine();
            }
            if (history.Count == 0) sb.AppendLine("まだ記録がありません。");
            if (_historyPage == 0) sb.Append(CareAftercare.Describe(_aftercareNotes));
            SetScrollableText(_historyText, sb.ToString());
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
                MakeBar(i * width - 420f, width * 0.38f, (float)r.TotalWater / maxWater * 224f,
                    new Color(0.25f, 0.70f, 0.90f));
                MakeBar(i * width - 420f + width * 0.44f, width * 0.38f, (float)r.GrowthSum / maxGrowth * 224f,
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

        private static string SiteLabel(string id) => CareTargets.Name(id);

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
            text.raycastTarget = false;
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

        private GardenUiButton MakeChip(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string label,
            int fontSize, Action onClick)
        {
            var obj = new GameObject("Chip_" + label);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<Image>();
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var button = obj.AddComponent<GardenUiButton>();
            FrameButton(button);
            button.onClick.AddListener(() => onClick?.Invoke());
            var text = MakeText(button.Cap.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size, fontSize, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(10f, 0f);
            text.rectTransform.offsetMax = new Vector2(-10f, 0f);
            text.text = label;
            GardenType.MakeStrong(text); // every button label in the real SemiBold weight
            button.Label = text;
            return button;
        }
    }
}
