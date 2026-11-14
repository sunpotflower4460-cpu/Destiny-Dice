using DestinyGarden.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Surface = DestinyGarden.Presentation.GardenUiArt.Surface;
using Symbol = DestinyGarden.Presentation.GardenUiArt.Symbol;

namespace DestinyGarden.Presentation
{
    public sealed partial class GardenUi
    {
        private GardenUiButton _magicDestination;
        private GardenPlotPointer _plotPointer;
        private Text _plotNotice;
        private bool _selectingPlot;

        private void BuildMagicDestination(Transform parent)
        {
            var backdrop = new GameObject("Pick a garden plot", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.SetAsFirstSibling();
            Stretch((RectTransform)backdrop.transform);
            backdrop.GetComponent<Image>().color = Color.clear;
            _plotPointer = backdrop.AddComponent<GardenPlotPointer>();
            _plotPointer.CancelSelection = () => SetPlotSelection(false);
            _plotPointer.OnTap = screenPoint =>
            {
                int cell = View != null ? View.PickGardenPlot(screenPoint) : -1;
                if (cell < 0)
                {
                    _plotNotice.text = "庭の土を選んでください";
                    return;
                }
                OnSiteSelected?.Invoke(CareTargets.CellMode, CareTargets.CellSiteId(cell));
                SetPlotSelection(false);
            };
            backdrop.SetActive(false);

            var anchor = new Vector2(0.5f, 0f);
            _magicDestination = MakeChip(parent, anchor, new Vector2(306f, 25f), new Vector2(204f, 54f),
                "魔法の届け先", 19, () => SetPlotSelection(!_selectingPlot));
            Engrave(_magicDestination, Symbol.Compass, 26f, 16f);
            _plotNotice = MakeText(_magicDestination.transform, anchor, new Vector2(0f, 54f),
                new Vector2(250f, 25f), 17, TextAnchor.MiddleCenter);
            _plotNotice.name = "Selected magic destination";
            _plotNotice.color = PaleGold;
        }

        private void SetPlotSelection(bool active)
        {
            if (active && (!_rollEnabled || IsPanelOpen || IsOpening)) return;
            _selectingPlot = active;
            _plotPointer.gameObject.SetActive(active);
            _magicDestination.Label.text = active ? "選択をやめる" : "魔法の届け先";
            SetSurface(_magicDestination, active ? Surface.Brass : Surface.Enamel);
            RefreshPlotNotice();
            EventSystem.current?.SetSelectedGameObject(active ? _plotPointer.gameObject : _magicDestination.gameObject);
            if (active && View?.Rig?.IsHouseFocused == true) OnResetView?.Invoke();
        }

        private string _savedPlotNotice;

        internal void ShowMagicDestination(bool enabled, int cell, bool applied)
        {
            _magicDestination.gameObject.SetActive(enabled);
            ((RectTransform)_luckyOffer.transform).anchoredPosition = new Vector2(enabled ? 548f : 322f, 25f);
            _savedPlotNotice = cell < 0 ? "" : (applied ? "今回 " : "次の準備から ") + CareTargets.Position(cell);
            if (!enabled && _selectingPlot) SetPlotSelection(false);
            RefreshPlotNotice();
        }

        private void RefreshPlotNotice()
        {
            _plotNotice.text = _selectingPlot ? "庭の土をタップ・クリック" : _savedPlotNotice;
        }
    }

    internal sealed class GardenPlotPointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, ICancelHandler
    {
        internal System.Action<Vector2> OnTap;
        internal System.Action CancelSelection;
        private int? _pointer;
        private bool _cancelled;

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            if (_pointer.HasValue) { _cancelled = true; return; }
            _pointer = data.pointerId;
            _cancelled = false;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (_pointer == data.pointerId) _pointer = null;
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (!_cancelled && data.button == PointerEventData.InputButton.Left) OnTap?.Invoke(data.position);
        }

        public void OnBeginDrag(PointerEventData data) { _cancelled = true; }
        public void OnDrag(PointerEventData data) { }
        public void OnCancel(BaseEventData data) => CancelSelection?.Invoke();
        private void OnDisable() { _pointer = null; _cancelled = false; }
    }
}
