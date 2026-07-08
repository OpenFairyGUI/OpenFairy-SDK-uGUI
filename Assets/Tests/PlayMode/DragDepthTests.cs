using System.Collections;
using NanamiUI.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NanamiUI.Tests
{
    // Depth（sortingOrder 兄弟序）+ Draggable（保偏移拖动 + 取整）+ DragDrop（agent + DropTarget）的运行时机制回归。
    // 直接驱动 Runtime（不经生成的 UI.{包} 类型），断言分析上精确的终点，故无需 FairyGUI 参照。
    public class DragDepthTests
    {
        private NanamiPageRenderer _rig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _rig = new NanamiPageRenderer();
            _rig.Setup();
            _rig.Configure(1136, 640);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _rig.Teardown();
            yield return null;
        }

        private RectTransform Child(RectTransform parent, string name, Vector2 fairyXY, Vector2 size, bool graphic)
        {
            var go = graphic
                ? new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image))
                : new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(fairyXY.x, -fairyXY.y);
            return rt;
        }

        private Vector2 ScreenOf(RectTransform parent, Vector2 local) =>
            RectTransformUtility.WorldToScreenPoint(_rig.Camera, parent.TransformPoint(local));

        private PointerEventData Ped(Vector2 screen) =>
            new(EventSystem.current) { position = screen, pointerPressRaycast = new RaycastResult { module = _rig.Raycaster } };

        [UnityTest]
        public IEnumerator SortingOrder_matches_FairyGUI_sibling_model()
        {
            var container = Child(_rig.CanvasRt, "container", Vector2.zero, new Vector2(600, 600), false);
            var fixedShape = Child(container, "fixed", new Vector2(50, 50), new Vector2(150, 150), true);

            NanamiUI.Depth.SetSortingOrder(fixedShape, 100);
            var red0 = NanamiUI.Depth.CreateRect(container, new Vector2(60, 60), 150, 150, 1, Color.black, Color.red, 0);
            var red1 = NanamiUI.Depth.CreateRect(container, new Vector2(70, 70), 150, 150, 1, Color.black, Color.red, 0);
            var green0 = NanamiUI.Depth.CreateRect(container, new Vector2(80, 80), 150, 150, 1, Color.black, Color.green, 200);
            var green1 = NanamiUI.Depth.CreateRect(container, new Vector2(90, 90), 150, 150, 1, Color.black, Color.green, 200);
            yield return null;

            // 期望兄弟序：order 0 保持插入序在前，order>0 升序在后 → [red0, red1, fixed, green0, green1]
            var order = new[] { red0.transform, red1.transform, fixedShape.transform, green0.transform, green1.transform };
            for (var i = 0; i < order.Length; i++)
                Assert.AreEqual(i, order[i].GetSiblingIndex(), $"child {order[i].name} at wrong sibling index");

            Object.Destroy(container.gameObject);
        }

        [UnityTest]
        public IEnumerator Draggable_free_drag_offsets_by_pointer_delta()
        {
            var container = Child(_rig.CanvasRt, "container", Vector2.zero, new Vector2(600, 600), false);
            var child = Child(container, "obj", new Vector2(100, 100), new Vector2(80, 80), true);
            child.gameObject.AddComponent<NanamiUI.Draggable>();
            var start = child.anchoredPosition; // (100, -100)

            var local0 = new Vector2(120, -140);
            var local1 = local0 + new Vector2(40, -25);
            var drag = child.GetComponent<NanamiUI.Draggable>();
            drag.OnBeginDrag(Ped(ScreenOf(container, local0)));
            drag.OnDrag(Ped(ScreenOf(container, local1)));
            yield return null;

            Assert.AreEqual(start.x + 40, child.anchoredPosition.x, 1.0f);
            Assert.AreEqual(start.y - 25, child.anchoredPosition.y, 1.0f);
            Object.Destroy(container.gameObject);
        }

        [UnityTest]
        public IEnumerator Draggable_clamps_to_dragBounds()
        {
            var container = Child(_rig.CanvasRt, "container", Vector2.zero, new Vector2(600, 600), false);
            var child = Child(container, "obj", new Vector2(100, 100), new Vector2(80, 80), true);
            var drag = child.gameObject.AddComponent<NanamiUI.Draggable>();
            // bounds parent-local y-down: x∈[50, 300], y(down)∈[50, 300] → obj (80x80) x∈[50,220], yDown∈[50,220]
            drag.dragBounds = new Rect(50, 50, 250, 250);

            var local0 = new Vector2(140, -140);
            var local1 = local0 + new Vector2(1000, -1000); // 大幅越界
            drag.OnBeginDrag(Ped(ScreenOf(container, local0)));
            drag.OnDrag(Ped(ScreenOf(container, local1)));
            yield return null;

            Assert.AreEqual(220f, child.anchoredPosition.x, 1.0f, "x 应钳到 bounds.xMax - width");
            Assert.AreEqual(-220f, child.anchoredPosition.y, 1.0f, "y 应钳到 -(bounds.yMax - height)");
            Object.Destroy(container.gameObject);
        }

        [UnityTest]
        public IEnumerator DragDrop_transfers_payload_to_drop_target()
        {
            // 用 Screen-Space-Overlay 画布（真实 demo 的配置）测 drop 命中——GraphicRaycaster 用屏幕坐标直接命中，
            // 不受 golden rig 的 WorldSpace + 禁用相机影响。
            var canvasGo = new GameObject("DropCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var raycaster = canvasGo.GetComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            // 放在靠左上、确保在测试 game view 屏幕内（避免 raycast 点落到屏幕外被拒）。
            var b = Child(root, "b", new Vector2(20, 20), new Vector2(80, 80), true);
            var c = Child(root, "c", new Vector2(200, 20), new Vector2(80, 80), true);
            Canvas.ForceUpdateCanvases(); // 让新建 Overlay 画布完成尺寸/布局，世界坐标才准
            yield return null;

            var tex = new Texture2D(4, 4);
            var payload = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

            var drag = b.gameObject.AddComponent<NanamiUI.Draggable>();
            drag.onDragStart = e =>
            {
                NanamiUI.DragDropManager.inst.StartDrag(canvas, raycaster, payload, payload, e);
                return true; // PreventDefault → agent 拖动
            };
            object dropped = null;
            c.gameObject.AddComponent<NanamiUI.DropTarget>().onDrop = data => dropped = data;

            // Overlay：屏幕点 = 世界点（null 相机）。取各自中心。
            Vector2 Screen(RectTransform rt) => RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
            PointerEventData At(Vector2 screen) => new(EventSystem.current) { position = screen, pointerPressRaycast = new RaycastResult { module = raycaster } };

            drag.OnBeginDrag(At(Screen(b)));
            drag.OnDrag(At(Screen(c)));
            drag.OnEndDrag(At(Screen(c)));
            yield return null;

            Assert.AreSame(payload, dropped, "DropTarget 应收到拖动 payload");
            Assert.IsFalse(NanamiUI.DragDropManager.inst.dragging, "松手后 agent 应停用");
            Assert.AreEqual(new Vector2(20, -20), b.anchoredPosition, "b 本体不应移动（PreventDefault）");

            Object.Destroy(canvasGo);
            Object.Destroy(tex);
        }
    }
}
