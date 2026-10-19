using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    // A nested sorting Canvas must retain the authored layer for UI-only cameras.
    public class RootLayerTests
    {
        private readonly List<Object> _owned = new();
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }

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
            Assert.That(Root.inst, Is.Null);
        }

        [UnityTest]
        public IEnumerator World_space_ui_only_camera_renders_the_popup() => Compare(RenderMode.WorldSpace);

        [UnityTest]
        public IEnumerator Screen_space_ui_only_camera_renders_the_popup() => Compare(RenderMode.ScreenSpaceCamera);

        private IEnumerator Compare(RenderMode mode)
        {
            Assert.That(Root.inst, Is.Null, "Run in an empty scene.");
            const int uiLayer = 5;
            var destination = Own(new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear));
            destination.antiAliasing = 1;
            destination.Create();
            var camera = Own(new GameObject("UI camera")).AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 32;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << uiLayer;
            camera.targetTexture = destination;
            var source = Own(new GameObject("UI Canvas", typeof(RectTransform), typeof(Canvas)));
            source.layer = uiLayer;
            var canvas = source.GetComponent<Canvas>();
            canvas.renderMode = mode;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            ((RectTransform)source.transform).sizeDelta = new Vector2(64, 64);
            Picture(source.transform, uiLayer, Color.green);
            var root = Root.Create((RectTransform)source.transform);
            Assert.That(root.gameObject.layer, Is.EqualTo(source.layer));
            var popup = Picture(root.transform, uiLayer, Color.red);
            var closed = root.ShowPopup((RectTransform)popup.transform).SuppressCancellationThrow().AsTask();
            // Restore the authored rectangle after ordinary popup placement.
            Stretch((RectTransform)popup.transform);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Render(camera, destination);
            var after = Read(destination);
            Assert.That(after.r, Is.GreaterThan(.95f));
            Assert.That(after.g, Is.LessThan(.05f));
            Assert.That(after.b, Is.LessThan(.05f));
            root.HidePopup();
            Assert.That(closed.IsCompleted, Is.True);
        }

        private static GameObject Picture(Transform parent, int layer, Color color)
        {
            var go = new GameObject("Picture", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            go.layer = layer;
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            go.GetComponent<UnityEngine.UI.Image>().color = color;
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
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

        private Color Read(RenderTexture source)
        {
            var texture = Own(new Texture2D(1, 1, TextureFormat.RGBA32, false, true));
            var active = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                texture.ReadPixels(new UnityEngine.Rect(32, 32, 1, 1), 0, 0);
                texture.Apply();
                return texture.GetPixel(0, 0);
            }
            finally { RenderTexture.active = active; }
        }
    }
}
