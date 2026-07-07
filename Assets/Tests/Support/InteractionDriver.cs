using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NanamiUI.TestSupport
{
    // NanamiUI 侧交互驱动：只经非泛型 Runtime 面驱动，故不依赖生成的 UI.{包} 类型。
    // Slider 用"由目标 value 反算出的合成指针"经真实 OnDrag 达到该值（同时验证 ScreenPoint↔Local 往返 + Apply）；
    // Button/Checkbox 经真实 OnPointerClick（Check/Radio 会翻 selected）。
    public static class InteractionDriver
    {
        public static void Drive(GameObject target, ParityCatalog.ActionKind action, float param, Camera camera)
        {
            switch (action)
            {
                case ParityCatalog.ActionKind.SliderValue:
                    DriveSlider(target.GetComponent<Slider>(), param, camera);
                    break;
                case ParityCatalog.ActionKind.ButtonSelected:
                    ((IPointerClickHandler)target.GetComponent<IPointerClickHandler>())
                        .OnPointerClick(new PointerEventData(EventSystem.current));
                    break;
                case ParityCatalog.ActionKind.ButtonDown:
                    ((IPointerDownHandler)target.GetComponent<IPointerDownHandler>())
                        .OnPointerDown(new PointerEventData(EventSystem.current));
                    break;
            }
        }

        private static void DriveSlider(Slider slider, float value, Camera camera)
        {
            var rt = (RectTransform)slider.transform;
            var rect = rt.rect;
            var percent = Mathf.Clamp01((value - slider.min) / (slider.max - slider.min));

            // 反算出会经 OnDrag 得到 value 的指针局部点（复刻 Slider.OnDrag 的 percent 公式）。
            Vector2 local = slider.bar != null
                ? new Vector2(rect.xMin + percent * (rect.width - slider.barMaxWidthDelta), rect.center.y)
                : new Vector2(rect.center.x, rect.yMax - percent * (rect.height - slider.barMaxHeightDelta));

            var world = rt.TransformPoint(new Vector3(local.x, local.y, 0));
            var screen = RectTransformUtility.WorldToScreenPoint(camera, world);
            // pressEventCamera 是只读、由 press 射线的 raycaster 推出。rig 画布上的 GraphicRaycaster.eventCamera
            // 即画布 worldCamera（= 我们的相机），塞进 pointerPressRaycast.module 即可让 OnDrag 反投影用对相机。
            var eventData = new PointerEventData(EventSystem.current) { position = screen };
            eventData.pointerPressRaycast = new RaycastResult { module = slider.GetComponentInParent<GraphicRaycaster>() };
            slider.OnPointerDown(eventData);
        }
    }
}
