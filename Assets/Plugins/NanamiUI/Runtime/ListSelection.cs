using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace NanamiUI
{
    // 复刻 FairyGUI GList 选择：点击列表项按 selectionMode 置选中/取消其它，并发 onClickItem(index)。
    // 由 Migrate 在 selectionMode != none 时挂到 list 根；ComboBox 下拉自管选择（Build 时移除本组件）。
    public sealed class ListSelection : UIBehaviour
    {
        public string selectionMode = "single"; // single / multiple / multiple_singleclick / none

        [System.NonSerialized] public UnityEvent<int> onClickItem = new();

        private readonly List<ButtonBase> _items = new();

        protected override void Start() => Rebind();

        // 扫描 content 里的按钮项接线（List.Fill 动态填充后可再调）。
        public void Rebind()
        {
            _items.Clear();
            var content = List.Container((RectTransform)transform);
            for (var i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (child.name == ScrollPane.HitName)
                    continue;
                if (child.GetComponent<ButtonBase>() is { } button)
                {
                    var index = _items.Count;
                    _items.Add(button);
                    button.changeStateOnClick = false; // 选择由本组件驱动，禁项本体自翻 selected（复刻 GList 关掉 item changeStateOnClick）
                    button.onClick.AddListener(() => Click(index));
                }
            }
        }

        public int selectedIndex
        {
            get
            {
                for (var i = 0; i < _items.Count; i++)
                    if (_items[i].Selected)
                        return i;
                return -1;
            }
        }

        private void Click(int index)
        {
            var clicked = _items[index];
            switch (selectionMode)
            {
                case "none":
                    break;
                case "multiple":
                case "multiple_singleclick":
                    clicked.Selected = !clicked.Selected; // 多选：切换本项
                    break;
                default: // single
                    foreach (var item in _items)
                        item.Selected = ReferenceEquals(item, clicked); // 单选：本项选中、其余取消
                    break;
            }
            onClickItem.Invoke(index);
        }
    }
}
