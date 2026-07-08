using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NanamiUI.Tests
{
    // Text 富文本 <a href> 链接命中：点链接触发 onClickLink(href)。用 Overlay 画布（屏幕坐标直接命中）。
    public class TextLinkTests
    {
        [UnityTest]
        public IEnumerator Clicking_link_fires_onClickLink()
        {
            var es = EventSystem.current == null ? new GameObject("ES", typeof(EventSystem)) : null;
            var canvasGo = new GameObject("C", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var raycaster = canvasGo.GetComponent<GraphicRaycaster>();

            var textGo = new GameObject("T", typeof(RectTransform), typeof(CanvasRenderer), typeof(NanamiUI.Text));
            var trt = (RectTransform)textGo.transform;
            trt.SetParent(canvasGo.transform, false);
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0, 1);
            trt.sizeDelta = new Vector2(300, 50);
            trt.anchoredPosition = new Vector2(50, -50);
            var text = textGo.GetComponent<NanamiUI.Text>();
            text.html = true;
            text.fontSize = 24;
            text.text = "<a href='foo'>clickme</a>";
            Canvas.ForceUpdateCanvases(); // 触发 Layout → 解析链接、填 _links
            yield return null;

            string clicked = null;
            text.onClickLink = href => clicked = href;

            // 点链接文本靠左上（layout 坐标 (10,10) 在第一个字符内）
            var screen = RectTransformUtility.WorldToScreenPoint(null, trt.TransformPoint(new Vector3(10, -10, 0)));
            ((IPointerClickHandler)text).OnPointerClick(
                new PointerEventData(EventSystem.current) { position = screen, pointerPressRaycast = new RaycastResult { module = raycaster } });
            yield return null;

            Assert.AreEqual("foo", clicked, "点链接应触发 onClickLink(href)");

            Object.Destroy(canvasGo);
            if (es != null)
                Object.Destroy(es);
        }
    }
}
