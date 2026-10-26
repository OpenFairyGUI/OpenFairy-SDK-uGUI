using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    // The stencil owns the content hierarchy; only its mesh follows the authored mask.
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class MaskShape : MaskableGraphic
    {
        [SerializeField] internal MaskSource source;
        private readonly List<UIVertex> _vertices = new();
        private readonly List<CanvasGroup> _groups = new();

        public override Texture mainTexture => source != null ? source.sourceGraphic.mainTexture : Texture2D.whiteTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            Canvas.preWillRenderCanvases += Refresh;
        }

        protected override void OnDisable()
        {
            Canvas.preWillRenderCanvases -= Refresh;
            base.OnDisable();
        }

        private void Refresh()
        {
            SetVerticesDirty();
            SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (source == null || !source.isActiveAndEnabled) return;
            source.sourceGraphic.Rebuild(CanvasUpdate.PreRender);
            var matrix = rectTransform.worldToLocalMatrix * source.transform.localToWorldMatrix;
            var alpha = 1f;
            for (var ancestor = source.transform; ancestor != transform; ancestor = ancestor.parent)
            {
                ancestor.GetComponents(_groups);
                foreach (var group in _groups)
                    if (group.isActiveAndEnabled) alpha *= group.alpha;
            }
            _vertices.Clear();
            foreach (var original in source.vertices)
            {
                var vertex = original;
                vertex.position = matrix.MultiplyPoint3x4(vertex.position);
                vertex.color.a = (byte)(vertex.color.a * alpha);
                _vertices.Add(vertex);
            }
            vh.AddUIVertexTriangleStream(_vertices);
        }
    }
}
