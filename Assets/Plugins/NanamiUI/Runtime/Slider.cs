using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NanamiUI
{
    public class Slider : Component, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public float value = 50;
        public float max = 100;
        public float min;
        public bool wholeNumbers;
        public bool changeOnClick = true; // FairyGUI GSlider 默认 true：点 bar 直接跳值；点 grip 则相对拖动、不跳
        public ProgressTitleType titleType;
        public TextField title;
        public RectTransform bar;
        public RectTransform barV;
        public RectTransform grip;
        public float barMaxWidthDelta;
        public float barMaxHeightDelta;
        public UnityEvent onChanged = new();
        public UnityEvent onGripTouchBegin = new(); // 复刻 GSlider.onGripTouchBegin/End
        public UnityEvent onGripTouchEnd = new();

        private bool _gripDrag;
        private Vector2 _clickPoint;
        private float _clickPercent;

        public void Apply()
        {
            var percent = Mathf.Clamp01((value - min) / (max - min));
            if (title != null)
                title.text = ProgressTitle.Format(titleType, value, min, max);

            var rect = ((RectTransform)transform).rect;
            if (bar != null)
                bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.RoundToInt((rect.width - barMaxWidthDelta) * percent));
            if (barV != null)
                barV.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.RoundToInt((rect.height - barMaxHeightDelta) * percent));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out var point);
            _gripDrag = grip != null && RectTransformUtility.RectangleContainsScreenPoint(grip, eventData.position, eventData.pressEventCamera);
            if (_gripDrag)
            {
                _clickPoint = point;
                _clickPercent = Mathf.Clamp01((value - min) / (max - min));
                onGripTouchBegin.Invoke(); // 复刻 FairyGUI：仅按到 grip 才发，点轨道不发
            }
            else if (changeOnClick)
                UpdateFromPoint(point);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_gripDrag)
                onGripTouchEnd.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out var point);
            if (_gripDrag)
            {
                var rect = ((RectTransform)transform).rect;
                var deltaPercent = bar != null
                    ? (point.x - _clickPoint.x) / (rect.width - barMaxWidthDelta)
                    : (_clickPoint.y - point.y) / (rect.height - barMaxHeightDelta);
                SetPercent(_clickPercent + deltaPercent);
            }
            else if (changeOnClick)
                UpdateFromPoint(point);
        }

        private void UpdateFromPoint(Vector2 point)
        {
            var rect = ((RectTransform)transform).rect;
            var percent = bar != null
                ? (point.x - rect.xMin) / (rect.width - barMaxWidthDelta)
                : (rect.yMax - point.y) / (rect.height - barMaxHeightDelta);
            SetPercent(percent);
        }

        private void SetPercent(float percent)
        {
            var newValue = min + (max - min) * Mathf.Clamp01(percent);
            if (wholeNumbers)
                newValue = Mathf.Round(newValue);
            if (newValue == value)
                return;
            value = newValue;
            Apply();
            onChanged.Invoke();
        }
    }
}
