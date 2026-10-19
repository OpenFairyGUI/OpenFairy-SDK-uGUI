using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    // 复刻 FairyGUI 文字描边（未开启 enhancedTextOutlineEffect）：沿四个轴向各复制一份字形，宽度取 effectDistance.x。
    public class TextStroke : Shadow
    {
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;
            var vertices = ListPool<UIVertex>.Get();
            vh.GetUIVertexStream(vertices);
            var width = effectDistance.x;
            int start = 0, end = vertices.Count;
            ApplyShadowZeroAlloc(vertices, effectColor, start, end, -width, 0);
            start = end; end = vertices.Count;
            ApplyShadowZeroAlloc(vertices, effectColor, start, end, width, 0);
            start = end; end = vertices.Count;
            ApplyShadowZeroAlloc(vertices, effectColor, start, end, 0, -width);
            start = end; end = vertices.Count;
            ApplyShadowZeroAlloc(vertices, effectColor, start, end, 0, width);
            vh.Clear();
            vh.AddUIVertexTriangleStream(vertices);
            ListPool<UIVertex>.Release(vertices);
        }
    }
}
