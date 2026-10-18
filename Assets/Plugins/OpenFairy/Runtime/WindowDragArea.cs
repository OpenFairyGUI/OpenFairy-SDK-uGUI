using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    public sealed class WindowDragArea : UIBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] internal Window window;
        private Vector2 _start;
        private Vector2 _startPointer;
        private bool _dragging;

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            _dragging = true;
            Root.inst.BringToFront(window);
            var content = window.contentPane;
            _start = content.anchoredPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)content.parent, e.position, e.pressEventCamera, out _startPointer);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging || e.button != PointerEventData.InputButton.Left) return;
            var content = window.contentPane;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)content.parent, e.position, e.pressEventCamera, out var position);
            content.anchoredPosition = _start + position - _startPointer;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) _dragging = false;
        }

        protected override void OnDisable()
        {
            _dragging = false;
            base.OnDisable();
        }
    }
}
