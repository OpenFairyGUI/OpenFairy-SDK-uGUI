using System.Collections;
using NanamiUI.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ZLinq;

namespace NanamiUI.Tests
{
    // 本批发布前整改新增功能的行为回归（NanamiUI-only、分析上精确、无需 FairyGUI 参照）：
    // Slider onChanged/绝对跳值、Radio 组互斥、Relation 尺寸跟随传播。
    public class NewFeatureTests
    {
        private enum RadioPage { up, down, over, selectedOver }
        private class TestButton : Button<RadioPage> { }
        private class TestCombo : ComboBox<RadioPage> { }
        private class TestOwner : NanamiUI.Component
        {
            public Controller<RadioPage> m_ctrl;

            protected override int GetControllerPage(int controller) => controller == 0 ? (int)m_ctrl.page : -1;

            protected override void SetControllerPage(int controller, int page)
            {
                if (controller == 0)
                    m_ctrl.page = (RadioPage)page;
            }
        }

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

        [UnityTest]
        public IEnumerator Slider_click_sets_value_and_fires_onChanged()
        {
            var sliderRt = Child(_rig.CanvasRt, "slider", new Vector2(100, 100), new Vector2(200, 20), false);
            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.min = 0;
            slider.max = 100;
            slider.value = 0;
            slider.bar = Child(sliderRt, "bar", Vector2.zero, new Vector2(200, 20), true); // 横向 slider
            var fired = 0;
            slider.onChanged.AddListener(() => fired++);
            yield return null;

            // 点击 bar 半程（local x=100 → percent 0.5 → value 50），changeOnClick 默认 true 直接跳值。
            var world = sliderRt.TransformPoint(new Vector3(100, -10, 0));
            var screen = RectTransformUtility.WorldToScreenPoint(_rig.Camera, world);
            var ped = new PointerEventData(EventSystem.current)
            {
                position = screen,
                pointerPressRaycast = new RaycastResult { module = _rig.Raycaster },
            };
            slider.OnPointerDown(ped);
            yield return null;

            Assert.AreEqual(50f, slider.value, 1.5f, "点击半程应跳到 value≈50");
            Assert.AreEqual(1, fired, "值变化应触发一次 onChanged");
        }

        [UnityTest]
        public IEnumerator Radio_group_is_mutually_exclusive()
        {
            var group = Child(_rig.CanvasRt, "group", Vector2.zero, new Vector2(200, 200), false);
            var b1 = MakeRadio(group, "b1");
            var b2 = MakeRadio(group, "b2");
            yield return null;

            var ped = new PointerEventData(EventSystem.current);
            b1.OnPointerClick(ped);
            Assert.IsTrue(b1.selected, "点 b1 后 b1 应选中");

            b2.OnPointerClick(ped);
            Assert.IsTrue(b2.selected, "点 b2 后 b2 应选中");
            Assert.IsFalse(b1.selected, "Radio 组互斥：选 b2 后 b1 应取消选中");
        }

        private TestButton MakeRadio(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            ((RectTransform)go.transform).SetParent(parent, false);
            var button = go.AddComponent<TestButton>();
            button.mode = ButtonMode.Radio;
            return button;
        }

        [UnityTest]
        public IEnumerator Relation_follows_target_resize()
        {
            var container = Child(_rig.CanvasRt, "container", Vector2.zero, new Vector2(600, 400), false);
            var target = Child(container, "target", new Vector2(0, 0), new Vector2(200, 100), true);
            var follower = Child(container, "follower", new Vector2(210, 0), new Vector2(50, 50), true);

            var relation = follower.gameObject.AddComponent<Relation>();
            relation.target = target;
            relation.sidePairs = new[] { RelationSide.RightRight }; // follower 的 x 跟随 target 右边缘变化
            relation.Record();
            yield return null;

            var startX = follower.anchoredPosition.x;
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 260); // target 变宽 +60
            yield return null; // LateUpdate 里 Relation.Sync 传播

            Assert.AreEqual(startX + 60, follower.anchoredPosition.x, 1.0f, "right-right：target 右移 60，follower 应同步右移 60");
            Object.Destroy(container.gameObject);
        }

        [UnityTest]
        public IEnumerator MovieClip_setframe_is_deterministic_and_autoplays()
        {
            var go = new GameObject("mc", typeof(RectTransform), typeof(CanvasRenderer));
            ((RectTransform)go.transform).SetParent(_rig.CanvasRt, false);
            var mc = go.AddComponent<MovieClip>();
            var tex = new Texture2D(4, 4);
            Sprite Frame() => Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            mc.frames = new[] { Frame(), Frame(), Frame() };
            mc.addDelays = new float[mc.frames.Length];

            mc.playing = false;
            mc.SetFrame(2);
            Assert.AreSame(mc.frames[2], mc.sprite, "SetFrame 应把 sprite 设为对应帧");

            mc.SetFrame(100); // 越界应钳到末帧，frame 字段也钳住（否则自播 Update 会越界索引 addDelays[frame]）
            Assert.AreEqual(2, mc.frame, "SetFrame 越界应把 frame 钳到末帧");

            // 自播：从第 0 帧起，跑若干真实帧应离开第 0 帧（interval 略大于单帧 dt，先 0→1 再循环，轮询捕捉过渡）。
            mc.SetFrame(0);
            mc.interval = 0.02f;
            mc.playing = true;
            var advanced = false;
            for (var i = 0; i < 30 && !advanced; i++)
            {
                yield return null;
                if (mc.frame != 0)
                    advanced = true;
            }
            Assert.IsTrue(advanced, "playing 时帧应自动前进离开第 0 帧");
            Assert.Less(mc.frame, mc.frames.Length, "帧应在范围内（循环）");

            Object.Destroy(go);
            Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator MovieClip_setplaysettings_plays_range_and_stops()
        {
            var go = new GameObject("mc", typeof(RectTransform), typeof(CanvasRenderer));
            ((RectTransform)go.transform).SetParent(_rig.CanvasRt, false);
            var mc = go.AddComponent<MovieClip>();
            var tex = new Texture2D(4, 4);
            Sprite Frame() => Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            mc.frames = new[] { Frame(), Frame(), Frame() };
            mc.addDelays = new float[mc.frames.Length];
            mc.interval = 0.005f;

            var ended = false;
            mc.onPlayEnd = () => ended = true;
            mc.SetPlaySettings(0, 2, 1, 0); // 播 0→2 一次，结束停在第 0 帧
            for (var i = 0; i < 80 && !ended; i++)
                yield return null;

            Assert.IsTrue(ended, "times=1 播完一轮应触发 onPlayEnd");
            Assert.AreEqual(0, mc.frame, "应停在 endAt=0 帧");

            Object.Destroy(go);
            Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator GearColor_applies_page_color()
        {
            var go = Child(_rig.CanvasRt, "gc", Vector2.zero, new Vector2(50, 50), true);
            var stroke = go.gameObject.AddComponent<TextStroke>();
            stroke.color = Color.black;
            var gear = new GearColor<RadioPage>
            {
                target = go.gameObject,
                pages = new[] { RadioPage.up, RadioPage.down },
                values = new[] { Color.red, Color.green },
                defaultValue = Color.white,
                strokeValues = new[] { Color.blue, Color.yellow },
                defaultStroke = Color.black,
            };
            gear.Apply(RadioPage.down);
            yield return null;
            Assert.AreEqual(Color.green, go.GetComponent<UnityEngine.UI.Image>().color, "GearColor 应把 down 页设为绿");
            Assert.AreEqual(Color.yellow, stroke.color, "GearColor 应同步应用文字描边色");
            gear.Apply(RadioPage.over); // 不在 pages → default
            Assert.AreEqual(Color.white, go.GetComponent<UnityEngine.UI.Image>().color, "不在页列表应回退 default");
            Assert.AreEqual(Color.black, stroke.color, "不在页列表时描边色也应回退 default");
        }

        [UnityTest]
        public IEnumerator GearLook_applies_alpha_via_canvasgroup_without_overwriting_child_color()
        {
            var go = Child(_rig.CanvasRt, "gl", Vector2.zero, new Vector2(50, 50), true);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(1, 1, 1, 1);
            var gear = new GearLook<RadioPage>
            {
                target = go.gameObject,
                pages = new[] { RadioPage.up, RadioPage.down },
                alphas = new[] { 1f, 0.3f },
                defaultAlpha = 1f,
                rotations = new[] { 0f, 0f },
                defaultRotation = 0f,
                grayed = new[] { false, false },
                defaultGrayed = false,
                touchables = new[] { true, false },
                defaultTouchable = true,
            };
            gear.Apply(RadioPage.down);
            yield return null;

            var cg = go.GetComponent<CanvasGroup>();
            Assert.IsNotNull(cg, "GearLook 应加 CanvasGroup 传播组 alpha");
            Assert.AreEqual(0.3f, cg.alpha, 0.001f, "down 页组 alpha 应为 0.3");
            Assert.IsFalse(cg.blocksRaycasts, "down 页 touchable=false 应关闭射线命中");
            Assert.AreEqual(1f, image.color.a, 0.001f, "子 Image 的 color.a 不应被覆写（CanvasGroup 乘算，非直改子色）");
        }

        [UnityTest]
        public IEnumerator Transition_playreverse_returns_to_start_state()
        {
            var container = Child(_rig.CanvasRt, "c", Vector2.zero, new Vector2(400, 400), false);
            var target = Child(container, "obj", new Vector2(0, 0), new Vector2(50, 50), true); // anchoredPos (0,0)
            var t = container.gameObject.AddComponent<Transition>();
            t.items = new[]
            {
                new TransitionItem
                {
                    type = TransitionItemType.XY,
                    target = target,
                    tween = true,
                    duration = 0.1f,
                    time = 0, // ease 用默认（端点断言与缓动无关；测试程序集不引用 DOTween）
                    start = new[] { 0f, 0f },   // FairyGUI xy (0,0)
                    end = new[] { 100f, 0f },   // → anchoredPos x=100
                    positionOffset = Vector2.zero,
                },
            };
            yield return null;

            // 正向播到末态（无 yield，避免 Update 再自行 Step 干扰确定性）
            t.Play();
            t.Step(0.2f);
            Assert.AreEqual(100f, target.anchoredPosition.x, 1f, "正向应到 end x=100");

            // 倒放回到起态
            t.PlayReverse();
            t.Step(0.2f);
            Assert.AreEqual(0f, target.anchoredPosition.x, 1f, "倒放应回到 start x=0");

            Object.Destroy(container.gameObject);
        }

        [UnityTest]
        public IEnumerator ComboBox_selectedIndex_updates_title_without_firing_onChanged()
        {
            var comboRt = Child(_rig.CanvasRt, "combo", Vector2.zero, new Vector2(100, 30), false);
            var combo = comboRt.gameObject.AddComponent<TestCombo>();
            combo.items = new[] { "A", "B", "C" };
            combo.values = new[] { "va", "vb", "vc" };
            var titleGo = new GameObject("title", typeof(RectTransform), typeof(CanvasRenderer));
            ((RectTransform)titleGo.transform).SetParent(comboRt, false);
            combo.titleText = titleGo.AddComponent<TextField>();
            var fired = 0;
            combo.onChanged.AddListener(() => fired++);
            yield return null;

            combo.selectedIndex = 1;
            Assert.AreEqual("B", combo.titleText.text, "selectedIndex 赋值应刷新标题");
            Assert.AreEqual("B", combo.text, "text 应为当前项显示文本");
            Assert.AreEqual("vb", combo.value, "value 应为平行 values 项");
            // 复刻 GComboBox：程序化赋值 selectedIndex 只刷新标题，不发 onChanged（onChanged 仅用户点选下拉项时发）。
            Assert.AreEqual(0, fired, "程序化赋值 selectedIndex 不应发 onChanged");

            combo.value = "vc";
            Assert.AreEqual(2, combo.selectedIndex, "程序化设置 value 应按 values 反查 selectedIndex");
            Assert.AreEqual("C", combo.text, "设置 value 后应刷新显示文本");
            combo.value = "no-such"; // 复刻 GComboBox.value：找不到回退首项
            Assert.AreEqual(0, combo.selectedIndex, "value 无匹配应回退首项");
            combo.text = "anything"; // 复刻 GComboBox.text：标题直通，不反查索引
            Assert.AreEqual("anything", combo.text, "text 赋值应直设标题");
            Assert.AreEqual(0, combo.selectedIndex, "text 赋值不应改变 selectedIndex");
            Assert.AreEqual(0, fired, "程序化设置 text/value 同样不应发 onChanged");

            Object.Destroy(comboRt.gameObject);
        }

        [UnityTest]
        public IEnumerator InputText_exposes_password_maxlength_editable()
        {
            var go = new GameObject("input", typeof(RectTransform), typeof(CanvasRenderer));
            ((RectTransform)go.transform).SetParent(_rig.CanvasRt, false);
            var textGo = new GameObject("t", typeof(RectTransform), typeof(CanvasRenderer));
            ((RectTransform)textGo.transform).SetParent(go.transform, false);
            var field = go.AddComponent<InputField>();
            field.textComponent = textGo.AddComponent<TextField>();
            var input = go.AddComponent<TextInput>();
            input.field = field;
            yield return null;

            input.password = true;
            Assert.AreEqual(InputField.ContentType.Password, field.contentType, "password 应设为密码输入");
            input.maxLength = 6;
            Assert.AreEqual(6, field.characterLimit, "maxLength 应设 characterLimit");
            input.editable = false;
            Assert.IsTrue(field.readOnly, "editable=false 应设只读（复刻 FairyGUI：只读但仍可聚焦/复制）");
            Assert.IsTrue(field.interactable, "只读不等于禁用，interactable 仍为 true");
            input.text = "hello";
            Assert.AreEqual("hello", input.text, "text 读写应经 field");

            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator ScrollPane_wheel_scrolls_and_clamps()
        {
            var root = Child(_rig.CanvasRt, "scroll", Vector2.zero, new Vector2(100, 100), false);
            var viewport = new GameObject("viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(root, false);
            viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0, 1);
            viewport.sizeDelta = new Vector2(100, 100);
            viewport.anchoredPosition = Vector2.zero;
            Child(viewport, "big", Vector2.zero, new Vector2(100, 400), true); // 内容高 400，可滚 300

            var pane = ScrollPane.Attach(root);
            Assert.IsNotNull(pane, "带 viewport(RectMask2D) 结构应可 Attach");
            var content = viewport.Find("content") as RectTransform;
            yield return null;

            var e = new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -1) };
            pane.OnScroll(e); // 向下滚一档
            Assert.Greater(content.anchoredPosition.y, 0f, "向下滚动应下移内容露出底部");

            e.scrollDelta = new Vector2(0, -100);
            pane.OnScroll(e); // 大幅滚动应被钳到最大
            Assert.AreEqual(300f, content.anchoredPosition.y, 0.5f, "滚动应钳到 contentHeight-viewHeight=300");

            Object.Destroy(root.gameObject);
        }

        [UnityTest]
        public IEnumerator ScrollPaneHost_attaches_and_real_drag_scrolls()
        {
            var root = Child(_rig.CanvasRt, "scroll", Vector2.zero, new Vector2(100, 100), false);
            var viewport = new GameObject("viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(root, false);
            viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0, 1);
            viewport.sizeDelta = new Vector2(100, 100);
            viewport.anchoredPosition = Vector2.zero;
            Child(viewport, "big", Vector2.zero, new Vector2(100, 400), true);
            root.gameObject.AddComponent<ScrollPaneHost>(); // 复刻 Migrate 烘焙：运行时自挂 ScrollPane
            yield return null; // ScrollPaneHost.Start

            var pane = root.GetComponent<ScrollPane>();
            Assert.IsNotNull(pane, "ScrollPaneHost 应在 Start 自挂 ScrollPane");
            var content = viewport.Find("content") as RectTransform;
            Assert.IsNotNull(content, "应把 viewport 子节点包进 content");

            // 真实拖动：手指上移（局部 y 从 -80 到 -20），内容下移露出底部（content.y 增大）。
            var e = new PointerEventData(EventSystem.current)
            {
                position = ScreenAt(viewport, new Vector2(50, -80)),
                pointerPressRaycast = new RaycastResult { module = _rig.Raycaster },
            };
            pane.OnBeginDrag(e);
            e.position = ScreenAt(viewport, new Vector2(50, -20)); // 指针上移 60
            pane.OnDrag(e);
            Assert.Greater(content.anchoredPosition.y, 30f, "手指上移 60 应下移内容约 60（content.y 增大）");
            Object.Destroy(root.gameObject);
        }

        [UnityTest]
        public IEnumerator List_fill_after_scrollpane_attached_refreshes_hit_and_selection()
        {
            var root = Child(_rig.CanvasRt, "list", Vector2.zero, new Vector2(100, 100), false);
            var viewport = new GameObject("viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(root, false);
            viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0, 1);
            viewport.sizeDelta = new Vector2(100, 100);

            var itemPrefab = new GameObject("item", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(TestButton));
            ((RectTransform)itemPrefab.transform).sizeDelta = new Vector2(100, 40);
            var source = root.gameObject.AddComponent<ListSource>();
            source.itemPrefab = itemPrefab;
            source.itemSize = new Vector2(100, 40);
            source.layout = ListLayoutType.SingleColumn;
            var selection = root.gameObject.AddComponent<ListSelection>();
            selection.selectionMode = ListSelectionMode.Multiple;
            var clicked = -1;
            selection.onClickItem.AddListener(i => clicked = i);
            ScrollPane.Attach(root);
            yield return null;

            NanamiUI.List.Fill(root, 3, (item, _) => item.GetComponent<TestButton>().mode = ButtonMode.Check);
            selection.Rebind(); // 第二次重绑不应留下重复 listener
            var content = viewport.Find("content") as RectTransform;
            var hit = content.GetChild(0) as RectTransform;
            Assert.IsNotNull(hit, "List.Fill 不应删除 ScrollPane 的透明命中面");
            Assert.AreEqual("__scrollHit", hit.name, "ScrollPane 的保留命中面应仍在 content 内");
            Assert.AreEqual(0, hit.GetSiblingIndex(), "命中面应保持在最底层，不遮住列表项");
            Assert.GreaterOrEqual(hit.sizeDelta.y, 120f, "命中面高度应随重填后的内容刷新");

            var button = content.GetComponentsInChildren<TestButton>(true).AsValueEnumerable().First();
            button.OnPointerClick(new PointerEventData(EventSystem.current));
            Assert.IsTrue(button.selected, "ListSelection 重绑后应由列表选择逻辑选中项");
            Assert.AreEqual(0, clicked, "onClickItem 应按当前动态项索引触发一次");
            Assert.IsFalse(button.changeStateOnClick, "列表项自身翻转应关闭，避免和 ListSelection 双翻");

            Object.Destroy(itemPrefab);
            Object.Destroy(root.gameObject);
        }

        [UnityTest]
        public IEnumerator List_fill_reuses_pooled_items_and_clears_runtime_listeners()
        {
            var root = Child(_rig.CanvasRt, "list", Vector2.zero, new Vector2(100, 100), false);
            var itemPrefab = new GameObject("item", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(TestButton));
            ((RectTransform)itemPrefab.transform).sizeDelta = new Vector2(100, 40);
            var source = root.gameObject.AddComponent<ListSource>();
            source.itemPrefab = itemPrefab;
            source.itemSize = new Vector2(100, 40);
            source.layout = ListLayoutType.SingleColumn;

            var fired = 0;
            NanamiUI.List.Fill(root, 2, (item, i) => item.GetComponent<TestButton>().onClick.AddListener(() => fired += i + 1));
            var first = root.GetComponentsInChildren<TestButton>(false).AsValueEnumerable().OrderBy(button => button.transform.GetSiblingIndex()).ToArray();
            first[0].onClick.Invoke();
            Assert.AreEqual(1, fired, "第一次填充的 listener 应触发一次");

            fired = 0;
            NanamiUI.List.Fill(root, 2, (item, i) => item.GetComponent<TestButton>().onClick.AddListener(() => fired += 10 + i));
            var second = root.GetComponentsInChildren<TestButton>(false).AsValueEnumerable().OrderBy(button => button.transform.GetSiblingIndex()).ToArray();
            CollectionAssert.AreEquivalent(first, second, "List.Fill 应复用整批 item，而不是销毁重建");
            Assert.AreEqual("__listPool", root.GetChild(root.childCount - 1).name, "池根应留在末尾，不污染可见 item 顺序");

            second[0].onClick.Invoke();
            Assert.AreEqual(10, fired, "复用 item 时旧 listener 应被清掉，只触发本轮 setup 的 listener");

            Object.Destroy(itemPrefab);
            Object.Destroy(root.gameObject);
            yield return null;
        }

        private Vector2 ScreenAt(RectTransform parent, Vector2 local) =>
            RectTransformUtility.WorldToScreenPoint(_rig.Camera, parent.TransformPoint(local));

        [UnityTest]
        public IEnumerator Button_related_controller_switches_page_and_syncs_group()
        {
            var ownerRt = Child(_rig.CanvasRt, "owner", Vector2.zero, new Vector2(300, 100), false);
            var owner = ownerRt.gameObject.AddComponent<TestOwner>();
            var buttons = new TestButton[3];
            var gears = new Gear<RadioPage>[3];
            for (var i = 0; i < 3; i++)
            {
                var b = Child(ownerRt, "b" + i, new Vector2(i * 100, 0), new Vector2(90, 40), false).gameObject.AddComponent<TestButton>();
                b.mode = ButtonMode.Radio;
                b.relatedOwner = owner;
                b.relatedController = 0;
                b.relatedPage = i;
                buttons[i] = b;
                // 同 Migrate 烘焙：每个关联按钮一个 GearButton，换页时同步组内选中态。
                gears[i] = new GearButton<RadioPage> { target = b.gameObject, pages = new[] { (RadioPage)i } };
            }
            owner.m_ctrl = new Controller<RadioPage> { gears = gears };
            yield return null;

            buttons[1].OnPointerClick(new PointerEventData(EventSystem.current));
            var page = owner.m_ctrl.page;
            Assert.AreEqual(RadioPage.down, page, "点第 1 个按钮应把控制器切到第 1 页（down）");
            Assert.IsTrue(buttons[1].selected, "被点按钮应选中");
            Assert.IsFalse(buttons[0].selected, "同组其它按钮应取消选中");
            Assert.IsFalse(buttons[2].selected, "同组其它按钮应取消选中");

            buttons[2].OnPointerClick(new PointerEventData(EventSystem.current));
            Assert.AreEqual(RadioPage.over, owner.m_ctrl.page, "点第 2 个按钮应切到第 2 页（over）");
            Assert.IsFalse(buttons[1].selected, "换选后前一个应取消选中");
            Assert.IsTrue(buttons[2].selected, "新点的应选中");

            // 复刻 GButton.HandleControllerChanged：程序化换页也同步按钮选中态（不限于点击）。
            InteractionDriver.DriveControllerPage(ownerRt.gameObject, "ctrl", "up");
            Assert.IsTrue(buttons[0].selected, "程序化切到第 0 页应选中对应按钮");
            Assert.IsFalse(buttons[2].selected, "程序化换页后其它按钮应取消选中");
            Object.Destroy(ownerRt.gameObject);
        }

        [UnityTest]
        public IEnumerator ListSelection_single_mode_selects_clicked_deselects_others()
        {
            var listRt = Child(_rig.CanvasRt, "list", Vector2.zero, new Vector2(200, 200), false);
            var buttons = new TestButton[3];
            for (var i = 0; i < 3; i++)
            {
                buttons[i] = Child(listRt, "item" + i, new Vector2(0, i * 40), new Vector2(200, 40), false).gameObject.AddComponent<TestButton>();
                buttons[i].mode = ButtonMode.Radio; // 可选中列表项 = Radio/Check（GButton.selected 对 Common 忽略，FairyGUI 同）
            }
            var sel = listRt.gameObject.AddComponent<ListSelection>();
            sel.selectionMode = ListSelectionMode.Single;
            var clicked = -1;
            sel.onClickItem.AddListener(i => clicked = i);
            yield return null; // ListSelection.Start -> Rebind 接线

            buttons[1].onClick.Invoke(); // 模拟真实点击项 1
            Assert.AreEqual(1, clicked, "onClickItem 应带被点项索引");
            Assert.IsTrue(buttons[1].selected, "单选：被点项选中");
            Assert.IsFalse(buttons[0].selected, "单选：其它项取消");

            buttons[0].onClick.Invoke();
            Assert.IsTrue(buttons[0].selected, "改点项 0 后其选中");
            Assert.IsFalse(buttons[1].selected, "项 1 取消（单选互斥）");
            Object.Destroy(listRt.gameObject);
        }

        [UnityTest]
        public IEnumerator GearLook_grayed_propagates_to_button_grayed()
        {
            var buttonGo = Child(_rig.CanvasRt, "btn", Vector2.zero, new Vector2(80, 40), false);
            var button = buttonGo.gameObject.AddComponent<TestButton>();
            var gear = new GearLook<RadioPage>
            {
                target = buttonGo.gameObject,
                pages = new[] { RadioPage.up, RadioPage.down },
                alphas = new[] { 1f, 1f },
                defaultAlpha = 1f,
                rotations = new[] { 0f, 0f },
                defaultRotation = 0f,
                grayed = new[] { false, true },
                defaultGrayed = false,
            };
            gear.Apply(RadioPage.down);
            yield return null;
            Assert.IsTrue(button.grayed, "GearLook 置灰应把按钮 grayed 置 true（进 disabled 页、拦截点击）");

            var fired = false;
            button.onClick.AddListener(() => fired = true);
            button.OnPointerClick(new PointerEventData(EventSystem.current));
            Assert.IsFalse(fired, "grayed 按钮点击应被拦截，不发 onClick");

            gear.Apply(RadioPage.up);
            Assert.IsFalse(button.grayed, "非置灰页应恢复可点");
            Object.Destroy(buttonGo.gameObject);
        }

        [UnityTest]
        public IEnumerator Depth_setsortingorder_forward_move_lands_correctly()
        {
            var container = Child(_rig.CanvasRt, "c", Vector2.zero, new Vector2(400, 400), false);
            var a = Child(container, "a", Vector2.zero, new Vector2(50, 50), true);
            var b = Child(container, "b", Vector2.zero, new Vector2(50, 50), true);
            var move = Child(container, "move", Vector2.zero, new Vector2(50, 50), true); // sibling index 2
            yield return null;

            // a、b order 0；把 move 提到 order 5：应排到 order<=5 的其它子物体之后 = 末尾（index 2 保持）。
            NanamiUI.Depth.SetSortingOrder(move, 5);
            Assert.AreEqual(2, move.GetSiblingIndex(), "forward move：move 应落在末尾（在 a、b 之后）");

            // 再把 a 提到 order 10：应排到最后 → 顺序 b, move, a。
            NanamiUI.Depth.SetSortingOrder(a, 10);
            Assert.AreEqual(2, a.GetSiblingIndex(), "a 提到最高 order 应到末尾");
            Assert.AreSame(b, container.GetChild(0), "顺序应为 b, move, a：首位是 b");
            Assert.AreSame(move.transform, container.GetChild(1), "次位是 move");
            Object.Destroy(container.gameObject);
        }

        [UnityTest]
        public IEnumerator Slider_reverse_fills_from_opposite_end()
        {
            var sliderRt = Child(_rig.CanvasRt, "slider", Vector2.zero, new Vector2(200, 20), false);
            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.min = 0;
            slider.max = 100;
            slider.reverse = true;
            slider.bar = Child(sliderRt, "bar", Vector2.zero, new Vector2(200, 20), true);
            slider.barStartX = slider.bar.anchoredPosition.x;
            yield return null;

            slider.value = 25; // reverse：percent 0.25 → bar 宽 50，右移到 (fullWidth-50)=150 处
            slider.Apply();
            Assert.AreEqual(50f, slider.bar.rect.width, 1f, "reverse slider 宽度仍按 percent");
            Assert.AreEqual(slider.barStartX + 150f, slider.bar.anchoredPosition.x, 1f, "reverse：bar 从右端起，左移露出");
            Object.Destroy(sliderRt.gameObject);
        }

        [UnityTest]
        public IEnumerator Button_check_related_controller_toggles_to_opposite_page()
        {
            var ownerRt = Child(_rig.CanvasRt, "owner", Vector2.zero, new Vector2(100, 100), false);
            var owner = ownerRt.gameObject.AddComponent<TestOwner>();
            var cb = Child(ownerRt, "cb", Vector2.zero, new Vector2(80, 40), false).gameObject.AddComponent<TestButton>();
            cb.mode = ButtonMode.Check;
            cb.relatedOwner = owner;
            cb.relatedController = 0;
            cb.relatedPage = 1; // 勾选 → 控制器第 1 页（down）
            yield return null;

            cb.OnPointerClick(new PointerEventData(EventSystem.current));
            Assert.IsTrue(cb.selected, "勾选应选中");
            Assert.AreEqual(RadioPage.down, owner.m_ctrl.page, "勾选应把控制器设到第 1 页");
            cb.OnPointerClick(new PointerEventData(EventSystem.current));
            Assert.IsFalse(cb.selected, "再点取消勾选");
            Assert.AreEqual(RadioPage.up, owner.m_ctrl.page, "取消勾选应把控制器回对页(第 0 页)——否则目标只能开不能关");
            Object.Destroy(ownerRt.gameObject);
        }

        [UnityTest]
        public IEnumerator Button_common_tab_switches_page_without_marking_selected()
        {
            var ownerRt = Child(_rig.CanvasRt, "owner", Vector2.zero, new Vector2(200, 100), false);
            var owner = ownerRt.gameObject.AddComponent<TestOwner>();
            var tab = Child(ownerRt, "tab", Vector2.zero, new Vector2(80, 40), false).gameObject.AddComponent<TestButton>();
            tab.mode = ButtonMode.Common; // Common tab：激活态由控制器 gears 驱动，不靠 selected
            tab.relatedOwner = owner;
            tab.relatedController = 0;
            tab.relatedPage = 1;
            yield return null;

            tab.OnPointerClick(new PointerEventData(EventSystem.current));
            Assert.AreEqual(RadioPage.down, owner.m_ctrl.page, "Common tab 点击应换控制器页");
            Assert.IsFalse(tab.selected, "Common tab 不应被置 selected（否则卡在按下页）");
            Object.Destroy(ownerRt.gameObject);
        }

        [UnityTest]
        public IEnumerator ListSelection_multiple_singleclick_toggles_each_item_once_per_click()
        {
            var listRt = Child(_rig.CanvasRt, "list", Vector2.zero, new Vector2(200, 200), false);
            var buttons = new TestButton[2];
            for (var i = 0; i < 2; i++)
            {
                buttons[i] = Child(listRt, "item" + i, new Vector2(0, i * 40), new Vector2(200, 40), false).gameObject.AddComponent<TestButton>();
                buttons[i].mode = ButtonMode.Check; // 勾选态项：验证不与 ListSelection 双翻
            }
            var sel = listRt.gameObject.AddComponent<ListSelection>();
            sel.selectionMode = ListSelectionMode.MultipleSingleClick;
            yield return null; // Rebind：接线 + 置 changeStateOnClick=false

            var ped = new PointerEventData(EventSystem.current);
            buttons[0].OnPointerClick(ped);
            Assert.IsTrue(buttons[0].selected, "multiple_singleclick：点一次应选中（本体自翻已禁，仅 ListSelection 翻一次）");
            buttons[1].OnPointerClick(ped);
            Assert.IsTrue(buttons[1].selected && buttons[0].selected, "multiple_singleclick：各项独立、互不取消");
            buttons[0].OnPointerClick(ped);
            Assert.IsFalse(buttons[0].selected, "multiple_singleclick：再点应取消（单次翻转，非双翻回原态）");
            Object.Destroy(listRt.gameObject);
        }

        [UnityTest]
        public IEnumerator ListSelection_multiple_plain_click_selects_exclusively()
        {
            // 复刻 GList.SetSelectionOnEvent：multiple 的无修饰键点击与 single 一致（排它选中），不是切换。
            var listRt = Child(_rig.CanvasRt, "list", Vector2.zero, new Vector2(200, 200), false);
            var buttons = new TestButton[2];
            for (var i = 0; i < 2; i++)
            {
                buttons[i] = Child(listRt, "item" + i, new Vector2(0, i * 40), new Vector2(200, 40), false).gameObject.AddComponent<TestButton>();
                buttons[i].mode = ButtonMode.Check;
            }
            var sel = listRt.gameObject.AddComponent<ListSelection>();
            sel.selectionMode = ListSelectionMode.Multiple;
            yield return null;

            var ped = new PointerEventData(EventSystem.current);
            buttons[0].OnPointerClick(ped);
            buttons[1].OnPointerClick(ped);
            Assert.IsTrue(buttons[1].selected, "multiple 普通点击：新点的选中");
            Assert.IsFalse(buttons[0].selected, "multiple 普通点击：其余取消（排它，同 FairyGUI）");
            buttons[1].OnPointerClick(ped);
            Assert.IsTrue(buttons[1].selected, "multiple 普通点击：再点已选中项保持选中，不切换");
            Object.Destroy(listRt.gameObject);
        }
    }
}
