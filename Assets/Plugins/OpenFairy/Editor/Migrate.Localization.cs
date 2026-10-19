using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Editor
{
    public static partial class Migrate
    {
        [Serializable]
        private sealed class LocalizationSettings
        {
            public LanguageSource[] languages = Array.Empty<LanguageSource>();
        }

        [Serializable]
        private sealed class LanguageSource
        {
            public string name = "";
            public string stringsFile = "";
        }

        private sealed class LocalChoice
        {
            public string member, component, suffix, text;
            public bool variables;
            public LocalChoice fallback;
            public StaticTextChoice target;
        }

        private sealed class LocalGear
        {
            public string controller, field;
        }

        private sealed class LocalFamily
        {
            public LocalView owner;
            public string name, element;
            public bool input, caption, combo;
            public bool privateText;
            public string textField, promptField, tooltipField;
            public Object bodySprites = null, promptSprites = null;
            public readonly List<string> variables = new();
            public readonly List<LocalGear> gears = new();
            public readonly List<string> refresh = new();
            public LocalRow source;
        }

        private sealed class LocalNode
        {
            public string element, field;
            public int item = -1;
            public bool privateField;
            public LocalView target;
        }

        private sealed class LocalRow
        {
            public LocalFamily family;
            public string member;
            public string targetField;
            public LocalNode[] path;
            public LocalChoice[] choices;
            public TextTemplate[] authored;
            public TextTemplate[][] localized;
            public string[][] prepared;
            public int? initialIndex;
        }

        private sealed class LocalView
        {
            public Resource resource;
            public XElement xml;
            public string tableField;
            public readonly List<LocalFamily> families = new();
            public readonly List<LocalNode> nodes = new();
            public readonly Dictionary<string, string> lists = new();
            public readonly List<LocalRow> overrides = new();
            public readonly HashSet<string> names = new(StringComparer.Ordinal);
            public readonly Dictionary<string, string> published = new();
        }

        private static readonly Dictionary<Resource, LocalView> LocalViews = new();
        private static LocalizationSettings _localization;
        private static FairyTranslations[] _translations;
        private static string LocaleNamespace => string.IsNullOrEmpty(CodeSettings().packageName)
            ? "OpenFairyGenerated.Localization" : string.Join(".", Array.ConvertAll(CodeSettings().packageName.Split('.'), Identifier)) + ".Localization";
        private static string LocaleType => "global::" + LocaleNamespace + ".LocaleTable";
        private static string ViewType(LocalView view) => "global::" + GeneratedNamespace(view.resource) + "." + ClassName(view.resource);
        private const BindingFlags LocalFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static string LocalName(HashSet<string> names, string proposed)
        {
            var name = Identifier(proposed);
            var unique = name;
            for (var suffix = 2; !names.Add(unique); suffix++) unique = name + "_" + suffix;
            return unique;
        }

        private static void PrepareLocalization(List<Resource> components)
        {
            var path = $"{UiRoot}/settings/OpenFairyLocalization.json";
            _localization = File.Exists(path)
                ? JsonUtility.FromJson<LocalizationSettings>(File.ReadAllText(path)) : new LocalizationSettings();
            _translations = new FairyTranslations[_localization.languages.Length];
            for (var i = 0; i < _translations.Length; i++)
                _translations[i] = new FairyTranslations(File.ReadAllText(_localization.languages[i].stringsFile));
            BuildLocalizationPlans(components);
        }

        private static void BuildLocalizationPlans(List<Resource> components)
        {
            LocalViews.Clear();
            var tableNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var resource in components)
            {
                var view = new LocalView { resource = resource, xml = FairyXml.LoadPublishedComponentXml(resource.File) };
                view.tableField = LocalName(tableNames, resource.Package + "_" + ClassName(resource));
                view.names.Add(ClassName(resource));
                view.names.Add("LocalizedTexts");
                view.names.Add("ApplyLocale");
                foreach (var controller in resource.ComponentXml.Controllers)
                {
                    view.names.Add(Field(controller.Name));
                    view.names.Add(ControllerEnumName(controller.Name));
                }
                foreach (var (element, field) in PublishedChildFields(resource.ComponentXml))
                {
                    view.names.Add(field);
                    if (FieldType(element, resource.PackageId) != null)
                        view.published.Add(element.Id, field);
                }
                foreach (var transition in resource.ComponentXml.Transitions) view.names.Add(TransitionField(transition.Name));
                LocalViews.Add(resource, view);
            }
            foreach (var view in LocalViews.Values) CollectLocalNodes(view);
            foreach (var view in LocalViews.Values) CollectLocalFamilies(view);
            foreach (var view in LocalViews.Values) CollectLocalOverrides(view);
            PrepareLocalizedImages();
            // Instance rows share the destination's slots, including variables introduced by a translation.
            foreach (var view in LocalViews.Values)
                foreach (var family in view.families) CompileLocalRow(family.source);
            foreach (var view in LocalViews.Values)
                foreach (var row in view.overrides) CompileLocalRow(row);
        }

        private static void CollectLocalNodes(LocalView view)
        {
            foreach (var element in view.xml.Element("displayList")?.Elements() ?? Array.Empty<XElement>())
            {
                var id = (string)element.Attribute("id");
                if (element.Name.LocalName == "component")
                {
                    var source = ResolveDisplayResource(element, view.resource);
                    var exposed = view.published.TryGetValue(id, out var field);
                    view.nodes.Add(new LocalNode { element = id, target = LocalViews[source], privateField = !exposed,
                        field = exposed ? field : LocalName(view.names, "_localizedChild" + view.nodes.Count) });
                }
                else if (element.Name.LocalName is "list" or "tree")
                {
                    view.lists.Add(id, LocalName(view.names, "_localizedList_" + id));
                    var index = 0;
                    foreach (var item in element.Elements("item"))
                    {
                        var url = (string)item.Attribute("url") ?? (string)element.Attribute("defaultItem");
                        view.nodes.Add(new LocalNode { element = id, item = index++, target = LocalViews[Resolve(url, view.resource.PackageId)],
                            field = LocalName(view.names, "_localizedItem" + view.nodes.Count), privateField = true });
                    }
                }
            }
        }

        private static Resource ResolveDisplayResource(XElement element, Resource owner) =>
            Resolve("ui://" + ((string)element.Attribute("pkg") ?? owner.PackageId) + (string)element.Attribute("src"), owner.PackageId);

        private static LocalChoice Choice(LocalView view, string member, string suffix, string text, bool variables = false,
            string target = null, bool setterCall = false, bool initialOnly = false) => new()
        {
            member = member, component = view.resource.PackageId + view.resource.Id, suffix = suffix, text = text ?? "",
            variables = variables, target = new StaticTextChoice(member, target, setterCall, initialOnly),
        };

        private static LocalFamily AddLocalFamily(LocalView view, string element, string name)
        {
            var family = new LocalFamily { owner = view, element = element,
                name = LocalName(view.names, "Localized" + Identifier(name)) };
            family.source = new LocalRow { family = family, member = family.name };
            view.families.Add(family);
            return family;
        }

        private static void CollectLocalFamilies(LocalView view)
        {
            foreach (var element in view.xml.Element("displayList")?.Elements() ?? Array.Empty<XElement>())
            {
                var id = (string)element.Attribute("id");
                var name = (string)element.Attribute("name");
                var kind = element.Name.LocalName;
                var text = kind is "text" or "richtext" or "inputtext";
                var input = (string)element.Attribute("input") == "true" || kind == "inputtext";
                var namedCaption = text && name == "title" && view.resource.ComponentXml.Extension is Schema.ComponentExtension.Button or Schema.ComponentExtension.ComboBox or Schema.ComponentExtension.Label;
                var caption = namedCaption && id == Array.Find(view.resource.ComponentXml.DisplayList, child => child.Name == "title")?.Id;
                if (!text && element.Element("gearText") == null && element.Attribute("tooltips") == null) continue;
                var family = AddLocalFamily(view, id, namedCaption ? "Caption" : name);
                family.input = input;
                family.caption = caption;
                var choices = new List<LocalChoice>();
                var variables = (string)element.Attribute("vars") == "true";
                if (text)
                {
                    string target;
                    var setterCall = false;
                    if (input)
                    {
                        family.privateText = !view.published.TryGetValue(id, out family.textField);
                        if (family.privateText) family.textField = LocalName(view.names, "_localizedInput" + view.families.Count);
                        target = "Set" + family.name + "Input({value});";
                        setterCall = true;
                    }
                    else if (caption) target = "title";
                    else
                    {
                        family.privateText = !view.published.TryGetValue(id, out family.textField);
                        if (family.privateText) family.textField = LocalName(view.names, "_localizedText" + view.families.Count);
                        target = family.textField + ".text";
                    }
                    choices.Add(Choice(view, "Text", id, FairyXml.PublishedText(element), variables, target, setterCall, input));
                    if (caption && view.resource.ComponentXml.Extension is Schema.ComponentExtension.Button or Schema.ComponentExtension.ComboBox)
                    {
                        choices.Add(Choice(view, "Selected", null, view.resource.ComponentXml.Button?.SelectedTitle, variables, "selectedTitle"));
                        family.refresh.Add("RefreshTitle();");
                    }
                    if (input)
                    {
                        family.promptField = LocalName(view.names, "_localizedPrompt" + view.families.Count);
                        choices.Add(Choice(view, "Prompt", id + "-prompt", (string)element.Attribute("prompt"), target: family.promptField + ".text"));
                    }
                }
                if (element.Attribute("tooltips") is { } tips)
                {
                    family.tooltipField = LocalName(view.names, "_localizedTooltip" + view.families.Count);
                    choices.Add(Choice(view, "Tooltip", id + "-tips", tips.Value, target: family.tooltipField + ".text"));
                    family.refresh.Add(family.tooltipField + ".RefreshText();");
                }
                var gearIndex = 0;
                foreach (var gearXml in element.Elements("gearText"))
                {
                    if (!text && choices.Find(choice => choice.member == "Text") == null)
                    {
                        var node = view.nodes.Find(value => value.element == id && value.item < 0);
                        var title = FindTitleChoice(node.target);
                        choices.Add(new LocalChoice { member = "Text", component = title.component, suffix = title.suffix,
                            text = title.text, variables = false, target = new StaticTextChoice("Text", node.field + ".title") });
                    }
                    var source = Array.Find(view.resource.ComponentXml.Controllers, c => c.Name == (string)gearXml.Attribute("controller"));
                    var validPages = new HashSet<string>();
                    foreach (var page in PageMembers(source)) validPages.Add(page.Id);
                    var gear = new LocalGear { controller = source.Name,
                        field = LocalName(view.names, "_localizedGear" + view.families.Count + "_" + gearIndex++) };
                    var fallback = gearXml.Attribute("default") is { } authoredDefault
                        ? Choice(view, "GearDefault" + gearIndex, id + "-texts_def", authoredDefault.Value, variables)
                        : CopyLocalChoice(choices.Find(choice => choice.member == "Text"), "GearDefault" + gearIndex);
                    fallback.target = new StaticTextChoice(fallback.member, gear.field + ".defaultValue");
                    choices.Add(fallback);
                    var pages = ((string)gearXml.Attribute("pages") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
                    var values = ((string)gearXml.Attribute("values") ?? "").Split('|');
                    var indices = new List<int>();
                    for (var i = 0; i < pages.Length; i++)
                        if (validPages.Contains(pages[i]))
                        {
                            var choice = Choice(view, "Gear" + gearIndex + "_" + i, id + "-texts_" + i, values[i], variables,
                                gear.field + ".values[" + indices.Count + "]");
                            if (values[i] == "-") choice.fallback = fallback;
                            choices.Add(choice);
                            indices.Add(i);
                        }
                    family.gears.Add(gear);
                    family.refresh.Add(gear.field + ".Apply(" + Field(source.Name) + ".page);");
                }
                family.source.choices = choices.ToArray();
            }
            if (view.resource.ComponentXml.Extension == Schema.ComponentExtension.ComboBox)
            {
                var family = AddLocalFamily(view, null, "Items");
                family.combo = true;
                family.source.choices = Array.Empty<LocalChoice>();
            }
        }

        private static LocalChoice FindTitleChoice(LocalView view)
        {
            var element = view.xml.Element("displayList");
            foreach (var child in element?.Elements() ?? Array.Empty<XElement>())
                if ((string)child.Attribute("name") == "title")
                    return Choice(view, "Text", (string)child.Attribute("id"), (string)child.Attribute("text"),
                        (string)child.Attribute("vars") == "true");
            throw new InvalidDataException("A text override requires an authored title.");
        }

        private static LocalChoice CopyLocalChoice(LocalChoice choice, string member = null) => new()
        {
            member = member ?? choice.member, component = choice.component, suffix = choice.suffix, text = choice.text,
            variables = choice.variables, fallback = choice.fallback, target = choice.target,
        };

        private static void CollectLocalOverrides(LocalView view)
        {
            foreach (var element in view.xml.Element("displayList")?.Elements() ?? Array.Empty<XElement>())
            {
                var id = (string)element.Attribute("id");
                if (element.Name.LocalName == "component")
                {
                    var node = view.nodes.Find(value => value.element == id && value.item < 0);
                    CollectInstanceTexts(view, new[] { node }, element, id);
                }
                else if (element.Name.LocalName is "list" or "tree")
                {
                    var index = 0;
                    foreach (var item in element.Elements("item"))
                    {
                        var node = view.nodes.Find(value => value.element == id && value.item == index);
                        CollectInstanceTexts(view, new[] { node }, item, id + "-" + index++);
                    }
                }
            }
        }

        private static void CollectInstanceTexts(LocalView owner, LocalNode[] path, XElement instance, string suffix)
        {
            var target = path[path.Length - 1].target;
            var extension = instance.Name.LocalName == "item" ? instance
                : instance.Element("Label") ?? instance.Element("Button") ?? instance.Element("ComboBox");
            if (extension != null)
            {
                AddCaptionOverride(owner, path, extension.Attribute("title"), suffix, "Text");
                AddCaptionOverride(owner, path, extension.Attribute("selectedTitle"), suffix + "-0", "Selected");
                AddCaptionOverride(owner, path, extension.Attribute("prompt"), suffix + "-prompt", "Prompt");
                if (extension.Element("item") != null || (string)extension.Attribute("autoClearItems") == "true")
                {
                    var family = target.families.Find(value => value.combo);
                    var row = OverrideRow(owner, path, family);
                    var choices = new List<LocalChoice>();
                    var index = 0;
                    foreach (var item in extension.Elements("item"))
                        choices.Add(Choice(owner, "Item" + index, suffix + "-" + index++, (string)item.Attribute("title")));
                    row.choices = choices.ToArray();
                    var title = (string)extension.Attribute("title");
                    row.initialIndex = title == null ? row.choices.Length == 0 ? -1 : 0 : Array.FindIndex(row.choices, value => value.text == title);
                }
            }
            foreach (var property in instance.Elements("property"))
                if ((string)property.Attribute("name") == "text")
                {
                    var names = ((string)property.Attribute("target")).Split('.');
                    var endpoint = target;
                    var fullPath = new List<LocalNode>(path);
                    for (var i = 0; i < names.Length - 1; i++)
                    {
                        var child = Array.Find(endpoint.resource.ComponentXml.DisplayList, value => value.Name == names[i]);
                        var node = child == null ? null : endpoint.nodes.Find(value => value.element == child.Id && value.item < 0);
                        if (node == null) { endpoint = null; break; }
                        fullPath.Add(node);
                        endpoint = node.target;
                    }
                    if (endpoint == null) continue; // The source object was deleted; FairyGUI skips the same override.
                    var display = Array.Find(endpoint.resource.ComponentXml.DisplayList, value => value.Name == names[names.Length - 1]);
                    if (display == null) continue;
                    LocalFamily family;
                    if (display.Kind == Schema.DisplayKind.Component)
                    {
                        var node = endpoint.nodes.Find(value => value.element == display.Id && value.item < 0);
                        fullPath.Add(node);
                        endpoint = node.target;
                        family = endpoint.families.Find(value => value.caption);
                    }
                    else family = endpoint.families.Find(value => value.element == display.Id);
                    if (family == null) continue; // GObject.text has no visible effect on a non-text leaf.
                    var row = OverrideRow(owner, fullPath.ToArray(), family);
                    ReplaceChoice(row, "Text", Choice(owner, "Text", suffix + (instance.Name.LocalName == "item" ? "-" : "-cp-")
                        + (string)property.Attribute("target"), (string)property.Attribute("value"),
                        Array.Find(row.choices, value => value.member == "Text").variables));
                }
        }

        private static void AddCaptionOverride(LocalView owner, LocalNode[] path, XAttribute attribute, string suffix, string member)
        {
            if (attribute == null) return;
            var family = path[path.Length - 1].target.families.Find(value => value.caption);
            if (family == null) return;
            var row = OverrideRow(owner, path, family);
            var source = Array.Find(row.choices, choice => choice.member == member);
            // An input may have no authored prompt; the override still owns the native placeholder.
            if (source == null) throw new InvalidDataException("The authored caption has no " + member + " slot.");
            ReplaceChoice(row, member, Choice(owner, member, suffix, attribute.Value, source.variables));
        }

        private static LocalRow OverrideRow(LocalView owner, LocalNode[] path, LocalFamily family)
        {
            foreach (var existing in owner.overrides)
            {
                if (existing.family != family || existing.path.Length != path.Length) continue;
                var equal = true;
                for (var i = 0; i < path.Length; i++) equal &= existing.path[i] == path[i];
                if (equal) return existing;
            }
            var row = new LocalRow { family = family, member = "Override" + owner.overrides.Count, path = path,
                targetField = path.Length == 1 ? path[0].field : LocalName(owner.names, "_localizedOverride" + owner.overrides.Count),
                choices = Array.ConvertAll(family.source.choices, choice => CopyLocalChoice(choice)) };
            owner.overrides.Add(row);
            return row;
        }

        private static void ReplaceChoice(LocalRow row, string member, LocalChoice value)
        {
            var index = Array.FindIndex(row.choices, choice => choice.member == member);
            value.target = row.choices[index].target;
            row.choices[index] = value;
        }

        private static string ChoiceText(LocalChoice choice, int language)
        {
            var fallback = choice.fallback != null ? ChoiceText(choice.fallback, language) : choice.text;
            return language < 0 || choice.suffix == null ? fallback
                : _translations[language].Resolve(choice.component, choice.suffix, fallback);
        }

        private static void CompileLocalRow(LocalRow row)
        {
            TextTemplate[] Compile(int language)
            {
                var values = new TextTemplate[row.choices.Length];
                for (var i = 0; i < values.Length; i++)
                {
                    var choice = row.choices[i];
                    var text = row.prepared[language + 1][i];
                    values[i] = choice.variables ? TextTemplateCompiler.Compile(text, row.family.variables)
                        : new TextTemplate { literals = new[] { text }, slots = Array.Empty<int>(), defaults = Array.Empty<string>() };
                }
                return values;
            }
            row.authored = Compile(-1);
            row.localized = new TextTemplate[_translations.Length][];
            for (var language = 0; language < row.localized.Length; language++) row.localized[language] = Compile(language);
        }

        private static void PrepareLocalizedImages()
        {
#if FAIRYGUI_TMPRO
            var imported = new HashSet<Resource>();
            Sprite Sprite(string uri, Resource owner)
            {
                if (!TryResolve(uri, owner.PackageId, out var resource)) return null;
                if (resource.Type is Schema.ResourceKind.Image or Schema.ResourceKind.MovieClip && imported.Add(resource)) ImportImage(resource);
                return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(resource));
            }
#endif
            foreach (var view in LocalViews.Values)
                foreach (var family in view.families)
                {
                    var rows = new List<LocalRow> { family.source };
                    foreach (var source in LocalViews.Values)
                        foreach (var row in source.overrides)
                            if (row.family == family) rows.Add(row);
                    foreach (var row in rows)
                    {
                        row.prepared = new string[_translations.Length + 1][];
                        for (var language = -1; language < _translations.Length; language++)
                        {
                            var texts = row.prepared[language + 1] = new string[row.choices.Length];
                            for (var i = 0; i < texts.Length; i++) texts[i] = ChoiceText(row.choices[i], language);
                        }
                    }
#if FAIRYGUI_TMPRO
                    if (family.combo || family.element == null || Array.Find(family.source.choices, choice => choice.member != "Tooltip") == null) continue;
                    var element = Array.Find(view.resource.ComponentXml.DisplayList, value => value.Id == family.element);
                    if (element.Kind == Schema.DisplayKind.Component)
                        element = Array.Find(Resolve(element.Source, view.resource.PackageId).ComponentXml.DisplayList, value => value.Name == "title");
                    foreach (var prompt in new[] { false, true })
                    {
                        var ubb = prompt || element.Ubb;
                        var html = !prompt && element.Kind == Schema.DisplayKind.RichText;
                        if (!ubb && !html) continue;
                        var values = new List<string>();
                        var slots = new List<(string[] Row, int Index)>();
                        foreach (var row in rows)
                            foreach (var language in row.prepared)
                                for (var i = 0; i < row.choices.Length; i++)
                                    if ((row.choices[i].member == "Prompt") == prompt && row.choices[i].member != "Tooltip")
                                    { values.Add(language[i]); slots.Add((language, i)); }
                        if (values.Count == 0) continue;
                        var path = Path.ChangeExtension(AssetPath(view.resource.File), null) + "_" + Identifier(family.element)
                            + (prompt ? "_LocalizedPromptSprites.asset" : "_LocalizedSprites.asset");
                        var baked = TmpSpriteAssetBuilder.BakeInlineImageVariants(path, values, ubb, html,
                            uri => Sprite(uri, view.resource), out var sprites);
                        for (var i = 0; i < baked.Length; i++) slots[i].Row[slots[i].Index] = baked[i];
                        if (prompt) family.promptSprites = sprites; else family.bodySprites = sprites;
                    }
#endif
                }
        }

        private static string LocalizationCode(Resource resource)
        {
            var view = LocalViews[resource];
            var code = new StringBuilder();
            foreach (var node in view.nodes)
                if (node.privateField) code.AppendLine($"[UnityEngine.SerializeField] private {ViewType(node.target)} {node.field} = null;");
            var lists = new HashSet<string>();
            foreach (var node in view.nodes)
                if (node.item >= 0 && lists.Add(node.element))
                    code.AppendLine($"[UnityEngine.SerializeField] private OpenFairy.UGUI.ListSource {LocalListField(view, node.element)} = null;");
            foreach (var family in view.families)
            {
                if (family.combo)
                {
                    code.AppendLine($"[System.Serializable] public sealed class {family.name}Texts {{ public string[] Items; }}")
                        .AppendLine($"[UnityEngine.SerializeField] private {family.name}Texts _{family.name}Texts = null;")
                        .AppendLine($"public void Apply{family.name}Texts({family.name}Texts texts) {{ _{family.name}Texts = texts; Refresh{family.name}Text(); }}")
                        .AppendLine($"public void Refresh{family.name}Text() {{ items = _{family.name}Texts.Items; if (selectedIndex >= 0 && selectedIndex < items.Length) text = items[selectedIndex]; }}");
                    continue;
                }
                if (family.textField != null && family.privateText)
                    code.AppendLine($"[UnityEngine.SerializeField] private {(family.input ? "OpenFairyInputComponent" : "OpenFairyTextComponent")} {family.textField} = null;");
                if (family.promptField != null)
                    code.AppendLine($"[UnityEngine.SerializeField] private OpenFairyTextComponent {family.promptField} = null;");
                if (family.tooltipField != null)
                    code.AppendLine($"[UnityEngine.SerializeField] private OpenFairy.UGUI.Tooltip {family.tooltipField} = null;");
                foreach (var gear in family.gears)
                    code.AppendLine($"[UnityEngine.SerializeReference] private OpenFairy.UGUI.GearText<{ControllerEnumName(gear.controller)}> {gear.field} = null;");
                if (family.input)
                    code.AppendLine($"private void Set{family.name}Input(string value)\n{{\n#if FAIRYGUI_TMPRO\n{family.textField}.SetTextWithoutNotify(value);\n#else\n{family.textField}.text = value;\n#endif\n}}");
                code.AppendLine(StaticTextCode.Emit(family.name, family.variables.ToArray(),
                    Array.ConvertAll(family.source.choices, choice => choice.target), family.refresh.ToArray(), Identifier));
            }
            code.AppendLine("[System.Serializable] public sealed class LocalizedTexts\n{");
            foreach (var family in view.families) code.AppendLine($"public {family.name}Texts {family.source.member};");
            foreach (var row in view.overrides)
            {
                code.AppendLine($"public {ViewType(row.family.owner)}.{row.family.name}Texts {row.member};");
            }
            code.AppendLine("}");
            foreach (var row in view.overrides)
                if (row.path.Length > 1) code.AppendLine($"[UnityEngine.SerializeField] private {ViewType(row.family.owner)} {row.targetField} = null;");
            code.AppendLine($"public void ApplyLocale({LocaleType} texts)\n{{");
            foreach (var node in view.nodes)
                code.AppendLine(node.item < 0 ? $"{node.field}.ApplyLocale(texts);"
                    : $"if ({LocalListField(view, node.element)}.version == 0 && {node.field} != null) {node.field}.ApplyLocale(texts);");
            foreach (var row in view.overrides)
            {
                var condition = ItemPathCondition(view, row.path);
                code.AppendLine($"{condition}{row.targetField}.Apply{row.family.name}Texts(texts.{view.tableField}.{row.member});");
            }
            foreach (var family in view.families)
                code.AppendLine($"Apply{family.name}Texts(texts.{view.tableField}.{family.source.member});");
            code.AppendLine("}");
            return code.ToString();
        }

        private static string LocalListField(LocalView view, string element) => view.lists[element];
        private static string ItemPathCondition(LocalView view, LocalNode[] path) => path[0].item < 0 ? ""
            : "if (" + LocalListField(view, path[0].element) + ".version == 0 && " + path[0].field + " != null) ";

        private static void GenerateLocaleScripts()
        {
            var fields = new List<StaticLocaleField>();
            foreach (var view in LocalViews.Values) fields.Add(new StaticLocaleField(ViewType(view) + ".LocalizedTexts", view.tableField));
            WriteScript($"{OutputRoot}/Scripts/Localization/LocaleTable.cs", StaticLocaleCode.Table(LocaleNamespace, "LocaleTable", fields.ToArray()));
            var names = LocalLanguageNames();
            WriteScript($"{OutputRoot}/Scripts/Localization/LocaleSet.cs", StaticLocaleCode.Languages(LocaleNamespace, "LocaleSet", LocaleType, names));
        }

        private static string[] LocalLanguageNames()
        {
            var names = new string[_localization.languages.Length];
            var used = new HashSet<string>();
            for (var i = 0; i < names.Length; i++) names[i] = LocalName(used, _localization.languages[i].name);
            return names;
        }

        private static void BindLocalization(Resource resource, Component comp,
            Dictionary<string, (Schema.Display Xml, GameObject Go)> children, Dictionary<string, ControllerData> controllers)
        {
            var view = LocalViews[resource];
            var type = comp.GetType();
            foreach (var node in view.nodes)
            {
                var go = children[node.element].Go;
                var instance = node.item < 0 ? go.GetComponent<Component>()
                    : go.GetComponent<ListSource>().content.GetChild(node.item).GetComponent<Component>();
                type.GetField(node.field, LocalFields).SetValue(comp, instance);
                if (node.item >= 0) type.GetField(LocalListField(view, node.element), LocalFields).SetValue(comp, go.GetComponent<ListSource>());
            }
            foreach (var family in view.families)
            {
                if (!family.combo)
                {
                    var go = children[family.element].Go;
#if FAIRYGUI_TMPRO
                    if (family.bodySprites != null) go.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).spriteAsset = (TMPro.TMP_SpriteAsset)family.bodySprites;
#endif
                    if (family.textField != null)
                    {
#if FAIRYGUI_TMPRO
                        var textType = family.input ? typeof(TMPro.TMP_InputField) : typeof(TMPro.TextMeshProUGUI);
#else
                        var textType = family.input ? typeof(TextInput) : typeof(TextField);
#endif
                        type.GetField(family.textField, LocalFields).SetValue(comp, go.GetComponent(textType));
                    }
                    if (family.promptField != null)
                    {
#if FAIRYGUI_TMPRO
                        var input = go.GetComponent<TMPro.TMP_InputField>();
                        ConfigureTmpPrompt(input, "");
                        var prompt = input.placeholder.GetComponent<TMPro.TextMeshProUGUI>();
                        if (family.promptSprites != null) prompt.spriteAsset = (TMPro.TMP_SpriteAsset)family.promptSprites;
#else
                        var input = go.GetComponent<TextInput>().field;
                        if (input.placeholder == null)
                        {
                            var placeholder = NewChild("placeholder", (RectTransform)go.transform, typeof(TextField));
                            StretchFull((RectTransform)placeholder.transform);
                            var text = placeholder.GetComponent<TextField>();
                            text.font = input.textComponent.font;
                            text.fontSize = input.textComponent.fontSize;
                            text.alignment = input.textComponent.alignment;
                            text.ubb = true;
                            input.placeholder = text;
                        }
                        var prompt = input.placeholder.GetComponent<TextField>();
#endif
                        type.GetField(family.promptField, LocalFields).SetValue(comp, prompt);
                    }
                    if (family.tooltipField != null)
                    {
                        if (!go.TryGetComponent<Tooltip>(out var tooltip)) tooltip = go.AddComponent<Tooltip>();
                        type.GetField(family.tooltipField, LocalFields).SetValue(comp, tooltip);
                    }
                    foreach (var gear in family.gears)
                    {
                        var data = controllers[gear.controller];
                        var gearType = typeof(GearText<>).MakeGenericType(data.PageType);
                        var baked = data.Gears.Find(value => value.GetType() == gearType && (GameObject)gearType.GetField("target").GetValue(value) == go);
                        type.GetField(gear.field, LocalFields).SetValue(comp, baked);
                    }
                }
                var row = CreateLocalFamilyRow(family.source, -1, type.GetNestedType(family.name + "Texts"));
                type.GetField("_" + family.name + "Texts", LocalFields).SetValue(comp, row);
            }
            foreach (var row in view.overrides)
            {
                var endpoint = comp;
                foreach (var node in row.path) endpoint = (Component)endpoint.GetType().GetField(node.field, LocalFields).GetValue(endpoint);
                type.GetField(row.targetField, LocalFields).SetValue(comp, endpoint);
                if (row.initialIndex is { } index) ((IComboBox)endpoint).selectedIndex = index;
                var texts = CreateLocalFamilyRow(row, -1, endpoint.GetType().GetNestedType(row.family.name + "Texts"));
                endpoint.GetType().GetMethod("Apply" + row.family.name + "Texts").Invoke(endpoint, row.family.input
                    ? new[] { texts, (object)true } : new[] { texts });
            }
            foreach (var family in view.families)
                type.GetMethod("Refresh" + family.name + "Text").Invoke(comp, family.input ? new object[] { true } : null);
        }

        private static object CreateLocalFamilyRow(LocalRow row, int language, Type type)
        {
            var family = row.family;
            var result = Activator.CreateInstance(type);
            if (family.combo)
            {
                var values = new string[row.choices.Length];
                if (row == family.source)
                    values = family.owner.resource.ComponentXml.ComboBox?.Items ?? Array.Empty<string>();
                else for (var i = 0; i < values.Length; i++) values[i] = ChoiceText(row.choices[i], language);
                type.GetField("Items").SetValue(result, values);
            }
            else
            {
                var values = language < 0 ? row.authored : row.localized[language];
                for (var i = 0; i < row.choices.Length; i++)
                    type.GetField(row.choices[i].member).SetValue(result, family.variables.Count > 0 ? values[i] : (object)values[i].literals[0]);
            }
            return result;
        }

        private static void SaveLocaleAssets(string directory)
        {
            var type = LocaleAssetType("LocaleTable");
            Directory.CreateDirectory(directory);
            var tables = Array.CreateInstance(type, _translations.Length);
            var names = LocalLanguageNames();
            for (var language = 0; language < _translations.Length; language++)
            {
                var path = directory + "/" + names[language] + ".asset";
                var table = AssetDatabase.LoadAssetAtPath(path, type);
                if (table == null) { table = ScriptableObject.CreateInstance(type); AssetDatabase.CreateAsset(table, path); }
                foreach (var view in LocalViews.Values)
                {
                    var rowType = type.GetField(view.tableField).FieldType;
                    var row = Activator.CreateInstance(rowType);
                    foreach (var family in view.families)
                    {
                        var field = rowType.GetField(family.source.member);
                        field.SetValue(row, CreateLocalFamilyRow(family.source, language, field.FieldType));
                    }
                    foreach (var binding in view.overrides)
                    {
                        var field = rowType.GetField(binding.member);
                        field.SetValue(row, CreateLocalFamilyRow(binding, language, field.FieldType));
                    }
                    type.GetField(view.tableField).SetValue(table, row);
                }
                EditorUtility.SetDirty(table);
                tables.SetValue(table, language);
            }
            var setType = LocaleAssetType("LocaleSet");
            var setPath = directory + "/Languages.asset";
            var set = AssetDatabase.LoadAssetAtPath(setPath, setType);
            if (set == null) { set = ScriptableObject.CreateInstance(setType); AssetDatabase.CreateAsset(set, setPath); }
            setType.GetField("tables", LocalFields).SetValue(set, tables);
            EditorUtility.SetDirty(set);
        }

        private static Type LocaleAssetType(string name)
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
                if (type.FullName == LocaleNamespace + "." + name) return type;
            throw new InvalidOperationException("The generated locale scripts must finish compiling before asset creation.");
        }
    }
}
