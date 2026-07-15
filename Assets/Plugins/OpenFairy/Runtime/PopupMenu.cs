using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Pool;

namespace OpenFairy.UGUI
{
    // 复刻 FairyGUI PopupMenu：内容 pane（背景 + 名为 "list" 的竖排列表），AddItem 建项，Show 经 Root 定位。瞬时无 tween。
    // itemPrefab 显式传入（避免为动态 list 重跑 Migrate 烘焙 ListSource；ComboBox/Window1 列表要通用化时再补 ListSource）。
    public sealed class PopupMenu : IDisposable
    {
        public bool hideOnClickItem = true;

        private readonly GameObject _content;
        private readonly RectTransform _contentRt;
        private readonly RectTransform _list;
        private readonly GameObject _itemPrefab;
        private readonly Vector2 _itemSize;
        private readonly float _authoredContentH;
        private readonly float _authoredListH;
        private readonly ObjectPool<GameObject> _itemPool;
        private RectTransform _poolRoot;
        private int _count;

        public RectTransform ContentPane => _contentRt;

        public PopupMenu(GameObject contentPrefab, GameObject itemPrefab)
        {
            _content = UnityEngine.Object.Instantiate(contentPrefab);
            _contentRt = (RectTransform)_content.transform;
            _list = (RectTransform)_contentRt.Find("list");
            _itemPrefab = itemPrefab;
            _itemSize = ((RectTransform)itemPrefab.transform).sizeDelta;
            _authoredContentH = _contentRt.rect.height; // Layout 用 authored 高度重算，避免重开时累加
            _authoredListH = _list.rect.height;
            _itemPool = new ObjectPool<GameObject>(
                () =>
                {
                    var item = UnityEngine.Object.Instantiate(_itemPrefab, PoolRoot, false);
                    ResetItem(item);
                    item.SetActive(false);
                    return item;
                },
                null,
                item =>
                {
                    ResetItem(item);
                    item.transform.SetParent(PoolRoot, false);
                    item.SetActive(false);
                },
                UnityEngine.Object.Destroy,
                false);
            for (var i = _list.childCount - 1; i >= 0; i--)
                _itemPool.Release(_list.GetChild(i).gameObject);
            // 菜单项点击 + 尺寸由 AddItem/Layout 自管，去掉列表自带的选择/自挂滚动接线。
            if (_list.GetComponent<ListSelection>() is { } listSelection)
                listSelection.enabled = false;
            if (_list.GetComponent<ScrollPaneHost>() is { } host)
                host.enabled = false;
            _content.SetActive(false);
        }

        // 返回项按钮：调用方可直接设 grayed/selected(勾选)/icon 等，无需再包一层 SetItemXxx。
        public ButtonBase AddItem(string caption)
        {
            var index = _count++;
            var itemGo = _itemPool.Get();
            itemGo.transform.SetParent(_list, false);
            itemGo.SetActive(true);
            var rt = (RectTransform)itemGo.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0, -index * _itemSize.y);
            var button = itemGo.GetComponent<ButtonBase>();
            button.title = caption;
            if (!itemGo.TryGetComponent(out PopupMenuItem item))
                item = itemGo.AddComponent<PopupMenuItem>();
            item.Bind(this, button);
            return button;
        }

        public void ClearItems()
        {
            for (var i = _list.childCount - 1; i >= 0; i--)
                _itemPool.Release(_list.GetChild(i).gameObject);
            _count = 0;
        }

        public UniTask Show(RectTransform target = null, PopupDirection dir = PopupDirection.Auto)
        {
            Layout();
            _content.SetActive(true);
            return Root.inst.ShowPopup(_contentRt, target, dir);
        }

        // 在指针处弹出（复刻 PopupMenu.Show() 无 target 时用 touchPosition），供右键上下文菜单。默认 Auto：贴近屏幕底时上翻。
        public UniTask ShowAtPointer(PointerEventData e, PopupDirection dir = PopupDirection.Auto)
        {
            Layout();
            _content.SetActive(true);
            return Root.inst.ShowPopupAt(_contentRt, Root.inst.ScreenToDesign(e.position, e.pressEventCamera), dir);
        }

        public void Hide() => Root.inst.HidePopup(_contentRt);

        public void Dispose()
        {
            if (Root.inst != null)
                Root.inst.HidePopup(_contentRt);
            _itemPool.Clear();
            UnityEngine.Object.Destroy(_content);
        }

        private RectTransform PoolRoot
        {
            get
            {
                if (_poolRoot == null)
                {
                    var go = new GameObject("__popupMenuPool", typeof(RectTransform));
                    _poolRoot = (RectTransform)go.transform;
                    _poolRoot.SetParent(_contentRt, false);
                    _poolRoot.anchorMin = _poolRoot.anchorMax = _poolRoot.pivot = new Vector2(0, 1);
                    go.SetActive(false);
                }
                return _poolRoot;
            }
        }

        private static void ResetItem(GameObject item)
        {
            item.GetComponent<PopupMenuItem>()?.Unbind();
            if (item.GetComponent<ButtonBase>() is { } button)
            {
                button.onClick.RemoveAllListeners();
                button.grayed = false;
                button.selected = false;
                button.changeStateOnClick = true;
            }
        }

        // ResizeToFit：list 高度 = 项数×项高；contentPane 高度 = authored + (list 增量)（复刻 Height relation 传播）。
        private void Layout()
        {
            var listH = _count * _itemSize.y;
            _list.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, listH);
            _contentRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _authoredContentH + (listH - _authoredListH));
        }
    }

    // 每个池化菜单项只创建一次 UnityAction；复用时只替换 owner，业务点击统一走 ButtonBase.onClick。
    internal sealed class PopupMenuItem : MonoBehaviour
    {
        private PopupMenu _owner;
        private ButtonBase _button;
        private UnityAction _click;

        internal void Bind(PopupMenu owner, ButtonBase button)
        {
            Unbind();
            _owner = owner;
            _button = button;
            _click ??= Click;
            _button.onClick.AddListener(_click);
        }

        internal void Unbind()
        {
            if (_button != null && _click != null)
                _button.onClick.RemoveListener(_click);
            _owner = null;
            _button = null;
        }

        private void Click()
        {
            if (_owner.hideOnClickItem)
                _owner.Hide();
        }
    }
}
