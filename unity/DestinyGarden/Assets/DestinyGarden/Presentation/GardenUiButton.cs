using UnityEngine;
using UnityEngine.UI;

namespace DestinyGarden.Presentation
{
    // Keep the hit area still while the cap, label and engraving move into the base.
    internal sealed class GardenUiButton : Button
    {
        internal Image Cap { get; private set; }
        internal Text Label { get; set; }
        internal Image Engraving { get; set; }
        private Image _focus;

        internal void SetCap(Image cap, Image focus)
        {
            Cap = cap;
            _focus = focus;
            targetGraphic = cap;
            colors = new ColorBlock
            {
                normalColor = new Color(0.94f, 0.94f, 0.94f),
                highlightedColor = Color.white,
                pressedColor = new Color(0.78f, 0.78f, 0.78f),
                selectedColor = Color.white,
                disabledColor = new Color(0.58f, 0.61f, 0.64f),
                colorMultiplier = 1f,
                fadeDuration = 0.07f
            };
            DoStateTransition(currentSelectionState, true);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (Cap != null && gameObject.activeInHierarchy) SetCapPosition(state);
        }

        protected override void InstantClearState()
        {
            base.InstantClearState();
            if (Cap != null) SetCapPosition(SelectionState.Normal);
        }

        private void SetCapPosition(SelectionState state)
        {
            float travel = state == SelectionState.Pressed ? 4f : 0f;
            Cap.rectTransform.offsetMin = new Vector2(0f, 5f - travel);
            Cap.rectTransform.offsetMax = new Vector2(0f, -travel);
            _focus.enabled = state == SelectionState.Selected || state == SelectionState.Highlighted;
            _focus.color = new Color(1f, 1f, 1f, state == SelectionState.Selected ? 1f : 0.5f);
        }
    }
}
