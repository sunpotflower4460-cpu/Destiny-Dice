using UnityEngine;

namespace DestinyGarden.Presentation
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class GardenSafeArea : MonoBehaviour
    {
        private RectTransform _layout;
        private Rect _lastArea;
        private Vector2 _lastSize;

        private void OnEnable()
        {
            _layout = (RectTransform)transform;
            _lastSize = Vector2.zero;
            Update();
        }

        private void Update()
        {
            var size = new Vector2(Screen.width, Screen.height);
            var safeArea = Screen.safeArea;
            if (size.x <= 0f || size.y <= 0f || (size == _lastSize && safeArea == _lastArea)) return;

            _lastSize = size;
            _lastArea = safeArea;
            Apply(_layout, safeArea, size);
        }

        public static void Apply(RectTransform layout, Rect safeArea, Vector2 screenSize)
        {
            // Rotation can briefly report empty bounds before the OS supplies the new insets.
            if (safeArea.width <= 0f || safeArea.height <= 0f) safeArea = new Rect(Vector2.zero, screenSize);
            var pixelsToAnchors = new Vector2(1f / screenSize.x, 1f / screenSize.y);
            layout.anchorMin = Vector2.Max(Vector2.zero, Vector2.Scale(safeArea.min, pixelsToAnchors));
            layout.anchorMax = Vector2.Min(Vector2.one, Vector2.Scale(safeArea.max, pixelsToAnchors));
            layout.offsetMin = layout.offsetMax = Vector2.zero;
        }
    }
}
