using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public class LoaderContentTests
    {
        private readonly List<Object> _owned = new();
        private GameObject _canvas;
        private RectTransform _host;
        private Loader _loader;
        private LoaderContentTestPanel _panel;
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }

        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = size;
            return rect;
        }

        [SetUp]
        public void Setup()
        {
            _canvas = Own(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _host = Rect("Host", _canvas.transform, new Vector2(300, 200));
            var rect = Rect("Loader", _host, new Vector2(100, 60));
            rect.pivot = new Vector2(.3f, .7f);
            rect.gameObject.AddComponent<CanvasRenderer>();
            _loader = rect.gameObject.AddComponent<Loader>();
            _panel = Rect("Content", rect, new Vector2(160, 80)).gameObject.AddComponent<LoaderContentTestPanel>();
            var icon = Rect("Icon", _panel.transform, new Vector2(40, 20));
            icon.anchoredPosition = new Vector2(10, -10);
            icon.gameObject.AddComponent<CanvasRenderer>();
            _panel.icon = icon.gameObject.AddComponent<UnityEngine.UI.Image>();
            _loader.componentContent = _panel;
            _loader.componentSize = new Vector2(160, 80);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var obj in _owned)
            {
                if (obj is RenderTexture texture) texture.Release();
                Object.Destroy(obj);
            }
            _owned.Clear();
            yield return null;
        }

        [TestCase(Loader.FillType.None, 1, 1, -30, -10)]
        [TestCase(Loader.FillType.Scale, .625f, .625f, 0, 5)]
        [TestCase(Loader.FillType.ScaleMatchWidth, .625f, .625f, 0, 5)]
        [TestCase(Loader.FillType.ScaleMatchHeight, .75f, .75f, -10, 0)]
        [TestCase(Loader.FillType.ScaleFree, .625f, .75f, 0, 0)]
        [TestCase(Loader.FillType.ScaleNoBorder, .75f, .75f, -10, 0)]
        public void Fill_scales_the_whole_component_without_reflowing_authored_children(
            Loader.FillType fill, float sx, float sy, float left, float top)
        {
            _loader.fill = fill;
            _loader.align = Loader.AlignType.Center;
            _loader.vAlign = Loader.VertAlignType.Middle;
            _loader.ShowContent();
            var rect = (RectTransform)_panel.transform;
            Assert.That(rect.rect.size, Is.EqualTo(new Vector2(160, 80)));
            Assert.That(_panel.icon.rectTransform.rect.size, Is.EqualTo(new Vector2(40, 20)));
            Assert.That(rect.localScale.x, Is.EqualTo(sx).Within(.0001f));
            Assert.That(rect.localScale.y, Is.EqualTo(sy).Within(.0001f));
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var actual = _loader.rectTransform.InverseTransformPoint(corners[1]);
            // The parent has pivot (.3,.7), so its local top-left is (-30,18).
            Assert.That(actual.x, Is.EqualTo(-30 + left).Within(.001f));
            Assert.That(actual.y, Is.EqualTo(18 - top).Within(.001f));
        }

        [UnityTest]
        public IEnumerator Native_parent_resize_updates_scale_without_a_refresh_loop()
        {
            var rect = _loader.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = new Vector2(-200, -140);
            _loader.fill = Loader.FillType.ScaleFree;
            _loader.ShowContent();
            _host.sizeDelta = new Vector2(400, 180);
            yield return new WaitForEndOfFrame();
            Assert.That(rect.rect.size, Is.EqualTo(new Vector2(200, 40)));
            Assert.That(_panel.transform.localScale.x, Is.EqualTo(1.25f).Within(.0001f));
            Assert.That(_panel.transform.localScale.y, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(((RectTransform)_panel.transform).rect.size, Is.EqualTo(new Vector2(160, 80)));
            Assert.That(_loader.content, Is.SameAs(_panel));
        }

        [Test]
        public void Auto_size_uses_the_source_rectangle_and_empty_size_without_a_white_quad()
        {
            _loader.autoSize = true;
            _loader.ShowContent();
            Assert.That(_loader.rectTransform.rect.size, Is.EqualTo(new Vector2(160, 80)));
            Assert.That(_panel.transform.localScale, Is.EqualTo(Vector3.one));
            MeshVertices(0);
            _loader.sprite = null;
            Assert.That(_loader.rectTransform.rect.size, Is.EqualTo(new Vector2(50, 30)));
            Assert.That(_loader.content, Is.Null);
            MeshVertices(0);
            _loader.ShowContent();
            Assert.That(_loader.content, Is.SameAs(_panel));
            Assert.That(_loader.rectTransform.rect.size, Is.EqualTo(new Vector2(160, 80)));
        }

        [UnityTest]
        public IEnumerator Component_image_movie_empty_and_restore_keep_the_same_baked_content()
        {
            var image = Sprite(Color.red);
            var second = Sprite(Color.blue);
            _loader.ShowContent();
            MeshVertices(0);
            _loader.sprite = image;
            Assert.That(_loader.content, Is.Null);
            Assert.That(_panel.gameObject.activeSelf, Is.False);
            MeshVertices(4);
            _loader.frames = new[] { image, second };
            _loader.addDelays = new float[2];
            _loader.interval = .01f;
            // Exercise the base MovieClip API, not a Loader-only frame assignment.
            yield return ((MovieClip)_loader).Play(0, 1, 1).ToCoroutine();
            Assert.That(_loader.sprite, Is.SameAs(second));
            Assert.That(_loader.content, Is.Null);
            MeshVertices(4);
            _loader.sprite = null;
            Assert.That(_loader.playing, Is.False);
            MeshVertices(0);
            _loader.ShowContent();
            yield return null;
            yield return null;
            Assert.That(_loader.content, Is.SameAs(_panel));
            Assert.That(_panel.gameObject.activeSelf, Is.True);
            Assert.That(_loader.sprite, Is.Null);
            Assert.That(_loader.transform.childCount, Is.EqualTo(1));
            MeshVertices(0);
        }

        [UnityTest]
        public IEnumerator Assigning_an_image_stops_the_previous_movie_before_restoring_content()
        {
            var first = Sprite(Color.red);
            var second = Sprite(Color.blue);
            _loader.frames = new[] { first, second };
            _loader.addDelays = new float[2];
            _loader.interval = .01f;
            ((MovieClip)_loader).Play(0, 1, 0).Forget();
            _loader.sprite = second;
            yield return null;
            yield return null;
            Assert.That(_loader.playing, Is.False);
            Assert.That(_loader.sprite, Is.SameAs(second));
            _loader.ShowContent();
            _loader.gameObject.SetActive(false);
            _loader.gameObject.SetActive(true);
            yield return null;
            Assert.That(_loader.content, Is.SameAs(_panel));
            Assert.That(_loader.sprite, Is.Null);
        }

        private Sprite Sprite(Color color)
        {
            var texture = Own(new Texture2D(8, 4, TextureFormat.RGBA32, false));
            var pixels = new Color[32];
            System.Array.Fill(pixels, color);
            texture.SetPixels(pixels);
            texture.Apply();
            return Own(UnityEngine.Sprite.Create(texture, new UnityEngine.Rect(0, 0, 8, 4), Vector2.zero));
        }

        private void MeshVertices(int count)
        {
            Canvas.ForceUpdateCanvases();
            _loader.Rebuild(CanvasUpdate.PreRender);
            var mesh = _loader.canvasRenderer.GetMesh();
            Assert.That(mesh == null ? 0 : mesh.vertexCount, Is.EqualTo(count));
        }

        [UnityTest]
        public IEnumerator Component_matches_native_image_mask_and_alpha_without_inheriting_loader_color()
        {
            var destination = Own(new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear));
            destination.antiAliasing = 1;
            destination.Create();
            var camera = Own(new GameObject("Camera")).AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 64;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.targetTexture = destination;
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            _canvas.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _canvas.transform.localScale = Vector3.one;
            var canvasRect = (RectTransform)_canvas.transform;
            canvasRect.pivot = Vector2.one * .5f;
            canvasRect.sizeDelta = new Vector2(128, 128);
            _host.anchorMin = _host.anchorMax = _host.pivot = Vector2.one * .5f;
            _host.anchoredPosition = Vector2.zero;
            _host.sizeDelta = new Vector2(32, 64);
            _host.gameObject.AddComponent<RectMask2D>();
            _host.gameObject.AddComponent<CanvasGroup>().alpha = .5f;
            var rect = _loader.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(64, 64);
            var icon = _panel.icon;
            icon.rectTransform.anchorMin = Vector2.zero;
            icon.rectTransform.anchorMax = Vector2.one;
            icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
            icon.color = Color.red;
            _loader.color = Color.black;
            _loader.fill = Loader.FillType.ScaleFree;
            _loader.ShowContent();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Render(camera, destination);
            var componentPixels = Read(destination);
            Assert.That(icon.color, Is.EqualTo(Color.red));
            _loader.gameObject.SetActive(false);
            var reference = Rect("Reference", _host, new Vector2(64, 64));
            reference.anchorMin = reference.anchorMax = reference.pivot = Vector2.one * .5f;
            reference.anchoredPosition = Vector2.zero;
            reference.gameObject.AddComponent<CanvasRenderer>();
            reference.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.red;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Render(camera, destination);
            var nativePixels = Read(destination);
            Assert.That(nativePixels[64 * 128 + 64].r, Is.GreaterThan(50), "Positive control: the native reference must render.");
            Assert.That(nativePixels[64 * 128 + 88].a, Is.Zero, "Reference outside the mask must be empty.");
            for (var i = 0; i < componentPixels.Length; ++i)
            {
                Assert.That(Mathf.Abs(componentPixels[i].r - nativePixels[i].r), Is.LessThanOrEqualTo(1), $"red pixel {i}");
                Assert.That(Mathf.Abs(componentPixels[i].a - nativePixels[i].a), Is.LessThanOrEqualTo(1), $"alpha pixel {i}");
            }
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

        private Color32[] Read(RenderTexture source)
        {
            var texture = Own(new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true));
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                texture.ReadPixels(new UnityEngine.Rect(0, 0, source.width, source.height), 0, 0);
                texture.Apply();
                return texture.GetPixels32();
            }
            finally { RenderTexture.active = previous; }
        }
    }
}
