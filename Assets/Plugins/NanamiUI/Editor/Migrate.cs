using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DG.Tweening;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Schema = NanamiUI.Editor.Schema;

namespace NanamiUI.Editor
{
    // 标记在 Migrate 完成后自动执行的静态无参方法（工程特有收尾，不污染通用导出器）。
    [AttributeUsage(AttributeTargets.Method)]
    public class MigratePostProcessAttribute : Attribute
    {
    }

    public static class Migrate
    {
        private const string UiRoot = "UIProject";
        private static readonly string OutputRoot = $"Assets/{Path.GetFileName(UiRoot)}";
        private const string Pending = "NanamiUI.Migrate.Pending";

        private class Resource
        {
            public Schema.ResourceKind Type;
            public string File;
            public string Package;
            public string PackageId;
            public Schema.ImageScale Scale;
            public string Scale9Grid;
            public string Texture;
            public bool Exported;
            private Schema.Component _componentXml;

            public Schema.Component ComponentXml => _componentXml ??= Schema.Component.Load(File);
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

        [MenuItem("Tools/NanamiUI/Migrate %e")]
        public static void Execute()
        {
            Resources.Clear();
            MovieClips.Clear();
            FontAssets.Clear();
            Sounds.Clear();
            Prefabs.Clear();
            foreach (var package in Directory.EnumerateFiles($"{UiRoot}/assets", "package.xml", SearchOption.AllDirectories))
                IndexPackage(package.Replace('\\', '/'));

            TextField.defaultFont = DefaultFont(); // 游戏 UIConfig.defaultFont：与运行时一致才能保证烘焙排版一致

            var components = new List<Resource>();
            var images = new HashSet<Resource>();
            // 默认滚动条是运行时 UIConfig 注入，任何 displayList 都不引用它，必须显式补建。
            foreach (var bar in new[] { Settings().scrollBars?.vertical, Settings().scrollBars?.horizontal })
                if (!string.IsNullOrEmpty(bar) && TryResolve(bar, null, out var barRes))
                    Collect(barRes, components, images);
            // 导出 exported 组件（可用 NanamiUISettings.packages 限定包名，留空则全部）；
            // Collect 会按依赖顺序把它们引用到的（可能未导出、可能跨包的）组件一并带出。
            var scope = Config()?.packages;
            bool InScope(Resource r) => scope == null || scope.Length == 0 || Array.IndexOf(scope, r.Package) >= 0;
            foreach (var entry in Resources.Values.Where(r => r.Type == Schema.ResourceKind.Component && r.Exported && InScope(r)).ToArray())
                Collect(entry, components, images);

            foreach (var image in images)
                ImportImage(image);
            // 字体/声音也提前导入：构建 prefab 中途导入资产会触发批量重载，把已赋值的引用打成 null
            foreach (var component in components)
                foreach (Match match in Regex.Matches(File.ReadAllText(component.File), @"ui://\w+"))
                    if (TryResolve(match.Value, component.PackageId, out var dep))
                    {
                        if (dep.Type == Schema.ResourceKind.Font)
                            ImportFont(dep);
                        else if (dep.Type == Schema.ResourceKind.Sound)
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
            // 通用导出器不认识任何具体工程；工程特有的收尾（如示例脚本挂载）通过此扩展点自行注册。
            foreach (var method in TypeCache.GetMethodsWithAttribute<MigratePostProcessAttribute>())
                method.Invoke(null, null);
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
            var package = Schema.Package.Load(packagePath);
            var packageName = Path.GetFileName(Path.GetDirectoryName(packagePath));

            foreach (var resource in package.Resources)
                Resources[package.Id + resource.Id] = new Resource
                {
                    Type = resource.Kind,
                    File = resource.File,
                    Package = packageName,
                    PackageId = package.Id,
                    Scale = resource.Scale,
                    Scale9Grid = resource.Scale9Grid,
                    Texture = resource.Texture,
                    Exported = resource.Exported,
                };
        }

        private static void Collect(Resource component, List<Resource> components, HashSet<Resource> images)
        {
            if (components.Contains(component))
                return;

            var xml = component.ComponentXml;
            foreach (var child in xml.DisplayList)
            {
                var src = child.Source;
                if (src != null && TryResolve(src, component.PackageId, out var dep))
                {
                    if (dep.Type == Schema.ResourceKind.Component)
                        Collect(dep, components, images);
                    else if (dep.Type is Schema.ResourceKind.Image or Schema.ResourceKind.MovieClip)
                        images.Add(dep);
                }
                if (child.DefaultItem is { } defaultItem && TryResolve(defaultItem, component.PackageId, out var item))
                    Collect(item, components, images);
            }
            // ComboBox 的 dropdown 组件只在根 <ComboBox dropdown> 属性引用（displayList 里没有），单独跟随。
            if (xml.ComboBox?.Dropdown is { } dropdown
                && TryResolve(dropdown, component.PackageId, out var dropdownRes) && dropdownRes.Type == Schema.ResourceKind.Component)
                Collect(dropdownRes, components, images);
            // 覆盖 icon 属性、富文本内嵌图、动效 Sound 之外的一切 ui:// 引用。
            // 编辑器源 XML 里的引用一律是 hex id（ui://<pkgId><itemId>）；命名引用（ui://Pkg/Name）只出现在运行时代码、这里读不到。
            // token 用 [\w/] 兜住命名形式的完整串（TryResolve 解析不到就跳过），避免在 '/' 处截断成错误的部分匹配。
            foreach (Match match in Regex.Matches(File.ReadAllText(component.File), @"ui://[\w/]+"))
                if (TryResolve(match.Value, component.PackageId, out var embedded) && embedded.Type is Schema.ResourceKind.Image or Schema.ResourceKind.MovieClip)
                    images.Add(embedded);
            components.Add(component);
        }

        private static void ImportImage(Resource image)
        {
            if (image.Type == Schema.ResourceKind.MovieClip)
            {
                ImportMovieClip(image);
                return;
            }

            var target = SpritePath(image);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (image.Scale == Schema.ImageScale.Tile)
                File.WriteAllBytes(target, TrimTransparent(File.ReadAllBytes(image.File))); // FairyGUI 图集会裁透明边，平铺单元按裁剪后尺寸
            else
                File.Copy(image.File, target, true);
            ImportSprite(target, image.Scale == Schema.ImageScale.Tile);

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
            var xml = component.ComponentXml;
            var name = Identifier(Path.GetFileNameWithoutExtension(component.File));
            var path = ScriptPath(component.File);
            var used = new HashSet<string>();
            var fields = new List<string>();

            foreach (var controller in xml.Controllers)
            {
                var cname = Identifier(controller.Name);
                DeleteScript($"{Path.GetDirectoryName(path)}/{name}_{cname}.cs".Replace('\\', '/'));
                if (used.Add(Field(cname)))
                {
                    fields.Add(EnumDeclaration(cname, xml.Extension));
                    fields.Add("        {");
                    fields.AddRange(PageMembers(controller).Select(page => $"            {page.Member},"));
                    fields.Add("        }");
                    fields.Add($"        public Controller<{cname}> {Field(cname)};");
                }
            }

            foreach (var child in xml.DisplayList)
            {
                var fieldName = Identifier(child.Name);
                var fieldType = FieldType(child, component.PackageId);
                if (fieldType != null && used.Add(Field(fieldName)))
                    fields.Add($"        public {fieldType} {Field(fieldName)};");
            }

            // FairyGUI 的 GButton/GComboBox 状态控制器固定名为 "button"（GetController("button")），不靠位置。
            // 无该控制器时补一个占位 enum，使 Button/ComboBox 仍是可交互的泛型面（Button<button>/ComboBox<button>），
            // 而不是退化成 Component 丢掉点击/下拉。占位 enum 是嵌套类型 `button`，与"button"子节点的 m_button 字段
            // 不同名（type vs m_ 前缀字段），不冲突——Dropdown（有 button 子节点、无 button 控制器、但有 dropdown 资源）正属此例。
            var buttonController = xml.Controllers.FirstOrDefault(c => c.Name == "button");
            var isButtonFamily = xml.Extension is Schema.ComponentExtension.Button or Schema.ComponentExtension.ComboBox;
            if (isButtonFamily && buttonController == null)
                fields.InsertRange(0, new[]
                {
                    EnumDeclaration("button", xml.Extension),
                    "        {",
                    "            up,",
                    "        }",
                });
            var stateEnum = buttonController != null ? Identifier(buttonController.Name) : "button";
            var baseType = xml.Extension switch
            {
                Schema.ComponentExtension.Button => $"NanamiUI.Button<{name}.{stateEnum}>",
                Schema.ComponentExtension.ComboBox => $"NanamiUI.ComboBox<{name}.{stateEnum}>",
                Schema.ComponentExtension.ProgressBar => "NanamiUI.ProgressBar",
                Schema.ComponentExtension.Slider => "NanamiUI.Slider",
                Schema.ComponentExtension.Label => "NanamiUI.Label",
                _ => "NanamiUI.Component",
            };
            var code = new StringBuilder();
            if (xml.Controllers.Length > 0)
                code.AppendLine("using NanamiUI;");
            code.AppendLine("using UnityEngine;");
            code.AppendLine("using UnityEngine.UI;");
            code.AppendLine();
            code.AppendLine($"namespace UI.{Identifier(component.Package)}");
            code.AppendLine("{");
            code.AppendLine($"    public partial class {name} : {baseType}");
            code.AppendLine("    {");
            foreach (var line in fields)
                code.AppendLine(line);
            code.AppendLine("    }");
            code.AppendLine("}");

            WriteScript(path, code.ToString());
        }

        private static string EnumDeclaration(string name, Schema.ComponentExtension extension) =>
            $"        public {(HidesRuntimeMember(name, extension) ? "new " : "")}enum {name}";

        private static bool HidesRuntimeMember(string name, Schema.ComponentExtension extension)
        {
            var type = extension switch
            {
                Schema.ComponentExtension.Button => typeof(Button<>),
                Schema.ComponentExtension.ComboBox => typeof(ComboBox<>),
                Schema.ComponentExtension.ProgressBar => typeof(ProgressBar),
                Schema.ComponentExtension.Slider => typeof(Slider),
                Schema.ComponentExtension.Label => typeof(Label),
                _ => typeof(Component),
            };
            return type.GetMember(name).Length > 0;
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

        private static string FieldType(Schema.Display child, string packageId) => child.Kind switch
        {
            Schema.DisplayKind.Image => "UnityEngine.UI.Image",
            Schema.DisplayKind.Graph => "NanamiUI.Graph",
            Schema.DisplayKind.Text or Schema.DisplayKind.RichText or Schema.DisplayKind.InputText => child.Input ? "NanamiUI.TextInput" : "NanamiUI.TextField",
            Schema.DisplayKind.Loader => "NanamiUI.Loader",
            Schema.DisplayKind.Component when TryResolve(child.Source, packageId, out var dep) =>
                $"UI.{Identifier(dep.Package)}.{Identifier(Path.GetFileNameWithoutExtension(dep.File))}",
            Schema.DisplayKind.Component => null,
            _ when IsMovieClip(child, packageId) => "NanamiUI.MovieClip",
            _ => "UnityEngine.RectTransform",
        };

        private static bool IsMovieClip(Schema.Display child, string packageId) =>
            child.Source != null && TryResolve(child.Source, packageId, out var resource) && resource.Type == Schema.ResourceKind.MovieClip;

        private static void BuildPrefab(Resource component)
        {
            var xml = component.ComponentXml;
            var root = new GameObject(Path.GetFileNameWithoutExtension(component.File), typeof(RectTransform));
            try
            {
                var rt = (RectTransform)root.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = xml.Size;
                if (xml.Overflow == Schema.Overflow.Hidden)
                    root.AddComponent<RectMask2D>();

                var componentType = FindType($"UI.{Identifier(component.Package)}.{Identifier(root.name)}");
                var comp = (Component)root.AddComponent(componentType);

                var controllers = new Dictionary<string, ControllerData>();
                foreach (var element in xml.Controllers)
                {
                    var name = Identifier(element.Name);
                    var pages = PageMembers(element).ToArray();
                    var pageType = componentType.GetNestedType(name);
                    controllers[element.Name] = new ControllerData
                    {
                        Name = name,
                        PageType = pageType,
                        PageIds = pages.Select(page => page.Id).ToArray(),
                        PageValues = pages.Select(page => Enum.Parse(pageType, page.Member)).ToArray(),
                        Selected = element.Selected,
                    };
                }

                var children = new List<(Schema.Display Xml, GameObject Go)>();
                foreach (var element in xml.DisplayList)
                    children.Add((element, CreateChild(element, rt, component, controllers)));

                foreach (var (element, go) in children)
                    if (element.Kind == Schema.DisplayKind.Group)
                        foreach (var (member, memberGo) in children)
                            if (member.Group == element.Id)
                                memberGo.transform.SetParent(go.transform, true);

                if (xml.Mask is { } mask)
                {
                    var maskGo = children.First(child => child.Xml.Id == mask).Go;
                    maskGo.AddComponent<Mask>().showMaskGraphic = false;
                    foreach (var (_, childGo) in children)
                        if (childGo != maskGo && childGo.transform.parent == rt)
                            childGo.transform.SetParent(maskGo.transform, true);
                }

                if (xml.Overflow == Schema.Overflow.Scroll)
                {
                    var view = BuildScrollView(root, xml, rt.sizeDelta);
                    foreach (var (_, childGo) in children)
                        if (childGo.transform.parent == rt)
                            childGo.transform.SetParent(view.Viewport, false);
                    SetGrips(view, ContentBounds(children));
                    AddScrollHost(root, view); // 运行时自挂 ScrollPane，转换后无需胶水即可滚动
                }

                var byId = new Dictionary<string, (Schema.Display Xml, GameObject Go)>();
                foreach (var (element, go) in children)
                    byId.TryAdd(element.Id, (element, go));
                foreach (var (element, go) in children)
                    foreach (var relationXml in element.Relations)
                        if (relationXml.Target != "" && byId.TryGetValue(relationXml.Target, out var relationTarget))
                        {
                            var relation = go.AddComponent<Relation>();
                            relation.target = (RectTransform)relationTarget.Go.transform;
                            relation.sidePairs = relationXml.SidePairs;
                            relation.Record();
                        }

                BuildTransitions(root, xml, byId, component.PackageId);

                // 按钮关联控制器（<Button controller=.. page=..>）：点击换 owner 上该控制器的页，实现 tab/radio 组
                //（复刻 relatedController）；并给该控制器烘 GearButton，使换页（含程序化）反向同步按钮选中态
                //（复刻 GButton.HandleControllerChanged）。须在 BuildController 之前，gear 才进控制器。
                foreach (var (element, go) in children)
                    if (element.Button?.Controller is { } ctrlName && controllers.TryGetValue(ctrlName, out var ctrlData)
                        && go.GetComponent<ButtonBase>() is { } relatedButton)
                    {
                        relatedButton.relatedOwner = comp;
                        relatedButton.relatedControllerField = ctrlData.FieldName;
                        relatedButton.relatedPage = Array.IndexOf(ctrlData.PageIds, element.Button.Page);
                        if (relatedButton.relatedPage >= 0)
                        {
                            var gearType = typeof(GearButton<>).MakeGenericType(ctrlData.PageType);
                            var gear = Activator.CreateInstance(gearType);
                            gearType.GetField("target").SetValue(gear, go);
                            var pages = Array.CreateInstance(ctrlData.PageType, 1);
                            pages.SetValue(ctrlData.PageValues[relatedButton.relatedPage], 0);
                            gearType.GetField("pages").SetValue(gear, pages);
                            ctrlData.Gears.Add(gear);
                        }
                    }

                foreach (var controller in controllers.Values)
                    BuildController(controller);
                SyncRelations(root);

                if (comp is ButtonBase buttonComp)
                {
                    // FairyGUI 按钮整块可点，与内部图形无关。uGUI 需要一个覆盖全 rect 的 raycast 面：
                    // 加一张透明(alpha=0，不改渲染)、raycastTarget 的 Image 到按钮根，保证点按钮任意位置都命中
                    // （否则内部图形（如只描边的 Shape）没盖住中心时，点击会穿过按钮打到背景）。
                    if (root.GetComponent<Graphic>() == null)
                    {
                        var hit = root.AddComponent<UnityEngine.UI.Image>();
                        hit.color = new Color(0, 0, 0, 0);
                        hit.raycastTarget = true;
                    }
                    if (controllers.TryGetValue("button", out var buttonData))
                        comp.GetType().GetField("controller").SetValue(comp, buttonData.Value); // Controller<T> 泛型字段，仅此保留反射
                    buttonComp.titleText = Child(children, "title")?.GetComponent<TextField>();
                    buttonComp.iconLoader = Child(children, "icon")?.GetComponent<Loader>();
                    if (xml.Button is { } buttonEl)
                        ConfigureButton(root, buttonEl, component.PackageId);
                    if (xml.ComboBox is { } comboEl) // 组件定义级：烘焙 dropdown 资源
                        ConfigureComboBox(root, comboEl, component.PackageId);
                    if (xml.Extension == Schema.ComponentExtension.ComboBox)
                    {
                        // ComboBox 内部可能含独立的 button 子组件（如 Dropdown 的 zd9g42 面）。uGUI 点击只解析到最深 handler、
                        // 不像 FairyGUI 那样冒泡到 combobox → 点击被内部 button 抢走、下拉打不开。加一张盖满全 rect 的透明射线面
                        // 作最后子物体，令整块点击都落到 ComboBox 自身（内部 button 只作视觉面，不单独可点）。
                        var comboHit = new GameObject("comboHit", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                        var chrt = (RectTransform)comboHit.transform;
                        chrt.SetParent(root.transform, false);
                        chrt.anchorMin = Vector2.zero;
                        chrt.anchorMax = Vector2.one;
                        chrt.offsetMin = chrt.offsetMax = Vector2.zero;
                        chrt.SetAsLastSibling();
                        comboHit.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0);
                    }
                }
                else if (comp is ProgressBar progressBar)
                    SetupProgressBar(progressBar, xml, children, root);
                else if (comp is Slider slider)
                    SetupSlider(slider, xml, children, root);
                else if (comp is Label label)
                {
                    label.titleText = Child(children, "title")?.GetComponent<TextField>();
                    label.iconLoader = Child(children, "icon")?.GetComponent<Loader>();
                }

                var byName = new Dictionary<string, GameObject>();
                foreach (var (element, go) in children)
                    byName.TryAdd(Field(element.Name), go);

                foreach (var field in comp.GetType().GetFields())
                {
                    var controller = controllers.Values.FirstOrDefault(data => data.FieldName == field.Name);
                    if (controller != null)
                        field.SetValue(comp, controller.Value);
                    else if (byName.TryGetValue(field.Name, out var go))
                        field.SetValue(comp, field.FieldType == typeof(RectTransform) ? go.transform : (object)go.GetComponent(field.FieldType));
                }

                var prefabPath = Path.ChangeExtension(AssetPath(component.File), ".prefab");
                Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
                Prefabs[prefabPath] = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateChild(Schema.Display element, RectTransform parent, Resource owner,
            Dictionary<string, ControllerData> controllers)
        {
            var name = element.Name;
            var size = element.Size;
            GameObject go;

            switch (element.Kind)
            {
                case Schema.DisplayKind.Image:
                {
                    go = NewChild(name, parent, typeof(Image));
                    var image = go.GetComponent<Image>();
                    var resource = Resolve(element.Source, owner.PackageId);
                    image.sprite = LoadSprite(element.Source, owner.PackageId);
                    ConfigureImage(image, resource, element);
                    size ??= image.sprite.rect.size;
                    break;
                }
                case Schema.DisplayKind.Graph when element.Type == Schema.ShapeType.None:
                    go = NewChild(name, parent);
                    break;
                case Schema.DisplayKind.Graph:
                {
                    go = NewChild(name, parent, typeof(Graph));
                    ConfigureShape(go.GetComponent<Graph>(), element);
                    break;
                }
                case Schema.DisplayKind.List:
                {
                    go = NewChild(name, parent);
                    BuildList(go, element, owner);
                    break;
                }
                case Schema.DisplayKind.Text:
                case Schema.DisplayKind.RichText:
                case Schema.DisplayKind.InputText:
                {
                    go = NewChild(name, parent, typeof(TextField));
                    var textComponent = go.GetComponent<TextField>();
                    ConfigureText(textComponent, element, owner);
                    if (element.StrokeColor is { } stroke)
                    {
                        var effect = go.AddComponent<TextStroke>();
                        effect.color = ParseColor(stroke);
                        effect.width = element.StrokeSize;
                    }
                    if (element.ShadowColor is { } shadow)
                    {
                        var effect = go.AddComponent<TextShadow>();
                        effect.color = ParseColor(shadow);
                        effect.offset = element.ShadowOffset ?? new Vector2(1, 1);
                    }
                    if (element.Input)
                        ConfigureInput(go, textComponent, element);
                    break;
                }
                case Schema.DisplayKind.Loader:
                {
                    go = NewChild(name, parent, typeof(Loader));
                    var loader = go.GetComponent<Loader>();
                    loader.fill = element.Fill switch
                    {
                        Schema.LoaderFill.Scale => Loader.FillType.Scale,
                        Schema.LoaderFill.ScaleMatchHeight => Loader.FillType.ScaleMatchHeight,
                        Schema.LoaderFill.ScaleMatchWidth => Loader.FillType.ScaleMatchWidth,
                        Schema.LoaderFill.ScaleFree => Loader.FillType.ScaleFree,
                        Schema.LoaderFill.ScaleNoBorder => Loader.FillType.ScaleNoBorder,
                        _ => Loader.FillType.None,
                    };
                    loader.align = element.Align switch { Schema.Align.Center => Loader.AlignType.Center, Schema.Align.Right => Loader.AlignType.Right, _ => Loader.AlignType.Left };
                    loader.vAlign = element.VAlign switch { Schema.VAlign.Middle => Loader.VertAlignType.Middle, Schema.VAlign.Bottom => Loader.VertAlignType.Bottom, _ => Loader.VertAlignType.Top };
                    var url = element.Source;
                    if (url != null && TryResolve(url, owner.PackageId, out var content) && content.Type == Schema.ResourceKind.Image)
                    {
                        loader.sprite = LoadSprite(url, owner.PackageId);
                        ConfigureImage(loader, content, element);
                    }
                    else if (url != null && TryResolve(url, owner.PackageId, out content) && content.Type == Schema.ResourceKind.MovieClip)
                        ConfigureMovieClip(loader, content, element);
                    else
                        loader.enabled = false;
                    break;
                }
                case Schema.DisplayKind.Component:
                {
                    var dep = Resolve(element.Source, owner.PackageId);
                    go = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(dep), parent);
                    go.name = name;
                    size ??= ((RectTransform)go.transform).sizeDelta;
                    if (element.Button is { } button)
                        ConfigureButton(go, button, owner.PackageId);
                    if (element.Label is { } label)
                        ConfigureLabel(go, label, owner.PackageId);
                    if (element.ProgressBar is { } progress)
                        ConfigureProgressBar(go, progress);
                    if (element.Slider is { } slider)
                        ConfigureSlider(go, slider);
                    if (element.ComboBox is { } comboBox) // 实例级：烘焙 items + 默认标题
                        ConfigureComboBox(go, comboBox, owner.PackageId);
                    break;
                }
                default:
                {
                    if (IsMovieClip(element, owner.PackageId))
                    {
                        go = NewChild(name, parent, typeof(MovieClip));
                        var movieClip = go.GetComponent<MovieClip>();
                        ConfigureMovieClip(movieClip, Resolve(element.Source, owner.PackageId), element);
                        size ??= MovieClips[Resolve(element.Source, owner.PackageId)].Size;
                    }
                    else
                        go = NewChild(name, parent);
                    break;
                }
            }

            SetRect((RectTransform)go.transform, element, size ?? Vector2.zero, parent);
            if (go.GetComponent<TextField>() is { } createdText)
                createdText.RebuildImages(); // 依赖最终 rect 的排版，须在 SetRect 之后烘焙
            ApplyElement(go, element);
            if (!element.Visible)
                go.SetActive(false);

            object display = null, display2 = null;
            foreach (var gearXml in element.Gears)
            {
                var kind = gearXml.Kind;
                var controller = controllers[gearXml.Controller];
                var gearType = (kind switch
                {
                    Schema.GearKind.Display or Schema.GearKind.Display2 => typeof(GearDisplay<>),
                    Schema.GearKind.XY => typeof(GearXY<>),
                    Schema.GearKind.Color => typeof(GearColor<>),
                    Schema.GearKind.Look => typeof(GearLook<>),
                    Schema.GearKind.Ani => typeof(GearAni<>),
                    Schema.GearKind.FontSize => typeof(GearFontSize<>),
                    Schema.GearKind.Text => typeof(GearText<>),
                    Schema.GearKind.Icon => typeof(GearIcon<>),
                    _ => typeof(GearSize<>),
                }).MakeGenericType(controller.PageType);
                var gear = Activator.CreateInstance(gearType);
                gearType.GetField("target").SetValue(gear, go);
                gearType.GetField("pages").SetValue(gear, PageValues(controller, gearXml.Pages));
                // 缓动配置对所有 gear 通用（gearXY/gearSize/gearLook 在 Basics 用到 tween），默认 QuadOut/0.3/0。
                gearType.GetField("tween").SetValue(gear, gearXml.Tween);
                gearType.GetField("duration").SetValue(gear, gearXml.Duration);
                gearType.GetField("ease").SetValue(gear, ParseEase(gearXml.Ease));
                gearType.GetField("delay").SetValue(gear, gearXml.Delay);
                if (kind == Schema.GearKind.XY)
                {
                    var rt = (RectTransform)go.transform;
                    var origin = element.Position ?? Vector2.zero;
                    Vector2 Convert(string pair)
                    {
                        var parts = pair.Split(',');
                        return rt.anchoredPosition + new Vector2(
                            float.Parse(parts[0], CultureInfo.InvariantCulture) - origin.x,
                            origin.y - float.Parse(parts[1], CultureInfo.InvariantCulture));
                    }
                    var defaultValue = gearXml.Default is { } def ? Convert(def) : rt.anchoredPosition;
                    gearType.GetField("values").SetValue(gear, gearXml.Values.Split('|').Select(value => value == "-" ? defaultValue : Convert(value)).ToArray());
                    gearType.GetField("defaultValue").SetValue(gear, defaultValue);
                }
                else if (kind == Schema.GearKind.Color)
                {
                    var graphic = go.GetComponent<Graphic>();
                    var def = gearXml.Default is { } value ? ParseColor(value) : graphic.color;
                    gearType.GetField("values").SetValue(gear, gearXml.Values.Split('|').Select(value => value == "-" ? def : ParseColor(value)).ToArray());
                    gearType.GetField("defaultValue").SetValue(gear, def);
                }
                else if (kind == Schema.GearKind.Look)
                    ConfigureGearLook(gearType, gear, gearXml, go);
                else if (kind == Schema.GearKind.Size)
                    ConfigureGearSize(gearType, gear, gearXml, (RectTransform)go.transform);
                else if (kind == Schema.GearKind.Ani)
                    ConfigureGearAni(gearType, gear, gearXml);
                else if (kind == Schema.GearKind.FontSize)
                {
                    var def = gearXml.Default is { } value ? int.Parse(value) : go.GetComponent<TextField>().fontSize;
                    gearType.GetField("values").SetValue(gear, gearXml.Values.Split('|').Select(value => value == "-" ? def : int.Parse(value)).ToArray());
                    gearType.GetField("defaultValue").SetValue(gear, def);
                }
                else if (kind == Schema.GearKind.Text)
                {
                    var def = gearXml.Default ?? go.GetComponentInChildren<TextField>(true).text;
                    gearType.GetField("values").SetValue(gear, gearXml.Values.Split('|').Select(value => value == "-" ? def : value).ToArray());
                    gearType.GetField("defaultValue").SetValue(gear, def);
                }
                else if (kind == Schema.GearKind.Icon)
                {
                    var def = gearXml.Default is { } value ? LoadSprite(value, owner.PackageId) : null;
                    gearType.GetField("values").SetValue(gear, gearXml.Values.Split('|').Select(value => value == "-" ? def : LoadSprite(value, owner.PackageId)).ToArray());
                    gearType.GetField("defaultValue").SetValue(gear, def);
                }
                else if (kind == Schema.GearKind.Display)
                    display = gear;
                else if (kind == Schema.GearKind.Display2)
                {
                    display2 = gear;
                    gearType.GetField("condition").SetValue(gear, gearXml.Condition);
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
            {
                var idx = Array.IndexOf(controller.PageIds, parts[i]);
                if (idx >= 0) // 跳过控制器已不存在的陈旧页 id（编辑器残留），避免越界；该槽留 default 页
                    values.SetValue(controller.PageValues[idx], i);
            }
            return values;
        }

        private static void ConfigureImage(UnityEngine.UI.Image image, Resource resource, Schema.Display element)
        {
            image.type = resource.Scale == Schema.ImageScale.Tile ? UnityEngine.UI.Image.Type.Tiled : image.sprite.border == Vector4.zero ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Sliced;
            image.color = ParseColor(element.Color ?? "#ffffffff");
            if (element.FillMethod != Schema.ImageFillMethod.None)
            {
                image.type = UnityEngine.UI.Image.Type.Filled;
                image.fillMethod = element.FillMethod switch
                {
                    Schema.ImageFillMethod.Horizontal => UnityEngine.UI.Image.FillMethod.Horizontal,
                    Schema.ImageFillMethod.Vertical => UnityEngine.UI.Image.FillMethod.Vertical,
                    Schema.ImageFillMethod.Radial90 => UnityEngine.UI.Image.FillMethod.Radial90,
                    Schema.ImageFillMethod.Radial180 => UnityEngine.UI.Image.FillMethod.Radial180,
                    _ => UnityEngine.UI.Image.FillMethod.Radial360,
                };
                if (image.fillMethod == UnityEngine.UI.Image.FillMethod.Radial360)
                    image.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
                image.fillClockwise = element.FillClockwise;
                image.fillAmount = element.FillAmount / 100;
            }
            if (image is Image flip && element.Flip != Schema.Flip.None)
            {
                flip.flipX = element.Flip is Schema.Flip.Horizontal or Schema.Flip.Both;
                flip.flipY = element.Flip is Schema.Flip.Vertical or Schema.Flip.Both;
            }
        }

        private static void ConfigureButton(GameObject go, Schema.Extension buttonXml, string packageId)
        {
            var button = go.GetComponent<ButtonBase>();
            if (button == null)
                return; // 非 button 项（plain component/label 作 list item）：无按钮面可配，跳过
            if (buttonXml.Title is { } title)
                button.Title = title;
            if (buttonXml.SelectedTitle is { } selectedTitle)
                button.selectedTitle = selectedTitle;
            if (buttonXml.Icon is { } icon)
                button.Icon = LoadSprite(icon, packageId);
            if (buttonXml.Mode is { } mode)
                button.mode = mode;
            button.selected = buttonXml.Checked && button.mode != ButtonMode.Common; // 复刻 GButton.selected：Common 忽略选中态
            button.RefreshState();
        }

        private static void ConfigureLabel(GameObject go, Schema.Extension labelXml, string packageId)
        {
            if (labelXml.Title is { } title && FindChild(go.transform, "title")?.GetComponent<TextField>() is { } titleText)
                titleText.text = title;
            if (labelXml.TitleColor is { } titleColor && FindChild(go.transform, "title")?.GetComponent<TextField>() is { } colorText)
                colorText.color = ParseColor(titleColor);
            if (labelXml.Icon is { } icon && FindChild(go.transform, "icon")?.GetComponent<UnityEngine.UI.Image>() is { } image)
            {
                image.sprite = LoadSprite(icon, packageId);
                image.enabled = true;
            }
        }

        private static void ConfigureShape(Graph shape, Schema.Display element)
        {
            shape.lineSize = element.LineSize;
            shape.lineColor = ParseColor(element.LineColor);
            shape.color = ParseColor(element.FillColor);
            if (element.Skew != null)
                shape.skew = (Vector2)element.Skew;
            switch (element.Type)
            {
                case Schema.ShapeType.Ellipse:
                    shape.kind = Graph.Kind.Ellipse;
                    break;
                case Schema.ShapeType.Polygon:
                    shape.kind = Graph.Kind.Polygon;
                    var values = element.Points.Split(',').Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                    shape.points = Enumerable.Range(0, values.Length / 2).Select(i => new Vector2(values[i * 2], values[i * 2 + 1])).ToArray();
                    break;
                case Schema.ShapeType.RegularPolygon:
                    shape.kind = Graph.Kind.RegularPolygon;
                    shape.sides = element.Sides;
                    shape.startAngle = element.StartAngle;
                    shape.distances = element.Distances?.Split(',').Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                    break;
                default:
                    if (element.Corner is { } corner)
                    {
                        shape.kind = Graph.Kind.RoundedRect;
                        var radii = corner.Split(',').Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                        shape.corners = Enumerable.Range(0, 4).Select(i => radii[Mathf.Min(i, radii.Length - 1)]).ToArray();
                    }
                    else
                        shape.kind = Graph.Kind.Rect;
                    break;
            }
        }

        private static void ConfigureMovieClip(MovieClip movieClip, Resource resource, Schema.Display element)
        {
            var data = MovieClips[resource];
            var basePath = AssetPath(resource.File);
            movieClip.frames = Enumerable.Range(0, data.FrameCount)
                .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(FramePath(basePath, i)))
                .ToArray();
            movieClip.interval = data.Interval;
            movieClip.addDelays = data.AddDelays;
            movieClip.playing = element.Playing;
            movieClip.frame = element.Frame;
            movieClip.sprite = movieClip.frames[movieClip.frame];
            movieClip.type = UnityEngine.UI.Image.Type.Simple;
            movieClip.color = ParseColor(element.Color ?? "#ffffffff");
        }

        private static void SetupProgressBar(ProgressBar progress, Schema.Component xml, List<(Schema.Display Xml, GameObject Go)> children, GameObject root)
        {
            var ext = xml.ProgressBar;
            progress.titleType = ext.TitleType;
            progress.reverse = ext.Reverse;
            progress.title = Child(children, "title")?.GetComponent<TextField>();
            var size = xml.Size;
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

        private static void SetupSlider(Slider slider, Schema.Component xml, List<(Schema.Display Xml, GameObject Go)> children, GameObject root)
        {
            var ext = xml.Slider;
            slider.titleType = ext.TitleType;
            slider.min = ext.Min;
            slider.reverse = ext.Reverse;
            slider.wholeNumbers = ext.WholeNumbers;
            slider.changeOnClick = ext.ChangeOnClick;
            slider.title = Child(children, "title")?.GetComponent<TextField>();
            var size = xml.Size;
            if (Child(children, "bar") is { } bar)
            {
                slider.bar = (RectTransform)bar.transform;
                slider.barMaxWidthDelta = size.x - slider.bar.rect.width;
                slider.barStartX = slider.bar.anchoredPosition.x;
            }
            if (Child(children, "bar_v") is { } barV)
            {
                slider.barV = (RectTransform)barV.transform;
                slider.barMaxHeightDelta = size.y - slider.barV.rect.height;
                slider.barStartY = -slider.barV.anchoredPosition.y;
            }
            if (Child(children, "grip") is { } grip)
            {
                slider.grip = (RectTransform)grip.transform;
                // grip 常是独立 button 组件、会吞掉指针事件，拖动中继必须烘在 grip 层（复刻 GSlider 挂 _gripObject 的 touch 处理器）。
                grip.AddComponent<SliderGrip>().slider = slider;
            }
            slider.Apply();
            SyncRelations(root);
        }

        private static GameObject Child(List<(Schema.Display Xml, GameObject Go)> children, string name) =>
            children.FirstOrDefault(child => child.Xml.Name == name).Go;

        private static void ConfigureProgressBar(GameObject go, Schema.Extension xml)
        {
            var progress = go.GetComponent<ProgressBar>();
            progress.value = FloatValue(xml.Value, 50);
            progress.max = xml.Max;
            progress.Apply();
            SyncRelations(go);
        }

        private static void ConfigureSlider(GameObject go, Schema.Extension xml)
        {
            // 实例级只覆盖 value/max/min（同 GSlider.Setup_AfterAdd）；reverse/wholeNumbers/changeOnClick 是定义级
            // （ConstructExtension），由 SetupSlider 烘定，实例 XML 的属性默认值不能覆盖它们。
            var slider = go.GetComponent<Slider>();
            slider.value = FloatValue(xml.Value, 50);
            slider.max = xml.Max;
            slider.min = xml.Min;
            slider.Apply();
            SyncRelations(go);
        }

        private static void ConfigureComboBox(GameObject go, Schema.Extension xml, string packageId)
        {
            var combo = go.GetComponent<ButtonBase>();
            if (combo == null)
                return;
            var type = combo.GetType();
            if (xml.Dropdown is { } dropdown && TryResolve(dropdown, packageId, out var dropRes))
                type.GetField("dropdownPrefab").SetValue(combo, LoadPrefab(dropRes));
            type.GetField("visibleItemCount").SetValue(combo, xml.VisibleItemCount);
            var items = xml.Items;
            if (items.Length > 0)
            {
                type.GetField("items").SetValue(combo, items);
                if (xml.Values.Length > 0)
                    type.GetField("values").SetValue(combo, xml.Values);
                type.GetProperty("Title").SetValue(combo, items[0]); // 默认选中项 0
            }
        }

        private static float FloatValue(string value, float defaultValue) =>
            value == null ? defaultValue : float.Parse(value, CultureInfo.InvariantCulture);

        private static void SyncRelations(GameObject go)
        {
            foreach (var relation in go.GetComponentsInChildren<Relation>(true))
                relation.Sync();
        }

        private static void ApplyElement(GameObject go, Schema.Display element)
        {
            var rt = (RectTransform)go.transform;
            if (element.Rotation is { } rotation)
                rt.localEulerAngles = new Vector3(0, 0, -rotation);
            if (element.Alpha is { } alpha)
                foreach (var graphic in go.GetComponentsInChildren<Graphic>(true))
                {
                    var color = graphic.color;
                    color.a *= alpha;
                    graphic.color = color;
                }
            if (!element.Touchable)
                go.AddComponent<CanvasGroup>().blocksRaycasts = false;
            if (element.Grayed)
            {
                var button = go.GetComponent<ButtonBase>();
                // FairyGUI 约定：存在名为 grayed 的控制器时，置灰只切换其页面 1，不套灰度材质。
                if (button != null && button.GetType().GetField("m_grayed") is { } grayedController)
                {
                    var controller = grayedController.GetValue(button);
                    var pageProperty = controller.GetType().GetProperty("page");
                    pageProperty.SetValue(controller, Enum.GetValues(pageProperty.PropertyType).GetValue(1));
                    grayedController.SetValue(button, controller);
                }
                else
                {
                    go.AddComponent<Grayed>().shader = GrayscaleShader;
                    if (button != null)
                        button.SetGrayed(true);
                }
            }
        }

        private static void ConfigureGearLook(Type gearType, object gear, Schema.Gear xml, GameObject go)
        {
            (float Alpha, float Rotation, bool Grayed) Parse(string value, (float Alpha, float Rotation, bool Grayed) def)
            {
                if (value == "-")
                    return def;
                var parts = value.Split(',');
                return (float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture), parts[2] == "1");
            }

            var def = Parse(xml.Default ?? "1,0,0", (1, 0, false));
            var values = xml.Values.Split('|').Select(value => Parse(value, def)).ToArray();
            gearType.GetField("alphas").SetValue(gear, values.Select(value => value.Alpha).ToArray());
            gearType.GetField("defaultAlpha").SetValue(gear, def.Alpha);
            gearType.GetField("rotations").SetValue(gear, values.Select(value => value.Rotation).ToArray());
            gearType.GetField("defaultRotation").SetValue(gear, def.Rotation);
            gearType.GetField("grayed").SetValue(gear, values.Select(value => value.Grayed).ToArray());
            gearType.GetField("defaultGrayed").SetValue(gear, def.Grayed);
            // 会用到置灰的 target 烘一个 disabled 的 Grayed，初始页的开关由 BuildController 应用 gear 时切换；
            // element 自带 grayed 时 ApplyElement 已加（enabled）。
            if ((def.Grayed || values.Any(value => value.Grayed)) && !go.TryGetComponent(out Grayed _))
            {
                var effect = go.AddComponent<Grayed>();
                effect.shader = GrayscaleShader;
                effect.enabled = false;
            }
        }

        private static void ConfigureGearSize(Type gearType, object gear, Schema.Gear xml, RectTransform rt)
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
            var def = xml.Default is { } defaultValue ? Parse(defaultValue, fallback) : fallback;
            var values = xml.Values.Split('|').Select(value => Parse(value, def)).ToArray();
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
        private static ScrollView BuildScrollView(GameObject root, Schema.Component element, Vector2 size) =>
            BuildScrollView(root, size, element.Margin, element.ScrollBarMargin, element.Scroll, element.ScrollBar, element.ScrollBarFlags, element.ClipSoftness);

        private static ScrollView BuildScrollView(GameObject root, Schema.Display element, Vector2 size)
        {
            return BuildScrollView(root, size, element.Margin, element.ScrollBarMargin, element.Scroll, element.ScrollBar, element.ScrollBarFlags, element.ClipSoftness);
        }

        private static ScrollView BuildScrollView(GameObject root, Vector2 size, string marginValue, string scrollBarMarginValue, Schema.Scroll scroll, string scrollBar, int scrollBarFlags, string clipSoftness)
        {
            var margin = ParseMargin(marginValue);
            var barMargin = ParseMargin(scrollBarMarginValue);
            var hideBars = scrollBar == "hidden";
            // scrollBarFlags bit0 = displayOnLeft：竖直滚动条放左侧、内容从左内缩（FairyGUI ScrollPane._displayOnLeft）。
            var displayOnLeft = (scrollBarFlags & 1) != 0;
            var vtBar = !hideBars && scroll is Schema.Scroll.Vertical or Schema.Scroll.Both ? InstantiateScrollBar(Settings().scrollBars?.vertical, root.transform) : null;
            var hzBar = !hideBars && scroll is Schema.Scroll.Horizontal or Schema.Scroll.Both ? InstantiateScrollBar(Settings().scrollBars?.horizontal, root.transform) : null;
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
            if (clipSoftness is { } softness)
            {
                var parts = softness.Split(',');
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

        private static Vector2 ContentBounds(List<(Schema.Display Xml, GameObject Go)> children)
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

        private static void BuildList(GameObject go, Schema.Display element, Resource owner)
        {
            var size = (Vector2)element.Size;
            var prefab = LoadPrefab(Resolve(element.DefaultItem, owner.PackageId));
            var itemSize = ((RectTransform)prefab.transform).sizeDelta;
            var lineGap = element.LineGap;
            var colGap = element.ColGap;
            var layout = element.Layout;

            // 烘焙动态实例化描述：运行时（PopupMenu/ComboBox 下拉/Window1/Grid）据此从 defaultItem 建项。
            var source = go.AddComponent<ListSource>();
            source.itemPrefab = prefab;
            source.itemSize = itemSize;
            source.lineGap = lineGap;
            source.colGap = colGap;
            source.layout = layout switch
            {
                Schema.ListLayout.SingleRow => ListLayoutType.SingleRow,
                Schema.ListLayout.FlowHorizontal => ListLayoutType.FlowHorizontal,
                Schema.ListLayout.FlowVertical => ListLayoutType.FlowVertical,
                Schema.ListLayout.Pagination => ListLayoutType.Pagination,
                _ => ListLayoutType.SingleColumn,
            };

            var view = element.Overflow == Schema.Overflow.Scroll
                ? BuildScrollView(go, element, size)
                : new ScrollView { Viewport = (RectTransform)go.transform };
            var viewSize = view.Viewport == go.transform ? size : view.ViewportSize;

            var positions = new List<Vector2>();
            float x = 0, y = 0;
            foreach (var _ in element.Items)
                switch (layout)
                {
                    case Schema.ListLayout.SingleRow:
                        positions.Add(new Vector2(x, 0));
                        x += itemSize.x + colGap;
                        break;
                    case Schema.ListLayout.FlowHorizontal:
                    case Schema.ListLayout.Pagination: // 分页吸附不做：按 flow_hz 排布
                        if (x != 0 && x + itemSize.x > viewSize.x)
                        {
                            x = 0;
                            y += itemSize.y + lineGap;
                        }
                        positions.Add(new Vector2(x, y));
                        x += itemSize.x + colGap;
                        break;
                    case Schema.ListLayout.FlowVertical:
                        if (y != 0 && y + itemSize.y > viewSize.y)
                        {
                            y = 0;
                            x += itemSize.x + colGap;
                        }
                        positions.Add(new Vector2(x, y));
                        y += itemSize.y + lineGap;
                        break;
                    default: // SingleColumn
                        positions.Add(new Vector2(0, y));
                        y += itemSize.y + lineGap;
                        break;
                }

            var content = Vector2.zero;
            var index = 0;
            foreach (var item in element.Items)
            {
                var position = positions[index++];
                content = Vector2.Max(content, position + itemSize);
                var itemGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, view.Viewport);
                ((RectTransform)itemGo.transform).anchoredPosition = new Vector2(position.x, -position.y);
                ConfigureButton(itemGo, item, owner.PackageId);
            }
            SetGrips(view, content);
            if (element.Overflow == Schema.Overflow.Scroll)
                AddScrollHost(go, view); // 运行时自挂 ScrollPane
            if (element.SelectionMode != ListSelectionMode.None)
                go.AddComponent<ListSelection>().selectionMode = element.SelectionMode; // 点项选中/发 onClickItem
        }

        // 滚动条轨道/grip 引用烘进 ScrollPaneHost（滚动条组件名任意，运行时不按名查找）；
        // "bar"/"grip" 是 FairyGUI 滚动条子件的契约名，仅在烘焙期解析一次。
        private static void AddScrollHost(GameObject go, ScrollView view)
        {
            var host = go.AddComponent<ScrollPaneHost>();
            if (view.VtBar != null)
            {
                host.vtBar = (RectTransform)FindChild(view.VtBar.transform, "bar");
                host.vtGrip = (RectTransform)FindChild(view.VtBar.transform, "grip");
            }
            if (view.HzBar != null)
            {
                host.hzBar = (RectTransform)FindChild(view.HzBar.transform, "bar");
                host.hzGrip = (RectTransform)FindChild(view.HzBar.transform, "grip");
            }
        }

        // 默认滚动条来自 Common.json 的 ui:// 引用；未配置则返回 null，viewport 退化为满宽。
        private static GameObject InstantiateScrollBar(string uiRef, Transform parent)
        {
            if (string.IsNullOrEmpty(uiRef) || !TryResolve(uiRef, null, out var resource))
                return null;
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
        private static void BuildTransitions(GameObject root, Schema.Component xml, Dictionary<string, (Schema.Display Xml, GameObject Go)> byId, string packageId)
        {
            foreach (var transitionXml in xml.Transitions)
            {
                var transition = root.AddComponent<Transition>();
                transition.transitionName = transitionXml.Name;
                transition.autoPlay = transitionXml.AutoPlay;
                transition.autoPlayTimes = transitionXml.AutoPlayRepeat;
                transition.autoPlayDelay = transitionXml.AutoPlayDelay;

                var items = new List<TransitionItem>();
                foreach (var itemXml in transitionXml.Items)
                {
                    var targetId = itemXml.Target;
                    (Schema.Display Xml, GameObject Go) target = default;
                    if (targetId != "" && !byId.TryGetValue(targetId, out target))
                        continue; // 编辑器遗留的失效目标
                    var type = ParseTransitionType(itemXml.Type);
                    if (type == null)
                        continue;

                    var item = new TransitionItem
                    {
                        time = itemXml.Time / 24f,
                        target = target.Go != null ? (RectTransform)target.Go.transform : null,
                        type = type.Value,
                        tween = itemXml.Tween,
                        duration = itemXml.Duration / 24f,
                        ease = ParseEase(itemXml.Ease),
                        repeat = itemXml.Repeat,
                        yoyo = itemXml.Yoyo,
                    };

                    if (item.tween)
                    {
                        item.start = ParseTransitionValues(itemXml.StartValue, item.type);
                        item.end = ParseTransitionValues(itemXml.EndValue, item.type);
                    }
                    else
                    {
                        var value = itemXml.Value;
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
                        var designXY = target.Xml != null ? target.Xml.Position ?? Vector2.zero : Vector2.zero;
                        item.positionOffset = rt.anchoredPosition - new Vector2(designXY.x, -designXY.y);
                        if (itemXml.Path is { } path)
                            item.pathData = path.Split(',').Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                    }
                    else if (item.type == TransitionItemType.ColorFilter)
                    {
                        // ColorFilter 目标烘一个 disabled 的 ColorAdjust，运行时 Transition 直接 Set。
                        var filterGo = target.Go != null ? target.Go : root;
                        if (!filterGo.TryGetComponent(out ColorAdjust adjust))
                            adjust = filterGo.AddComponent<ColorAdjust>();
                        adjust.shader = ColorMatrixShader;
                        adjust.enabled = false;
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
            "Skew" => TransitionItemType.Skew,
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
            if (parts.Length < 2) // "Custom"（编辑器自绘曲线）等单段值 DOTween 无对应，回退
                return Ease.OutQuad;
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

        private static void ConfigureGearAni(Type gearType, object gear, Schema.Gear xml)
        {
            (int Frame, bool Playing) Parse(string value, (int Frame, bool Playing) def)
            {
                if (value == "-")
                    return def;
                var parts = value.Split(',');
                return (int.Parse(parts[0]), parts[1] == "p");
            }

            var def = Parse(xml.Default ?? "0,p", (0, true));
            var values = xml.Values.Split('|').Select(value => Parse(value, def)).ToArray();
            gearType.GetField("frames").SetValue(gear, values.Select(value => value.Frame).ToArray());
            gearType.GetField("defaultFrame").SetValue(gear, def.Frame);
            gearType.GetField("playings").SetValue(gear, values.Select(value => value.Playing).ToArray());
            gearType.GetField("defaultPlaying").SetValue(gear, def.Playing);
        }

        private static Transform FindChild(Transform parent, string name)
        {
            if (parent.name == name)
                return parent;
            foreach (Transform child in parent)
                if (FindChild(child, name) is { } found)
                    return found;
            return null;
        }

        private static void SetRect(RectTransform rt, Schema.Display element, Vector2 size, RectTransform parent)
        {
            var xy = element.Position ?? Vector2.zero;
            if (element.Anchor && element.Pivot is { } anchorPivot)
                xy -= anchorPivot * size;
            Vector2 anchorMin = new(0, 1), anchorMax = new(0, 1);
            foreach (var relation in element.Relations)
            {
                if (relation.Target != "")
                    continue;
                // 容器关联 → uGUI 锚点。*-left/*-top 跟随容器左/上边，容器局部系里该边不动 = 默认左上锚；
                // ext 关联未在容器关联中出现过（数据里只作兄弟关联），不映射。
                foreach (var pair in relation.SidePairs)
                    switch (pair)
                    {
                        case RelationSide.Width:
                        case RelationSide.WidthWidth:
                            anchorMin.x = 0; anchorMax.x = 1; break;
                        case RelationSide.Height:
                        case RelationSide.HeightHeight:
                            anchorMin.y = 0; anchorMax.y = 1; break;
                        case RelationSide.Size:
                            anchorMin = Vector2.zero; anchorMax = Vector2.one; break;
                        case RelationSide.LeftRight:
                        case RelationSide.RightRight:
                            anchorMin.x = anchorMax.x = 1; break;
                        case RelationSide.LeftCenter:
                        case RelationSide.CenterCenter:
                        case RelationSide.RightCenter:
                            anchorMin.x = anchorMax.x = 0.5f; break;
                        case RelationSide.TopBottom:
                        case RelationSide.BottomBottom:
                            anchorMin.y = anchorMax.y = 0; break;
                        case RelationSide.TopMiddle:
                        case RelationSide.MiddleMiddle:
                        case RelationSide.BottomMiddle:
                            anchorMin.y = anchorMax.y = 0.5f; break;
                    }
            }

            var pw = parent.rect.width;
            var ph = parent.rect.height;
            rt.pivot = new Vector2(0, 1);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(xy.x - anchorMin.x * pw, ph - xy.y - size.y - anchorMin.y * ph);
            rt.offsetMax = new Vector2(xy.x + size.x - anchorMax.x * pw, ph - xy.y - anchorMax.y * ph);
            if (element.Pivot is { } pivot)
            {
                // pivot setter 保持 anchoredPosition，需要补偿位移以保持 rect 不动。
                var newPivot = new Vector2(pivot.x, 1 - pivot.y);
                var delta = newPivot - rt.pivot;
                rt.pivot = newPivot;
                rt.anchoredPosition += new Vector2(delta.x * size.x, delta.y * size.y);
            }
            if (element.Scale is { } scale)
                rt.localScale = new Vector3(scale.x, scale.y, 1);
        }

        // 把显示 Text 升级为可编辑输入框（复刻 FairyGUI GTextInput）：走原生 uGUI InputField。
        // 不复用 NanamiUI.TextField 作输入面——它的 OnEnable 会按 onClickLink 把 raycastTarget 清零（Text.cs），运行时把烘焙的
        // raycastTarget 覆盖成 false → GraphicRaycaster 跳过 → 输入框永远无法聚焦；且它自绘 OnPopulateMesh 不填
        // cachedTextGenerator，InputField 光标定位/选区失效。故：targetGraphic 用一张透明常驻 Image（空文本时 Text 无网格
        // → 单靠 Text 不可点），textComponent 用普通 UI.Text（原生渲染 + 填充 generator，输入框功能完整）。
        // 输入框不追求 FairyGUI 字体排版一致（项目允许）；prompt 仍用 NanamiUI.TextField 以复刻 UBB 斜体灰样式，
        // 空文本静置时页面显示的正是 placeholder，故静态 golden 不受影响。
        private static void ConfigureInput(GameObject go, TextField display, Schema.Display element)
        {
            var fontSize = display.fontSize;
            var color = display.color;
            var alignment = display.alignment;
            Object.DestroyImmediate(display); // 卸掉 NanamiUI.TextField 显示面，改用原生结构

            var bg = go.AddComponent<UnityEngine.UI.Image>();
            bg.color = Color.clear; // 透明但常驻 raycast，保证整框可点/可聚焦

            var field = go.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.lineType = element.SingleLine ? InputField.LineType.SingleLine : InputField.LineType.MultiLineNewline;
            if (element.Password)
                field.contentType = InputField.ContentType.Password;
            if (element.MaxLength > 0)
                field.characterLimit = element.MaxLength;

            var textGo = NewChild("Text", (RectTransform)go.transform, typeof(UnityEngine.UI.Text));
            var text = textGo.GetComponent<UnityEngine.UI.Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 可序列化的内置字体（Resources 名与本类字段冲突，需全限定）
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = false;
            StretchFull((RectTransform)textGo.transform);
            field.textComponent = text;
            field.text = element.Text; // textComponent 就绪后再设，UpdateLabel 才能写进显示

            if (element.Prompt is { } prompt)
            {
                var phGo = NewChild("placeholder", (RectTransform)go.transform, typeof(TextField));
                var ph = phGo.GetComponent<TextField>();
                ph.fontSize = fontSize;
                ph.alignment = alignment;
                ph.ubb = true;
                ph.text = prompt;
                ph.raycastTarget = false;
                StretchFull((RectTransform)phGo.transform);
                field.placeholder = ph;
            }

            var submit = go.AddComponent<InputSubmit>(); // Enter 提交中继（仅回车、仅单行），复刻 GTextInput onSubmit
            submit.field = field;
            var input = go.AddComponent<TextInput>();
            input.field = field;
            input.submit = submit;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void ConfigureText(TextField text, Schema.Display element, Resource owner)
        {
            text.text = element.Text;
            text.fontSize = element.FontSize ?? Settings().fontSize;
            text.leading = element.Leading;
            text.letterSpacing = element.LetterSpacing;
            text.color = ParseColor(element.Color ?? Settings().textColor);
            text.supportRichText = false;
            text.html = element.Kind == Schema.DisplayKind.RichText;
            text.ubb = element.Ubb;
            text.underlined = element.Underline;
            if (text.text == "" && element.Prompt is { } prompt)
            {
                text.text = prompt;
                text.ubb = true;
            }
            text.imageSprites = Regex.Matches(text.text, @"ui://\w+")
                .Select(match => TryResolve(match.Value, owner.PackageId, out var res)
                    ? AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(res))
                    : null) // 陈旧/非图片的内嵌 img 标签解析不到 → null 占位，不抛
                .ToArray();
            var bold = element.Bold;
            var italic = element.Italic;
            text.fontStyle = bold && italic ? FontStyle.BoldAndItalic : bold ? FontStyle.Bold : italic ? FontStyle.Italic : FontStyle.Normal;
            // Shrink/Ellipsis 未实现，按 Height（竖向撑开）处理。
            text.horizontalOverflow = element.AutoSize == Schema.TextAutoSize.Both || element.SingleLine
                ? HorizontalWrapMode.Overflow
                : HorizontalWrapMode.Wrap;
            text.verticalOverflow = element.AutoSize == Schema.TextAutoSize.None ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
            text.alignment = (element.VAlign, element.Align) switch
            {
                (Schema.VAlign.Middle, Schema.Align.Center) => TextAnchor.MiddleCenter,
                (Schema.VAlign.Middle, Schema.Align.Right) => TextAnchor.MiddleRight,
                (Schema.VAlign.Middle, _) => TextAnchor.MiddleLeft,
                (Schema.VAlign.Bottom, Schema.Align.Center) => TextAnchor.LowerCenter,
                (Schema.VAlign.Bottom, Schema.Align.Right) => TextAnchor.LowerRight,
                (Schema.VAlign.Bottom, _) => TextAnchor.LowerLeft,
                (_, Schema.Align.Center) => TextAnchor.UpperCenter,
                (_, Schema.Align.Right) => TextAnchor.UpperRight,
                _ => TextAnchor.UpperLeft,
            };
            if (element.Font is { } font)
            {
                if (TryResolve(font, owner.PackageId, out var fontResource) && fontResource.Type == Schema.ResourceKind.Font)
                    text.bitmapFont = ImportFont(fontResource);
                else
                    text.fontNames = font;
            }
            // 运行时字体也烘定：通用工程无需业务胶水设 TextField.defaultFont 静态量。
            if (text.bitmapFont == null && string.IsNullOrEmpty(text.fontNames))
                text.fontNames = TextField.defaultFont;
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

        private static IEnumerable<(string Id, string Member)> PageMembers(Schema.Controller controller)
        {
            // 两个页名经 Identifier 折叠后可能相同（如 "my page" 与 "my-page"），去重避免枚举成员重名编译失败。
            var used = new HashSet<string>();
            foreach (var (id, name) in controller.Pages)
            {
                var member = Identifier(name == "" ? id : name);
                var unique = member;
                for (var i = 2; !used.Add(unique); i++)
                    unique = $"{member}_{i}";
                yield return (id, unique);
            }
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

        // 编辑器期查找、序列化进 prefab；运行时组件不做 Shader.Find。
        private static Shader GrayscaleShader => Shader.Find("NanamiUI/UI Grayscale");
        private static Shader ColorMatrixShader => Shader.Find("NanamiUI/UI ColorMatrix");

        private static Color ParseColor(string value)
        {
            ColorUtility.TryParseHtmlString(value.Length == 9 ? $"#{value[3..]}{value[1..3]}" : value, out var color);
            return color;
        }

        // C# 关键字：任意合法 FairyGUI 名字都可能撞上（如 button 控制器的 "checked" 页），需转义。
        private static readonly HashSet<string> Keywords = new()
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
            "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
            "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc",
            "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
            "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        };

        private static string Identifier(string name)
        {
            var id = new string((name ?? "").Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
            return id.Length == 0 || char.IsDigit(id[0]) || Keywords.Contains(id) ? "_" + id : id;
        }

        private static string Field(string name) => $"m_{Identifier(name)}";

        private static string AssetPath(string file) =>
            $"{OutputRoot}/Assets/{Path.GetRelativePath($"{UiRoot}/assets", file).Replace('\\', '/')}";

        private static string SpritePath(Resource resource) =>
            resource.Type == Schema.ResourceKind.MovieClip ? FramePath(AssetPath(resource.File), 0) : AssetPath(resource.File);

        private static string ScriptPath(string file) =>
            Path.ChangeExtension($"{OutputRoot}/Scripts/{Path.GetRelativePath($"{UiRoot}/assets", file).Replace('\\', '/')}", ".cs").Replace('\\', '/');

        private static CommonSettings Settings() =>
            _settings ??= JsonUtility.FromJson<CommonSettings>(File.ReadAllText($"{UiRoot}/settings/Common.json"));

        // 游戏 UIConfig.defaultFont（运行时字体，不在 FairyGUI 工程文件里）：优先 NanamiUISettings 覆盖，
        // 否则回退 Common.json 的设计期字体首个族名。
        private static string DefaultFont()
        {
            if (Config() is { defaultFont: { Length: > 0 } font })
                return font;
            var designFont = Settings().font;
            return string.IsNullOrEmpty(designFont) ? "Arial" : designFont.Split(',')[0].Trim();
        }

        private static bool _configLoaded;
        private static NanamiUISettings _config;
        private static NanamiUISettings Config()
        {
            if (_configLoaded)
                return _config;
            _configLoaded = true;
            var guid = AssetDatabase.FindAssets("t:NanamiUISettings").FirstOrDefault();
            if (guid != null)
                _config = AssetDatabase.LoadAssetAtPath<NanamiUISettings>(AssetDatabase.GUIDToAssetPath(guid));
            return _config;
        }

        [Serializable]
        private class CommonSettings
        {
            public int fontSize;
            public string textColor;
            public string font;
            public ScrollBars scrollBars;
        }

        [Serializable]
        private class ScrollBars
        {
            public string vertical;
            public string horizontal;
            public string defaultDisplay;
        }
    }
}
