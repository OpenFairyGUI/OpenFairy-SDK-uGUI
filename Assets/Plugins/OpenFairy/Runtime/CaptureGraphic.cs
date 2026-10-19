using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    // A render-only snapshot: no source behaviour or text/layout engine is copied.
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class CaptureGraphic : MaskableGraphic
    {
        private Mesh _mesh;
        private Texture _texture;
        public override Texture mainTexture => _texture;

        internal void Bind(Mesh mesh, Texture texture, Material snapshotMaterial, bool isMaskable)
        {
            _mesh = mesh;
            _texture = texture;
            material = snapshotMaterial;
            maskable = isMaskable;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void UpdateGeometry() => canvasRenderer.SetMesh(_mesh);
    }
}
