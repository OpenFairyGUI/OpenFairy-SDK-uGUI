using UnityEngine;
using UnityEngine.Events;

namespace OpenFairy.UGUI
{
    // 重填可以发生在同一次点击派发中；旧绑定的在途回调不能作用于回收后重新绑定的项。
    internal sealed class ListSelectionItem : MonoBehaviour
    {
        private ButtonBase _button;
        private UnityAction _action;
        private uint _version;

        internal void Bind(ListSelection owner, ButtonBase button, int index)
        {
            Unbind();
            _button = button;
            var version = _version;
            _action = () =>
            {
                if (version == _version) owner.Click(index);
            };
            _button.onClick.AddListener(_action);
        }

        internal void Unbind()
        {
            _version++;
            if (_button != null && _action != null)
                _button.onClick.RemoveListener(_action);
            _button = null;
        }
    }
}
