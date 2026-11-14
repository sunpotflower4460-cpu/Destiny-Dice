using System.Collections.Generic;
using DestinyGarden.Application;
using UnityEngine;
using UnityEngine.UI;
using Symbol = DestinyGarden.Presentation.GardenUiArt.Symbol;

namespace DestinyGarden.Presentation
{
    public sealed partial class GardenUi
    {
        private readonly List<GardenMoment> _reviewMoments = new List<GardenMoment>();
        private GameObject _momentReviewPanel;
        private GardenUiButton _reviewButton, _reviewPrevious, _reviewNext, _reviewPlace;
        private Text _reviewCounter, _reviewCaption, _reviewHeadline, _reviewDetail, _reviewFortune;
        private int _reviewIndex;

        private void BuildMomentReview(Transform controls, Transform panels)
        {
            _reviewButton = MakeChip(controls, new Vector2(0.5f, 1f), new Vector2(0f, -186f),
                new Vector2(242f, 45f), "この投の出来事", 18, () =>
                {
                    RenderMomentReview();
                    OpenPanel(_momentReviewPanel);
                });
            Engrave(_reviewButton, Symbol.Observe, 22f, 14f);
            _reviewButton.gameObject.SetActive(false);

            _momentReviewPanel = MakePanel(panels, new Vector2(820f, 480f));
            AddPanelHeader(_momentReviewPanel, "この投の出来事");
            var panel = _momentReviewPanel.transform;
            _reviewCaption = ReviewLine(panel, -91f, 24f, 18);
            _reviewFortune = ReviewLine(panel, -132f, 82f, 19);
            _reviewFortune.color = PaleGold;
            _reviewHeadline = ReviewLine(panel, -226f, 74f, 29);
            GardenType.MakeStrong(_reviewHeadline);
            _reviewDetail = ReviewLine(panel, -305f, 66f, 20);

            var bottom = new Vector2(0.5f, 0f);
            _reviewPrevious = MakeChip(panel, bottom, new Vector2(-291f, 24f), new Vector2(124f, 48f),
                "前へ", 18, () => { _reviewIndex--; RenderMomentReview(); });
            _reviewCounter = MakeText(panel, bottom, new Vector2(-117f, 31f), new Vector2(132f, 32f),
                17, TextAnchor.MiddleCenter);
            _reviewNext = MakeChip(panel, bottom, new Vector2(7f, 24f), new Vector2(124f, 48f),
                "次へ", 18, () => { _reviewIndex++; RenderMomentReview(); });
            _reviewPlace = MakeChip(panel, bottom, new Vector2(239f, 24f), new Vector2(230f, 48f),
                "この場所を見る", 18, () =>
                {
                    var moment = _reviewMoments[_reviewIndex];
                    ShowMessage(null, null, moment);
                    ClosePanels();
                    OnObserveCell?.Invoke(moment.CellId);
                });
            Engrave(_reviewPlace, Symbol.Compass, 21f, 12f);
            _momentReviewPanel.SetActive(false);
        }

        private Text ReviewLine(Transform panel, float top, float height, int size)
        {
            var line = MakeText(panel, new Vector2(0.5f, 1f), new Vector2(0f, top),
                new Vector2(728f, height), size, TextAnchor.UpperLeft);
            line.verticalOverflow = VerticalWrapMode.Truncate;
            return line;
        }

        internal void ShowMomentReview(IEnumerable<GardenMoment> moments, string roundId, string fortune)
        {
            _reviewMoments.Clear();
            _reviewIndex = 0;
            if (roundId != null)
                foreach (var moment in moments)
                    if (moment.RoundId == roundId) _reviewMoments.Add(moment);
            _reviewFortune.text = fortune ?? "";
            bool show = _reviewMoments.Count != 0;
            _reviewButton.gameObject.SetActive(show);
            if (show) _reviewButton.Label.text = fortune == null ? "この投の出来事" : "幸運と出来事を読む";
        }

        private void RenderMomentReview()
        {
            if (_reviewMoments.Count == 0) return;
            _reviewIndex = Mathf.Clamp(_reviewIndex, 0, _reviewMoments.Count - 1);
            var moment = _reviewMoments[_reviewIndex];
            bool hasFortune = !string.IsNullOrEmpty(_reviewFortune.text);
            _reviewCaption.text = moment.Caption;
            _reviewHeadline.text = moment.Headline;
            _reviewHeadline.rectTransform.anchoredPosition = new Vector2(0f, hasFortune ? -226f : -160f);
            _reviewDetail.text = moment.Detail ?? "";
            _reviewDetail.rectTransform.anchoredPosition = new Vector2(0f, hasFortune ? -305f : -240f);
            _reviewCounter.text = $"{_reviewIndex + 1} / {_reviewMoments.Count}";
            _reviewPrevious.interactable = _reviewIndex > 0;
            _reviewNext.interactable = _reviewIndex + 1 < _reviewMoments.Count;
            _reviewPlace.interactable = moment.CellId >= 0;
        }
    }
}
