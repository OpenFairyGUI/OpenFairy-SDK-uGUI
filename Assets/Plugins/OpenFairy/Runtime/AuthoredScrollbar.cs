using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    public sealed class AuthoredScrollbar : Scrollbar, IEndDragHandler
    {
        public ScrollPane pane;
        public bool horizontal;
        public bool fixedGripSize;
        [SerializeField] internal float authoredGripSize;
        private Vector2 _dragOffset;
        public void StepBackward() => pane.Step(horizontal, false, 1);
        public void StepForward() => pane.Step(horizontal, true, 1);

        protected override void OnEnable()
        {
            base.OnEnable();
            Canvas.preWillRenderCanvases += Queue;
        }

        private void Queue() => CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);

        public override void Rebuild(CanvasUpdate executing)
        {
            // Base Prelayout sends onValueChanged in Editor; a render rebuild must not synthesize input.
            if (executing == CanvasUpdate.PostLayout) ApplyGeometry();
        }

        protected override void Update() { base.Update(); ApplyGeometry(); }
        private void LateUpdate() => ApplyGeometry();
        public override void SetValueWithoutNotify(float input) { base.SetValueWithoutNotify(input); ApplyGeometry(); }

        private void ApplyGeometry()
        {
            if (handleRect == null) return;
            var alongX = direction is Direction.LeftToRight or Direction.RightToLeft;
            var reversed = direction is Direction.RightToLeft or Direction.TopToBottom;
            var track = (RectTransform)handleRect.parent;
            var root = (RectTransform)transform;
            var point = PixelAlignment.LocalPoint(track, new Vector3(track.rect.xMin, track.rect.yMax), root)
                - new Vector3(root.rect.xMin, root.rect.yMax);
            var extent = alongX ? track.rect.width : track.rect.height;
            var length = fixedGripSize ? authoredGripSize : Mathf.Floor(size * extent);
            var fraction = reversed ? 1 - value : value;
            var start = alongX ? point.x : -point.y;
            var offset = (extent - length) * (alongX ? fraction : 1 - fraction);
            var position = Mathf.Round(start + offset) - start;
            var movement = Mathf.Clamp01(reversed ? 1 - value : value) * (1 - size);
            var anchorMin = handleRect.anchorMin;
            var anchorMax = handleRect.anchorMax;
            var axis = alongX ? 0 : 1;
            anchorMin[axis] = movement;
            anchorMax[axis] = movement + size;
            handleRect.anchorMin = anchorMin;
            handleRect.anchorMax = anchorMax;
            var min = handleRect.offsetMin;
            var max = handleRect.offsetMax;
            min[axis] = (alongX ? position : extent - position - length) - movement * extent;
            max[axis] = min[axis] + length - size * extent;
            handleRect.offsetMin = min;
            handleRect.offsetMax = max;
            handleRect.gameObject.SetActive(size is > 0 and < 1);
        }

        public override void OnBeginDrag(PointerEventData data)
        {
            base.OnBeginDrag(data);
            if (data.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
            var track = (RectTransform)handleRect.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(track, data.position, data.pressEventCamera, out var point);
            var topLeft = PixelAlignment.LocalPoint(handleRect, new Vector3(handleRect.rect.xMin, handleRect.rect.yMax), track);
            _dragOffset = point - (Vector2)topLeft;
            pane.SetBarDragging(true);
        }
        public override void OnDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
            var track = (RectTransform)handleRect.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(track, data.position, data.pressEventCamera, out var point);
            var alongX = direction is Direction.LeftToRight or Direction.RightToLeft;
            var reversed = direction is Direction.RightToLeft or Direction.TopToBottom;
            var remaining = alongX ? track.rect.width - handleRect.rect.width : track.rect.height - handleRect.rect.height;
            var fraction = remaining == 0 ? 0 : (alongX ? point.x - _dragOffset.x - track.rect.xMin
                : point.y - _dragOffset.y - track.rect.yMin - handleRect.rect.height) / remaining;
            value = Mathf.Clamp01(reversed ? 1 - fraction : fraction);
            ApplyGeometry();
        }
        public void OnEndDrag(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) pane.SetBarDragging(false); }

        protected override void OnDisable()
        {
            Canvas.preWillRenderCanvases -= Queue;
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
