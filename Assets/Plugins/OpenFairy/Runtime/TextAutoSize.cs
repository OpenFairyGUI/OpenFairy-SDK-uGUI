#if FAIRYGUI_TMPRO
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace OpenFairy.UGUI
{
    // Authoring policy around native TMP sizing; TMP owns shaping and layout.
    public static class TextAutoSize
    {
        private static readonly Regex AbsoluteSize = new(
            @"(?<literal><noparse>.*?(?:</noparse>|$))|<size=(?<quote>[""']?)(?<sign>[+-]?)(?<size>(?:\d+(?:\.\d*)?|\.\d+))(?:px)?\k<quote>>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        public static void ConfigureShrink(TMP_Text text, float authoredFontSize)
        {
            text.enableAutoSizing = false;
            text.fontSize = Mathf.Max(1, authoredFontSize);
            text.fontSizeMin = 1;
            text.fontSizeMax = text.fontSize;
            text.characterWidthAdjustment = 0;
            text.lineSpacingAdjustment = 0;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
        }

        public static void ConfigureEllipsis(TMP_Text text, float authoredFontSize)
        {
            text.enableAutoSizing = false;
            text.fontSize = authoredFontSize;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        // TMP fontSize is the fitted result when auto sizing is enabled.
        // Changing the authored point size must also change its upper bound.
        public static void SetFontSize(TMP_Text text, float fontSize)
        {
            if (text.enableAutoSizing)
                text.fontSizeMax = Mathf.Max(text.fontSizeMin, fontSize);
            text.fontSize = fontSize;
        }

        // Absolute rich-text point sizes must participate in global shrinking.
        // Relative percentages/em already follow TMP's fitted base point size.
        public static string NormalizeSizeTags(string markup, float authoredFontSize) =>
            AbsoluteSize.Replace(markup, match =>
            {
                if (match.Groups["literal"].Success)
                    return match.Value;
                float size = float.Parse(match.Groups["size"].Value, CultureInfo.InvariantCulture);
                string sign = match.Groups["sign"].Value;
                if (sign.Length > 0)
                    size = authoredFontSize + (sign == "-" ? -size : size);
                return "<size=" + (size / authoredFontSize * 100).ToString("R", CultureInfo.InvariantCulture) + "%>";
            });
    }
}
#endif
