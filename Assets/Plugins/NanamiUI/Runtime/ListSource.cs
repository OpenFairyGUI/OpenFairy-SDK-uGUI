using UnityEngine;

namespace NanamiUI
{
    // list 节点的动态实例化描述（由 Migrate.BuildList 烘焙）：运行时据此从 itemPrefab 建项。
    // 让 PopupMenu/ComboBox 下拉/Window1 列表/Grid 等空/动态列表无需显式传 prefab。
    public sealed class ListSource : MonoBehaviour
    {
        public GameObject itemPrefab;
        public Vector2 itemSize;
        public float lineGap;
        public float colGap;
        public string layout;
    }
}
