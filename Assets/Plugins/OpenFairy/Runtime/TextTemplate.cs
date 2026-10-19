using System;
using System.Text;

namespace OpenFairy.UGUI
{
    [Serializable]
    public sealed class TextTemplate
    {
        public string[] literals;
        public int[] slots;
        public string[] defaults;

        internal string Format(TextTemplateState.Value[] values, StringBuilder buffer)
        {
            if (slots.Length == 0)
                return literals[0];
            buffer.Clear();
            for (var i = 0; i < slots.Length; i++)
            {
                buffer.Append(literals[i]);
                var value = values[slots[i]];
                buffer.Append(value.assigned ? value.text : defaults[i]);
            }
            return buffer.Append(literals[slots.Length]).ToString();
        }
    }

    // One instance belongs to one authored text, shared by its base and gear pages.
    public sealed class TextTemplateState
    {
        internal struct Value
        {
            public bool assigned;
            public string text;
        }

        private readonly Value[] _values;
        private readonly StringBuilder _buffer = new();

        public TextTemplateState(int variableCount) => _values = new Value[variableCount];

        public void Set(int slot, string value) => _values[slot] = new Value { assigned = true, text = value };
        public void Clear(int slot) => _values[slot] = default;
        public string Format(TextTemplate template) => template.Format(_values, _buffer);
    }
}
