using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace NanamiUI
{
    public class FlipImage : Image
    {
        public bool flipX;
        public bool flipY;

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            // 平铺：按 FairyGUI 语义直接生成单 quad（贴图 wrapMode=Repeat，UV 从原点铺出 N 格），把 flip 并进 UV。
            // FairyGUI 从内容左上角起铺、残缺格落在右下；单 quad 横向原点天然在左边（一致），但纵向原点
            // 落在底边（Unity Y-up），需把顶边对齐到整格边界，让残缺格回到底边。Unity 自带 Tiled 缺这步，
            // 分数格平铺（翻转与否都）会纵向差半格。
            if (type == Type.Tiled)
            {
                var rect = GetPixelAdjustedRect();
                var uv = DataUtility.GetOuterUV(sprite);
                var nx = rect.width * multipliedPixelsPerUnit / sprite.rect.width;
                var ny = rect.height * multipliedPixelsPerUnit / sprite.rect.height;
                var u0 = flipX ? uv.z : uv.x;
                var u1 = u0 + (flipX ? uv.x - uv.z : uv.z - uv.x) * nx;
                var v0 = flipY ? uv.w : uv.y;
                var v1 = v0 + (flipY ? uv.y - uv.w : uv.w - uv.y) * ny;
                var dv = Mathf.Floor(v1) - v1;
                v0 += dv;
                v1 += dv;
                Color32 c = color;
                toFill.Clear();
                toFill.AddVert(new Vector3(rect.xMin, rect.yMin), c, new Vector2(u0, v0));
                toFill.AddVert(new Vector3(rect.xMin, rect.yMax), c, new Vector2(u0, v1));
                toFill.AddVert(new Vector3(rect.xMax, rect.yMax), c, new Vector2(u1, v1));
                toFill.AddVert(new Vector3(rect.xMax, rect.yMin), c, new Vector2(u1, v0));
                toFill.AddTriangle(0, 1, 2);
                toFill.AddTriangle(2, 3, 0);
                return;
            }

            base.OnPopulateMesh(toFill);
            if (!flipX && !flipY)
                return;

            // 非平铺：绕 sprite 自身 UV 区间镜像每个顶点即可。
            var outer = DataUtility.GetOuterUV(sprite);
            var vert = new UIVertex();
            for (var i = 0; i < toFill.currentVertCount; i++)
            {
                toFill.PopulateUIVertex(ref vert, i);
                if (flipX)
                    vert.uv0.x = outer.x + outer.z - vert.uv0.x;
                if (flipY)
                    vert.uv0.y = outer.y + outer.w - vert.uv0.y;
                toFill.SetUIVertex(vert, i);
            }
        }
    }
}
