using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace OpenFairy.UGUI
{
    // 复刻 GPathPoint.CurveType。
    public enum CurveType
    {
        CRSpline,
        Bezier,
        CubicBezier,
        Straight,
    }

    // 移植自 FairyGUI GPath：按弧长参数化在直线/贝塞尔/CR样条混合路径上取点。
    // 曲线求值交给 com.unity.splines：直线取三等分控制点（保持等速）、二次贝塞尔升阶、CR 样条按标准
    // Catmull-Rom→三次贝塞尔换算，均为精确等价、参数化与 GPath 逐点一致；
    // 段长权重保持 FairyGUI 口径（直线/贝塞尔=端点距、CR=控制点折线长），非真弧长。
    // 坐标为 FairyGUI 编辑器导出的相对起点偏移（y 向下），由调用方翻转。
    public class TransitionPath
    {
        private struct Segment
        {
            public bool Straight;
            public float Length;
            public int Start;
            public int Count; // CR 段的子曲线数；其它恒 1
        }

        private readonly List<Segment> _segments = new();
        private readonly List<BezierCurve> _curves = new();
        private float _fullLength;

        // 编辑器 path 属性格式：每个点为 [curveType, x, y, (控制点…), smooth]。
        public TransitionPath(float[] tokens)
        {
            var points = new List<PathPoint>();
            var i = 0;
            while (i < tokens.Length - 3)
            {
                var type = (CurveType)(int)tokens[i];
                var pos = new Vector2(tokens[i + 1], tokens[i + 2]);
                i += 3;
                Vector2 c1 = Vector2.zero, c2 = Vector2.zero;
                if (type == CurveType.Bezier)
                {
                    c1 = new Vector2(tokens[i], tokens[i + 1]);
                    i += 2;
                }
                else if (type == CurveType.CubicBezier)
                {
                    c1 = new Vector2(tokens[i], tokens[i + 1]);
                    c2 = new Vector2(tokens[i + 2], tokens[i + 3]);
                    i += 4;
                }
                i++; // smooth
                points.Add(new PathPoint(pos, type, c1, c2));
            }
            Create(points);
        }

        // 直接给点构造（代码里建路径，如 Demo_Graph 的折线）。C1/C2 为绝对控制点。
        public readonly struct PathPoint
        {
            public readonly CurveType Type;
            public readonly Vector2 Pos, C1, C2;
            public PathPoint(Vector2 pos, CurveType type = CurveType.CRSpline, Vector2 c1 = default, Vector2 c2 = default)
            {
                Type = type; Pos = pos; C1 = c1; C2 = c2;
            }
        }

        public TransitionPath(IReadOnlyList<PathPoint> pts) => Create(pts);

        public float Length => _fullLength;
        public int SegmentCount => _segments.Count;
        public float GetSegmentLength(int i) => _segments[i].Length;

        // 复刻 GPath.GetPointsInSegment：按 pointDensity 在段内采样，端点各补一个；直线只出两端点。
        public void GetPointsInSegment(int segIndex, float t0, float t1, List<Vector2> points, List<float> ts, float pointDensity)
        {
            ts?.Add(t0);
            var seg = _segments[segIndex];
            points.Add(Evaluate(seg, t0));
            if (!seg.Straight)
            {
                var smooth = (int)Mathf.Min(seg.Length * pointDensity, 50);
                for (var j = 0; j <= smooth; j++)
                {
                    var t = (float)j / smooth;
                    if (t > t0 && t < t1)
                    {
                        points.Add(Evaluate(seg, t));
                        ts?.Add(t);
                    }
                }
            }
            points.Add(Evaluate(seg, t1));
            ts?.Add(t1);
        }

        private void Create(IReadOnlyList<PathPoint> points)
        {
            var spline = new List<Vector2>();
            for (var i = 0; i < points.Count; i++)
            {
                var current = points[i];
                if (i > 0)
                {
                    var prev = points[i - 1];
                    if (prev.Type != CurveType.CRSpline)
                    {
                        _curves.Add(prev.Type switch
                        {
                            CurveType.Straight => new BezierCurve(F3(prev.Pos),
                                F3(Vector2.Lerp(prev.Pos, current.Pos, 1f / 3)), F3(Vector2.Lerp(prev.Pos, current.Pos, 2f / 3)), F3(current.Pos)),
                            CurveType.Bezier => new BezierCurve(F3(prev.Pos), F3(prev.C1), F3(current.Pos)),
                            _ => new BezierCurve(F3(prev.Pos), F3(prev.C1), F3(prev.C2), F3(current.Pos)),
                        });
                        var segment = new Segment
                        {
                            Straight = prev.Type == CurveType.Straight,
                            Length = Vector2.Distance(prev.Pos, current.Pos),
                            Start = _curves.Count - 1,
                            Count = 1,
                        };
                        _fullLength += segment.Length;
                        _segments.Add(segment);
                    }

                    if (current.Type != CurveType.CRSpline)
                    {
                        if (spline.Count > 0)
                        {
                            spline.Add(current.Pos);
                            CreateSplineSegment(spline);
                        }
                    }
                    else
                        spline.Add(current.Pos);
                }
                else if (current.Type == CurveType.CRSpline)
                    spline.Add(current.Pos);
            }
            if (spline.Count > 1)
                CreateSplineSegment(spline);
        }

        private void CreateSplineSegment(List<Vector2> spline)
        {
            var segment = new Segment { Start = _curves.Count, Count = spline.Count - 1 };
            for (var i = 1; i < spline.Count; i++)
            {
                Vector2 p0 = spline[Mathf.Max(i - 2, 0)], p1 = spline[i - 1], p2 = spline[i], p3 = spline[Mathf.Min(i + 1, spline.Count - 1)];
                _curves.Add(new BezierCurve(F3(p1), F3(p1 + (p2 - p0) / 6), F3(p2 - (p3 - p1) / 6), F3(p2)));
                segment.Length += Vector2.Distance(p1, p2);
            }
            _fullLength += segment.Length;
            _segments.Add(segment);
            spline.Clear();
        }

        private static float3 F3(Vector2 v) => new(v.x, v.y, 0);

        public Vector2 GetPointAt(float t)
        {
            t = Mathf.Clamp01(t);
            if (_segments.Count == 0)
                return Vector2.zero;

            if (t == 1)
                return Evaluate(_segments[^1], 1);

            var len = t * _fullLength;
            foreach (var segment in _segments)
            {
                len -= segment.Length;
                if (len < 0)
                    return Evaluate(segment, 1 + len / segment.Length);
            }
            return Vector2.zero;
        }

        // 段内求值：t 均匀切给段内子曲线（与 GPath 的 CR 全段参数化一致；t==1 落最后一条曲线收尾）。
        private Vector2 Evaluate(Segment segment, float t)
        {
            var index = Mathf.Min(Mathf.FloorToInt(t * segment.Count), segment.Count - 1);
            var position = CurveUtility.EvaluatePosition(_curves[segment.Start + index], t * segment.Count - index);
            return new Vector2(position.x, position.y);
        }
    }
}
