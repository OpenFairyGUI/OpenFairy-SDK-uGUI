using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    public sealed class AuthoredScrollbar : Scrollbar, IEndDragHandler
    {
        public ScrollPane pane;
        public bool horizontal;
        public void StepBackward() => pane.Step(horizontal, false, 1);
        public void StepForward() => pane.Step(horizontal, true, 1);

        public override void OnBeginDrag(PointerEventData data)
        {
            base.OnBeginDrag(data);
            if (data.button == PointerEventData.InputButton.Left && IsActive() && IsInteractable()) pane.SetBarDragging(true);
        }
        public void OnEndDrag(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) pane.SetBarDragging(false); }

        protected override void OnDisable()
        {
            if (pane != null) pane.SetBarDragging(false);
            base.OnDisable();
        }

        public override void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
            if (RectTransformUtility.RectangleContainsScreenPoint(handleRect, data.position, data.pressEventCamera))
            {
                base.OnPointerDown(data);
                return;
            }
            var area = (RectTransform)handleRect.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, data.position, data.pressEventCamera, out var point);
            var center = area.InverseTransformPoint(handleRect.TransformPoint(handleRect.rect.center));
            pane.Step(horizontal, horizontal ? point.x > center.x : point.y < center.y, 4);
        }
    }
}
