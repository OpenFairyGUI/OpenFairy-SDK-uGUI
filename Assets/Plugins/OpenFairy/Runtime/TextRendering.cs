using UnityEngine;
using UnityEngine.UI;
#if FAIRYGUI_TMPRO
using TMPro;
#endif

namespace OpenFairy.UGUI
{
    // Styling shared by generated controls and scene-created text.
    public static class TextRendering
    {
        public static void SetAlignment(this Graphic text, TextAnchor alignment)
        {
#if FAIRYGUI_TMPRO
            if (text is TMP_Text tmp)
            {
                tmp.alignment = alignment switch
                {
                    TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                    TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                    TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                    TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
                    TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                    TextAnchor.MiddleRight => TextAlignmentOptions.Right,
                    TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                    TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                    _ => TextAlignmentOptions.BottomRight,
                };
                return;
            }
#endif
            ((UnityEngine.UI.Text)text).alignment = alignment;
        }

        public static void SetFontStyle(this Graphic text, FontStyle style)
        {
#if FAIRYGUI_TMPRO
            if (text is TMP_Text tmp)
            {
                tmp.fontStyle = style switch
                {
                    FontStyle.Bold => FontStyles.Bold,
                    FontStyle.Italic => FontStyles.Italic,
                    FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
                    _ => FontStyles.Normal,
                };
                return;
            }
#endif
            ((UnityEngine.UI.Text)text).fontStyle = style;
        }

        public static void SetWrapping(this Graphic text, HorizontalWrapMode mode)
        {
#if FAIRYGUI_TMPRO
            if (text is TMP_Text tmp)
            {
                tmp.textWrappingMode = mode == HorizontalWrapMode.Wrap
                    ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                return;
            }
#endif
            ((UnityEngine.UI.Text)text).horizontalOverflow = mode;
        }

        public static void SetOverflow(this Graphic text, VerticalWrapMode mode)
        {
#if FAIRYGUI_TMPRO
            if (text is TMP_Text tmp)
            {
                tmp.overflowMode = mode == VerticalWrapMode.Overflow
                    ? TextOverflowModes.Overflow : TextOverflowModes.Truncate;
                return;
            }
#endif
            ((UnityEngine.UI.Text)text).verticalOverflow = mode;
        }


        public static void SetUbb(this Graphic text, bool enabled)
        {
#if FAIRYGUI_TMPRO
            if (text is TMP_Text tmp)
            {
                var markup = tmp.GetComponent<TmpTextMarkup>();
                if (markup == null)
                    markup = tmp.gameObject.AddComponent<TmpTextMarkup>();
                markup.ubb = enabled;
                markup.Bind();
                tmp.richText = enabled || markup.html;
                tmp.SetAllDirty();
                return;
            }
#endif
            ((TextField)text).ubb = enabled;
        }

        public static void RefreshText(this Graphic text)
        {
#if FAIRYGUI_TMPRO
            if (text is TMP_Text tmp)
            {
                tmp.ForceMeshUpdate(true);
                return;
            }
#endif
            if (text is TextField legacy)
                legacy.WarmUp();
            text.SetAllDirty();
        }

        public static Color GetOutlineColor(this Graphic text)
        {
#if FAIRYGUI_TMPRO
            if (text is TMP_Text tmp)
                return tmp.outlineColor;
#endif
            var stroke = text.GetComponent<TextStroke>();
            return stroke != null ? stroke.color : Color.clear;
        }

        public static void SetOutlineColor(this Graphic text, Color color)
        {
#if FAIRYGUI_TMPRO
            if (text is TMP_Text tmp)
            {
                tmp.outlineColor = color;
                return;
            }
#endif
            var stroke = text.GetComponent<TextStroke>();
            if (stroke != null)
                stroke.color = color;
        }
    }
}
