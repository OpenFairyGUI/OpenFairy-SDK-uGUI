using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Editor
{
    public static class ListLayoutBuilder
    {
        // A nonvirtual row follows its children's actual widths. Virtual rows keep fixed extents.
        public static void Configure(ListSource source)
        {
            if (source.layout != ListLayoutType.SingleRow) return;
            var content = source.content;
            if (!content.TryGetComponent<HorizontalLayoutGroup>(out var layout)) layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            if (!content.TryGetComponent<ContentSizeFitter>(out var size)) size = content.gameObject.AddComponent<ContentSizeFitter>();
            if (!content.TryGetComponent<LayoutElement>(out var minimum)) minimum = content.gameObject.AddComponent<LayoutElement>();
            source.rowLayout = layout; source.rowSize = size; source.rowMinimum = minimum;
            layout.spacing = source.colGap;
            layout.childAlignment = source.alignment;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var viewport = source.scrollPane != null ? source.scrollPane.viewportSize : ((RectTransform)source.transform).rect.size;
            minimum.minWidth = viewport.x; minimum.minHeight = viewport.y;
            if (content != source.transform)
            {
                size.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                size.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else size.horizontalFit = size.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            if (source.autoItemSize)
                foreach (RectTransform child in content)
                    child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewport.y);
            EditorUtility.SetDirty(source);
        }
    }
}
