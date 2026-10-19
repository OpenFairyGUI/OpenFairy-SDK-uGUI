using System;
using System.Collections.Generic;
using System.Reflection;
using ZLinq;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Editor
{
    public static partial class Migrate
    {
        private static string UniqueName(HashSet<string> used, string proposed)
        {
            var name = proposed;
            for (var suffix = 2; !used.Add(name); suffix++) name = proposed + "_" + suffix;
            return name;
        }

        private sealed class GeneratedMembers
        {
            public readonly HashSet<string> used = new(SerializedBaseFields, StringComparer.Ordinal);
            public readonly Dictionary<Schema.Controller, string> controllerFields = new();
            public readonly Dictionary<Schema.Controller, string> controllerEnums = new();
            public readonly Dictionary<Schema.Transition, string> transitionFields = new();
            public readonly Dictionary<Schema.Transition, string> hookTypes = new();
            public readonly Dictionary<Schema.Transition, string> hookMethods = new();
            public readonly List<(Schema.Display Element, string Field)> children = new();
            public readonly string buttonEnum;

            public GeneratedMembers(Schema.Component xml, string className)
            {
                used.UnionWith(CodeTypeNames);
                used.Add(className);
                used.UnionWith(new[] { "GetControllerPage", "IsControllerChanging", "SetControllerPage", "InitializeControllers" });
                var baseType = xml.Extension switch
                {
                    Schema.ComponentExtension.Button => typeof(Button<>),
                    Schema.ComponentExtension.ComboBox => typeof(ComboBox<>),
                    Schema.ComponentExtension.ProgressBar => typeof(ProgressBar),
                    Schema.ComponentExtension.Slider => typeof(Slider),
                    Schema.ComponentExtension.Label => typeof(Label),
                    _ => typeof(Component),
                };
                for (var type = baseType; type != null; type = type.BaseType)
                    foreach (var member in type.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        if (member.MemberType != MemberTypes.Constructor) used.Add(member.Name);
                foreach (var controller in xml.Controllers)
                {
                    controllerFields.Add(controller, UniqueName(used, Field(controller.Name)));
                    var name = UniqueName(used, ControllerEnumName(controller.Name));
                    controllerEnums.Add(controller, name);
                    if (controller.Name == "button") buttonEnum = name;
                }
                if (buttonEnum == null && xml.Extension is Schema.ComponentExtension.Button or Schema.ComponentExtension.ComboBox)
                    buttonEnum = UniqueName(used, ControllerEnumName("button"));
                foreach (var transition in xml.Transitions)
                {
                    transitionFields.Add(transition, UniqueName(used, TransitionField(transition.Name)));
                    if (TransitionHooks(xml, transition).Count == 0) continue;
                    var prefix = Identifier(transition.Name);
                    hookTypes.Add(transition, UniqueName(used, prefix + "Hook"));
                    hookMethods.Add(transition, UniqueName(used, "Set" + prefix + "Hook"));
                }
                var authored = new HashSet<string>(StringComparer.Ordinal);
                foreach (var child in xml.DisplayList)
                {
                    if (CodeSettings().ignoreNoname && System.Text.RegularExpressions.Regex.IsMatch(child.Name ?? "", @"\An[0-9]+\z")) continue;
                    if (!authored.Add(child.Name ?? "")) continue; // Exact duplicate authored names keep their first public reference.
                    children.Add((child, UniqueName(used, Field(child.Name))));
                }
            }
        }

        private static List<(string Name, int Item, bool End)> TransitionHooks(Schema.Component xml, Schema.Transition transition)
        {
            var result = new List<(string, int, bool)>();
            var labels = new HashSet<string>(StringComparer.Ordinal);
            var used = new HashSet<string>(StringComparer.Ordinal) { "value__" };
            var index = 0;
            foreach (var item in TransitionItems(xml, transition))
            {
                if (!string.IsNullOrEmpty(item.Label) && labels.Add(item.Label)) result.Add((UniqueName(used, Identifier(item.Label)), index, false));
                if (item.Tween && !string.IsNullOrEmpty(item.EndLabel) && labels.Add(item.EndLabel)) result.Add((UniqueName(used, Identifier(item.EndLabel)), index, true));
                index++;
            }
            return result;
        }

        private static readonly Dictionary<Resource, string> ClassNames = new();
        private static readonly Dictionary<string, string> PackageNames = new(StringComparer.Ordinal);
        private static string _classPrefix;
        private static int _namedResources;

        private static void PrepareCodeNames(Resource resource)
        {
            var prefix = CodeSettings().classNamePrefix;
            if (_classPrefix == prefix && _namedResources == Resources.Count && ClassNames.ContainsKey(resource)) return;
            ClassNames.Clear(); PackageNames.Clear();
            _classPrefix = prefix; _namedResources = Resources.Count;
            var packages = new SortedDictionary<string, List<Resource>>(StringComparer.Ordinal);
            foreach (var item in Resources.Values)
            {
                if (!packages.TryGetValue(item.PackageId, out var group)) packages[item.PackageId] = group = new List<Resource>();
                group.Add(item);
            }
            if (!packages.TryGetValue(resource.PackageId, out var own)) packages[resource.PackageId] = own = new List<Resource>();
            if (!own.Contains(resource)) own.Add(resource);
            var namespaces = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in packages)
            {
                PackageNames.Add(group.Key, UniqueName(namespaces, Identifier(group.Value[0].Package)));
                var items = group.Value.AsValueEnumerable().Where(item => item.Type == Schema.ResourceKind.Component || item == resource).ToArray();
                Array.Sort(items, (a, b) => string.CompareOrdinal(a.Id, b.Id));
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                string Proposed(Resource item) => Identifier(prefix + System.IO.Path.GetFileNameWithoutExtension(item.File));
                foreach (var item in items) { var name = Proposed(item); counts.TryGetValue(name, out var count); counts[name] = count + 1; }
                var used = new HashSet<string>(CodeTypeNames, StringComparer.Ordinal);
                foreach (var item in items)
                {
                    var name = Proposed(item);
                    if (counts[name] > 1) name += "_" + Identifier(item.Id);
                    ClassNames.Add(item, UniqueName(used, name));
                }
            }
        }

        private static string PackageName(Resource resource)
        {
            PrepareCodeNames(resource);
            return PackageNames[resource.PackageId];
        }
    }
}
