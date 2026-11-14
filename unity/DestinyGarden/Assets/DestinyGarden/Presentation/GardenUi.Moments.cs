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
        private const float MomentTop = -17f;
        // The left edge stays at x=366 or farther right on a 1600-unit canvas.
        private const float MomentWidth = 868f;
        private CanvasGroup _momentCard;
        private Image _momentSeal, _momentIcon;
        private Text _momentHeadline, _momentHabitat;
        private float _momentShownAt = float.NegativeInfinity;

        private void BuildMomentCard(Transform parent)
        {
            var plate = Decoration(parent, "Garden moment", GardenUiArt.Plaque);
            plate.type = Image.Type.Sliced;
            PlaceDecoration(plate, new Vector2(0.5f, 1f), new Vector2(0f, MomentTop), new Vector2(MomentWidth, 60f));
            _momentCard = plate.gameObject.AddComponent<CanvasGroup>();
            _momentCard.blocksRaycasts = false;
            _momentCard.interactable = false;

            _momentSeal = Decoration(plate.transform, "Blessing seal", GardenUiArt.Button(Surface.Enamel));
            _momentSeal.type = Image.Type.Sliced;
            PlaceDecoration(_momentSeal, new Vector2(0f, 0.5f), new Vector2(16f, 0f), Vector2.one * 42f);
            _momentIcon = Decoration(_momentSeal.transform, "Blessing mark", GardenUiArt.Icon(Symbol.Star));
            PlaceDecoration(_momentIcon, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 28f);

            _momentHeadline = MakeText(plate.transform, new Vector2(0f, 0.5f), new Vector2(78f, 0f),
                new Vector2(620f, 34f), 24, TextAnchor.MiddleLeft);
            _momentHeadline.name = "Moment headline";
            GardenType.MakeStrong(_momentHeadline);
            _momentHeadline.color = PaleGold;
            // A single line, with room for long species and resident names.
            _momentHeadline.verticalOverflow = VerticalWrapMode.Truncate;
            _momentHeadline.resizeTextForBestFit = true;
            _momentHeadline.resizeTextMinSize = 16;
            _momentHeadline.resizeTextMaxSize = 24;
            _momentHabitat = MakeText(plate.transform, new Vector2(1f, 0.5f), new Vector2(-20f, 0f),
                new Vector2(104f, 34f), 16, TextAnchor.MiddleLeft);
            _momentHabitat.name = "Moment habitat";
            _momentHabitat.color = GardenUiArt.Pigment(0xdcece6);
            var place = Decoration(_momentHabitat.transform, "Habitat mark", GardenUiArt.Icon(Symbol.Compass));
            place.color = PaleGold;
            PlaceDecoration(place, new Vector2(0f, 0.5f), new Vector2(-25f, 0f), Vector2.one * 18f);
            ShowMessage(null, null);
        }

        private void PaintMoment(string recipeId, bool lucky, bool live, string headline, string habitat, int faceId = 0)
        {
            string element = Array.IndexOf(RecipeCatalog.Order, recipeId) >= 0 ? ElementOfRecipe(recipeId) : null;
            var symbol = ElementSymbol(element);
            Color ink = element != null ? ElementLine(element) : PaleGold;
            ink.a = 1f;
            _momentSeal.sprite = GardenUiArt.Button(lucky ? Surface.Brass : BlessingSurface(element));
            _momentIcon.sprite = GardenUiArt.Icon(lucky ? Symbol.Die : symbol);
            _momentIcon.color = lucky ? BrassInk : ink;

            // A live roll begins with the face that came up, in its blessing's colour, so the strip and the event meet.
            if (live && faceId > 0 && element != null)
                headline = $"<color=#{ColorUtility.ToHtmlStringRGB(lucky ? PaleGold : ink)}>{faceId} {FaceName(recipeId)}</color>　{headline}";
            _momentHeadline.supportRichText = true;
            _momentHeadline.text = headline;
            bool hasHabitat = !string.IsNullOrEmpty(habitat);
            _momentHeadline.rectTransform.sizeDelta = new Vector2(hasHabitat ? 620f : 770f, 34f);
            _momentHabitat.text = habitat;
            _momentHabitat.gameObject.SetActive(hasHabitat);
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
    }
}
