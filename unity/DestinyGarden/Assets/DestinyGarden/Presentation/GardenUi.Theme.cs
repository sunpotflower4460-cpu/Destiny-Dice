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
        private Image _openingLettering, _openingRule;
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
            button.Label.fontStyle = FontStyle.Bold;
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
        }

        private void LayoutOpeningDecorations(bool side, bool portrait)
        {
            var anchor = new Vector2(side ? 0f : 0.5f, 1f);
            var letteringSize = side ? new Vector2(460f, 290f) : portrait ? new Vector2(410f, 258f) : new Vector2(270f, 170f);
            if (_openingLettering != null)
                PlaceDecoration(_openingLettering, anchor, new Vector2(0f, side ? 0f : -4f), letteringSize);
            PlaceDecoration(_openingRule, anchor, new Vector2(side ? 5f : 0f, -letteringSize.y - (side ? 0f : 4f)),
                new Vector2(Mathf.Min(430f, letteringSize.x), side ? 13f : 10f));
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
