using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NanamiUI
{
    public class Slider : Component, IPointerDownHandler
    {
        public float value = 50;
        public float max = 100;
        public float min;
        public bool wholeNumbers;
        public bool changeOnClick = true; // FairyGUI GSlider 默认 true：点 bar 直接跳值；点 grip 则相对拖动、不跳
        public bool reverse;              // 复刻 GSlider._reverse：填充方向反向、拖动增量反号
        public ProgressTitleType titleType;
        public TextField title;
        public RectTransform bar;
        public RectTransform barV;
        public RectTransform grip;
        public float barMaxWidthDelta;
        public float barMaxHeightDelta;
        public float barStartX;
        public float barStartY;
        public UnityEvent onChanged = new();
        public UnityEvent onGripTouchBegin = new(); // 复刻 GSlider.onGripTouchBegin/End（由 grip 上的 SliderGrip 发）
        public UnityEvent onGripTouchEnd = new();

        public void Apply()
        {
            var percent = Mathf.Clamp01((value - min) / (max - min));
            if (title != null)
                title.text = ProgressTitle.Format(titleType, value, min, max);

            var rect = ((RectTransform)transform).rect;
            if (bar != null && !SetFillAmount(bar, percent))
            {
                var fullWidth = rect.width - barMaxWidthDelta;
                var w = Mathf.RoundToInt(fullWidth * percent);
                bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
                if (reverse)
                    bar.anchoredPosition = new Vector2(barStartX + (fullWidth - w), bar.anchoredPosition.y);
            }
            if (barV != null && !SetFillAmount(barV, percent))
            {
                var fullHeight = rect.height - barMaxHeightDelta;
                var h = Mathf.RoundToInt(fullHeight * percent);
                barV.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
                if (reverse)
                    barV.anchoredPosition = new Vector2(barV.anchoredPosition.x, -(barStartY + (fullHeight - h)));
            }
        }

        // 复刻 GSlider.SetFillAmount：bar 是 Filled Image 时用 fillAmount 而非缩尺寸。
        private bool SetFillAmount(RectTransform barRt, float percent)
        {
            var image = barRt.GetComponent<UnityEngine.UI.Image>();
            if (image == null || image.type != UnityEngine.UI.Image.Type.Filled)
                return false;
            image.fillAmount = reverse ? 1 - percent : percent;
            return true;
        }

        // 轨道点按（grip 的按下/拖动被 grip 上的 SliderGrip 接住，到不了这里）。
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !changeOnClick)
                return;
            JumpFromClick(eventData); // 复刻 GSlider.__barTouchBegin：只跳一次，不跟踪拖动
        }

        // 一次跳值 = 当前值 + 点击点相对 grip 中心的偏移（复刻 __barTouchBegin）；无 grip 时按轨道绝对位置映射。
        private void JumpFromClick(PointerEventData eventData)
        {
            var rect = ((RectTransform)transform).rect;
            float percent;
            if (grip != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(grip, eventData.position, eventData.pressEventCamera, out var gp);
                var delta = bar != null
                    ? (gp.x - grip.rect.center.x) / (rect.width - barMaxWidthDelta)
                    : (grip.rect.center.y - gp.y) / (rect.height - barMaxHeightDelta);
                if (reverse)
                    delta = -delta;
                percent = Mathf.Clamp01((value - min) / (max - min)) + delta;
            }
            else
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out var point);
                percent = bar != null
                    ? (point.x - rect.xMin) / (rect.width - barMaxWidthDelta)
                    : (rect.yMax - point.y) / (rect.height - barMaxHeightDelta);
                if (reverse)
                    percent = 1 - percent;
            }
            SetPercent(percent);
        }

        internal void SetPercent(float percent)
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
