using UnityEngine;

namespace OpenFairy.UGUI
{
    // 复刻 FairyGUI GComponent 的 sortingOrder 兄弟序：order==0 的子物体保持插入序在前，order>0 的按升序排后（等值稳定）。
    // uGUI 里 later sibling = 画在上面，与 FairyGUI child index 语义一致。
    public static class Depth
    {
        // 复刻 GetInsertPosForSortingChild：扫描到第一个 order 更大的子物体处插入（严格 < → 等值追加在后，稳定）。
        public static int SortIndex(RectTransform parent, int order, Transform ignore = null)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c == ignore)
                    continue;
                var o = c.TryGetComponent(out SortObject s) ? s.order : 0;
                if (order < o)
                    return i;
            }
            return parent.childCount;
        }

        // 复刻 GObject.sortingOrder setter + ChildSortingOrderChanged。SetSiblingIndex 先移除再插入，故目标位
        // = 应排在它前面的其它子物体数（order<=它的都在前，等值稳定追加在后，同 GetInsertPosForSortingChild 的 first-strictly-greater）。
        public static void SetSortingOrder(RectTransform child, int order)
        {
            if (!child.TryGetComponent(out SortObject so))
                so = child.gameObject.AddComponent<SortObject>();
            so.order = Mathf.Max(0, order);
            var parent = child.parent;
            var index = 0;
            for (var i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c == child)
                    continue;
                var o = c.TryGetComponent(out SortObject s) ? s.order : 0;
                if (o <= so.order)
                    index++;
            }
            child.SetSiblingIndex(index);
        }

        // 运行时建矩形（复刻 GGraph.DrawRect）：Graph 的 Rect 网格已完全对齐 FairyGUI；按 order 插到正确兄弟位。
        public static Graph CreateRect(RectTransform parent, Vector2 fairyXY, float w, float h, int lineSize, Color line, Color fill, int order)
        {
            var go = new GameObject("graph", typeof(RectTransform), typeof(CanvasRenderer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(fairyXY.x, -fairyXY.y);
            var shape = go.AddComponent<Graph>();
            shape.kind = Graph.Kind.Rect;
            shape.lineSize = lineSize;
            shape.lineColor = line;
            shape.color = fill;
            go.AddComponent<SortObject>().order = order;
            rt.SetSiblingIndex(SortIndex(parent, order));
            return shape;
        }
    }
}
