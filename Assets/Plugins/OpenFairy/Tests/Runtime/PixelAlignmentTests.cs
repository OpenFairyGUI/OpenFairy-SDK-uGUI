using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public class PixelAlignmentTests
    {
        private GameObject _canvas;
        private RectTransform _root;

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pivot, Vector2 position)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        // Where the rendered top-left corner of a graphic's mesh lands in another space.
        private static Vector2 Rendered(Graphic graphic, Transform space)
        {
            var bounds = graphic.canvasRenderer.GetMesh().bounds;
            return space.InverseTransformPoint(graphic.transform.TransformPoint(new Vector3(bounds.min.x, bounds.max.y)));
        }

        private static void ExpectRendered(Graphic graphic, Transform space, float x, float y)
        {
            var point = Rendered(graphic, space);
            Assert.That(point.x, Is.EqualTo(x).Within(.0002), graphic.name + " x");
            Assert.That(point.y, Is.EqualTo(y).Within(.0002), graphic.name + " y");
        }

        [SetUp]
        public void Setup()
        {
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _root = (RectTransform)_canvas.transform;
            _root.pivot = new Vector2(0, 1);
            _root.sizeDelta = new Vector2(2560, 1440);
        }

        [TearDown]
        public void Cleanup() => Object.DestroyImmediate(_canvas);

        [Test]
        public void Scaled_bottom_anchor_moves_the_whole_panel_by_half_a_parent_pixel()
        {
            var panel = Rect("Panel", _root, new Vector2(670, 385), Vector2.zero, new Vector2(0, -1440));
            panel.localScale = new Vector3(.9f, .9f, 1);
            panel.gameObject.AddComponent<PixelAlignment>();
            var background = Rect("Background", panel, new Vector2(671, 450), new Vector2(0, 1), new Vector2(0, 65));
            var graphic = background.gameObject.AddComponent<Image>();
            graphic.raycastTarget = false;
            for (var i = 0; i < 8; i++) Canvas.ForceUpdateCanvases();
            ExpectRendered(graphic, _root, 0, -1035.5f);
            Assert.That(panel.anchoredPosition.y, Is.EqualTo(-1440));
            Assert.That(background.rect.width, Is.EqualTo(671));
        }

        [Test]
        public void Nested_alignment_rounds_in_each_author_parent_instead_of_screen_space()
        {
            _root.localScale = Vector3.one * 1.25f;
            var panel = Rect("Panel", _root, new Vector2(100, 100), new Vector2(0, 1), new Vector2(.5f, -.5f));
            panel.localScale = Vector3.one * .9f;
            panel.gameObject.AddComponent<PixelAlignment>();
            var group = Rect("Group", panel, new Vector2(70, 70), new Vector2(0, 1), new Vector2(10.25f, -20.75f));
            var image = Rect("Image", group, new Vector2(10.4f, 8.6f), new Vector2(0, 1), new Vector2(.5f, -.5f));
            image.gameObject.AddComponent<PixelAlignment>().coordinateSpace = panel;
            var graphic = image.gameObject.AddComponent<Image>();
            Canvas.ForceUpdateCanvases();
            ExpectRendered(graphic, _root, 9.9f, -18.9f);
            Assert.That(image.rect.size, Is.EqualTo(new Vector2(10.4f, 8.6f)));
        }

        [TestCase(15)]
        [TestCase(.01f)]
        public void Rotation_skips_own_rounding_and_the_layout_position_is_never_written(float rotation)
        {
            var panel = Rect("Panel", _root, new Vector2(10, 10), new Vector2(0, 1), new Vector2(10.3f, -20.7f));
            panel.localRotation = Quaternion.Euler(0, 0, rotation);
            panel.gameObject.AddComponent<PixelAlignment>();
            var graphic = panel.gameObject.AddComponent<Image>();
            Canvas.ForceUpdateCanvases();
            ExpectRendered(graphic, panel, 0, 0);
            panel.localRotation = Quaternion.identity;
            Canvas.ForceUpdateCanvases();
            ExpectRendered(graphic, _root, 10, -21);
            panel.anchoredPosition = new Vector2(30.2f, -40.8f);
            Canvas.ForceUpdateCanvases();
            ExpectRendered(graphic, _root, 30, -41);
            Assert.That(panel.anchoredPosition, Is.EqualTo(new Vector2(30.2f, -40.8f)));
        }

        [UnityTest]
        public IEnumerator Fractional_motion_rounds_every_frame_from_the_layout_position()
        {
            var panel = Rect("Panel", _root, new Vector2(10, 10), new Vector2(0, 1), new Vector2(10.3f, -20.7f));
            panel.gameObject.AddComponent<PixelAlignment>();
            var graphic = panel.gameObject.AddComponent<Image>();
            for (var i = 0; i < 8; i++)
            {
                yield return null;
                panel.anchoredPosition += new Vector2(.125f, -.125f);
                Canvas.ForceUpdateCanvases();
                ExpectRendered(graphic, _root, Mathf.Round(panel.anchoredPosition.x), Mathf.Round(panel.anchoredPosition.y));
                yield return new WaitForEndOfFrame();
            }
            Assert.That(panel.anchoredPosition.x, Is.EqualTo(11.3f).Within(.0001));
            Assert.That(panel.anchoredPosition.y, Is.EqualTo(-21.7f).Within(.0001));
        }

        [UnityTest]
        public IEnumerator Static_elements_share_one_registration_and_are_not_rebuilt()
        {
            var graphics = new Image[17];
            for (var i = 0; i < graphics.Length; i++)
            {
                // The first element sits on whole pixels.
                var rect = Rect("Element", _root, new Vector2(10, 10), new Vector2(0, 1),
                    i == 0 ? new Vector2(10, -20) : new Vector2(10.3f + i, -20.7f));
                rect.gameObject.AddComponent<PixelAlignment>();
                graphics[i] = rect.gameObject.AddComponent<Image>();
            }
            var rebuilt = 0;
            foreach (var graphic in graphics) graphic.RegisterDirtyVerticesCallback(() => rebuilt++);
            var queue = (IList<ICanvasElement>)typeof(CanvasUpdateRegistry)
                .GetField("m_LayoutRebuildQueue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(CanvasUpdateRegistry.instance);
            var registered = -1;
            void Probe()
            {
                registered = 0;
                for (var i = 0; i < queue.Count; i++)
                    if (queue[i].GetType().Assembly == typeof(PixelAlignment).Assembly) registered++;
            }
            Canvas.preWillRenderCanvases += Probe;
            try
            {
                Canvas.ForceUpdateCanvases();
                yield return null;
                rebuilt = 0;
                for (var i = 0; i < 4; i++)
                {
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    Assert.That(registered, Is.EqualTo(1));
                    Assert.That(rebuilt, Is.Zero);
                    for (var j = 1; j < graphics.Length; j++) ExpectRendered(graphics[j], _root, 10 + j, -21);
                }
            }
            finally { Canvas.preWillRenderCanvases -= Probe; }
            Assert.That(graphics[3].rectTransform.anchoredPosition.x, Is.EqualTo(13.3f));
        }

        [UnityTest]
        public IEnumerator Moving_resizing_or_scaling_an_ancestor_realigns_the_child_in_the_same_update()
        {
            var panel = Rect("Panel", _root, new Vector2(200, 200), new Vector2(0, 1), Vector2.zero);
            var group = Rect("Group", panel, new Vector2(100, 50), new Vector2(.5f, .5f), new Vector2(60, -40));
            var image = Rect("Image", group, new Vector2(10, 10), new Vector2(0, 1), Vector2.zero);
            image.anchorMin = image.anchorMax = Vector2.one;
            image.gameObject.AddComponent<PixelAlignment>().coordinateSpace = panel;
            var graphic = image.gameObject.AddComponent<Image>();
            void Expect(float x, float y)
            {
                Canvas.ForceUpdateCanvases();
                ExpectRendered(graphic, panel, x, y);
            }
            Expect(110, -15);
            yield return null;
            group.anchoredPosition = new Vector2(60.75f, -40.25f);
            Expect(111, -15);
            yield return null;
            group.sizeDelta = new Vector2(101.5f, 50);
            Expect(112, -15);
            yield return null;
            group.localScale = new Vector3(.5f, 1, 1);
            Expect(86, -15);
            Assert.That(image.anchoredPosition, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Rotating_or_scaling_a_child_keeps_the_rounding_of_its_ancestors()
        {
            var panel = Rect("Panel", _root, new Vector2(100, 100), new Vector2(0, 1), new Vector2(10.3f, -20.7f));
            panel.gameObject.AddComponent<PixelAlignment>();
            var icon = Rect("Icon", panel, new Vector2(10, 10), new Vector2(.5f, .5f), new Vector2(20, -20));
            icon.gameObject.AddComponent<PixelAlignment>();
            var graphic = icon.gameObject.AddComponent<Image>();
            foreach (var (angle, scale) in new[] { (0f, 1f), (30f, 1f), (90f, 1f), (0f, 2f) })
            {
                icon.localRotation = Quaternion.Euler(0, 0, angle);
                icon.localScale = new Vector3(scale, scale, 1);
                Canvas.ForceUpdateCanvases();
                var center = _root.InverseTransformPoint(icon.TransformPoint(graphic.canvasRenderer.GetMesh().bounds.center));
                Assert.That(center.x, Is.EqualTo(30).Within(.0002), $"{angle} {scale}");
                Assert.That(center.y, Is.EqualTo(-41).Within(.0002), $"{angle} {scale}");
            }
        }

        [Test]
        public void Children_native_graphics_and_rect_masks_follow_the_rounded_ancestors()
        {
            var panel = Rect("Panel", _root, new Vector2(100, 100), new Vector2(0, 1), new Vector2(10.3f, -20.7f));
            var alignment = panel.gameObject.AddComponent<PixelAlignment>();
            var mask = panel.gameObject.AddComponent<RectMask2D>();
            var child = Rect("Child", panel, new Vector2(10, 10), new Vector2(0, 1), new Vector2(5, -5)).gameObject.AddComponent<Image>();
            var native = Rect("Native", panel, new Vector2(10, 10), new Vector2(0, 1), new Vector2(20.4f, -5));
            native.gameObject.AddComponent<PixelAlignment>();
            var raw = native.gameObject.AddComponent<RawImage>();
            Canvas.ForceUpdateCanvases();
            ExpectRendered(child, _root, 15, -26);
            ExpectRendered(raw, _root, 30, -26);
            Assert.That(mask.canvasRect.xMin + mask.padding.x, Is.EqualTo(10).Within(.0002));
            Assert.That(mask.canvasRect.yMax - mask.padding.w, Is.EqualTo(-21).Within(.0002));
            panel.anchoredPosition = new Vector2(10.6f, -20.7f);
            Canvas.ForceUpdateCanvases();
            ExpectRendered(child, _root, 16, -26);
            Assert.That(mask.canvasRect.xMin + mask.padding.x, Is.EqualTo(11).Within(.0002));
            alignment.enabled = false;
            Canvas.ForceUpdateCanvases();
            ExpectRendered(child, _root, 15.6f, -25.7f);
            ExpectRendered(raw, _root, 30.6f, -25.7f);
        }

#if FAIRYGUI_TMPRO
        [Test]
        public void Tmp_vertices_move_with_the_rounding_without_changing_the_layout()
        {
            var panel = Rect("Panel", _root, new Vector2(100, 40), new Vector2(0, 1), new Vector2(10.3f, -20.7f));
            // Migrate adds the alignment after the text component.
            var text = panel.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            var alignment = panel.gameObject.AddComponent<PixelAlignment>();
            text.font = TMPro.TMP_Settings.defaultFontAsset;
            text.text = "I";
            Vector3 Shift()
            {
                Canvas.ForceUpdateCanvases();
                return text.mesh.vertices[0] - text.textInfo.characterInfo[0].vertex_BL.position;
            }
            Assert.That(Vector3.Distance(Shift(), new Vector3(-.3f, -.3f)), Is.LessThan(.0002f));
            alignment.enabled = false;
            Assert.That(Shift().magnitude, Is.LessThan(.0002f));
        }
#endif

        [TestCase(80, 46, 80, 54.2f, Loader.FillType.ScaleMatchHeight, -8, 0)]
        [TestCase(160, 80, 101, 63, Loader.FillType.None, -30, -9)]
        [TestCase(160, 80, 101, 63, Loader.FillType.Scale, 0, 6)]
        public void Editor_loader_centers_with_floor_while_the_sdk_default_keeps_fractions(
            float sw, float sh, float width, float height, Loader.FillType fill, float left, float top)
        {
            var rect = Rect("Loader", _root, new Vector2(width, height), new Vector2(0, 1), Vector2.zero);
            var loader = rect.gameObject.AddComponent<Loader>();
            var texture = new Texture2D((int)sw, (int)sh);
            var sprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, sw, sh), Vector2.zero);
            try
            {
                loader.sprite = sprite;
                loader.fill = fill;
                loader.align = Loader.AlignType.Center;
                loader.vAlign = Loader.VertAlignType.Middle;
                loader.editorAlignment = true;
                loader.SetVerticesDirty();
                loader.Rebuild(CanvasUpdate.PreRender);
                var bounds = loader.canvasRenderer.GetMesh().bounds;
                Assert.That(bounds.min.x, Is.EqualTo(left).Within(.0001));
                Assert.That(-bounds.max.y, Is.EqualTo(top).Within(.0001));
                loader.editorAlignment = false;
                loader.SetVerticesDirty();
                loader.Rebuild(CanvasUpdate.PreRender);
                bounds = loader.canvasRenderer.GetMesh().bounds;
                Assert.That(bounds.center.x, Is.EqualTo(width / 2).Within(.0001));
                Assert.That(bounds.center.y, Is.EqualTo(-height / 2).Within(.0001));
            }
            finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }
    }
}
