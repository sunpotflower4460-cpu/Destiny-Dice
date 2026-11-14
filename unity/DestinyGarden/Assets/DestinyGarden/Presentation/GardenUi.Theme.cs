using UnityEngine;
using UnityEngine.UI;
using Surface = DestinyGarden.Presentation.GardenUiArt.Surface;
using Symbol = DestinyGarden.Presentation.GardenUiArt.Symbol;

namespace DestinyGarden.Presentation
{
    public sealed partial class GardenUi
    {
        private static readonly Color BrassInk = GardenUiArt.Pigment(0x231611);
        private static readonly Color PaleGold = GardenUiArt.Pigment(0xf5daa1);
        private static readonly Color WishInk = GardenUiArt.Pigment(0xe2d2ff);
        private Image _openingLettering, _openingRule, _openingSeal, _openingDie, _openingArrow;
        private RectTransform _openingBlessings;
        private readonly Image[] _openingMarks = new Image[4];
        private readonly Image[] _faceAccents = new Image[7];

        private static Image Decoration(Transform parent, string name, Sprite sprite)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static void PlaceDecoration(Image image, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetPlate(Image image, Sprite plate)
        {
            image.sprite = plate;
            image.type = Image.Type.Sliced;
        }

        private static void FrameButton(GardenUiButton button)
        {
            SetPlate(button.GetComponent<Image>(), GardenUiArt.Well);
            var cap = Decoration(button.transform, "Button cap", GardenUiArt.Button(Surface.Enamel));
            cap.type = Image.Type.Sliced;
            Stretch(cap.rectTransform);
            var focus = Decoration(cap.transform, "Focus ring", GardenUiArt.Focus);
            focus.type = Image.Type.Sliced;
            Stretch(focus.rectTransform);
            focus.rectTransform.offsetMin = new Vector2(3f, 3f);
            focus.rectTransform.offsetMax = new Vector2(-3f, -3f);
            button.SetCap(cap, focus);
        }

        private static void Engrave(GardenUiButton chip, Symbol symbol, float size, float inset, bool leftAlign = false)
        {
            chip.Engraving = Decoration(chip.Cap.transform, "Engraving", GardenUiArt.Icon(symbol));
            chip.Engraving.color = PaleGold;
            PlaceDecoration(chip.Engraving, new Vector2(0f, 0.5f), new Vector2(inset, 0f), Vector2.one * size);
            chip.Label.rectTransform.offsetMin = new Vector2(inset + size + 8f, 0f);
            if (leftAlign) chip.Label.alignment = TextAnchor.MiddleLeft;
        }

        private static void SetSurface(GardenUiButton chip, Surface surface)
        {
            chip.Cap.sprite = GardenUiArt.Button(surface);
            bool brass = surface == Surface.Brass;
            chip.Label.color = brass ? BrassInk : TextColor;
            chip.Label.GetComponent<Shadow>().enabled = !brass;
            if (chip.Engraving == null) return;
            Color ink = surface switch
            {
                Surface.Brass => BrassInk,
                Surface.Amethyst => WishInk,
                Surface.Water => ElementLine("water"),
                Surface.Wind => ElementLine("wind"),
                Surface.Light => ElementLine("light"),
                Surface.Seed => ElementLine("seed"),
                _ => PaleGold
            };
            ink.a = 1f;
            chip.Engraving.color = ink;
        }

        private static void StylePrimary(GardenUiButton button)
        {
            SetSurface(button, Surface.Brass);
            GardenType.MakeStrong(button.Label);
        }

        private static Surface BlessingSurface(string element) => element switch
        {
            "water" => Surface.Water,
            "wind" => Surface.Wind,
            "light" => Surface.Light,
            "seed" => Surface.Seed,
            _ => Surface.Enamel
        };

        private void DecorateOpening(Transform parent)
        {
            var shade = Decoration(parent, "Opening shade", GardenUiArt.Veil);
            Stretch(shade.rectTransform);
            shade.rectTransform.offsetMin = new Vector2(-170f, -74f);
            shade.rectTransform.offsetMax = new Vector2(170f, 74f);
            var lettering = GardenUiArt.TitleLettering;
            if (lettering != null)
            {
                _openingLettering = Decoration(parent, "Destiny Garden", lettering);
                _openingLettering.preserveAspect = true;
            }
            _openingRule = Decoration(parent, "Opening rule", GardenUiArt.TitleRule);

            _openingBlessings = (RectTransform)new GameObject("Opening blessings", typeof(RectTransform)).transform;
            _openingBlessings.SetParent(parent, false);
            _openingMarks[0] = OpeningBlessing(Symbol.Water, "water");
            _openingMarks[1] = OpeningBlessing(Symbol.Wind, "wind");
            _openingMarks[2] = OpeningBlessing(Symbol.Star, "light");
            _openingMarks[3] = OpeningBlessing(Symbol.Seed, "seed");
        }

        private Image OpeningBlessing(Symbol symbol, string element)
        {
            var icon = Decoration(_openingBlessings, symbol.ToString(), GardenUiArt.Icon(symbol));
            var pigment = ElementLine(element);
            pigment.a = 1f;
            icon.color = Color.Lerp(pigment, PaleGold, 0.22f);
            return icon;
        }

        private void DecorateOpeningEntry(GardenUiButton button)
        {
            SetPlate(button.Cap, GardenUiArt.OpeningButton);
            button.Label.color = GardenUiArt.Pigment(0xffeec8);
            _openingSeal = Decoration(button.Cap.transform, "Dice seal", GardenUiArt.OpeningSeal);
            _openingDie = Decoration(_openingSeal.transform, "Die", GardenUiArt.Icon(Symbol.Die));
            _openingDie.color = button.Label.color;
            _openingArrow = Decoration(button.Cap.transform, "Entry arrow", GardenUiArt.Icon(Symbol.Back));
            _openingArrow.color = PaleGold;
            _openingArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        }

        private static void PlaceOpeningText(Text text, float x, float y, float width, float height, int fontSize, float scale)
        {
            text.rectTransform.anchoredPosition = new Vector2(x, y) * scale;
            text.rectTransform.sizeDelta = new Vector2(width, height) * scale;
            text.fontSize = Mathf.RoundToInt(fontSize * scale);
        }

        private void LayoutOpeningEntry(float scale)
        {
            var label = _enterButton.Label;
            label.fontSize = Mathf.RoundToInt(30 * scale);
            label.rectTransform.offsetMin = new Vector2(77f, 0f) * scale;
            label.rectTransform.offsetMax = new Vector2(-53f, 0f) * scale;
            PlaceDecoration(_openingSeal, new Vector2(0f, 0.5f), new Vector2(15f, 0f) * scale, Vector2.one * (54f * scale));
            PlaceDecoration(_openingDie, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (36f * scale));
            PlaceDecoration(_openingArrow, new Vector2(1f, 0.5f), new Vector2(-32f, 0f) * scale, Vector2.one * (26f * scale));
        }

        private void LayoutOpeningDecorations(bool side, bool portrait, float scale)
        {
            var anchor = new Vector2(side ? 0f : 0.5f, 1f);
            var letteringSize = side ? new Vector2(460f, 290f) : portrait ? new Vector2(410f, 258f) : new Vector2(270f, 170f);
            if (_openingLettering != null)
                PlaceDecoration(_openingLettering, anchor, new Vector2(0f, side ? 0f : -4f) * scale, letteringSize * scale);
            PlaceDecoration(_openingRule, anchor, new Vector2(side ? 5f : 0f, -letteringSize.y - (side ? 0f : 4f)) * scale,
                new Vector2(Mathf.Min(430f, letteringSize.x), side ? 13f : 10f) * scale);
            _openingBlessings.anchorMin = _openingBlessings.anchorMax = _openingBlessings.pivot = anchor;
            _openingBlessings.anchoredPosition = new Vector2(side ? 4f * scale : 0f,
                _openingTagline.rectTransform.anchoredPosition.y - 56f * scale);
            const float markSize = 28f, spacing = 46f;
            _openingBlessings.sizeDelta = new Vector2((_openingMarks.Length - 1) * spacing + markSize, markSize) * scale;
            for (int i = 0; i < _openingMarks.Length; i++)
                PlaceDecoration(_openingMarks[i], new Vector2(0f, 0.5f),
                    new Vector2(i * spacing * scale, 0f), Vector2.one * (markSize * scale));
        }

        private static void DecoratePanelHeader(Transform panel)
        {
            var separator = Decoration(panel, "Header rule", GardenUiArt.Rule);
            var rect = separator.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(30f, -81f);
            rect.offsetMax = new Vector2(-30f, -79f);
        }
    }
}
