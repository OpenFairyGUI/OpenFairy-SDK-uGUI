using System;

namespace NanamiUI
{
    public class GearDisplay : Gear
    {
        public override void Apply(int page) => gameObject.SetActive(pages.Length == 0 || Array.IndexOf(pages, page) >= 0);
    }
}
