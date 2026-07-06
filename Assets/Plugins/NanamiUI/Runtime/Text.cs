using System.Collections.Generic;
using UnityEngine;

namespace NanamiUI
{
    public class Text : UnityEngine.UI.Text
    {
        public static string defaultFont = "Arial";

        public string fontNames;

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
    }
}
