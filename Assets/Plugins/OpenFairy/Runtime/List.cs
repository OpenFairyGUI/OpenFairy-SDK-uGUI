using UnityEngine;

namespace OpenFairy.UGUI
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
        public static RectTransform Container(RectTransform list)
        {
            if (list.TryGetComponent<ListSource>(out var source) && source.content != null) return source.content;
            var content = ScrollPane.Attach(list)?.content ?? list;
            if (source != null) source.content = content;
            return content;
        }
    }
}
