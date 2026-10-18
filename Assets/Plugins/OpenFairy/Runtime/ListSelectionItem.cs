using UnityEngine;
using UnityEngine.Events;

namespace OpenFairy.UGUI
{
    // 运行时列表项点击中继：每个池化 item 只创建一次 UnityAction，Rebind 只更新 owner/index，不再逐项分配闭包。
    internal sealed class ListSelectionItem : MonoBehaviour
    {
        private ListSelection _owner;
        private ButtonBase _button;
        private int _index;
        private UnityAction _action;

        internal void Bind(ListSelection owner, ButtonBase button, int index)
        {
            Unbind();
            _owner = owner;
            _button = button;
            _index = index;
            _action ??= Click;
            _button.onClick.AddListener(_action);
        }

        internal void Unbind()
        {
            if (_button != null && _action != null)
                _button.onClick.RemoveListener(_action);
            _owner = null;
            _button = null;
        }

        private void Click() => _owner.Click(_index);
    }
}
