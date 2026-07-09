using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NanamiUI.TestSupport
{
    // NanamiUI 侧交互驱动：只经非泛型 Runtime 面驱动，故不依赖生成的 UI.{包} 类型。
    // Slider 用"由目标 value 反算出的合成指针"经真实 OnPointerDown 达到该值（changeOnClick 路径，同时验证 ScreenPoint↔Local 往返 + Apply）；
    // 连续 OnDrag 拖动路径由 BakedInteractionTests.Baked_slider_continuous_drag 覆盖。
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

        // 按名切控制器的页（复刻 FairyGUI GetController(name).selectedIndex）。Controller<T> 是泛型 struct、
        // 生成的 UI.{包} 类型测试程序集不可达，故经反射设 m_{name}.page（同 NanamiUI.Example.BasicsMain.SetPage）。
        // 运行时 page setter 触发 gears 缓动。pageName 用生成枚举名（"_0"/"_1"）。
        public static void DriveControllerPage(GameObject root, string controllerName, string pageName)
        {
            var fieldName = "m_" + controllerName;
            foreach (var component in root.GetComponents<NanamiUI.Component>())
            {
                var field = component.GetType().GetField(fieldName);
                if (field == null)
                    continue;
                var controller = field.GetValue(component); // 装箱的 Controller<T>
                var pageProp = controller.GetType().GetProperty("page");
                pageProp.SetValue(controller, Enum.Parse(pageProp.PropertyType, pageName)); // setter 里 gears 作用于真实 GameObject
                field.SetValue(component, controller); // 结构体写回，持久化 _page
                return;
            }
        }

        private static void DriveSlider(Slider slider, float value, Camera camera)
        {
            var rt = (RectTransform)slider.transform;
            var rect = rt.rect;
            var percent = Mathf.Clamp01((value - slider.min) / (slider.max - slider.min));

            // 反算出会经轨道点击跳到 value 的指针位置（复刻 Slider.JumpFromClick 的公式）：
            // 有 grip 时跳值 = 当前值 + 相对 grip 中心的偏移；无 grip 时按轨道绝对位置映射。
            Vector3 world;
            if (slider.grip != null)
            {
                var current = Mathf.Clamp01((slider.value - slider.min) / (slider.max - slider.min));
                var track = slider.bar != null ? rect.width - slider.barMaxWidthDelta : rect.height - slider.barMaxHeightDelta;
                var offset = (percent - current) * track * (slider.reverse ? -1 : 1);
                var gripCenter = slider.grip.TransformPoint(slider.grip.rect.center);
                world = gripCenter + rt.TransformVector(slider.bar != null ? new Vector3(offset, 0) : new Vector3(0, -offset));
            }
            else
            {
                Vector2 local = slider.bar != null
                    ? new Vector2(rect.xMin + percent * (rect.width - slider.barMaxWidthDelta), rect.center.y)
                    : new Vector2(rect.center.x, rect.yMax - percent * (rect.height - slider.barMaxHeightDelta));
                world = rt.TransformPoint(new Vector3(local.x, local.y, 0));
            }
            var screen = RectTransformUtility.WorldToScreenPoint(camera, world);
            // pressEventCamera 是只读、由 press 射线的 raycaster 推出。rig 画布上的 GraphicRaycaster.eventCamera
            // 即画布 worldCamera（= 我们的相机），塞进 pointerPressRaycast.module 即可让 OnDrag 反投影用对相机。
            var eventData = new PointerEventData(EventSystem.current) { position = screen };
            eventData.pointerPressRaycast = new RaycastResult { module = slider.GetComponentInParent<GraphicRaycaster>() };
            slider.OnPointerDown(eventData);
        }
    }
}
