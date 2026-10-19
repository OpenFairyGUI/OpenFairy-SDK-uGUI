using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public class ColorFilterClippingTests
    {
        public enum MaskKind { HardRect, SoftRect, Stencil }
        private static readonly Color Background = new(.07f, .12f, .18f, 1);

        [Serializable]
        public sealed class Observation
        {
            public string mask;
            public int frame;
            public bool isPlaying, raycastsUnchanged;
            // Each lane records its inside, outside and soft-edge pixel in that order.
            public Color[] plain, production, unmasked;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position = default)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.layer = 30;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void Render(Camera camera, RenderTexture target)
        {
            if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
            else
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
        }

        // Synchronous diagnostic: no business scene, network, font or asynchronous frame dependency.
        public static Observation Observe(MaskKind kind, string outputDirectory = null)
        {
            var root = Rect("Clipping fixture", null, new Vector2(288, 128));
            root.position = new Vector3(10000, 10000, 0);
            var cameraObject = new GameObject("Clipping camera", typeof(Camera));
            var target = new RenderTexture(288, 128, 24, RenderTextureFormat.ARGB32);
            Texture2D read = null;
            var active = RenderTexture.active;
            try
            {
                var canvas = root.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = root.position + new Vector3(0, 0, -100);
                camera.orthographic = true;
                camera.orthographicSize = 64;
                camera.aspect = 2.25f;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 1000;
                camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Background;
                camera.allowHDR = camera.allowMSAA = false;
                canvas.worldCamera = camera;
                target.Create();
                camera.targetTexture = target;
                var images = new UnityEngine.UI.Image[3];
                for (var lane = 0; lane < images.Length; lane++)
                {
                    var mask = Rect("Mask", root, new Vector2(48, 64), new Vector2(-96 + 96 * lane, 0));
                    if (kind == MaskKind.Stencil)
                    {
                        mask.gameObject.AddComponent<UnityEngine.UI.Image>();
                        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                    }
                    else
                    {
                        var clip = mask.gameObject.AddComponent<RectMask2D>();
                        if (kind == MaskKind.SoftRect) clip.softness = new Vector2Int(8, 8);
                    }
                    var image = Rect("Image", mask, new Vector2(64, 100), new Vector2(0, 12))
                        .gameObject.AddComponent<UnityEngine.UI.Image>();
                    image.color = new Color(.3f, .6f, .9f, .75f);
                    images[lane] = image;
                    if (lane == 2) { image.maskable = false; image.RecalculateClipping(); }
                    if (lane == 0) continue;
                    var filter = image.gameObject.AddComponent<ColorAdjust>();
                    filter.shader = Shader.Find("OpenFairy/UI ColorMatrix");
                    Assert.That(filter.shader, Is.Not.Null);
                    filter.graphic = image;
                    filter.Set(.1f, 0, 0, 0);
                }
                Canvas.ForceUpdateCanvases();
                Render(camera, target);
                read = new Texture2D(288, 128, TextureFormat.RGBA32, false, true);
                RenderTexture.active = target;
                read.ReadPixels(new UnityEngine.Rect(0, 0, 288, 128), 0, 0);
                read.Apply();
                Color[] Samples(int lane) => new[] { read.GetPixel(48 + 96 * lane, 64),
                    read.GetPixel(48 + 96 * lane, 110), read.GetPixel(48 + 96 * lane, 94) };
                if (outputDirectory != null)
                {
                    System.IO.Directory.CreateDirectory(outputDirectory);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(outputDirectory, kind + ".png"), read.EncodeToPNG());
                }
                return new Observation { mask = kind.ToString(), frame = Time.frameCount, isPlaying = Application.isPlaying,
                    raycastsUnchanged = Array.TrueForAll(images, image => image.raycastTarget),
                    plain = Samples(0), production = Samples(1), unmasked = Samples(2) };
            }
            finally
            {
                RenderTexture.active = active;
                Object.DestroyImmediate(root.gameObject);
                Object.DestroyImmediate(cameraObject);
                target.Release();
                Object.DestroyImmediate(target);
                if (read != null) Object.DestroyImmediate(read);
            }
        }

        private static float Difference(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r - b.r),
            Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b), Mathf.Abs(a.a - b.a));

        private static void Verify(MaskKind kind)
        {
            var result = Observe(kind);
            Assert.That(result.raycastsUnchanged, Is.True);
            Assert.That(Difference(result.plain[1], Background), Is.LessThan(.015f), "Native Image positive control must clip.");
            Assert.That(Difference(result.unmasked[1], Background), Is.GreaterThan(.1f), "Disabling native maskability must expose the image at the same outside sample.");
            Assert.That(Difference(result.production[1], Background), Is.LessThan(.015f));
            Assert.That(Difference(result.production[0], Background), Is.GreaterThan(.1f));
            Assert.That(Difference(result.production[0], result.unmasked[0]), Is.LessThan(.015f));
            Assert.That(result.production[0].r - result.plain[0].r, Is.GreaterThan(.03f), "The clipped production lane must actually apply its color adjustment.");
            if (kind == MaskKind.SoftRect)
            {
                Assert.That(Difference(result.production[2], Background), Is.GreaterThan(.01f));
                Assert.That(Difference(result.production[2], Background), Is.LessThan(Difference(result.production[0], Background) - .01f));
            }
        }

        [UnityTest] public IEnumerator Direct_image_respects_hard_rect_clipping() { Verify(MaskKind.HardRect); yield return null; }
        [UnityTest] public IEnumerator Direct_image_respects_soft_rect_clipping() { Verify(MaskKind.SoftRect); yield return null; }
        [UnityTest] public IEnumerator Direct_image_respects_stencil_clipping() { Verify(MaskKind.Stencil); yield return null; }

        [UnityTest]
        public IEnumerator Explicit_pma_blit_retains_alpha_zero_emission_and_coverage_weighted_offset()
        {
            var shader = Shader.Find("OpenFairy/UI ColorMatrix");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var input = new Texture2D(2, 1, TextureFormat.RGBA32, false, true);
            var output = new RenderTexture(2, 1, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D read = null;
            var active = RenderTexture.active;
            try
            {
                material.SetMatrix("_ColorMatrix", Matrix4x4.identity);
                material.SetVector("_ColorOffset", new Vector4(.3f, .3f, .3f, 0));
                input.SetPixel(0, 0, new Color(.2f, .1f, .05f, 0));
                input.SetPixel(1, 0, new Color(.2f, .1f, .05f, .5f)); input.Apply();
                output.Create();
                Graphics.Blit(input, output, material, 1);
                RenderTexture.active = output;
                read = new Texture2D(2, 1, TextureFormat.RGBA32, false, true);
                read.ReadPixels(new UnityEngine.Rect(0, 0, 2, 1), 0, 0); read.Apply();
                Assert.That(Difference(read.GetPixel(0, 0), input.GetPixel(0, 0)), Is.LessThan(.01f));
                var second = input.GetPixel(1, 0);
                var expected = second + new Color(.3f, .3f, .3f, 0) * second.a;
                Assert.That(Difference(read.GetPixel(1, 0), expected), Is.LessThan(.01f));
            }
            finally
            {
                RenderTexture.active = active;
                output.Release();
                Object.DestroyImmediate(output); Object.DestroyImmediate(input); Object.DestroyImmediate(material);
                if (read != null) Object.DestroyImmediate(read);
            }
            yield return null;
        }
    }
}
