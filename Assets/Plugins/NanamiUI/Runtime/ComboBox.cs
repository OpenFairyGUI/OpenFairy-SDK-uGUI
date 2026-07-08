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
        public GameObject dropdownPrefab;
        public int selectedIndex;
        public UnityEvent onChanged = new();

        [NonSerialized] private GameObject _dropdown;
        [NonSerialized] private RectTransform _dropdownRt;

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (grayed)
                return;
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
                GRoot.inst.ShowPopup(_dropdownRt, (RectTransform)transform, PopupDirection.Down);
        }

        private void Build()
        {
            _dropdown = Instantiate(dropdownPrefab);
            if (_dropdown.transform.Find("list") is not RectTransform list || list.GetComponent<ListSource>() == null)
                return; // 下拉资源结构不符（无 list/ListSource），不弹
            _dropdownRt = (RectTransform)_dropdown.transform;
            var source = list.GetComponent<ListSource>();
            GList.Fill(list, items.Length, (itemGo, i) =>
            {
                var button = itemGo.GetComponent<ButtonBase>();
                button.Title = items[i];
                var index = i;
                button.onClick.AddListener(() => Select(index));
            });
            // 撑开 popup + list 容纳全部项（简版：不虚拟化、不裁剪滚动）。
            var h = items.Length * source.itemSize.y;
            list.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            _dropdownRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h + 2);
            if (_dropdown.transform.Find("n0") is RectTransform bg)
            {
                bg.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _dropdownRt.rect.width);
                bg.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h + 2);
            }
        }

        private void Select(int index)
        {
            selectedIndex = index;
            if (titleText != null)
                titleText.text = items[index];
            GRoot.inst.HidePopup(_dropdownRt);
            onChanged.Invoke();
        }
    }
}
