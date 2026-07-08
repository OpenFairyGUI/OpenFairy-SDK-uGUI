using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NanamiUI
{
    // 运行时拖动滚动（复刻 FairyGUI ScrollPane 的拖拽部分）：把 viewport（RectMask2D）里的内容包进一个 content 容器，
    // 拖动时在内容边界内平移 content，并按滚动比例移动滚动条 grip。自配置：Attach 扫描已烘焙的 viewport 结构。
    public sealed class ScrollPane : UIBehaviour, IBeginDragHandler, IDragHandler
    {
        private RectTransform _viewport;
        private RectTransform _content;
        private RectTransform _vtBar, _vtGrip, _hzBar, _hzGrip;
        private Vector2 _contentSize, _viewSize;
        private Vector2 _startContent, _startPointer;

        // 给已烘焙的滚动根（有名为 "viewport" 的 RectMask2D 子节点）挂上运行时滚动。返回 null 表示不是滚动结构。
        public static ScrollPane Attach(RectTransform scrollRoot)
        {
            var viewportTf = scrollRoot.Find("viewport");
            if (viewportTf == null || viewportTf.GetComponent<RectMask2D>() == null)
                return null;
            var viewport = (RectTransform)viewportTf;

            // 把 viewport 的现有子节点包进 content 容器（原点与 viewport 一致，故 anchoredPosition 不变）。
            var content = new GameObject("content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            content.anchoredPosition = Vector2.zero;
            for (var i = viewport.childCount - 1; i >= 0; i--)
                if (viewport.GetChild(i) != content)
                    viewport.GetChild(i).SetParent(content, false);

            var pane = scrollRoot.gameObject.AddComponent<ScrollPane>();
            pane._viewport = viewport;
            pane._content = content;
            pane._viewSize = viewport.rect.size;
            pane._contentSize = ContentBounds(content);
            content.sizeDelta = pane._contentSize;

            // 透明射线面覆盖 viewport，令空白处也能起拖。
            var blocker = new GameObject("scrollHit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var brt = (RectTransform)blocker.transform;
            brt.SetParent(content, false);
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0, 1);
            brt.sizeDelta = pane._contentSize;
            brt.anchoredPosition = Vector2.zero;
            brt.SetAsFirstSibling();
            var img = blocker.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0);

            pane.FindBar(scrollRoot, "ScrollBar_VT", ref pane._vtBar, ref pane._vtGrip);
            pane.FindBar(scrollRoot, "ScrollBar_HZ", ref pane._hzBar, ref pane._hzGrip);
            return pane;
        }

        private void FindBar(RectTransform root, string namePrefix, ref RectTransform bar, ref RectTransform grip)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name.StartsWith(namePrefix))
                {
                    bar = c.Find("bar") as RectTransform;
                    grip = c.Find("grip") as RectTransform;
                    return;
                }
            }
        }

        private static Vector2 ContentBounds(RectTransform content)
        {
            var bounds = Vector2.zero;
            for (var i = 0; i < content.childCount; i++)
            {
                var rt = (RectTransform)content.GetChild(i);
                var right = rt.anchoredPosition.x + rt.rect.width;
                var bottom = -rt.anchoredPosition.y + rt.rect.height;
                bounds = Vector2.Max(bounds, new Vector2(right, bottom));
            }
            return bounds;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _contentSize = ContentBounds(_content); // 内容可能在 Attach 之后被 GList.Fill 填充，起拖时重算滚动范围
            _startContent = _content.anchoredPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, e.position, e.pressEventCamera, out _startPointer);
        }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, e.position, e.pressEventCamera, out var local);
            var pos = _startContent + (local - _startPointer);
            pos.x = Mathf.Clamp(pos.x, -Mathf.Max(0, _contentSize.x - _viewSize.x), 0); // 左移露右
            pos.y = Mathf.Clamp(pos.y, 0, Mathf.Max(0, _contentSize.y - _viewSize.y));   // 上移露下
            _content.anchoredPosition = pos;
            UpdateGrips();
        }

        private void UpdateGrips()
        {
            if (_vtGrip != null && _vtBar != null && _contentSize.y > _viewSize.y)
            {
                var percent = _content.anchoredPosition.y / (_contentSize.y - _viewSize.y);
                var travel = _vtBar.rect.height - _vtGrip.rect.height;
                _vtGrip.anchoredPosition = new Vector2(_vtGrip.anchoredPosition.x, Relation.TopLeft(_vtBar).y - percent * travel);
            }
            if (_hzGrip != null && _hzBar != null && _contentSize.x > _viewSize.x)
            {
                var percent = -_content.anchoredPosition.x / (_contentSize.x - _viewSize.x);
                var travel = _hzBar.rect.width - _hzGrip.rect.width;
                _hzGrip.anchoredPosition = new Vector2(Relation.TopLeft(_hzBar).x + percent * travel, _hzGrip.anchoredPosition.y);
            }
        }
    }
}
