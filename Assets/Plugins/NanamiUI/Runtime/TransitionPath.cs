using System.Collections.Generic;
using UnityEngine;

namespace NanamiUI
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
    // 坐标为 FairyGUI 编辑器导出的相对起点偏移（y 向下），由调用方翻转。
    public class TransitionPath
    {
        private struct Segment
        {
            public CurveType Type;
            public float Length;
            public int Start;
            public int Count;
        }

        private readonly List<Segment> _segments = new();
        private readonly List<Vector2> _points = new();
        private float _fullLength;

        // 编辑器 path 属性格式：每个点为 [curveType, x, y, (控制点…), smooth]。
        public TransitionPath(float[] tokens)
        {
            var points = new List<(CurveType Type, Vector2 Pos, Vector2 C1, Vector2 C2)>();
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
                points.Add((type, pos, c1, c2));
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

        public TransitionPath(IReadOnlyList<PathPoint> pts)
        {
            var list = new List<(CurveType Type, Vector2 Pos, Vector2 C1, Vector2 C2)>(pts.Count);
            foreach (var p in pts)
                list.Add((p.Type, p.Pos, p.C1, p.C2));
            Create(list);
        }

        public float Length => _fullLength;
        public int SegmentCount => _segments.Count;
        public float GetSegmentLength(int i) => _segments[i].Length;

        // 复刻 GPath.GetPointsInSegment：按 pointDensity 在段内采样，端点各补一个。
        public void GetPointsInSegment(int segIndex, float t0, float t1, List<Vector2> points, List<float> ts, float pointDensity)
        {
            ts?.Add(t0);
            var seg = _segments[segIndex];
            if (seg.Type == CurveType.Straight)
            {
                points.Add(Vector2.Lerp(_points[seg.Start], _points[seg.Start + 1], t0));
                points.Add(Vector2.Lerp(_points[seg.Start], _points[seg.Start + 1], t1));
            }
            else
            {
                Vector2 Eval(float t) => seg.Type == CurveType.CRSpline ? CRSpline(seg.Start, seg.Count, t) : Bezier(seg.Start, seg.Count, t);
                points.Add(Eval(t0));
                var smooth = (int)Mathf.Min(seg.Length * pointDensity, 50);
                for (var j = 0; j <= smooth; j++)
                {
                    var t = (float)j / smooth;
                    if (t > t0 && t < t1)
                    {
                        points.Add(Eval(t));
                        ts?.Add(t);
                    }
                }
                points.Add(Eval(t1));
            }
            ts?.Add(t1);
        }

        private void Create(List<(CurveType Type, Vector2 Pos, Vector2 C1, Vector2 C2)> points)
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
                        var segment = new Segment { Type = prev.Type, Start = _points.Count };
                        if (prev.Type == CurveType.Straight)
                        {
                            segment.Count = 2;
                            _points.Add(prev.Pos);
                            _points.Add(current.Pos);
                        }
                        else if (prev.Type == CurveType.Bezier)
                        {
                            segment.Count = 3;
                            _points.Add(prev.Pos);
                            _points.Add(current.Pos);
                            _points.Add(prev.C1);
                        }
                        else
                        {
                            segment.Count = 4;
                            _points.Add(prev.Pos);
                            _points.Add(current.Pos);
                            _points.Add(prev.C1);
                            _points.Add(prev.C2);
                        }
                        segment.Length = Vector2.Distance(prev.Pos, current.Pos);
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
            var cnt = spline.Count;
            spline.Insert(0, spline[0]);
            spline.Add(spline[cnt]);
            spline.Add(spline[cnt]);
            cnt += 3;

            var segment = new Segment { Type = CurveType.CRSpline, Start = _points.Count, Count = cnt };
            _points.AddRange(spline);
            for (var i = 1; i < cnt; i++)
                segment.Length += Vector2.Distance(spline[i - 1], spline[i]);
            _fullLength += segment.Length;
            _segments.Add(segment);
            spline.Clear();
        }

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

        private Vector2 Evaluate(Segment segment, float t)
        {
            if (segment.Type == CurveType.Straight)
                return Vector2.Lerp(_points[segment.Start], _points[segment.Start + 1], t);
            if (segment.Type == CurveType.CRSpline)
                return CRSpline(segment.Start, segment.Count, t);
            return Bezier(segment.Start, segment.Count, t);
        }

        private Vector2 CRSpline(int start, int count, float t)
        {
            var index = Mathf.FloorToInt(t * (count - 4)) + start;
            var adjusted = t == 1f ? 1f : Mathf.Repeat(t * (count - 4), 1f);
            var p0 = _points[index];
            var p1 = _points[index + 1];
            var p2 = _points[index + 2];
            var p3 = _points[index + 3];
            var t0 = ((-adjusted + 2f) * adjusted - 1f) * adjusted * 0.5f;
            var t1 = ((3f * adjusted - 5f) * adjusted * adjusted + 2f) * 0.5f;
            var t2 = ((-3f * adjusted + 4f) * adjusted + 1f) * adjusted * 0.5f;
            var t3 = (adjusted - 1f) * adjusted * adjusted * 0.5f;
            return p0 * t0 + p1 * t1 + p2 * t2 + p3 * t3;
        }

        private Vector2 Bezier(int start, int count, float t)
        {
            var t2 = 1f - t;
            var p0 = _points[start];
            var p1 = _points[start + 1];
            var c0 = _points[start + 2];
            if (count == 4)
            {
                var c1 = _points[start + 3];
                return t2 * t2 * t2 * p0 + 3f * t2 * t2 * t * c0 + 3f * t2 * t * t * c1 + t * t * t * p1;
            }
            return t2 * t2 * p0 + 2f * t2 * t * c0 + t * t * p1;
        }
    }
}
