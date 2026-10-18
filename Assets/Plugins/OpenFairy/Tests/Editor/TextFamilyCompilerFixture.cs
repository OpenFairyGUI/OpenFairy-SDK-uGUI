using System;
using System.Collections.Generic;

namespace OpenFairy.UGUI.Editor
{
    public readonly struct TextChoice
    {
        public readonly string suffix;
        public readonly string authored;
        public readonly bool variables;
        public readonly string component;

        public TextChoice(string suffix, string authored, bool variables, string component = null)
        {
            this.suffix = suffix;
            this.authored = authored;
            this.variables = variables;
            this.component = component;
        }
    }

    public sealed class CompiledTextFamily
    {
        public List<string> schema;
        public string[] variables => schema.ToArray();
        public TextTemplate[] authored;
        public TextTemplate[][] translations;
    }

    public static class TextFamilyCompiler
    {
        // A family's slots cover all choices and locales; language changes keep assignments.
        public static CompiledTextFamily Compile(string component, TextChoice[] choices, FairyTranslations[] translations, List<string> sharedSchema = null)
        {
            var variables = sharedSchema ?? new List<string>();
            TextTemplate Compile(TextChoice choice, string text) => choice.variables
                ? TextTemplateCompiler.Compile(text, variables)
                : new TextTemplate { literals = new[] { text }, slots = Array.Empty<int>(), defaults = Array.Empty<string>() };

            var authored = new TextTemplate[choices.Length];
            for (var i = 0; i < choices.Length; i++)
                authored[i] = Compile(choices[i], choices[i].authored);
            var localized = new TextTemplate[translations.Length][];
            for (var language = 0; language < translations.Length; language++)
            {
                var row = localized[language] = new TextTemplate[choices.Length];
                for (var i = 0; i < choices.Length; i++)
                    row[i] = Compile(choices[i], translations[language].Resolve(choices[i].component ?? component, choices[i].suffix, choices[i].authored));
            }
            return new CompiledTextFamily { schema = variables, authored = authored, translations = localized };
        }
    }
}
