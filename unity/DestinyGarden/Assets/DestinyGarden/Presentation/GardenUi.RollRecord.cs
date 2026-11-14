using DestinyGarden.Application;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace DestinyGarden.Presentation
{
    public sealed partial class GardenUi
    {
        private const int HistoryPageSize = 16;
        private static string PrayerPlaceholder => $"言葉で祈る（{RollRecord.PrayerLimit}文字まで）";
#if UNITY_WEBGL && !UNITY_EDITOR
        private RectTransform _browserPrayerBounds;
        private Rect _browserPrayerRect;
        private readonly Vector3[] _browserPrayerCorners = new Vector3[4];
        private bool _browserPrayerOpen, _captureKeyboard;

        [DllImport("__Internal")] private static extern void GardenPrayerInputOpen(string value, string placeholder, int limit);
        [DllImport("__Internal")] private static extern void GardenPrayerInputPlace(float left, float top, float width, float height);
        [DllImport("__Internal")] private static extern string GardenPrayerInputRead();
        [DllImport("__Internal")] private static extern void GardenPrayerInputClose();
#else
        private InputField _writtenPrayer;
#endif
        private string _prayerDraft;
        private Text _rollResult, _rollContext;
        private Text _luckyClue;
        private Image _luckyClueBacking;
        private int _luckyClueFace;
        private int _historyPage;
        private Text _historyPageLabel;
        private GardenUiButton _newerRolls, _olderRolls;
        private bool _wishEditable = true;

        internal string PrayerDraft => _prayerDraft;

        private void BuildWrittenPrayer(Transform panel)
        {
            var field = new GameObject("Written prayer", typeof(RectTransform), typeof(Image));
            field.transform.SetParent(panel, false);
            var rect = field.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -428f);
            rect.sizeDelta = new Vector2(668f, 56f);
            SetPlate(field.GetComponent<Image>(), GardenUiArt.Well);

#if UNITY_WEBGL && !UNITY_EDITOR
            // A DOM field can receive an actual mobile keyboard and browser IME composition.
            _browserPrayerBounds = rect;
#else
            var entered = MakeText(field.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero, 22, TextAnchor.MiddleLeft);
            var placeholder = MakeText(field.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero, 20, TextAnchor.MiddleLeft);
            foreach (var label in new[] { entered, placeholder })
            {
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(15f, 4f);
                label.rectTransform.offsetMax = new Vector2(-15f, -4f);
                label.supportRichText = false;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Truncate;
            }
            placeholder.text = PrayerPlaceholder;
            placeholder.color = GardenUiArt.Pigment(0xb7c2be);
            _writtenPrayer = field.AddComponent<InputField>();
            _writtenPrayer.targetGraphic = field.GetComponent<Image>();
            _writtenPrayer.textComponent = entered;
            _writtenPrayer.placeholder = placeholder;
            _writtenPrayer.lineType = InputField.LineType.SingleLine;
            _writtenPrayer.keyboardType = TouchScreenKeyboardType.Default;
            _writtenPrayer.characterLimit = RollRecord.PrayerLimit * 2;
#if !ENABLE_LEGACY_INPUT_MANAGER
            var module = FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (module != null && module.inputOverride == null)
                module.inputOverride = module.gameObject.AddComponent<PrayerImeInput>();
#endif
#endif

            var apply = MakeChip(panel, new Vector2(0.5f, 0f), new Vector2(138f, 27f),
                new Vector2(234f, 48f), "祈りを添える", 19, () =>
                {
                    ApplyWrittenPrayer();
                    ClosePanels();
                });
            StylePrimary(apply);
        }

        private void SetWrittenPrayerVisible(bool visible)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (visible == _browserPrayerOpen) return;
            _browserPrayerOpen = visible;
            if (!visible)
            {
                GardenPrayerInputClose();
                WebGLInput.captureAllKeyboardInput = _captureKeyboard;
                return;
            }
            _captureKeyboard = WebGLInput.captureAllKeyboardInput;
            WebGLInput.captureAllKeyboardInput = false;
            GardenPrayerInputOpen(_prayerDraft ?? "", PrayerPlaceholder, RollRecord.PrayerLimit * 2);
            _browserPrayerRect = default;
            Canvas.ForceUpdateCanvases();
            LateUpdate();
#else
            if (visible) _writtenPrayer.SetTextWithoutNotify(_prayerDraft ?? "");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void OnDisable() => SetWrittenPrayerVisible(false);

        private void LateUpdate()
        {
            if (!_browserPrayerOpen || Screen.width <= 0 || Screen.height <= 0) return;
            _browserPrayerBounds.GetWorldCorners(_browserPrayerCorners);
            Vector2 lower = RectTransformUtility.WorldToScreenPoint(null, _browserPrayerCorners[0]);
            Vector2 upper = RectTransformUtility.WorldToScreenPoint(null, _browserPrayerCorners[2]);
            var rect = new Rect(lower.x / Screen.width, 1f - upper.y / Screen.height,
                (upper.x - lower.x) / Screen.width, (upper.y - lower.y) / Screen.height);
            if (rect == _browserPrayerRect) return;
            _browserPrayerRect = rect;
            GardenPrayerInputPlace(rect.x, rect.y, rect.width, rect.height);
        }
#endif

        internal void SetWishEditable(bool editable)
        {
            _wishEditable = editable;
            _wishButton.interactable = _rollEnabled && editable;
        }

        internal void SetPrayerDraft(string text)
        {
            _prayerDraft = text;
#if !UNITY_WEBGL || UNITY_EDITOR
            if (_writtenPrayer != null) _writtenPrayer.SetTextWithoutNotify(text ?? "");
#endif
            RefreshWishLight();
        }

        private void ApplyWrittenPrayer()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_browserPrayerOpen) SetPrayerDraft(RollRecord.NormalizePrayer(GardenPrayerInputRead()));
#else
            SetPrayerDraft(RollRecord.NormalizePrayer(_writtenPrayer.text));
            _writtenPrayer.DeactivateInputField();
#endif
        }

        private void RefreshWishLight()
        {
            if (_wishButton != null)
                _wishButton.Engraving.color = _hasSelectedWish || _prayerDraft != null ? PaleGold : WishInk;
        }

        private void BuildLuckyClue()
        {
            _luckyClue = MakeText(_faceStrip, new Vector2(0.5f, 1f), new Vector2(0f, 29f),
                new Vector2(194f, 23f), 17, TextAnchor.MiddleCenter);
            _luckyClue.name = "Lucky number clue";
            _luckyClue.text = "ラッキーナンバーの目";
            _luckyClue.color = PaleGold;
            _luckyClueBacking = AddBacking(_luckyClue, new Vector2(5f, 2f));
            RefreshLuckyClue();
        }

        internal void ShowLuckyClue(int faceId)
        {
            _luckyClueFace = faceId;
            if (faceId > 0)
            {
                var position = new Vector2(_faceLabels[faceId - 1].rectTransform.anchoredPosition.x, 29f);
                _luckyClue.rectTransform.anchoredPosition = position;
                _luckyClueBacking.rectTransform.anchoredPosition = position + Vector2.up * 2f;
            }
            RefreshLuckyClue();
        }

        private void RefreshLuckyClue()
        {
            if (_luckyClue == null) return;
            bool visible = _luckyClueFace > 0 && !_status.gameObject.activeSelf;
            _luckyClue.gameObject.SetActive(visible);
            _luckyClueBacking.gameObject.SetActive(visible);
        }

        private void BuildHistoryPages(Transform panel)
        {
            var anchor = new Vector2(0.5f, 0f);
            _newerRolls = MakeChip(panel, anchor, new Vector2(-175f, 25f), new Vector2(210f, 48f),
                "新しい投", 20, () => { _historyPage--; RenderHistory(); });
            _olderRolls = MakeChip(panel, anchor, new Vector2(175f, 25f), new Vector2(210f, 48f),
                "古い投", 20, () => { _historyPage++; RenderHistory(); });
            _historyPageLabel = MakeText(panel, anchor, new Vector2(0f, 31f), new Vector2(114f, 36f),
                18, TextAnchor.MiddleCenter);
        }

        private void BuildRollResult(Transform card)
        {
            _rollResult = MakeText(card, new Vector2(0f, 1f), new Vector2(78f, -8f),
                new Vector2(730f, 35f), 26, TextAnchor.MiddleLeft);
            _rollResult.name = "Rolled face and blessing";
            _rollResult.color = PaleGold;
            GardenType.MakeStrong(_rollResult);
            _rollContext = MakeText(card, new Vector2(0f, 1f), new Vector2(78f, -78f),
                new Vector2(762f, 30f), 19, TextAnchor.MiddleLeft);
            _rollContext.name = "Wish and lucky number";
            _rollContext.color = GardenUiArt.Pigment(0xdbe5e2);
            foreach (var line in new[] { _rollResult, _rollContext })
            {
                line.horizontalOverflow = HorizontalWrapMode.Overflow;
                line.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        private void LayoutRollResult(int faceId, string recipeId, bool amplified, string comparison, bool hasPrayer, string lucky)
        {
            bool hasResult = faceId > 0;
            string context = comparison ?? (hasPrayer ? "祈りを記録しました" : null);
            if (lucky != null) context = context == null ? lucky : context + "    ·    " + lucky;
            bool hasContext = !string.IsNullOrEmpty(context);
            _rollResult.gameObject.SetActive(hasResult);
            _rollResult.text = hasResult
                ? faceId + "の目" + (recipeId != null ? "  ·  " + FaceName(recipeId) : "") + (amplified ? " ×2" : "")
                : "";
            _rollResult.color = amplified || recipeId == null ? PaleGold : ElementLine(ElementOfRecipe(recipeId));
            _rollContext.gameObject.SetActive(hasContext);
            _rollContext.text = context;
            _rollContext.color = lucky != null ? PaleGold : GardenUiArt.Pigment(0xdbe5e2);
            float height = hasContext ? 116f : hasResult ? 88f : 60f;
            var card = (RectTransform)_momentCard.transform;
            card.sizeDelta = new Vector2(MomentWidth, height);
            _momentHeadline.rectTransform.anchoredPosition = new Vector2(78f, height * 0.5f - (hasResult ? 59f : 30f));
            _momentHabitat.rectTransform.anchoredPosition = new Vector2(-20f, _momentHeadline.rectTransform.anchoredPosition.y);
            _momentSeal.rectTransform.anchoredPosition = new Vector2(16f, height * 0.5f - (hasResult ? 34f : 30f));
        }

        internal void ShowSavedRoll(RoundSummary round, GardenMoment moment)
        {
            if (round?.FaceId == null) return;
            string headline = round.MomentHeadline ?? moment?.Headline ?? round.MessageText;
            _momentCard.gameObject.SetActive(true);
            LayoutRollResult(round.FaceId.Value, round.RecipeId, round.LuckySeven,
                RollRecord.WishComparison(round.IntentKind, round.IntentValue, round.IntentMatched),
                round.PrayerText != null, round.LuckyNumber ? RollRecord.LuckyLabel(round.FaceId.Value, round.LuckySumFace) : null);
            PaintMoment(round.RecipeId, round.LuckySeven, false, headline, moment?.Habitat);
        }
    }
}
