using System;

namespace NanamiUI
{
    [Serializable]
    public class GearFontSize<T> : Gear<T> where T : struct, Enum
    {
        public int[] values;
        public int defaultValue;

        public override void Apply(T page)
        {
            var index = Array.IndexOf(pages, page);
            target.GetComponent<TextField>().fontSize = index >= 0 ? values[index] : defaultValue;
        }
    }
}
