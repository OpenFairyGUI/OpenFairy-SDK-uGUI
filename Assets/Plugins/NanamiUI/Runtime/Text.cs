using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    // 排版逻辑复刻 FairyGUI 的 TextField + DynamicFont：
    // ascent = fontSize，行高 = round(fontSize * 1.25)，四周 2px gutter，对齐偏移取整。
    public class Text : UnityEngine.UI.Text
    {
        public static string defaultFont = "Arial";

        public string fontNames;
        public int leading = 3;

        private static readonly Dictionary<string, Font> Fonts = new();

        protected override void OnEnable()
        {
            if (font == null)
            {
                var names = string.IsNullOrEmpty(fontNames) ? defaultFont : fontNames;
                if (!Fonts.TryGetValue(names, out var osFont))
                    Fonts[names] = osFont = Font.CreateDynamicFontFromOSFont(names.Split(','), 16);
                font = osFont;
            }
            base.OnEnable();
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            toFill.Clear();
            if (font == null || string.IsNullOrEmpty(text))
                return;

            font.RequestCharactersInTexture(text, fontSize, fontStyle);

            var rect = rectTransform.rect;
            var lineHeight = Mathf.RoundToInt(fontSize * 1.25f);
            var baseline = Mathf.RoundToInt((float)fontSize);
            var lines = text.Split('\n');
            var hAlign = (int)alignment % 3;
            var vAlign = (int)alignment / 3;

            var textHeight = 4f + lines.Length * lineHeight + (lines.Length - 1) * leading;
            var yOffset = vAlign == 1 ? (int)(Mathf.Max(0, rect.height - textHeight) / 2)
                : vAlign == 2 ? Mathf.Max(0, rect.height - textHeight) : 0;

            var y = 2f + yOffset;
            var color32 = (Color32)color;
            foreach (var line in lines)
            {
                var width = 0f;
                foreach (var ch in line)
                    if (font.GetCharacterInfo(ch, out var info, fontSize, fontStyle))
                        width += info.advance;

                var indent = hAlign == 1 ? (int)((rect.width - 4 - width) / 2)
                    : hAlign == 2 ? rect.width - 4 - width : 0;
                var x = 2 + Mathf.Max(0, indent);

                var baselineY = rect.yMax - y - baseline;
                foreach (var ch in line)
                {
                    if (!font.GetCharacterInfo(ch, out var info, fontSize, fontStyle))
                        continue;
                    if (info.glyphWidth > 0)
                    {
                        var v = toFill.currentVertCount;
                        toFill.AddVert(new Vector3(rect.xMin + x + info.minX, baselineY + info.minY), color32, info.uvBottomLeft);
                        toFill.AddVert(new Vector3(rect.xMin + x + info.minX, baselineY + info.maxY), color32, info.uvTopLeft);
                        toFill.AddVert(new Vector3(rect.xMin + x + info.maxX, baselineY + info.maxY), color32, info.uvTopRight);
                        toFill.AddVert(new Vector3(rect.xMin + x + info.maxX, baselineY + info.minY), color32, info.uvBottomRight);
                        toFill.AddTriangle(v, v + 1, v + 2);
                        toFill.AddTriangle(v, v + 2, v + 3);
                    }
                    x += info.advance;
                }
                y += lineHeight + leading;
            }
        }
    }
}
