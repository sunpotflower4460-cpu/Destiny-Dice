using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Symbol = DestinyGarden.Presentation.GardenUiArt.Symbol;

namespace DestinyGarden.Presentation
{
    /// <summary>庭のしおり: the garden's keepsake card, every 48 rolls and from the menu. Saved facts only.</summary>
    public sealed partial class GardenUi
    {
        private GameObject _keepsakePanel;
        private Text _keepsakeName, _keepsakeHouse, _keepsakeCounts, _keepsakeFlowers, _keepsakeCreatures;

        /// <summary>The menu asks for the card (the bootstrap builds it from the saved garden).</summary>
        public Action OnOpenKeepsake;

        internal bool KeepsakeOpen => _keepsakePanel != null && _keepsakePanel.activeSelf;

        private void BuildKeepsakePanel(Transform parent)
        {
            _keepsakePanel = MakePanel(parent, new Vector2(860f, 520f));
            _keepsakePanel.name = "Keepsake";
            AddPanelHeader(_keepsakePanel, "庭のしおり");
            var card = _keepsakePanel.transform;
            _keepsakeName = MakeText(card, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(780f, 52f), 34, TextAnchor.MiddleCenter);
            _keepsakeName.color = PaleGold;
            GardenType.MakeStrong(_keepsakeName);
            _keepsakeHouse = MakeText(card, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(780f, 32f), 21, TextAnchor.MiddleCenter);
            var rule = Decoration(card, "Keepsake rule", GardenUiArt.Rule);
            PlaceDecoration(rule, new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(420f, 3f));
            rule.color = new Color(PaleGold.r, PaleGold.g, PaleGold.b, 0.6f);
            _keepsakeCounts = MakeText(card, new Vector2(0.5f, 1f), new Vector2(0f, -218f), new Vector2(780f, 36f), 22, TextAnchor.MiddleCenter);
            _keepsakeCounts.supportRichText = true;
            _keepsakeFlowers = MakeText(card, new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(760f, 70f), 20, TextAnchor.MiddleCenter);
            _keepsakeCreatures = MakeText(card, new Vector2(0.5f, 1f), new Vector2(0f, -352f), new Vector2(760f, 70f), 20, TextAnchor.MiddleCenter);
            foreach (var t in new[] { _keepsakeFlowers, _keepsakeCreatures })
            {
                t.supportRichText = true;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.lineSpacing = 1.15f;
            }
            MakeChip(card, new Vector2(0.5f, 0f), new Vector2(-262f, 32f), new Vector2(232f, 54f), "記念に残す", 20, () =>
            {
                // Hide every overlay for four seconds (the same as the setting), to keep the garden as it is now.
                ClosePanels();
                OnHideUi?.Invoke();
            });
            var go = MakeChip(card, new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(232f, 54f), "続ける", 21, ClosePanels);
            StylePrimary(go);
            MakeChip(card, new Vector2(0.5f, 0f), new Vector2(262f, 32f), new Vector2(232f, 54f), "新しい庭", 20, () => OpenPanel(_newGardenPanel));
            _keepsakePanel.SetActive(false);
        }

        private static string Gold(int n) => $"<color=#f5daa1>{n.ToString(CultureInfo.InvariantCulture)}</color>";

        /// <summary>Shows the card. Returns false when it cannot open now (rolling, title, another panel without the menu).</summary>
        public bool ShowKeepsake(GardenKeepsake.Card card)
        {
            if (card == null || _keepsakePanel == null) return false;
            _keepsakeName.text = $"〈{card.Name}〉";
            _keepsakeHouse.text = $"{card.Rolls.ToString(CultureInfo.InvariantCulture)}投　{card.House}";
            var counts = new List<string> { "ラッキーナンバー " + Gold(card.LuckyNumbers) };
            if (card.Rainbows > 0) counts.Add("八の虹 " + Gold(card.Rainbows));
            if (card.LuckyPlants > 0) counts.Add("幸運の株 " + Gold(card.LuckyPlants));
            counts.Add("叶った願い " + Gold(card.Wishes));
            _keepsakeCounts.text = string.Join("　", counts);
            string flowers = GardenKeepsake.NameList(card.Flowers), creatures = GardenKeepsake.NameList(card.Creatures);
            _keepsakeFlowers.text = flowers != null ? "<color=#cdbfa8>咲いた花</color>　" + flowers : "";
            _keepsakeCreatures.text = creatures != null ? "<color=#cdbfa8>出会った生き物</color>　" + creatures : "";
            if (_activePanel != null && _activePanel != _menuPanel) return false;
            OpenPanel(_keepsakePanel);
            return KeepsakeOpen;
        }
    }
}
