using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public class BlurFilterTests
    {
        private readonly List<Object> _owned = new();
        private Camera _camera;
        private RectTransform _canvas;
        private RenderTexture _destination;
        private EventSystem _events;
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }

        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return rect;
        }

        private static UnityEngine.UI.Image Image(Transform parent, Vector2 size, Color color)
        {
            var image = Rect("Image", parent, size).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            return image;
        }

        private RectTransform Scene()
        {
            var go = Own(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)));
            go.layer = 30;
            _canvas = (RectTransform)go.transform;
            _canvas.sizeDelta = new Vector2(128, 128);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            _camera = Own(new GameObject("UI Camera", typeof(Camera))).GetComponent<Camera>();
            _camera.enabled = false;
            _camera.transform.position = new Vector3(0, 0, -100);
            _camera.orthographic = true;
            _camera.orthographicSize = 64;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 1000;
            _camera.cullingMask = 1 << 30;
            _camera.aspect = 1;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            canvas.worldCamera = _camera;
            _destination = Own(new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear));
            _destination.Create();
            _camera.targetTexture = _destination;
            return Rect("Host", _canvas, new Vector2(128, 128));
        }

        private BlurFilter Filter(RectTransform source, float size = 2)
        {
            var image = Rect("Blur Presentation", source.parent, source.rect.size).gameObject.AddComponent<RawImage>();
            image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            image.raycastTarget = false;
            var filter = source.gameObject.AddComponent<BlurFilter>();
            filter.presentation = image;
            filter.presentationGroup = image.gameObject.AddComponent<CanvasGroup>();
            var shader = Shader.Find("OpenFairy/UI Premultiplied");
            Assert.That(shader, Is.Not.Null);
            filter.presentationMaterial = Own(new Material(shader));
            filter.captureShader = Shader.Find("OpenFairy/UI Capture");
            filter.blurShader = Shader.Find("OpenFairy/Texture Blur");
            filter.captureLayer = 31;
            filter.blurSize = size;
            image.gameObject.SetActive(false);
            return filter;
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

        private Texture2D Draw()
        {
            Canvas.ForceUpdateCanvases();
            Render(_camera, _destination);
            var result = Own(new Texture2D(128, 128, TextureFormat.RGBA32, false, true));
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _destination;
                result.ReadPixels(new UnityEngine.Rect(0, 0, 128, 128), 0, 0);
                result.Apply();
                return result;
            }
            finally { RenderTexture.active = previous; }
        }

        private static Color At(Texture2D texture, int x, int y = 0) => texture.GetPixel(x + 64, y + 64);

        private void AssertRaycast(Graphic source)
        {
            if (_events == null) _events = Own(new GameObject("Events", typeof(EventSystem))).GetComponent<EventSystem>();
            var pointer = new PointerEventData(_events) { position = _camera.WorldToScreenPoint(source.transform.position) };
            var results = new List<RaycastResult>();
            _canvas.GetComponent<GraphicRaycaster>().Raycast(pointer, results);
            Assert.That(results.Exists(result => result.gameObject == source.gameObject), Is.True);
            ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var obj in _owned)
            {
                if (obj is RenderTexture rt) rt.Release();
                Object.Destroy(obj);
            }
            _owned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FinalCompositionAppliesRootAndParentAlphaOnceWithoutChangingRaycast()
        {
            var host = Scene();
            host.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
            var source = Rect("Source", host, new Vector2(96, 96));
            source.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
            var image = Image(source, new Vector2(80, 80), Color.red);
            int clicks = 0;
            image.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() => clicks++);
            var originalMaterial = image.material;
            var filter = Filter(source);
            yield return null;
            AssertRaycast(image);
            filter.Refresh(Render);
            Color color = At(Draw(), 0);
            Assert.That(color.a, Is.EqualTo(0.25f).Within(0.025f));
            Assert.That(color.r, Is.EqualTo(color.a).Within(0.02f));
            Assert.That(color.g + color.b, Is.LessThan(0.01f));
            Assert.That(image.material, Is.SameAs(originalMaterial));
            Assert.That(image.raycastTarget && image.enabled, Is.True);
            Assert.That(image.canvasRenderer.cull, Is.False);
            Assert.That(filter.presentation.raycastTarget, Is.False);
            AssertRaycast(image);
            filter.Dispose();
            filter.Dispose();
            Assert.That(filter.isPresenting, Is.False);
            Assert.That(filter.texture, Is.Null);
            Assert.That(filter.presentation.gameObject.activeSelf, Is.False);
            AssertRaycast(image);
            Assert.That(clicks, Is.EqualTo(3));
            filter.enabled = true;
            filter.Refresh(Render);
            Assert.That(At(Draw(), 0).a, Is.EqualTo(0.25f).Within(0.025f));
        }

        [UnityTest]
        public IEnumerator InternalMaskIsBlurredAndOuterSoftMaskClipsOnlyTheFinalImage()
        {
            var host = Scene();
            host.sizeDelta = new Vector2(60, 60);
            host.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(4, 4);
            var source = Rect("Source Mask", host, new Vector2(48, 48));
            var maskGraphic = source.gameObject.AddComponent<UnityEngine.UI.Image>();
            source.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Image(source, new Vector2(120, 120), Color.red);
            var filter = Filter(source);
            yield return null;
            filter.Refresh(Render);
            var pixels = Draw();
            Assert.That(At(pixels, 0).a, Is.GreaterThan(0.97f));
            Assert.That(At(pixels, 26).a, Is.GreaterThan(0.02f));
            Assert.That(At(pixels, 32).a, Is.LessThan(0.01f));
            Assert.That(At(pixels, 29).a, Is.GreaterThan(0).And.LessThan(At(pixels, 26).a));
            Assert.That(maskGraphic.GetComponent<Mask>().showMaskGraphic, Is.False);
        }

        [UnityTest]
        public IEnumerator RefreshSeesContentAndSizeChangesAndRenderFailureRestoresTheSource()
        {
            var host = Scene();
            var source = Rect("Source", host, new Vector2(64, 64));
            var image = Image(source, new Vector2(60, 60), Color.red);
            var filter = Filter(source);
            yield return null;
            filter.Refresh(Render);
            Assert.That(At(Draw(), 0).r, Is.GreaterThan(0.97f));
            var firstTexture = filter.texture;
            image.color = Color.green;
            source.sizeDelta = new Vector2(80, 64);
            filter.Refresh(Render);
            Assert.That(filter.texture, Is.Not.SameAs(firstTexture));
            Assert.That(At(Draw(), 0).g, Is.GreaterThan(0.97f));
            var added = Image(source, new Vector2(30, 30), Color.blue);
            filter.Refresh(Render);
            Assert.That(At(Draw(), 0).b, Is.GreaterThan(0.97f));
            Assert.That(added.GetComponent<ObjectFilterSourceVisibility>(), Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() => filter.Refresh((_, _) => throw new InvalidOperationException("render failure")));
            Assert.That(filter.isPresenting, Is.False);
            Assert.That(filter.texture, Is.Null);
            Assert.That(filter.presentation.gameObject.activeSelf, Is.False);
            Assert.That(At(Draw(), 0).b, Is.GreaterThan(0.97f));
            filter.Refresh(Render);
            Assert.That(filter.isPresenting, Is.True);
            filter.blurSize = 0;
            filter.Refresh(Render);
            Assert.That(filter.isPresenting, Is.False);
            Assert.That(At(Draw(), 0).b, Is.GreaterThan(0.97f));
        }

        [UnityTest]
        public IEnumerator NestedFiltersComposeOnceAndDisablingEitherRestoresItsOwnSource()
        {
            var host = Scene();
            var outerSource = Rect("Outer", host, new Vector2(100, 100));
            outerSource.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
            var innerSource = Rect("Inner", outerSource, new Vector2(64, 64));
            innerSource.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
            Image(innerSource, new Vector2(60, 60), Color.red);
            var inner = Filter(innerSource, 1);
            var outer = Filter(outerSource, 1);
            yield return null;
            outer.Refresh(Render);
            Color both = At(Draw(), 0);
            Assert.That(both.a, Is.EqualTo(0.25f).Within(0.03f));
            Assert.That(both.r, Is.EqualTo(both.a).Within(0.02f));
            inner.enabled = false;
            outer.Refresh(Render);
            Assert.That(At(Draw(), 0).a, Is.EqualTo(0.25f).Within(0.03f));
            inner.enabled = true;
            outer.Refresh(Render);
            outer.enabled = false;
            Assert.That(inner.isPresenting, Is.True);
            Assert.That(At(Draw(), 0).a, Is.EqualTo(0.25f).Within(0.03f));
            inner.enabled = false;
            Assert.That(inner.isPresenting, Is.False);
        }

        [UnityTest]
        public IEnumerator PreviouslyRenderedGraphicsCanJoinAndLeaveAnActiveFilter()
        {
            var host = Scene();
            var source = Rect("Source", host, new Vector2(64, 64));
            Image(source, new Vector2(60, 60), Color.red);
            var filter = Filter(source);
            yield return null;
            filter.Refresh(Render);
            var added = Image(host, new Vector2(20, 20), new Color(0, 0, 1, 0.5f));
            added.transform.localPosition = new Vector3(48, 0, 0);
            // Rebuild it before it is known to the filter; adding the modifier must mark it dirty.
            Color original = At(Draw(), 48);
            added.transform.SetParent(source, false);
            added.transform.localPosition = Vector3.zero;
            filter.Refresh(Render);
            Color inside = At(Draw(), 0);
            Assert.That(inside.r, Is.EqualTo(0.5f).Within(0.04f));
            Assert.That(inside.b, Is.EqualTo(0.5f).Within(0.04f));
            Assert.That(inside.a, Is.GreaterThan(0.97f));
            AssertRaycast(added);
            // Do not refresh the old owner after detaching: its source list must not be needed.
            added.transform.SetParent(host, false);
            added.transform.localPosition = new Vector3(48, 0, 0);
            Color outside = At(Draw(), 48);
            Assert.That(outside.a, Is.EqualTo(original.a).Within(0.01f));
            Assert.That(outside.b, Is.EqualTo(original.b).Within(0.01f));
            Assert.That(outside.b, Is.GreaterThan(0.4f));
            AssertRaycast(added);
        }
    }
}
