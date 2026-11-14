using System;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using UnityEngine;
using UnityEngine.UI;
using Surface = DestinyGarden.Presentation.GardenUiArt.Surface;
using Symbol = DestinyGarden.Presentation.GardenUiArt.Symbol;

namespace DestinyGarden.Presentation
{
    public sealed partial class GardenUi
    {
        private const float MomentRevealDuration = 0.28f;
        private const float MomentTop = -106f;
        private CanvasGroup _momentCard;
        private Image _momentSeal, _momentIcon, _momentRule;
        private Text _momentHeadline, _momentCaption;
        private float _momentShownAt = float.NegativeInfinity;

        private void BuildMomentCard(Transform parent)
        {
            var plate = Decoration(parent, "Garden moment", GardenUiArt.Plaque);
            plate.type = Image.Type.Sliced;
            PlaceDecoration(plate, new Vector2(0.5f, 1f), new Vector2(0f, MomentTop), new Vector2(886f, 106f));
            _momentCard = plate.gameObject.AddComponent<CanvasGroup>();
            _momentCard.blocksRaycasts = false;
            _momentCard.interactable = false;

            _momentSeal = Decoration(plate.transform, "Blessing seal", GardenUiArt.Button(Surface.Enamel));
            _momentSeal.type = Image.Type.Sliced;
            PlaceDecoration(_momentSeal, new Vector2(0f, 0.5f), new Vector2(24f, 0f), Vector2.one * 64f);
            _momentIcon = Decoration(_momentSeal.transform, "Blessing mark", GardenUiArt.Icon(Symbol.Star));
            PlaceDecoration(_momentIcon, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 40f);

            _momentHeadline = MakeText(plate.transform, new Vector2(0f, 1f), new Vector2(112f, -10f),
                new Vector2(742f, 59f), 26, TextAnchor.MiddleLeft);
            _momentHeadline.name = "Moment headline";
            _momentHeadline.fontStyle = FontStyle.Bold;
            _momentHeadline.color = PaleGold;
            _momentHeadline.verticalOverflow = VerticalWrapMode.Truncate;
            _momentHeadline.resizeTextForBestFit = true;
            _momentHeadline.resizeTextMinSize = 21;
            _momentHeadline.resizeTextMaxSize = 26;
            _momentCaption = MakeText(plate.transform, new Vector2(0f, 0f), new Vector2(112f, 12f),
                new Vector2(742f, 28f), 17, TextAnchor.MiddleLeft);
            _momentCaption.name = "Moment caption";
            _momentCaption.color = GardenUiArt.Pigment(0xdcece6);
            _momentRule = Decoration(plate.transform, "Moment rule", GardenUiArt.Rule);
            PlaceDecoration(_momentRule, new Vector2(0f, 0f), new Vector2(112f, 8f), new Vector2(272f, 2f));
            ShowMessage(null, null);
        }

        private void PaintMoment(string recipeId, bool lucky, bool live, string headline, string caption)
        {
            string element = Array.IndexOf(RecipeCatalog.Order, recipeId) >= 0 ? ElementOfRecipe(recipeId) : null;
            var symbol = element switch
            {
                "water" => Symbol.Water,
                "wind" => Symbol.Wind,
                "seed" => Symbol.Seed,
                _ => Symbol.Star
            };
            Color ink = element != null ? ElementLine(element) : PaleGold;
            ink.a = 1f;
            _momentSeal.sprite = GardenUiArt.Button(lucky ? Surface.Brass : BlessingSurface(element));
            _momentIcon.sprite = GardenUiArt.Icon(lucky ? Symbol.Die : symbol);
            _momentIcon.color = lucky ? BrassInk : ink;
            _momentRule.color = lucky ? PaleGold : ink;

            _momentHeadline.text = headline;
            bool hasCaption = !string.IsNullOrEmpty(caption);
            _momentHeadline.rectTransform.sizeDelta = new Vector2(742f, hasCaption ? 59f : 86f);
            _momentCaption.text = caption;
            _momentCaption.gameObject.SetActive(hasCaption);
            _momentShownAt = live && View?.ReducedMotion != true ? Time.unscaledTime : float.NegativeInfinity;
            UpdateMomentCard();
        }

        private void UpdateMomentCard()
        {
            float elapsed = Time.unscaledTime - _momentShownAt;
            float progress = View?.ReducedMotion == true ? 1f : Mathf.Clamp01(elapsed / MomentRevealDuration);
            if (progress >= 1f) _momentShownAt = float.NegativeInfinity;
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            _momentCard.alpha = Mathf.Lerp(0.58f, 1f, eased);
            var rect = (RectTransform)_momentCard.transform;
            rect.anchoredPosition = new Vector2(0f, MomentTop + 8f * (1f - eased));
        }

        private static string MomentCaption(GardenMoment moment, RollOutcome outcome)
        {
            if (outcome == null || outcome.FaceId <= 0) return moment.Caption;
            string blessing = RecipeCatalog.LabelJa(outcome.RecipeId);
            string face = outcome.LuckySeven ? "幸運の七" : $"{outcome.FaceId}の目";
            if (outcome.LuckySeven) blessing += "×2";
            return $"{moment.Revision}投目　·　{face}「{blessing}」　/　{moment.Habitat ?? "庭"}";
        }
    }
}
