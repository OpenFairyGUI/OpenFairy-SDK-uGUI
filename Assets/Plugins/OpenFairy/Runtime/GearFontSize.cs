using System;

#if FAIRYGUI_TMPRO
using TextComponent = TMPro.TextMeshProUGUI;
#else
using TextComponent = OpenFairy.UGUI.TextField;
#endif

namespace OpenFairy.UGUI
{
    [Serializable]
    public class GearFontSize<T> : Gear<T> where T : struct, Enum
    {
        public int[] values;
        public int defaultValue;

        [NonSerialized] private TextComponent _textField;

        public override void Apply(T page)
        {
            var index = Array.IndexOf(pages, page);
            _textField ??= target.GetComponentInChildren<TextComponent>(true);
            int value = index >= 0 ? values[index] : defaultValue;
#if FAIRYGUI_TMPRO
            TextAutoSize.SetFontSize(_textField, value);
#else
            _textField.fontSize = value;
#endif
        }
    }
}
