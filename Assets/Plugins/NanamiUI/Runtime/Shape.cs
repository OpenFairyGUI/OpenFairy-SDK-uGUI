using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    // 顶点生成复刻 FairyGUI 的 RectMesh/RoundedRectMesh/EllipseMesh/PolygonMesh/RegularPolygonMesh。
    // FairyGUI 网格坐标系 y 向下，这里在 AddVert 时翻转到 uGUI 局部坐标。
    public class Shape : MaskableGraphic
    {
        public enum Kind
        {
            Rect,
            RoundedRect,
            Ellipse,
            Polygon,
            RegularPolygon,
        }

        public Kind kind;
        public float lineSize;
        public Color lineColor = Color.black;
        public float[] corners;
        public Vector2[] points;
        public int sides = 3;
        public float startAngle;
        public float[] distances;
        public Vector2 skew;

        private Rect _rect;
        private VertexHelper _vh;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            _rect = GetPixelAdjustedRect();
            _vh = vh;

            switch (kind)
            {
                case Kind.Rect:
                    FillRect();
                    break;
                case Kind.RoundedRect:
                    FillRoundedRect();
                    break;
                case Kind.Ellipse:
                    FillEllipse();
                    break;
                case Kind.Polygon:
                    FillPolygon();
                    break;
                case Kind.RegularPolygon:
                    FillRegularPolygon();
                    break;
            }

            if (skew != Vector2.zero)
                ApplySkew(vh);
        }

        private void AddVert(float x, float y, Color32 c) =>
            _vh.AddVert(new Vector3(_rect.xMin + x, _rect.yMax - y), c, Vector2.zero);

        private void AddQuad(Rect part, Color32 c)
        {
            var v = _vh.currentVertCount;
            AddVert(part.xMin, part.yMin, c);
            AddVert(part.xMax, part.yMin, c);
            AddVert(part.xMax, part.yMax, c);
            AddVert(part.xMin, part.yMax, c);
            _vh.AddTriangle(v, v + 1, v + 2);
            _vh.AddTriangle(v, v + 2, v + 3);
        }

        private void FillRect()
        {
            var rect = new Rect(0, 0, _rect.width, _rect.height);
            if (lineSize == 0)
            {
                if (color.a != 0)
                    AddQuad(rect, color);
                return;
            }

            AddQuad(new Rect(rect.x, rect.y, lineSize, rect.height), lineColor);
            AddQuad(new Rect(rect.xMax - lineSize, rect.y, lineSize, rect.height), lineColor);
            AddQuad(new Rect(rect.x + lineSize, rect.y, rect.width - lineSize * 2, lineSize), lineColor);
            AddQuad(new Rect(rect.x + lineSize, rect.yMax - lineSize, rect.width - lineSize * 2, lineSize), lineColor);
            if (color.a != 0)
            {
                var part = Rect.MinMaxRect(rect.x + lineSize, rect.y + lineSize, rect.xMax - lineSize, rect.yMax - lineSize);
                if (part.width > 0 && part.height > 0)
                    AddQuad(part, color);
            }
        }

        private void FillRoundedRect()
        {
            var w = _rect.width;
            var h = _rect.height;
            var radiusX = w / 2;
            var radiusY = h / 2;
            var cornerMaxRadius = Mathf.Min(radiusX, radiusY);

            AddVert(radiusX, radiusY, color);

            var cnt = _vh.currentVertCount;
            for (var i = 0; i < 4; i++)
            {
                // FairyGUI 顺序：bottomRight, bottomLeft, topLeft, topRight；corners 序列化为 tl,tr,bl,br
                var radius = Mathf.Min(cornerMaxRadius, i switch { 0 => corners[3], 1 => corners[2], 2 => corners[0], _ => corners[1] });
                var offsetX = i is 0 or 3 ? w - radius * 2 : 0f;
                var offsetY = i is 0 or 1 ? h - radius * 2 : 0f;

                if (radius != 0)
                {
                    var partNumSides = Mathf.Max(1, Mathf.CeilToInt(Mathf.PI * radius / 8)) + 1;
                    var angleDelta = Mathf.PI / 2 / partNumSides;
                    var angle = Mathf.PI / 2 * i;
                    var startAngle = angle;
                    for (var j = 1; j <= partNumSides; j++)
                    {
                        if (j == partNumSides)
                            angle = startAngle + Mathf.PI / 2;
                        AddVert(offsetX + Mathf.Cos(angle) * (radius - lineSize) + radius, offsetY + Mathf.Sin(angle) * (radius - lineSize) + radius, color);
                        if (lineSize != 0)
                        {
                            AddVert(offsetX + Mathf.Cos(angle) * (radius - lineSize) + radius, offsetY + Mathf.Sin(angle) * (radius - lineSize) + radius, lineColor);
                            AddVert(offsetX + Mathf.Cos(angle) * radius + radius, offsetY + Mathf.Sin(angle) * radius + radius, lineColor);
                        }
                        angle += angleDelta;
                    }
                }
                else
                {
                    if (lineSize != 0)
                    {
                        var innerX = i is 0 or 3 ? offsetX - lineSize : offsetX + lineSize;
                        var innerY = i is 0 or 1 ? offsetY - lineSize : offsetY + lineSize;
                        AddVert(innerX, innerY, color);
                        AddVert(innerX, innerY, lineColor);
                    }
                    AddVert(offsetX, offsetY, lineSize != 0 ? lineColor : (Color32)color);
                }
            }
            cnt = _vh.currentVertCount - cnt;

            if (lineSize > 0)
                for (var i = 0; i < cnt; i += 3)
                {
                    if (i != cnt - 3)
                    {
                        _vh.AddTriangle(0, i + 1, i + 4);
                        _vh.AddTriangle(i + 5, i + 2, i + 3);
                        _vh.AddTriangle(i + 3, i + 6, i + 5);
                    }
                    else
                    {
                        _vh.AddTriangle(0, i + 1, 1);
                        _vh.AddTriangle(2, i + 2, i + 3);
                        _vh.AddTriangle(i + 3, 3, 2);
                    }
                }
            else
                for (var i = 0; i < cnt; i++)
                    _vh.AddTriangle(0, i + 1, i == cnt - 1 ? 1 : i + 2);
        }

        private void FillEllipse()
        {
            var radiusX = _rect.width / 2;
            var radiusY = _rect.height / 2;
            var sideCount = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * (radiusX + radiusY) / 4), 40, 800);
            var angleDelta = 2 * Mathf.PI / sideCount;
            var angle = 0f;

            AddVert(radiusX, radiusY, color);
            for (var i = 0; i < sideCount; i++)
            {
                var x = Mathf.Cos(angle) * (radiusX - lineSize) + radiusX;
                var y = Mathf.Sin(angle) * (radiusY - lineSize) + radiusY;
                AddVert(x, y, color);
                if (lineSize > 0)
                {
                    AddVert(x, y, lineColor);
                    AddVert(Mathf.Cos(angle) * radiusX + radiusX, Mathf.Sin(angle) * radiusY + radiusY, lineColor);
                }
                angle += angleDelta;
            }

            if (lineSize > 0)
            {
                var cnt = sideCount * 3;
                for (var i = 0; i < cnt; i += 3)
                {
                    if (i != cnt - 3)
                    {
                        _vh.AddTriangle(0, i + 1, i + 4);
                        _vh.AddTriangle(i + 5, i + 2, i + 3);
                        _vh.AddTriangle(i + 3, i + 6, i + 5);
                    }
                    else
                    {
                        _vh.AddTriangle(0, i + 1, 1);
                        _vh.AddTriangle(2, i + 2, i + 3);
                        _vh.AddTriangle(i + 3, 3, 2);
                    }
                }
            }
            else
                for (var i = 0; i < sideCount; i++)
                    _vh.AddTriangle(0, i + 1, i == sideCount - 1 ? 1 : i + 2);
        }

        private void FillPolygon()
        {
            var numVertices = points.Length;
            foreach (var point in points)
                AddVert(point.x, point.y, color);

            var rest = new System.Collections.Generic.List<int>();
            for (var i = 0; i < numVertices; i++)
                rest.Add(i);

            var restIndexPos = 0;
            var numRestIndices = numVertices;
            while (numRestIndices > 3)
            {
                var i0 = rest[restIndexPos % numRestIndices];
                var i1 = rest[(restIndexPos + 1) % numRestIndices];
                var i2 = rest[(restIndexPos + 2) % numRestIndices];
                var a = points[i0];
                var b = points[i1];
                var c = points[i2];
                var earFound = false;
                if ((a.y - b.y) * (c.x - b.x) + (b.x - a.x) * (c.y - b.y) >= 0)
                {
                    earFound = true;
                    for (var i = 3; i < numRestIndices; i++)
                    {
                        var p = points[rest[(restIndexPos + i) % numRestIndices]];
                        if (InTriangle(p, a, b, c))
                        {
                            earFound = false;
                            break;
                        }
                    }
                }

                if (earFound)
                {
                    _vh.AddTriangle(i0, i1, i2);
                    rest.RemoveAt((restIndexPos + 1) % numRestIndices);
                    numRestIndices--;
                    restIndexPos = 0;
                }
                else
                {
                    restIndexPos++;
                    if (restIndexPos == numRestIndices)
                        break;
                }
            }
            _vh.AddTriangle(rest[0], rest[1], rest[2]);

            if (lineSize > 0)
                DrawOutline();
        }

        private void DrawOutline()
        {
            var numVertices = points.Length;
            var k = _vh.currentVertCount;
            var start = k - numVertices;
            var vert = new UIVertex();
            for (var i = 0; i < numVertices; i++)
            {
                _vh.PopulateUIVertex(ref vert, start + i);
                var p0 = vert.position;
                _vh.PopulateUIVertex(ref vert, i < numVertices - 1 ? start + i + 1 : start);
                var p1 = vert.position;

                var widthVector = Vector3.Cross(p1 - p0, new Vector3(0, 0, 1)).normalized;
                _vh.AddVert(p0 - widthVector * (lineSize * 0.5f), lineColor, Vector2.zero);
                _vh.AddVert(p0 + widthVector * (lineSize * 0.5f), lineColor, Vector2.zero);
                _vh.AddVert(p1 - widthVector * (lineSize * 0.5f), lineColor, Vector2.zero);
                _vh.AddVert(p1 + widthVector * (lineSize * 0.5f), lineColor, Vector2.zero);

                k += 4;
                _vh.AddTriangle(k - 4, k - 3, k - 1);
                _vh.AddTriangle(k - 4, k - 1, k - 2);
                if (i != 0)
                {
                    _vh.AddTriangle(k - 6, k - 5, k - 3);
                    _vh.AddTriangle(k - 6, k - 3, k - 4);
                }
                if (i == numVertices - 1)
                {
                    start += numVertices;
                    _vh.AddTriangle(k - 2, k - 1, start + 1);
                    _vh.AddTriangle(k - 2, start + 1, start);
                }
            }
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var v0 = c - a;
            var v1 = b - a;
            var v2 = p - a;
            var dot00 = Vector2.Dot(v0, v0);
            var dot01 = Vector2.Dot(v0, v1);
            var dot02 = Vector2.Dot(v0, v2);
            var dot11 = Vector2.Dot(v1, v1);
            var dot12 = Vector2.Dot(v1, v2);
            var invDen = 1f / (dot00 * dot11 - dot01 * dot01);
            var u = (dot11 * dot02 - dot01 * dot12) * invDen;
            var v = (dot00 * dot12 - dot01 * dot02) * invDen;
            return u >= 0 && v >= 0 && u + v < 1;
        }

        private void FillRegularPolygon()
        {
            var radius = Mathf.Min(_rect.width / 2, _rect.height / 2);
            var angleDelta = 2 * Mathf.PI / sides;
            var angle = startAngle * Mathf.Deg2Rad;

            AddVert(radius, radius, color);
            for (var i = 0; i < sides; i++)
            {
                var r = distances != null && distances.Length > 0 ? radius * distances[i] : radius;
                var vec = new Vector2(Mathf.Cos(angle) * (r - lineSize) + radius, Mathf.Sin(angle) * (r - lineSize) + radius);
                AddVert(vec.x, vec.y, color);
                if (lineSize > 0)
                {
                    AddVert(vec.x, vec.y, lineColor);
                    AddVert(Mathf.Cos(angle) * r + radius, Mathf.Sin(angle) * r + radius, lineColor);
                }
                angle += angleDelta;
            }

            if (lineSize > 0)
            {
                var cnt = sides * 3;
                for (var i = 0; i < cnt; i += 3)
                {
                    if (i != cnt - 3)
                    {
                        _vh.AddTriangle(0, i + 1, i + 4);
                        _vh.AddTriangle(i + 5, i + 2, i + 3);
                        _vh.AddTriangle(i + 3, i + 6, i + 5);
                    }
                    else
                    {
                        _vh.AddTriangle(0, i + 1, 1);
                        _vh.AddTriangle(2, i + 2, i + 3);
                        _vh.AddTriangle(i + 3, 3, 2);
                    }
                }
            }
            else
                for (var i = 0; i < sides; i++)
                    _vh.AddTriangle(0, i + 1, i == sides - 1 ? 1 : i + 2);
        }

        private void ApplySkew(VertexHelper vh)
        {
            var skewX = -skew.x * Mathf.Deg2Rad;
            var skewY = -skew.y * Mathf.Deg2Rad;
            var sinX = Mathf.Sin(skewX);
            var cosX = Mathf.Cos(skewX);
            var sinY = Mathf.Sin(skewY);
            var cosY = Mathf.Cos(skewY);
            var pivot = rectTransform.pivot;
            var center = new Vector2(_rect.xMin + pivot.x * _rect.width, _rect.yMax - pivot.y * _rect.height);

            var vert = new UIVertex();
            for (var i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                var p = (Vector2)vert.position - center;
                vert.position = center + new Vector2(p.x * cosY - p.y * sinX, p.x * sinY + p.y * cosX);
                vh.SetUIVertex(vert, i);
            }
        }
    }
}
