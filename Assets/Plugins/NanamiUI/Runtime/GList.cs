using System;
using UnityEngine;

namespace NanamiUI
{
    // 运行时列表填充（复刻 FairyGUI GList.AddItemFromPool 的简版）：按 list 上的 ListSource 从 itemPrefab 建 count 个项，
    // 列式排布，逐项回调设数据。项建在 viewport/content（若已挂 ScrollPane）或 viewport 或 list 本身。
    public static class GList
    {
        public static RectTransform Container(RectTransform list) =>
            list.Find("viewport/content") as RectTransform
            ?? list.Find("viewport") as RectTransform
            ?? list;

        public static void Fill(RectTransform list, int count, Action<GameObject, int> setup)
        {
            var source = list.GetComponent<ListSource>();
            var container = Container(list);
            for (var i = container.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(container.GetChild(i).gameObject);
            var step = source.itemSize.y + source.lineGap;
            for (var i = 0; i < count; i++)
            {
                var item = UnityEngine.Object.Instantiate(source.itemPrefab, container, false);
                var rt = (RectTransform)item.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(0, -i * step);
                setup(item, i);
            }
        }
    }
}
