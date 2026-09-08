using System;
using UnityEngine.UI;

#if FAIRYGUI_TMPRO
using TextComponent = TMPro.TextMeshProUGUI;
#else
using TextComponent = OpenFairy.UGUI.TextField;
#endif

namespace OpenFairy.UGUI
{
    [Serializable]
    public class GearText<T> : Gear<T> where T : struct, Enum
    {
        public string[] values;
        public string defaultValue;

        [NonSerialized] private TextComponent _textField;

        public override void Apply(T page)
        {
            var index = Array.IndexOf(pages, page);
            (_textField ??= target.GetComponentInChildren<TextComponent>(true)).text = index >= 0 ? values[index] : defaultValue;
        }
    }
}
