using System;
using UnityEngine;

namespace NanamiUI
{
    [Serializable]
    public class GearIcon<T> : Gear<T> where T : struct, Enum
    {
        public Sprite[] values;
        public Sprite defaultValue;

        public override void Apply(T page)
        {
            var index = Array.IndexOf(pages, page);
            var sprite = index >= 0 ? values[index] : defaultValue;
            var loader = target.GetComponentInChildren<Loader>(true);
            loader.sprite = sprite;
            loader.enabled = sprite != null;
        }
    }
}
