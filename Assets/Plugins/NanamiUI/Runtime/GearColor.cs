using System;
using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    [Serializable]
    public class GearColor<T> : Gear<T> where T : struct, Enum
    {
        public Color[] values;
        public Color defaultValue;

        public override void Apply(T page)
        {
            var index = Array.IndexOf(pages, page);
            target.GetComponent<Graphic>().color = index >= 0 ? values[index] : defaultValue;
        }
    }
}
