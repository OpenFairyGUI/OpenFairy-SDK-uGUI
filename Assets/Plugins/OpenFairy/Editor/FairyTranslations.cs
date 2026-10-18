using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace OpenFairy.UGUI.Editor
{
    // Editor-owned input. Resource keys are removed from the runtime binding surface.
    public sealed class FairyTranslations
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public FairyTranslations(string source)
        {
            using var input = new StringReader(source);
            using var reader = new XmlTextReader(input) { Normalization = false };
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            foreach (var text in document.Root.Elements("string"))
            {
                var name = (string)text.Attribute("name");
                if (name.IndexOf('-') >= 0)
                    _values[name] = text.Value;
            }
        }

        public string Resolve(string component, string suffix, string authored) =>
            _values.TryGetValue(component + "-" + suffix, out var text) ? text : authored;
    }

}
