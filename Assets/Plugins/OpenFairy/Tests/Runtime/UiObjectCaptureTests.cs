using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if FAIRYGUI_TMPRO
using TMPro;
using UnityEngine.TextCore;
#endif
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public sealed class CaptureLifecycleWitness : MonoBehaviour
    {
        public static int enables;
        private void OnEnable() => enables++;
    }

    public class UiObjectCaptureTests
    {
        private readonly List<Object> _owned = new();
        private UiObjectCapture _capture;
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }

        private RectTransform MakeRoot()
        {
            var canvas = Own(new GameObject("Source Canvas", typeof(RectTransform), typeof(Canvas)));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            return Rect("Target", canvas.transform, new Vector2(128, 128));
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static UnityEngine.UI.Image Image(Transform parent, Vector2 size, Color color, Vector2 position = default)
        {
            var image = Rect("Image", parent, size, position).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            return image;
        }

        private RenderTexture Destination()
        {
            var rt = Own(new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear));
            rt.antiAliasing = 1;
            rt.Create();
            return rt;
        }

        private UiObjectCapture Capture(RectTransform target)
        {
            var shader = Shader.Find("OpenFairy/UI Capture");
            Assert.That(shader, Is.Not.Null);
            return _capture = new UiObjectCapture(target, shader, 31);
        }

        private static void Render(Camera camera, RenderTexture destination)
        {
            if (GraphicsSettings.currentRenderPipeline == null)
                camera.Render();
            else
            {
                var request = new RenderPipeline.StandardRequest { destination = destination };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
        }

        private Texture2D Read(RenderTexture source)
        {
            var texture = Own(new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true));
            var active = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                texture.ReadPixels(new UnityEngine.Rect(0, 0, source.width, source.height), 0, 0);
                texture.Apply();
                return texture;
            }
            finally { RenderTexture.active = active; }
        }

        private static Color At(Texture2D image, UnityEngine.Rect bounds, Vector2 point) => image.GetPixel(
            Mathf.Clamp((int)((point.x - bounds.xMin) / bounds.width * image.width), 0, image.width - 1),
            Mathf.Clamp((int)((point.y - bounds.yMin) / bounds.height * image.height), 0, image.height - 1));

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _capture?.Dispose();
            _capture = null;
            foreach (var obj in _owned)
            {
                if (obj is RenderTexture rt) rt.Release();
                Object.Destroy(obj);
            }
            _owned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ImageCaptureIsPmaAndExcludesOuterAndTargetOpacity()
        {
            var root = MakeRoot();
            var outer = root.parent.gameObject.AddComponent<CanvasGroup>();
            outer.alpha = 0;
            var ownAlpha = root.gameObject.AddComponent<CanvasGroup>();
            ownAlpha.alpha = 0.3f;
            var inner = Rect("Inner Alpha", root, new Vector2(100, 100));
            inner.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
            var source = Image(inner, new Vector2(100, 100), new Color(1, 0, 0, 0.5f));
            yield return null;
            var capture = Capture(root);
            var rt = Destination();
            capture.Capture(rt, Render);
            Color color = At(Read(rt), capture.bounds, Vector2.zero);
            Assert.That(color.a, Is.EqualTo(0.25f).Within(0.025f));
            Assert.That(color.r, Is.EqualTo(color.a).Within(0.01f));
            Assert.That(color.g + color.b, Is.LessThan(0.01f));
            Assert.That(outer.alpha, Is.Zero);
            Assert.That(ownAlpha.alpha, Is.EqualTo(0.3f));
            Assert.That(source.raycastTarget, Is.True);
            Assert.That(source.color.a, Is.EqualTo(0.5f));
        }

        [UnityTest]
        public IEnumerator InternalStencilAndRectMasksAreReplayedWithoutOuterClipping()
        {
            var root = MakeRoot();
            var outside = Rect("Outer Mask", root.parent, new Vector2(10, 10));
            outside.gameObject.AddComponent<RectMask2D>();
            root.SetParent(outside, false);
            var viewport = Rect("Viewport", root, new Vector2(100, 60));
            viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(4, 6);
            var maskImage = Image(viewport, new Vector2(80, 80), Color.white);
            var texture = Own(new Texture2D(64, 64));
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    pixels[y * 64 + x] = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32)) < 25
                        ? Color.white : Color.clear;
            texture.SetPixels(pixels);
            texture.Apply();
            maskImage.sprite = Own(Sprite.Create(texture, new UnityEngine.Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f)));
            maskImage.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Image(maskImage.transform, new Vector2(120, 120), new Color(0, 0, 1, 0.5f));
            yield return null;
            var capture = Capture(root);
            var rt = Destination();
            var noStencil = Own(new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32));
            noStencil.Create();
            Assert.Throws<ArgumentException>(() => capture.Capture(noStencil, Render, localBounds: root.rect));
            capture.Capture(rt, Render, localBounds: root.rect);
            var image = Read(rt);
            Assert.That(At(image, capture.bounds, new Vector2(20, 0)).a, Is.EqualTo(0.5f).Within(0.03f));
            Assert.That(At(image, capture.bounds, new Vector2(38, 0)).a, Is.LessThan(0.02f));
            Assert.That(At(image, capture.bounds, new Vector2(0, 38)).a, Is.LessThan(0.02f));
            float soft = At(image, capture.bounds, new Vector2(0, 29)).a;
            Assert.That(soft, Is.GreaterThan(0.01f).And.LessThan(0.49f));
            Assert.That(root.GetComponentInParent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        }

        [UnityTest]
        public IEnumerator CaptureAndRenderFailureLeaveBusinessObjectsAndSourceStateUntouched()
        {
            var root = MakeRoot();
            root.gameObject.AddComponent<CaptureLifecycleWitness>();
            var image = Image(root, new Vector2(90, 70), Color.red);
            image.gameObject.AddComponent<CaptureLifecycleWitness>();
            yield return null;
            int enables = CaptureLifecycleWitness.enables;
            var parent = root.parent;
            var position = root.localPosition;
            var size = root.sizeDelta;
            bool culled = image.canvasRenderer.cull;
            var capture = Capture(root);
            var rt = Destination();
            var active = RenderTexture.active;
            Camera transient = null;
            Assert.Throws<InvalidOperationException>(() => capture.Capture(rt, (camera, _) =>
            {
                transient = camera;
                throw new InvalidOperationException("Render failure");
            }));
            Assert.That(transient.gameObject.activeInHierarchy, Is.False);
            Assert.That(transient.targetTexture, Is.Null);
            Assert.That(RenderTexture.active, Is.SameAs(active));
            capture.Capture(rt, Render);
            capture.Capture(rt, Render);
            Assert.That(CaptureLifecycleWitness.enables, Is.EqualTo(enables));
            Assert.That(root.parent, Is.SameAs(parent));
            Assert.That(root.localPosition, Is.EqualTo(position));
            Assert.That(root.sizeDelta, Is.EqualTo(size));
            Assert.That(root.gameObject.activeSelf, Is.True);
            Assert.That(image.enabled, Is.True);
            Assert.That(image.raycastTarget, Is.True);
            Assert.That(image.canvasRenderer.cull, Is.EqualTo(culled));
            Assert.That(At(Read(rt), capture.bounds, Vector2.zero).r, Is.GreaterThan(0.95f));
            capture.Dispose();
            capture.Dispose();
            Assert.Throws<ObjectDisposedException>(() => capture.Capture(rt, Render));
        }

        [UnityTest]
        public IEnumerator SameFrameRemovalAndReadditionDoNotRenderStaleProxyNodes()
        {
            var root = MakeRoot();
            Image(root, new Vector2(110, 110), Color.red);
            var foreground = Image(root, new Vector2(30, 30), Color.green);
            yield return null;
            var capture = Capture(root);
            var rt = Destination();
            capture.Capture(rt, Render, localBounds: root.rect);
            var first = Read(rt).GetPixels32();
            foreground.gameObject.SetActive(false);
            capture.Capture(rt, Render, localBounds: root.rect);
            var middle = Read(rt);
            Assert.That(At(middle, capture.bounds, Vector2.zero).r, Is.GreaterThan(0.95f));
            Assert.That(At(middle, capture.bounds, Vector2.zero).g, Is.LessThan(0.03f));
            foreground.gameObject.SetActive(true);
            capture.Capture(rt, Render, localBounds: root.rect);
            CollectionAssert.AreEqual(first, Read(rt).GetPixels32());
        }


        private Camera SourceCamera(RectTransform root, RenderTexture output)
        {
            var canvas = root.parent.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            canvas.transform.localScale = Vector3.one;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(128, 128);
            canvas.gameObject.layer = root.gameObject.layer = 30;
            var camera = Own(new GameObject("Source Camera", typeof(Camera))).GetComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -100);
            camera.orthographic = true; camera.orthographicSize = 64; camera.aspect = 1;
            camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
            camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .12f, .16f, 1);
            camera.allowHDR = camera.allowMSAA = false;
            camera.targetTexture = output;
            canvas.worldCamera = camera;
            return camera;
        }

        private void Adjust(UnityEngine.UI.Image image, float brightness = 0)
        {
            var effect = image.gameObject.AddComponent<ColorAdjust>();
            effect.graphic = image;
            effect.shader = Shader.Find("OpenFairy/UI ColorMatrix");
            Assert.That(effect.shader, Is.Not.Null);
            effect.Set(brightness, 0, 0, 0);
        }

        [UnityTest]
        public IEnumerator StencilColorCaptureKeepsSourceMaterialsAndPopAcrossFrames()
        {
            Assume.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Gamma), "The expected values are gamma-space blend results.");
            yield return StencilModifierLifetime(false);
        }

        [UnityTest]
        public IEnumerator StencilBlendCaptureKeepsSourceMaterialsAndPopAcrossFrames()
        {
            Assume.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Gamma), "The expected values are gamma-space blend results.");
            yield return StencilModifierLifetime(true);
        }

        private IEnumerator StencilModifierLifetime(bool blend)
        {
            var root = MakeRoot();
            var output = Destination();
            var camera = SourceCamera(root, output);
            var maskImage = Image(root, new Vector2(80, 80), Color.white);
            maskImage.gameObject.layer = 30;
            var alpha = Own(new Texture2D(16, 16) { filterMode = FilterMode.Point });
            var pixels = new Color[256];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++) pixels[y * 16 + x] = x < 8 ? Color.white : Color.clear;
            alpha.SetPixels(pixels); alpha.Apply();
            maskImage.sprite = Own(Sprite.Create(alpha, new UnityEngine.Rect(0, 0, 16, 16), new Vector2(.5f, .5f)));
            var mask = maskImage.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            Adjust(maskImage);
            var image = Image(maskImage.transform, new Vector2(104, 104), new Color(.2f, .4f, .7f, .5f));
            image.gameObject.layer = 30;
            if (blend) Blend(image, BlendMode.Add);
            else Adjust(image, .1f);
            yield return null;
            Canvas.ForceUpdateCanvases(); Render(camera, output);
            var baseline = Read(output);
            var inside = At(baseline, root.rect, new Vector2(-20, 0));
            var outside = At(baseline, root.rect, new Vector2(20, 0));
            Assert.That(inside.b, Is.GreaterThan(outside.b + .1f), "Visible source positive control");
            AssertRgba(At(baseline, root.rect, new Vector2(46, 0)), outside, "Mask rectangle positive control");
            var material = image.canvasRenderer.GetMaterial();
            var writer = maskImage.canvasRenderer.GetMaterial();
            var pop = maskImage.canvasRenderer.GetPopMaterial(0);
            Assert.That(material.GetFloat("_StencilComp"), Is.EqualTo((float)CompareFunction.Equal));
            Assert.That(maskImage.canvasRenderer.popMaterialCount, Is.EqualTo(1));
            var capture = Capture(image.rectTransform);
            var rt = Destination();
            for (int i = 0; i < 3; i++)
            {
                capture.Capture(rt, Render, localBounds: image.rectTransform.rect);
                var unmasked = Read(rt);
                var color = At(unmasked, capture.bounds, new Vector2(-20, 0));
                Assert.That(color.b, Is.GreaterThan(.1f));
                Assert.That(color.a, Is.EqualTo(blend ? 0 : .5f).Within(.025f));
                AssertRgba(At(unmasked, capture.bounds, new Vector2(46, 0)), color, "Child-only capture excludes its outer mask");
                int frame = Time.frameCount;
                yield return null;
                Assert.That(Time.frameCount, Is.GreaterThan(frame));
                Assert.That(material != null && writer != null && pop != null, Is.True, "Source native materials survive capture");
                Assert.That(image.canvasRenderer.GetMaterial(), Is.SameAs(material));
                Assert.That(maskImage.canvasRenderer.GetMaterial(), Is.SameAs(writer));
                Assert.That(maskImage.canvasRenderer.GetPopMaterial(0), Is.SameAs(pop));
                Canvas.ForceUpdateCanvases(); Render(camera, output);
                var original = Read(output);
                AssertRgba(At(original, root.rect, new Vector2(-20, 0)), inside, "Source remains visible next frame");
                AssertRgba(At(original, root.rect, new Vector2(20, 0)), outside, "Source mask stays active");
            }
            capture.Dispose();
            capture = Capture(root);
            capture.Capture(rt, Render, localBounds: root.rect);
            var masked = Read(rt);
            Assert.That(At(masked, capture.bounds, new Vector2(-20, 0)).b, Is.GreaterThan(.1f));
            AssertRgba(At(masked, capture.bounds, new Vector2(20, 0)), Color.clear, "Proxy mask retains its alpha silhouette");
            AssertRgba(At(masked, capture.bounds, new Vector2(46, 0)), Color.clear, "Proxy mask retains its rectangle");
            yield return null;
            Assert.That(material != null && writer != null && pop != null, Is.True);
            Assert.That(image.canvasRenderer.GetMaterial(), Is.SameAs(material));
            Assert.That(maskImage.canvasRenderer.GetMaterial(), Is.SameAs(writer));
            Assert.That(maskImage.canvasRenderer.GetPopMaterial(0), Is.SameAs(pop));
            Assert.That(maskImage.canvasRenderer.popMaterialCount, Is.EqualTo(1));
            Canvas.ForceUpdateCanvases(); Render(camera, output);
            AssertRgba(At(Read(output), root.rect, new Vector2(-20, 0)), inside, "Internal proxy masks leave the native source intact");
            Assert.That(image.raycastTarget && maskImage.raycastTarget, Is.True);
        }

        private static BlendModeEffect Blend(UnityEngine.UI.Image image, BlendMode mode)
        {
            var effect = image.gameObject.AddComponent<BlendModeEffect>();
            effect.shader = Shader.Find("OpenFairy/UI Blend");
            Assert.That(effect.shader, Is.Not.Null);
            effect.blendMode = mode;
            return effect;
        }

        private static Color BlendInput(Color color, BlendMode mode)
            => mode != BlendMode.Normal && QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;

        private static Color IsolatedBlendReference(BlendMode mode, Color source, Color destination)
        {
            // Independent framebuffer equations, not the production material-factor helper.
            var s = new Vector3(source.r, source.g, source.b);
            var d = new Vector3(destination.r, destination.g, destination.b);
            var p = s * source.a;
            Vector3 rgb;
            float alpha;
            switch (mode)
            {
                case BlendMode.None: rgb = s + d; alpha = destination.a; break;
                case BlendMode.Add: rgb = p + d; alpha = destination.a; break;
                case BlendMode.Multiply: rgb = Vector3.Scale(p, d) + d * (1 - source.a); alpha = destination.a; break;
                case BlendMode.Screen: rgb = p + d - Vector3.Scale(p, d); alpha = source.a + destination.a * (1 - source.a); break;
                case BlendMode.Erase: rgb = d * (1 - source.a); alpha = destination.a * (1 - source.a); break;
                case BlendMode.Mask: rgb = d * source.a; alpha = destination.a * source.a; break;
                case BlendMode.Below:
                    rgb = s * (1 - destination.a) + d * destination.a;
                    alpha = source.a * (1 - destination.a) + destination.a * destination.a;
                    break;
                case BlendMode.Off: rgb = s; alpha = source.a; break;
                case BlendMode.One_OneMinusSrcAlpha: rgb = s + d * (1 - source.a); alpha = source.a + destination.a * (1 - source.a); break;
                default: rgb = p + d * (1 - source.a); alpha = source.a + destination.a * (1 - source.a); break;
            }
            return new Color(rgb.x, rgb.y, rgb.z, alpha);
        }

        private static void AssertRgba(Color actual, Color expected, string context)
        {
            for (int channel = 0; channel < 4; channel++)
                Assert.That(actual[channel], Is.EqualTo(expected[channel]).Within(.015), context + " channel " + channel);
        }

        [UnityTest]
        public IEnumerator AuthoredBlendModesKeepRgbFactorsAndIsolatedCaptureCoverage()
        {
            Assume.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Gamma), "The expected values are gamma-space blend results.");
            var root = MakeRoot();
            var background = Image(root, new Vector2(100, 100), new Color(.4f, .2f, .6f, .5f));
            var foreground = Image(root, new Vector2(100, 100), new Color(.4f, .7f, .2f, .4f));
            var original = foreground.material;
            var effect = Blend(foreground, BlendMode.Add);
            yield return null;
            var capture = Capture(root);
            var rt = Destination();
            var destination = new Color(.2f, .1f, .3f, .5f);
            foreach (BlendMode mode in Enum.GetValues(typeof(BlendMode)))
            {
                effect.blendMode = mode;
                capture.Capture(rt, Render, localBounds: root.rect);
                var expected = IsolatedBlendReference(mode, BlendInput(foreground.color, mode), destination);
                AssertRgba(At(Read(rt), capture.bounds, new Vector2(12, 8)), expected, mode.ToString());
            }
            // Alpha zero is a real active additive graphic, not a reason to skip its mode.
            effect.blendMode = BlendMode.Add;
            foreground.color = new Color(.4f, .7f, .2f, 0);
            capture.Capture(rt, Render, localBounds: root.rect);
            AssertRgba(At(Read(rt), capture.bounds, new Vector2(12, 8)), destination, "Transparent additive source");
            Assert.That(foreground.material, Is.SameAs(original));
            Assert.That(foreground.raycastTarget && background.raycastTarget, Is.True);
        }

        [UnityTest]
        public IEnumerator GrayedAdditiveEmissionAndScreenCompositeOnceOverANonblackBackdrop()
        {
            Assume.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Gamma), "The expected values are gamma-space blend results.");
            var root = MakeRoot();
            var canvas = root.parent.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            canvas.transform.localScale = Vector3.one;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(128, 128);
            canvas.gameObject.layer = root.gameObject.layer = 30;
            var background = Image(canvas.transform, new Vector2(128, 128), new Color(.1f, .25f, .4f, 1));
            background.transform.SetAsFirstSibling();
            background.gameObject.layer = 30;
            var image = Image(root, new Vector2(100, 100), new Color(.8f, .2f, .4f, .5f));
            image.gameObject.layer = 30;
            var original = image.material;
            var effect = Blend(image, BlendMode.Add);
            var opacity = root.gameObject.AddComponent<CanvasGroup>(); opacity.alpha = .5f;
            var camera = Own(new GameObject("Camera", typeof(Camera))).GetComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -100);
            camera.orthographic = true; camera.orthographicSize = 64; camera.aspect = 1;
            camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
            camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.allowHDR = camera.allowMSAA = false;
            canvas.worldCamera = camera;
            var output = Destination();
            camera.targetTexture = output;
            var filter = root.gameObject.AddComponent<ColorAdjust>();
            filter.shader = Shader.Find("OpenFairy/UI ColorMatrix");
            filter.captureShader = Shader.Find("OpenFairy/UI Capture");
            filter.captureLayer = 31;
            filter.presentation = Rect("Presentation", root.parent, root.rect.size).gameObject.AddComponent<RawImage>();
            filter.presentation.gameObject.layer = 30;
            filter.presentationGroup = filter.presentation.gameObject.AddComponent<CanvasGroup>();
            filter.presentationMaterial = Own(new Material(Shader.Find("OpenFairy/UI Premultiplied")));
            filter.enabled = false;
            var gray = root.gameObject.AddComponent<Grayed>();
            gray.filter = filter; filter.grayed = gray; gray.Refresh();
            yield return null;
            root.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases(); Render(camera, output);
            var baseline = Read(output).GetPixel(76, 72);
            Assert.That(baseline.b, Is.GreaterThan(.1f), "The backdrop positive control must be visible and nonblack.");
            root.gameObject.SetActive(true);
            filter.Refresh(Render);
            var source = BlendInput(image.color, BlendMode.Add);
            float emission = (.299f * source.r + .587f * source.g + .114f * source.b) * source.a;
            var captured = Read(filter.texture).GetPixel(76, 72);
            AssertRgba(captured, new Color(emission, emission, emission, 0), "Gray preserves zero-coverage emission");
            Canvas.ForceUpdateCanvases(); Render(camera, output);
            AssertRgba(Read(output).GetPixel(76, 72), new Color(baseline.r + emission * .5f,
                baseline.g + emission * .5f, baseline.b + emission * .5f, baseline.a), "Add over external backdrop");
            filter.Set(.25f, 0, 0, 0);
            filter.Refresh(Render);
            AssertRgba(Read(filter.texture).GetPixel(76, 72), captured, "Color offset is weighted by coverage, not emission");
            effect.blendMode = BlendMode.Screen;
            filter.Refresh(Render);
            float screen = emission + .25f * source.a;
            AssertRgba(Read(filter.texture).GetPixel(76, 72), new Color(screen, screen, screen, source.a), "Screen has source coverage");
            Canvas.ForceUpdateCanvases(); Render(camera, output);
            AssertRgba(Read(output).GetPixel(76, 72), new Color(screen * .5f + baseline.r * (1 - source.a * .5f),
                screen * .5f + baseline.g * (1 - source.a * .5f), screen * .5f + baseline.b * (1 - source.a * .5f), 1), "Screen over external backdrop");
            filter.Dispose();
            Assert.That(filter.texture, Is.Null);
            Assert.That(image.material, Is.SameAs(original));
            Assert.That(image.raycastTarget && opacity.blocksRaycasts, Is.True);
        }

#if FAIRYGUI_TMPRO
        [UnityTest]
        public IEnumerator MixedSdfAndNativeSpriteSubmeshesKeepAtlasAndPmaAlpha()
        {
            var root = MakeRoot();
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null);
            var font = Own(TMP_FontAsset.CreateFontAsset(TMP_Settings.defaultFontAsset.sourceFontFile));
            font.TryAddCharacters("X");
            for (int i = 0; i < font.atlasTextureCount; i++) Own(font.atlasTextures[i]);
            Own(font.material);
            var maskImage = Image(root, new Vector2(120, 80), Color.white);
            maskImage.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var label = Rect("Text", maskImage.transform, new Vector2(110, 70)).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 36;
            label.color = new Color(1, 0, 0, 0.5f);
            label.alignment = TextAlignmentOptions.Center;
            var texture = Own(new Texture2D(8, 8));
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.blue;
            texture.SetPixels(pixels); texture.Apply();
            var sprites = Own(ScriptableObject.CreateInstance<TMP_SpriteAsset>());
            JsonUtility.FromJsonOverwrite("{\"m_Version\":\"1.1.0\"}", sprites);
            sprites.faceInfo = new FaceInfo { pointSize = 1, scale = 1, ascentLine = 0.8f, descentLine = -0.2f, lineHeight = 1 };
            sprites.spriteSheet = texture;
            var glyph = new TMP_SpriteGlyph(0, new GlyphMetrics(1, 1, 0, 0.8f, 1), new GlyphRect(0, 0, 8, 8), 1, 0);
            sprites.spriteGlyphTable.Add(glyph);
            sprites.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, sprites, glyph) { name = "badge", scale = 1 });
            sprites.UpdateLookupTables();
            sprites.material = Own(new Material(Shader.Find("TextMeshPro/Sprite")) { mainTexture = texture });
            label.spriteAsset = sprites;
            label.text = "X<sprite name=\"badge\">";
            yield return null;
            label.ForceMeshUpdate();
            Assert.That(label.textInfo.characterInfo[1].elementType, Is.EqualTo(TMP_TextElementType.Sprite));
            Canvas.ForceUpdateCanvases();
            var submesh = label.GetComponentInChildren<TMP_SubMeshUI>();
            Assert.That(submesh, Is.Not.Null);
            var textMaterial = label.canvasRenderer.GetMaterial();
            var spriteMaterial = submesh.canvasRenderer.GetMaterial();
            var pop = maskImage.canvasRenderer.GetPopMaterial(0);
            Assert.That(textMaterial.GetFloat("_StencilComp"), Is.EqualTo((float)CompareFunction.Equal));
            var capture = Capture(root);
            var rt = Destination();
            capture.Capture(rt, Render, localBounds: root.rect);
            var image = Read(rt);
            var matrix = root.worldToLocalMatrix * label.transform.localToWorldMatrix;
            var fontChar = label.textInfo.characterInfo[0];
            var spriteChar = label.textInfo.characterInfo[1];
            Color textColor = At(image, capture.bounds, matrix.MultiplyPoint3x4((fontChar.bottomLeft + fontChar.topRight) * 0.5f));
            Color spriteColor = At(image, capture.bounds, matrix.MultiplyPoint3x4((spriteChar.bottomLeft + spriteChar.topRight) * 0.5f));
            Assert.That(textColor.a, Is.GreaterThan(0.1f).And.LessThan(0.6f));
            Assert.That(textColor.r, Is.EqualTo(textColor.a).Within(0.025f));
            Assert.That(spriteColor.a, Is.EqualTo(0.5f).Within(0.04f));
            Assert.That(spriteColor.b, Is.EqualTo(spriteColor.a).Within(0.025f));
            Assert.That(spriteColor.r, Is.LessThan(0.03f));
            yield return null;
            Assert.That(textMaterial != null && spriteMaterial != null && pop != null, Is.True);
            Assert.That(label.canvasRenderer.GetMaterial(), Is.SameAs(textMaterial));
            Assert.That(submesh.canvasRenderer.GetMaterial(), Is.SameAs(spriteMaterial));
            capture.Dispose();
            capture = Capture(label.rectTransform);
            capture.Capture(rt, Render, localBounds: label.rectTransform.rect);
            var unmasked = Read(rt);
            var unmaskedText = At(unmasked, capture.bounds, (fontChar.bottomLeft + fontChar.topRight) * .5f);
            var unmaskedSprite = At(unmasked, capture.bounds, (spriteChar.bottomLeft + spriteChar.topRight) * .5f);
            Assert.That(unmaskedText.a, Is.GreaterThan(.1f).And.LessThan(.6f));
            Assert.That(unmaskedText.r, Is.EqualTo(unmaskedText.a).Within(.025f));
            Assert.That(unmaskedSprite.a, Is.EqualTo(.5f).Within(.04f));
            Assert.That(unmaskedSprite.b, Is.EqualTo(unmaskedSprite.a).Within(.025f));
            Assert.That(unmaskedSprite.r, Is.LessThan(.03f));
            yield return null;
            Assert.That(textMaterial != null && spriteMaterial != null && pop != null, Is.True);
            Assert.That(label.canvasRenderer.GetMaterial(), Is.SameAs(textMaterial));
            Assert.That(submesh.canvasRenderer.GetMaterial(), Is.SameAs(spriteMaterial));
            Assert.That(maskImage.canvasRenderer.GetPopMaterial(0), Is.SameAs(pop));
            label.text = "";
            capture.Capture(rt, Render, localBounds: root.rect);
            foreach (var pixel in Read(rt).GetPixels32())
                Assert.That(pixel.a, Is.Zero);
        }
#endif
    }
}
