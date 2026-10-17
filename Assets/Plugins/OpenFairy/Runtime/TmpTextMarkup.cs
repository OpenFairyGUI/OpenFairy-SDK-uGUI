#if FAIRYGUI_TMPRO
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace OpenFairy.UGUI
{
    // Preserve FairyGUI source text in bindings/gears while TMP receives native tags.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class TmpTextMarkup : MonoBehaviour, ITextPreprocessor
    {
        public bool ubb;
        public bool html;
        private TMP_Text _text;
        private static readonly Regex Tags = new(@"(?<!\\)\[(/?)(b|i|u|s|size|color|url)(?:=([^\]]+))?\]", RegexOptions.Compiled);

        private void OnEnable() => Bind();

        public void Bind()
        {
            _text = GetComponent<TMP_Text>();
            _text.textPreprocessor = this;
        }

        private void OnDisable()
        {
            if (_text != null && ReferenceEquals(_text.textPreprocessor, this))
                _text.textPreprocessor = null;
        }

        public string PreprocessText(string source)
        {
            if (string.IsNullOrEmpty(source))
                return source;
            var text = source.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
            if (!ubb)
                return NormalizeSizes(text);
            return NormalizeSizes(Tags.Replace(text, match =>
            {
                var closing = match.Groups[1].Value == "/";
                var tag = match.Groups[2].Value;
                var value = match.Groups[3].Value;
                if (tag == "url")
                    return closing ? "</link>" : "<link=\"" + value + "\">";
                if (closing)
                    return "</" + tag + ">";
                if (tag is "color" or "size")
                    return value.Length == 0 ? match.Value : "<" + tag + "=" + (tag == "color" ? ConvertColor(value) : value) + ">";
                return "<" + tag + ">";
            }).Replace("\\[", "["));
        }

        private string NormalizeSizes(string markup) => _text.enableAutoSizing && _text.richText
            ? TextAutoSize.NormalizeSizeTags(markup, _text.fontSizeMax) : markup;

        private static string ConvertColor(string color)
        {
            // FairyGUI/OpenFairy use AARRGGBB; TMP uses RRGGBBAA.
            return color.Length == 9 && color[0] == '#'
                ? "#" + color.Substring(3, 6) + color.Substring(1, 2)
                : color;
        }
    }
}
#endif
