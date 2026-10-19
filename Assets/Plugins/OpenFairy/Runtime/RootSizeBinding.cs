using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    // A sibling overlay must not inherit the design root's mask or CanvasGroup.
    [DisallowMultipleComponent, AddComponentMenu("")]
    internal sealed class RootSizeBinding : UIBehaviour
    {
        [SerializeField] private Root _root;
        private Rect _sourceRect;
        private Matrix4x4 _sourceMatrix;

        internal void Bind(Root root)
        {
            _root = root;
            Sync();
        }

        internal void Unbind(Root root)
        {
            if (_root == root) _root = null;
        }

        protected override void OnRectTransformDimensionsChange() => Sync();

        private void LateUpdate()
        {
            var source = (RectTransform)transform;
            if (_root != null && (_sourceRect != source.rect || _sourceMatrix != source.localToWorldMatrix))
                Sync();
        }

        private void Sync()
        {
            if (_root == null) return;
            var source = (RectTransform)transform;
            var target = _root.rect;
            // Share native anchors and pivot so ancestor resizing needs no pose arithmetic.
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.anchoredPosition3D = source.anchoredPosition3D;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
            _sourceRect = source.rect;
            _sourceMatrix = source.localToWorldMatrix;
        }
    }
}
