using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace NanamiUI
{
    // list 节点的动态实例化描述（由 Migrate.BuildList 烘焙）：运行时据此从 itemPrefab 建项。
    // 让 PopupMenu/ComboBox 下拉/Window1 列表/Grid 等空/动态列表无需显式传 prefab。
    public sealed class ListSource : MonoBehaviour
    {
        internal const string PoolName = "__listPool";

        public GameObject itemPrefab;
        public Vector2 itemSize;
        public float lineGap;
        public float colGap;
        public ListLayoutType layout;

        private ObjectPool<GameObject> _pool;
        private RectTransform _poolRoot;
        private readonly List<ButtonBase> _buttons = new();

        // 运行时列表填充（复刻 FairyGUI GList numItems+itemRenderer 的简版）：从池建 count 个项、按 layout 排布、逐项回调设数据。
        public void Fill(int count, Action<GameObject, int> setup, bool rebindSelection = true)
        {
            var list = (RectTransform)transform;
            var container = List.Container(list);
            for (var i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                if (child.name != ScrollPane.HitName && child.name != PoolName)
                    ReleaseItem(child.gameObject);
            }

            var stepX = itemSize.x + colGap;
            var stepY = itemSize.y + lineGap;
            var columns = stepX > 0 ? Mathf.Max(1, Mathf.FloorToInt((list.rect.width + colGap) / stepX)) : 1;
            var rows = stepY > 0 ? Mathf.Max(1, Mathf.FloorToInt((list.rect.height + lineGap) / stepY)) : 1;

            for (var i = 0; i < count; i++)
            {
                var item = GetItem(container);
                var rt = (RectTransform)item.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
                var (col, row) = layout switch
                {
                    ListLayoutType.SingleRow => (i, 0),
                    ListLayoutType.FlowHorizontal or ListLayoutType.Pagination => (i % columns, i / columns),
                    ListLayoutType.FlowVertical => (i / rows, i % rows),
                    _ => (0, i), // SingleColumn
                };
                rt.anchoredPosition = new Vector2(col * stepX, -row * stepY);
                setup(item, i);
            }
            PlacePoolRootLast();
            list.GetComponent<ScrollPane>()?.RefreshContent();
            if (rebindSelection && list.GetComponent<ListSelection>() is { enabled: true } selection)
                selection.Rebind();
        }

        private GameObject GetItem(RectTransform parent)
        {
            EnsurePool();
            var item = _pool.Get();
            var rt = (RectTransform)item.transform;
            rt.SetParent(parent, false);
            item.SetActive(true);
            return item;
        }

        private void ReleaseItem(GameObject item)
        {
            EnsurePool();
            _pool.Release(item);
        }

        private void PlacePoolRootLast()
        {
            if (_poolRoot != null)
                _poolRoot.SetAsLastSibling();
        }

        private void EnsurePool()
        {
            _pool ??= new ObjectPool<GameObject>(
                () =>
                {
                    var item = Instantiate(itemPrefab, PoolRoot, false);
                    ResetButtons(item);
                    item.SetActive(false);
                    return item;
                },
                null,
                item =>
                {
                    ResetButtons(item);
                    item.transform.SetParent(PoolRoot, false);
                    item.SetActive(false);
                },
                Destroy,
                false);
        }

        private RectTransform PoolRoot
        {
            get
            {
                if (_poolRoot == null)
                {
                    var go = new GameObject(PoolName, typeof(RectTransform));
                    _poolRoot = (RectTransform)go.transform;
                    _poolRoot.SetParent(transform, false);
                    _poolRoot.anchorMin = _poolRoot.anchorMax = _poolRoot.pivot = new Vector2(0, 1);
                    go.SetActive(false);
                }
                return _poolRoot;
            }
        }

        private void ResetButtons(GameObject item)
        {
            _buttons.Clear();
            item.GetComponentsInChildren(true, _buttons);
            foreach (var button in _buttons)
            {
                button.onClick.RemoveAllListeners();
                button.grayed = false;
                button.selected = false;
                button.changeStateOnClick = true;
            }
        }

        private void OnDestroy() => _pool?.Clear();
    }
}
