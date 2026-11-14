using System;
using System.Collections.Generic;
using DestinyGarden.Domain;
using UnityEngine;
using UnityEngine.UI;
using Surface = DestinyGarden.Presentation.GardenUiArt.Surface;
using Symbol = DestinyGarden.Presentation.GardenUiArt.Symbol;

namespace DestinyGarden.Presentation
{
    public sealed partial class GardenUi
    {
        private readonly Stack<GameObject> _panelHistory = new Stack<GameObject>();
        private GameObject _panelLayer, _activePanel, _menuPanel, _wishPanel, _destinationPanel, _habitatPanel, _helpPanel, _newGardenPanel;
        private Button _resetViewButton, _muteButton, _motionButton;
        private GardenUiButton _menuButton, _wishButton;
        private Text _resetViewLabel;
        private Button _observedDestinationButton;
        private bool _choosingHabitat;

        internal bool IsPanelOpen => _activePanel != null;

        private void BuildRollControls(Transform parent)
        {
            var strip = new GameObject("PublishedFaces", typeof(RectTransform), typeof(Image));
            strip.transform.SetParent(parent, false);
            _faceStrip = (RectTransform)strip.transform;
            _faceStrip.anchorMin = _faceStrip.anchorMax = new Vector2(0.5f, 0f);
            _faceStrip.anchoredPosition = new Vector2(0f, 116f);
            _faceStrip.sizeDelta = new Vector2(986f, 40f);
            var background = strip.GetComponent<Image>();
            SetPlate(background, GardenUiArt.Plaque);
            background.raycastTarget = false;
            for (int i = 0; i < _faceLabels.Length; i++)
            {
                var wish = Decoration(_faceStrip, "Wished face", GardenUiArt.Button(Surface.Amethyst));
                wish.type = Image.Type.Sliced;
                PlaceDecoration(wish, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(FaceSpacing - 6f, 34f));
                wish.gameObject.SetActive(false);
                _faceWishBackings[i] = wish;
                _faceLabels[i] = MakeText(_faceStrip, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(FaceSpacing, 40f), 18, TextAnchor.MiddleCenter);
                var accent = Decoration(_faceLabels[i].transform, "Blessing rule", GardenUiArt.Rule);
                PlaceDecoration(accent, new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(72f, 3f));
                _faceAccents[i] = accent;
                var mark = Decoration(_faceLabels[i].transform, "Lucky mark", GardenUiArt.Icon(Symbol.LuckyStar));
                PlaceDecoration(mark, new Vector2(0.5f, 1f), new Vector2(0f, 13f), new Vector2(28f, 28f));
                mark.gameObject.SetActive(false);
                _faceMarks[i] = mark;
                // Tapping a face on the strip wishes for it; tapping it again withdraws the wish.
                int face = i + 1;
                _faceLabels[i].raycastTarget = true;
                var tap = _faceLabels[i].gameObject.AddComponent<Button>();
                tap.transition = Selectable.Transition.None;
                tap.navigation = new Navigation { mode = Navigation.Mode.None };
                tap.onClick.AddListener(() => TapFace(face));
            }
            _rollButton = MakeChip(parent, new Vector2(0.5f, 0f), new Vector2(0f, 23f),
                new Vector2(350f, 62f), "サイコロを振る", 26, () =>
                {
                    if (_rollEnabled && _activePanel == null && !_titleCard.activeSelf) OnRoll?.Invoke();
                });
            Engrave(_rollButton, Symbol.Die, 43f, 43f);
            StylePrimary(_rollButton);
            _wishButton = MakeChip(parent, new Vector2(0.5f, 0f), new Vector2(-306f, 25f),
                new Vector2(204f, 54f), "願いをかける", 19, () => OpenPanel(_wishPanel));
            Engrave(_wishButton, Symbol.Star, 28f, 16f);
            SetSurface(_wishButton, Surface.Amethyst);
            _luckyOffer = MakeChip(parent, new Vector2(0.5f, 0f), new Vector2(322f, 25f),
                new Vector2(248f, 54f), "七つめの面を加える", 18, () =>
                {
                    if (_rollEnabled) OnAddLuckySeven?.Invoke();
                });
            Engrave(_luckyOffer, Symbol.Star, 24f, 12f);
            SetSurface(_luckyOffer, Surface.Light);
            _luckyOfferGlow = Decoration(_luckyOffer.Cap.transform, "Seventh face glow", GardenUiArt.Focus);
            _luckyOfferGlow.type = Image.Type.Sliced;
            Stretch(_luckyOfferGlow.rectTransform);
            _luckyOfferGlow.color = PaleGold;
            _luckyOffer.gameObject.SetActive(false);
        }

        private Transform BuildNavigation(Transform canvas)
        {
            var parent = _gardenControls.transform;
            _menuButton = MakeChip(parent, Vector2.one, new Vector2(-34f, -21f),
                new Vector2(154f, 54f), "メニュー", 21, () => OpenPanel(_menuPanel));
            Engrave(_menuButton, Symbol.Menu, 26f, 15f);
            _resetViewButton = MakeChip(parent, Vector2.one, new Vector2(-34f, -88f),
                new Vector2(176f, 50f), "視点を戻す", 18, () => OnResetView?.Invoke());
            _resetViewLabel = _resetViewButton.GetComponentInChildren<Text>();
            _resetViewButton.gameObject.SetActive(false);

            _panelLayer = new GameObject("GardenPanels", typeof(RectTransform));
            _panelLayer.transform.SetParent(canvas, false);
            Stretch((RectTransform)_panelLayer.transform);
            var backdropObject = new GameObject("PanelBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdropObject.transform.SetParent(_panelLayer.transform, false);
            Stretch((RectTransform)backdropObject.transform);
            var backdrop = backdropObject.GetComponent<Image>();
            backdrop.color = new Color(0.015f, 0.032f, 0.046f, 0.66f);
            var dismiss = backdropObject.GetComponent<Button>();
            dismiss.targetGraphic = backdrop;
            dismiss.transition = Selectable.Transition.None;
            dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
            dismiss.onClick.AddListener(GoBack);
            var panelContent = new GameObject("PanelSafeArea", typeof(RectTransform));
            panelContent.transform.SetParent(_panelLayer.transform, false);
            panelContent.AddComponent<GardenSafeArea>();
            var panels = panelContent.transform;

            _menuPanel = MakePanel(panels, new Vector2(720f, 482f));
            AddPanelHeader(_menuPanel, "メニュー");
            var menu = _menuPanel.transform;
            MenuChip(menu, 0, 0, "庭を観察", Symbol.Observe, OpenObservation);
            var house = MenuChip(menu, 1, 0, "家を見る", Symbol.House, () =>
            {
                ClosePanels();
                OnViewHouse?.Invoke();
            });
            SetSurface(house, Surface.Light);
            _houseViewLabel = house.Label;
            _houseViewLabel.supportRichText = true;
            MenuChip(menu, 0, 1, "庭の記録", Symbol.Journal, () => OpenPanel(_graphPanel));
            MenuChip(menu, 1, 1, "図鑑", Symbol.Leaf, () => OpenPanel(_guidePanel));
            MenuChip(menu, 0, 2, "届け先を選ぶ", Symbol.Compass, () => OpenPanel(_destinationPanel));
            MenuChip(menu, 1, 2, "設定", Symbol.Settings, () => OpenPanel(_settingsPanel));
            var helpButton = MakeChip(menu, new Vector2(0.5f, 0f), new Vector2(-112f, 26f), new Vector2(206f, 52f),
                "遊び方", 21, () => OpenPanel(_helpPanel));
            Engrave(helpButton, Symbol.Help, 26f, 24f);
            var keepsake = MakeChip(menu, new Vector2(0.5f, 0f), new Vector2(112f, 26f), new Vector2(206f, 52f),
                "庭のしおり", 21, () => OnOpenKeepsake?.Invoke());
            Engrave(keepsake, Symbol.Bottle, 26f, 18f);
            _menuPanel.SetActive(false);

            BuildWishPanel(panels);
            BuildDestinationPanel(panels);
            _helpPanel = MakePanel(panels, new Vector2(780f, 415f));
            AddPanelHeader(_helpPanel, "遊び方");
            var help = MakeText(_helpPanel.transform, new Vector2(0f, 1f), new Vector2(38f, -108f),
                new Vector2(704f, 260f), 24, TextAnchor.UpperLeft);
            help.lineSpacing = 1.35f;
            help.text = "「サイコロを振る」で、水・風・光・種が庭に届きます。\n" +
                "「願いをかける」で、恵みや面を選べます。\n" +
                "届け先の変更は、次の一投の準備から反映されます。\n" +
                "ドラッグで見回し、ホイールや二本指で近づけます。\n" +
                "「庭を観察」で、出来事を振り返れます。";
            _helpPanel.SetActive(false);
            _panelLayer.SetActive(false);
            return panels;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private GardenUiButton MenuChip(Transform menu, int column, int row, string label, Symbol symbol, Action onClick)
        {
            var chip = MakeChip(menu, new Vector2(0.5f, 1f), new Vector2(-160f + column * 320f, -100f - row * 90f),
                new Vector2(296f, 72f), label, 24, onClick);
            Engrave(chip, symbol, 37f, 23f, leftAlign: true);
            return chip;
        }

        private void BuildWishPanel(Transform panels)
        {
            _wishPanel = MakePanel(panels, new Vector2(776f, 488f));
            AddPanelHeader(_wishPanel, "願いをかける");
            var parent = _wishPanel.transform;
            PanelText(parent, -94f, "願いは自由です。選ぶと庭へ戻ります。");
            var odds = MakeText(parent, new Vector2(0.5f, 1f), new Vector2(0f, -124f),
                new Vector2(690f, 26f), 16, TextAnchor.MiddleLeft);
            odds.color = new Color(0.73f, 0.84f, 0.82f);
            odds.text = "願いを選んでも、出目の確率は変わりません。";
            PanelText(parent, -149f, "恵みで選ぶ");
            string[] keys = { "water", "wind", "light", "seed" };
            string[] labels = { "水", "風", "光", "種" };
            Symbol[] symbols = { Symbol.Water, Symbol.Wind, Symbol.Star, Symbol.Seed };
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                var blessing = MakeChip(parent, new Vector2(0.5f, 1f),
                    new Vector2(-244f + i * 162f, -182f), new Vector2(148f, 52f), labels[i], 22,
                    () => ChooseWish("family", key));
                Engrave(blessing, symbols[i], 27f, 21f);
                SetSurface(blessing, BlessingSurface(key));
                _intentChips[key] = blessing;
            }

            PanelText(parent, -253f, "サイコロの面で選ぶ");
            for (int i = 0; i < _faceChips.Length; i++)
            {
                string face = (i + 1).ToString();
                _faceChips[i] = MakeChip(parent, new Vector2(0.5f, 1f),
                    new Vector2(-243f + (i % 4) * 162f, -286f - (i / 4) * 63f),
                    new Vector2(148f, 52f), "", 18, () => ChooseWish("face", face));
                _faceChips[i].gameObject.SetActive(false);
            }
            _intentChips["none"] = MakeChip(parent, new Vector2(0.5f, 0f), new Vector2(0f, 27f),
                new Vector2(220f, 48f), "願いなし", 19, () => ChooseWish("none", null));
            _wishPanel.SetActive(false);
        }

        private void TapFace(int face)
        {
            if (!_rollEnabled || _activePanel != null || _titleCard.activeSelf) return;
            if (_wishedFaceId == face) OnIntentSelected?.Invoke("none", null);
            else OnIntentSelected?.Invoke("face", face.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private void ChooseWish(string kind, string value)
        {
            OnIntentSelected?.Invoke(kind, value);
            ClosePanels();
        }

        private void BuildDestinationPanel(Transform panels)
        {
            _destinationPanel = MakePanel(panels, new Vector2(780f, 430f));
            AddPanelHeader(_destinationPanel, "届け先を選ぶ");
            var parent = _destinationPanel.transform;
            PanelText(parent, -96f, "ここで選んだ届け先は、次に準備される一投から使います。");
            _careSelection = MakeText(parent, new Vector2(0.5f, 1f), new Vector2(0f, -143f),
                new Vector2(700f, 36f), 21, TextAnchor.MiddleCenter);
            string[] keys = { "auto", "inlet", "shade", "meadow" };
            string[] labels = { "おまかせ", "入口", "岩陰", "ひらけた土" };
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                _siteChips[key] = MakeChip(parent, new Vector2(0.5f, 1f),
                    new Vector2(-162f + (i % 2) * 324f, -199f - (i / 2) * 67f),
                    new Vector2(300f, 52f), labels[i], 22, () =>
                    {
                        OnSiteSelected?.Invoke(key == "auto" ? "auto" : "fixed", key == "auto" ? null : key);
                        ClosePanels();
                    });
            }
            _observedDestinationButton = MakeChip(parent, new Vector2(0.5f, 0f), new Vector2(0f, 41f),
                new Vector2(420f, 52f), "水辺・草地などから選ぶ", 20, () => OpenHabitats(true));
            _targetNote = MakeText(parent, new Vector2(0.5f, 0f), new Vector2(0f, 8f),
                new Vector2(690f, 26f), 17, TextAnchor.MiddleCenter);
            _destinationPanel.SetActive(false);
        }

        private void PanelText(Transform panel, float top, string label)
        {
            var text = MakeText(panel, new Vector2(0f, 1f), new Vector2(38f, top),
                new Vector2(700f, 32f), 20, TextAnchor.UpperLeft);
            text.text = label;
        }

        private Text AddPanelHeader(GameObject panel, string title)
        {
            DecoratePanelHeader(panel.transform);
            var heading = MakeText(panel.transform, new Vector2(0f, 1f), new Vector2(36f, -22f),
                new Vector2(465f, 47f), 29, TextAnchor.MiddleLeft);
            heading.text = title;
            heading.color = PaleGold;
            GardenType.MakeStrong(heading);
            var back = MakeChip(panel.transform, Vector2.one, new Vector2(-23f, -19f),
                new Vector2(110f, 48f), "戻る", 18, GoBack);
            Engrave(back, Symbol.Back, 22f, 10f);
            back.gameObject.name = "PanelBack";
            return heading;
        }

        private void OpenObservation()
        {
            _observationDetails = false;
            _observationHabitat = null;
            _momentIndex = 0;
            _memoryView = null;
            OpenPanel(_observationPanel);
        }

        private void OpenHabitats(bool chooseDestination)
        {
            _choosingHabitat = chooseDestination;
            _habitatHeading.text = chooseDestination ? "届け先の環境を選ぶ" : "観察する場所を選ぶ";
            for (int i = 0; i < _habitatChoices.Count; i++)
            {
                var chip = _habitatChoices[i];
                bool visible = !chooseDestination || i > 0;
                chip.gameObject.SetActive(visible);
                if (!visible) continue;
                int position = i - (chooseDestination ? 1 : 0);
                ((RectTransform)chip.transform).anchoredPosition =
                    new Vector2(-226f + (position % 3) * 226f, -111f - (position / 3) * 82f);
                var label = chip.GetComponentInChildren<Text>();
                bool selected = !chooseDestination && label.text == (_observationHabitat ?? "全体");
                SetSurface(chip, selected ? Surface.Brass : Surface.Enamel);
            }
            OpenPanel(_habitatPanel);
        }

        private void RefreshSettings()
        {
            _muteButton.GetComponentInChildren<Text>().text = Audio?.Muted == true ? "音を戻す" : "音を消す";
            _motionButton.GetComponentInChildren<Text>().text = View?.ReducedMotion == true ? "動きを戻す" : "動きを抑える";
        }

        private void OpenPanel(GameObject panel)
        {
            if (!_rollEnabled || panel == null || panel == _activePanel || _titleCard.activeSelf) return;
            if (_activePanel != null)
            {
                _panelHistory.Push(_activePanel);
                _activePanel.SetActive(false);
            }
            ShowPanel(panel);
        }

        private void ShowPanel(GameObject panel)
        {
            _activePanel = panel;
            _panelLayer.SetActive(panel != null);
            UpdateGardenControls();
            if (panel == null)
            {
                if (_rollEnabled && !_titleCard.activeSelf) _rollButton.Select();
                return;
            }
            panel.SetActive(true);
            if (panel == _observationPanel) RenderObservation();
            if (panel == _settingsPanel) RefreshSettings();
            var back = panel.transform.Find("PanelBack");
            if (back != null) back.GetComponent<Button>().Select();
        }

        private void UpdateGardenControls()
        {
            bool visible = _activePanel == null && !_titleCard.activeSelf;
            _gardenControls.alpha = visible ? 1f : 0f;
            _gardenControls.interactable = visible;
            _gardenControls.blocksRaycasts = visible;
        }

        private void GoBack()
        {
            if (_activePanel == null) return;
            _activePanel.SetActive(false);
            ShowPanel(_panelHistory.Count > 0 ? _panelHistory.Pop() : null);
        }

        private void ClosePanels()
        {
            if (_activePanel != null) _activePanel.SetActive(false);
            _panelHistory.Clear();
            ShowPanel(null);
        }
    }
}
