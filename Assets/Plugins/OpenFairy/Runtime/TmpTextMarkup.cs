#if FAIRYGUI_TMPRO
using System;
using System.Globalization;
using System.Net;
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

        private const string Literal = @"(?<literal><noparse>.*?(?:</noparse>|$))|";
        private static readonly Regex Tags = new(Literal +
            @"(?<escaped>\\\[)|(?<!\\)\[img\](?<image>.*?)\[/img\]|(?<!\\)\[(?<closing>/?)(?<tag>b|i|u|s|strike|sup|sub|size|color|url)(?:=(?<value>[^\]]+))?\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        private static readonly Regex Html = new(Literal + @"<(?<closing>/)?(?<tag>img|a)\b(?<attributes>[^>]*)>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        private static readonly Regex Attributes = new(
            @"(?<name>[\w-]+)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
            UpdateLinkRaycast();
        }

        private void OnDisable()
        {
            ReleaseLinkRaycast();
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
            if (string.IsNullOrEmpty(source))
                return source;
            string text = source.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
            if (!_text.richText)
                return text;
            if (ubb)
                text = ConvertUbb(text);
            if (html)
                text = Html.Replace(text, match =>
                {
                    if (match.Groups["literal"].Success)
                        return match.Value;
                    bool closing = match.Groups["closing"].Success;
                    if (match.Groups["tag"].Value.Equals("a", StringComparison.OrdinalIgnoreCase))
                        return closing ? LinkEnd() : LinkStart(Attribute(match.Groups["attributes"].Value, "href") ?? "");
                    if (closing)
                        return "";
                    string attributes = match.Groups["attributes"].Value;
                    string name = Attribute(attributes, "src");
                    if (name == null)
                        return match.Value;
                    float? width = Dimension(attributes, "width");
                    float? height = Dimension(attributes, "height");
                    return width.HasValue || height.HasValue
                        ? SizedSpriteTag(_text.spriteAsset, _text.font, name, width, height, Dimension(attributes, "baseline") ?? 0.8f)
                        : SpriteTag(name);
                });
            return _text.enableAutoSizing && _text.richText
                ? TextAutoSize.NormalizeSizeTags(text, _text.fontSizeMax) : text;
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
            if (tag == "strike")
                tag = "s";
            if (closing)
                return "</" + tag + ">";
            if (tag is "color" or "size")
                return value.Length == 0 ? match.Value : "<" + tag + "=" + (tag == "color" ? ConvertColor(value) : value) + ">";
            return "<" + tag + ">";
        });

        private string LinkStart(string value) => "<color=#" + ColorUtility.ToHtmlStringRGBA(linkColor) + ">"
            + (linkUnderline ? "<u>" : "") + "<link=\"" + WebUtility.HtmlEncode(value) + "\">";
        private string LinkEnd() => "</link>" + (linkUnderline ? "</u>" : "") + "</color>";

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
