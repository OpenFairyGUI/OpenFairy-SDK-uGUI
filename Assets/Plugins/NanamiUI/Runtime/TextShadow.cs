using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace NanamiUI
{
    public class TextShadow : BaseMeshEffect
    {
        public Color color = Color.black;
        public Vector2 offset = new(1, 1);

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;

            var stream = ListPool<UIVertex>.Get();
            var output = ListPool<UIVertex>.Get();
            vh.GetUIVertexStream(stream);
            vh.Clear();

            foreach (var vertex in stream)
            {
                var copy = vertex;
                copy.position += new Vector3(offset.x, -offset.y);
                copy.color = color;
                output.Add(copy);
            }
            output.AddRange(stream);
            vh.AddUIVertexTriangleStream(output);
            ListPool<UIVertex>.Release(output);
            ListPool<UIVertex>.Release(stream);
        }
    }
}
