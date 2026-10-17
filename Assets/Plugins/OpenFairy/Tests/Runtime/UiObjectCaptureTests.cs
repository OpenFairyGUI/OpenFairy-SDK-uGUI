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
            var label = Rect("Text", root, new Vector2(110, 70)).gameObject.AddComponent<TextMeshProUGUI>();
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
            label.text = "";
            capture.Capture(rt, Render, localBounds: root.rect);
            foreach (var pixel in Read(rt).GetPixels32())
                Assert.That(pixel.a, Is.Zero);
        }
#endif
    }
}
