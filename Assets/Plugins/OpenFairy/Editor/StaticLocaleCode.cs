using System.Text;

namespace OpenFairy.UGUI.Editor
{
    public readonly struct StaticLocaleField
    {
        public readonly string type;
        public readonly string member;

        public StaticLocaleField(string type, string member)
        {
            this.type = type;
            this.member = member;
        }
    }

    public static class StaticLocaleCode
    {
        public static string Table(string namespaceName, string className, StaticLocaleField[] fields)
        {
            var code = new StringBuilder();
            code.Append("namespace ").Append(namespaceName).AppendLine("\n{")
                .Append("public sealed class ").Append(className).AppendLine(" : UnityEngine.ScriptableObject\n{");
            foreach (var field in fields)
                code.Append("    public ").Append(field.type).Append(' ').Append(field.member).AppendLine(";");
            return code.AppendLine("}\n}").ToString();
        }

        public static string Languages(string namespaceName, string className, string tableType, string[] languages)
        {
            var code = new StringBuilder();
            code.Append("namespace ").Append(namespaceName).AppendLine("\n{")
                .Append("public sealed class ").Append(className).AppendLine(" : UnityEngine.ScriptableObject\n{")
                .AppendLine("    public enum Language\n    {");
            foreach (var language in languages)
                code.Append("        ").Append(language).AppendLine(",");
            return code.AppendLine("    }")
                .Append("    [UnityEngine.SerializeField] private ").Append(tableType).Append("[] tables = new ").Append(tableType).Append('[').Append(languages.Length).AppendLine("];")
                .Append("    public ").Append(tableType).AppendLine(" Get(Language language) => tables[(int)language];")
                .AppendLine("}\n}").ToString();
        }
    }
}
