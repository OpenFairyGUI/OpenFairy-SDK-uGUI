using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    // ComboBox 的非泛型面：不知道控制器 enum T 的代码（业务/测试）经它读写选项与选中态。
    public interface IComboBox
    {
        string[] items { get; set; }
        string[] values { get; set; }
        int selectedIndex { get; set; }
        string text { get; set; }
        string value { get; set; }
    }

    // 复刻 FairyGUI GComboBox：继承 Button<T> 复用 up/down/over 视觉态；点击弹下拉列表，选项设标题、发 onChanged。
    // items 与 dropdownPrefab(ComboBoxPopup，含名为 "list" 的列表) 由 Migrate 烘焙；下拉列表用其 ListSource 填 items。
    public abstract class ComboBox<T> : Button<T>, IComboBox where T : struct, Enum
    {
        public int visibleItemCount = 10; // 下拉最多同屏项数，超出则裁剪并滚动（复刻 GComboBox.visibleItemCount）
        public PopupDirection popupDirection = PopupDirection.Auto; // 复刻 GComboBox 默认 Auto：贴屏幕底时向上翻
        [SerializeField] internal GameObject dropdownPrefab; // 烘焙接线
        // onChanged 复用 ButtonBase 的事件（FairyGUI 中 GButton/GComboBox 的 "onChanged" 本就是同一事件通道）。

        [SerializeField] internal string[] _items;
        [SerializeField] internal string[] _values;

        [SerializeField]
        private int _selectedIndex;

        [NonSerialized] private GameObject _dropdown;
        [NonSerialized] private RectTransform _dropdownRt;
        [NonSerialized] private RectTransform _list;
        [NonSerialized] private bool _itemsChanged;

        // 复刻 GComboBox.items/values 的 setter：标脏，下次 ShowDropdown 重建下拉（对齐 _itemsUpdated）。
        public string[] items
        {
            get => _items;
            set
            {
                _items = value;
                _itemsChanged = true;
            }
        }

        // 可选：与 items 平行的值集（FairyGUI GComboBox.values），未烘焙时 value 回退到显示文本。
        public string[] values
        {
            get => _values;
            set
            {
                _values = value;
                _itemsChanged = true;
            }
        }

        // 复刻 GComboBox.selectedIndex 的 setter：只刷新标题/图标，不发 onChanged（onChanged 仅由用户点选下拉项触发）。
        public int selectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (value == _selectedIndex)
                    return;
                _selectedIndex = value;
                Apply();
            }
        }

        // 复刻 GComboBox.text：标题直通，不反查索引、不发 onChanged。
        public string text
        {
            get => title;
            set => title = value;
        }

        // 复刻 GComboBox.value：设值按 values 反查，找不到回退首项；不发 onChanged。
        public string value
        {
            get => _values != null && _selectedIndex >= 0 && _selectedIndex < _values.Length ? _values[_selectedIndex] : text;
            set
            {
                var index = _values != null ? Array.IndexOf(_values, value) : -1;
                selectedIndex = index >= 0 ? index : 0;
            }
        }

        // 经 base title 设标题（写 _title），使后续 RefreshState 不把标题回退到初始项。
        private void Apply()
        {
            title = _items != null && _selectedIndex >= 0 && _selectedIndex < _items.Length ? _items[_selectedIndex] : "";
        }

        private bool DropdownOpen => _dropdown != null && _dropdown.activeSelf;

        // 复刻 GComboBox.__rollover/__rollout：下拉打开或按住期间不改态。
        public override void OnPointerEnter(PointerEventData eventData)
        {
            _over = true;
            if (!_down && !DropdownOpen)
                RefreshState();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            _over = false;
            if (!_down && !DropdownOpen)
                RefreshState();
        }

        // 复刻 GComboBox.__touchBegin：按下即开关下拉（同一次按下先由 PopupInputModule 关闭的下拉不会重开）。
        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            _down = true;
            ShowDropdown().Forget();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !_down)
                return;
            _down = false;
            if (DropdownOpen)
                RefreshState();
        }

        // 复刻 GComboBox.__click：左键只播放点击音。
        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                PlayClickSound();
            else
                base.OnPointerClick(eventData);
        }

        // 复刻 GComboBox.SetCurrentState。
        public override void RefreshState()
        {
            RefreshTitle();
            SetState(grayed && StatePages.HasDisabled ? VisualState.Disabled
                : DropdownOpen ? VisualState.Down
                : _over ? VisualState.Over : VisualState.Up);
        }

        private async UniTask ShowDropdown()
        {
            if (_items == null || _items.Length == 0 || dropdownPrefab == null)
                return;
            var root = Root.Ensure(this);
            if (_itemsChanged && _dropdown != null) // items/values 变过：销毁重建（复刻 _itemsUpdated → RenderDropdownList）
            {
                root.HidePopupImmediately(_dropdownRt);
                Destroy(_dropdown);
                _dropdown = null;
            }
            if (_dropdown == null)
                Build();
            // 复刻 GComboBox.ShowDropdown 的 dropdown.width = width，列表按 Width 关联跟随。
            var width = ((RectTransform)transform).rect.width;
            var delta = width - _dropdownRt.rect.width;
            _dropdownRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            if (_list.anchorMin.x == _list.anchorMax.x)
                _list.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _list.rect.width + delta);
            var closed = root.TogglePopup(_dropdownRt, (RectTransform)transform, popupDirection);
            RefreshState();
            if (await closed.SuppressCancellationThrow() || this == null) return;
            RefreshState(); // 关闭（含外点）恢复态
        }

        protected override void OnDestroy()
        {
            if (_dropdown != null)
            {
                if (Root.inst != null) Root.inst.HidePopupImmediately(_dropdownRt);
                Destroy(_dropdown);
            }
            base.OnDestroy();
        }

        private void Build()
        {
            _dropdown = Instantiate(dropdownPrefab);
            _dropdownRt = (RectTransform)_dropdown.transform;
            _itemsChanged = false;
            var list = _list = (RectTransform)_dropdownRt.Find("list");
            var source = list.GetComponent<ListSource>();
            // 下拉项点击 + 滚动由 ComboBox 自管，去掉列表自带的选择接线。
            if (list.TryGetComponent<ListSelection>(out var listSelection))
                listSelection.enabled = false;
            source.Fill(_items.Length, (itemGo, i) =>
            {
                var button = itemGo.GetComponent<ButtonBase>();
                button.title = _items[i];
                var index = i;
                button.onClick.AddListener(() => Select(index));
            }, false);
            // 复刻 GList.ResizeToFit(visibleItemCount) 与下拉对列表的 Height 关联：下拉保持与列表的作者高度差。
            var visible = Mathf.Min(_items.Length, Mathf.Max(1, visibleItemCount));
            var listHeight = visible * source.itemSize.y + (visible - 1) * source.lineGap;
            _dropdownRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _dropdownRt.rect.height + listHeight - list.rect.height);
            list.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, listHeight);
            // 项数超出可见数：viewport 已被裁到可见高，内容更高，挂 ScrollPane 支持拖动/滚轮滚动。
            var pane = ScrollPane.Attach(list);
            if (pane != null) pane.enabled = _items.Length > visible;
        }

        private void Select(int index)
        {
            Root.inst.HidePopup(_dropdownRt);
            // 复刻 GComboBox.__clickItem：即使点的是当前项也刷新并发 onChanged（绕过 setter 的同值早返回）。
            _selectedIndex = index;
            Apply();
            onChanged.Invoke();
        }
    }
}
