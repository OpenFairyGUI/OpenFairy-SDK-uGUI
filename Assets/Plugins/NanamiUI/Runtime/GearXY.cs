using System;
using UnityEngine;

namespace NanamiUI
{
    public class GearXY : Gear
    {
        public Vector2[] values;
        public Vector2 defaultValue;

        public override void Apply(int page)
        {
            var index = Array.IndexOf(pages, page);
            ((RectTransform)transform).anchoredPosition = index >= 0 ? values[index] : defaultValue;
        }
    }
}
