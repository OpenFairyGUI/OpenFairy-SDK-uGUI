using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public class AutomaticFilterTests
    {
        private readonly List<Object> _owned = new();
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }

        private (RectTransform Root, Camera Camera, RenderTexture Target) Canvas()
        {
            var go = Own(new GameObject("Canvas", typeof(RectTransform), typeof(UnityEngine.Canvas), typeof(GraphicRaycaster)));
            go.layer = 30;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(128, 128);
            var camera = Own(new GameObject("UI Camera", typeof(Camera))).GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -100);
            camera.orthographic = true;
            camera.orthographicSize = 64;
            camera.aspect = 1;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 1000;
            camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = camera.allowMSAA = false;
            var target = Own(new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear));
            target.Create();
            camera.targetTexture = target;
            var canvas = go.GetComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            return (rect, camera, target);
        }

        private static RectTransform Rect(Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return rect;
        }

        private static UnityEngine.UI.Image Image(Transform parent, Vector2 size, Color color)
        {
            var image = Rect(parent, size).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            return image;
        }

        private static IEnumerator Captured(AutomaticFilterProbe filter, int previous = 0)
        {
            for (int i = 0; i < 10 && filter.frames.Count <= previous; i++) yield return null;
            Assert.That(filter.frames.Count, Is.GreaterThan(previous), "A native Play frame must automatically capture the filter.");
        }

        private static void Render(Camera camera, RenderTexture target)
        {
            if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
            else RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
        }

        private Texture2D Draw(Camera camera, RenderTexture target)
        {
            UnityEngine.Canvas.ForceUpdateCanvases();
            Render(camera, target);
            return Read(target);
        }

        private Texture2D Read(RenderTexture target)
        {
            var image = Own(new Texture2D(128, 128, TextureFormat.RGBA32, false, true));
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new UnityEngine.Rect(0, 0, 128, 128), 0, 0);
                image.Apply();
                return image;
            }
            finally { RenderTexture.active = previous; }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var obj in _owned)
            {
                if (obj is GameObject go && go.TryGetComponent(out Camera camera)) camera.enabled = false;
                if (obj is RenderTexture target) target.Release();
                Object.Destroy(obj);
            }
            _owned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LateUpdate_change_is_captured_after_the_same_frames_relation_sync()
        {
            var scene = Canvas();
            var reference = Rect(scene.Root, new Vector2(48, 48));
            var filter = AutomaticFilterProbe.Create(scene.Root, new Vector2(48, 48));
            var image = Image(filter.transform, new Vector2(128, 128), Color.red);
            var relation = filter.gameObject.AddComponent<Relation>();
            relation.reference = scene.Root;
            relation.coordinateSpace = scene.Root;
            relation.bindings = new[] { new RelationBinding { target = reference,
                rules = new[] { new RelationRule { side = RelationSide.Width } } } };
            relation.enabled = false;
            relation.enabled = true;
            yield return Captured(filter);
            var change = reference.gameObject.AddComponent<AutomaticFilterChange>();
            change.reference = reference;
            change.image = image;
            change.pending = true;
            yield return null;
            yield return null;
            Assert.That(change.appliedFrame, Is.GreaterThan(0));
            int captured = filter.frames.IndexOf(change.appliedFrame);
            Assert.That(captured, Is.GreaterThanOrEqualTo(0), "Capture must follow LateUpdate and PostLateUpdate in that frame.");
            Assert.That(filter.sizes[captured].x, Is.EqualTo(72));
            Assert.That(filter.pixel.b, Is.GreaterThan(.98f));
            Assert.That(filter.pixel.r, Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator Native_layout_completes_before_capture_dimensions_are_selected()
        {
            var scene = Canvas();
            var filter = AutomaticFilterProbe.Create(scene.Root, new Vector2(48, 48));
            var layout = filter.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = filter.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var image = Image(filter.transform, new Vector2(48, 48), Color.green);
            var item = image.gameObject.AddComponent<LayoutElement>();
            item.preferredWidth = item.preferredHeight = 48;
            yield return Captured(filter);
            int previous = filter.frames.Count;
            int changedFrame = Time.frameCount;
            item.preferredWidth = 96;
            yield return Captured(filter, previous);
            int index = filter.frames.FindIndex(frame => frame >= changedFrame);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That(filter.sizes[index], Is.EqualTo(new Vector2(96, 48)));
            Assert.That(filter.texture.width, Is.EqualTo(96));
            Assert.That(filter.pixel.g, Is.GreaterThan(.98f));
        }

        [UnityTest]
        public IEnumerator Multiple_canvases_capture_nested_filters_once_in_inside_out_order()
        {
            var a = Canvas();
            var b = Canvas();
            b.Root.position = new Vector3(300, 0, 0);
            b.Camera.transform.position += new Vector3(300, 0, 0);
            var outer = AutomaticFilterProbe.Create(a.Root, new Vector2(96, 96));
            var inner = AutomaticFilterProbe.Create(outer.transform, new Vector2(48, 48));
            Image(inner.transform, new Vector2(48, 48), Color.blue);
            var sibling = AutomaticFilterProbe.Create(b.Root, new Vector2(48, 48));
            Image(sibling.transform, new Vector2(48, 48), Color.green);
            var trace = new List<(AutomaticFilterProbe Filter, int Frame)>();
            foreach (var filter in new[] { outer, inner, sibling })
                filter.processed = item => trace.Add((item, Time.frameCount));
            yield return Captured(outer);
            yield return Captured(sibling);
            int frame = outer.frames[outer.frames.Count - 1];
            int innerAt = trace.FindIndex(entry => entry.Frame == frame && entry.Filter == inner);
            int outerAt = trace.FindIndex(entry => entry.Frame == frame && entry.Filter == outer);
            Assert.That(innerAt, Is.GreaterThanOrEqualTo(0));
            Assert.That(outerAt, Is.GreaterThan(innerAt));
            foreach (var filter in new[] { outer, inner, sibling })
                Assert.That(filter.frames.FindAll(value => value == frame).Count, Is.EqualTo(1));
            Assert.That(outer.pixel.b, Is.GreaterThan(.98f));
            Assert.That(sibling.pixel.g, Is.GreaterThan(.98f));
            int count = outer.frames.Count;
            Draw(a.Camera, a.Target);
            Draw(b.Camera, b.Target);
            Assert.That(outer.frames.Count, Is.EqualTo(count), "Explicit camera rendering must not recurse into automatic capture.");
        }

        [UnityTest]
        public IEnumerator Direct_material_ancestor_still_drives_its_captured_descendant()
        {
            var scene = Canvas();
            var host = Image(scene.Root, new Vector2(96, 96), Color.red);
            var color = host.gameObject.AddComponent<ColorAdjust>();
            color.graphic = host;
            color.shader = Shader.Find("OpenFairy/UI ColorMatrix");
            var filter = AutomaticFilterProbe.Create(host.transform, new Vector2(48, 48));
            Image(filter.transform, new Vector2(48, 48), Color.blue);
            yield return Captured(filter);
            int frame = filter.frames[filter.frames.Count - 1];
            Assert.That(filter.frames.FindAll(value => value == frame).Count, Is.EqualTo(1));
            Assert.That(color.isPresenting, Is.False);
            Assert.That(filter.pixel.b, Is.GreaterThan(.98f));
        }

        [UnityTest]
        public IEnumerator Automatic_presentation_keeps_parent_alpha_mask_and_source_raycast()
        {
            var scene = Canvas();
            var mask = Rect(scene.Root, new Vector2(60, 60));
            mask.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(4, 4);
            mask.gameObject.AddComponent<CanvasGroup>().alpha = .5f;
            var filter = AutomaticFilterProbe.Create(mask, new Vector2(96, 96));
            filter.gameObject.AddComponent<CanvasGroup>().alpha = .5f;
            var image = Image(filter.transform, new Vector2(96, 96), Color.red);
            yield return Captured(filter);
            // Read the preceding native camera frame without forcing layout or requesting another render.
            var pixels = Read(scene.Target);
            var center = pixels.GetPixel(64, 64);
            Assert.That(center.a, Is.EqualTo(.25f).Within(.025f));
            Assert.That(center.r, Is.EqualTo(center.a).Within(.02f));
            Assert.That(pixels.GetPixel(96, 64).a, Is.LessThan(.01f));
            Assert.That(image.enabled && image.raycastTarget, Is.True);
            Assert.That(image.canvasRenderer.cull, Is.False);
            Assert.That(filter.presentation.raycastTarget, Is.False);
            var events = Own(new GameObject("Events", typeof(EventSystem))).GetComponent<EventSystem>();
            var pointer = new PointerEventData(events) { position = scene.Camera.WorldToScreenPoint(image.transform.position) };
            var hits = new List<RaycastResult>();
            scene.Root.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits.Exists(hit => hit.gameObject == image.gameObject), Is.True);
        }

        [UnityTest]
        public IEnumerator Disabled_ancestor_reparent_and_destroy_preserve_automatic_root_lifecycle()
        {
            var scene = Canvas();
            var outer = AutomaticFilterProbe.Create(scene.Root, new Vector2(96, 96));
            var inner = AutomaticFilterProbe.Create(outer.transform, new Vector2(48, 48));
            var image = Image(inner.transform, new Vector2(48, 48), Color.red);
            yield return Captured(outer);
            int before = inner.frames.Count;
            outer.enabled = false;
            image.color = Color.blue;
            yield return Captured(inner, before);
            Assert.That(outer.texture, Is.Null);
            Assert.That(inner.pixel.b, Is.GreaterThan(.98f));
            // The baked source/presentation pair keeps its documented shared-parent contract.
            inner.transform.SetParent(scene.Root, false);
            inner.presentation.transform.SetParent(scene.Root, false);
            before = inner.frames.Count;
            outer.enabled = true;
            yield return Captured(inner, before);
            int frame = inner.frames[inner.frames.Count - 1];
            Assert.That(inner.frames.FindAll(value => value == frame).Count, Is.EqualTo(1));
            Object.Destroy(outer.gameObject);
            yield return null;
            inner.enabled = false;
            int stopped = inner.frames.Count;
            yield return null;
            yield return null;
            Assert.That(inner.frames.Count, Is.EqualTo(stopped));
            Assert.That(inner.texture, Is.Null);
            inner.enabled = true;
            yield return Captured(inner, stopped);
            Assert.That(inner.isPresenting, Is.True);
        }

        [UnityTest]
        public IEnumerator Public_same_frame_refresh_is_not_coalesced_by_automatic_updates()
        {
            var scene = Canvas();
            var filter = AutomaticFilterProbe.Create(scene.Root, new Vector2(48, 48));
            var image = Image(filter.transform, new Vector2(48, 48), Color.red);
            yield return Captured(filter);
            int count = filter.frames.Count;
            filter.Refresh(Render);
            image.color = Color.blue;
            filter.Refresh(Render);
            Assert.That(filter.frames.Count, Is.EqualTo(count + 2));
            Assert.That(filter.pixel.b, Is.GreaterThan(.98f));
        }

        [UnityTest]
        public IEnumerator Native_scrolling_and_live_transition_update_the_filtered_pixels_without_refresh_calls()
        {
            var scene = Canvas();
            var filter = AutomaticFilterProbe.Create(scene.Root, new Vector2(64, 64));
            filter.gameObject.AddComponent<RectMask2D>();
            var content = Rect(filter.transform, new Vector2(128, 64));
            content.anchoredPosition = new Vector2(32, 0);
            var left = Image(content, new Vector2(64, 64), Color.red);
            left.rectTransform.anchoredPosition = new Vector2(-32, 0);
            var right = Image(content, new Vector2(64, 64), Color.blue);
            right.rectTransform.anchoredPosition = new Vector2(32, 0);
            var scroll = filter.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)filter.transform;
            scroll.content = content;
            scroll.vertical = false;
            scroll.inertia = false;
            yield return Captured(filter);
            Assert.That(filter.pixel.r, Is.GreaterThan(.98f));
            int count = filter.frames.Count;
            scroll.horizontalNormalizedPosition = 1;
            yield return Captured(filter, count);
            Assert.That(filter.pixel.b, Is.GreaterThan(.98f));
            var transition = filter.gameObject.AddComponent<Transition>();
            transition.items = new[] { new TransitionItem { type = TransitionItemType.Color,
                target = right.rectTransform, component = right, tween = true, duration = .05f,
                start = new[] { 0f, 0f, 1f, 1f }, end = new[] { 0f, 1f, 0f, 1f } } };
            var completion = transition.Play();
            for (int i = 0; i < 120 && transition.playing; i++) yield return null;
            Assert.That(transition.playing, Is.False);
            completion.GetAwaiter().GetResult();
            count = filter.frames.Count;
            yield return Captured(filter, count);
            Assert.That(filter.pixel.g, Is.GreaterThan(.98f));
            Assert.That(filter.pixel.b, Is.LessThan(.01f));
        }
    }
}
