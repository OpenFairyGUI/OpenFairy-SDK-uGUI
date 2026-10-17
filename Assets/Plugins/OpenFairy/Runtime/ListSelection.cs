using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
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

        private readonly List<(Transform Item, ButtonBase Button, ListSelectionItem Relay, int Index)> _items = new();
        private readonly HashSet<int> _selectedIndices = new();
        private ListSource _source;

        private bool Virtual => _source != null && _source.isVirtual;

        protected override void Start() => Rebind();

        // 扫描 content 里的项接线（ListSource.Fill 动态填充后可再调）；索引 = 项在 content 里的次序（含非按钮项）。
        public void Rebind()
        {
            foreach (var item in _items)
                item.Relay?.Unbind();
            _items.Clear();
            _source = GetComponent<ListSource>();
            if (!Virtual)
                _selectedIndices.Clear();
            else
                _selectedIndices.RemoveWhere(index => index >= _source.count);
            var content = List.Container((RectTransform)transform);
            for (var i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (child.name == ListSource.PoolName)
                    continue;
                int index = _source != null && _source.TryGetItemIndex(child.gameObject, out int logical)
                    ? logical : _items.Count;
                var button = child.GetComponent<ButtonBase>();
                ListSelectionItem relay = null;
                if (button != null)
                {
                    if (!child.TryGetComponent(out relay))
                        relay = child.gameObject.AddComponent<ListSelectionItem>();
                    relay.Bind(this, button, index);
                    button.changeStateOnClick = false; // 选择由本组件驱动，禁项本体自翻 selected（复刻 GList 关掉 item changeStateOnClick）
                }
                if (!Virtual && button != null && button.selected)
                    _selectedIndices.Add(index);
                if (Virtual && button != null)
                    button.selected = _selectedIndices.Contains(index);
                _items.Add((child, button, relay, index));
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
                            onClickItem.Invoke(_items[i].Index);
                    return;
                }
        }

        // 复刻 GList.selectedIndex：get 取第一个选中项；set 排它选中该项（-1 = 清空）。
        public int selectedIndex
        {
            get
            {
                if (!Virtual)
                {
                    foreach (var item in _items)
                        if (item.Button != null && item.Button.selected)
                            return item.Index;
                    return -1;
                }
                int first = int.MaxValue;
                foreach (int index in _selectedIndices)
                    if (index < first)
                        first = index;
                return first == int.MaxValue ? -1 : first;
            }
            set
            {
                _selectedIndices.Clear();
                int count = Virtual ? _source.count : _items.Count;
                if (value >= 0 && value < count)
                    _selectedIndices.Add(value);
                ApplySelection();
            }
        }

        public void ClearSelection() => selectedIndex = -1;

        public List<int> GetSelection()
        {
            var result = new List<int>();
            if (Virtual)
                result.AddRange(_selectedIndices);
            else
                foreach (var item in _items)
                    if (item.Button is { selected: true })
                        result.Add(item.Index);
            result.Sort();
            return result;
        }

        private void ApplySelection()
        {
            foreach (var item in _items)
                if (item.Button != null)
                    item.Button.selected = _selectedIndices.Contains(item.Index);
        }

        internal void Click(int index)
        {
            if (!Virtual)
            {
                _selectedIndices.Clear();
                foreach (var item in _items)
                    if (item.Button is { selected: true })
                        _selectedIndices.Add(item.Index);
            }
            switch (selectionMode)
            {
                case ListSelectionMode.None:
                    break;
                case ListSelectionMode.MultipleSingleClick:
                    if (!_selectedIndices.Remove(index))
                        _selectedIndices.Add(index);
                    break;
                case ListSelectionMode.Single:
                case ListSelectionMode.Multiple:
                    _selectedIndices.Clear();
                    _selectedIndices.Add(index);
                    break;
            }
            ApplySelection();
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
