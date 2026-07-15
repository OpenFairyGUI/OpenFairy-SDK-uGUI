using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    // 挂在 slider 的 grip 上（Migrate 烘焙，故独立成同名文件以便序列化）：按下记录起点、相对拖动驱动 Slider。
    // 复刻 GSlider 把 touch 处理器挂在 _gripObject 上——grip 常是独立 button 组件，会吞掉指针事件，
    // 事件必须在 grip 层接住，Slider 本体只处理轨道点按跳值。
    public sealed class SliderGrip : UIBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Slider slider;

        private Vector2 _clickPoint;
        private float _clickPercent;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)slider.transform, e.position, e.pressEventCamera, out _clickPoint);
            _clickPercent = Mathf.Clamp01((slider.value - slider.min) / (slider.max - slider.min));
            slider.onGripTouchBegin.Invoke(); // 复刻 FairyGUI：仅按到 grip 才发，点轨道不发
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)slider.transform, e.position, e.pressEventCamera, out var point);
            var rect = ((RectTransform)slider.transform).rect;
            var deltaPercent = slider.bar != null
                ? (point.x - _clickPoint.x) / (rect.width - slider.barMaxWidthDelta)
                : (_clickPoint.y - point.y) / (rect.height - slider.barMaxHeightDelta);
            if (slider.reverse)
                deltaPercent = -deltaPercent;
            slider.SetPercent(_clickPercent + deltaPercent);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left)
                slider.onGripTouchEnd.Invoke();
        }
    }
}
