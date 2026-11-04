using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    public enum GroupLayoutAxis { Horizontal, Vertical }

    // Authored groups flow on one axis and preserve each member's offset on the other.
    public sealed class GroupLayout : LayoutGroup
    {
        [SerializeField] private GroupLayoutAxis _axis;
        [SerializeField] private float _spacing;
        [SerializeField] private bool _excludeInvisibles;
        [SerializeField] private int _mainGridIndex = -1;
        [SerializeField] private float _mainGridMinSize = 50;
        public int mainGridIndex { get => _mainGridIndex; set { if (_mainGridIndex == value) return; _members = null; SetProperty(ref _mainGridIndex, value); } }
        public float mainGridMinSize { get => _mainGridMinSize; set { if (_mainGridMinSize == value) return; _members = null; SetProperty(ref _mainGridMinSize, value); } }
        public bool autoSizeDisabled;
        private Vector2 _previousSize;
        private bool _resizing;
        [System.Serializable]
        private struct Member
        {
            public RectTransform rect;
            public float size, percent;
        }
        [SerializeField] private Member[] _members;
        [SerializeField] private float _totalSize;

        protected override void OnEnable()
        {
            _previousSize = rectTransform.rect.size;
            base.OnEnable();
        }

        protected override void OnTransformChildrenChanged()
        {
            _members = null;
            base.OnTransformChildrenChanged();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            var size = rectTransform.rect.size;
            var delta = size - _previousSize;
            _previousSize = size;
            base.OnRectTransformDimensionsChange();
            if (_resizing || !IsActive() || CanvasUpdateRegistry.IsRebuildingLayout() || delta == Vector2.zero) return;
            CalculateLayoutInputHorizontal();
            ResizeChildren(delta);
        }

        private void ResizeChildren(Vector2 delta)
        {
            var size = rectTransform.rect.size;
            var axis = (int)_axis;
            if (_members != null && _members.Length == rectChildren.Count)
                for (var i = 0; i < rectChildren.Count; i++)
                    if (_members[i].rect != rectChildren[i] || _members[i].size != rectChildren[i].rect.size[axis])
                    {
                        _members = null;
                        break;
                    }
            if (_members == null || _members.Length != rectChildren.Count)
            {
                _totalSize = 0;
                _members = new Member[rectChildren.Count];
                for (var i = 0; i < rectChildren.Count; i++)
                    _totalSize += rectChildren[i].GetSiblingIndex() == mainGridIndex ? mainGridMinSize : rectChildren[i].rect.size[axis];
                for (var i = 0; i < rectChildren.Count; i++)
                    _members[i] = new Member { rect = rectChildren[i], size = rectChildren[i].rect.size[axis],
                        percent = _totalSize > 0 ? (rectChildren[i].GetSiblingIndex() == mainGridIndex
                            ? mainGridMinSize : rectChildren[i].rect.size[axis]) / _totalSize : 0 };
            }
            _resizing = true;
            try
            {
                var remaining = size[axis] - (rectChildren.Count - 1) * _spacing;
                float percent = 1;
                RectTransform main = null;
                for (var i = 0; i < rectChildren.Count; i++)
                    if (rectChildren[i].GetSiblingIndex() == mainGridIndex && remaining >= _totalSize)
                    {
                        main = rectChildren[i];
                        main.SetSizeWithCurrentAnchors((RectTransform.Axis)axis, remaining - (_totalSize - mainGridMinSize));
                        remaining -= main.rect.size[axis];
                        percent -= _members[i].percent;
                        break;
                    }
                for (var i = 0; i < rectChildren.Count; i++)
                {
                    var child = rectChildren[i];
                    if (child == main) continue;
                    child.SetSizeWithCurrentAnchors((RectTransform.Axis)axis,
                        Mathf.Max(0, Mathf.Round(_members[i].percent / percent * remaining)));
                    percent -= _members[i].percent;
                    remaining -= child.rect.size[axis];
                }
                foreach (RectTransform child in rectTransform)
                    child.SetSizeWithCurrentAnchors((RectTransform.Axis)(1 - axis), Mathf.Max(0, child.rect.size[1 - axis] + delta[1 - axis]));
                Arrange(axis);
                for (var i = 0; i < rectChildren.Count; i++) _members[i].size = rectChildren[i].rect.size[axis];
            }
            finally { _resizing = false; }
        }

        public GroupLayoutAxis axis { get => _axis; set { if (_axis == value) return; _members = null; SetProperty(ref _axis, value); } }
        public float spacing { get => _spacing; set => SetProperty(ref _spacing, value); }
        public bool excludeInvisibles { get => _excludeInvisibles; set => SetProperty(ref _excludeInvisibles, value); }

        public override void CalculateLayoutInputHorizontal()
        {
            rectChildren.Clear();
            var ignorers = ListPool<UnityEngine.Component>.Get();
            for (var i = 0; i < rectTransform.childCount; i++)
            {
                if (rectTransform.GetChild(i) is not RectTransform child
                    || (_excludeInvisibles && !child.gameObject.activeSelf)) continue;
                child.GetComponents(typeof(ILayoutIgnorer), ignorers);
                var include = ignorers.Count == 0;
                foreach (var component in ignorers)
                    if (!((ILayoutIgnorer)component).ignoreLayout) { include = true; break; }
                if (include) rectChildren.Add(child);
            }
            ListPool<UnityEngine.Component>.Release(ignorers);
            m_Tracker.Clear();
            Measure(0);
        }

        public override void CalculateLayoutInputVertical() => Measure(1);

        private void Measure(int dimension)
        {
            var flow = dimension == (int)_axis;
            var cursor = 0f;
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            foreach (var child in rectChildren)
            {
                var size = Size(child, dimension);
                var start = flow ? cursor : CrossPosition(child, dimension, size);
                min = Mathf.Min(min, start);
                max = Mathf.Max(max, start + size);
                // A zero-size member is positioned but consumes neither its size nor a gap.
                if (flow && size != 0) cursor += size + _spacing;
            }
            var extent = rectChildren.Count == 0 ? 0 : max - min;
#if UNITY_6000_6_OR_NEWER
            SetLayoutInputForAxis(extent, float.PositiveInfinity, extent, 0, dimension);
#else
            SetLayoutInputForAxis(extent, extent, 0, dimension);
#endif
        }

        private static float Size(RectTransform child, int dimension)
        {
            if (child.TryGetComponent(out ContentSizeFitter fitter) && fitter.isActiveAndEnabled)
            {
                var fit = dimension == 0 ? fitter.horizontalFit : fitter.verticalFit;
                if (fit == ContentSizeFitter.FitMode.PreferredSize) return LayoutUtility.GetPreferredSize(child, dimension);
                if (fit == ContentSizeFitter.FitMode.MinSize) return LayoutUtility.GetMinSize(child, dimension);
            }
            return child.rect.size[dimension];
        }

        private float CrossPosition(RectTransform child, int dimension, float size) => dimension == 0
            ? child.localPosition.x - child.pivot.x * size - rectTransform.rect.xMin
            : rectTransform.rect.yMax - child.localPosition.y - (1 - child.pivot.y) * size;

        public override void SetLayoutHorizontal()
        {
            if (autoSizeDisabled) ResizeChildren(Vector2.zero);
            Arrange(0);
        }
        public override void SetLayoutVertical() => Arrange(1);

        private void Arrange(int dimension)
        {
            if (dimension != (int)_axis)
            {
                // Updating group bounds must not move the members in parent coordinates.
                var minimum = float.PositiveInfinity;
                foreach (var child in rectChildren) minimum = Mathf.Min(minimum, CrossPosition(child, dimension, Size(child, dimension)));
                if (rectChildren.Count == 0 || Mathf.Abs(minimum) < .0001f) return;
                var delta = rectTransform.TransformVector(dimension == 0
                    ? new Vector3(minimum, 0, 0) : new Vector3(0, -minimum, 0));
                rectTransform.position += delta;
                foreach (var child in rectChildren) child.position -= delta;
                return;
            }

            var cursor = 0f;
            foreach (var child in rectChildren)
            {
                var size = Size(child, dimension);
                m_Tracker.Add(this, child, dimension == 0
                    ? DrivenTransformProperties.AnchorMinX | DrivenTransformProperties.AnchorMaxX | DrivenTransformProperties.AnchoredPositionX
                    : DrivenTransformProperties.AnchorMinY | DrivenTransformProperties.AnchorMaxY | DrivenTransformProperties.AnchoredPositionY);
                child.SetInsetAndSizeFromParentEdge(dimension == 0 ? RectTransform.Edge.Left : RectTransform.Edge.Top, cursor, size);
                if (size != 0) cursor += size + _spacing;
            }
        }
    }
}
