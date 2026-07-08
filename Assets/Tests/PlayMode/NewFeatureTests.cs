using System.Collections;
using NanamiUI.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NanamiUI.Tests
{
    // 本批发布前整改新增功能的行为回归（NanamiUI-only、分析上精确、无需 FairyGUI 参照）：
    // Slider onChanged/绝对跳值、Radio 组互斥、Relation 尺寸跟随传播。
    public class NewFeatureTests
    {
        private enum RadioPage { up, down, over, selectedOver }
        private class TestButton : Button<RadioPage> { }
        private class TestCombo : ComboBox<RadioPage> { }

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
            relation.sidePairs = new[] { "right-right" }; // follower 的 x 跟随 target 右边缘变化
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
            var gear = new GearColor<RadioPage>
            {
                target = go.gameObject,
                pages = new[] { RadioPage.up, RadioPage.down },
                values = new[] { Color.red, Color.green },
                defaultValue = Color.white,
            };
            gear.Apply(RadioPage.down);
            yield return null;
            Assert.AreEqual(Color.green, go.GetComponent<UnityEngine.UI.Image>().color, "GearColor 应把 down 页设为绿");
            gear.Apply(RadioPage.over); // 不在 pages → default
            Assert.AreEqual(Color.white, go.GetComponent<UnityEngine.UI.Image>().color, "不在页列表应回退 default");
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
            };
            gear.Apply(RadioPage.down);
            yield return null;

            var cg = go.GetComponent<CanvasGroup>();
            Assert.IsNotNull(cg, "GearLook 应加 CanvasGroup 传播组 alpha");
            Assert.AreEqual(0.3f, cg.alpha, 0.001f, "down 页组 alpha 应为 0.3");
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
        public IEnumerator ComboBox_selectedIndex_updates_title_and_fires_once()
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
            Assert.AreEqual(1, fired, "值变化应发一次 onChanged");

            combo.selectedIndex = 1; // 同值不再发
            Assert.AreEqual(1, fired, "赋相同值不应再发 onChanged");

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
            Assert.IsFalse(field.interactable, "editable=false 应禁用交互");
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
    }
}
