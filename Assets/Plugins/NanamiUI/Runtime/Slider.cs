using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NanamiUI
{
    public class Slider : Component, IPointerDownHandler, IDragHandler
    {
        public float value = 50;
        public float max = 100;
        public float min;
        public ProgressTitleType titleType;
        public Text title;
        public RectTransform bar;
        public RectTransform barV;
        public RectTransform grip;
        public float barMaxWidthDelta;
        public float barMaxHeightDelta;

        public void Apply()
        {
            var percent = Mathf.Clamp01((value - min) / (max - min));
            if (title != null)
                title.text = titleType switch
                {
                    ProgressTitleType.Percent => Mathf.FloorToInt(percent * 100) + "%",
                    ProgressTitleType.ValueAndMax => Mathf.Round(value) + "/" + Mathf.Round(max),
                    ProgressTitleType.Value => "" + Mathf.Round(value),
                    _ => "" + Mathf.Round(max),
                };

            var rect = ((RectTransform)transform).rect;
            if (bar != null)
                bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.RoundToInt((rect.width - barMaxWidthDelta) * percent));
            if (barV != null)
                barV.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.RoundToInt((rect.height - barMaxHeightDelta) * percent));
        }

        public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

        public void OnDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out var point);
            var rect = ((RectTransform)transform).rect;
            var percent = bar != null
                ? (point.x - rect.xMin) / (rect.width - barMaxWidthDelta)
                : (rect.yMax - point.y) / (rect.height - barMaxHeightDelta);
            value = min + (max - min) * Mathf.Clamp01(percent);
            Apply();
        }
    }
}
