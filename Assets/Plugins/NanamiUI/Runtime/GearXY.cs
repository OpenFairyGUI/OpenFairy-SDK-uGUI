using System;
using UnityEngine;

namespace NanamiUI
{
    [Serializable]
    public class GearXY<T> : Gear<T> where T : struct, Enum
    {
        public Vector2[] values;
        public Vector2 defaultValue;

        public override void Apply(T page)
        {
            var index = Array.IndexOf(pages, page);
            ((RectTransform)target.transform).anchoredPosition = index >= 0 ? values[index] : defaultValue;
        }
    }
}
