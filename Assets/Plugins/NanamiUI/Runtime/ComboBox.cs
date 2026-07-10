using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NanamiUI
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

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || grayed)
                return;
            // 展开时点按钮外区域由 Root 的透明 blocker 收起（blocker 在按钮之上截获点击），故这里只管展开。
            RefreshState();
            ShowDropdown().Forget();
        }

        private async UniTask ShowDropdown()
        {
            if (_items == null || _items.Length == 0 || dropdownPrefab == null)
                return;
            if (Root.inst == null) // 烘焙 prefab 无需业务胶水：首次弹下拉时自建覆盖层（复刻 GRoot.inst 惰性自建）
                Root.Create((RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform);
            if (_itemsChanged && _dropdown != null) // items/values 变过：销毁重建（复刻 _itemsUpdated → RenderDropdownList）
            {
                Destroy(_dropdown);
                _dropdown = null;
                _dropdownRt = null;
            }
            if (_dropdown == null)
                Build();
            if (_dropdownRt != null)
            {
                var closed = Root.inst.ShowPopup(_dropdownRt, (RectTransform)transform, popupDirection);
                SetState(VisualState.Down); // 打开时按钮进 down 态（复刻 GComboBox）
                await closed;
                if (this != null)
                    RefreshState(); // 关闭（含外点）恢复态
            }
        }

        private void Build()
        {
            var dropdown = Instantiate(dropdownPrefab);
            if (dropdown.transform.Find("list") is not RectTransform list || list.GetComponent<ListSource>() == null)
            {
                Destroy(dropdown); // 下拉资源结构不符（无 list/ListSource），销毁实例、留待下次重试
                return;
            }
            _dropdown = dropdown;
            _dropdownRt = (RectTransform)_dropdown.transform;
            _itemsChanged = false;
            // 下拉项点击 + 滚动由 ComboBox 自管，去掉列表自带的选择/自挂滚动接线（否则未超可见数也会挂 ScrollPane）。
            if (list.GetComponent<ListSelection>() is { } listSelection)
                listSelection.enabled = false;
            if (list.GetComponent<ScrollPaneHost>() is { } host)
                host.enabled = false;
            var source = list.GetComponent<ListSource>();
            source.Fill(_items.Length, (itemGo, i) =>
            {
                var button = itemGo.GetComponent<ButtonBase>();
                button.title = _items[i];
                var index = i;
                button.onClick.AddListener(() => Select(index));
            }, false);
            // 可见高度按 visibleItemCount 裁剪；项数不超时表现与撑开一致（demo 短下拉不受影响）。
            var visible = Mathf.Min(_items.Length, Mathf.Max(1, visibleItemCount));
            var h = visible * source.itemSize.y;
            list.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            _dropdownRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h + 2);
            // 背景等其余子件带容器 relation，已烘成拉伸锚点，随下拉根缩放自动跟随。
            // 项数超出可见数：viewport 已被裁到可见高，内容更高，挂 ScrollPane 支持拖动/滚轮滚动。
            if (_items.Length > visible)
                ScrollPane.Attach(list);
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
