#if FAIRYGUI_TMPRO && UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    // FairyGUI strokes extend each glyph outward by the authored width without covering its face.
    public class TmpStrokeTests
    {
        private const string Path = "Assets/__TmpStroke.mat";

        [UnityTest]
        public IEnumerator Baked_stroke_extends_the_glyph_by_the_authored_width()
        {
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var target = new RenderTexture(256, 128, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            try
            {
                camera.orthographic = true; camera.orthographicSize = 64; camera.aspect = 2;
                camera.transform.position = new Vector3(128, -64, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.cullingMask = 1 << 30;
                canvas.layer = 30;
                var rect = (RectTransform)canvas.transform;
                rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(256, 128);
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                canvas.GetComponent<Canvas>().worldCamera = camera;
                var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)) { layer = 30 };
                go.transform.SetParent(rect, false);
                var text = go.GetComponent<TextMeshProUGUI>();
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0, 1);
                text.rectTransform.sizeDelta = new Vector2(180, 70); text.rectTransform.anchoredPosition = new Vector2(30, -25);
                text.font = TMP_Settings.defaultFontAsset; text.fontSize = 50; text.color = Color.red; text.extraPadding = true;
                text.text = "I";
                target.Create();
                yield return null;
                var (face, _, firstRow, endRow) = Extents(text, camera, target);
                var builder = Type.GetType("OpenFairy.UGUI.Editor.TmpTextMaterialBuilder, OpenFairy.UGUI.Editor", true);
                foreach (var size in new[] { 2f, 4f })
                {
                    text.fontSharedMaterial = (Material)builder.GetMethod("BakeStroke").Invoke(null,
                        new object[] { text.font.material, text.font, 50f, Path, Color.black, size });
                    yield return null;
                    var (strokedFace, stroke, _, _) = Extents(text, camera, target, firstRow, endRow);
                    Assert.That(strokedFace + stroke - face, Is.EqualTo(size).Within(.5f), "Outward extent per side");
                    Assert.That(strokedFace, Is.EqualTo(face).Within(.75f), "The stroke must not cover the face.");
                    AssetDatabase.DeleteAsset(Path);
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(Path);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(canvas);
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        // Half the face and stroke coverage across the middle rows of a vertical stem, in pixels per side.
        private static (float Face, float Stroke, int FirstRow, int EndRow) Extents(TextMeshProUGUI text, Camera camera, RenderTexture target, int firstRow = 0, int endRow = 0)
        {
            text.UpdateMeshPadding(); text.ForceMeshUpdate(); Canvas.ForceUpdateCanvases();
            camera.targetTexture = target;
            if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
            else RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            camera.targetTexture = null;
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(256, 128, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 256, 128), 0, 0); image.Apply(); RenderTexture.active = previous;
            if (endRow == 0)
            {
                var first = image.height;
                var last = -1;
                for (var y = 0; y < image.height; y++)
                    for (var x = 0; x < image.width; x++)
                    {
                        var pixel = image.GetPixel(x, y);
                        if (pixel.r <= pixel.g) continue;
                        first = Mathf.Min(first, y); last = Mathf.Max(last, y);
                    }
                Assert.That(last, Is.GreaterThan(first), "The unstroked glyph must be visible.");
                // Different font baselines move the glyph vertically. Reuse its central rows so
                // expanded caps are never mistaken for a change in the stem's sideways extent.
                var margin = (last - first + 1) / 4;
                firstRow = first + margin; endRow = last + 1 - margin;
            }
            float face = 0, stroke = 0;
            for (var y = firstRow; y < endRow; y++)
                for (var x = 0; x < 256; x++)
                {
                    var pixel = image.GetPixel(x, y);
                    face += Mathf.Clamp01(pixel.r - pixel.g);
                    stroke += Mathf.Clamp01(1 - pixel.r);
                }
            Object.DestroyImmediate(image);
            var divisor = 2f * (endRow - firstRow);
            return (face / divisor, stroke / divisor, firstRow, endRow);
        }
    }
}
#endif
