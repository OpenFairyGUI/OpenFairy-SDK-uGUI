using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NanamiUI
{
    // 排版逻辑复刻 FairyGUI 的 TextField + DynamicFont：
    // 行高 = round(size*1.25)，基线 = round(size)，四周 2px gutter，对齐偏移取整，
    // 下划线取 "_" 字形中心 UV，行内图片基线 = 高度*0.8、占宽 = 宽度+2。
    // 支持 UBB（color/渐变/b/i/u/size/img）与 richtext 的 img/a 标签，以及位图字体。
    public class Text : UnityEngine.UI.Text, IPointerClickHandler
    {
        public static string defaultFont = "Arial";

        public string fontNames;
        public int leading = 3;
        public bool ubb;
        public bool html;
        public bool underlined;
        public BitmapFont bitmapFont;
        public Sprite[] imageSprites;

        [NonSerialized] private Action<string> _onClickLink;

        // 点富文本 <a href> 链接的回调（复刻 FairyGUI GRichTextField.onClickLink）。
        public Action<string> onClickLink
        {
            get => _onClickLink;
            set
            {
                _onClickLink = value;
                raycastTarget = value != null;
            }
        }

        private static readonly Dictionary<string, Font> Fonts = new();

        private struct Run
        {
            public string Text;
            public int Image;
            public int Size;
            public FontStyle Style;
            public bool Underline;
            public string Href;
            public Color32 TL, BL, TR, BR;
        }

        private struct Quad
        {
            public Rect Rect;
            public Color32 TL, BL, TR, BR;
            public Vector2 UvBL, UvTL, UvTR, UvBR;
        }

        private readonly List<Quad> _quads = new();
        private readonly List<(int Image, Vector2 Position)> _placements = new();
        private readonly List<(Rect Rect, string Href)> _links = new(); // 排版坐标（左上 y-down），供点击命中

        public void OnPointerClick(PointerEventData eventData)
        {
            if (onClickLink == null || _links.Count == 0)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out var local);
            var rect = rectTransform.rect;
            var point = new Vector2(local.x - rect.xMin, rect.yMax - local.y); // 转 top-down 排版坐标
            foreach (var (r, href) in _links)
                if (r.Contains(point))
                {
                    onClickLink(href);
                    return;
                }
        }

        private static string ParseHref(string tag)
        {
            var idx = tag.IndexOf("href", StringComparison.Ordinal);
            if (idx < 0)
                return "";
            var quote = tag.IndexOfAny(new[] { '\'', '"' }, idx);
            if (quote < 0)
                return "";
            var end = tag.IndexOf(tag[quote], quote + 1);
            return end > quote ? tag[(quote + 1)..end] : "";
        }

        public override Texture mainTexture => bitmapFont != null ? bitmapFont.texture : base.mainTexture;

        protected override void OnEnable()
        {
            raycastTarget = _onClickLink != null;
            // 位图字体也需要挂一个动态字体占位：UI.Text.UpdateGeometry 在 font == null 时不生成网格。
            if (font == null)
            {
                var names = string.IsNullOrEmpty(fontNames) ? defaultFont : fontNames;
                // 进出 Play Mode 会销毁运行时创建的字体，缓存命中已销毁对象时需要重建。
                if (!Fonts.TryGetValue(names, out var osFont) || osFont == null)
                    Fonts[names] = osFont = Font.CreateDynamicFontFromOSFont(names.Split(','), 16);
                font = osFont;
            }
            base.OnEnable();
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            toFill.Clear();
            Layout();
            foreach (var quad in _quads)
            {
                var v = toFill.currentVertCount;
                var rect = rectTransform.rect;
                toFill.AddVert(new Vector3(rect.xMin + quad.Rect.xMin, rect.yMax - quad.Rect.yMax), quad.BL, quad.UvBL);
                toFill.AddVert(new Vector3(rect.xMin + quad.Rect.xMin, rect.yMax - quad.Rect.yMin), quad.TL, quad.UvTL);
                toFill.AddVert(new Vector3(rect.xMin + quad.Rect.xMax, rect.yMax - quad.Rect.yMin), quad.TR, quad.UvTR);
                toFill.AddVert(new Vector3(rect.xMin + quad.Rect.xMax, rect.yMax - quad.Rect.yMax), quad.BR, quad.UvBR);
                toFill.AddTriangle(v, v + 1, v + 2);
                toFill.AddTriangle(v, v + 2, v + 3);
            }
        }

        // 提前请求全部字符，避免动态字体图集在渲染同帧内扩容导致已生成网格失效。
        public void WarmUp()
        {
            if (bitmapFont != null || font == null)
                return;
            foreach (var run in Parse())
                if (run.Text != null)
                    font.RequestCharactersInTexture(run.Text, run.Size, run.Style);
            font.RequestCharactersInTexture("_", 50, FontStyle.Normal);
        }

        // 让 Migrate 在构建 prefab 时烘焙行内图片子节点。
        public void RebuildImages()
        {
            Layout();
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("img"))
                {
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }
            foreach (var (image, position) in _placements)
            {
                var go = new GameObject("img" + image, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(position.x, -position.y);
                var sprite = imageSprites[image];
                rt.sizeDelta = sprite.rect.size;
                var imageComponent = go.GetComponent<Image>();
                imageComponent.sprite = sprite;
                imageComponent.raycastTarget = false;
            }
        }

        // --- 解析 ---

        private List<Run> Parse()
        {
            var runs = new List<Run>();
            var current = new Run { Size = fontSize, Style = fontStyle, Underline = underlined, Image = -1 };
            SetColors(ref current, new[] { (Color32)color });
            if (!ubb && !html)
            {
                current.Text = text ?? "";
                runs.Add(current);
                return runs;
            }

            var stack = new Stack<Run>();
            var buffer = new StringBuilder();
            var imageIndex = 0;

            void Flush()
            {
                if (buffer.Length == 0)
                    return;
                var run = current;
                run.Text = buffer.ToString();
                runs.Add(run);
                buffer.Clear();
            }

            var value = text ?? "";
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (ubb && ch == '[')
                {
                    var end = value.IndexOf(']', i);
                    var tag = end > i ? value[(i + 1)..end] : null;
                    if (tag == "img")
                    {
                        var close = value.IndexOf("[/img]", end, System.StringComparison.Ordinal);
                        Flush();
                        runs.Add(new Run { Image = imageIndex++ });
                        i = close + 5;
                        continue;
                    }
                    if (tag != null && HandleTag(tag, ref current, stack, Flush))
                    {
                        i = end;
                        continue;
                    }
                    buffer.Append(ch);
                }
                else if (html && ch == '<')
                {
                    var end = value.IndexOf('>', i);
                    var tag = value[(i + 1)..end].Trim();
                    if (tag.StartsWith("img"))
                    {
                        Flush();
                        runs.Add(new Run { Image = imageIndex++ });
                    }
                    else if (tag.StartsWith("a"))
                    {
                        // FairyGUI HtmlParseOptions 默认链接样式：#3A67CC + 下划线。
                        Flush();
                        stack.Push(current);
                        SetColors(ref current, new[] { new Color32(0x3A, 0x67, 0xCC, 0xFF) });
                        current.Underline = true;
                        current.Href = ParseHref(tag);
                    }
                    else if (tag.StartsWith("/a"))
                    {
                        Flush();
                        if (stack.Count > 0)
                            current = stack.Pop();
                    }
                    i = end;
                }
                else
                    buffer.Append(ch);
            }
            Flush();
            return runs;
        }

        private bool HandleTag(string tag, ref Run current, Stack<Run> stack, System.Action flush)
        {
            switch (tag)
            {
                case "b":
                case "i":
                    flush();
                    stack.Push(current);
                    var italic = tag == "i" ? FontStyle.Italic : FontStyle.Normal;
                    var bold = tag == "b" ? FontStyle.Bold : FontStyle.Normal;
                    current.Style |= bold | italic;
                    return true;
                case "u":
                    flush();
                    stack.Push(current);
                    current.Underline = true;
                    return true;
                case "/b":
                case "/i":
                case "/u":
                case "/size":
                case "/color":
                    flush();
                    if (stack.Count > 0)
                        current = stack.Pop();
                    return true;
            }
            if (tag.StartsWith("size="))
            {
                flush();
                stack.Push(current);
                current.Size = int.Parse(tag[5..]);
                return true;
            }
            if (tag.StartsWith("color="))
            {
                flush();
                stack.Push(current);
                var parts = tag[6..].Split(',');
                var colors = new Color32[parts.Length];
                for (var i = 0; i < parts.Length; i++)
                    colors[i] = ParseColor(parts[i]);
                SetColors(ref current, colors);
                return true;
            }
            return false;
        }

        // FillVertexColors 语义：2 色为纵向渐变，4 色为四角 [左上,左下,右上,右下]。
        private static void SetColors(ref Run run, Color32[] colors)
        {
            run.TL = colors[0];
            run.BL = colors.Length > 1 ? colors[1] : colors[0];
            run.TR = colors.Length > 2 ? colors[2] : colors[0];
            run.BR = colors.Length > 3 ? colors[3] : colors.Length > 2 ? colors[2] : run.BL;
        }

        private static Color32 ParseColor(string value)
        {
            ColorUtility.TryParseHtmlString(value.Length == 9 ? $"#{value[3..]}{value[1..3]}" : value, out var color);
            return color;
        }

        // --- 排版 ---

        private struct Segment
        {
            public Run Run;
            public string Text;
        }

        private void Layout()
        {
            _quads.Clear();
            _placements.Clear();
            _links.Clear();
            var runs = Parse();
            if (bitmapFont == null)
            {
                if (font == null)
                    return;
                foreach (var run in runs)
                    if (run.Text != null)
                        font.RequestCharactersInTexture(run.Text, run.Size, run.Style);
                font.RequestCharactersInTexture("_", 50, FontStyle.Normal);
            }

            var rect = rectTransform.rect;
            var wrap = horizontalOverflow == HorizontalWrapMode.Wrap;
            var rectWidth = rect.width - 4;
            var lines = BreakLines(runs, wrap, rectWidth);

            var hAlign = (int)alignment % 3;
            var vAlign = (int)alignment / 3;
            var lineSpacing = leading - 1; // TextField: 实际行距 = lineSpacing - 1
            var lineMetrics = new List<(float Baseline, float Height, float Width)>();
            var textHeight = 4f;
            foreach (var line in lines)
            {
                var metrics = MeasureLine(line);
                lineMetrics.Add(metrics);
                textHeight += metrics.Height;
            }
            textHeight += (lines.Count - 1) * lineSpacing;

            var y = 2f + (vAlign == 1 ? (int)(Mathf.Max(0, rect.height - textHeight) / 2)
                : vAlign == 2 ? Mathf.Max(0, rect.height - textHeight) : 0);

            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var (baseline, height, width) = lineMetrics[lineIndex];
                if (verticalOverflow == VerticalWrapMode.Truncate && lineIndex > 0 && y + height > rect.height - 2)
                    break;

                var indent = hAlign == 1 ? (int)((rectWidth - width) / 2) : hAlign == 2 ? rectWidth - width : 0;
                var x = 2f + Mathf.Max(0, indent);
                var baselineY = y + baseline;

                foreach (var segment in lines[lineIndex])
                {
                    if (segment.Run.Image >= 0)
                    {
                        var sprite = imageSprites[segment.Run.Image];
                        _placements.Add((segment.Run.Image, new Vector2(x + 1, baselineY - sprite.rect.height * 0.8f)));
                        x += sprite.rect.width + 2;
                        continue;
                    }

                    var underlineStart = x;
                    foreach (var ch in segment.Text)
                        x += DrawChar(ch, segment.Run, x, baselineY, y);
                    if (segment.Run.Underline)
                        DrawUnderline(segment.Run, underlineStart, x, baselineY);
                    if (segment.Run.Href != null && x > underlineStart)
                        _links.Add((new Rect(underlineStart, y, x - underlineStart, height), segment.Run.Href));
                }
                y += height + lineSpacing;
            }
        }

        private float DrawChar(char ch, Run run, float x, float baselineY, float lineY)
        {
            if (bitmapFont != null)
            {
                if (ch == ' ')
                    return Mathf.RoundToInt(bitmapFont.size / 2f);
                if (!bitmapFont.TryGetGlyph(ch, out var glyph))
                    return 0;
                var top = baselineY - glyph.lineHeight + glyph.y;
                var tint = bitmapFont.canTint;
                Color32 white = Color.white;
                _quads.Add(new Quad
                {
                    Rect = new Rect(x + glyph.x, top, glyph.width, glyph.height),
                    TL = tint ? run.TL : white, BL = tint ? run.BL : white, TR = tint ? run.TR : white, BR = tint ? run.BR : white,
                    UvBL = new Vector2(glyph.uv.xMin, glyph.uv.yMin),
                    UvTL = new Vector2(glyph.uv.xMin, glyph.uv.yMax),
                    UvTR = new Vector2(glyph.uv.xMax, glyph.uv.yMax),
                    UvBR = new Vector2(glyph.uv.xMax, glyph.uv.yMin),
                });
                return glyph.advance;
            }

            if (!font.GetCharacterInfo(ch, out var info, run.Size, run.Style))
                return 0;
            if (info.glyphWidth > 0)
                _quads.Add(new Quad
                {
                    Rect = Rect.MinMaxRect(x + info.minX, baselineY - info.maxY, x + info.maxX, baselineY - info.minY),
                    TL = run.TL, BL = run.BL, TR = run.TR, BR = run.BR,
                    UvBL = info.uvBottomLeft,
                    UvTL = info.uvTopLeft,
                    UvTR = info.uvTopRight,
                    UvBR = info.uvBottomRight,
                });
            return info.advance;
        }

        private void DrawUnderline(Run run, float from, float to, float baselineY)
        {
            if (to <= from || bitmapFont != null)
                return;
            font.GetCharacterInfo('_', out var info, 50, FontStyle.Normal);
            var thickness = Mathf.Max(1, run.Size / 16f);
            var offset = Mathf.RoundToInt(info.minY * run.Size / 50f + thickness);
            var uv = (info.uvBottomLeft + info.uvTopRight) / 2;
            _quads.Add(new Quad
            {
                Rect = new Rect(from, baselineY - offset, to - from, thickness),
                TL = run.TL, BL = run.TL, TR = run.TL, BR = run.TL,
                UvBL = uv, UvTL = uv, UvTR = uv, UvBR = uv,
            });
        }

        private float CharWidth(char ch, Run run)
        {
            if (run.Image >= 0)
                return imageSprites[run.Image].rect.width + 2;
            if (bitmapFont != null)
                return ch == ' ' ? Mathf.RoundToInt(bitmapFont.size / 2f)
                    : bitmapFont.TryGetGlyph(ch, out var glyph) ? glyph.advance : 0;
            return font.GetCharacterInfo(ch, out var info, run.Size, run.Style) ? info.advance : 0;
        }

        private (float Baseline, float Height, float Width) MeasureLine(List<Segment> line)
        {
            float baseline = 0, descent = 0, width = 0;
            foreach (var segment in line)
            {
                if (segment.Run.Image >= 0)
                {
                    var size = imageSprites[segment.Run.Image].rect.size;
                    baseline = Mathf.Max(baseline, size.y * 0.8f);
                    descent = Mathf.Max(descent, size.y * 0.2f);
                    width += size.x + 2;
                    continue;
                }
                float glyphBaseline, glyphHeight;
                if (bitmapFont != null)
                {
                    glyphBaseline = glyphHeight = LineHeight(segment.Text);
                }
                else
                {
                    glyphBaseline = Mathf.RoundToInt(segment.Run.Size);
                    glyphHeight = Mathf.RoundToInt(segment.Run.Size * 1.25f);
                }
                baseline = Mathf.Max(baseline, glyphBaseline);
                descent = Mathf.Max(descent, glyphHeight - glyphBaseline);
                foreach (var ch in segment.Text)
                    width += CharWidth(ch, segment.Run);
            }
            if (baseline == 0)
            {
                baseline = bitmapFont != null ? bitmapFont.size : Mathf.RoundToInt(fontSize);
                descent = bitmapFont != null ? 0 : Mathf.RoundToInt(fontSize * 1.25f) - baseline;
            }
            return (baseline, baseline + descent, width);
        }

        private float LineHeight(string value)
        {
            float height = bitmapFont.size;
            foreach (var ch in value)
                if (bitmapFont.TryGetGlyph(ch, out var glyph))
                    height = Mathf.Max(height, glyph.lineHeight);
            return height;
        }

        // 复刻 TextField.BuildLines：字符加入后超宽才换行；仅空格后的词整体回退（wordLen<20），否则只移当前字符。
        private List<List<Segment>> BreakLines(List<Run> runs, bool wrap, float rectWidth)
        {
            var lines = new List<List<Segment>>();
            var current = new List<(Run Run, char Ch)>();
            var x = 0f;
            var wordPossible = false;
            var wordLen = 0;

            void Append(List<Segment> line, Run run, char ch)
            {
                if (run.Image >= 0)
                {
                    line.Add(new Segment { Run = run });
                    return;
                }
                if (line.Count > 0 && line[^1].Text != null && line[^1].Run.Equals(run))
                    line[^1] = new Segment { Run = run, Text = line[^1].Text + ch };
                else
                    line.Add(new Segment { Run = run, Text = ch.ToString() });
            }

            void Commit(int count)
            {
                var line = new List<Segment>();
                for (var i = 0; i < count; i++)
                    Append(line, current[i].Run, current[i].Ch);
                lines.Add(line);
                current.RemoveRange(0, count);
                x = 0;
                foreach (var (run, ch) in current)
                    x += run.Image >= 0 ? CharWidth('\0', run) : CharWidth(ch, run);
            }

            foreach (var run in runs)
            {
                if (run.Image >= 0)
                {
                    current.Add((run, '\0'));
                    x += CharWidth('\0', run);
                    wordPossible = false;
                    if (wrap && x > rectWidth && current.Count > 1)
                        Commit(current.Count - 1);
                    continue;
                }

                foreach (var ch in run.Text)
                {
                    if (ch == '\n')
                    {
                        Commit(current.Count);
                        wordPossible = false;
                        continue;
                    }

                    if (wordPossible)
                    {
                        if (char.IsWhiteSpace(ch))
                            wordLen = 0;
                        else if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '"' or '\'')
                            wordLen++;
                        else
                            wordPossible = false;
                    }
                    else if (char.IsWhiteSpace(ch))
                    {
                        wordLen = 0;
                        wordPossible = true;
                    }

                    current.Add((run, ch));
                    x += CharWidth(ch, run);

                    if (wrap && x > rectWidth)
                    {
                        if (wordPossible && wordLen < 20 && current.Count > 2)
                            Commit(current.Count - wordLen);
                        else if (current.Count > 1)
                            Commit(current.Count - 1);
                        wordPossible = false;
                    }
                }
            }
            Commit(current.Count);
            return lines;
        }
    }
}
