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

    // 列表结构工具：动态填充入口在 ListSource.Fill（状态与烘焙描述都在 ListSource 上）。
    public static class List
    {
        // 列表项所在容器：viewport/content（已挂 ScrollPane）或 viewport 或 list 本身。
        public static RectTransform Container(RectTransform list) =>
            list.Find("viewport/content") as RectTransform
            ?? list.Find("viewport") as RectTransform
            ?? list;
    }
}
