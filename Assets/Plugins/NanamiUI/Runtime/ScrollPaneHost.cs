using UnityEngine;

namespace NanamiUI
{
    // Migrate 给 overflow=scroll 的根挂上：运行时 Start 自挂 ScrollPane（复刻 FairyGUI 载入即建 ScrollPane），
    // 使转换后的滚动组件无需业务胶水即可滚动。烘焙进 prefab 的 MonoBehaviour 必须独立成同名文件才能被序列化。
    public sealed class ScrollPaneHost : MonoBehaviour
    {
        private void Start() => ScrollPane.Attach((RectTransform)transform);
    }
}
