using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    public class TextStroke : BaseMeshEffect
    {
        public Color color = Color.black;
        public float width = 1;

        private static readonly Vector2[] Directions = { new(-1, 0), new(1, 0), new(0, -1), new(0, 1) };
        private static readonly List<UIVertex> Stream = new();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;

            Stream.Clear();
            vh.GetUIVertexStream(Stream);
            vh.Clear();

            var output = new List<UIVertex>(Stream.Count * 5);
            foreach (var direction in Directions)
                foreach (var vertex in Stream)
                {
                    var copy = vertex;
                    copy.position += (Vector3)(direction * width);
                    copy.color = color;
                    output.Add(copy);
                }
            output.AddRange(Stream);
            vh.AddUIVertexTriangleStream(output);
        }
    }
}
