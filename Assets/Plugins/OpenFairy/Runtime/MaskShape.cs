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

        // Source mesh and texture changes arrive through the source's dirty callbacks; placement, ancestor alpha and
        // activation have no callback, so they are compared before each canvas update. A frame starts with pixel-snapped
        // elements restored to their layout positions while the mesh is built after snapping, so the inputs are unchanged
        // when they match either the previous pre-update sample or the last build. Rebuilding unconditionally would dirty
        // the whole canvas every frame.
        private bool _sourceActive;
        private Matrix4x4 _matrix, _builtMatrix;
        private float _alpha, _builtAlpha;

        public override Texture mainTexture => source != null ? source.sourceGraphic.mainTexture : Texture2D.whiteTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            Canvas.preWillRenderCanvases += Refresh;
            if (source == null) return;
            source.sourceGraphic.RegisterDirtyVerticesCallback(SetVerticesDirty);
            source.sourceGraphic.RegisterDirtyMaterialCallback(SetMaterialDirty);
        }

        protected override void OnDisable()
        {
            Canvas.preWillRenderCanvases -= Refresh;
            if (source != null)
            {
                source.sourceGraphic.UnregisterDirtyVerticesCallback(SetVerticesDirty);
                source.sourceGraphic.UnregisterDirtyMaterialCallback(SetMaterialDirty);
            }
            base.OnDisable();
        }

        private void Refresh()
        {
            if (source == null) return;
            var active = source.isActiveAndEnabled;
            var matrix = active ? Matrix() : default;
            var alpha = active ? Alpha() : 0;
            var unchanged = active == _sourceActive
                && (matrix.Equals(_matrix) && alpha == _alpha || matrix.Equals(_builtMatrix) && alpha == _builtAlpha);
            _sourceActive = active;
            _matrix = matrix;
            _alpha = alpha;
            if (!unchanged) SetVerticesDirty();
        }

        private Matrix4x4 Matrix() => rectTransform.worldToLocalMatrix * source.transform.localToWorldMatrix;

        private float Alpha()
        {
            var alpha = 1f;
            for (var ancestor = source.transform; ancestor != transform; ancestor = ancestor.parent)
            {
                ancestor.GetComponents(_groups);
                foreach (var group in _groups)
                    if (group.isActiveAndEnabled) alpha *= group.alpha;
            }
            return alpha;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (source == null || !source.isActiveAndEnabled) return;
            source.sourceGraphic.Rebuild(CanvasUpdate.PreRender);
            var matrix = _builtMatrix = Matrix();
            var alpha = _builtAlpha = Alpha();
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
