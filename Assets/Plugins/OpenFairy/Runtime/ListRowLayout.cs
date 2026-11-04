using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    public sealed class ListRowLayout : HorizontalLayoutGroup
    {
        public bool foldInvisibleItems;
        private float _width, _height;

        public override void CalculateLayoutInputHorizontal()
        {
            m_Tracker.Clear();
            rectChildren.Clear();
            var ignorers = ListPool<UnityEngine.Component>.Get();
            foreach (RectTransform child in rectTransform)
            {
                if (foldInvisibleItems && !child.gameObject.activeSelf) continue;
                child.GetComponents(typeof(ILayoutIgnorer), ignorers);
                var include = ignorers.Count == 0;
                foreach (var component in ignorers)
                    if (!((ILayoutIgnorer)component).ignoreLayout) { include = true; break; }
                if (include) rectChildren.Add(child);
            }
            ListPool<UnityEngine.Component>.Release(ignorers);
            _width = 0;
            foreach (var child in rectChildren)
            {
                if (_width != 0) _width += spacing;
                _width += Mathf.Ceil(child.rect.width);
            }
            Measure(_width + padding.horizontal, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            _height = 0;
            foreach (var child in rectChildren) _height = Mathf.Max(_height, Mathf.Ceil(child.rect.height));
            Measure(_height + padding.vertical, 1);
        }

        private void Measure(float extent, int axis)
        {
#if UNITY_6000_6_OR_NEWER
            SetLayoutInputForAxis(extent, float.PositiveInfinity, extent, 0, axis);
#else
            SetLayoutInputForAxis(extent, extent, 0, axis);
#endif
        }

        public override void SetLayoutHorizontal()
        {
            var gap = Mathf.Max(0, rectTransform.rect.width - padding.horizontal - _width);
            var alignment = (int)childAlignment % 3;
            var offset = padding.left + (alignment == 1 ? (int)(gap / 2) : alignment == 2 ? gap : 0);
            float cursor = 0;
            for (var i = 0; i < rectChildren.Count; i++)
            {
                var child = rectChildren[reverseArrangement ? rectChildren.Count - 1 - i : i];
                if (cursor != 0) cursor += spacing;
                SetChildAlongAxis(child, 0, offset + cursor);
                cursor += Mathf.Ceil(child.rect.width);
            }
        }

        public override void SetLayoutVertical()
        {
            var gap = Mathf.Max(0, rectTransform.rect.height - padding.vertical - _height);
            var alignment = (int)childAlignment / 3;
            var position = padding.top + (alignment == 1 ? (int)(gap / 2) : alignment == 2 ? gap : 0);
            foreach (var child in rectChildren) SetChildAlongAxis(child, 1, position);
        }
    }
}
