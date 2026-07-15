using System;
using System.Collections;
using System.Collections.Generic;
using OpenFairy.UGUI.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    // 系统性可达性扫：对每个交互 demo 页，枚举页内每个 active 的按钮/下拉（ButtonBase），真实射线打其中心，
    // 断言命中落在它自己子树内、且解析到一个 IPointerClickHandler；下拉还须解析到下拉自身（内部 button 面不得抢走点击）。
    // 这是"每个可交互元素点了都真有反应"的枚举式保证——不是抽查代表元素，防的正是"被内部子组件/覆盖层截走、点了没反应"这类系统性盲区。
    public class PageInteractionReachabilityTests
    {
        // 交互面已烘焙、无需 demo 胶水即可点的页（Grid/List 的项在运行时由胶水填充，故不在此扫）。
        private static readonly string[] Pages =
        {
            "Demo_Button", "Demo_ComboBox", "Demo_Component", "Demo_Label",
        };

        private static bool IsComboBox(Type t)
        {
            for (; t != null; t = t.BaseType)
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ComboBox<>))
                    return true;
            return false;
        }

        [UnityTest]
        public IEnumerator Every_active_button_center_receives_its_own_click()
        {
            var rig = new OpenFairyPageRenderer();
            rig.Setup();
            var failures = new List<string>();
            var swept = 0;

            foreach (var page in Pages)
            {
                rig.LoadComponent("Basics", page);
                rig.PlaceCamera();
                var go = rig.Instance;
                if (go == null)
                {
                    failures.Add($"{page}: prefab 缺失");
                    continue;
                }
                yield return null;

                var raycaster = rig.Raycaster;
                foreach (var button in go.GetComponentsInChildren<OpenFairy.UGUI.ButtonBase>(false)) // 只扫 active
                {
                    var rt = (RectTransform)button.transform;
                    if (rt.rect.width < 2 || rt.rect.height < 2)
                        continue; // 零尺寸占位不计
                    // 跳过嵌套在另一个 Button/ComboBox 里的子面（如 Dropdown 的内部 button 面）——外层才是点击单元。
                    if (button.transform.parent != null && button.transform.parent.GetComponentInParent<OpenFairy.UGUI.ButtonBase>() != null)
                        continue;
                    // 跳过 touchable=false（烘成 CanvasGroup.blocksRaycasts=false）的故意不可点元素（如 Button demo 的禁用按钮 n32）。
                    if (System.Array.Exists(button.GetComponentsInParent<CanvasGroup>(true), cg => !cg.blocksRaycasts))
                        continue;
                    swept++;
                    var screen = RectTransformUtility.WorldToScreenPoint(rig.Camera, rt.TransformPoint(rt.rect.center));
                    var e = new PointerEventData(EventSystem.current) { position = screen };
                    var hits = new List<RaycastResult>();
                    raycaster.Raycast(e, hits);
                    if (hits.Count == 0)
                    {
                        failures.Add($"{page}/{button.name}: 中心射线无命中（点了没反应）");
                        continue;
                    }
                    var hitTf = hits[0].gameObject.transform;
                    if (hitTf != rt && !hitTf.IsChildOf(rt))
                    {
                        failures.Add($"{page}/{button.name}: 中心命中落在自身子树外（{hits[0].gameObject.name}），被遮挡");
                        continue;
                    }
                    var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                    if (handler == null)
                    {
                        failures.Add($"{page}/{button.name}: 命中处向上找不到 IPointerClickHandler");
                        continue;
                    }
                    if (IsComboBox(button.GetType()) && handler != button.gameObject)
                        failures.Add($"{page}/{button.name}: 下拉点击被内部元素抢走（解析到 {handler.name}），点了不弹下拉");
                }

                rig.Unload();
                yield return null;
            }

            rig.Teardown();
            Assert.Greater(swept, 0, "应至少扫到若干可交互按钮");
            Assert.IsEmpty(failures, $"扫了 {swept} 个交互元素，以下中心点击不可达/被抢：\n" + string.Join("\n", failures));
        }
    }
}
