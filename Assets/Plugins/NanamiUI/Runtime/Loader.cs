using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    public class Loader : MovieClip
    {
        public enum FillType
        {
            None,
            Scale,
            ScaleMatchHeight,
            ScaleMatchWidth,
            ScaleFree,
            ScaleNoBorder,
        }

        public FillType fill;
        public int align;
        public int vAlign;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (type != Type.Simple || overrideSprite == null)
            {
                base.OnPopulateMesh(vh);
                return;
            }

            var rect = GetPixelAdjustedRect();
            var size = overrideSprite.rect.size;
            float sx = 1, sy = 1;
            if (fill != FillType.None)
            {
                sx = rect.width / size.x;
                sy = rect.height / size.y;
                if (fill == FillType.ScaleMatchHeight)
                    sx = sy;
                else if (fill == FillType.ScaleMatchWidth)
                    sy = sx;
                else if (fill == FillType.Scale)
                    sx = sy = Mathf.Min(sx, sy);
                else if (fill == FillType.ScaleNoBorder)
                    sx = sy = Mathf.Max(sx, sy);
            }

            var w = size.x * sx;
            var h = size.y * sy;
            var x = rect.xMin + (align == 1 ? (rect.width - w) / 2 : align == 2 ? rect.width - w : 0);
            var y = rect.yMax - (vAlign == 1 ? (rect.height - h) / 2 : vAlign == 2 ? rect.height - h : 0);
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(overrideSprite);
            var color32 = (Color32)color;

            vh.Clear();
            vh.AddVert(new Vector3(x, y - h), color32, new Vector2(uv.x, uv.y));
            vh.AddVert(new Vector3(x, y), color32, new Vector2(uv.x, uv.w));
            vh.AddVert(new Vector3(x + w, y), color32, new Vector2(uv.z, uv.w));
            vh.AddVert(new Vector3(x + w, y - h), color32, new Vector2(uv.z, uv.y));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
        }
    }
}
