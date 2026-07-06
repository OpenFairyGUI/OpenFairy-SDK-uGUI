using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    public class TextShadow : BaseMeshEffect
    {
        public Color color = Color.black;
        public Vector2 offset = new(1, 1);

        private static readonly List<UIVertex> Stream = new();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;

            Stream.Clear();
            vh.GetUIVertexStream(Stream);
            vh.Clear();

            var output = new List<UIVertex>(Stream.Count * 2);
            foreach (var vertex in Stream)
            {
                var copy = vertex;
                copy.position += new Vector3(offset.x, -offset.y);
                copy.color = color;
                output.Add(copy);
            }
            output.AddRange(Stream);
            vh.AddUIVertexTriangleStream(output);
        }
    }
}
