using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NanamiUI
{
    // 复刻 FairyGUI GObject 拖动：保偏移拖动 + 逐轴 RoundToInt（在 y-down 空间钳制再取整）。
    // onDragStart 返回 true = PreventDefault（改走 DragDropManager 的 agent 拖动）；dragBounds 为 parent-local、y-down 的钳制矩形。
    public sealed class Draggable : UIBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Rect? dragBounds;
        public Func<PointerEventData, bool> onDragStart;
        public Action onDragMove;
        public Action onDragEnd;
        public static Draggable dragging;

        private Vector2 _startAnchored;
        private Vector2 _startPointerLocal;
        private bool _prevented;

        private RectTransform Rt => (RectTransform)transform;
        private RectTransform ParentRt => (RectTransform)transform.parent;

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left)
                return;
            _prevented = onDragStart != null && onDragStart(e);
            if (_prevented)
                return;
            dragging = this;
            _startAnchored = Rt.anchoredPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(ParentRt, e.position, e.pressEventCamera, out _startPointerLocal);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_prevented)
            {
                DragDropManager.inst.MoveAgent(e);
                return;
            }
            if (dragging != this)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(ParentRt, e.position, e.pressEventCamera, out var p);
            var pos = _startAnchored + (p - _startPointerLocal);
            float x = pos.x, yd = -pos.y; // 转 y-down 做钳制（复刻 GObject dragBounds 语义）
            if (dragBounds is { } b)
            {
                x = Mathf.Clamp(x, b.x, Mathf.Max(b.x, b.xMax - Rt.rect.width));
                yd = Mathf.Clamp(yd, b.y, Mathf.Max(b.y, b.yMax - Rt.rect.height));
            }
            Rt.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(-yd));
            onDragMove?.Invoke();
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_prevented)
            {
                _prevented = false;
                DragDropManager.inst.Drop(e);
                return;
            }
            if (dragging != this)
                return;
            dragging = null;
            onDragEnd?.Invoke();
        }
    }
}
