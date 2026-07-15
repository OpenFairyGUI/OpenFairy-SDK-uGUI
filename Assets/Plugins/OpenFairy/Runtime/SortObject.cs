using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    // 承载子物体的 sortingOrder（复刻 FairyGUI GObject.sortingOrder），供 Depth 维护兄弟序。缺省即 order 0 = 非排序块。
    public sealed class SortObject : UIBehaviour
    {
        public int order;
    }
}
