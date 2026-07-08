using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace NanamiUI
{
    // 复刻 FairyGUI GComboBox：继承 Button<T> 复用 up/down/over 视觉态；点击弹下拉列表，选项设标题、发 onChanged。
    // items 与 dropdownPrefab(ComboBoxPopup，含名为 "list" 的列表) 由 Migrate 烘焙；下拉列表用其 ListSource 填 items。
    public abstract class ComboBox<T> : Button<T> where T : struct, Enum
    {
        public string[] items;
        public string[] values; // 可选：与 items 平行的值集（FairyGUI GComboBox.values），未烘焙时 value 回退到显示文本
        public int visibleItemCount = 10; // 下拉最多同屏项数，超出则裁剪并滚动（复刻 GComboBox.visibleItemCount）
        public GameObject dropdownPrefab;
        public UnityEvent onChanged = new();

        [SerializeField]
        private int _selectedIndex;

        [NonSerialized] private GameObject _dropdown;
        [NonSerialized] private RectTransform _dropdownRt;

        // 复刻 GComboBox.selectedIndex：程序化赋值也刷新标题并发 onChanged。
        public int selectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (value == _selectedIndex)
                    return;
                _selectedIndex = value;
                Apply();
                onChanged.Invoke();
            }
        }

        // 当前显示文本 / 值（复刻 GComboBox.text / value）。
        public string text => items != null && _selectedIndex >= 0 && _selectedIndex < items.Length ? items[_selectedIndex] : "";
        public string value => values != null && _selectedIndex >= 0 && _selectedIndex < values.Length ? values[_selectedIndex] : text;

        private void Apply()
        {
            if (titleText != null && _selectedIndex >= 0 && items != null && _selectedIndex < items.Length)
                titleText.text = items[_selectedIndex];
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (grayed)
                return;
            // 展开时点按钮外区域由 Root 的透明 blocker 收起（blocker 在按钮之上截获点击），故这里只管展开。
            RefreshState();
            ShowDropdown();
        }

        private void ShowDropdown()
        {
            if (items == null || items.Length == 0 || dropdownPrefab == null)
                return;
            if (_dropdown == null)
                Build();
            if (_dropdownRt != null)
            {
                if (Enum.TryParse<T>("down", out var down)) // 打开时按钮进 down 态（复刻 GComboBox）
                    controller.page = down;
                Root.inst.ShowPopup(_dropdownRt, (RectTransform)transform, PopupDirection.Down, RefreshState); // 关闭（含外点）恢复态
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
            var source = list.GetComponent<ListSource>();
            List.Fill(list, items.Length, (itemGo, i) =>
            {
                var button = itemGo.GetComponent<ButtonBase>();
                button.Title = items[i];
                var index = i;
                button.onClick.AddListener(() => Select(index));
            });
            // 可见高度按 visibleItemCount 裁剪；项数不超时表现与撑开一致（demo 短下拉不受影响）。
            var visible = Mathf.Min(items.Length, Mathf.Max(1, visibleItemCount));
            var h = visible * source.itemSize.y;
            list.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            _dropdownRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h + 2);
            if (_dropdown.transform.Find("n0") is RectTransform bg)
            {
                bg.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _dropdownRt.rect.width);
                bg.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h + 2);
            }
            // 项数超出可见数：viewport 已被裁到可见高，内容更高，挂 ScrollPane 支持拖动/滚轮滚动。
            if (items.Length > visible)
                ScrollPane.Attach(list);
        }

        private void Select(int index)
        {
            Root.inst.HidePopup(_dropdownRt);
            selectedIndex = index; // 属性内部刷新标题并发 onChanged
        }
    }
}
