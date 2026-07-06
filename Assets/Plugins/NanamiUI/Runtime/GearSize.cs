using System;
using UnityEngine;

namespace NanamiUI
{
    [Serializable]
    public class GearSize<T> : Gear<T> where T : struct, Enum
    {
        public Vector2[] sizes;
        public Vector2 defaultSize;
        public Vector2[] scales;
        public Vector2 defaultScale;

        public override void Apply(T page)
        {
            var index = Array.IndexOf(pages, page);
            var rt = (RectTransform)target.transform;
            var scale = index >= 0 ? scales[index] : defaultScale;
            rt.sizeDelta = index >= 0 ? sizes[index] : defaultSize;
            rt.localScale = new Vector3(scale.x, scale.y, 1);
        }
    }
}
