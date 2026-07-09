using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace NanamiUI
{
    // 复刻 FairyGUI GList 选择：点击列表项按 selectionMode 置选中，并对任意类型的项发 onClickItem(index)。
    // 无修饰键点击下 single 与 multiple 都是排它选中，multiple_singleclick 每次点击切换（复刻 SetSelectionOnEvent）。
    // 由 Migrate 在 selectionMode != none 时挂到 list 根；ComboBox 下拉自管选择（Build 时禁用本组件）。
    public sealed class ListSelection : UIBehaviour, IPointerClickHandler
    {
        public string selectionMode = "single"; // single / multiple / multiple_singleclick / none

        [System.NonSerialized] public UnityEvent<int> onClickItem = new();

        private readonly List<(Transform Item, ButtonBase Button, UnityAction Action)> _items = new();

        protected override void Start() => Rebind();

        // 扫描 content 里的项接线（List.Fill 动态填充后可再调）；索引 = 项在 content 里的次序（含非按钮项）。
        public void Rebind()
        {
            foreach (var item in _items)
                if (item.Button != null)
                    item.Button.onClick.RemoveListener(item.Action);
            _items.Clear();
            var content = List.Container((RectTransform)transform);
            for (var i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (child.name == ScrollPane.HitName || child.name == ListSource.PoolName)
                    continue;
                var button = child.GetComponent<ButtonBase>();
                UnityAction action = null;
                if (button != null)
                {
                    var index = _items.Count;
                    action = () => Click(index);
                    button.changeStateOnClick = false; // 选择由本组件驱动，禁项本体自翻 selected（复刻 GList 关掉 item changeStateOnClick）
                    button.onClick.AddListener(action);
                }
                _items.Add((child, button, action));
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

        public int selectedIndex
        {
            get
            {
                for (var i = 0; i < _items.Count; i++)
                    if (_items[i].Button != null && _items[i].Button.Selected)
                        return i;
                return -1;
            }
        }

        private void Click(int index)
        {
            var clicked = _items[index].Button;
            switch (selectionMode)
            {
                case "none":
                    break;
                case "multiple_singleclick":
                    clicked.Selected = !clicked.Selected; // 每次点击切换（复刻 Multiple_SingleClick）
                    break;
                default: // single 与 multiple 的无修饰键点击一致：排它选中
                    foreach (var item in _items)
                        if (item.Button != null)
                            item.Button.Selected = ReferenceEquals(item.Button, clicked);
                    break;
            }
            onClickItem.Invoke(index);
        }
    }
}
