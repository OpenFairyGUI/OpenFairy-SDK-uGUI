using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFairy.UGUI.Editor
{
    public readonly struct StaticTextChoice
    {
        public readonly string member;
        public readonly string nativeTarget;
        public readonly bool setterCall;
        public readonly bool initialOnly;

        public StaticTextChoice(string member, string nativeTarget, bool setterCall = false, bool initialOnly = false)
        {
            this.member = member;
            this.nativeTarget = nativeTarget;
            this.setterCall = setterCall;
            this.initialOnly = initialOnly;
        }
    }

    // Emits members inside an existing generated view; targets are importer-owned C# expressions.
    public static class StaticTextCode
    {
        public static string Emit(string name, string[] variables, StaticTextChoice[] choices,
            string[] refreshStatements, Func<string, string> identifier)
        {
            var code = new StringBuilder();
            var hasInput = false;
            foreach (var choice in choices) hasInput |= choice.initialOnly;
            code.AppendLine("[global::System.Serializable]")
                .Append("public sealed class ").Append(name).AppendLine("Texts\n{");
            foreach (var choice in choices)
                code.Append("    public ").Append(variables.Length > 0 ? "global::OpenFairy.UGUI.TextTemplate " : "string ").Append(choice.member).AppendLine(";");
            code.AppendLine("}")
                .Append("[global::UnityEngine.SerializeField] private ").Append(name).Append("Texts _").Append(name).AppendLine("Texts = null;");
            if (variables.Length > 0)
            {
                code.Append("private readonly global::OpenFairy.UGUI.TextTemplateState _").Append(name).Append("Variables = new(").Append(variables.Length).AppendLine(");");
                code.Append("public enum ").Append(name).AppendLine("Variable\n{");
                var used = new HashSet<string>(StringComparer.Ordinal) { "value__" };
                foreach (var variable in variables)
                {
                    var member = identifier(variable);
                    var unique = member;
                    for (var suffix = 2; !used.Add(unique); suffix++)
                        unique = member + "_" + suffix;
                    code.Append("    ").Append(unique).AppendLine(",");
                }
                code.AppendLine("}")
                    .Append("public void Set").Append(name).Append("Variable(").Append(name).Append("Variable variable, string value) => _")
                    .Append(name).AppendLine("Variables.Set((int)variable, value);")
                    .Append("public void Clear").Append(name).Append("Variable(").Append(name).Append("Variable variable) => _")
                    .Append(name).AppendLine("Variables.Clear((int)variable);");
            }
            code.Append("public void Apply").Append(name).Append("Texts(").Append(name).Append("Texts texts")
                .AppendLine(hasInput ? ", bool updateInput = false)\n{" : ")\n{")
                .Append("    _").Append(name).AppendLine("Texts = texts;")
                .Append("    Refresh").Append(name).AppendLine(hasInput ? "Text(updateInput);\n}" : "Text();\n}")
                .Append("public void Refresh").Append(name).AppendLine(hasInput ? "Text(bool updateInput = true)\n{" : "Text()\n{");
            foreach (var choice in choices)
            {
                var value = new StringBuilder();
                if (variables.Length > 0)
                    value.Append('_').Append(name).Append("Variables.Format(");
                value.Append('_').Append(name).Append("Texts.").Append(choice.member);
                if (variables.Length > 0) value.Append(')');
                code.Append("    ");
                if (choice.initialOnly) code.Append("if (updateInput) ");
                code.AppendLine(choice.setterCall ? choice.nativeTarget.Replace("{value}", value.ToString())
                    : choice.nativeTarget + " = " + value + ";");
            }
            foreach (var statement in refreshStatements)
                code.Append("    ").AppendLine(statement);
            return code.AppendLine("}").ToString();
        }
    }
}
