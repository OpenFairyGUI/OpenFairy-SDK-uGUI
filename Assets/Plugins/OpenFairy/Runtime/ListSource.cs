using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    public sealed class ListSource : MonoBehaviour
    {
        internal const string PoolName = "__listPool";
        public GameObject itemPrefab;
        [SerializeField] internal GameObject[] variantItems = Array.Empty<GameObject>();
        public Vector2 itemSize;
        public float lineGap;
        public float colGap;
        public ListLayoutType layout;
        public int lineItemCount;
        public bool autoItemSize = true;
        public ListRenderOrder renderOrder;
        public int apexIndex;
        [SerializeField] internal HorizontalLayoutGroup rowLayout;
        [SerializeField] internal ContentSizeFitter rowSize;
        [SerializeField] internal LayoutElement rowMinimum;
        internal bool hasNativeRowLayout => !_virtual && rowLayout != null && rowLayout.enabled;
        private Vector2[] _nativeRowItemSizes = Array.Empty<Vector2>();
        public TextAnchor alignment;

        private ObjectPool<GameObject> _pool;
        private RectTransform _poolRoot;
        private readonly List<ButtonBase> _buttons = new();
        private readonly Dictionary<int, GameObject> _visible = new();
        private readonly Dictionary<GameObject, int> _indices = new();
        private readonly Dictionary<int, GameObject> _rebased = new();
        private readonly List<int> _release = new();
        private readonly List<(int Order, Transform Item)> _drawOrder = new();
        public ScrollPane scrollPane;
        public RectTransform content;
        private Action<GameObject, int> _setup;
        private Func<GameObject, int, float> _measureHeight;
        private float[] _heights = Array.Empty<float>();
        private float[] _starts = { 0 };
        private Vector2 _viewport;
        private Vector2 _contentOffset;
        private bool _virtual;
        private bool _loop;
        private int _lanes = 1;
        private int _physicalCount;
        private int _centerCycle;

        [field: SerializeField] public int count { get; internal set; }
        public int version { get; private set; }
        public bool isVirtual => _virtual;
        public IReadOnlyDictionary<int, GameObject> visibleItems => _visible;

        // Horizontal loop uses repeated physical slots but binds logical indices.
        // Variable-height rows use Fill's optional measureHeight callback.
        public void SetVirtual(bool loop = false)
        {
            if (loop && layout != ListLayoutType.SingleRow)
                throw new InvalidOperationException("Loop lists must use SingleRow layout.");
            _virtual = true;
            if (rowLayout != null) { rowLayout.enabled = false; rowSize.enabled = false; rowMinimum.enabled = false; }
            _loop = loop;
            scrollPane ??= ScrollPane.Attach((RectTransform)transform);
            scrollPane.loopHorizontal = loop;
            content = scrollPane.content;
            scrollPane.onScroll.RemoveListener(OnScroll);
            scrollPane.onScroll.AddListener(OnScroll);
        }

        public void Fill(int count, Action<GameObject, int> setup, bool rebindSelection = true,
            Func<GameObject, int, float> measureHeight = null)
        {
            if (measureHeight != null && (!_virtual || layout != ListLayoutType.SingleColumn))
                throw new InvalidOperationException("Measured rows require a virtual SingleColumn list.");
            version++;
            if (!_virtual)
            {
                this.count = count;
                FillAll(count, setup, rebindSelection);
                return;
            }

            bool first = _setup == null;
            bool atBottom = !first && (scrollPane.maxScroll.y == 0 || scrollPane.percY >= 1);
            int anchor = FirstPhysicalIndex();
            float anchorOffset = scrollPane.position.y - ItemPosition(anchor).y;
            this.count = count;
            _setup = setup;
            _measureHeight = measureHeight;
            _indices.Clear();
            ReleaseChildren();
            _visible.Clear();
            if (measureHeight != null)
            {
                int previousCount = _heights.Length;
                Array.Resize(ref _heights, count);
                for (int i = previousCount; i < count; i++)
                    _heights[i] = itemSize.y;
                RebuildStarts();
            }

            ConfigureExtent();
            if (_loop && first && count > 0)
                scrollPane.SetPosition(new Vector2(_centerCycle * count * (itemSize.x + colGap), 0));
            else if (measureHeight != null)
                scrollPane.SetPosition(new Vector2(0, atBottom ? scrollPane.maxScroll.y
                    : ItemPosition(Math.Min(anchor, Math.Max(0, count - 1))).y + anchorOffset));
            else
                scrollPane.SetPosition(scrollPane.position);
            UpdateVisible(rebindSelection);
        }

        private void FillAll(int count, Action<GameObject, int> setup, bool rebindSelection)
        {
            var list = (RectTransform)transform;
            var container = List.Container(list);
            var pane = scrollPane;
            Vector2 viewSize = pane != null ? pane.viewportSize : container.rect.size;
            ReleaseChildren();
            _indices.Clear();
            var stepX = itemSize.x + colGap;
            var stepY = itemSize.y + lineGap;
            var columns = lineItemCount > 0 ? lineItemCount
                : stepX > 0 ? Mathf.Max(1, Mathf.FloorToInt((viewSize.x + colGap) / stepX)) : 1;
            var rows = lineItemCount > 0 ? lineItemCount
                : stepY > 0 ? Mathf.Max(1, Mathf.FloorToInt((viewSize.y + lineGap) / stepY)) : 1;
            for (var i = 0; i < count; i++)
            {
                var item = GetItem(container);
                var rt = (RectTransform)item.transform;
                var (col, row) = layout switch
                {
                    ListLayoutType.SingleRow => (i, 0),
                    ListLayoutType.FlowHorizontal or ListLayoutType.Pagination => (i % columns, i / columns),
                    ListLayoutType.FlowVertical => (i / rows, i % rows),
                    _ => (0, i),
                };
                if (!hasNativeRowLayout) rt.anchoredPosition = new Vector2(col * stepX, -row * stepY);
                _indices[item] = i;
                setup(item, i);
            }
            if (hasNativeRowLayout)
            {
                UpdateNativeRowItemSizes();
                UpdateNativeRowSize(viewSize);
                LayoutRebuilder.ForceRebuildLayoutImmediate(container);
                if (rebindSelection && list.GetComponent<ListSelection>() is { enabled: true } rowSelection) rowSelection.Rebind();
                return;
            }
            var contentSize = Vector2.zero;
            foreach (var item in _indices.Keys)
            {
                var rt = (RectTransform)item.transform;
                contentSize = Vector2.Max(contentSize, new Vector2(rt.anchoredPosition.x + rt.rect.width,
                    -rt.anchoredPosition.y + rt.rect.height));
            }
            Vector2 offset = _contentOffset = AlignmentOffset(viewSize, contentSize);
            foreach (var item in _indices.Keys)
                ((RectTransform)item.transform).anchoredPosition += new Vector2(offset.x, -offset.y);
            ApplyRenderOrder();
            if (_poolRoot != null)
                _poolRoot.SetAsLastSibling();
            scrollPane?.RefreshContent();
            if (rebindSelection && list.GetComponent<ListSelection>() is { enabled: true } selection)
                selection.Rebind();
        }

        private bool Horizontal => layout == ListLayoutType.SingleRow || layout == ListLayoutType.FlowVertical;
        private float Position => Horizontal ? scrollPane.position.x : scrollPane.position.y;
        private float ViewExtent => Horizontal ? _viewport.x : _viewport.y;
        private float ItemExtent => Horizontal ? itemSize.x : itemSize.y;
        private float Gap => Horizontal ? colGap : lineGap;
        private float MainOffset => Horizontal ? _contentOffset.x : _contentOffset.y;

        private void ConfigureExtent()
        {
            _viewport = scrollPane.viewportSize;
            float crossSize = Horizontal ? itemSize.y : itemSize.x;
            float crossGap = Horizontal ? lineGap : colGap;
            float crossView = Horizontal ? _viewport.y : _viewport.x;
            _lanes = layout == ListLayoutType.SingleRow || layout == ListLayoutType.SingleColumn ? 1
                : lineItemCount > 0 ? lineItemCount
                : Mathf.Max(1, Mathf.FloorToInt((crossView + crossGap) / (crossSize + crossGap)));
            _physicalCount = count;
            if (_loop && count > 0)
            {
                // Enough periods on both sides even when only one item is available.
                _centerCycle = Mathf.CeilToInt(ViewExtent / (count * (ItemExtent + Gap))) + 1;
                _physicalCount = count * (_centerCycle * 2 + 1);
            }
            float extent = _measureHeight != null ? _starts[count] - (count == 0 ? 0 : Gap)
                : VirtualListLayout.FixedExtent(_physicalCount, _lanes, ItemExtent, Gap);
            int crossLanes = Math.Min(count, _lanes);
            float cross = crossLanes == 0 ? 0 : crossLanes * crossSize + (crossLanes - 1) * crossGap;
            var size = Horizontal ? new Vector2(extent, cross) : new Vector2(cross, extent);
            _contentOffset = AlignmentOffset(_viewport, size);
            scrollPane.SetContentSize(size);
        }

        private Vector2 AlignmentOffset(Vector2 view, Vector2 size) => new(
            Mathf.Max(0, view.x - size.x) * ((int)alignment % 3) / 2,
            Mathf.Max(0, view.y - size.y) * ((int)alignment / 3) / 2);

        private Vector2 ItemPosition(int physical)
        {
            if (_measureHeight != null)
                return _contentOffset + new Vector2(0, _starts[Math.Clamp(physical, 0, _starts.Length - 1)]);
            int line = physical / _lanes;
            int lane = physical % _lanes;
            return _contentOffset + (Horizontal ? new Vector2(line * (itemSize.x + colGap), lane * (itemSize.y + lineGap))
                : new Vector2(lane * (itemSize.x + colGap), line * (itemSize.y + lineGap)));
        }

        private int FirstPhysicalIndex()
        {
            if (count == 0)
                return 0;
            return _measureHeight != null
                ? VirtualListLayout.VariableRange(_starts, Gap, Position - MainOffset, ViewExtent, 0).First
                : VirtualListLayout.FixedRange(_physicalCount, _lanes, ItemExtent, Gap, Position - MainOffset, ViewExtent, 0).First;
        }

        public int firstVisibleIndex => count == 0 ? -1 : hasNativeRowLayout ? NativeRowIndex(false)
            : VirtualListLayout.LogicalIndex(FirstPhysicalIndex(), count);

        public int centeredIndex => count == 0 ? -1 : hasNativeRowLayout ? NativeRowIndex(true) : _loop
            ? VirtualListLayout.LogicalIndex(Mathf.RoundToInt((Position + ViewExtent / 2 - ItemExtent / 2) / (ItemExtent + Gap)), count)
            : firstVisibleIndex;

        private int NativeRowIndex(bool centered)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var view = scrollPane != null ? scrollPane.viewportSize : ((RectTransform)transform).rect.size;
            var left = content.rect.xMin + (scrollPane != null ? scrollPane.position.x : 0);
            var right = left + view.x;
            int index = -1;
            float distance = float.PositiveInfinity;
            foreach (var item in _indices)
            {
                var bounds = NativeRowBounds(item.Key.transform);
                if (!centered)
                {
                    if (bounds.max.x > left && bounds.min.x < right && (index < 0 || item.Value < index)) index = item.Value;
                    continue;
                }
                var delta = Mathf.Abs(bounds.center.x - (left + right) / 2);
                if (delta >= distance) continue;
                index = item.Value;
                distance = delta;
            }
            return index;
        }

        private Bounds NativeRowBounds(Transform item) => RectTransformUtility.CalculateRelativeRectTransformBounds(content, item);

        internal bool TryGetItemIndex(GameObject item, out int index) => _indices.TryGetValue(item, out index);
        public int GetItemIndex(GameObject item) => _indices[item];

        public GameObject GetItemAt(int index)
        {
            if (_pool == null && _indices.Count == 0)
            {
                var authored = List.Container((RectTransform)transform);
                for (var i = 0; i < count; i++) _indices.Add(authored.GetChild(i).gameObject, i);
            }
            foreach (var item in _indices)
                if (item.Value == index) return item.Key;
            throw new KeyNotFoundException($"No bound list item at index {index}.");
        }

        // Allows real async icon/model binders to reject a recycled row or old Fill.
        public bool IsBound(GameObject item, int index, int fillVersion) => version == fillVersion
            && _indices.TryGetValue(item, out int bound) && bound == index;

        private void OnScroll()
        {
            if (_setup == null)
                return;
            if (_loop && count > 0)
            {
                var shift = VirtualListLayout.LoopRebase(Position, count, ItemExtent + Gap, _centerCycle);
                if (shift.SlotShift != 0)
                {
                    scrollPane.Rebase(new Vector2(shift.PixelShift, 0));
                    _rebased.Clear();
                    foreach (var pair in _visible)
                    {
                        int slot = pair.Key - shift.SlotShift;
                        SetItemPosition(pair.Value, slot);
                        _rebased.Add(slot, pair.Value);
                    }
                    _visible.Clear();
                    foreach (var pair in _rebased)
                        _visible.Add(pair.Key, pair.Value);
                }
            }
            UpdateVisible(true);
        }

        private void UpdateVisible(bool rebindSelection)
        {
            // At most one overscan line at either edge. Measurements can expose
            // another row; repeat until the visible interval is fully measured.
            bool repeat;
            do
            {
                repeat = false;
                var range = _measureHeight != null
                    ? VirtualListLayout.VariableRange(_starts, Gap, Position - MainOffset, ViewExtent)
                    : VirtualListLayout.FixedRange(_physicalCount, _lanes, ItemExtent, Gap, Position - MainOffset, ViewExtent);
                _release.Clear();
                foreach (var pair in _visible)
                    if (pair.Key < range.First || pair.Key >= range.End)
                        _release.Add(pair.Key);
                foreach (int slot in _release)
                {
                    var item = _visible[slot];
                    _indices.Remove(item);
                    _pool.Release(item);
                    _visible.Remove(slot);
                }
                int anchor = FirstPhysicalIndex();
                float offset = _measureHeight == null ? 0 : Position - ItemPosition(anchor).y;
                bool atBottom = _measureHeight != null && (scrollPane.maxScroll.y == 0 || scrollPane.percY >= 1);
                for (int slot = range.First; slot < range.End; slot++)
                {
                    if (_visible.ContainsKey(slot))
                        continue;
                    var item = GetItem(content);
                    int index = VirtualListLayout.LogicalIndex(slot, count);
                    _visible.Add(slot, item);
                    _indices[item] = index;
                    SetItemPosition(item, slot);
                    _setup(item, index);
                    if (_measureHeight != null)
                    {
                        float height = _measureHeight(item, index);
                        ((RectTransform)item.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                        if (_heights[index] != height)
                        {
                            _heights[index] = height;
                            repeat = true;
                        }
                    }
                }
                if (repeat)
                {
                    RebuildStarts();
                    ConfigureExtent();
                    scrollPane.Rebase(new Vector2(0, Position - (atBottom ? scrollPane.maxScroll.y : ItemPosition(anchor).y + offset)));
                }
                foreach (var pair in _visible)
                    SetItemPosition(pair.Value, pair.Key);
            } while (repeat);
            ApplyRenderOrder();
            if (rebindSelection && TryGetComponent<ListSelection>(out var selection) && selection.enabled)
                selection.Rebind();
        }

        // 作者项按作者顺序编号后再应用绘制顺序，选择索引不随兄弟序改变。
        private void Awake()
        {
            if (renderOrder == ListRenderOrder.Ascent)
                return;
            var authored = List.Container((RectTransform)transform);
            for (var i = 0; i < count; i++)
                _indices[authored.GetChild(i).gameObject] = i;
            ApplyRenderOrder();
        }

        // 复刻 GComponent.BuildNativeDisplayList：只改兄弟序（绘制先后），项的索引与位置不变。
        private void ApplyRenderOrder()
        {
            if (renderOrder == ListRenderOrder.Ascent)
                return;
            _drawOrder.Clear();
            if (_virtual)
                foreach (var pair in _visible) _drawOrder.Add((pair.Key, pair.Value.transform));
            else
                foreach (var pair in _indices) _drawOrder.Add((pair.Value, pair.Key.transform));
            _drawOrder.Sort((a, b) => a.Order.CompareTo(b.Order));
            var last = _drawOrder.Count - 1;
            var apex = Mathf.Clamp(apexIndex, 0, last + 1);
            for (var i = 0; i <= last; i++)
                _drawOrder[i].Item.SetSiblingIndex(renderOrder == ListRenderOrder.Descent ? last - i
                    : i < apex ? i : apex + last - i);
        }

        private void SetItemPosition(GameObject item, int slot)
        {
            Vector2 point = ItemPosition(slot);
            ((RectTransform)item.transform).anchoredPosition = new Vector2(point.x, -point.y);
        }

        private void RebuildStarts()
        {
            Array.Resize(ref _starts, count + 1);
            _starts[0] = 0;
            for (int i = 0; i < count; i++)
                _starts[i + 1] = _starts[i] + _heights[i] + lineGap;
        }

        public void ScrollToIndex(int index, bool center = false, bool animated = false)
        {
            if (index < 0 || index >= count)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (hasNativeRowLayout)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                foreach (var item in _indices)
                    if (item.Value == index)
                    {
                        var rect = (RectTransform)item.Key.transform;
                        var bounds = NativeRowBounds(rect);
                        if (center || bounds.size.x > scrollPane.viewportSize.x)
                        {
                            var x = center ? bounds.center.x - content.rect.xMin - scrollPane.viewportSize.x / 2
                                : bounds.min.x - content.rect.xMin;
                            scrollPane.SetPosition(new Vector2(x, scrollPane.position.y), animated);
                        }
                        else scrollPane.ScrollToView(rect, animated);
                        return;
                    }
                return;
            }
            int slot = index;
            if (_loop)
            {
                int cycle = Mathf.RoundToInt((Position + ViewExtent / 2 - ItemExtent / 2 - index * (ItemExtent + Gap))
                    / (count * (ItemExtent + Gap)));
                slot += cycle * count;
            }
            Vector2 point = ItemPosition(slot);
            float start = Horizontal ? point.x : point.y;
            float size = _measureHeight != null ? _heights[index] : ItemExtent;
            float position = center ? start + size / 2 - ViewExtent / 2
                : size > ViewExtent || start < Position ? start
                : start + size > Position + ViewExtent ? start + size - ViewExtent : Position;
            scrollPane.SetPosition(Horizontal ? new Vector2(position, scrollPane.position.y) : new Vector2(scrollPane.position.x, position), animated);
        }

        internal Vector2 SnapPosition(Vector2 position)
        {
            if (count == 0) return Vector2.zero;
            if (hasNativeRowLayout)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                float closest = position.x;
                float distance = float.PositiveInfinity;
                foreach (var item in _indices.Keys)
                {
                    var bounds = NativeRowBounds(item.transform);
                    var start = bounds.min.x - content.rect.xMin;
                    var delta = Mathf.Abs(position.x - start);
                    if (delta >= distance) continue;
                    closest = start;
                    distance = delta;
                }
                position.x = closest;
                return position;
            }
            var axis = Horizontal ? 0 : 1;
            if (_measureHeight == null)
                position[axis] = MainOffset + Mathf.Round((position[axis] - MainOffset) / (ItemExtent + Gap)) * (ItemExtent + Gap);
            else
            {
                var value = position.y - MainOffset;
                var index = Array.BinarySearch(_starts, value);
                if (index < 0)
                {
                    index = ~index;
                    if (index > 0 && (index == _starts.Length || value - _starts[index - 1] < _starts[index] - value)) index--;
                }
                position.y = MainOffset + _starts[Math.Min(index, count - 1)];
            }
            return position;
        }

        private bool UpdateNativeRowItemSizes()
        {
            if (_nativeRowItemSizes.Length != _indices.Count) _nativeRowItemSizes = new Vector2[_indices.Count];
            bool changed = false;
            foreach (var item in _indices)
            {
                var size = ((RectTransform)item.Key.transform).rect.size;
                if (_nativeRowItemSizes[item.Value] == size) continue;
                _nativeRowItemSizes[item.Value] = size;
                changed = true;
            }
            return changed;
        }

        private void UpdateNativeRowSize(Vector2 viewport)
        {
            rowMinimum.minWidth = viewport.x;
            rowMinimum.minHeight = viewport.y;
            if (autoItemSize)
                foreach (var item in _indices.Keys)
                    ((RectTransform)item.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewport.y);
            _viewport = viewport;
        }

        private void LateUpdate()
        {
            if (hasNativeRowLayout)
            {
                var view = scrollPane != null ? scrollPane.viewportSize : ((RectTransform)transform).rect.size;
                if (_viewport != view || _nativeRowItemSizes.Length != _indices.Count) UpdateNativeRowSize(view);
                if (UpdateNativeRowItemSizes()) LayoutRebuilder.MarkLayoutForRebuild(content);
                return;
            }
            if (_virtual && _setup != null && _viewport != scrollPane.viewportSize)
                Fill(count, _setup, true, _measureHeight);
            else if (_measureHeight != null)
            {
                int anchor = FirstPhysicalIndex();
                float offset = Position - ItemPosition(anchor).y;
                bool atBottom = scrollPane.maxScroll.y == 0 || scrollPane.percY >= 1;
                bool changed = false;
                foreach (var pair in _visible)
                {
                    int index = _indices[pair.Value];
                    float height = _measureHeight(pair.Value, index);
                    if (_heights[index] == height)
                        continue;
                    _heights[index] = height;
                    ((RectTransform)pair.Value.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                    changed = true;
                }
                if (changed)
                {
                    RebuildStarts();
                    ConfigureExtent();
                    scrollPane.Rebase(new Vector2(0, Position - (atBottom ? scrollPane.maxScroll.y : ItemPosition(anchor).y + offset)));
                    UpdateVisible(true);
                }
            }
        }

        private void ReleaseChildren()
        {
            var content = List.Container((RectTransform)transform);
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i);
                if (child.name != PoolName)
                {
                    if (Array.IndexOf(variantItems, child.gameObject) >= 0)
                    {
                        child.gameObject.SetActive(false);
                        child.SetParent(PoolRoot, false);
                        continue;
                    }
                    EnsurePool();
                    _pool.Release(child.gameObject);
                }
            }
        }

        private GameObject GetItem(RectTransform parent)
        {
            EnsurePool();
            var item = _pool.Get();
            var rt = (RectTransform)item.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            item.SetActive(true);
            return item;
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
                button.grayed = false;
                button.selected = false;
                button.changeStateOnClick = true;
            }
        }

        private void OnDestroy()
        {
            if (scrollPane != null)
                scrollPane.onScroll.RemoveListener(OnScroll);
            _pool?.Clear();
        }
    }
}
