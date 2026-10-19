#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public sealed class FilterHierarchyTests
    {
        [UnityTest]
        public IEnumerator Imported_group_mask_scroll_pairs_render_and_keep_the_strict_runtime_parent_contract()
        {
            var type = Type.GetType("OpenFairy.UGUI.Tests.FilterHierarchyFixture, OpenFairy.UGUI.Tests.Editor", true);
            for (var mode = 0; mode < 3; mode++)
            {
                using var fixture = (IDisposable)Activator.CreateInstance(type, new object[] { mode });
                var path = (string)type.GetProperty("PrefabPath").GetValue(fixture);
                var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
                var cameraObject = new GameObject("Camera", typeof(Camera));
                var target = new RenderTexture(80, 80, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                var image = new Texture2D(80, 80, TextureFormat.RGBA32, false);
                try
                {
                    var camera = cameraObject.GetComponent<Camera>();
                    camera.orthographic = true; camera.orthographicSize = 40; camera.aspect = 1;
                    camera.transform.position = new Vector3(40, -40, -10);
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                    camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.cullingMask = 1 << 30;
                    canvasObject.layer = 30;
                    var rect = (RectTransform)canvasObject.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(80, 80);
                    var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                    var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), rect, false);
                    var panel = instance.GetComponent<FilterHierarchyTestPanel>();
                    foreach (var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
                    var source = panel.m_Source.GetComponent<ColorAdjust>();
                    var presentation = source.presentation;
                    Assert.That(presentation.transform.parent, Is.SameAs(source.transform.parent));
                    yield return null;
                    void Render(Camera c, RenderTexture destination)
                    {
                        if (GraphicsSettings.currentRenderPipeline == null) c.Render();
                        else RenderPipeline.SubmitRenderRequest(c, new RenderPipeline.StandardRequest { destination = destination });
                    }
                    Canvas.ForceUpdateCanvases();
                    foreach (var filter in instance.GetComponentsInChildren<ObjectFilter>(true)) filter.Refresh(Render);
                    Assert.That(source.isPresenting, Is.True);
                    target.Create(); camera.targetTexture = target; Render(camera, target); camera.targetTexture = null;
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 80, 80), 0, 0); image.Apply(); RenderTexture.active = previous;
                    var pixel = image.GetPixel(40, 40);
                    Assert.That(pixel.a, Is.GreaterThan(.9f), "The pre-baked presentation must reach the final camera.");
                    Assert.That(pixel.r, Is.InRange(.15f, .5f));
                    Assert.That(pixel.r, Is.EqualTo(pixel.g).Within(.02f));
                    Assert.That(pixel.g, Is.EqualTo(pixel.b).Within(.02f));
                    var folder = Path.Combine(Path.GetTempPath(), "OpenFairyFilterHierarchy"); Directory.CreateDirectory(folder);
                    File.WriteAllBytes(Path.Combine(folder, "mode" + mode + ".png"), image.EncodeToPNG());
                    var correctParent = presentation.transform.parent;
                    presentation.transform.SetParent(canvasObject.transform, true);
                    Assert.Throws<InvalidOperationException>(() => source.Refresh(Render));
                    presentation.transform.SetParent(correctParent, true);
                    source.Refresh(Render);
                    Assert.That(source.isPresenting, Is.True);
                    Assert.That(panel.m_Direct.GetComponent<ColorAdjust>().presentation, Is.Null);
                    Debug.Log("FILTER_HIERARCHY mode=" + mode + " pixel=" + pixel);
                }
                finally
                {
                    Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(cameraObject);
                    target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                }
            }
        }
    }
}
#endif
