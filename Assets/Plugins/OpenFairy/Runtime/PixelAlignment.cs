using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    [ExecuteAlways, DefaultExecutionOrder(-32000), DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed partial class PixelAlignment : UIBehaviour
    {
        [SerializeField] internal RectTransform coordinateSpace;
        internal static bool importing;
        private static readonly List<PixelAlignment> Active = new();
        private static readonly Pass AlignPass = new();
        private static bool _sorted = true;
        private RectTransform _rect;
        private int _depth;
        private bool _aligned;
        private Vector3 _layoutPosition;
        private Vector3 _renderedPosition;

        protected override void OnEnable()
        {
            base.OnEnable();
            _rect = (RectTransform)transform;
            if (Active.Count == 0) Canvas.preWillRenderCanvases += Queue;
            Active.Add(this);
            UpdateDepth();
            if (!importing) Register();
        }

        protected override void OnDisable()
        {
            Active.Remove(this);
            if (Active.Count == 0) Canvas.preWillRenderCanvases -= Queue;
            Restore();
            base.OnDisable();
        }

        private void Update() => Restore();

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            if (coordinateSpace != null && !transform.IsChildOf(coordinateSpace))
                coordinateSpace = transform.parent as RectTransform;
            UpdateDepth();
        }

        private void UpdateDepth()
        {
            _depth = 0;
            for (var parent = transform.parent; parent != null; parent = parent.parent) _depth++;
            _sorted = false;
        }

        private void Restore()
        {
            if (!_aligned) return;
#if UNITY_EDITOR
            if (_previewing)
            {
                var position = _rect.anchoredPosition3D;
                EditorPropertyPreview.End(this);
                if (!position.Equals(_renderedPosition)) _rect.anchoredPosition3D = position;
                _previewing = false;
                _aligned = false;
                return;
            }
#endif
            if (_rect.anchoredPosition3D.Equals(_renderedPosition)) _rect.anchoredPosition3D = _layoutPosition;
            _aligned = false;
        }

        // A single layout element aligns every instance parent-first, after the PostLayout writers above them.
        private static void Queue()
        {
            // An earlier subscriber in the same invocation can disable the last instance, e.g. a scrollbar hiding its grip.
            if (importing || Active.Count == 0) return;
            foreach (var alignment in Active) alignment.Restore();
            Register();
        }

        private static void Register()
        {
            if (!_sorted)
            {
                Active.Sort(static (a, b) => a._depth.CompareTo(b._depth));
                _sorted = true;
            }
            AlignPass.transform = Active[^1]._rect;
            CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(AlignPass);
        }

        private sealed class Pass : ICanvasElement
        {
            public Transform transform { get; set; }

            public void Rebuild(CanvasUpdate executing)
            {
                if (executing != CanvasUpdate.PostLayout || importing) return;
                foreach (var alignment in Active) alignment.Align();
            }

            public void LayoutComplete() { }
            public void GraphicUpdateComplete() { }
            public bool IsDestroyed() => false;
        }

        private void Align()
        {
            var space = coordinateSpace != null ? coordinateSpace : _rect.parent as RectTransform;
            if (space == null || _rect.localEulerAngles != Vector3.zero) return;
            var rect = _rect.rect;
            var topLeft = AuthorPoint(_rect, new Vector3(rect.xMin, rect.yMax), space);
            var offset = new Vector3(Mathf.Round(topLeft.x) - topLeft.x, Mathf.Round(topLeft.y) - topLeft.y);
            // Whole-pixel elements keep their layout position; skipping the write leaves static ones untouched.
            if (offset.x == 0 && offset.y == 0) return;
            var delta = space.TransformVector(offset);
            _layoutPosition = _rect.anchoredPosition3D;
#if UNITY_EDITOR
            if (!Application.isPlaying && !editorPositionDriven)
            {
                EditorPropertyPreview.Begin(this, _rect, "m_AnchoredPosition");
                EditorPropertyPreview.Begin(this, _rect, "m_LocalPosition");
                _previewing = true;
            }
#endif
            _rect.anchoredPosition3D += _rect.parent.InverseTransformVector(delta);
            _renderedPosition = _rect.anchoredPosition3D;
            _aligned = true;
        }

        private static Vector3 AuthorPoint(RectTransform rect, Vector3 point, RectTransform space)
        {
            // Cancelling localPosition's parent-pivot offset can move an exact half-pixel across its rounding boundary.
            for (var current = rect; ;)
            {
                var parent = (RectTransform)current.parent;
                var parentRect = parent.rect;
                var anchor = current.anchorMin + Vector2.Scale(current.anchorMax - current.anchorMin, current.pivot);
                point = current.localRotation * Vector3.Scale(current.localScale, point) + current.anchoredPosition3D
                    + new Vector3(parentRect.width * anchor.x, parentRect.height * (anchor.y - 1));
                if (parent == space) return point;
                point += new Vector3(parentRect.xMin, parentRect.yMax);
                current = parent;
            }
        }

        internal static Vector3 LocalPoint(Transform rect, Vector3 point, Transform space)
        {
            for (var current = rect; current != space; current = current.parent)
                point = current.localPosition + current.localRotation * Vector3.Scale(current.localScale, point);
            return point;
        }
    }
}
