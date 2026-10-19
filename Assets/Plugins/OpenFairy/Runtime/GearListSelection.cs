using System;

namespace OpenFairy.UGUI
{
    [Serializable]
    public sealed class GearListSelection<T> : Gear<T> where T : struct, Enum
    {
        public ListSelection selection;
        public override void Apply(T page) => selection.SelectFromController(Array.IndexOf(pages, page));
    }
}
