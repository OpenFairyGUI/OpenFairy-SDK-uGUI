using System.Collections.Generic;
using UnityEngine;

namespace OpenFairy.UGUI
{
    // GList.UpdateBounds uses different rounding for each layout, including its final partial line.
    internal static class ListLayout
    {
        internal static Vector2 Arrange(ListSource source, IReadOnlyList<RectTransform> items, Vector2 view,
            IReadOnlyList<Vector2> sourceSizes)
        {
            float x = 0, y = 0, maxWidth = 0, maxHeight = 0;
            int line = 0, page = 0, row = 0;
            var vertical = source.layout == ListLayoutType.FlowVertical;
            var pagination = source.layout == ListLayoutType.Pagination;
            var eachHeight = pagination && source.autoItemSize && source.lineItemCount2 > 0
                ? Mathf.Floor((view.y - (source.lineItemCount2 - 1) * source.lineGap) / source.lineItemCount2) : 0;
            Vector2 extent;
            bool Include(int i) => !source.foldInvisibleItems || items[i].gameObject.activeSelf;
            float Weight(int i) => (int)(sourceSizes != null ? sourceSizes[i] : source.itemSize)[vertical ? 1 : 0];
            void Size(RectTransform item, float width, float height)
            {
                item.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0, width));
                item.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0, height));
            }
            void Position(RectTransform item, float left, float top)
            {
                var parent = (RectTransform)item.parent;
                item.localPosition = new Vector3(parent.rect.xMin + left + item.pivot.x * item.rect.width,
                    parent.rect.yMax - top - (1 - item.pivot.y) * item.rect.height, item.localPosition.z);
            }

            if (source.layout is ListLayoutType.SingleColumn or ListLayoutType.SingleRow)
            {
                var horizontal = source.layout == ListLayoutType.SingleRow;
                for (var i = 0; i < items.Count; i++)
                {
                    if (!Include(i)) continue;
                    var item = items[i];
                    if (source.autoItemSize) Size(item, horizontal ? item.rect.width : view.x,
                        horizontal ? view.y : item.rect.height);
                    if (horizontal)
                    {
                        if (x != 0) x += source.colGap;
                        Position(item, x, 0);
                        x += Mathf.Ceil(item.rect.width);
                        maxHeight = Mathf.Max(maxHeight, item.rect.height);
                    }
                    else
                    {
                        if (y != 0) y += source.lineGap;
                        Position(item, 0, y);
                        y += Mathf.Ceil(item.rect.height);
                        maxWidth = Mathf.Max(maxWidth, item.rect.width);
                    }
                }
                extent = horizontal ? new Vector2(x, Mathf.Ceil(maxHeight)) : new Vector2(Mathf.Ceil(maxWidth), y);
            }
            else if (source.autoItemSize && source.lineItemCount > 0)
            {
                float total = 0;
                int start = 0;
                for (var i = 0; i < items.Count; i++)
                {
                    if (!Include(i)) continue;
                    total += Weight(i);
                    line++;
                    if (line != source.lineItemCount && i != items.Count - 1) continue;
                    if (pagination && (source.lineItemCount2 > 0 && row >= source.lineItemCount2
                        || source.lineItemCount2 == 0 && y + items[i].rect.height > view.y))
                    {
                        page++;
                        y = 0;
                        row = 0;
                    }
                    var remaining = (vertical ? view.y : view.x)
                        - (line - 1) * (vertical ? source.lineGap : source.colGap);
                    float percent = 1;
                    if (vertical) y = 0; else x = 0;
                    for (var j = start; j <= i; j++)
                    {
                        if (!Include(j)) continue;
                        var item = items[j];
                        var fraction = Weight(j) / total;
                        var size = Mathf.Round(fraction / percent * remaining);
                        Size(item, vertical ? item.rect.width : size,
                            vertical ? size : pagination && source.lineItemCount2 > 0 ? eachHeight : item.rect.height);
                        Position(item, page * view.x + x, y);
                        remaining -= vertical ? item.rect.height : item.rect.width;
                        percent -= fraction;
                        if (vertical)
                        {
                            y += item.rect.height + source.lineGap;
                            maxWidth = Mathf.Max(maxWidth, item.rect.width);
                        }
                        else
                        {
                            x += item.rect.width + source.colGap;
                            maxHeight = Mathf.Max(maxHeight, item.rect.height);
                        }
                    }
                    if (vertical) { x += Mathf.Ceil(maxWidth) + source.colGap; maxWidth = 0; }
                    else { y += Mathf.Ceil(maxHeight) + source.lineGap; maxHeight = 0; }
                    line = 0;
                    start = i + 1;
                    total = 0;
                    row++;
                }
                extent = vertical ? new Vector2(x + Mathf.Ceil(maxWidth), view.y)
                    : new Vector2(pagination ? (page + 1) * view.x : view.x,
                        pagination && page > 0 ? view.y : y + Mathf.Ceil(maxHeight));
            }
            else
            {
                for (var i = 0; i < items.Count; i++)
                {
                    if (!Include(i)) continue;
                    var item = items[i];
                    if (pagination && source.autoItemSize && source.lineItemCount2 > 0)
                        Size(item, item.rect.width, eachHeight);
                    if (vertical)
                    {
                        if (y != 0) y += source.lineGap;
                        if (source.lineItemCount > 0 && line >= source.lineItemCount
                            || source.lineItemCount == 0 && y + item.rect.height > view.y && maxWidth != 0)
                        {
                            y = 0; x += Mathf.Ceil(maxWidth) + source.colGap; maxWidth = 0; line = 0;
                        }
                        Position(item, x, y);
                        y += item.rect.height;
                        maxHeight = Mathf.Max(maxHeight, y);
                        maxWidth = Mathf.Max(maxWidth, item.rect.width);
                    }
                    else
                    {
                        if (x != 0) x += source.colGap;
                        if (source.lineItemCount > 0 && line >= source.lineItemCount
                            || source.lineItemCount == 0 && x + item.rect.width > view.x && maxHeight != 0)
                        {
                            x = 0; y += (pagination ? maxHeight : Mathf.Ceil(maxHeight)) + source.lineGap;
                            maxHeight = 0; line = 0; row++;
                            if (pagination && (source.lineItemCount2 > 0 && row >= source.lineItemCount2
                                || source.lineItemCount2 == 0 && y + item.rect.height > view.y && maxWidth != 0))
                            {
                                page++; y = 0; row = 0;
                            }
                        }
                        Position(item, page * view.x + x, y);
                        x += Mathf.Ceil(item.rect.width);
                        maxWidth = Mathf.Max(maxWidth, x);
                        maxHeight = Mathf.Max(maxHeight, item.rect.height);
                    }
                    line++;
                }
                extent = vertical ? new Vector2(x + Mathf.Ceil(maxWidth), Mathf.Ceil(maxHeight))
                    : new Vector2(pagination ? (page + 1) * view.x : Mathf.Ceil(maxWidth),
                        pagination && page > 0 ? view.y : y + Mathf.Ceil(maxHeight));
            }
            var gap = Vector2.Max(Vector2.zero, view - extent);
            var alignX = (int)source.alignment % 3;
            var alignY = (int)source.alignment / 3;
            var offset = new Vector3(alignX == 1 ? (int)(gap.x / 2) : alignX == 2 ? gap.x : 0,
                -(alignY == 1 ? (int)(gap.y / 2) : alignY == 2 ? gap.y : 0));
            for (var i = 0; i < items.Count; i++) if (Include(i)) items[i].localPosition += offset;
            return extent;
        }
    }
}
