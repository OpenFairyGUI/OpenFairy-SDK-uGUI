using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    public class FlipImage : Image
    {
        public bool flipX;
        public bool flipY;

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            base.OnPopulateMesh(toFill);
            if (!flipX && !flipY)
                return;

            var vert = new UIVertex();
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < toFill.currentVertCount; i++)
            {
                toFill.PopulateUIVertex(ref vert, i);
                min = Vector2.Min(min, vert.uv0);
                max = Vector2.Max(max, vert.uv0);
            }

            for (var i = 0; i < toFill.currentVertCount; i++)
            {
                toFill.PopulateUIVertex(ref vert, i);
                if (flipX)
                    vert.uv0.x = min.x + max.x - vert.uv0.x;
                if (flipY)
                    vert.uv0.y = min.y + max.y - vert.uv0.y;
                toFill.SetUIVertex(vert, i);
            }
        }
    }
}
