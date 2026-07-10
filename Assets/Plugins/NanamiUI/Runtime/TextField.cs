using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ZLinq;

namespace NanamiUI
{
    // 排版逻辑复刻 FairyGUI 的 TextField + DynamicFont：
    // 行高 = round(size*1.25)，基线 = round(size)，四周 2px gutter，对齐偏移取整，
    // 下划线取 "_" 字形中心 UV，行内图片基线 = 高度*0.8、占宽 = 宽度+2。
    // 支持 UBB（color/渐变/b/i/u/size/img）与 richtext 的 img/a 标签，以及位图字体。
    public class TextField : UnityEngine.UI.Text, IPointerClickHandler
    {
        public static string defaultFont = "Arial";

        public string fontNames;
        public int leading = 3;
        public int letterSpacing;
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
        private readonly List<Run> _runs = new();
        private readonly Stack<Run> _runStack = new();
        private readonly StringBuilder _buffer = new();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || onClickLink == null || _links.Count == 0)
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

        // 从标签串里取属性值，支持带引号或裸值（如 font color='#fff' size=20）。
        private static string AttrValue(string tag, string name)
        {
            var idx = tag.IndexOf(name + "=", StringComparison.Ordinal);
            if (idx < 0)
                return null;
            var start = idx + name.Length + 1;
            if (start < tag.Length && (tag[start] == '\'' || tag[start] == '"'))
            {
                var quote = tag[start];
                var end = tag.IndexOf(quote, start + 1);
                return end > start ? tag[(start + 1)..end] : null;
            }
            var stop = start;
            while (stop < tag.Length && tag[stop] != ' ')
                stop++;
            return tag[start..stop];
        }

        private static string ParseHref(string tag)
        {
            var idx = tag.IndexOf("href", StringComparison.Ordinal);
            if (idx < 0)
                return "";
            var single = tag.IndexOf('\'', idx);
            var doubleQuote = tag.IndexOf('"', idx);
            var quote = single < 0 ? doubleQuote : doubleQuote < 0 ? single : Mathf.Min(single, doubleQuote);
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
            Font.textureRebuilt += OnFontRebuilt;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            Font.textureRebuilt -= OnFontRebuilt;
            base.OnDisable();
        }

        // 动态字体图集重建后字形 UV 全部失效，缓存的排版必须作废。
        private void OnFontRebuilt(Font rebuilt)
        {
            if (rebuilt == font)
                _layoutValid = false;
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            toFill.Clear();
            EnsureLayout();
            var rect = rectTransform.rect;
            foreach (var quad in _quads)
            {
                var v = toFill.currentVertCount;
                toFill.AddVert(new Vector3(rect.xMin + quad.Rect.xMin, rect.yMax - quad.Rect.yMax), quad.BL, quad.UvBL);
                toFill.AddVert(new Vector3(rect.xMin + quad.Rect.xMin, rect.yMax - quad.Rect.yMin), quad.TL, quad.UvTL);
                toFill.AddVert(new Vector3(rect.xMin + quad.Rect.xMax, rect.yMax - quad.Rect.yMin), quad.TR, quad.UvTR);
                toFill.AddVert(new Vector3(rect.xMin + quad.Rect.xMax, rect.yMax - quad.Rect.yMax), quad.BR, quad.UvBR);
                toFill.AddTriangle(v, v + 1, v + 2);
                toFill.AddTriangle(v, v + 2, v + 3);
            }
        }

        // 排版缓存（对齐 FairyGUI 的 _textChanged 门控）：GearColor/Transition 的纯着色 tween 每帧令 vertices dirty，
        // 但文本/尺寸/字体没变就不重解析重排版——无 UBB 色标时只回填 quad 颜色，有色标才全量重排。
        private bool _layoutValid;
        private bool _hasColorTags;
        private string _layoutText;
        private Color _layoutColor;
        private Vector2 _layoutRectSize;
        private int _layoutFontSize;
        private int _layoutLeading;
        private int _layoutLetterSpacing;
        private FontStyle _layoutStyle;
        private TextAnchor _layoutAlignment;
        private HorizontalWrapMode _layoutHOverflow;
        private VerticalWrapMode _layoutVOverflow;
        private Font _layoutFont;

        private void EnsureLayout()
        {
            var rectSize = rectTransform.rect.size;
            if (_layoutValid && ReferenceEquals(_layoutText, text) && _layoutRectSize == rectSize
                && _layoutFontSize == fontSize && _layoutStyle == fontStyle && _layoutAlignment == alignment
                && _layoutHOverflow == horizontalOverflow && _layoutVOverflow == verticalOverflow
                && _layoutLeading == leading && _layoutLetterSpacing == letterSpacing
                && ReferenceEquals(_layoutFont, font))
            {
                if (_layoutColor == color)
                    return;
                if (!_hasColorTags) // 全部 quad 都用基础色：原地回填颜色，免重排版
                {
                    _layoutColor = color;
                    Color32 c = color;
                    for (var i = 0; i < _quads.Count; i++)
                    {
                        var quad = _quads[i];
                        quad.TL = quad.BL = quad.TR = quad.BR = c;
                        _quads[i] = quad;
                    }
                    return;
                }
            }
            Layout();
        }

        // 提前请求全部字符，避免动态字体图集在渲染同帧内扩容导致已生成网格失效。
        public void WarmUp()
        {
            if (bitmapFont != null || font == null)
                return;
            RequestChars(Parse());
        }

        private void RequestChars(List<Run> runs)
        {
            foreach (var run in runs)
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
                var go = new GameObject("img" + image, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(position.x, -position.y);
                var sprite = imageSprites[image];
                rt.sizeDelta = sprite.rect.size;
                var imageComponent = go.GetComponent<UnityEngine.UI.Image>();
                imageComponent.sprite = sprite;
                imageComponent.raycastTarget = false;
            }
        }

        // --- 解析 ---

        private List<Run> Parse()
        {
            _hasColorTags = false;
            _runs.Clear();
            _runStack.Clear();
            _buffer.Clear();
            var current = new Run { Size = fontSize, Style = fontStyle, Underline = underlined, Image = -1 };
            SetColor(ref current, color);
            // 复刻 FairyGUI ParseText：归一 \r\n → \n，制表符按 4 空格宽展开。
            var source = (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\t", "    ");
            if (!ubb && !html)
            {
                current.Text = source;
                _runs.Add(current);
                return _runs;
            }

            var imageIndex = 0;

            var value = source;
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (ubb && ch == '[')
                {
                    var end = value.IndexOf(']', i);
                    var tag = end > i ? value[(i + 1)..end] : null;
                    if (tag == "img")
                    {
                        var close = value.IndexOf("[/img]", end, StringComparison.Ordinal);
                        if (close < 0) // 无闭合：按原文输出（复刻 UBBParser 对残缺标签的容错），不能让 i 倒退
                        {
                            _buffer.Append(ch);
                            continue;
                        }
                        Flush(current);
                        _runs.Add(new Run { Image = imageIndex++ });
                        i = close + 5;
                        continue;
                    }
                    if (tag != null && HandleTag(tag, ref current))
                    {
                        i = end;
                        continue;
                    }
                    _buffer.Append(ch);
                }
                else if (html && ch == '<')
                {
                    var end = value.IndexOf('>', i);
                    if (end < 0) // 无闭合：按原文输出（复刻 HtmlParser 对残缺标签的容错）
                    {
                        _buffer.Append(ch);
                        continue;
                    }
                    var tag = value[(i + 1)..end].Trim();
                    if (tag.StartsWith("img"))
                    {
                        Flush(current);
                        _runs.Add(new Run { Image = imageIndex++ });
                    }
                    else if (tag.StartsWith("a"))
                    {
                        // FairyGUI HtmlParseOptions 默认链接样式：#3A67CC + 下划线。
                        Flush(current);
                        _runStack.Push(current);
                        _hasColorTags = true;
                        SetColor(ref current, new Color32(0x3A, 0x67, 0xCC, 0xFF));
                        current.Underline = true;
                        current.Href = ParseHref(tag);
                    }
                    else if (tag.StartsWith("font"))
                    {
                        Flush(current);
                        _runStack.Push(current);
                        if (AttrValue(tag, "color") is { } col)
                        {
                            _hasColorTags = true;
                            SetColor(ref current, ParseColor(col));
                        }
                        if (AttrValue(tag, "size") is { } sz && int.TryParse(sz, out var size))
                            current.Size = size;
                    }
                    else if (tag is "b" or "i" or "u")
                    {
                        Flush(current);
                        _runStack.Push(current);
                        if (tag == "b") current.Style |= FontStyle.Bold;
                        else if (tag == "i") current.Style |= FontStyle.Italic;
                        else current.Underline = true;
                    }
                    else if (tag is "/a" or "/font" or "/b" or "/i" or "/u")
                    {
                        Flush(current);
                        if (_runStack.Count > 0)
                            current = _runStack.Pop();
                    }
                    else if (tag is "br" or "br/")
                        _buffer.Append('\n');
                    i = end;
                }
                else
                    _buffer.Append(ch);
            }
            Flush(current);
            return _runs;
        }

        private void Flush(Run current)
        {
            if (_buffer.Length == 0)
                return;
            current.Text = _buffer.ToString();
            _runs.Add(current);
            _buffer.Clear();
        }

        private bool HandleTag(string tag, ref Run current)
        {
            switch (tag)
            {
                case "b":
                case "i":
                    Flush(current);
                    _runStack.Push(current);
                    var italic = tag == "i" ? FontStyle.Italic : FontStyle.Normal;
                    var bold = tag == "b" ? FontStyle.Bold : FontStyle.Normal;
                    current.Style |= bold | italic;
                    return true;
                case "u":
                    Flush(current);
                    _runStack.Push(current);
                    current.Underline = true;
                    return true;
                case "/b":
                case "/i":
                case "/u":
                case "/size":
                case "/color":
                case "/url":
                    Flush(current);
                    if (_runStack.Count > 0)
                        current = _runStack.Pop();
                    return true;
            }
            if (tag.StartsWith("url"))
            {
                // [url=href] 超链接（复刻 UBBParser）：默认样式 #3A67CC + 下划线，命中回调同 <a>。
                Flush(current);
                _runStack.Push(current);
                _hasColorTags = true;
                SetColor(ref current, new Color32(0x3A, 0x67, 0xCC, 0xFF));
                current.Underline = true;
                current.Href = tag.StartsWith("url=") ? tag[4..].Trim('\'', '"') : "";
                return true;
            }
            if (tag.StartsWith("size="))
            {
                Flush(current);
                _runStack.Push(current);
                current.Size = int.Parse(tag[5..]);
                return true;
            }
            if (tag.StartsWith("color="))
            {
                Flush(current);
                _runStack.Push(current);
                _hasColorTags = true;
                var parts = tag[6..].Split(',');
                current.TL = ParseColor(parts[0]);
                current.BL = parts.Length > 1 ? ParseColor(parts[1]) : current.TL;
                current.TR = parts.Length > 2 ? ParseColor(parts[2]) : current.TL;
                current.BR = parts.Length > 3 ? ParseColor(parts[3]) : parts.Length > 2 ? current.TR : current.BL;
                return true;
            }
            return false;
        }

        // FillVertexColors 语义：2 色为纵向渐变，4 色为四角 [左上,左下,右上,右下]。
        private static void SetColor(ref Run run, Color32 color)
        {
            run.TL = run.BL = run.TR = run.BR = color;
        }

        private static Color32 ParseColor(string value)
        {
            ColorUtility.TryParseHtmlString(value.Length == 9 ? $"#{value[3..]}{value[1..3]}" : value, out var color);
            return color;
        }

        // --- 排版 ---

        private struct Character
        {
            public int Run;
            public char Value;
            public float Width;
        }

        private struct Line
        {
            public int Start;
            public int Count;
            public float Baseline;
            public float Height;
            public float Width;
        }

        private readonly List<Character> _pendingCharacters = new();
        private readonly List<Character> _characters = new();
        private readonly List<Line> _lines = new();

        private void Layout()
        {
            _layoutValid = true;
            _layoutText = text;
            _layoutColor = color;
            _layoutRectSize = rectTransform.rect.size;
            _layoutFontSize = fontSize;
            _layoutLeading = leading;
            _layoutLetterSpacing = letterSpacing;
            _layoutStyle = fontStyle;
            _layoutAlignment = alignment;
            _layoutHOverflow = horizontalOverflow;
            _layoutVOverflow = verticalOverflow;
            _layoutFont = font;

            _quads.Clear();
            _placements.Clear();
            _links.Clear();
            var runs = Parse();
            if (bitmapFont == null)
            {
                if (font == null)
                    return;
                RequestChars(runs);
            }

            var rect = rectTransform.rect;
            var wrap = horizontalOverflow == HorizontalWrapMode.Wrap;
            var rectWidth = rect.width - 4;
            BreakLines(runs, wrap, rectWidth);

            var hAlign = (int)alignment % 3;
            var vAlign = (int)alignment / 3;
            var lineSpacing = leading - 1; // TextField: 实际行距 = lineSpacing - 1
            var textHeight = 4f + _lines.AsValueEnumerable().Sum(line => line.Height) + (_lines.Count - 1) * lineSpacing;

            var y = 2f + (vAlign == 1 ? (int)(Mathf.Max(0, rect.height - textHeight) / 2)
                : vAlign == 2 ? Mathf.Max(0, rect.height - textHeight) : 0);

            for (var lineIndex = 0; lineIndex < _lines.Count; lineIndex++)
            {
                var line = _lines[lineIndex];
                if (verticalOverflow == VerticalWrapMode.Truncate && lineIndex > 0 && y + line.Height > rect.height - 2)
                    break;

                var indent = hAlign == 1 ? (int)((rectWidth - line.Width) / 2) : hAlign == 2 ? rectWidth - line.Width : 0;
                var x = 2f + Mathf.Max(0, indent);
                var baselineY = y + line.Baseline;
                var runStart = x;
                var activeRun = -1;

                for (var i = line.Start; i < line.Start + line.Count; i++)
                {
                    var character = _characters[i];
                    var run = runs[character.Run];
                    if (run.Image >= 0)
                    {
                        if (activeRun >= 0)
                        {
                            FinishRun(runs[activeRun], runStart, x, y, line.Height, baselineY);
                            activeRun = -1;
                        }
                        var sprite = imageSprites[run.Image];
                        _placements.Add((run.Image, new Vector2(x + 1, baselineY - sprite.rect.height * 0.8f)));
                        x += character.Width;
                        continue;
                    }

                    if (activeRun != character.Run)
                    {
                        if (activeRun >= 0)
                            FinishRun(runs[activeRun], runStart, x, y, line.Height, baselineY);
                        activeRun = character.Run;
                        runStart = x;
                    }
                    x += DrawChar(character.Value, run, x, baselineY);
                }
                if (activeRun >= 0)
                    FinishRun(runs[activeRun], runStart, x, y, line.Height, baselineY);
                y += line.Height + lineSpacing;
            }
        }

        private void FinishRun(Run run, float from, float to, float lineY, float lineHeight, float baselineY)
        {
            if (run.Underline)
                DrawUnderline(run, from, to, baselineY);
            if (run.Href != null && to > from)
                _links.Add((new Rect(from, lineY, to - from, lineHeight), run.Href));
        }

        private float DrawChar(char ch, Run run, float x, float baselineY)
        {
            if (bitmapFont != null)
            {
                if (ch == ' ')
                    return Mathf.RoundToInt(bitmapFont.size / 2f) + letterSpacing;
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
                return glyph.advance + letterSpacing;
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
            return info.advance + letterSpacing;
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
                return imageSprites[run.Image].rect.width + 2 + letterSpacing;
            if (bitmapFont != null)
                return ch == ' ' ? Mathf.RoundToInt(bitmapFont.size / 2f) + letterSpacing
                    : bitmapFont.TryGetGlyph(ch, out var glyph) ? glyph.advance + letterSpacing : 0;
            return font.GetCharacterInfo(ch, out var info, run.Size, run.Style) ? info.advance + letterSpacing : 0;
        }

        private (float Baseline, float Height, float Width) MeasureLine(int start, int count)
        {
            float baseline = 0, descent = 0, width = 0;
            for (var i = start; i < start + count; i++)
            {
                var character = _characters[i];
                var run = _runs[character.Run];
                if (run.Image >= 0)
                {
                    var size = imageSprites[run.Image].rect.size;
                    baseline = Mathf.Max(baseline, size.y * 0.8f);
                    descent = Mathf.Max(descent, size.y * 0.2f);
                    width += character.Width;
                    continue;
                }
                float glyphBaseline, glyphHeight;
                if (bitmapFont != null)
                {
                    glyphHeight = bitmapFont.TryGetGlyph(character.Value, out var glyph)
                        ? Mathf.Max(bitmapFont.size, glyph.lineHeight)
                        : bitmapFont.size;
                    glyphBaseline = glyphHeight;
                }
                else
                {
                    glyphBaseline = Mathf.RoundToInt(run.Size);
                    glyphHeight = Mathf.RoundToInt(run.Size * 1.25f);
                }
                baseline = Mathf.Max(baseline, glyphBaseline);
                descent = Mathf.Max(descent, glyphHeight - glyphBaseline);
                width += character.Width;
            }
            if (baseline == 0)
            {
                baseline = bitmapFont != null ? bitmapFont.size : Mathf.RoundToInt(fontSize);
                descent = bitmapFont != null ? 0 : Mathf.RoundToInt(fontSize * 1.25f) - baseline;
            }
            return (baseline, baseline + descent, width);
        }

        // 复刻 TextField.BuildLines：字符加入后超宽才换行；仅空格后的词整体回退（wordLen<20），否则只移当前字符。
        private void BreakLines(List<Run> runs, bool wrap, float rectWidth)
        {
            _pendingCharacters.Clear();
            _characters.Clear();
            _lines.Clear();
            var x = 0f;
            var wordPossible = false;
            var wordLen = 0;

            for (var runIndex = 0; runIndex < runs.Count; runIndex++)
            {
                var run = runs[runIndex];
                if (run.Image >= 0)
                {
                    var width = CharWidth('\0', run);
                    _pendingCharacters.Add(new Character { Run = runIndex, Width = width });
                    x += width;
                    wordPossible = false;
                    if (wrap && x > rectWidth && _pendingCharacters.Count > 1)
                        CommitLine(_pendingCharacters.Count - 1, ref x);
                    continue;
                }

                foreach (var ch in run.Text)
                {
                    if (ch == '\n')
                    {
                        CommitLine(_pendingCharacters.Count, ref x);
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

                    var width = CharWidth(ch, run);
                    _pendingCharacters.Add(new Character { Run = runIndex, Value = ch, Width = width });
                    x += width;

                    if (wrap && x > rectWidth)
                    {
                        if (wordPossible && wordLen < 20 && _pendingCharacters.Count > 2)
                            CommitLine(_pendingCharacters.Count - wordLen, ref x);
                        else if (_pendingCharacters.Count > 1)
                            CommitLine(_pendingCharacters.Count - 1, ref x);
                        wordPossible = false;
                    }
                }
            }
            CommitLine(_pendingCharacters.Count, ref x);
        }

        private void CommitLine(int count, ref float width)
        {
            var start = _characters.Count;
            for (var i = 0; i < count; i++)
                _characters.Add(_pendingCharacters[i]);
            var metrics = MeasureLine(start, count);
            _lines.Add(new Line
            {
                Start = start,
                Count = count,
                Baseline = metrics.Baseline,
                Height = metrics.Height,
                Width = metrics.Width,
            });
            _pendingCharacters.RemoveRange(0, count);
            width = _pendingCharacters.AsValueEnumerable().Sum(character => character.Width);
        }
    }
}
