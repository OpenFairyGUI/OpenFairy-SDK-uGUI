using System;
using UnityEngine.EventSystems;

namespace NanamiUI.Example
{
    // Demo 用的通用点击中继：给本身非 Button 的节点（如 ComboBox 根）挂上以接收点击。
    public sealed class Clickable : UnityEngine.EventSystems.UIBehaviour, IPointerClickHandler
    {
        public Action onClick;
        public void OnPointerClick(PointerEventData eventData) => onClick?.Invoke();
    }
}
