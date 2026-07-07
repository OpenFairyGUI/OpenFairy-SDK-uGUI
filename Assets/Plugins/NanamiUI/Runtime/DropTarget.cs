using System;
using UnityEngine.EventSystems;

namespace NanamiUI
{
    // 拖放落点（复刻 FairyGUI onDrop 监听）：DragDropManager 在松手命中时向其派发 sourceData。
    public sealed class DropTarget : UIBehaviour
    {
        public Action<object> onDrop;
        internal void Fire(object data) => onDrop?.Invoke(data);
    }
}
