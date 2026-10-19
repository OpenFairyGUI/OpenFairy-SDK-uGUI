using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public sealed class HiddenListClipTests
    {
        private readonly List<Object> owned = new();
        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

        private static void Render(Camera camera, RenderTexture texture)
        {
            if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
            else
            {
                var request = new RenderPipeline.StandardRequest { destination = texture };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
        }

        private Texture2D Draw(Camera camera, RenderTexture texture)
        {
            Canvas.ForceUpdateCanvases();
            Render(camera, texture);
            var read = Own(new Texture2D(128, 128, TextureFormat.RGBA32, false, true));
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = texture;
                read.ReadPixels(new UnityEngine.Rect(0, 0, 128, 128), 0, 0);
                read.Apply();
                return read;
            }
            finally { RenderTexture.active = previous; }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var value in owned)
            {
                if (value is RenderTexture texture) texture.Release();
                Object.Destroy(value);
            }
            owned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Native_root_mask_clips_filled_rows_and_tracks_a_collapsed_section()
        {
            var canvasObject = Own(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.layer = 30;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(128, 128);
            var camera = Own(new GameObject("UI Camera", typeof(Camera))).GetComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -100);
            camera.orthographic = true;
            camera.orthographicSize = 64;
            camera.aspect = 1;
            camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = camera.allowMSAA = false;
            canvas.worldCamera = camera;
            var texture = Own(new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32));
            texture.Create();
            camera.targetTexture = texture;
            var list = (RectTransform)new GameObject("List", typeof(RectTransform), typeof(RectMask2D), typeof(ListSource)).transform;
            list.gameObject.layer = 30;
            list.SetParent(canvas.transform, false);
            list.pivot = new Vector2(0, 1);
            list.anchoredPosition = new Vector2(-32, 16);
            list.sizeDelta = new Vector2(64, 32);
            var mask = list.GetComponent<RectMask2D>();
            var template = Own(new GameObject("Item", typeof(RectTransform), typeof(UnityEngine.UI.Image)));
            template.layer = 30;
            ((RectTransform)template.transform).sizeDelta = new Vector2(64, 32);
            template.GetComponent<UnityEngine.UI.Image>().color = Color.green;
            template.SetActive(false);
            var source = list.GetComponent<ListSource>();
            source.content = list;
            source.itemPrefab = template;
            source.itemSize = new Vector2(64, 32);
            source.layout = ListLayoutType.FlowHorizontal;
            source.alignment = TextAnchor.UpperLeft;
            source.Fill(2, (_, _) => { }, false);
            yield return null;
            yield return null;
            Assert.That(source.content, Is.SameAs(list));
            Assert.That(source.scrollPane, Is.Null);
            Assert.That(source.isVirtual, Is.False);
            Assert.That(((RectTransform)list.GetChild(1)).anchoredPosition, Is.EqualTo(new Vector2(0, -32)));
            var clipped = Draw(camera, texture);
            Assert.That(clipped.GetPixel(64, 64).g, Is.GreaterThan(.5f), "The first row is visibly rendered inside the mask.");
            Assert.That(clipped.GetPixel(64, 32).g, Is.LessThan(.02f), "The second row must not render outside the list root.");
            Assert.That(mask.IsRaycastLocationValid(camera.WorldToScreenPoint(new Vector3(0, 0)), camera), Is.True);
            Assert.That(mask.IsRaycastLocationValid(camera.WorldToScreenPoint(new Vector3(0, -32)), camera), Is.False);

            mask.enabled = false;
            yield return null;
            var visible = Draw(camera, texture);
            Assert.That(visible.GetPixel(64, 32).g, Is.GreaterThan(.5f), "Positive control: the second row exists and is visible without clipping.");
            mask.enabled = true;
            list.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0);
            yield return null;
            yield return null;
            var collapsed = Draw(camera, texture);
            Assert.That(collapsed.GetPixel(64, 64).g, Is.LessThan(.02f));
            Assert.That(collapsed.GetPixel(64, 32).g, Is.LessThan(.02f));
            Assert.That(((RectTransform)list.GetChild(1)).anchoredPosition, Is.EqualTo(new Vector2(0, -32)), "Clipping must not relayout or resize list items.");
            list.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 32);
            yield return null;
            var restored = Draw(camera, texture);
            Assert.That(restored.GetPixel(64, 64).g, Is.GreaterThan(.5f));
            Assert.That(restored.GetPixel(64, 32).g, Is.LessThan(.02f));
        }
    }
}
