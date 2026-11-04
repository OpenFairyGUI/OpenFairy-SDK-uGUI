using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    [ExecuteAlways, DefaultExecutionOrder(-32000), DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class PixelAlignment : UIBehaviour, ICanvasElement
    {
        [SerializeField] internal RectTransform coordinateSpace;
        internal static bool importing;
        private RectTransform _rect;
        private Vector3 _offset;
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
            if (_offset == Vector3.zero) return;
            if (_rect.localPosition.Equals(_renderedPosition)) _rect.localPosition = _layoutPosition;
            _offset = Vector3.zero;
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
            var topLeft = LocalPoint(_rect, new Vector3(_rect.rect.xMin, _rect.rect.yMax), space)
                - new Vector3(space.rect.xMin, space.rect.yMax);
            var delta = space.TransformVector(new Vector3(Mathf.Round(topLeft.x) - topLeft.x, Mathf.Round(topLeft.y) - topLeft.y));
            _offset = _rect.parent != null ? _rect.parent.InverseTransformVector(delta) : delta;
            _layoutPosition = _rect.localPosition;
            _rect.localPosition += _offset;
            _renderedPosition = _rect.localPosition;
        }

        public void LayoutComplete() { }
        public void GraphicUpdateComplete() { }
        bool ICanvasElement.IsDestroyed() => this == null;

        internal static Vector3 LocalPoint(Transform rect, Vector3 point, Transform space)
        {
            for (var current = rect; current != space; current = current.parent)
                point = current.localPosition + current.localRotation * Vector3.Scale(current.localScale, point);
            return point;
        }
    }
}
