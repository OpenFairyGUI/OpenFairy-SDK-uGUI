using System;

namespace OpenFairy.UGUI
{
    // Pure index/geometry calculation for ListSource. This does not own scrolling,
    // GameObjects, pooling, selection, or presentation.
    internal static class VirtualListLayout
    {
        // [First, End). ItemCount is logical; a grid line contains lanes items.
        public static (int First, int End) FixedRange(int itemCount, int lanes,
            float itemExtent, float gap, float position, float viewport, int overscan = 1)
        {
            if (itemCount == 0)
                return (0, 0);
            float step = itemExtent + gap;
            int lineCount = (itemCount - 1) / lanes + 1;
            int firstLine = Math.Max(0, (int)Math.Floor((position - itemExtent) / step) + 1 - overscan);
            int endLine = Math.Min(lineCount, (int)Math.Ceiling((position + viewport) / step) + overscan);
            firstLine = Math.Min(lineCount, firstLine);
            endLine = Math.Max(firstLine, endLine);
            return (Math.Min(itemCount, firstLine * lanes), Math.Min(itemCount, endLine * lanes));
        }

        public static float FixedExtent(int itemCount, int lanes, float itemExtent, float gap)
        {
            if (itemCount == 0)
                return 0;
            int lines = (itemCount - 1) / lanes + 1;
            return lines * itemExtent + (lines - 1) * gap;
        }

        // starts[i] is the top of row i; starts[count] includes the final gap.
        // Sizes are measured with the same actual text/layout used to draw rows.
        public static (int First, int End) VariableRange(float[] starts, float gap,
            float position, float viewport, int overscan = 1)
        {
            int count = starts.Length - 1;
            if (count == 0)
                return (0, 0);
            int first = UpperBound(starts, position + gap) - 1;
            int end = LowerBound(starts, position + viewport);
            first = Math.Clamp(first - overscan, 0, count);
            end = Math.Clamp(end + overscan, first, count);
            return (first, end);
        }

        public static int LogicalIndex(int physicalIndex, int count) =>
            ((physicalIndex % count) + count) % count;

        // Rebase an unbounded physical slot stream around one stable period.
        // Shift BOTH content position and drag origin by -PixelShift; relocate
        // physical slots by -SlotShift. Viewport positions then stay identical.
        public static (float PixelShift, int SlotShift) LoopRebase(float position,
            int count, float step, int anchorCycle = 1)
        {
            float period = count * step;
            int cycles = (int)Math.Floor(position / period) - anchorCycle;
            return (cycles * period, cycles * count);
        }

        private static int LowerBound(float[] values, float value)
        {
            int lo = 0, hi = values.Length;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (values[mid] < value)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        private static int UpperBound(float[] values, float value)
        {
            int lo = 0, hi = values.Length;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (values[mid] <= value)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }
    }
}
