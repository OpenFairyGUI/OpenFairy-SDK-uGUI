using System;

namespace NanamiUI
{
    [Serializable]
    public class GearDisplay<T> : Gear<T> where T : struct, Enum
    {
        public override void Apply(T page) => target.SetActive(pages.Length == 0 || Array.IndexOf(pages, page) >= 0);
    }
}
