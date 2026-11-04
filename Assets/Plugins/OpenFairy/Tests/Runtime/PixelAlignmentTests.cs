using System.Collections;
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
            Canvas.ForceUpdateCanvases();
            Assert.That(_root.InverseTransformPoint(panel.TransformPoint(new Vector3(0, 385))).y, Is.EqualTo(-1094).Within(.0002));
            Assert.That(_root.InverseTransformPoint(background.TransformPoint(new Vector3(0, 0))).y, Is.EqualTo(-1035.5f).Within(.0002));
            Assert.That(background.rect.width, Is.EqualTo(671));
            for (var i = 0; i < 8; i++) Canvas.ForceUpdateCanvases();
            Assert.That(_root.InverseTransformPoint(panel.TransformPoint(new Vector3(0, 385))).y, Is.EqualTo(-1094).Within(.0002));
            panel.GetComponent<PixelAlignment>().enabled = false;
            Assert.That(panel.anchoredPosition.y, Is.EqualTo(-1440).Within(.0002));
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
            Canvas.ForceUpdateCanvases();
            var point = _root.InverseTransformPoint(image.TransformPoint(image.rect.min + new Vector2(0, image.rect.height)));
            Assert.That(point.x, Is.EqualTo(9.9f).Within(.0001));
            Assert.That(point.y, Is.EqualTo(-18.9f).Within(.0001));
            Assert.That(image.rect.size, Is.EqualTo(new Vector2(10.4f, 8.6f)));
        }

        [TestCase(15)]
        [TestCase(.01f)]
        public void Rotation_skips_own_rounding_and_an_absolute_write_is_not_undone(float rotation)
        {
            var panel = Rect("Panel", _root, new Vector2(10, 10), new Vector2(0, 1), new Vector2(10.3f, -20.7f));
            panel.localRotation = Quaternion.Euler(0, 0, rotation);
            var alignment = panel.gameObject.AddComponent<PixelAlignment>();
            Canvas.ForceUpdateCanvases();
            Assert.That(panel.anchoredPosition, Is.EqualTo(new Vector2(10.3f, -20.7f)));
            panel.localRotation = Quaternion.identity;
            Canvas.ForceUpdateCanvases();
            Assert.That(panel.anchoredPosition, Is.EqualTo(new Vector2(10, -21)));
            panel.anchoredPosition = new Vector2(30.2f, -40.8f);
            Canvas.ForceUpdateCanvases();
            alignment.enabled = false;
            Assert.That(panel.anchoredPosition, Is.EqualTo(new Vector2(30.2f, -40.8f)));
        }

        [UnityTest]
        public IEnumerator Fractional_motion_does_not_accumulate_the_previous_render_offset()
        {
            var panel = Rect("Panel", _root, new Vector2(10, 10), new Vector2(0, 1), new Vector2(10.3f, -20.7f));
            var alignment = panel.gameObject.AddComponent<PixelAlignment>();
            for (var i = 0; i < 8; i++)
            {
                yield return null;
                panel.anchoredPosition += new Vector2(.125f, -.125f);
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
            }
            alignment.enabled = false;
            Assert.That(panel.anchoredPosition.x, Is.EqualTo(11.3f).Within(.0001));
            Assert.That(panel.anchoredPosition.y, Is.EqualTo(-21.7f).Within(.0001));
        }

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
