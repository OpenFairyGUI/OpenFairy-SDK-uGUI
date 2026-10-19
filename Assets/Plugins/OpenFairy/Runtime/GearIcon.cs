using System;
using UnityEngine;

namespace OpenFairy.UGUI
{
    [Serializable]
    public class GearIcon<T> : Gear<T> where T : struct, Enum
    {
        public Sprite[] values;
        public Sprite defaultValue;
        [SerializeField] internal ButtonBase button;
        [SerializeField] internal Label label;
        [SerializeField] internal Loader loader;

        public override void Apply(T page)
        {
            var index = Array.IndexOf(pages, page);
            var sprite = index >= 0 ? values[index] : defaultValue;
            if (button != null) button.icon = sprite;
            else if (label != null) label.icon = sprite;
            else if (loader != null)
            {
                loader.sprite = sprite;
                loader.enabled = sprite != null;
            }
        }
    }
}
