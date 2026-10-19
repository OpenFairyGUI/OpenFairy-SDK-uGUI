using System;

namespace OpenFairy.UGUI
{
    [Serializable]
    public sealed class GearScrollPage<T> : Gear<T> where T : struct, Enum
    {
        public ScrollPane pane;
        public override void Apply(T page) => Apply(page, false);
        public override void Apply(T page, bool animate)
        {
            if (!pane.updatingPage) pane.SetPage(Array.IndexOf(pages, page), animate);
        }
    }
}
