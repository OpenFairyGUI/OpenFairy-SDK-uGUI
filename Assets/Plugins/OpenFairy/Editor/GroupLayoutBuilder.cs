using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Editor
{
    public static class GroupLayoutBuilder
    {
        public static void Configure(GameObject group, Schema.Display source)
        {
            if (!source.Advanced || !source.LayoutSpecified) return;
            var layout = group.AddComponent<GroupLayout>();
            layout.axis = source.Layout == Schema.ListLayout.SingleRow ? GroupLayoutAxis.Horizontal : GroupLayoutAxis.Vertical;
            layout.spacing = layout.axis == GroupLayoutAxis.Horizontal ? source.ColGap : source.LineGap;
            layout.excludeInvisibles = source.ExcludeInvisibles;
            var fitter = group.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }
}
