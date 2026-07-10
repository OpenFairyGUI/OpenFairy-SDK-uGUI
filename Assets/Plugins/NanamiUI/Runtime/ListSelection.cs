using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace NanamiUI
{
    // 复刻 FairyGUI ListSelectionMode。
    public enum ListSelectionMode
    {
        Single,
        Multiple,
        MultipleSingleClick,
        None,
    }

    // 复刻 FairyGUI GList 选择：点击列表项按 selectionMode 置选中，并对任意类型的项发 onClickItem(index)。
    // 无修饰键点击下 Single 与 Multiple 都是排它选中，MultipleSingleClick 每次点击切换（复刻 SetSelectionOnEvent）。
    // 由 Migrate 在 selectionMode != None 时挂到 list 根；ComboBox 下拉自管选择（Build 时禁用本组件）。
    public sealed class ListSelection : UIBehaviour, IPointerClickHandler
    {
        public ListSelectionMode selectionMode = ListSelectionMode.Single;

        [System.NonSerialized] public UnityEvent<int> onClickItem = new();

        private readonly List<(Transform Item, ButtonBase Button, ListSelectionItem Relay)> _items = new();

        protected override void Start() => Rebind();

        // 扫描 content 里的项接线（ListSource.Fill 动态填充后可再调）；索引 = 项在 content 里的次序（含非按钮项）。
        public void Rebind()
        {
            foreach (var item in _items)
                item.Relay?.Unbind();
            _items.Clear();
            var content = List.Container((RectTransform)transform);
            for (var i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (child.name == ScrollPane.HitName || child.name == ListSource.PoolName)
                    continue;
                var button = child.GetComponent<ButtonBase>();
                ListSelectionItem relay = null;
                if (button != null)
                {
                    var index = _items.Count;
                    relay = child.GetComponent<ListSelectionItem>() ?? child.gameObject.AddComponent<ListSelectionItem>();
                    relay.Bind(this, button, index);
                    button.changeStateOnClick = false; // 选择由本组件驱动，禁项本体自翻 selected（复刻 GList 关掉 item changeStateOnClick）
                }
                _items.Add((child, button, relay));
            }
        }

        // 非按钮项自身无点击 handler，点击冒泡到列表根：解析落点所属项，同样发 onClickItem（复刻 GList 对任意项派发）。
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.pointerPressRaycast.gameObject == null)
                return;
            var content = List.Container((RectTransform)transform);
            for (var t = eventData.pointerPressRaycast.gameObject.transform; t != null && t != transform; t = t.parent)
                if (t.parent == content)
                {
                    for (var i = 0; i < _items.Count; i++)
                        if (_items[i].Item == t && _items[i].Button == null) // 按钮项经自身 onClick 走 Click，不重复
                            onClickItem.Invoke(i);
                    return;
                }
        }

        // 复刻 GList.selectedIndex：get 取第一个选中项；set 排它选中该项（-1 = 清空）。
        public int selectedIndex
        {
            get => _items.FindIndex(item => item.Button != null && item.Button.selected);
            set
            {
                for (var i = 0; i < _items.Count; i++)
                    if (_items[i].Button is { } button)
                        button.selected = i == value;
            }
        }

        public void ClearSelection() => selectedIndex = -1;

        // 复刻 GList.GetSelection：当前全部选中项索引（Multiple 模式可多个）。
        public List<int> GetSelection()
        {
            var result = new List<int>();
            for (var i = 0; i < _items.Count; i++)
                if (_items[i].Button is { selected: true })
                    result.Add(i);
            return result;
        }

        internal void Click(int index)
        {
            var clicked = _items[index].Button;
            switch (selectionMode)
            {
                case ListSelectionMode.None:
                    break;
                case ListSelectionMode.MultipleSingleClick:
                    clicked.selected = !clicked.selected; // 每次点击切换
                    break;
                case ListSelectionMode.Single:
                case ListSelectionMode.Multiple: // 无修饰键点击与 Single 一致：排它选中
                    foreach (var item in _items)
                        if (item.Button != null)
                            item.Button.selected = ReferenceEquals(item.Button, clicked);
                    break;
            }
            onClickItem.Invoke(index);
        }
    }

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
