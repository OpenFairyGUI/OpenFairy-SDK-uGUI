using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public class ColorFilterTests
    {
        private readonly List<Object> _owned = new();
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }

        [Test]
        public void Matrix_identity_and_saturation_have_the_authored_order()
        {
            var matrix = new ColorMatrix();
            matrix.Set(0, 0, 0, 0);
            for (var i = 0; i < 20; i++) Assert.That(matrix.Values[i], Is.EqualTo(i % 6 == 0 ? 1 : 0).Within(.000001));
            matrix.Set(.2f, .5f, -1, 0);
            for (var row = 0; row < 3; row++)
            {
                var i = row * 5;
                Assert.That(matrix.Values[i], Is.EqualTo(.299f * 1.5f).Within(.000001));
                Assert.That(matrix.Values[i + 1], Is.EqualTo(.587f * 1.5f).Within(.000001));
                Assert.That(matrix.Values[i + 2], Is.EqualTo(.114f * 1.5f).Within(.000001));
                Assert.That(matrix.Values[i + 4], Is.EqualTo(.2f * 1.5f - 128f / 255 * .5f).Within(.000001));
            }
        }

        private RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return rect;
        }
        private RectTransform Source()
        {
            var canvas = Own(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            return Rect("Source", canvas.transform, new Vector2(64, 64));
        }
        private static UnityEngine.UI.Image Image(RectTransform target, Color color)
        {
            var image = target.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            return image;
        }
        private Shader Shader(string name)
        {
            var shader = UnityEngine.Shader.Find(name);
            Assert.That(shader, Is.Not.Null);
            return shader;
        }
        private ColorAdjust Filter(RectTransform source, Graphic graphic = null)
        {
            var filter = source.gameObject.AddComponent<ColorAdjust>();
            filter.shader = Shader("OpenFairy/UI ColorMatrix");
            filter.graphic = graphic;
            if (graphic == null)
            {
                filter.presentation = Rect("Presentation", source.parent, source.rect.size).gameObject.AddComponent<RawImage>();
                filter.presentationGroup = filter.presentation.gameObject.AddComponent<CanvasGroup>();
                filter.presentationMaterial = Own(new Material(Shader("OpenFairy/UI Premultiplied")));
                filter.captureShader = Shader("OpenFairy/UI Capture");
                filter.captureLayer = 31;
            }
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
        private Color Read(RenderTexture texture)
        {
            var active = RenderTexture.active;
            var read = Own(new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true));
            try
            {
                RenderTexture.active = texture;
                read.ReadPixels(new UnityEngine.Rect(0, 0, texture.width, texture.height), 0, 0);
                read.Apply();
                return read.GetPixel(texture.width / 2, texture.height / 2);
            }
            finally { RenderTexture.active = active; }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var value in _owned)
            {
                if (value is RenderTexture texture) texture.Release();
                Object.Destroy(value);
            }
            _owned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Direct_image_preserves_alpha_stencil_and_restores_its_original_material()
        {
            var source = Source();
            var image = Image(source, new Color(.2f, .4f, .8f, .5f));
            var original = image.material;
            var filter = Filter(source, image);
            filter.Set(.2f, 0, 0, 0);
            yield return null;
            var stencil = Own(new Material(original));
            stencil.SetInt("_Stencil", 7);
            stencil.EnableKeyword("UNITY_UI_ALPHACLIP");
            var changed = filter.GetModifiedMaterial(stencil);
            Assert.That(changed.GetInt("_Stencil"), Is.EqualTo(7));
            Assert.That(changed.IsKeywordEnabled("UNITY_UI_ALPHACLIP"), Is.True);
            Assert.That(image.material, Is.SameAs(original));
            var texture = Own(new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32));
            texture.Create();
            using (var capture = new UiObjectCapture(source, Shader("OpenFairy/UI Capture"), 31)) capture.Capture(texture, Render);
            var color = Read(texture);
            Assert.That(color.a, Is.EqualTo(.5f).Within(.025));
            Assert.That(color.r, Is.EqualTo(.2f).Within(.025));
            Assert.That(color.g, Is.EqualTo(.3f).Within(.025));
            Assert.That(color.b, Is.EqualTo(.5f).Within(.025));
            filter.enabled = false;
            Assert.That(filter.GetModifiedMaterial(original), Is.SameAs(original));
            Assert.That(image.raycastTarget, Is.True);
        }

        [UnityTest]
        public IEnumerator Container_transforms_the_composited_color_once_and_releases_without_hiding_input()
        {
            var source = Source();
            var group = source.gameObject.AddComponent<CanvasGroup>();
            group.alpha = .35f;
            var back = Image(Rect("Back", source, source.rect.size), new Color(.2f, .4f, .8f));
            var front = Image(Rect("Front", source, source.rect.size), new Color(.9f, .1f, .2f, .5f));
            var filter = Filter(source);
            filter.Set(0, 1, 0, 0);
            yield return null;
            filter.Refresh(Render);
            var color = Read(filter.texture);
            Assert.That(color.r, Is.EqualTo(.5980392f).Within(.025));
            Assert.That(color.g, Is.EqualTo(0).Within(.025));
            Assert.That(color.b, Is.EqualTo(.4980392f).Within(.025));
            Assert.That(color.a, Is.EqualTo(1).Within(.025));
            Assert.That(filter.presentationGroup.alpha, Is.EqualTo(.35f));
            Assert.That(filter.presentation.raycastTarget, Is.False);
            Assert.That(front.raycastTarget && back.raycastTarget, Is.True);
            Assert.That(front.materialForRendering.GetInt("_ColorMask"), Is.Zero);
            filter.enabled = false;
            Assert.That(filter.isPresenting, Is.False);
            Assert.That(filter.texture, Is.Null);
            Assert.That(filter.presentation.gameObject.activeSelf, Is.False);
            Assert.That(front.materialForRendering.GetInt("_ColorMask"), Is.Not.Zero);
            filter.Set(.1f, 0, 0, 0);
            filter.Refresh(Render);
            Assert.That(filter.isPresenting, Is.True);
        }

        [UnityTest]
        public IEnumerator Transition_activates_a_prebaked_disabled_color_filter()
        {
            var source = Source();
            var image = Image(source, Color.white);
            var filter = Filter(source, image);
            filter.enabled = false;
            var transition = source.gameObject.AddComponent<Transition>();
            transition.items = new[] { new TransitionItem { type = TransitionItemType.ColorFilter,
                target = source, component = filter, start = new[] { .2f, .3f, -.4f, .5f } } };
            transition.Play().Forget();
            Assert.That(filter.enabled, Is.True);
            Assert.That(filter.brightness, Is.EqualTo(.2f));
            Assert.That(filter.contrast, Is.EqualTo(.3f));
            Assert.That(filter.saturation, Is.EqualTo(-.4f));
            Assert.That(filter.hue, Is.EqualTo(.5f));
            yield return null;
        }
    }
}
