using System;
using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    [Serializable]
    public class GearLook<T> : Gear<T> where T : struct, Enum
    {
        public float[] alphas;
        public float defaultAlpha;
        public float[] rotations;
        public float defaultRotation;
        public bool[] grayed;
        public bool defaultGrayed;

        public override void Apply(T page)
        {
            var index = Array.IndexOf(pages, page);
            var alpha = index >= 0 ? alphas[index] : defaultAlpha;
            foreach (var graphic in target.GetComponentsInChildren<Graphic>(true))
            {
                var color = graphic.color;
                color.a = alpha;
                graphic.color = color;
            }

            ((RectTransform)target.transform).localEulerAngles = new Vector3(0, 0, -(index >= 0 ? rotations[index] : defaultRotation));
            var isGrayed = index >= 0 ? grayed[index] : defaultGrayed;
            var effect = target.GetComponent<Grayed>();
            if (isGrayed && effect == null)
                target.AddComponent<Grayed>();
            else if (!isGrayed && effect != null)
                UnityEngine.Object.DestroyImmediate(effect);
        }
    }
}
