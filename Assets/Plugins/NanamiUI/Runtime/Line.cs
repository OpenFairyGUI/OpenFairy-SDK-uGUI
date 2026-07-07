using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    // 顶点生成复刻 FairyGUI LineMesh：沿 GPath 按弧长铺带状网格，支持宽度曲线、圆头、渐变、fillStart/End。
    // 几何在 FairyGUI y 向下空间算，AddVert 时翻转到 uGUI 局部坐标（同 Shape）。
    public class Line : MaskableGraphic
    {
        public float lineWidth = 2;
        public AnimationCurve lineWidthCurve;
        public Gradient gradient;
        public bool roundEdge;
        public float fillStart;
        public float fillEnd = 1;
        public float pointDensity = 0.1f;
        public Sprite sprite;

        private TransitionPath _path;
        private Rect _rect;
        private VertexHelper _vh;
        private static readonly List<Vector2> Points = new();
        private static readonly List<float> Ts = new();

        public override Texture mainTexture => sprite != null ? sprite.texture : base.mainTexture;

        public void SetPath(IReadOnlyList<TransitionPath.PathPoint> pts)
        {
            _path = new TransitionPath(pts);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_path == null || _path.Length <= 0)
                return;
            _rect = GetPixelAdjustedRect();
            _vh = vh;

            var uvr = sprite != null ? UnityEngine.Sprites.DataUtility.GetOuterUV(sprite) : new Vector4(0, 0, 1, 1);
            var uvMin = new Vector2(uvr.x, uvr.y);
            var uvMax = new Vector2(uvr.z, uvr.w);

            var segCount = _path.SegmentCount;
            var t = 0f;
            var lw = lineWidth;
            for (var si = 0; si < segCount; si++)
            {
                var ratio = _path.GetSegmentLength(si) / _path.Length;
                var t0 = Mathf.Clamp(fillStart - t, 0, ratio) / ratio;
                var t1 = Mathf.Clamp(fillEnd - t, 0, ratio) / ratio;
                if (t0 >= t1)
                {
                    t += ratio;
                    continue;
                }

                Points.Clear();
                Ts.Clear();
                _path.GetPointsInSegment(si, t0, t1, Points, Ts, pointDensity);
                var cnt = Points.Count;

                Color c0 = color, c1 = color;
                if (gradient != null)
                    c0 = gradient.Evaluate(t);
                if (lineWidthCurve != null)
                    lw = lineWidthCurve.Evaluate(t);

                if (roundEdge && si == 0 && t0 == 0)
                    DrawRoundEdge(Points[0], Points[1], lw, c0, uvMin);

                var vertCount = _vh.currentVertCount;
                for (var i = 1; i < cnt; i++)
                {
                    Vector2 p0 = Points[i - 1], p1 = Points[i];
                    var k = vertCount + (i - 1) * 2;
                    var tc = t + ratio * Ts[i];

                    Vector2 widthVector = ((Vector3)Vector3.Cross(p1 - p0, new Vector3(0, 0, 1))).normalized;

                    if (i == 1)
                    {
                        var u0 = Mathf.Lerp(uvMin.x, uvMax.x, t + ratio * Ts[i - 1]);
                        AddVert(p0 - widthVector * (lw * 0.5f), c0, new Vector2(u0, uvMax.y));
                        AddVert(p0 + widthVector * (lw * 0.5f), c0, new Vector2(u0, uvMin.y));
                        if (si != 0) // 与上一段接头
                        {
                            _vh.AddTriangle(k - 2, k - 1, k + 1);
                            _vh.AddTriangle(k - 2, k + 1, k);
                        }
                    }
                    if (gradient != null)
                        c1 = gradient.Evaluate(tc);
                    if (lineWidthCurve != null)
                        lw = lineWidthCurve.Evaluate(tc);

                    var u = Mathf.Lerp(uvMin.x, uvMax.x, tc);
                    AddVert(p1 - widthVector * (lw * 0.5f), c1, new Vector2(u, uvMax.y));
                    AddVert(p1 + widthVector * (lw * 0.5f), c1, new Vector2(u, uvMin.y));

                    _vh.AddTriangle(k, k + 1, k + 3);
                    _vh.AddTriangle(k, k + 3, k + 2);
                }

                if (roundEdge && si == segCount - 1 && t1 == 1)
                    DrawRoundEdge(Points[cnt - 1], Points[cnt - 2], lw, c1, uvMax);

                t += ratio;
            }
        }

        private void AddVert(Vector2 fairyPos, Color32 c, Vector2 uv) =>
            _vh.AddVert(new Vector3(_rect.xMin + fairyPos.x, _rect.yMax - fairyPos.y), c, uv);

        private void DrawRoundEdge(Vector2 p0, Vector2 p1, float lw, Color32 c, Vector2 uv)
        {
            Vector2 widthVector = ((Vector3)Vector3.Cross(p0 - p1, new Vector3(0, 0, 1))).normalized * (lw / 2f);
            Vector2 lineVector = (p0 - p1).normalized * (lw / 2f);
            var sides = Mathf.Max(6, Mathf.CeilToInt(Mathf.PI * lw / 2));
            var current = _vh.currentVertCount;
            var angleUnit = Mathf.PI / (sides - 1);
            AddVert(p0, c, uv);
            AddVert(p0 + widthVector, c, uv);
            for (var n = 0; n < sides; n++)
            {
                AddVert(p0 + Mathf.Cos(angleUnit * n) * widthVector + Mathf.Sin(angleUnit * n) * lineVector, c, uv);
                _vh.AddTriangle(current, current + 1 + n, current + 2 + n);
            }
        }
    }
}
