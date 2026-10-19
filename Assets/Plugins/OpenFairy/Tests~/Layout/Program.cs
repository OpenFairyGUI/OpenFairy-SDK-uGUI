using OpenFairy.UGUI;
using System.Text.Json;

long checks = 0;
void Check(bool value, string message)
{
    checks++;
    if (!value)
        throw new Exception(message);
}

var random = new Random(107181);
for (int sample = 0; sample < 8000; sample++)
{
    int count = random.Next(0, 1001), lanes = random.Next(1, 9);
    float height = random.Next(1, 1001), gap = random.Next(0, 41), viewport = random.Next(1, 1801);
    float extent = VirtualListLayout.FixedExtent(count, lanes, height, gap);
    float position = random.Next(-200, Math.Max(201, (int)extent + 200));
    var range = VirtualListLayout.FixedRange(count, lanes, height, gap, position, viewport, 0);
    Check(range.First >= 0 && range.End <= count && range.End >= range.First, "Fixed interval invalid");
    int realVisible = 0;
    for (int index = 0; index < count; index++)
    {
        float top = index / lanes * (height + gap);
        bool visible = top + height > position && top < position + viewport;
        bool included = index >= range.First && index < range.End;
        Check(visible == included, $"Fixed visibility mismatch at {sample}:{index}");
        if (visible) realVisible++;
    }
    var padded = VirtualListLayout.FixedRange(count, lanes, height, gap, position, viewport);
    Check(padded.First <= range.First && padded.End >= range.End, "Overscan excluded visible item");
    Check(padded.End - padded.First <= realVisible + 4 * lanes, "Overscan is not viewport-bounded");
}

for (int sample = 0; sample < 4000; sample++)
{
    int count = random.Next(0, 501);
    float gap = random.Next(0, 12), viewport = random.Next(1, 1201);
    var starts = new float[count + 1];
    for (int i = 0; i < count; i++)
        starts[i + 1] = starts[i] + random.Next(1, 401) + gap;
    float position = random.Next(-100, Math.Max(101, (int)starts[^1] + 100));
    var range = VirtualListLayout.VariableRange(starts, gap, position, viewport, 0);
    Check(range.First >= 0 && range.End <= count && range.End >= range.First, "Variable interval invalid");
    for (int i = 0; i < count; i++)
    {
        bool visible = starts[i + 1] - gap > position && starts[i] < position + viewport;
        bool included = i >= range.First && i < range.End;
        Check(visible == included, $"Variable visibility mismatch at {sample}:{i}");
    }
}

for (int sample = 0; sample < 10000; sample++)
{
    int count = random.Next(1, 80), cycle = random.Next(1, 8);
    float step = random.Next(1, 901), position = random.Next(-10000, 10001);
    var rebase = VirtualListLayout.LoopRebase(position, count, step, cycle);
    float current = position - rebase.PixelShift;
    float period = count * step;
    Check(current >= cycle * period && current < (cycle + 1) * period, "Loop did not enter center period");
    int slot = random.Next(-2000, 2001);
    int movedSlot = slot - rebase.SlotShift;
    Check(VirtualListLayout.LogicalIndex(slot, count) == VirtualListLayout.LogicalIndex(movedSlot, count), "Loop changed logical item");
    Check(slot * step - position == movedSlot * step - current, "Loop changed viewport coordinate");
    float dragOrigin = random.Next(-1000, 1001), pointerDelta = random.Next(-200, 201);
    Check((dragOrigin - rebase.PixelShift + pointerDelta) + rebase.PixelShift == dragOrigin + pointerDelta,
        "Loop lost drag anchor");
}

// Large data sets must remain viewport-bounded. Variable-size rows also
// exercise exact start/end boundaries; repeated slots retain logical identity.
var rows = VirtualListLayout.FixedRange(1000000, 1, 60, 0, 30000000, 540);
Check(rows.End - rows.First <= 11, "Large row list inflated active range");
var grid = VirtualListLayout.FixedRange(1000000, 7, 260, 10, 2700000, 910);
Check(grid.End - grid.First <= 7 * 7, "Grid inflated active range");
var variable = VirtualListLayout.VariableRange(new float[] { 0, 69, 218, 487, 556 }, 5, 69, 398, 0);
Check(variable.First == 1 && variable.End == 3, "Exact-boundary range wrong");
for (int slot = -100; slot <= 100; slot++)
    Check(VirtualListLayout.LogicalIndex(slot, 1) == 0, "Single-item loop changed item");

Console.WriteLine(JsonSerializer.Serialize(new { passed = true, checks, seed = 107181,
    fixedSamples = 8000, variableSamples = 4000, loopSamples = 10000,
    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription }));
