using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    [ExecuteAlways, DefaultExecutionOrder(-32000), DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed partial class PixelAlignment : UIBehaviour, ICanvasElement
    {
        [SerializeField] internal RectTransform coordinateSpace;
        internal static bool importing;
        private RectTransform _rect;
        private bool _aligned;
        private Vector3 _layoutPosition;
        private Vector3 _renderedPosition;

        protected override void OnEnable()
        {
            base.OnEnable();
            _rect = (RectTransform)transform;
            Canvas.preWillRenderCanvases += Queue;
            Queue();
        }

        protected override void OnDisable()
        {
            Canvas.preWillRenderCanvases -= Queue;
            CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
            Restore();
            base.OnDisable();
        }

        private void Update() => Restore();

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            if (coordinateSpace != null && !transform.IsChildOf(coordinateSpace))
                coordinateSpace = transform.parent as RectTransform;
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

        private void Queue()
        {
            if (importing) return;
            Restore();
            CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
        }

        public void Rebuild(CanvasUpdate executing)
        {
            if (executing != CanvasUpdate.PostLayout || importing || !IsActive()) return;
            var space = coordinateSpace != null ? coordinateSpace : _rect.parent as RectTransform;
            if (space == null || _rect.localEulerAngles != Vector3.zero) return;
            var topLeft = AuthorPoint(_rect, new Vector3(_rect.rect.xMin, _rect.rect.yMax), space);
            var delta = space.TransformVector(new Vector3(Mathf.Round(topLeft.x) - topLeft.x, Mathf.Round(topLeft.y) - topLeft.y));
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

        public void LayoutComplete() { }
        public void GraphicUpdateComplete() { }
        bool ICanvasElement.IsDestroyed() => this == null;

        private static Vector3 AuthorPoint(RectTransform rect, Vector3 point, RectTransform space)
        {
            // Cancelling localPosition's parent-pivot offset can move an exact half-pixel across its rounding boundary.
            for (var current = rect; ;)
            {
                var parent = (RectTransform)current.parent;
                var anchor = current.anchorMin + Vector2.Scale(current.anchorMax - current.anchorMin, current.pivot);
                point = current.localRotation * Vector3.Scale(current.localScale, point) + current.anchoredPosition3D
                    + new Vector3(parent.rect.width * anchor.x, parent.rect.height * (anchor.y - 1));
                if (parent == space) return point;
                point += new Vector3(parent.rect.xMin, parent.rect.yMax);
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
