using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFairy.UGUI.Editor
{
    public static class TextTemplateCompiler
    {
        // Call for the base text, gear pages and translations using the same slot list.
        public static TextTemplate Compile(string source, List<string> variables)
        {
            var literals = new List<string>();
            var slots = new List<int>();
            var defaults = new List<string>();
            var literal = new StringBuilder();
            var position = 0;
            int open;
            while ((open = source.IndexOf('{', position)) >= 0)
            {
                if (open > 0 && source[open - 1] == '\\')
                {
                    literal.Append(source, position, open - position - 1).Append('{');
                    position = open + 1;
                    continue;
                }
                literal.Append(source, position, open - position);
                position = open;
                var close = source.IndexOf('}', open);
                if (close < 0)
                    break;
                if (close == open + 1)
                {
                    literal.Append("{}");
                    position = close + 1;
                    continue;
                }

                var tag = source.Substring(open + 1, close - open - 1);
                var equals = tag.IndexOf('=');
                var name = equals >= 0 ? tag.Substring(0, equals) : tag;
                var slot = variables.IndexOf(name);
                if (slot < 0)
                {
                    slot = variables.Count;
                    variables.Add(name);
                }
                literals.Add(literal.ToString());
                literal.Clear();
                slots.Add(slot);
                defaults.Add(equals >= 0 ? tag.Substring(equals + 1) : "");
                position = close + 1;
            }
            literal.Append(source, position, source.Length - position);
            literals.Add(literal.ToString());
            return new TextTemplate { literals = literals.ToArray(), slots = slots.ToArray(), defaults = defaults.ToArray() };
        }
    }
}
