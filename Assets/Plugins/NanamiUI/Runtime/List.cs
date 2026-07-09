using System;
using UnityEngine;

namespace NanamiUI
{
    // 复刻 FairyGUI ListLayoutType。Pagination 仅按 FlowHorizontal 排布（分页吸附不做）。
    public enum ListLayoutType
    {
        SingleColumn,
        SingleRow,
        FlowHorizontal,
        FlowVertical,
        Pagination,
    }

    // 运行时列表填充（复刻 FairyGUI List.AddItemFromPool 的简版）：按 list 上的 ListSource 从 itemPrefab 建 count 个项，
    // 列式排布，逐项回调设数据。项建在 viewport/content（若已挂 ScrollPane）或 viewport 或 list 本身。
    public static class List
    {
        public static RectTransform Container(RectTransform list) =>
            list.Find("viewport/content") as RectTransform
            ?? list.Find("viewport") as RectTransform
            ?? list;

        public static void Fill(RectTransform list, int count, Action<GameObject, int> setup, bool rebindSelection = true)
        {
            var source = list.GetComponent<ListSource>();
            var container = Container(list);
            for (var i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                if (child.name != ScrollPane.HitName && child.name != ListSource.PoolName)
                    source.ReleaseItem(child.gameObject);
            }

            var stepX = source.itemSize.x + source.colGap;
            var stepY = source.itemSize.y + source.lineGap;
            var columns = stepX > 0 ? Mathf.Max(1, Mathf.FloorToInt((list.rect.width + source.colGap) / stepX)) : 1;
            var rows = stepY > 0 ? Mathf.Max(1, Mathf.FloorToInt((list.rect.height + source.lineGap) / stepY)) : 1;

            for (var i = 0; i < count; i++)
            {
                var item = source.GetItem(container);
                var rt = (RectTransform)item.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
                var (col, row) = source.layout switch
                {
                    ListLayoutType.SingleRow => (i, 0),
                    ListLayoutType.FlowHorizontal or ListLayoutType.Pagination => (i % columns, i / columns),
                    ListLayoutType.FlowVertical => (i / rows, i % rows),
                    _ => (0, i), // SingleColumn
                };
                rt.anchoredPosition = new Vector2(col * stepX, -row * stepY);
                setup(item, i);
            }
            source.PlacePoolRootLast();
            if (container.Find(ScrollPane.HitName) is RectTransform hit)
                hit.SetAsFirstSibling();
            list.GetComponent<ScrollPane>()?.RefreshContent();
            if (rebindSelection && list.GetComponent<ListSelection>() is { enabled: true } selection)
                selection.Rebind();
        }
    }
}
