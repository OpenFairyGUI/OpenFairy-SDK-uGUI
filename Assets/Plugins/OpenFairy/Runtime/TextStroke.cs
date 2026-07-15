using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    public class TextStroke : BaseMeshEffect
    {
        public Color color = Color.black;
        public float width = 1;

        private static readonly Vector2[] Directions = { new(-1, 0), new(1, 0), new(0, -1), new(0, 1) };

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;

            var stream = ListPool<UIVertex>.Get();
            var output = ListPool<UIVertex>.Get();
            vh.GetUIVertexStream(stream);
            vh.Clear();

            foreach (var direction in Directions)
                foreach (var vertex in stream)
                {
                    var copy = vertex;
                    copy.position += (Vector3)(direction * width);
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
