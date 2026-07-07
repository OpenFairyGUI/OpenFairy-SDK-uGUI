using UnityEngine;

namespace NanamiUI
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

        // 复刻 GObject.sortingOrder setter + ChildSortingOrderChanged：SetSiblingIndex 自带移除位移，
        // 故用 ignore=child 求目标位再钳到 childCount-1，等价 oldIndex<newIndex → newIndex-1 的调整。
        public static void SetSortingOrder(RectTransform child, int order)
        {
            var so = child.GetComponent<SortObject>();
            if (so == null)
                so = child.gameObject.AddComponent<SortObject>();
            so.order = Mathf.Max(0, order);
            var index = SortIndex((RectTransform)child.parent, so.order, child);
            child.SetSiblingIndex(Mathf.Min(index, child.parent.childCount - 1));
        }

        // 运行时建矩形（复刻 GGraph.DrawRect）：Shape 的 Rect 网格已完全对齐 FairyGUI；按 order 插到正确兄弟位。
        public static Shape CreateRect(RectTransform parent, Vector2 fairyXY, float w, float h, int lineSize, Color line, Color fill, int order)
        {
            var go = new GameObject("graph", typeof(RectTransform), typeof(CanvasRenderer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(fairyXY.x, -fairyXY.y);
            var shape = go.AddComponent<Shape>();
            shape.kind = Shape.Kind.Rect;
            shape.lineSize = lineSize;
            shape.lineColor = line;
            shape.color = fill;
            go.AddComponent<SortObject>().order = order;
            rt.SetSiblingIndex(SortIndex(parent, order));
            return shape;
        }
    }
}
