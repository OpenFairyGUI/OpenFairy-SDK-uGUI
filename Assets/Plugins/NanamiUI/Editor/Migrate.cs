using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NanamiUI.Editor
{
    public static class Migrate
    {
        private const string UiRoot = "UIProject";
        private const string OutputRoot = "Assets/UIProject";
        private const string Pending = "NanamiUI.Migrate.Pending";
        private static readonly string[] Entries = { "Basics/Main.xml" };

        private class Resource
        {
            public string Type;
            public string File;
            public string Package;
            public string PackageId;
            public string Scale9Grid;
        }

        private static readonly Dictionary<string, Resource> Resources = new();
        private static CommonSettings _settings;

        [MenuItem("Tools/Migrate %e")]
        public static void Execute()
        {
            Resources.Clear();
            foreach (var package in Directory.EnumerateFiles($"{UiRoot}/assets", "package.xml", SearchOption.AllDirectories))
                IndexPackage(package.Replace('\\', '/'));

            var components = new List<Resource>();
            var images = new HashSet<Resource>();
            foreach (var entry in Entries)
                Collect(Resources.Values.First(r => r.File == $"{UiRoot}/assets/{entry}"), components, images);

            foreach (var image in images)
                ImportImage(image);
            foreach (var component in components)
                GenerateScript(component);

            AssetDatabase.Refresh();
            if (EditorApplication.isCompiling)
            {
                SessionState.SetBool(Pending, true);
                return;
            }

            foreach (var component in components)
                BuildPrefab(component);
            AssetDatabase.SaveAssets();
            Debug.Log($"NanamiUI migrated {components.Count} components to {OutputRoot}.");
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (!SessionState.GetBool(Pending, false))
                return;
            SessionState.SetBool(Pending, false);
            Execute();
        }

        private static void IndexPackage(string packagePath)
        {
            var package = XDocument.Load(packagePath).Root;
            var packageId = package.Attribute("id").Value;
            var packageDir = Path.GetDirectoryName(packagePath).Replace('\\', '/');

            foreach (var resource in package.Element("resources").Elements())
                Resources[packageId + resource.Attribute("id").Value] = new Resource
                {
                    Type = resource.Name.LocalName,
                    File = $"{packageDir}{resource.Attribute("path").Value.TrimEnd('/')}/{resource.Attribute("name").Value}",
                    Package = Path.GetFileName(packageDir),
                    PackageId = packageId,
                    Scale9Grid = resource.Attribute("scale9grid")?.Value,
                };
        }

        private static void Collect(Resource component, List<Resource> components, HashSet<Resource> images)
        {
            if (components.Contains(component))
                return;

            foreach (var child in DisplayList(component.File))
            {
                var src = child.Attribute("src")?.Value ?? child.Attribute("url")?.Value;
                if (src == null || !TryResolve(src, component.PackageId, out var dep))
                    continue;
                if (dep.Type == "component")
                    Collect(dep, components, images);
                else if (dep.Type == "image")
                    images.Add(dep);
            }
            components.Add(component);
        }

        private static void ImportImage(Resource image)
        {
            var target = AssetPath(image.File);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(image.File, target, true);
            AssetDatabase.ImportAsset(target);

            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (image.Scale9Grid != null)
            {
                var texture = new Texture2D(2, 2);
                texture.LoadImage(File.ReadAllBytes(image.File));
                var grid = image.Scale9Grid.Split(',').Select(int.Parse).ToArray();
                importer.spriteBorder = new Vector4(grid[0], texture.height - grid[1] - grid[3], texture.width - grid[0] - grid[2], grid[1]);
                Object.DestroyImmediate(texture);
            }
            importer.SaveAndReimport();
        }

        private static void GenerateScript(Resource component)
        {
            var xml = XDocument.Load(component.File).Root;
            var name = Identifier(Path.GetFileNameWithoutExtension(component.File));
            var path = ScriptPath(component.File);
            var used = new HashSet<string>();
            var fields = new List<string>();

            foreach (var controller in xml.Elements("controller"))
            {
                var cname = Identifier(controller.Attribute("name").Value);
                var members = PageNames(controller).Select((page, i) => page == "" ? $"Page{i}" : Identifier(Cap(page)));
                if (used.Add(cname))
                    fields.Add($"        public {name}_{cname} {cname};");
                WriteScript($"{Path.GetDirectoryName(path)}/{name}_{cname}.cs".Replace('\\', '/'),
                    $"namespace {component.Package}\n{{\n    public class {name}_{cname} : NanamiUI.Controller<{name}_{cname}.Page>\n    {{\n        public enum Page {{ {string.Join(", ", members)} }}\n    }}\n}}\n");
            }

            foreach (var child in xml.Element("displayList")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                var fieldName = Identifier(child.Attribute("name").Value);
                var fieldType = FieldType(child, component.PackageId);
                if (fieldType != null && used.Add(fieldName))
                    fields.Add($"        public {fieldType} {fieldName};");
            }

            var baseType = xml.Attribute("extention")?.Value == "Button" ? "NanamiUI.Button" : "NanamiUI.Component";
            var code = new StringBuilder();
            code.AppendLine("using UnityEngine;");
            code.AppendLine("using UnityEngine.UI;");
            code.AppendLine();
            code.AppendLine($"namespace {component.Package}");
            code.AppendLine("{");
            code.AppendLine($"    public class {name} : {baseType}");
            code.AppendLine("    {");
            foreach (var line in fields)
                code.AppendLine(line);
            code.AppendLine("    }");
            code.AppendLine("}");

            WriteScript(path, code.ToString());
        }

        private static void WriteScript(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!File.Exists(path) || File.ReadAllText(path) != text)
                File.WriteAllText(path, text);
        }

        private static string FieldType(XElement child, string packageId) => child.Name.LocalName switch
        {
            "image" or "graph" => "Image",
            "text" or "richtext" or "inputtext" => "NanamiUI.Text",
            "loader" => "NanamiUI.Loader",
            "component" when TryResolve(child.Attribute("src").Value, packageId, out var dep) =>
                $"{dep.Package}.{Identifier(Path.GetFileNameWithoutExtension(dep.File))}",
            "component" => null,
            _ => "RectTransform",
        };

        private static void BuildPrefab(Resource component)
        {
            var xml = XDocument.Load(component.File).Root;
            var root = new GameObject(Path.GetFileNameWithoutExtension(component.File), typeof(RectTransform));
            try
            {
                var rt = (RectTransform)root.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = Pair(xml, "size").Value;
                if (xml.Attribute("overflow")?.Value == "hidden")
                    root.AddComponent<RectMask2D>();

                var comp = (Component)root.AddComponent(FindType($"{component.Package}.{Identifier(root.name)}"));

                var controllers = new Dictionary<string, Controller>();
                var pageIds = new Dictionary<Controller, string[]>();
                var gears = new Dictionary<Controller, List<Gear>>();
                foreach (var element in xml.Elements("controller"))
                {
                    var cname = element.Attribute("name").Value;
                    var controller = (Controller)root.AddComponent(FindType($"{component.Package}.{Identifier(root.name)}_{Identifier(cname)}"));
                    controller.pageNames = PageNames(element);
                    controller.selected = int.Parse(element.Attribute("selected")?.Value ?? "0");
                    pageIds[controller] = element.Attribute("pages").Value.Split(',').Where((_, i) => i % 2 == 0).ToArray();
                    controllers[cname] = controller;
                    gears[controller] = new List<Gear>();
                }

                var children = new List<(XElement Xml, GameObject Go)>();
                foreach (var element in xml.Element("displayList")?.Elements() ?? Enumerable.Empty<XElement>())
                    children.Add((element, CreateChild(element, rt, component, controllers, pageIds, gears)));

                foreach (var (element, go) in children)
                    if (element.Name.LocalName == "group")
                        foreach (var (member, memberGo) in children)
                            if (member.Attribute("group")?.Value == element.Attribute("id").Value)
                                memberGo.transform.SetParent(go.transform, true);

                foreach (var (controller, list) in gears)
                {
                    controller.gears = list.ToArray();
                    foreach (var gear in controller.gears)
                        gear.Apply(controller.selected);
                }

                if (comp is Button button)
                {
                    button.controller = controllers.GetValueOrDefault("button");
                    button.titleText = children.FirstOrDefault(c => c.Xml.Attribute("name").Value == "title").Go?.GetComponent<Text>();
                }

                var byName = new Dictionary<string, GameObject>();
                foreach (var (element, go) in children)
                    byName.TryAdd(Identifier(element.Attribute("name").Value), go);

                foreach (var field in comp.GetType().GetFields())
                {
                    var controller = controllers.FirstOrDefault(pair => Identifier(pair.Key) == field.Name).Value;
                    if (controller != null && field.FieldType.IsInstanceOfType(controller))
                        field.SetValue(comp, controller);
                    else if (byName.TryGetValue(field.Name, out var go))
                        field.SetValue(comp, field.FieldType == typeof(RectTransform) ? go.transform : (object)go.GetComponent(field.FieldType));
                }

                var prefabPath = Path.ChangeExtension(AssetPath(component.File), ".prefab");
                Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateChild(XElement element, RectTransform parent, Resource owner,
            Dictionary<string, Controller> controllers, Dictionary<Controller, string[]> pageIds, Dictionary<Controller, List<Gear>> gears)
        {
            var name = element.Attribute("name").Value;
            var size = Pair(element, "size");
            GameObject go;

            switch (element.Name.LocalName)
            {
                case "image":
                {
                    go = NewChild(name, parent, typeof(Image));
                    var image = go.GetComponent<Image>();
                    image.sprite = LoadSprite(element.Attribute("src").Value, owner.PackageId);
                    image.type = image.sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
                    if (element.Attribute("color") is { } tint)
                        image.color = ParseColor(tint.Value);
                    size ??= image.sprite.rect.size;
                    break;
                }
                case "graph":
                {
                    go = NewChild(name, parent, typeof(Image));
                    var fill = ParseColor(element.Attribute("fillColor")?.Value ?? "#ffffffff");
                    var line = float.Parse(element.Attribute("lineSize")?.Value ?? "1", CultureInfo.InvariantCulture);
                    if (line > 0)
                    {
                        go.GetComponent<Image>().color = ParseColor(element.Attribute("lineColor")?.Value ?? "#ff000000");
                        var inner = NewChild("fill", (RectTransform)go.transform, typeof(Image));
                        var innerRt = (RectTransform)inner.transform;
                        innerRt.anchorMin = Vector2.zero;
                        innerRt.anchorMax = Vector2.one;
                        innerRt.offsetMin = new Vector2(line, line);
                        innerRt.offsetMax = new Vector2(-line, -line);
                        inner.GetComponent<Image>().color = fill;
                    }
                    else
                        go.GetComponent<Image>().color = fill;
                    break;
                }
                case "text":
                case "richtext":
                case "inputtext":
                {
                    go = NewChild(name, parent, typeof(Text));
                    ConfigureText(go.GetComponent<Text>(), element, owner);
                    if (element.Attribute("strokeColor") is { } stroke)
                    {
                        var effect = go.AddComponent<TextStroke>();
                        effect.color = ParseColor(stroke.Value);
                        effect.width = float.Parse(element.Attribute("strokeSize")?.Value ?? "1", CultureInfo.InvariantCulture);
                    }
                    break;
                }
                case "loader":
                {
                    go = NewChild(name, parent, typeof(Loader));
                    var loader = go.GetComponent<Loader>();
                    var url = element.Attribute("url")?.Value;
                    if (url != null && TryResolve(url, owner.PackageId, out var content) && content.Type == "image")
                    {
                        loader.sprite = LoadSprite(url, owner.PackageId);
                        loader.type = loader.sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
                    }
                    else
                        loader.enabled = false;
                    break;
                }
                case "component":
                {
                    var dep = Resolve(element.Attribute("src").Value, owner.PackageId);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path.ChangeExtension(AssetPath(dep.File), ".prefab"));
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    go.name = name;
                    size ??= ((RectTransform)go.transform).sizeDelta;
                    if (element.Element("Button")?.Attribute("title") is { } title)
                        go.GetComponent<Button>().Title = title.Value;
                    break;
                }
                default:
                    go = NewChild(name, parent);
                    break;
            }

            SetRect((RectTransform)go.transform, element, size ?? Vector2.zero, parent);
            if (element.Attribute("visible")?.Value == "false")
                go.SetActive(false);

            foreach (var gearXml in element.Elements())
            {
                var kind = gearXml.Name.LocalName;
                if (kind != "gearDisplay" && kind != "gearXY")
                    continue;

                var controller = controllers[gearXml.Attribute("controller").Value];
                Gear gear;
                if (kind == "gearDisplay")
                    gear = go.AddComponent<GearDisplay>();
                else
                {
                    var xy = go.AddComponent<GearXY>();
                    var rt = (RectTransform)go.transform;
                    var origin = Pair(element, "xy") ?? Vector2.zero;
                    Vector2 Convert(string pair)
                    {
                        var parts = pair.Split(',');
                        return rt.anchoredPosition + new Vector2(
                            float.Parse(parts[0], CultureInfo.InvariantCulture) - origin.x,
                            origin.y - float.Parse(parts[1], CultureInfo.InvariantCulture));
                    }
                    xy.values = gearXml.Attribute("values").Value.Split('|').Select(Convert).ToArray();
                    xy.defaultValue = gearXml.Attribute("default") is { } def ? Convert(def.Value) : rt.anchoredPosition;
                    gear = xy;
                }
                gear.pages = (gearXml.Attribute("pages")?.Value.Split(',') ?? Array.Empty<string>())
                    .Select(id => Array.IndexOf(pageIds[controller], id)).ToArray();
                gears[controller].Add(gear);
            }

            return go;
        }

        private static void SetRect(RectTransform rt, XElement element, Vector2 size, RectTransform parent)
        {
            var xy = Pair(element, "xy") ?? Vector2.zero;
            Vector2 anchorMin = new(0, 1), anchorMax = new(0, 1);
            foreach (var relation in element.Elements("relation"))
            {
                if (relation.Attribute("target")?.Value != "")
                    continue;
                foreach (var pair in relation.Attribute("sidePair").Value.Split(','))
                    switch (pair)
                    {
                        case "width-width": anchorMin.x = 0; anchorMax.x = 1; break;
                        case "height-height": anchorMin.y = 0; anchorMax.y = 1; break;
                        case "right-right": anchorMin.x = anchorMax.x = 1; break;
                        case "center-center": anchorMin.x = anchorMax.x = 0.5f; break;
                        case "bottom-bottom": anchorMin.y = anchorMax.y = 0; break;
                        case "middle-middle": anchorMin.y = anchorMax.y = 0.5f; break;
                    }
            }

            var pw = parent.rect.width;
            var ph = parent.rect.height;
            rt.pivot = new Vector2(0, 1);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(xy.x - anchorMin.x * pw, ph - xy.y - size.y - anchorMin.y * ph);
            rt.offsetMax = new Vector2(xy.x + size.x - anchorMax.x * pw, ph - xy.y - anchorMax.y * ph);
        }

        private static void ConfigureText(Text text, XElement element, Resource owner)
        {
            text.text = element.Attribute("text")?.Value ?? "";
            text.fontSize = int.Parse(element.Attribute("fontSize")?.Value ?? Settings().fontSize.ToString());
            text.leading = int.Parse(element.Attribute("leading")?.Value ?? "3");
            text.color = ParseColor(element.Attribute("color")?.Value ?? Settings().textColor);
            text.supportRichText = element.Name.LocalName == "richtext";
            var bold = element.Attribute("bold")?.Value == "true";
            var italic = element.Attribute("italic")?.Value == "true";
            text.fontStyle = bold && italic ? FontStyle.BoldAndItalic : bold ? FontStyle.Bold : italic ? FontStyle.Italic : FontStyle.Normal;
            var autoSize = element.Attribute("autoSize")?.Value ?? "both";
            text.horizontalOverflow = autoSize == "both" || element.Attribute("singleLine")?.Value == "true"
                ? HorizontalWrapMode.Overflow
                : HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = (element.Attribute("vAlign")?.Value, element.Attribute("align")?.Value) switch
            {
                ("middle", "center") => TextAnchor.MiddleCenter,
                ("middle", "right") => TextAnchor.MiddleRight,
                ("middle", _) => TextAnchor.MiddleLeft,
                ("bottom", "center") => TextAnchor.LowerCenter,
                ("bottom", "right") => TextAnchor.LowerRight,
                ("bottom", _) => TextAnchor.LowerLeft,
                (_, "center") => TextAnchor.UpperCenter,
                (_, "right") => TextAnchor.UpperRight,
                _ => TextAnchor.UpperLeft,
            };
            if (element.Attribute("font") is { } font && !TryResolve(font.Value, owner.PackageId, out _))
                text.fontNames = font.Value;
        }

        private static IEnumerable<XElement> DisplayList(string file) =>
            XDocument.Load(file).Root.Element("displayList")?.Elements() ?? Enumerable.Empty<XElement>();

        private static string[] PageNames(XElement controller) =>
            controller.Attribute("pages").Value.Split(',').Where((_, i) => i % 2 == 1).ToArray();

        private static GameObject NewChild(string name, RectTransform parent, params Type[] components)
        {
            var go = new GameObject(name, components.Prepend(typeof(RectTransform)).ToArray());
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Sprite LoadSprite(string src, string packageId) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath(Resolve(src, packageId).File));

        private static Resource Resolve(string value, string packageId) =>
            Resources[value.StartsWith("ui://") ? value[5..] : packageId + value];

        private static bool TryResolve(string value, string packageId, out Resource resource) =>
            Resources.TryGetValue(value.StartsWith("ui://") ? value[5..] : packageId + value, out resource);

        private static Type FindType(string fullName) =>
            TypeCache.GetTypesDerivedFrom<MonoBehaviour>().First(type => type.FullName == fullName);

        private static Vector2? Pair(XElement element, string attribute)
        {
            var value = element.Attribute(attribute)?.Value;
            if (value == null)
                return null;
            var parts = value.Split(',');
            return new Vector2(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture));
        }

        private static Color ParseColor(string value)
        {
            ColorUtility.TryParseHtmlString(value.Length == 9 ? $"#{value[3..]}{value[1..3]}" : value, out var color);
            return color;
        }

        private static string Identifier(string name)
        {
            var id = new string(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
            return char.IsDigit(id[0]) ? "_" + id : id;
        }

        private static string Cap(string name) => char.ToUpper(name[0]) + name[1..];

        private static string AssetPath(string file) =>
            $"{OutputRoot}/Assets/{Path.GetRelativePath($"{UiRoot}/assets", file).Replace('\\', '/')}";

        private static string ScriptPath(string file) =>
            Path.ChangeExtension($"{OutputRoot}/Scripts/{Path.GetRelativePath($"{UiRoot}/assets", file).Replace('\\', '/')}", ".cs").Replace('\\', '/');

        private static CommonSettings Settings() =>
            _settings ??= JsonUtility.FromJson<CommonSettings>(File.ReadAllText($"{UiRoot}/settings/Common.json"));

        [Serializable]
        private class CommonSettings
        {
            public int fontSize;
            public string textColor;
        }
    }
}
