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

            // 九宫格 + 翻转：uGUI 的 base 只镜像 UV，却保留未翻转的边条厚度，非对称边框会错位。
            // FairyGUI 同时翻转 grid rect（Image.cs:298-311）并喂入预翻转的 uvRect（NGraphics.cs:711-732），此处一并复刻。
            if (type == Type.Sliced && (flipX || flipY) && sprite != null && sprite.border != Vector4.zero)
            {
                SliceFillFlipped(toFill);
                return;
            }

            base.OnPopulateMesh(toFill);
            if (!flipX && !flipY)
                return;

            // 非平铺（Simple/Filled）：绕 sprite 自身 UV 区间镜像每个顶点即可。
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

        // grid rect 翻转 + uvRect 预翻转的合并效果：翻转轴上两条边框的屏幕厚度互换、内容镜像。
        private void SliceFillFlipped(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var outer = DataUtility.GetOuterUV(sprite); // (uMin, vMin, uMax, vMax)
            var bd = sprite.border;                     // px: (left, bottom, right, top)
            float sw = sprite.rect.width, sh = sprite.rect.height;
            var ppu = multipliedPixelsPerUnit;

            // 屏幕边框厚度（本地单位），翻转轴上互换。
            float left = (flipX ? bd.z : bd.x) / ppu, right = (flipX ? bd.x : bd.z) / ppu;
            float top = (flipY ? bd.y : bd.w) / ppu, bottom = (flipY ? bd.w : bd.y) / ppu;

            // X 断点（左→右），带 FairyGUI/Unity 相同的比例收缩兜底规则。
            float x0 = rect.xMin, x3 = rect.xMax, x1, x2;
            if (rect.width >= left + right) { x1 = x0 + left; x2 = x3 - right; }
            else { var t = rect.width * left / (left + right); x1 = x2 = x0 + t; }

            // Y 断点（上→下，Y 向上）。
            float y0 = rect.yMax, y3 = rect.yMin, y1, y2;
            if (rect.height >= top + bottom) { y1 = y0 - top; y2 = y3 + bottom; }
            else { var t = rect.height * top / (top + bottom); y1 = y2 = y0 - t; }

            // UV 网格线（未翻转），再按 flip 反转 = 预翻转 uvRect。
            float su = (outer.z - outer.x) / sw, sv = (outer.w - outer.y) / sh;
            float ua = outer.x, ub = outer.x + bd.x * su, uc = outer.z - bd.z * su, ud = outer.z;
            float va = outer.w, vb = outer.w - bd.w * sv, vc = outer.y + bd.y * sv, vd = outer.y;
            var us = flipX ? new[] { ud, uc, ub, ua } : new[] { ua, ub, uc, ud };
            var vs = flipY ? new[] { vd, vc, vb, va } : new[] { va, vb, vc, vd };
            var xs = new[] { x0, x1, x2, x3 };
            var ys = new[] { y0, y1, y2, y3 };

            Color32 c = color;
            for (var r = 0; r < 4; r++)
                for (var col = 0; col < 4; col++)
                    vh.AddVert(new Vector3(xs[col], ys[r]), c, new Vector2(us[col], vs[r]));
            for (var r = 0; r < 3; r++)
                for (var col = 0; col < 3; col++)
                {
                    int tl = r * 4 + col, tr = tl + 1, bl = tl + 4, br = bl + 1;
                    vh.AddTriangle(tl, tr, br);
                    vh.AddTriangle(br, bl, tl);
                }
        }
    }
}
