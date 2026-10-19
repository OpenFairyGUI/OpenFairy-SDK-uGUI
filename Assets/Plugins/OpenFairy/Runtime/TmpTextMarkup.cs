#if FAIRYGUI_TMPRO
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class TmpTextMarkup : MonoBehaviour, ITextPreprocessor, IPointerClickHandler
    {
        public bool ubb;
        public bool html;
        public Color linkColor = new Color32(0x3A, 0x67, 0xCC, 0xFF);
        public bool linkUnderline = true;
        private TMP_Text _text;
        private Action<string> _onClickLink;
        private bool _ownsRaycast;
        private readonly List<GradientSpan> _gradients = new();
        // 复刻 HtmlParser：外层已指定颜色时链接保留该颜色；<font> 关闭时还原它打开的样式。
        private readonly Stack<bool> _linkColors = new();
        private readonly Stack<(string Closer, bool Color)> _fonts = new();
        private int _colorDepth;
        private readonly struct GradientSpan
        {
            public readonly int start, end;
            public readonly VertexGradient colors;
            public GradientSpan(int start, int end, VertexGradient colors)
            { this.start = start; this.end = end; this.colors = colors; }
        }

        private const string Literal = @"(?<literal><noparse>.*?(?:</noparse>|$))|";
        private static readonly Regex Tags = new(Literal +
            @"(?<escaped>\\\[)|(?<!\\)\[img\](?<image>.*?)\[/img\]|(?<!\\)\[(?<closing>/?)(?<tag>b|i|u|s|strike|sup|sub|size|color|url|align|font)(?:=(?<value>[^\]]+))?\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        private static readonly Regex Html = new(Literal + @"<(?<closing>/)?(?<tag>img|a|font|strike|color)\b(?<attributes>[^>]*)>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        private static readonly Regex Attributes = new(
            @"(?<name>[\w-]+)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ColorTags = new(Literal + @"<(?<closing>/)?color(?:=(?<value>[^>]+))?>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        public Action<string> onClickLink
        {
            get => _onClickLink;
            set
            {
                _onClickLink = value;
                _text ??= GetComponent<TMP_Text>();
                UpdateLinkRaycast();
            }
        }

        private void OnEnable() => Bind();

        public void Bind()
        {
            _text = GetComponent<TMP_Text>();
            _text.textPreprocessor = this;
            _text.OnPreRenderText -= ApplyGradients;
            _text.OnPreRenderText += ApplyGradients;
            UpdateLinkRaycast();
        }

        private void OnDisable()
        {
            ReleaseLinkRaycast();
            if (_text != null)
                _text.OnPreRenderText -= ApplyGradients;
            if (_text != null && ReferenceEquals(_text.textPreprocessor, this))
                _text.textPreprocessor = null;
        }

        private void UpdateLinkRaycast()
        {
            if (_onClickLink != null && isActiveAndEnabled)
            {
                if (!_text.raycastTarget)
                {
                    _text.raycastTarget = true;
                    _ownsRaycast = true;
                }
            }
            else
                ReleaseLinkRaycast();
        }

        private void ReleaseLinkRaycast()
        {
            if (!_ownsRaycast)
                return;
            _text.raycastTarget = false;
            _ownsRaycast = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _onClickLink == null)
                return;
            _text.ForceMeshUpdate();
            int index = TMP_TextUtilities.FindIntersectingLink(_text, eventData.position, eventData.pressEventCamera);
            if (index >= 0)
                _onClickLink(WebUtility.HtmlDecode(_text.textInfo.linkInfo[index].GetLinkID()));
        }

        public string PreprocessText(string source)
        {
            _gradients.Clear();
            if (string.IsNullOrEmpty(source))
                return source;
            string text = source.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
            if (!_text.richText)
                return text;
            if (ubb)
            {
                ResetStyles();
                text = ConvertUbb(text);
            }
            if (html)
            {
                ResetStyles();
                text = Html.Replace(text, match =>
                {
                    if (match.Groups["literal"].Success)
                        return match.Value;
                    bool closing = match.Groups["closing"].Success;
                    string tag = match.Groups["tag"].Value.ToLowerInvariant();
                    string attributes = match.Groups["attributes"].Value;
                    switch (tag)
                    {
                        case "a":
                            return closing ? LinkEnd() : LinkStart(Attribute(attributes, "href") ?? "");
                        case "strike":
                            return closing ? "</s>" : "<s>";
                        case "color":
                            _colorDepth += closing ? -1 : 1;
                            return match.Value;
                        case "font" when closing:
                            if (_fonts.Count == 0)
                                return "";
                            var font = _fonts.Pop();
                            if (font.Color) _colorDepth--;
                            return font.Closer;
                        case "font":
                        {
                            string color = Attribute(attributes, "color"), size = Attribute(attributes, "size");
                            if (color != null) _colorDepth++;
                            _fonts.Push(((size != null ? "</size>" : "") + (color != null ? "</color>" : ""), color != null));
                            return (color != null ? "<color=" + ConvertColor(color) + ">" : "") + (size != null ? "<size=" + size + ">" : "");
                        }
                    }
                    if (closing)
                        return "";
                    string name = Attribute(attributes, "src");
                    if (name == null)
                        return match.Value;
                    float? width = Dimension(attributes, "width");
                    float? height = Dimension(attributes, "height");
                    return width.HasValue || height.HasValue
                        ? SizedSpriteTag(_text.spriteAsset, _text.font, name, width, height, Dimension(attributes, "baseline") ?? 0.8f)
                        : SpriteTag(name);
                });
            }
            if (_text.enableAutoSizing)
                text = TextAutoSize.NormalizeSizeTags(text, _text.fontSizeMax);
            return ConvertGradients(text);
        }

        private string ConvertGradients(string source)
        {
            if (source.IndexOf(',') < 0)
                return source;
            var result = new StringBuilder(source.Length);
            var stack = new Stack<VertexGradient?>();
            VertexGradient? current = null;
            void Append(string value)
            {
                int start = result.Length;
                result.Append(value);
                if (current.HasValue && value.Length != 0)
                    _gradients.Add(new GradientSpan(start, result.Length, current.Value));
            }
            int position = 0;
            foreach (Match match in ColorTags.Matches(source))
            {
                Append(source.Substring(position, match.Index - position));
                if (match.Groups["literal"].Success)
                    Append(match.Value);
                else if (match.Groups["closing"].Success)
                {
                    current = stack.Count == 0 ? null : stack.Pop();
                    result.Append(match.Value);
                }
                else
                {
                    stack.Push(current);
                    current = null;
                    var parts = match.Groups["value"].Value.Trim('\'', '"').Split(',');
                    if (parts.Length >= 2 && parts.Length <= 4)
                    {
                        var colors = new Color[parts.Length];
                        bool valid = true;
                        for (int i = 0; i < parts.Length; i++)
                            valid &= ColorUtility.TryParseHtmlString(ConvertColor(parts[i].Trim()), out colors[i]);
                        if (valid)
                            current = new VertexGradient(colors[0], parts.Length > 2 ? colors[2] : colors[0],
                                colors[1], parts.Length > 3 ? colors[3] : parts.Length > 2 ? colors[2] : colors[1]);
                    }
                    // Keep TMP's native color stack for nested solid colors and inherited alpha.
                    result.Append(current.HasValue ? "<color=#FFFFFFFF>" : match.Value);
                }
                position = match.Index + match.Length;
            }
            Append(source.Substring(position));
            return result.ToString();
        }

        private void ApplyGradients(TMP_TextInfo info)
        {
            int span = 0;
            static Color32 Alpha(Color32 color, byte alpha)
            { color.a = (byte)(color.a * alpha / 255); return color; }
            for (int i = 0; i < info.characterCount && span < _gradients.Count; i++)
            {
                var character = info.characterInfo[i];
                while (span < _gradients.Count && character.index >= _gradients[span].end)
                    span++;
                if (span == _gradients.Count)
                    break;
                if (!character.isVisible || character.index < _gradients[span].start)
                    continue;
                var gradient = _gradients[span].colors;
                var colors = info.meshInfo[character.materialReferenceIndex].colors32;
                int vertex = character.vertexIndex;
                colors[vertex] = Alpha(gradient.bottomLeft, colors[vertex].a);
                colors[vertex + 1] = Alpha(gradient.topLeft, colors[vertex + 1].a);
                colors[vertex + 2] = Alpha(gradient.topRight, colors[vertex + 2].a);
                colors[vertex + 3] = Alpha(gradient.bottomRight, colors[vertex + 3].a);
            }
        }

        private string ConvertUbb(string source) => Tags.Replace(source, match =>
        {
            if (match.Groups["literal"].Success)
                return match.Value;
            if (match.Groups["escaped"].Success)
                return "[";
            if (match.Groups["image"].Success)
                return SpriteTag(match.Groups["image"].Value.Trim());
            bool closing = match.Groups["closing"].Value == "/";
            string tag = match.Groups["tag"].Value.ToLowerInvariant();
            string value = match.Groups["value"].Value.Trim('\'', '"');
            if (tag == "url")
            {
                if (closing)
                    return LinkEnd();
                if (!match.Groups["value"].Success)
                {
                    int start = match.Index + match.Length;
                    int end = source.IndexOf('[', start);
                    value = source.Substring(start, (end < 0 ? source.Length : end) - start);
                }
                return LinkStart(WebUtility.HtmlDecode(value));
            }
            if (tag == "font") // 本后端不按名称切换字体，消费标签以免原样显示
                return "";
            if (tag == "strike")
                tag = "s";
            if (tag == "color")
                _colorDepth += closing ? -1 : 1;
            if (closing)
                return "</" + tag + ">";
            if (tag is "color" or "size" or "align")
                return value.Length == 0 ? match.Value : "<" + tag + "=" + (tag == "color" ? ConvertColor(value) : value) + ">";
            return "<" + tag + ">";
        });

        private void ResetStyles()
        {
            _colorDepth = 0;
            _linkColors.Clear();
            _fonts.Clear();
        }

        private string LinkStart(string value)
        {
            bool colored = _colorDepth <= 0;
            _linkColors.Push(colored);
            return (colored ? "<color=#" + ColorUtility.ToHtmlStringRGBA(linkColor) + ">" : "")
                + (linkUnderline ? "<u>" : "") + "<link=\"" + WebUtility.HtmlEncode(value) + "\">";
        }

        private string LinkEnd() => "</link>" + (linkUnderline ? "</u>" : "")
            + (_linkColors.Count > 0 && _linkColors.Pop() ? "</color>" : "");

        // A name is a glyph in the assigned native sprite asset, never a resource URL.
        public static string SpriteTag(string name)
        {
            if (name.IndexOf('"') >= 0 || name.IndexOf('<') >= 0 || name.IndexOf('>') >= 0)
                throw new ArgumentException("Sprite names cannot contain native tag delimiters.", nameof(name));
            return "<sprite name=\"" + name + "\">";
        }

        public static string SpriteTag(TMP_Text text, string name, float width, float height, float baseline = 0.8f) =>
            SizedSpriteTag(text.spriteAsset, text.font, name, width, height, baseline);

        public static string SpriteTag(TMP_SpriteAsset asset, string name, float width, float height, float baseline = 0.8f) =>
            SizedSpriteTag(asset, null, name, width, height, baseline);

        private static string SizedSpriteTag(TMP_SpriteAsset source, TMP_FontAsset font, string name, float? width, float? height, float baseline)
        {
            var asset = TMP_SpriteAsset.SearchForSpriteByHashCode(source, TMP_TextUtilities.GetHashCode(name), true, out int index);
            if (asset == null || index < 0)
                throw new ArgumentException($"Sprite glyph '{name}' was not found in the assigned sprite asset.", nameof(name));
            var character = asset.spriteCharacterTable[index];
            var glyph = character.glyph;
            var metrics = glyph.metrics;
            float targetHeight = height ?? width.Value * metrics.height / metrics.width;
            float targetWidth = width ?? targetHeight * metrics.width / metrics.height;
            if (!(targetWidth > 0) || !(targetHeight > 0))
                throw new ArgumentOutOfRangeException(nameof(width), "Inline sprite dimensions must be positive.");
            float perPoint = asset.faceInfo.pointSize > 0
                ? asset.faceInfo.scale / asset.faceInfo.pointSize
                : font != null ? font.faceInfo.ascentLine * font.faceInfo.scale / (metrics.height * font.faceInfo.pointSize)
                : throw new ArgumentException("Sprite assets without face metrics require the TMP_Text overload.", nameof(source));
            perPoint *= character.scale * glyph.scale;
            float pointSize = targetHeight / (metrics.height * perPoint);
            float scale = targetWidth / (metrics.width * perPoint * pointSize);
            float offset = baseline * targetHeight - metrics.horizontalBearingY * perPoint * pointSize;
            string tag = SpriteTag(name);
            if (!Mathf.Approximately(offset, 0))
                tag = "<voffset=" + Number(offset) + ">" + tag + "</voffset>";
            if (!Mathf.Approximately(scale, 1))
                tag = "<scale=" + Number(scale) + ">" + tag + "</scale>";
            return "<size=" + Number(pointSize) + ">" + tag + "</size>";
        }

        private static string Attribute(string attributes, string name)
        {
            foreach (Match match in Attributes.Matches(attributes))
                if (match.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return WebUtility.HtmlDecode(match.Groups["value"].Value);
            return null;
        }

        private static float? Dimension(string attributes, string name) => Attribute(attributes, name) is { } value
            ? float.Parse(value, CultureInfo.InvariantCulture) : null;
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string ConvertColor(string color) => color.Length == 9 && color[0] == '#'
            ? "#" + color.Substring(3, 6) + color.Substring(1, 2) : color;
    }
}
#endif
