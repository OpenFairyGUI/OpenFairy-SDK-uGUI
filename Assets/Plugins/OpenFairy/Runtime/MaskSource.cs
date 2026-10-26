using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    // Keep the authored object's mesh and controller bindings without drawing it as content.
    [DisallowMultipleComponent]
    internal sealed class MaskSource : BaseMeshEffect
    {
        internal readonly List<UIVertex> vertices = new();
        internal Graphic sourceGraphic => graphic;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            vertices.Clear();
            vh.GetUIVertexStream(vertices);
            vh.Clear();
        }
    }
}
