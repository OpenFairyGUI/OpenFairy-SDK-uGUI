using System;
using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Example
{
    // Demo_Graph 的运行时改造（复刻 FairyGUI BasicsMain.PlayGraph）。抽成静态方法，
    // 供运行时示例 BasicsMain 与截图对比工具 BasicsRenderDiff 共用。返回被 fillEnd 动画驱动的 line。
    public static class GraphDemo
    {
        public static Line Setup(GameObject go, Sprite changeSprite)
        {
            var demo = Array.Find(go.GetComponents<OpenFairy.UGUI.Component>(), c => c.GetType().FullName == "UI.Basics.Demo_Graph");

            var pie = (Graph)Field(demo, "m_pie");
            pie.startDegree = 30;
            pie.endDegree = 300;
            pie.SetVerticesDirty();

            var trapezoid = (Graph)Field(demo, "m_trapezoid");
            trapezoid.kind = Graph.Kind.Polygon;
            trapezoid.usePercentPositions = true;
            trapezoid.lineSize = 0;
            trapezoid.points = new[] { new Vector2(0, 1), new Vector2(0.3f, 0), new Vector2(0.7f, 0), new Vector2(1, 1) };
            trapezoid.texcoords = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            trapezoid.texture = changeSprite;
            trapezoid.SetVerticesDirty();
            trapezoid.SetMaterialDirty();

            var line = ReplaceWithLine((Graphic)Field(demo, "m_line"));
            line.lineWidthCurve = AnimationCurve.Linear(0, 25, 1, 10);
            line.roundEdge = true;
            line.gradient = LineGradient();
            line.fillEnd = 0;
            line.SetPath(new[] { P(0, 120), P(20, 120), P(100, 100), P(180, 30), P(100, 0), P(20, 30), P(100, 100), P(180, 120), P(200, 120) });

            var line2 = ReplaceWithLine((Graphic)Field(demo, "m_line2"));
            line2.lineWidth = 3;
            line2.roundEdge = true;
            line2.SetPath(new[] { S(0, 120), S(60, 30), S(80, 90), S(140, 30), S(160, 90), S(220, 30) });

            var line3src = (UnityEngine.UI.Image)Field(demo, "m_line3");
            var line3sprite = line3src.sprite; // 替换前取出，ReplaceWithLine 会销毁原 Image
            var line3 = ReplaceWithLine(line3src);
            line3.lineWidth = 30;
            line3.roundEdge = false;
            line3.sprite = line3sprite;
            line3.SetPath(new[]
            {
                new TransitionPath.PathPoint(new Vector2(0, 30), CurveType.CubicBezier, new Vector2(50, -30), new Vector2(150, -50)),
                new TransitionPath.PathPoint(new Vector2(200, 30), CurveType.Bezier, new Vector2(300, 130)),
                new TransitionPath.PathPoint(new Vector2(400, 30)),
            });

            return line;
        }

        private static object Field(object owner, string name) => owner.GetType().GetField(name).GetValue(owner);
        private static TransitionPath.PathPoint P(float x, float y) => new(new Vector2(x, y));
        private static TransitionPath.PathPoint S(float x, float y) => new(new Vector2(x, y), CurveType.Straight);

        // uGUI 每个 GameObject 只能有一个 Graphic：销毁原图形（Graph/Image），复用其 CanvasRenderer 挂上 Line，
        // 并沿用原色（FairyGUI LineMesh 用 shape 的 fillColor 作顶点色；line2 无渐变时靠它上蓝色）。
        private static Line ReplaceWithLine(Graphic existing)
        {
            var go = existing.gameObject;
            var color = existing.color;
            UnityEngine.Object.DestroyImmediate(existing);
            var line = go.AddComponent<Line>();
            line.color = color;
            return line;
        }

        public static Gradient LineGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.7941176f, 0.21604672f, 0.21604672f), 0f),
                    new GradientColorKey(new Color(0.069582626f, 0.8602941f, 0.27135026f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
