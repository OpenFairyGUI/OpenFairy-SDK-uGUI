using System;
using UnityEngine.UI;

namespace NanamiUI
{
    [Serializable]
    public class GearText<T> : Gear<T> where T : struct, Enum
    {
        public string[] values;
        public string defaultValue;

        public override void Apply(T page)
        {
            var index = Array.IndexOf(pages, page);
            target.GetComponentInChildren<Text>(true).text = index >= 0 ? values[index] : defaultValue;
        }
    }
}
