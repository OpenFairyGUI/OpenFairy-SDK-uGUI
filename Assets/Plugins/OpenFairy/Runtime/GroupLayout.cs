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

        public GroupLayoutAxis axis { get => _axis; set => SetProperty(ref _axis, value); }
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

        public override void SetLayoutHorizontal() => Arrange(0);
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
