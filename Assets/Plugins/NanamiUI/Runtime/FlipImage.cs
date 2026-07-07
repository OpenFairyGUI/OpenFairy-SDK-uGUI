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
            base.OnPopulateMesh(toFill);
            if (!flipX && !flipY)
                return;

            // 绕 sprite 自身的 UV 区间镜像（而非整块 mesh 的跨度），平铺时才能保持瓦片相位与 FairyGUI 一致。
            var uv = DataUtility.GetOuterUV(sprite);
            var vert = new UIVertex();
            for (var i = 0; i < toFill.currentVertCount; i++)
            {
                toFill.PopulateUIVertex(ref vert, i);
                if (flipX)
                    vert.uv0.x = uv.x + uv.z - vert.uv0.x;
                if (flipY)
                    vert.uv0.y = uv.y + uv.w - vert.uv0.y;
                toFill.SetUIVertex(vert, i);
            }
        }
    }
}
