using System;
using UnityEngine;

namespace NanamiUI
{
    public abstract class Gear : MonoBehaviour
    {
        public int[] pages;

        public abstract void Apply(int page);
    }

    public class GearDisplay : Gear
    {
        public override void Apply(int page) => gameObject.SetActive(pages.Length == 0 || Array.IndexOf(pages, page) >= 0);
    }

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
