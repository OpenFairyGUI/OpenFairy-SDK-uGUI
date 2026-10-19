using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public class RootSizingTests
    {
        private GameObject _canvas;

        private RectTransform CreateCanvas()
        {
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)_canvas.transform;
            rt.sizeDelta = new Vector2(640, 480);
            return rt;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.Destroy(_canvas);
            yield return null;
            Assert.That(Root.inst, Is.Null);
        }

        private static void SameCorners(RectTransform expected, RectTransform actual)
        {
            var first = new Vector3[4];
            var second = new Vector3[4];
            expected.GetWorldCorners(first);
            actual.GetWorldCorners(second);
            for (int i = 0; i < 4; i++) Assert.That(Vector3.Distance(first[i], second[i]), Is.LessThan(.001f));
        }

        [UnityTest]
        public IEnumerator Canvas_resize_uses_native_stretch_and_keeps_the_overlay_top_left_origin()
        {
            var canvas = CreateCanvas();
            var root = Root.Create(canvas);
            SameCorners(canvas, root.rect);
            Assert.That(root.rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(root.rect.anchorMax, Is.EqualTo(Vector2.one));
            canvas.sizeDelta = new Vector2(900, 700);
            UnityEngine.Canvas.ForceUpdateCanvases();
            yield return null;
            SameCorners(canvas, root.rect);
            Assert.That(root.size, Is.EqualTo(new Vector2(900, 700)));
            Assert.That(root.RootTopLeft(canvas).magnitude, Is.LessThan(.001f));
            Assert.That(root.GetComponent<LayoutElement>().ignoreLayout, Is.True);
            Assert.That(canvas.GetComponent<RootSizeBinding>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator Design_resize_keeps_a_sibling_overlay_outside_its_mask_and_opacity()
        {
            var canvas = CreateCanvas();
            var design = (RectTransform)new GameObject("Design", typeof(RectTransform), typeof(RectMask2D), typeof(CanvasGroup)).transform;
            design.SetParent(canvas, false);
            design.anchorMin = new Vector2(.1f, .2f);
            design.anchorMax = new Vector2(.8f, .9f);
            design.pivot = new Vector2(.3f, .6f);
            design.sizeDelta = new Vector2(100, 80);
            design.anchoredPosition = new Vector2(15, 20);
            design.localScale = new Vector3(1.2f, .8f, 1);
            design.localRotation = Quaternion.Euler(0, 0, 12);
            design.GetComponent<CanvasGroup>().alpha = .25f;
            var root = Root.Create(design);
            SameCorners(design, root.rect);
            Assert.That(root.transform.parent, Is.SameAs(canvas));
            Assert.That(root.GetComponentInParent<RectMask2D>(), Is.Null);
            Assert.That(root.GetComponentInParent<CanvasGroup>(), Is.Null);
            design.sizeDelta = new Vector2(200, 120);
            yield return null;
            SameCorners(design, root.rect);
            canvas.sizeDelta = new Vector2(800, 600);
            // Observe the pose at the rendered-frame boundary after LateUpdate.
            yield return new WaitForEndOfFrame();
            SameCorners(design, root.rect);
            design.localRotation = Quaternion.Euler(0, 0, -9);
            design.localScale = new Vector3(.9f, 1.1f, 1);
            design.anchoredPosition += new Vector2(25, -10);
            yield return new WaitForEndOfFrame();
            SameCorners(design, root.rect);
            Assert.That(Root.Create(design), Is.SameAs(root));
            Assert.That(design.GetComponents<RootSizeBinding>().Length, Is.EqualTo(1));
            Object.Destroy(root.gameObject);
            yield return null;
            Assert.That(Root.inst, Is.Null);
            design.sizeDelta = new Vector2(120, 160);
            var recreated = Root.Create(design);
            SameCorners(design, recreated.rect);
            Assert.That(design.GetComponents<RootSizeBinding>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Native_modal_blocker_and_popup_placement_follow_the_resized_root()
        {
            var canvas = CreateCanvas();
            var root = Root.Create(canvas);
            var window = new GameObject("Modal", typeof(RectTransform)).AddComponent<PopupTestWindow>();
            window.modal = true;
            window.Show();
            var popup = (RectTransform)new GameObject("Popup", typeof(RectTransform)).transform;
            popup.sizeDelta = new Vector2(120, 60);
            var closed = root.ShowPopupAt(popup, new Vector2(600, 450));
            Assert.That(-popup.anchoredPosition.y, Is.EqualTo(450), "Explicit Down retains the requested direction, including overflow.");
            var blocker = (RectTransform)root.transform.Find("ModalLayer");
            SameCorners(root.rect, blocker);
            root.HidePopup();
            Assert.That(closed.Status, Is.EqualTo(Cysharp.Threading.Tasks.UniTaskStatus.Succeeded));
            canvas.sizeDelta = new Vector2(400, 300);
            yield return null;
            SameCorners(root.rect, blocker);
            root.ShowPopupAt(popup, new Vector2(390, 280), PopupDirection.Auto).Forget();
            Assert.That(popup.anchoredPosition.x, Is.LessThanOrEqualTo(400 - popup.rect.width));
            Assert.That(-popup.anchoredPosition.y, Is.LessThanOrEqualTo(300 - popup.rect.height));
            root.HidePopup();
            window.HideImmediately();
        }
    }
}
