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
        private static readonly string[] Entries =
        {
            "Basics/Main.xml",
            "Basics/Demo_Button.xml",
            "Basics/Demo_Image.xml",
            "Basics/Demo_Graph.xml",
            "Basics/Demo_Loader.xml",
            "Basics/Demo_Label.xml",
            "Basics/Demo_Text.xml",
        };
        private static readonly string[] BasicsDemoNames =
        {
            "Button",
            "Image",
            "Graph",
            "Loader",
            "Label",
            "Text",
        };

        private class Resource
        {
            public string Type;
            public string File;
            public string Package;
            public string PackageId;
            public string Scale;
            public string Scale9Grid;
        }

        private class ControllerData
        {
            public string Name;
            public Type PageType;
            public string[] PageIds;
            public object[] PageValues;
            public int Selected;
            public List<object> Gears = new();
            public object Value;

            public string FieldName => Field(Name);
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
                    Scale = resource.Attribute("scale")?.Value,
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
                else if (dep.Type is "image" or "movieclip")
                    images.Add(dep);
                foreach (var icon in child.Elements().Select(element => element.Attribute("icon")?.Value).Where(value => value != null))
                    if (TryResolve(icon, component.PackageId, out dep) && dep.Type is "image" or "movieclip")
                        images.Add(dep);
            }
            components.Add(component);
        }

        private static void ImportImage(Resource image)
        {
            var target = SpritePath(image);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (image.Type == "movieclip")
                File.WriteAllBytes(target, FirstPng(File.ReadAllBytes(image.File)));
            else
                File.Copy(image.File, target, true);
            AssetDatabase.ImportAsset(target);

            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = image.Scale == "tile" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
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
                DeleteScript($"{Path.GetDirectoryName(path)}/{name}_{cname}.cs".Replace('\\', '/'));
                if (used.Add(Field(cname)))
                {
                    fields.Add($"        public enum {cname}");
                    fields.Add("        {");
                    fields.AddRange(PageMembers(controller).Select(page => $"            {page.Member},"));
                    fields.Add("        }");
                    fields.Add($"        public Controller<{cname}> {Field(cname)};");
                }
            }

            foreach (var child in xml.Element("displayList")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                var fieldName = Identifier(child.Attribute("name").Value);
                var fieldType = FieldType(child, component.PackageId);
                if (fieldType != null && used.Add(Field(fieldName)))
                    fields.Add($"        public {fieldType} {Field(fieldName)};");
            }

            var baseType = xml.Attribute("extention")?.Value == "Button"
                ? $"NanamiUI.Button<{name}.{Identifier(xml.Element("controller").Attribute("name").Value)}>"
                : "NanamiUI.Component";
            var code = new StringBuilder();
            if (xml.Elements("controller").Any())
                code.AppendLine("using NanamiUI;");
            code.AppendLine("using UnityEngine;");
            code.AppendLine("using UnityEngine.UI;");
            code.AppendLine();
            code.AppendLine($"namespace {component.Package}");
            code.AppendLine("{");
            code.AppendLine($"    public partial class {name} : {baseType}");
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

        private static void DeleteScript(string path)
        {
            if (File.Exists(path))
                AssetDatabase.DeleteAsset(path);
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

                var componentType = FindType($"{component.Package}.{Identifier(root.name)}");
                var comp = (Component)root.AddComponent(componentType);

                var controllers = new Dictionary<string, ControllerData>();
                foreach (var element in xml.Elements("controller"))
                {
                    var name = Identifier(element.Attribute("name").Value);
                    var pages = PageMembers(element).ToArray();
                    var pageType = componentType.GetNestedType(name);
                    controllers[element.Attribute("name").Value] = new ControllerData
                    {
                        Name = name,
                        PageType = pageType,
                        PageIds = pages.Select(page => page.Id).ToArray(),
                        PageValues = pages.Select(page => Enum.Parse(pageType, page.Member)).ToArray(),
                        Selected = int.Parse(element.Attribute("selected")?.Value ?? "0"),
                    };
                }

                var children = new List<(XElement Xml, GameObject Go)>();
                foreach (var element in xml.Element("displayList")?.Elements() ?? Enumerable.Empty<XElement>())
                    children.Add((element, CreateChild(element, rt, component, controllers)));

                foreach (var (element, go) in children)
                    if (element.Name.LocalName == "group")
                        foreach (var (member, memberGo) in children)
                            if (member.Attribute("group")?.Value == element.Attribute("id").Value)
                                memberGo.transform.SetParent(go.transform, true);

                foreach (var controller in controllers.Values)
                    BuildController(controller);

                if (IsButton(comp.GetType()))
                {
                    comp.GetType().GetField("controller").SetValue(comp, controllers["button"].Value);
                    comp.GetType().GetField("titleText").SetValue(comp, children.FirstOrDefault(c => c.Xml.Attribute("name").Value == "title").Go?.GetComponent<Text>());
                    comp.GetType().GetField("iconLoader").SetValue(comp, children.FirstOrDefault(c => c.Xml.Attribute("name").Value == "icon").Go?.GetComponent<Loader>());
                    ConfigureButton(root, xml.Element("Button"), component.PackageId);
                }

                var byName = new Dictionary<string, GameObject>();
                foreach (var (element, go) in children)
                    byName.TryAdd(Field(element.Attribute("name").Value), go);

                foreach (var field in comp.GetType().GetFields())
                {
                    var controller = controllers.Values.FirstOrDefault(data => data.FieldName == field.Name);
                    if (controller != null)
                        field.SetValue(comp, controller.Value);
                    else if (byName.TryGetValue(field.Name, out var go))
                        field.SetValue(comp, field.FieldType == typeof(RectTransform) ? go.transform : (object)go.GetComponent(field.FieldType));
                }

                if (component.File == $"{UiRoot}/assets/Basics/Main.xml")
                    ConfigureBasicsExample(root);

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
            Dictionary<string, ControllerData> controllers)
        {
            var name = element.Attribute("name").Value;
            var size = Pair(element, "size");
            GameObject go;

            switch (element.Name.LocalName)
            {
                case "image":
                {
                    go = NewChild(name, parent, typeof(FlipImage));
                    var image = go.GetComponent<FlipImage>();
                    var resource = Resolve(element.Attribute("src").Value, owner.PackageId);
                    image.sprite = LoadSprite(element.Attribute("src").Value, owner.PackageId);
                    ConfigureImage(image, resource, element);
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
                        ConfigureImage(loader, content, element);
                    }
                    else if (url != null && TryResolve(url, owner.PackageId, out content) && content.Type == "movieclip")
                    {
                        loader.sprite = LoadSprite(url, owner.PackageId);
                        loader.type = Image.Type.Simple;
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
                    if (element.Element("Button") is { } button)
                        ConfigureButton(go, button, owner.PackageId);
                    if (element.Element("Label") is { } label)
                        ConfigureLabel(go, label, owner.PackageId);
                    break;
                }
                default:
                    go = NewChild(name, parent);
                    break;
            }

            SetRect((RectTransform)go.transform, element, size ?? Vector2.zero, parent);
            ApplyElement(go, element);
            if (element.Attribute("visible")?.Value == "false")
                go.SetActive(false);

            foreach (var gearXml in element.Elements())
            {
                var kind = gearXml.Name.LocalName;
                if (kind != "gearDisplay" && kind != "gearXY" && kind != "gearColor" && kind != "gearLook" && kind != "gearSize")
                    continue;

                var controller = controllers[gearXml.Attribute("controller").Value];
                var gearType = (kind switch
                {
                    "gearDisplay" => typeof(GearDisplay<>),
                    "gearXY" => typeof(GearXY<>),
                    "gearColor" => typeof(GearColor<>),
                    "gearLook" => typeof(GearLook<>),
                    _ => typeof(GearSize<>),
                }).MakeGenericType(controller.PageType);
                var gear = Activator.CreateInstance(gearType);
                gearType.GetField("target").SetValue(gear, go);
                gearType.GetField("pages").SetValue(gear, PageValues(controller, gearXml.Attribute("pages")?.Value));
                if (kind == "gearXY")
                {
                    var rt = (RectTransform)go.transform;
                    var origin = Pair(element, "xy") ?? Vector2.zero;
                    Vector2 Convert(string pair)
                    {
                        if (pair == "-")
                            return rt.anchoredPosition;
                        var parts = pair.Split(',');
                        return rt.anchoredPosition + new Vector2(
                            float.Parse(parts[0], CultureInfo.InvariantCulture) - origin.x,
                            origin.y - float.Parse(parts[1], CultureInfo.InvariantCulture));
                    }
                    gearType.GetField("values").SetValue(gear, gearXml.Attribute("values").Value.Split('|').Select(Convert).ToArray());
                    gearType.GetField("defaultValue").SetValue(gear, gearXml.Attribute("default") is { } def ? Convert(def.Value) : rt.anchoredPosition);
                }
                else if (kind == "gearColor")
                {
                    var graphic = go.GetComponent<Graphic>();
                    var def = gearXml.Attribute("default") is { } value ? ParseColor(value.Value) : graphic.color;
                    gearType.GetField("values").SetValue(gear, gearXml.Attribute("values").Value.Split('|').Select(value => value == "-" ? def : ParseColor(value)).ToArray());
                    gearType.GetField("defaultValue").SetValue(gear, def);
                }
                else if (kind == "gearLook")
                    ConfigureGearLook(gearType, gear, gearXml);
                else if (kind == "gearSize")
                    ConfigureGearSize(gearType, gear, gearXml, (RectTransform)go.transform);
                controller.Gears.Add(gear);
            }

            return go;
        }

        private static void BuildController(ControllerData data)
        {
            var gearType = typeof(Gear<>).MakeGenericType(data.PageType);
            var gears = Array.CreateInstance(gearType, data.Gears.Count);
            for (var i = 0; i < data.Gears.Count; i++)
                gears.SetValue(data.Gears[i], i);

            var controllerType = typeof(Controller<>).MakeGenericType(data.PageType);
            data.Value = Activator.CreateInstance(controllerType);
            controllerType.GetField("gears").SetValue(data.Value, gears);
            controllerType.GetProperty("page").SetValue(data.Value, data.PageValues[data.Selected]);
        }

        private static Array PageValues(ControllerData controller, string ids)
        {
            var parts = ids?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            var values = Array.CreateInstance(controller.PageType, parts.Length);
            for (var i = 0; i < parts.Length; i++)
                values.SetValue(controller.PageValues[Array.IndexOf(controller.PageIds, parts[i])], i);
            return values;
        }

        private static void ConfigureBasicsExample(GameObject root)
        {
            var demo = root.AddComponent<global::NanamiUI.Example.BasicsMain>();
            demo.demoNames = BasicsDemoNames;
            demo.demoPrefabs = BasicsDemoNames
                .Select(name => AssetDatabase.LoadAssetAtPath<GameObject>($"{OutputRoot}/Assets/Basics/Demo_{name}.prefab"))
                .ToArray();
        }

        private static void ConfigureImage(Image image, Resource resource, XElement element)
        {
            image.type = resource.Scale == "tile" ? Image.Type.Tiled : image.sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
            image.color = ParseColor(element.Attribute("color")?.Value ?? "#ffffffff");
            if (image is FlipImage flip && element.Attribute("flip") is { } value)
            {
                flip.flipX = value.Value is "hz" or "both";
                flip.flipY = value.Value is "vt" or "both";
            }
        }

        private static void ConfigureButton(GameObject go, XElement buttonXml, string packageId)
        {
            var button = ButtonComponent(go);
            if (buttonXml.Attribute("title") is { } title)
                button.GetType().GetProperty("Title").SetValue(button, title.Value);
            if (buttonXml.Attribute("selectedTitle") is { } selectedTitle)
                button.GetType().GetField("selectedTitle").SetValue(button, selectedTitle.Value);
            if (buttonXml.Attribute("icon") is { } icon)
                button.GetType().GetProperty("Icon").SetValue(button, LoadSprite(icon.Value, packageId));
            if (buttonXml.Attribute("mode") is { } mode)
                button.GetType().GetField("mode").SetValue(button, Enum.Parse(typeof(ButtonMode), mode.Value));
            button.GetType().GetField("selected").SetValue(button, buttonXml.Attribute("checked")?.Value == "true");
            button.GetType().GetMethod("RefreshState").Invoke(button, null);
        }

        private static void ConfigureLabel(GameObject go, XElement labelXml, string packageId)
        {
            if (labelXml.Attribute("title") is { } title && FindChild(go.transform, "title")?.GetComponent<Text>() is { } titleText)
                titleText.text = title.Value;
            if (labelXml.Attribute("titleColor") is { } titleColor && FindChild(go.transform, "title")?.GetComponent<Text>() is { } colorText)
                colorText.color = ParseColor(titleColor.Value);
            if (labelXml.Attribute("icon") is { } icon && FindChild(go.transform, "icon")?.GetComponent<Image>() is { } image)
            {
                image.sprite = LoadSprite(icon.Value, packageId);
                image.enabled = true;
            }
        }

        private static void ApplyElement(GameObject go, XElement element)
        {
            var rt = (RectTransform)go.transform;
            if (element.Attribute("rotation") is { } rotation)
                rt.localEulerAngles = new Vector3(0, 0, -float.Parse(rotation.Value, CultureInfo.InvariantCulture));
            if (element.Attribute("alpha") is { } alpha)
                foreach (var graphic in go.GetComponentsInChildren<Graphic>(true))
                {
                    var color = graphic.color;
                    color.a *= float.Parse(alpha.Value, CultureInfo.InvariantCulture);
                    graphic.color = color;
                }
            if (element.Attribute("touchable")?.Value == "false")
                go.AddComponent<CanvasGroup>().blocksRaycasts = false;
            if (element.Attribute("grayed")?.Value == "true")
            {
                go.AddComponent<Grayed>();
                if (go.GetComponents<UnityEngine.Component>().FirstOrDefault(component => IsButton(component.GetType())) is { } button)
                {
                    button.GetType().GetField("grayed").SetValue(button, true);
                    button.GetType().GetMethod("RefreshState").Invoke(button, null);
                }
            }
        }

        private static void ConfigureGearLook(Type gearType, object gear, XElement xml)
        {
            (float Alpha, float Rotation, bool Grayed) Parse(string value, (float Alpha, float Rotation, bool Grayed) def)
            {
                if (value == "-")
                    return def;
                var parts = value.Split(',');
                return (float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture), parts[2] == "1");
            }

            var def = Parse(xml.Attribute("default")?.Value ?? "1,0,0", (1, 0, false));
            var values = xml.Attribute("values").Value.Split('|').Select(value => Parse(value, def)).ToArray();
            gearType.GetField("alphas").SetValue(gear, values.Select(value => value.Alpha).ToArray());
            gearType.GetField("defaultAlpha").SetValue(gear, def.Alpha);
            gearType.GetField("rotations").SetValue(gear, values.Select(value => value.Rotation).ToArray());
            gearType.GetField("defaultRotation").SetValue(gear, def.Rotation);
            gearType.GetField("grayed").SetValue(gear, values.Select(value => value.Grayed).ToArray());
            gearType.GetField("defaultGrayed").SetValue(gear, def.Grayed);
        }

        private static void ConfigureGearSize(Type gearType, object gear, XElement xml, RectTransform rt)
        {
            (Vector2 Size, Vector2 Scale) Parse(string value, (Vector2 Size, Vector2 Scale) def)
            {
                if (value == "-")
                    return def;
                var parts = value.Split(',');
                return (new Vector2(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture)),
                    new Vector2(float.Parse(parts[2], CultureInfo.InvariantCulture), float.Parse(parts[3], CultureInfo.InvariantCulture)));
            }

            var fallback = (Size: rt.sizeDelta, Scale: Vector2.one);
            var def = xml.Attribute("default") is { } defaultValue ? Parse(defaultValue.Value, fallback) : fallback;
            var values = xml.Attribute("values").Value.Split('|').Select(value => Parse(value, def)).ToArray();
            gearType.GetField("sizes").SetValue(gear, values.Select(value => value.Size).ToArray());
            gearType.GetField("defaultSize").SetValue(gear, def.Size);
            gearType.GetField("scales").SetValue(gear, values.Select(value => value.Scale).ToArray());
            gearType.GetField("defaultScale").SetValue(gear, def.Scale);
        }

        private static UnityEngine.Component ButtonComponent(GameObject go) =>
            go.GetComponents<UnityEngine.Component>().First(component => IsButton(component.GetType()));

        private static Transform FindChild(Transform parent, string name)
        {
            if (parent.name == name)
                return parent;
            foreach (Transform child in parent)
                if (FindChild(child, name) is { } found)
                    return found;
            return null;
        }

        private static bool IsButton(Type type)
        {
            for (var t = type; t != null; t = t.BaseType)
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Button<>))
                    return true;
            return false;
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
                        case "width-width":
                        case "width":
                            anchorMin.x = 0; anchorMax.x = 1; break;
                        case "height-height":
                        case "height":
                            anchorMin.y = 0; anchorMax.y = 1; break;
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
            if (Pair(element, "pivot") is { } pivot)
                rt.pivot = new Vector2(pivot.x, 1 - pivot.y);
            if (Pair(element, "scale") is { } scale)
                rt.localScale = new Vector3(scale.x, scale.y, 1);
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

        private static IEnumerable<(string Id, string Member)> PageMembers(XElement controller)
        {
            var parts = controller.Attribute("pages").Value.Split(',');
            for (var i = 0; i < parts.Length; i += 2)
                yield return (parts[i], Identifier(parts[i + 1] == "" ? parts[i] : parts[i + 1]));
        }

        private static GameObject NewChild(string name, RectTransform parent, params Type[] components)
        {
            var go = new GameObject(name, components.Prepend(typeof(RectTransform)).ToArray());
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Sprite LoadSprite(string src, string packageId) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(Resolve(src, packageId)));

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

        private static string Field(string name) => $"m_{Identifier(name)}";

        private static string AssetPath(string file) =>
            $"{OutputRoot}/Assets/{Path.GetRelativePath($"{UiRoot}/assets", file).Replace('\\', '/')}";

        private static string SpritePath(Resource resource) =>
            resource.Type == "movieclip" ? Path.ChangeExtension(AssetPath(resource.File), ".png") : AssetPath(resource.File);

        private static string ScriptPath(string file) =>
            Path.ChangeExtension($"{OutputRoot}/Scripts/{Path.GetRelativePath($"{UiRoot}/assets", file).Replace('\\', '/')}", ".cs").Replace('\\', '/');

        private static byte[] FirstPng(byte[] data)
        {
            var start = Array.FindIndex(data, value => value == 0x89);
            while (data[start + 1] != 0x50 || data[start + 2] != 0x4e || data[start + 3] != 0x47)
                start = Array.FindIndex(data, start + 1, value => value == 0x89);
            var end = start;
            while (data[end] != 0x49 || data[end + 1] != 0x45 || data[end + 2] != 0x4e || data[end + 3] != 0x44)
                end++;
            end += 8;
            return data[start..end];
        }

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
