using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
#if FAIRYGUI_TMPRO
using TMPro;
#endif

namespace OpenFairy.UGUI.Tests
{
    public class GrayedRenderingTests
    {
        private enum Page { Color, Gray }
        private readonly List<Object> _owned = new();
        private Camera _camera;
        private RenderTexture _destination;
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position = default)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.gameObject.layer = 30;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private RectTransform Scene()
        {
            var canvasObject = Own(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
            canvasObject.layer = 30;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(128, 128);
            _camera = Own(new GameObject("UI Camera", typeof(Camera))).GetComponent<Camera>();
            _camera.enabled = false;
            _camera.transform.position = new Vector3(0, 0, -100);
            _camera.orthographic = true;
            _camera.orthographicSize = 64;
            _camera.aspect = 1;
            _camera.nearClipPlane = .1f;
            _camera.farClipPlane = 1000;
            _camera.cullingMask = 1 << 30;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.allowHDR = _camera.allowMSAA = false;
            canvas.worldCamera = _camera;
            _destination = Own(new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32));
            _destination.Create();
            _camera.targetTexture = _destination;
            return (RectTransform)canvas.transform;
        }

        private static UnityEngine.UI.Image Image(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            return image;
        }

        private Graphic Text(RectTransform root)
        {
            var rect = Rect("Text", root, new Vector2(72, 56), new Vector2(-24, 20));
#if FAIRYGUI_TMPRO
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null, "The test host must supply a native default font.");
            text.font = TMP_Settings.defaultFontAsset;
            text.alignment = TextAlignmentOptions.Center;
#else
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
#endif
            text.text = "MMMM";
            text.fontSize = 32;
            text.color = new Color(.08f, .3f, 1);
            return text;
        }

        private ColorAdjust Filter(RectTransform source)
        {
            var filter = source.gameObject.AddComponent<ColorAdjust>();
            filter.shader = Shader.Find("OpenFairy/UI ColorMatrix");
            filter.captureShader = Shader.Find("OpenFairy/UI Capture");
            Assert.That(filter.shader, Is.Not.Null);
            Assert.That(filter.captureShader, Is.Not.Null);
            filter.captureLayer = 31;
            filter.presentation = Rect("Presentation", source.parent, source.rect.size).gameObject.AddComponent<RawImage>();
            filter.presentationGroup = filter.presentation.gameObject.AddComponent<CanvasGroup>();
            filter.presentationMaterial = Own(new Material(Shader.Find("OpenFairy/UI Premultiplied")));
            return filter;
        }

        private static Grayed Gray(ColorAdjust filter, bool enabled)
        {
            var gray = filter.gameObject.AddComponent<Grayed>();
            gray.filter = filter;
            filter.grayed = gray;
            gray.enabled = enabled;
            gray.Refresh();
            return gray;
        }

        private static void Render(Camera camera, RenderTexture destination)
        {
            if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
            else
            {
                var request = new RenderPipeline.StandardRequest { destination = destination };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
        }

        private Texture2D Draw(ColorAdjust filter = null)
        {
            Canvas.ForceUpdateCanvases();
            if (filter != null) filter.Refresh(Render);
            Canvas.ForceUpdateCanvases();
            Render(_camera, _destination);
            var active = RenderTexture.active;
            var image = Own(new Texture2D(128, 128, TextureFormat.RGBA32, false, true));
            try
            {
                RenderTexture.active = _destination;
                image.ReadPixels(new UnityEngine.Rect(0, 0, 128, 128), 0, 0);
                image.Apply();
                return image;
            }
            finally { RenderTexture.active = active; }
        }

        private static void IsGray(Color color)
        {
            Assert.That(color.a, Is.GreaterThan(.03f), "A transparent pixel cannot prove graying.");
            Assert.That(color.r, Is.EqualTo(color.g).Within(.015));
            Assert.That(color.b, Is.EqualTo(color.g).Within(.015));
        }

        private static Vector2Int BlueTextPixel(Texture2D image)
        {
            var result = Vector2Int.zero;
            float maximum = 0;
            for (int y = 66; y < 112; y++)
                for (int x = 17; x < 65; x++)
                {
                    var color = image.GetPixel(x, y);
                    float blue = color.b - color.r;
                    if (color.a > .1f && blue > maximum) { maximum = blue; result = new Vector2Int(x, y); }
                }
            Assert.That(maximum, Is.GreaterThan(.1f), "Positive control requires a genuinely colored native glyph.");
            return result;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var value in _owned)
            {
                if (value is RenderTexture texture) texture.Release();
                Object.Destroy(value);
            }
            _owned.Clear();
            yield return null;
        }

        [Test]
        public void Grayscale_is_composed_after_the_authored_color_matrix()
        {
            var source = new ColorMatrix(); source.Set(.1f, .2f, .3f, -.4f);
            var gray = new ColorMatrix(); gray.Set(.1f, .2f, .3f, -.4f, true);
            for (int column = 0; column < 5; column++)
            {
                float expected = .299f * source.Values[column] + .587f * source.Values[5 + column] + .114f * source.Values[10 + column];
                for (int row = 0; row < 3; row++) Assert.That(gray.Values[row * 5 + column], Is.EqualTo(expected).Within(.000001));
                Assert.That(gray.Values[15 + column], Is.EqualTo(source.Values[15 + column]));
            }
        }

        [UnityTest]
        public IEnumerator Native_text_nested_filter_and_dynamic_graphics_gray_and_restore_without_changing_input_or_masks()
        {
            var canvas = Scene();
            var mask = Rect("Mask", canvas, new Vector2(96, 112));
            mask.gameObject.AddComponent<RectMask2D>();
            var source = Rect("Source", mask, new Vector2(128, 128));
            var alpha = source.gameObject.AddComponent<CanvasGroup>(); alpha.alpha = .6f;
            var text = Text(source);
            var originalTextMaterial = text.material;
#if FAIRYGUI_TMPRO
            var sdf = ((TMP_Text)text).fontSharedMaterial;
#endif
            var innerSource = Rect("Nested", source, new Vector2(32, 32), new Vector2(28, 0));
            var image = Image(innerSource, new Color(.1f, .4f, .9f));
            var inner = Filter(innerSource); inner.Set(.1f, 0, 0, 0);
            var filter = Filter(source); filter.enabled = false;
            var gray = Gray(filter, false);
            yield return null;
            inner.Refresh(Render);
            var original = Draw();
            var glyph = BlueTextPixel(original);
            Assert.That(original.GetPixel(92, 64).b - original.GetPixel(92, 64).r, Is.GreaterThan(.1f));
            gray.enabled = true;
            var grayed = Draw(filter);
            IsGray(grayed.GetPixel(glyph.x, glyph.y));
            IsGray(grayed.GetPixel(92, 64));
            Assert.That(grayed.GetPixel(92, 64).a, Is.EqualTo(original.GetPixel(92, 64).a).Within(.02));
            Assert.That(grayed.GetPixel(120, 64).a, Is.LessThan(.01), "The parent native mask still clips the presentation.");
            Assert.That(filter.presentationGroup.alpha, Is.EqualTo(.6f));
            Assert.That(filter.presentation.raycastTarget, Is.False);
            Assert.That(image.raycastTarget && alpha.blocksRaycasts, Is.True);
            Assert.That(text.material, Is.SameAs(originalTextMaterial));
#if FAIRYGUI_TMPRO
            Assert.That(((TMP_Text)text).fontSharedMaterial, Is.SameAs(sdf));
            ((TMP_Text)text).text = "MMMMM";
            ((TMP_Text)text).ForceMeshUpdate();
#endif
            // This graphic did not exist when Grayed was enabled.
            Image(Rect("Dynamic", source, new Vector2(24, 24), new Vector2(0, -32)), Color.red);
            var dynamic = Draw(filter);
            IsGray(dynamic.GetPixel(64, 32));
            filter.Set(.15f, 0, 0, 0);
            var combined = Draw(filter).GetPixel(92, 64);
            IsGray(combined);
            Assert.That(combined.r, Is.GreaterThan(grayed.GetPixel(92, 64).r + .03f), "Authored brightness is retained while graying is enabled.");
            gray.enabled = false;
            var colored = Draw(filter);
            Assert.That(filter.isFiltering, Is.True, "Disabling graying retains the authored color filter.");
            Assert.That(colored.GetPixel(92, 64).b - colored.GetPixel(92, 64).r, Is.GreaterThan(.1f));
            gray.enabled = true;
            filter.enabled = false;
            IsGray(Draw(filter).GetPixel(92, 64));
            Assert.That(filter.isFiltering && filter.isPresenting, Is.True, "Graying survives disabling the authored color filter.");
            source.gameObject.SetActive(false);
            Assert.That(filter.texture, Is.Null);
            source.gameObject.SetActive(true);
            IsGray(Draw(filter).GetPixel(92, 64));
            for (int i = 0; i < 3; i++)
            {
                gray.enabled = false;
                Assert.That(filter.texture, Is.Null);
                Assert.That(filter.isPresenting, Is.False);
                var restored = Draw().GetPixel(92, 64);
                Assert.That(restored.b - restored.r, Is.GreaterThan(.1f));
                gray.enabled = true;
                IsGray(Draw(filter).GetPixel(92, 64));
            }
            filter.Dispose();
            Assert.That(gray.enabled || filter.isFiltering, Is.False);
            Assert.That(filter.texture, Is.Null);
        }

        [UnityTest]
        public IEnumerator GearLook_grays_virtual_rows_created_and_rebound_after_enable_and_restores_current_content()
        {
            var canvas = Scene();
            var source = Rect("Source", canvas, new Vector2(128, 128));
            var group = source.gameObject.AddComponent<CanvasGroup>();
            var filter = Filter(source); filter.enabled = false;
            var gray = Gray(filter, false);
            var gear = new GearLook<Page>
            {
                target = source.gameObject, pages = new[] { Page.Gray },
                alphas = new[] { 1f }, rotations = new[] { 0f }, grayed = new[] { true },
                touchables = new[] { true }, defaultAlpha = 1,
            };
            gear.Apply(Page.Gray);
            Assert.That(gray.enabled && group.blocksRaycasts, Is.True);
            yield return null;
            Draw(filter);
            var listRect = Rect("List", source, new Vector2(64, 24), new Vector2(0, -32));
            Rect("viewport", listRect, listRect.rect.size).gameObject.AddComponent<RectMask2D>();
            var template = Own(new GameObject("Template", typeof(RectTransform), typeof(UnityEngine.UI.Image)));
            ((RectTransform)template.transform).sizeDelta = new Vector2(64, 24);
            template.SetActive(false);
            var list = listRect.gameObject.AddComponent<ListSource>();
            list.itemPrefab = template;
            list.itemSize = new Vector2(64, 24);
            list.layout = ListLayoutType.SingleColumn;
            list.SetVirtual();
            list.Fill(30, (go, index) => go.GetComponent<UnityEngine.UI.Image>().color = index % 2 == 0 ? Color.red : Color.blue);
            var first = Draw(filter).GetPixel(64, 32);
            IsGray(first);
            list.ScrollToIndex(13);
            var rebound = Draw(filter).GetPixel(64, 32);
            IsGray(rebound);
            Assert.That(Mathf.Abs(first.r - rebound.r), Is.GreaterThan(.05f), "Rebound row must update captured content, not retain the first snapshot.");
            gear.Apply(Page.Color);
            Assert.That(gray.enabled, Is.False);
            Assert.That(group.blocksRaycasts, Is.True);
            var restored = Draw().GetPixel(64, 32);
            Assert.That(restored.b - restored.r, Is.GreaterThan(.2f), "The current blue row returns after disabling graying.");
            Assert.That(filter.texture, Is.Null);
        }
    }
}
