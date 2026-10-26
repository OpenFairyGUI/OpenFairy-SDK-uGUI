using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public class SoftClippingTests
    {
        [UnityTest]
        public IEnumerator Soft_clip_uses_native_minimum_axis_and_screen_pixel_ramp_at_each_canvas_scale()
        {
            var cameraGo = new GameObject("Camera", typeof(Camera));
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.layer = 30;
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 32;
            camera.aspect = 1;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 30;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var clip = new GameObject("Clip", typeof(RectTransform), typeof(RectMask2D));
            clip.transform.SetParent(canvasGo.transform, false);
            ((RectTransform)clip.transform).sizeDelta = new Vector2(64, 64);
            clip.GetComponent<RectMask2D>().softness = new Vector2Int(100, 50);
            var content = new GameObject("Image", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            content.layer = 30;
            content.transform.SetParent(clip.transform, false);
            ((RectTransform)content.transform).sizeDelta = new Vector2(64, 64);
            var material = new Material(Shader.Find("OpenFairy/UI Blend"));
            content.GetComponent<UnityEngine.UI.Image>().material = material;
            var target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create();
            camera.targetTexture = target;
            var image = new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true);
            try
            {
                foreach (var scale in new[] { .5f, 1f, 2f })
                {
                    clip.transform.localScale = Vector3.one * scale;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
                    else RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    var previous = RenderTexture.active;
                    RenderTexture.active = target;
                    image.ReadPixels(new UnityEngine.Rect(0, 0, 64, 64), 0, 0);
                    image.Apply();
                    RenderTexture.active = previous;
                    for (var y = 0; y < 64; y++)
                        for (var x = 0; x < 64; x++)
                        {
                            var dx = 32 * scale - Mathf.Abs(x + .5f - 32);
                            var dy = 32 * scale - Mathf.Abs(y + .5f - 32);
                            var expected = Mathf.Clamp01(Mathf.Min(dx / 20, dy / 10));
                            var actual = image.GetPixel(x, y);
                            Assert.That(actual.r, Is.EqualTo(expected).Within(.000001f), $"scale {scale} ({x},{y})");
                            Assert.That(actual.g, Is.EqualTo(expected).Within(.000001f));
                            Assert.That(actual.b, Is.EqualTo(expected).Within(.000001f));
                        }
                }
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(canvasGo);
                Object.DestroyImmediate(cameraGo);
            }
        }
    }
}
