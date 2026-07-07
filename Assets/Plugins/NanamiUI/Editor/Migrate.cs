using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DG.Tweening;
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
        // 对应 UIConfig.verticalScrollBar/horizontalScrollBar（BasicsMain.Awake 里配置的默认滚动条）。
        private static readonly string VerticalScrollBar = "ScrollBar_VT";
        private static readonly string HorizontalScrollBar = "ScrollBar_HZ";
        private static readonly string[] Entries =
        {
            "Basics/scrollbars/ScrollBar_VT.xml",
            "Basics/scrollbars/ScrollBar_HZ.xml",
            "Basics/Main.xml",
            "Basics/Demo_Button.xml",
            "Basics/Demo_Image.xml",
            "Basics/Demo_Graph.xml",
            "Basics/Demo_MovieClip.xml",
            "Basics/Demo_Depth.xml",
            "Basics/Demo_Loader.xml",
            "Basics/Demo_List.xml",
            "Basics/Demo_Grid.xml",
            "Basics/Demo_Clip&Scroll.xml",
            "Basics/Demo_ProgressBar.xml",
            "Basics/Demo_Slider.xml",
            "Basics/Demo_ComboBox.xml",
            "Basics/Demo_Controller.xml",
            "Basics/Demo_Relation.xml",
            "Basics/Demo_Label.xml",
            "Basics/Demo_Popup.xml",
            "Basics/Demo_Window.xml",
            "Basics/Demo_Drag&Drop.xml",
            "Basics/Demo_Component.xml",
            "Basics/Demo_Text.xml",
            "Transition/BOSS.xml",
            "Transition/BOSS_SKILL.xml",
            "Transition/TRAP.xml",
            "Transition/GoodHit.xml",
            "Transition/PowerUp.xml",
            "Transition/PathDemo.xml",
        };
        private static readonly string[] BasicsDemoNames =
        {
            "Button",
            "Image",
            "Graph",
            "MovieClip",
            "Depth",
            "Loader",
            "List",
            "Grid",
            "Clip&Scroll",
            "ProgressBar",
            "Slider",
            "ComboBox",
            "Controller",
            "Relation",
            "Label",
            "Popup",
            "Window",
            "Drag&Drop",
            "Component",
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
            public string Texture;
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

        private class MovieClipData
        {
            public Vector2 Size;
            public float Interval;
            public float[] AddDelays;
            public int FrameCount;
        }

        private static readonly Dictionary<string, Resource> Resources = new();
        private static readonly Dictionary<Resource, MovieClipData> MovieClips = new();
        private static readonly Dictionary<Resource, BitmapFont> FontAssets = new();
        private static readonly Dictionary<Resource, AudioClip> Sounds = new();
        private static readonly Dictionary<string, GameObject> Prefabs = new();
        private static CommonSettings _settings;

        [MenuItem("Tools/Migrate %e")]
        public static void Execute()
        {
            Resources.Clear();
            MovieClips.Clear();
            FontAssets.Clear();
            Sounds.Clear();
            Prefabs.Clear();
            Text.defaultFont = "Microsoft YaHei"; // 与 BasicsMain 的 UIConfig.defaultFont 一致，保证烘焙排版与运行时一致
            foreach (var package in Directory.EnumerateFiles($"{UiRoot}/assets", "package.xml", SearchOption.AllDirectories))
                IndexPackage(package.Replace('\\', '/'));

            var components = new List<Resource>();
            var images = new HashSet<Resource>();
            foreach (var entry in Entries)
                Collect(Resources.Values.First(r => r.File == $"{UiRoot}/assets/{entry}"), components, images);

            foreach (var image in images)
                ImportImage(image);
            // 字体/声音也提前导入：构建 prefab 中途导入资产会触发批量重载，把已赋值的引用打成 null
            foreach (var component in components)
                foreach (Match match in Regex.Matches(File.ReadAllText(component.File), @"ui://\w+"))
                    if (TryResolve(match.Value, component.PackageId, out var dep))
                    {
                        if (dep.Type == "font")
                            ImportFont(dep);
                        else if (dep.Type == "sound")
                            ImportSound(dep);
                    }
            _scriptsChanged = false;
            foreach (var component in components)
                GenerateScript(component);

            AssetDatabase.Refresh();
            // 编译是异步启动的，脚本一有变化就必须等域重载后再构建 prefab，否则会用到旧类型。
            if (_scriptsChanged || EditorApplication.isCompiling)
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
            EditorApplication.delayCall += Execute;
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
                    Texture = resource.Attribute("texture")?.Value,
                };
        }

        private static void Collect(Resource component, List<Resource> components, HashSet<Resource> images)
        {
            if (components.Contains(component))
                return;

            foreach (var child in DisplayList(component.File))
            {
                var src = child.Attribute("src")?.Value ?? child.Attribute("url")?.Value;
                if (src != null && TryResolve(src, component.PackageId, out var dep))
                {
                    if (dep.Type == "component")
                        Collect(dep, components, images);
                    else if (dep.Type is "image" or "movieclip")
                        images.Add(dep);
                }
                if (child.Attribute("defaultItem") is { } defaultItem && TryResolve(defaultItem.Value, component.PackageId, out var item))
                    Collect(item, components, images);
            }
            // 覆盖 icon 属性、富文本内嵌图、动效 Sound 之外的一切 ui:// 引用
            foreach (Match match in Regex.Matches(File.ReadAllText(component.File), @"ui://\w+"))
                if (TryResolve(match.Value, component.PackageId, out var embedded) && embedded.Type is "image" or "movieclip")
                    images.Add(embedded);
            components.Add(component);
        }

        private static void ImportImage(Resource image)
        {
            if (image.Type == "movieclip")
            {
                ImportMovieClip(image);
                return;
            }

            var target = SpritePath(image);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (image.Scale == "tile")
                File.WriteAllBytes(target, TrimTransparent(File.ReadAllBytes(image.File))); // FairyGUI 图集会裁透明边，平铺单元按裁剪后尺寸
            else
                File.Copy(image.File, target, true);
            ImportSprite(target, image.Scale == "tile");

            if (image.Scale9Grid != null)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(target);
                var texture = new Texture2D(2, 2);
                texture.LoadImage(File.ReadAllBytes(image.File));
                var grid = image.Scale9Grid.Split(',').Select(int.Parse).ToArray();
                importer.spriteBorder = new Vector4(grid[0], texture.height - grid[1] - grid[3], texture.width - grid[0] - grid[2], grid[1]);
                Object.DestroyImmediate(texture);
                importer.SaveAndReimport();
            }
        }

        private static void ImportSprite(string target, bool tile = false)
        {
            AssetDatabase.ImportAsset(target);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // jta 格式："yytou" 头 + interval(ms) + 8字节保留 + 尺寸 + 标志字节 + 帧数，
        // 每帧 12 字节 (addDelay, x, y, w, h, imageIndex)，然后 int16 图片数，每张 PNG 前有 int32 长度。
        private static void ImportMovieClip(Resource movieClip)
        {
            var data = File.ReadAllBytes(movieClip.File);
            int I16(int o) => (data[o] << 8) | data[o + 1];
            int I32(int o) => (I16(o) << 16) | I16(o + 2);

            var width = I16(19);
            var height = I16(21);
            var frameCount = I32(24);
            var frames = new (int AddDelay, int X, int Y, int W, int H, int Image)[frameCount];
            var offset = 28;
            for (var i = 0; i < frameCount; i++, offset += 12)
                frames[i] = (I16(offset), I16(offset + 2), I16(offset + 4), I16(offset + 6), I16(offset + 8), I16(offset + 10));

            var imageCount = I16(offset);
            offset += 2;
            var images = new Texture2D[imageCount];
            for (var i = 0; i < imageCount; i++)
            {
                var size = I32(offset);
                offset += 4;
                images[i] = new Texture2D(2, 2);
                images[i].LoadImage(data[offset..(offset + size)]);
                offset += size;
            }

            var basePath = AssetPath(movieClip.File);
            Directory.CreateDirectory(Path.GetDirectoryName(basePath));
            for (var i = 0; i < frameCount; i++)
            {
                var frame = frames[i];
                var canvas = new Texture2D(width, height, TextureFormat.RGBA32, false);
                canvas.SetPixels32(new Color32[width * height]);
                canvas.SetPixels32(frame.X, height - frame.Y - frame.H, frame.W, frame.H, images[frame.Image].GetPixels32());
                canvas.Apply();
                File.WriteAllBytes(FramePath(basePath, i), canvas.EncodeToPNG());
                Object.DestroyImmediate(canvas);
                ImportSprite(FramePath(basePath, i));
            }
            foreach (var image in images)
                Object.DestroyImmediate(image);

            MovieClips[movieClip] = new MovieClipData
            {
                Size = new Vector2(width, height),
                Interval = I32(7) / 1000f,
                AddDelays = frames.Select(frame => frame.AddDelay / 1000f).ToArray(),
                FrameCount = frameCount,
            };
        }

        private static string FramePath(string basePath, int frame) =>
            $"{basePath[..^4]}_{frame}.png";

        private static byte[] TrimTransparent(byte[] data)
        {
            var texture = new Texture2D(2, 2);
            texture.LoadImage(data);
            var pixels = texture.GetPixels32();
            int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
            for (var y = 0; y < texture.height; y++)
                for (var x = 0; x < texture.width; x++)
                    if (pixels[y * texture.width + x].a > 0)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
            var cropped = new Texture2D(maxX - minX + 1, maxY - minY + 1, TextureFormat.RGBA32, false);
            cropped.SetPixels(texture.GetPixels(minX, minY, cropped.width, cropped.height));
            cropped.Apply();
            var result = cropped.EncodeToPNG();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(cropped);
            return result;
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

            // FairyGUI 允许 Button 组件没有 button 控制器（如 BagGridSub2），补一个占位 enum。
            if (xml.Attribute("extention")?.Value == "Button" && xml.Element("controller") == null && used.Add(Field("button")))
                fields.InsertRange(0, new[]
                {
                    "        public enum button",
                    "        {",
                    "            up,",
                    "        }",
                });
            var baseType = xml.Attribute("extention")?.Value switch
            {
                "Button" => $"NanamiUI.Button<{name}.{(xml.Element("controller") is { } buttonController ? Identifier(buttonController.Attribute("name").Value) : "button")}>",
                "ProgressBar" => "NanamiUI.ProgressBar",
                "Slider" => "NanamiUI.Slider",
                _ => "NanamiUI.Component",
            };
            var code = new StringBuilder();
            if (xml.Elements("controller").Any())
                code.AppendLine("using NanamiUI;");
            code.AppendLine("using UnityEngine;");
            code.AppendLine("using UnityEngine.UI;");
            code.AppendLine();
            code.AppendLine($"namespace UI.{component.Package}");
            code.AppendLine("{");
            code.AppendLine($"    public partial class {name} : {baseType}");
            code.AppendLine("    {");
            foreach (var line in fields)
                code.AppendLine(line);
            code.AppendLine("    }");
            code.AppendLine("}");

            WriteScript(path, code.ToString());
        }

        private static bool _scriptsChanged;

        private static void WriteScript(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path) && File.ReadAllText(path) == text)
                return;
            File.WriteAllText(path, text);
            _scriptsChanged = true;
        }

        private static void DeleteScript(string path)
        {
            if (!File.Exists(path))
                return;
            AssetDatabase.DeleteAsset(path);
            _scriptsChanged = true;
        }

        private static string FieldType(XElement child, string packageId) => child.Name.LocalName switch
        {
            "image" => "Image",
            "graph" => "NanamiUI.Shape",
            "text" or "richtext" or "inputtext" => "NanamiUI.Text",
            "loader" => "NanamiUI.Loader",
            "component" when TryResolve(child.Attribute("src").Value, packageId, out var dep) =>
                $"UI.{dep.Package}.{Identifier(Path.GetFileNameWithoutExtension(dep.File))}",
            "component" => null,
            _ when IsMovieClip(child, packageId) => "NanamiUI.MovieClip",
            _ => "RectTransform",
        };

        private static bool IsMovieClip(XElement child, string packageId) =>
            child.Attribute("src") is { } src && TryResolve(src.Value, packageId, out var resource) && resource.Type == "movieclip";

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

                var componentType = FindType($"UI.{component.Package}.{Identifier(root.name)}");
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

                if (xml.Attribute("mask") is { } mask)
                {
                    var maskGo = children.First(child => child.Xml.Attribute("id").Value == mask.Value).Go;
                    maskGo.AddComponent<Mask>().showMaskGraphic = false;
                    foreach (var (_, childGo) in children)
                        if (childGo != maskGo && childGo.transform.parent == rt)
                            childGo.transform.SetParent(maskGo.transform, true);
                }

                if (xml.Attribute("overflow")?.Value == "scroll")
                {
                    var view = BuildScrollView(root, xml, rt.sizeDelta);
                    foreach (var (_, childGo) in children)
                        if (childGo.transform.parent == rt)
                            childGo.transform.SetParent(view.Viewport, false);
                    SetGrips(view, ContentBounds(children));
                }

                var byId = new Dictionary<string, (XElement Xml, GameObject Go)>();
                foreach (var (element, go) in children)
                    byId.TryAdd(element.Attribute("id").Value, (element, go));
                foreach (var (element, go) in children)
                    foreach (var relationXml in element.Elements("relation"))
                        if (relationXml.Attribute("target").Value != "" && byId.TryGetValue(relationXml.Attribute("target").Value, out var relationTarget))
                        {
                            var relation = go.AddComponent<Relation>();
                            relation.target = (RectTransform)relationTarget.Go.transform;
                            relation.sidePairs = relationXml.Attribute("sidePair").Value.Split(',');
                            relation.Record();
                        }

                BuildTransitions(root, xml, byId, component.PackageId);

                foreach (var controller in controllers.Values)
                    BuildController(controller);
                SyncRelations(root);

                if (IsButton(comp.GetType()))
                {
                    if (controllers.TryGetValue("button", out var buttonData))
                        comp.GetType().GetField("controller").SetValue(comp, buttonData.Value);
                    comp.GetType().GetField("titleText").SetValue(comp, children.FirstOrDefault(c => c.Xml.Attribute("name").Value == "title").Go?.GetComponent<Text>());
                    comp.GetType().GetField("iconLoader").SetValue(comp, children.FirstOrDefault(c => c.Xml.Attribute("name").Value == "icon").Go?.GetComponent<Loader>());
                    ConfigureButton(root, xml.Element("Button"), component.PackageId);
                }
                else if (comp is ProgressBar progressBar)
                    SetupProgressBar(progressBar, xml, children, root);
                else if (comp is Slider slider)
                    SetupSlider(slider, xml, children, root);

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
                Prefabs[prefabPath] = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
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
                case "graph" when element.Attribute("type") == null:
                    go = NewChild(name, parent);
                    break;
                case "graph":
                {
                    go = NewChild(name, parent, typeof(Shape));
                    ConfigureShape(go.GetComponent<Shape>(), element);
                    break;
                }
                case "list":
                {
                    go = NewChild(name, parent);
                    BuildList(go, element, owner);
                    break;
                }
                case "text":
                case "richtext":
                case "inputtext":
                {
                    go = NewChild(name, parent, typeof(Text));
                    var textComponent = go.GetComponent<Text>();
                    ConfigureText(textComponent, element, owner);
                    if (element.Attribute("strokeColor") is { } stroke)
                    {
                        var effect = go.AddComponent<TextStroke>();
                        effect.color = ParseColor(stroke.Value);
                        effect.width = float.Parse(element.Attribute("strokeSize")?.Value ?? "1", CultureInfo.InvariantCulture);
                    }
                    if (element.Attribute("shadowColor") is { } shadow)
                    {
                        var effect = go.AddComponent<TextShadow>();
                        effect.color = ParseColor(shadow.Value);
                        effect.offset = Pair(element, "shadowOffset") ?? new Vector2(1, 1);
                    }
                    break;
                }
                case "loader":
                {
                    go = NewChild(name, parent, typeof(Loader));
                    var loader = go.GetComponent<Loader>();
                    loader.fill = element.Attribute("fill")?.Value switch
                    {
                        "scale" => Loader.FillType.Scale,
                        "scaleMatchHeight" => Loader.FillType.ScaleMatchHeight,
                        "scaleMatchWidth" => Loader.FillType.ScaleMatchWidth,
                        "scaleFree" => Loader.FillType.ScaleFree,
                        "scaleNoBorder" => Loader.FillType.ScaleNoBorder,
                        _ => Loader.FillType.None,
                    };
                    loader.align = element.Attribute("align")?.Value switch { "center" => 1, "right" => 2, _ => 0 };
                    loader.vAlign = element.Attribute("vAlign")?.Value switch { "middle" => 1, "bottom" => 2, _ => 0 };
                    var url = element.Attribute("url")?.Value;
                    if (url != null && TryResolve(url, owner.PackageId, out var content) && content.Type == "image")
                    {
                        loader.sprite = LoadSprite(url, owner.PackageId);
                        ConfigureImage(loader, content, element);
                    }
                    else if (url != null && TryResolve(url, owner.PackageId, out content) && content.Type == "movieclip")
                        ConfigureMovieClip(loader, content, element);
                    else
                        loader.enabled = false;
                    break;
                }
                case "component":
                {
                    var dep = Resolve(element.Attribute("src").Value, owner.PackageId);
                    go = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(dep), parent);
                    go.name = name;
                    size ??= ((RectTransform)go.transform).sizeDelta;
                    if (element.Element("Button") is { } button)
                        ConfigureButton(go, button, owner.PackageId);
                    if (element.Element("Label") is { } label)
                        ConfigureLabel(go, label, owner.PackageId);
                    if (element.Element("ProgressBar") is { } progress)
                        ConfigureProgressBar(go, progress);
                    if (element.Element("Slider") is { } slider)
                        ConfigureSlider(go, slider);
                    if (element.Element("ComboBox") is { } comboBox)
                        ConfigureComboBox(go, comboBox);
                    break;
                }
                default:
                {
                    if (IsMovieClip(element, owner.PackageId))
                    {
                        go = NewChild(name, parent, typeof(MovieClip));
                        var movieClip = go.GetComponent<MovieClip>();
                        ConfigureMovieClip(movieClip, Resolve(element.Attribute("src").Value, owner.PackageId), element);
                        size ??= MovieClips[Resolve(element.Attribute("src").Value, owner.PackageId)].Size;
                    }
                    else
                        go = NewChild(name, parent);
                    break;
                }
            }

            SetRect((RectTransform)go.transform, element, size ?? Vector2.zero, parent);
            if (go.GetComponent<Text>() is { } createdText)
                createdText.RebuildImages(); // 依赖最终 rect 的排版，须在 SetRect 之后烘焙
            ApplyElement(go, element);
            if (element.Attribute("visible")?.Value == "false")
                go.SetActive(false);

            object display = null, display2 = null;
            foreach (var gearXml in element.Elements())
            {
                var kind = gearXml.Name.LocalName;
                if (kind is not ("gearDisplay" or "gearDisplay2" or "gearXY" or "gearColor" or "gearLook" or "gearSize" or "gearAni" or "gearFontSize"))
                    continue;

                var controller = controllers[gearXml.Attribute("controller").Value];
                var gearType = (kind switch
                {
                    "gearDisplay" or "gearDisplay2" => typeof(GearDisplay<>),
                    "gearXY" => typeof(GearXY<>),
                    "gearColor" => typeof(GearColor<>),
                    "gearLook" => typeof(GearLook<>),
                    "gearAni" => typeof(GearAni<>),
                    "gearFontSize" => typeof(GearFontSize<>),
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
                        var parts = pair.Split(',');
                        return rt.anchoredPosition + new Vector2(
                            float.Parse(parts[0], CultureInfo.InvariantCulture) - origin.x,
                            origin.y - float.Parse(parts[1], CultureInfo.InvariantCulture));
                    }
                    var defaultValue = gearXml.Attribute("default") is { } def ? Convert(def.Value) : rt.anchoredPosition;
                    gearType.GetField("values").SetValue(gear, gearXml.Attribute("values").Value.Split('|').Select(value => value == "-" ? defaultValue : Convert(value)).ToArray());
                    gearType.GetField("defaultValue").SetValue(gear, defaultValue);
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
                else if (kind == "gearAni")
                    ConfigureGearAni(gearType, gear, gearXml);
                else if (kind == "gearFontSize")
                {
                    var def = int.Parse(gearXml.Attribute("default")?.Value ?? go.GetComponent<Text>().fontSize.ToString());
                    gearType.GetField("values").SetValue(gear, gearXml.Attribute("values").Value.Split('|').Select(value => value == "-" ? def : int.Parse(value)).ToArray());
                    gearType.GetField("defaultValue").SetValue(gear, def);
                }
                else if (kind == "gearDisplay")
                    display = gear;
                else if (kind == "gearDisplay2")
                {
                    display2 = gear;
                    gearType.GetField("condition").SetValue(gear, int.Parse(gearXml.Attribute("condition")?.Value ?? "0"));
                }
                controller.Gears.Add(gear);
            }

            if (display != null && display2 != null)
            {
                display.GetType().GetField("partner").SetValue(display, display2);
                display2.GetType().GetField("partner").SetValue(display2, display);
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
            if (element.Attribute("fillMethod") is { } fillMethod)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = fillMethod.Value switch
                {
                    "hz" => Image.FillMethod.Horizontal,
                    "vt" => Image.FillMethod.Vertical,
                    "radial90" => Image.FillMethod.Radial90,
                    "radial180" => Image.FillMethod.Radial180,
                    _ => Image.FillMethod.Radial360,
                };
                if (image.fillMethod == Image.FillMethod.Radial360)
                    image.fillOrigin = (int)Image.Origin360.Top;
                image.fillClockwise = element.Attribute("fillClockwise")?.Value != "false";
                image.fillAmount = float.Parse(element.Attribute("fillAmount")?.Value ?? "100", CultureInfo.InvariantCulture) / 100;
            }
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

        private static void ConfigureShape(Shape shape, XElement element)
        {
            shape.lineSize = float.Parse(element.Attribute("lineSize")?.Value ?? "1", CultureInfo.InvariantCulture);
            shape.lineColor = ParseColor(element.Attribute("lineColor")?.Value ?? "#ff000000");
            shape.color = ParseColor(element.Attribute("fillColor")?.Value ?? "#ffffffff");
            if (element.Attribute("skew") is { } skew)
                shape.skew = (Vector2)Pair(element, "skew");
            switch (element.Attribute("type")?.Value)
            {
                case "eclipse":
                    shape.kind = Shape.Kind.Ellipse;
                    break;
                case "polygon":
                    shape.kind = Shape.Kind.Polygon;
                    var values = element.Attribute("points").Value.Split(',').Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                    shape.points = Enumerable.Range(0, values.Length / 2).Select(i => new Vector2(values[i * 2], values[i * 2 + 1])).ToArray();
                    break;
                case "regular_polygon":
                    shape.kind = Shape.Kind.RegularPolygon;
                    shape.sides = int.Parse(element.Attribute("sides").Value);
                    shape.startAngle = float.Parse(element.Attribute("startAngle")?.Value ?? "0", CultureInfo.InvariantCulture);
                    shape.distances = element.Attribute("distances")?.Value.Split(',').Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                    break;
                default:
                    if (element.Attribute("corner") is { } corner)
                    {
                        shape.kind = Shape.Kind.RoundedRect;
                        var radii = corner.Value.Split(',').Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                        shape.corners = Enumerable.Range(0, 4).Select(i => radii[Mathf.Min(i, radii.Length - 1)]).ToArray();
                    }
                    else
                        shape.kind = Shape.Kind.Rect;
                    break;
            }
        }

        private static void ConfigureMovieClip(MovieClip movieClip, Resource resource, XElement element)
        {
            var data = MovieClips[resource];
            var basePath = AssetPath(resource.File);
            movieClip.frames = Enumerable.Range(0, data.FrameCount)
                .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(FramePath(basePath, i)))
                .ToArray();
            movieClip.interval = data.Interval;
            movieClip.addDelays = data.AddDelays;
            movieClip.playing = element.Attribute("playing")?.Value != "false";
            movieClip.frame = int.Parse(element.Attribute("frame")?.Value ?? "0");
            movieClip.sprite = movieClip.frames[movieClip.frame];
            movieClip.type = Image.Type.Simple;
            movieClip.color = ParseColor(element.Attribute("color")?.Value ?? "#ffffffff");
        }

        private static void SetupProgressBar(ProgressBar progress, XElement xml, List<(XElement Xml, GameObject Go)> children, GameObject root)
        {
            var ext = xml.Element("ProgressBar");
            progress.titleType = ParseTitleType(ext.Attribute("titleType")?.Value);
            progress.reverse = ext.Attribute("reverse")?.Value == "true";
            progress.title = Child(children, "title")?.GetComponent<Text>();
            var size = (Vector2)Pair(xml, "size");
            if (Child(children, "bar") is { } bar)
            {
                progress.bar = (RectTransform)bar.transform;
                progress.barMaxWidthDelta = size.x - progress.bar.rect.width;
                progress.barStartX = progress.bar.anchoredPosition.x;
            }
            if (Child(children, "bar_v") is { } barV)
            {
                progress.barV = (RectTransform)barV.transform;
                progress.barMaxHeightDelta = size.y - progress.barV.rect.height;
                progress.barStartY = -progress.barV.anchoredPosition.y;
            }
            if (Child(children, "ani") is { } ani && ani.GetComponent<MovieClip>() is { } movieClip && movieClip is not Loader)
                progress.ani = movieClip;
            progress.Apply();
            SyncRelations(root);
        }

        private static void SetupSlider(Slider slider, XElement xml, List<(XElement Xml, GameObject Go)> children, GameObject root)
        {
            var ext = xml.Element("Slider");
            slider.titleType = ParseTitleType(ext.Attribute("titleType")?.Value);
            slider.title = Child(children, "title")?.GetComponent<Text>();
            var size = (Vector2)Pair(xml, "size");
            if (Child(children, "bar") is { } bar)
            {
                slider.bar = (RectTransform)bar.transform;
                slider.barMaxWidthDelta = size.x - slider.bar.rect.width;
            }
            if (Child(children, "bar_v") is { } barV)
            {
                slider.barV = (RectTransform)barV.transform;
                slider.barMaxHeightDelta = size.y - slider.barV.rect.height;
            }
            if (Child(children, "grip") is { } grip)
                slider.grip = (RectTransform)grip.transform;
            slider.Apply();
            SyncRelations(root);
        }

        private static ProgressTitleType ParseTitleType(string value) => value?.ToLowerInvariant() switch
        {
            "valueandmax" => ProgressTitleType.ValueAndMax,
            "value" => ProgressTitleType.Value,
            "max" => ProgressTitleType.Max,
            _ => ProgressTitleType.Percent,
        };

        private static GameObject Child(List<(XElement Xml, GameObject Go)> children, string name) =>
            children.FirstOrDefault(child => child.Xml.Attribute("name").Value == name).Go;

        private static void ConfigureProgressBar(GameObject go, XElement xml)
        {
            var progress = go.GetComponent<ProgressBar>();
            progress.value = float.Parse(xml.Attribute("value")?.Value ?? "50", CultureInfo.InvariantCulture);
            progress.max = float.Parse(xml.Attribute("max")?.Value ?? "100", CultureInfo.InvariantCulture);
            progress.Apply();
            SyncRelations(go);
        }

        private static void ConfigureSlider(GameObject go, XElement xml)
        {
            var slider = go.GetComponent<Slider>();
            slider.value = float.Parse(xml.Attribute("value")?.Value ?? "50", CultureInfo.InvariantCulture);
            slider.max = float.Parse(xml.Attribute("max")?.Value ?? "100", CultureInfo.InvariantCulture);
            slider.Apply();
            SyncRelations(go);
        }

        private static void ConfigureComboBox(GameObject go, XElement xml)
        {
            if (xml.Element("item") is { } item && FindChild(go.transform, "title")?.GetComponent<Text>() is { } title)
                title.text = item.Attribute("title")?.Value ?? "";
        }

        private static void SyncRelations(GameObject go)
        {
            foreach (var relation in go.GetComponentsInChildren<Relation>(true))
                relation.Sync();
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
                var button = go.GetComponents<UnityEngine.Component>().FirstOrDefault(component => IsButton(component.GetType()));
                // FairyGUI 约定：存在名为 grayed 的控制器时，置灰只切换其页面 1，不套灰度材质。
                if (button?.GetType().GetField("m_grayed") is { } grayedController)
                {
                    var controller = grayedController.GetValue(button);
                    var pageProperty = controller.GetType().GetProperty("page");
                    pageProperty.SetValue(controller, Enum.GetValues(pageProperty.PropertyType).GetValue(1));
                    grayedController.SetValue(button, controller);
                }
                else
                {
                    go.AddComponent<Grayed>();
                    if (button != null)
                    {
                        button.GetType().GetField("grayed").SetValue(button, true);
                        button.GetType().GetMethod("RefreshState").Invoke(button, null);
                    }
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

        private class ScrollView
        {
            public RectTransform Viewport;
            public GameObject VtBar;
            public GameObject HzBar;
            public Vector2 ViewportSize; // 设计态 viewport 尺寸：拉伸锚点后 sizeDelta/rect 不可靠，布局与 grip 用它
        }

        // 复刻 ScrollPane 静态布局：viewport 缩进 margin 与滚动条宽度，滚动条常显，grip 长度按显示比例。
        private static ScrollView BuildScrollView(GameObject root, XElement element, Vector2 size)
        {
            var margin = ParseMargin(element.Attribute("margin")?.Value);
            var barMargin = ParseMargin(element.Attribute("scrollBarMargin")?.Value);
            var scroll = element.Attribute("scroll")?.Value ?? "vertical";
            var hideBars = element.Attribute("scrollBar")?.Value == "hidden";
            // scrollBarFlags bit0 = displayOnLeft：竖直滚动条放左侧、内容从左内缩（FairyGUI ScrollPane._displayOnLeft）。
            var displayOnLeft = (int.Parse(element.Attribute("scrollBarFlags")?.Value ?? "0") & 1) != 0;
            var vtBar = !hideBars && scroll is "vertical" or "both" && VerticalScrollBar != null ? InstantiateComponent(VerticalScrollBar, root.transform) : null;
            var hzBar = !hideBars && scroll is "horizontal" or "both" && HorizontalScrollBar != null ? InstantiateComponent(HorizontalScrollBar, root.transform) : null;
            var vtWidth = vtBar != null ? ((RectTransform)vtBar.transform).sizeDelta.x : 0;
            var hzHeight = hzBar != null ? ((RectTransform)hzBar.transform).sizeDelta.y : 0;
            var leftInset = displayOnLeft ? vtWidth : 0;
            var rightInset = displayOnLeft ? 0 : vtWidth;

            // viewport 与滚动条一律用拉伸锚点：组件被以非设计尺寸实例化时（如 Clip&Scroll 里 425 设计→387 实例的
            // 横向面板）滚动几何随根尺寸自适应。设计尺寸下与固定锚点逐像素等价。
            var viewport = NewChild("viewport", (RectTransform)root.transform, typeof(RectMask2D));
            var viewportRt = (RectTransform)viewport.transform;
            viewportRt.pivot = new Vector2(0, 1);
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = new Vector2(margin.Left + leftInset, margin.Bottom + hzHeight);
            viewportRt.offsetMax = new Vector2(-(margin.Right + rightInset), -margin.Top);
            viewport.transform.SetSiblingIndex(0);
            if (element.Attribute("clipSoftness") is { } softness)
            {
                var parts = softness.Value.Split(',');
                viewport.GetComponent<RectMask2D>().softness = new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
            }

            if (vtBar != null)
            {
                var barRt = (RectTransform)vtBar.transform;
                barRt.anchorMin = new Vector2(displayOnLeft ? 0 : 1, 0);
                barRt.anchorMax = new Vector2(displayOnLeft ? 0 : 1, 1);
                barRt.offsetMin = new Vector2(displayOnLeft ? 0 : -vtWidth, barMargin.Bottom + hzHeight);
                barRt.offsetMax = new Vector2(displayOnLeft ? vtWidth : 0, -barMargin.Top);
            }
            if (hzBar != null)
            {
                var barRt = (RectTransform)hzBar.transform;
                barRt.anchorMin = new Vector2(0, 0);
                barRt.anchorMax = new Vector2(1, 0);
                barRt.offsetMin = new Vector2(barMargin.Left + leftInset, 0);
                barRt.offsetMax = new Vector2(-(barMargin.Right + rightInset), hzHeight);
            }

            return new ScrollView
            {
                Viewport = viewportRt, VtBar = vtBar, HzBar = hzBar,
                ViewportSize = new Vector2(size.x - margin.Left - margin.Right - vtWidth, size.y - margin.Top - margin.Bottom - hzHeight),
            };
        }

        private static void SetGrips(ScrollView view, Vector2 content)
        {
            var viewSize = view.ViewportSize;
            if (view.VtBar != null)
            {
                var bar = (RectTransform)FindChild(view.VtBar.transform, "bar");
                var grip = (RectTransform)FindChild(view.VtBar.transform, "grip");
                grip.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.FloorToInt(Mathf.Min(1, viewSize.y / Mathf.Max(content.y, 1)) * bar.rect.height));
                grip.anchoredPosition = new Vector2(grip.anchoredPosition.x, Relation.TopLeft(bar).y);
            }
            if (view.HzBar != null)
            {
                var bar = (RectTransform)FindChild(view.HzBar.transform, "bar");
                var grip = (RectTransform)FindChild(view.HzBar.transform, "grip");
                grip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.FloorToInt(Mathf.Min(1, viewSize.x / Mathf.Max(content.x, 1)) * bar.rect.width));
                grip.anchoredPosition = new Vector2(Relation.TopLeft(bar).x, grip.anchoredPosition.y);
            }
        }

        private static Vector2 ContentBounds(List<(XElement Xml, GameObject Go)> children)
        {
            var bounds = Vector2.zero;
            foreach (var (_, go) in children)
            {
                var rt = (RectTransform)go.transform;
                var topLeft = Relation.TopLeft(rt);
                bounds = Vector2.Max(bounds, new Vector2(topLeft.x + rt.rect.width, rt.rect.height - topLeft.y));
            }
            return bounds;
        }

        private static void BuildList(GameObject go, XElement element, Resource owner)
        {
            var size = (Vector2)Pair(element, "size");
            var prefab = LoadPrefab(Resolve(element.Attribute("defaultItem").Value, owner.PackageId));
            var itemSize = ((RectTransform)prefab.transform).sizeDelta;
            var lineGap = float.Parse(element.Attribute("lineGap")?.Value ?? "0", CultureInfo.InvariantCulture);
            var colGap = float.Parse(element.Attribute("colGap")?.Value ?? "0", CultureInfo.InvariantCulture);
            var layout = element.Attribute("layout")?.Value ?? "column";

            var view = element.Attribute("overflow")?.Value == "scroll"
                ? BuildScrollView(go, element, size)
                : new ScrollView { Viewport = (RectTransform)go.transform };
            var viewSize = view.Viewport == go.transform ? size : view.ViewportSize;

            var positions = new List<Vector2>();
            float x = 0, y = 0;
            foreach (var _ in element.Elements("item"))
                switch (layout)
                {
                    case "row":
                        positions.Add(new Vector2(x, 0));
                        x += itemSize.x + colGap;
                        break;
                    case "flow_hz":
                        if (x != 0 && x + itemSize.x > viewSize.x)
                        {
                            x = 0;
                            y += itemSize.y + lineGap;
                        }
                        positions.Add(new Vector2(x, y));
                        x += itemSize.x + colGap;
                        break;
                    case "flow_vt":
                        if (y != 0 && y + itemSize.y > viewSize.y)
                        {
                            y = 0;
                            x += itemSize.x + colGap;
                        }
                        positions.Add(new Vector2(x, y));
                        y += itemSize.y + lineGap;
                        break;
                    default:
                        positions.Add(new Vector2(0, y));
                        y += itemSize.y + lineGap;
                        break;
                }

            var content = Vector2.zero;
            var index = 0;
            foreach (var item in element.Elements("item"))
            {
                var position = positions[index++];
                content = Vector2.Max(content, position + itemSize);
                var itemGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, view.Viewport);
                ((RectTransform)itemGo.transform).anchoredPosition = new Vector2(position.x, -position.y);
                ConfigureButton(itemGo, item, owner.PackageId);
            }
            SetGrips(view, content);
        }

        private static GameObject InstantiateComponent(string componentName, Transform parent)
        {
            var resource = Resources.Values.First(r => Path.GetFileNameWithoutExtension(r.File) == componentName);
            return (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(resource), parent);
        }

        private static GameObject LoadPrefab(Resource component)
        {
            var path = Path.ChangeExtension(AssetPath(component.File), ".prefab");
            return Prefabs.TryGetValue(path, out var prefab) && prefab != null
                ? prefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static (float Top, float Bottom, float Left, float Right) ParseMargin(string value)
        {
            if (value == null)
                return (0, 0, 0, 0);
            var parts = value.Split(',').Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
            return (parts[0], parts[1], parts[2], parts[3]);
        }

        // 动效：XML 时间/时长单位是 24fps 帧；值里 "-" 表示保持当前（NaN）。
        private static void BuildTransitions(GameObject root, XElement xml, Dictionary<string, (XElement Xml, GameObject Go)> byId, string packageId)
        {
            foreach (var transitionXml in xml.Elements("transition"))
            {
                var transition = root.AddComponent<Transition>();
                transition.transitionName = transitionXml.Attribute("name").Value;
                transition.autoPlay = transitionXml.Attribute("autoPlay")?.Value == "true";
                transition.autoPlayTimes = int.Parse(transitionXml.Attribute("autoPlayRepeat")?.Value ?? "1");
                transition.autoPlayDelay = float.Parse(transitionXml.Attribute("autoPlayDelay")?.Value ?? "0", CultureInfo.InvariantCulture);

                var items = new List<TransitionItem>();
                foreach (var itemXml in transitionXml.Elements("item"))
                {
                    var targetId = itemXml.Attribute("target")?.Value ?? "";
                    (XElement Xml, GameObject Go) target = default;
                    if (targetId != "" && !byId.TryGetValue(targetId, out target))
                        continue; // 编辑器遗留的失效目标
                    var type = ParseTransitionType(itemXml.Attribute("type").Value);
                    if (type == null)
                        continue;

                    var item = new TransitionItem
                    {
                        time = int.Parse(itemXml.Attribute("time").Value) / 24f,
                        target = target.Go != null ? (RectTransform)target.Go.transform : null,
                        type = type.Value,
                        tween = itemXml.Attribute("tween")?.Value == "true",
                        duration = int.Parse(itemXml.Attribute("duration")?.Value ?? "0") / 24f,
                        ease = ParseEase(itemXml.Attribute("ease")?.Value),
                        repeat = int.Parse(itemXml.Attribute("repeat")?.Value ?? "0"),
                        yoyo = itemXml.Attribute("yoyo")?.Value == "true",
                    };

                    if (item.tween)
                    {
                        item.start = ParseTransitionValues(itemXml.Attribute("startValue")?.Value, item.type);
                        item.end = ParseTransitionValues(itemXml.Attribute("endValue")?.Value, item.type);
                    }
                    else
                    {
                        var value = itemXml.Attribute("value")?.Value;
                        switch (item.type)
                        {
                            case TransitionItemType.Sound:
                                if (value != null && TryResolve(value, packageId, out var soundResource))
                                    item.sound = ImportSound(soundResource);
                                break;
                            case TransitionItemType.Nested:
                                item.stringValue = value.Split(',')[0];
                                break;
                            case TransitionItemType.Text:
                                item.stringValue = value;
                                break;
                            default:
                                item.start = ParseTransitionValues(value, item.type);
                                break;
                        }
                    }

                    if (item.type == TransitionItemType.XY)
                    {
                        var rt = target.Go != null ? (RectTransform)target.Go.transform : (RectTransform)root.transform;
                        var designXY = target.Xml != null ? Pair(target.Xml, "xy") ?? Vector2.zero : Vector2.zero;
                        item.positionOffset = rt.anchoredPosition - new Vector2(designXY.x, -designXY.y);
                        if (itemXml.Attribute("path") is { } path)
                            item.pathData = path.Value.Split(',').Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                    }

                    items.Add(item);
                }
                transition.items = items.ToArray();
            }
        }

        private static TransitionItemType? ParseTransitionType(string value) => value switch
        {
            "XY" => TransitionItemType.XY,
            "Size" => TransitionItemType.Size,
            "Scale" => TransitionItemType.Scale,
            "Pivot" => TransitionItemType.Pivot,
            "Alpha" => TransitionItemType.Alpha,
            "Rotation" => TransitionItemType.Rotation,
            "Color" => TransitionItemType.Color,
            "Animation" => TransitionItemType.Animation,
            "Visible" => TransitionItemType.Visible,
            "Sound" => TransitionItemType.Sound,
            "Transition" => TransitionItemType.Nested,
            "Shake" => TransitionItemType.Shake,
            "ColorFilter" => TransitionItemType.ColorFilter,
            "Text" => TransitionItemType.Text,
            _ => null,
        };

        private static float[] ParseTransitionValues(string value, TransitionItemType type)
        {
            switch (type)
            {
                case TransitionItemType.Visible:
                    return new[] { value == "true" ? 1f : 0f };
                case TransitionItemType.Animation:
                {
                    var parts = value.Split(',');
                    return new[] { parts[0] == "-" ? -1f : float.Parse(parts[0], CultureInfo.InvariantCulture), parts.Length > 1 && parts[1] == "p" ? 1f : 0f };
                }
                case TransitionItemType.Color:
                {
                    var color = ParseColor(value);
                    return new[] { color.r, color.g, color.b, color.a };
                }
                default:
                    return value.Split(',')
                        .Select(part => part == "-" ? float.NaN : float.Parse(part, CultureInfo.InvariantCulture))
                        .ToArray();
            }
        }

        // FairyGUI ease 名 "Expo.Out" → DOTween Ease.OutExpo
        private static Ease ParseEase(string value)
        {
            if (value == null)
                return Ease.OutQuad;
            if (value == "Linear")
                return Ease.Linear;
            var parts = value.Split('.');
            return (Ease)Enum.Parse(typeof(Ease), parts[1] + parts[0]);
        }

        private static AudioClip ImportSound(Resource resource)
        {
            if (Sounds.TryGetValue(resource, out var clip))
                return clip;
            var target = AssetPath(resource.File);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(resource.File, target, true);
            AssetDatabase.ImportAsset(target);
            return Sounds[resource] = AssetDatabase.LoadAssetAtPath<AudioClip>(target);
        }

        private static void ConfigureGearAni(Type gearType, object gear, XElement xml)
        {
            (int Frame, bool Playing) Parse(string value, (int Frame, bool Playing) def)
            {
                if (value == "-")
                    return def;
                var parts = value.Split(',');
                return (int.Parse(parts[0]), parts[1] == "p");
            }

            var def = Parse(xml.Attribute("default")?.Value ?? "0,p", (0, true));
            var values = xml.Attribute("values").Value.Split('|').Select(value => Parse(value, def)).ToArray();
            gearType.GetField("frames").SetValue(gear, values.Select(value => value.Frame).ToArray());
            gearType.GetField("defaultFrame").SetValue(gear, def.Frame);
            gearType.GetField("playings").SetValue(gear, values.Select(value => value.Playing).ToArray());
            gearType.GetField("defaultPlaying").SetValue(gear, def.Playing);
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
            if (element.Attribute("anchor")?.Value == "true" && Pair(element, "pivot") is { } anchorPivot)
                xy -= anchorPivot * size;
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
            {
                // pivot setter 保持 anchoredPosition，需要补偿位移以保持 rect 不动。
                var newPivot = new Vector2(pivot.x, 1 - pivot.y);
                var delta = newPivot - rt.pivot;
                rt.pivot = newPivot;
                rt.anchoredPosition += new Vector2(delta.x * size.x, delta.y * size.y);
            }
            if (Pair(element, "scale") is { } scale)
                rt.localScale = new Vector3(scale.x, scale.y, 1);
        }

        private static void ConfigureText(Text text, XElement element, Resource owner)
        {
            text.text = element.Attribute("text")?.Value ?? "";
            text.fontSize = int.Parse(element.Attribute("fontSize")?.Value ?? Settings().fontSize.ToString());
            text.leading = int.Parse(element.Attribute("leading")?.Value ?? "3");
            text.color = ParseColor(element.Attribute("color")?.Value ?? Settings().textColor);
            text.supportRichText = false;
            text.html = element.Name.LocalName == "richtext";
            text.ubb = element.Attribute("ubb")?.Value == "true";
            text.underlined = element.Attribute("underline")?.Value == "true";
            if (text.text == "" && element.Attribute("prompt") is { } prompt)
            {
                text.text = prompt.Value;
                text.ubb = true;
            }
            text.imageSprites = Regex.Matches(text.text, @"ui://\w+")
                .Select(match => LoadSprite(match.Value, owner.PackageId))
                .ToArray();
            var bold = element.Attribute("bold")?.Value == "true";
            var italic = element.Attribute("italic")?.Value == "true";
            text.fontStyle = bold && italic ? FontStyle.BoldAndItalic : bold ? FontStyle.Bold : italic ? FontStyle.Italic : FontStyle.Normal;
            var autoSize = element.Attribute("autoSize")?.Value ?? "both";
            text.horizontalOverflow = autoSize == "both" || element.Attribute("singleLine")?.Value == "true"
                ? HorizontalWrapMode.Overflow
                : HorizontalWrapMode.Wrap;
            text.verticalOverflow = autoSize == "none" ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
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
            if (element.Attribute("font") is { } font)
            {
                if (TryResolve(font.Value, owner.PackageId, out var fontResource) && fontResource.Type == "font")
                    text.bitmapFont = ImportFont(fontResource);
                else
                    text.fontNames = font.Value;
            }
        }

        // 解析 FairyGUI 编辑器工程里的 .fnt：标准 BMFont（atlas 坐标 + texture 属性指向图集）
        // 或 UIBuilder 逐字图片格式（img=资源id，打包成横向图集）。
        private static BitmapFont ImportFont(Resource fontResource)
        {
            if (FontAssets.TryGetValue(fontResource, out var existing))
                return existing;

            var glyphs = new List<BitmapFont.Glyph>();
            var size = 0;
            float scaleW = 0, scaleH = 0;
            var charLines = new List<Dictionary<string, string>>();
            foreach (var line in File.ReadAllLines(fontResource.File))
            {
                var attrs = ParseFntLine(line);
                if (line.StartsWith("info") && attrs.TryGetValue("size", out var infoSize))
                    size = int.Parse(infoSize);
                else if (line.StartsWith("common"))
                {
                    scaleW = float.Parse(attrs.GetValueOrDefault("scaleW", "0"), CultureInfo.InvariantCulture);
                    scaleH = float.Parse(attrs.GetValueOrDefault("scaleH", "0"), CultureInfo.InvariantCulture);
                }
                else if (line.StartsWith("char "))
                    charLines.Add(attrs);
            }

            string texturePath;
            var uvRects = new Rect[charLines.Count];
            var sizes = new Vector2[charLines.Count];
            if (fontResource.Texture != null)
            {
                var atlas = Resources[fontResource.PackageId + fontResource.Texture];
                texturePath = AssetPath(atlas.File);
                Directory.CreateDirectory(Path.GetDirectoryName(texturePath));
                File.Copy(atlas.File, texturePath, true);
                ImportSprite(texturePath);
                for (var i = 0; i < charLines.Count; i++)
                {
                    var attrs = charLines[i];
                    float x = int.Parse(attrs["x"]), y = int.Parse(attrs["y"]), w = int.Parse(attrs["width"]), h = int.Parse(attrs["height"]);
                    uvRects[i] = new Rect(x / scaleW, 1 - (y + h) / scaleH, w / scaleW, h / scaleH);
                    sizes[i] = new Vector2(w, h);
                }
            }
            else
            {
                var textures = charLines
                    .Select(attrs => Resources[fontResource.PackageId + attrs["img"]])
                    .Select(image =>
                    {
                        var texture = new Texture2D(2, 2);
                        texture.LoadImage(File.ReadAllBytes(image.File));
                        return texture;
                    })
                    .ToArray();
                var width = textures.Sum(texture => texture.width + 1);
                var height = textures.Max(texture => texture.height);
                var canvas = new Texture2D(width, height, TextureFormat.RGBA32, false);
                canvas.SetPixels32(new Color32[width * height]);
                var x = 0;
                for (var i = 0; i < textures.Length; i++)
                {
                    canvas.SetPixels32(x, height - textures[i].height, textures[i].width, textures[i].height, textures[i].GetPixels32());
                    uvRects[i] = new Rect((float)x / width, (float)(height - textures[i].height) / height, (float)textures[i].width / width, (float)textures[i].height / height);
                    sizes[i] = new Vector2(textures[i].width, textures[i].height);
                    x += textures[i].width + 1;
                    Object.DestroyImmediate(textures[i]);
                }
                canvas.Apply();
                texturePath = Path.ChangeExtension(AssetPath(fontResource.File), ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(texturePath));
                File.WriteAllBytes(texturePath, canvas.EncodeToPNG());
                Object.DestroyImmediate(canvas);
                ImportSprite(texturePath);
            }

            for (var i = 0; i < charLines.Count; i++)
            {
                var attrs = charLines[i];
                var offsetY = int.Parse(attrs.GetValueOrDefault("yoffset", "0"));
                var glyphHeight = sizes[i].y;
                if (size == 0)
                    size = (int)glyphHeight;
                var advance = int.Parse(attrs.GetValueOrDefault("xadvance", "0"));
                var offsetX = int.Parse(attrs.GetValueOrDefault("xoffset", "0"));
                glyphs.Add(new BitmapFont.Glyph
                {
                    code = int.Parse(attrs["id"]),
                    uv = uvRects[i],
                    x = offsetX,
                    y = offsetY,
                    width = sizes[i].x,
                    height = glyphHeight,
                    advance = advance == 0 ? offsetX + (int)sizes[i].x : advance,
                    lineHeight = Mathf.Max(size, offsetY < 0 ? (int)glyphHeight : offsetY + (int)glyphHeight),
                });
            }

            // 已有资产原地更新：CreateAsset 覆盖会换对象身份，后续导入批次会把引用打成 null
            var assetPath = Path.ChangeExtension(AssetPath(fontResource.File), ".asset");
            var asset = AssetDatabase.LoadAssetAtPath<BitmapFont>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BitmapFont>();
                AssetDatabase.CreateAsset(asset, assetPath);
                asset = AssetDatabase.LoadAssetAtPath<BitmapFont>(assetPath);
            }
            asset.size = size;
            asset.canTint = fontResource.Texture != null; // 标准 BMFont 可着色，逐字图片字体保留原色
            asset.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            asset.glyphs = glyphs.ToArray();
            EditorUtility.SetDirty(asset);
            return FontAssets[fontResource] = asset;
        }

        private static Dictionary<string, string> ParseFntLine(string line)
        {
            var attrs = new Dictionary<string, string>();
            foreach (Match match in Regex.Matches(line, "(\\w+)=(\"[^\"]*\"|\\S*)"))
                attrs[match.Groups[1].Value] = match.Groups[2].Value.Trim('"');
            return attrs;
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
            // GameObject 构造器不处理 RequireComponent，Graphic 需要显式补 CanvasRenderer。
            var types = components.Length == 0
                ? new[] { typeof(RectTransform) }
                : components.Prepend(typeof(CanvasRenderer)).Prepend(typeof(RectTransform)).ToArray();
            var go = new GameObject(name, types);
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
            resource.Type == "movieclip" ? FramePath(AssetPath(resource.File), 0) : AssetPath(resource.File);

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
