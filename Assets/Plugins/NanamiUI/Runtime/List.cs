using System;
using UnityEngine;

namespace NanamiUI
{
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

            // 复刻 FairyGUI ListLayoutType：column（竖排，默认）/ row（横排）/ flow_hz（横向流式换行网格）/ flow_vt（纵向流式）。
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
                    "row" => (i, 0),
                    "flow_hz" or "pagination" => (i % columns, i / columns),
                    "flow_vt" => (i / rows, i % rows),
                    _ => (0, i), // column（默认）
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
