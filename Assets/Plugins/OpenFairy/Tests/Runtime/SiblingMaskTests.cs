using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public class SiblingMaskTests
    {
        private readonly List<Object> _owned = new();
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.layer = 30;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(64, 64);
            return rect;
        }

        private Sprite Sprite(params Color[] pixels)
        {
            var texture = Own(new Texture2D(pixels.Length, 1, TextureFormat.RGBA32, false, true));
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels(pixels);
            texture.Apply();
            return Own(UnityEngine.Sprite.Create(texture, new UnityEngine.Rect(0, 0, pixels.Length, 1), Vector2.one * .5f));
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var value in _owned)
                if (value is GameObject go && go.TryGetComponent<Camera>(out var camera)) camera.targetTexture = null;
            for (var i = _owned.Count - 1; i >= 0; i--)
            {
                if (_owned[i] is RenderTexture target) target.Release();
                Object.DestroyImmediate(_owned[i]);
            }
            _owned.Clear();
        }

        [UnityTest]
        public IEnumerator Mask_alpha_and_flip_change_only_clipping_while_content_keeps_parent_alpha_and_orientation()
        {
            var camera = Own(new GameObject("Camera", typeof(Camera))).GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 32;
            camera.aspect = 1;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 30;
            var canvas = Own(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup)));
            canvas.layer = 30;
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvas.GetComponent<Canvas>().worldCamera = camera;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(64, 64);
            var parentAlpha = canvas.GetComponent<CanvasGroup>();
            parentAlpha.alpha = .4f;
            var carrier = Rect("Stencil", canvas.transform);
            var shape = carrier.gameObject.AddComponent<MaskShape>();
            carrier.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var source = Rect("Authored mask", carrier);
            source.gameObject.AddComponent<UnityEngine.UI.Image>().sprite = Sprite(Color.white, Color.white, Color.white, Color.clear);
            var maskAlpha = source.gameObject.AddComponent<CanvasGroup>();
            maskAlpha.alpha = .2f;
            shape.source = source.gameObject.AddComponent<MaskSource>();
            var content = Rect("Content", carrier);
            content.gameObject.AddComponent<UnityEngine.UI.Image>().sprite = Sprite(Color.red, Color.blue);
            var target = Own(new RenderTexture(64, 64, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear));
            target.Create();
            camera.targetTexture = target;
            var image = Own(new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true));
            yield return null;

            void Draw()
            {
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
                else RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                image.ReadPixels(new UnityEngine.Rect(0, 0, 64, 64), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
            }

            void Pixel(int x, Color expected)
            {
                var actual = image.GetPixel(x, 32);
                Assert.That(actual.r, Is.EqualTo(expected.r).Within(.000001f), "red at " + x);
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(.000001f), "green at " + x);
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(.000001f), "blue at " + x);
            }

            Draw();
            Pixel(8, Color.red * .4f);
            Pixel(40, Color.blue * .4f);
            Pixel(56, Color.black);
            source.localScale = new Vector3(-1, 1, 1);
            Draw();
            Pixel(8, Color.black);
            Pixel(24, Color.red * .4f);
            Pixel(56, Color.blue * .4f);
            maskAlpha.alpha = 0;
            Draw();
            Pixel(24, Color.black);
            Pixel(56, Color.black);
            maskAlpha.alpha = .2f;
            parentAlpha.alpha = 1;
            Draw();
            Pixel(24, Color.red);
            Pixel(56, Color.blue);
            Assert.That(content.localScale, Is.EqualTo(Vector3.one));
            Assert.That(content.parent, Is.SameAs(source.parent));
        }
    }
}
