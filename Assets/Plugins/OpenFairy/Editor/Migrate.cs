using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DG.Tweening;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UI;
using ZLinq;
#if FAIRYGUI_TMPRO
using TMPro;
#endif
#if FAIRYGUI_TMPRO
using TextComponent = TMPro.TextMeshProUGUI;
#else
using TextComponent = OpenFairy.UGUI.TextField;
#endif
using Object = UnityEngine.Object;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Editor
{
    // 标记在 Migrate 完成后自动执行的静态无参方法（工程特有收尾，不污染通用导出器）。
    [AttributeUsage(AttributeTargets.Method)]
    public class MigratePostProcessAttribute : Attribute
    {
    }

    public static partial class Migrate
    {
        private static string UiRoot => MigrationSettings.instance.sourceDirectory;
        private static string OutputRoot => MigrationSettings.instance.outputDirectory;
        private const string Pending = "OpenFairy.Migrate.Pending";

        private class Resource
        {
            public string Id;
            public Schema.ResourceKind Type;
            public string File;
            public string Package;
            public string PackageId;
            public Schema.ImageScale Scale;
            public string Scale9Grid;
            public string Texture;
            public bool Exported;
            public int BuildLevel;
            private Schema.Component _componentXml;

            public Schema.Component ComponentXml => _componentXml ??= Schema.Component.Load(File);
        }

        private class ControllerData
        {
            public int Index;
            public Type PageType;
            public string[] PageIds;
            public Schema.Controller Source;
            public object[] PageValues;
            public int Selected;
            public List<object> Gears = new();
            public List<object> Actions = new();
            public object Value;

            public string FieldName;
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
        private static readonly Dictionary<Resource, Font> DynamicFonts = new();
        private static readonly Dictionary<Resource, AudioClip> Sounds = new();
        private static readonly Dictionary<string, GameObject> Prefabs = new();
        private static CommonSettings _settings;
        private static CodeGenerationSettings _codeGenerationSettings;

        [MenuItem("Tools/OpenFairy/Migrate %e")]
        public static void Execute()
        {
            EditorApplication.delayCall -= Execute;
            Resources.Clear();
            MovieClips.Clear();
            FontAssets.Clear();
            DynamicFonts.Clear();
            Sounds.Clear();
            Prefabs.Clear();
            IndexSources();

            _settings = null;
            _codeGenerationSettings = null;
            TextField.defaultFont = DefaultFont(); // 游戏 UIConfig.defaultFont：与运行时一致才能保证烘焙排版一致

            var components = new List<Resource>();
            var images = new HashSet<Resource>();
            // Build the configured tooltip before consumers, including an unexported template.
            if (!string.IsNullOrEmpty(Settings().tipsRes))
                Collect(Resolve(Settings().tipsRes, null), components, images);
            // 默认滚动条是运行时 UIConfig 注入，任何 displayList 都不引用它，必须显式补建。
            foreach (var bar in new[] { Settings().scrollBars?.vertical, Settings().scrollBars?.horizontal })
                if (TryResolveScrollBar(bar, out var barRes))
                    Collect(barRes, components, images);
            var prerequisiteCount = components.Count;
            // 导出所有 exported 组件；
            // Collect 会按依赖顺序把它们引用到的（可能未导出、可能跨包的）组件一并带出。
            foreach (var entry in Resources.Values.AsValueEnumerable().Where(r => r.Type == Schema.ResourceKind.Component
                && r.Exported && WithinAssets(UiRoot, r.File, out _)).ToArray())
                Collect(entry, components, images);

            foreach (var image in images)
                ImportImage(image);
            if (!string.IsNullOrEmpty(Settings().font) && TryResolve(Settings().font, null, out var defaultFont)
                && defaultFont.Type == Schema.ResourceKind.Font)
            {
                if (defaultFont.File.EndsWith(".fnt", StringComparison.OrdinalIgnoreCase)) ImportFont(defaultFont);
                else ImportDynamicFont(defaultFont);
            }
            // 字体/声音也提前导入：构建 prefab 中途导入资产会触发批量重载，把已赋值的引用打成 null
            foreach (var component in components)
                foreach (Match match in RuntimeResourceReferences(component.File))
                    if (TryResolve(match.Value, component.PackageId, out var dep))
                    {
                        if (dep.Type == Schema.ResourceKind.Font)
                        {
                            if (dep.File.EndsWith(".fnt", StringComparison.OrdinalIgnoreCase)) ImportFont(dep);
                            else ImportDynamicFont(dep);
                        }
                        else if (dep.Type == Schema.ResourceKind.Sound)
                            ImportSound(dep);
                    }
            if (!string.IsNullOrEmpty(Settings().buttonClickSound))
                LoadButtonSound(Settings().buttonClickSound, null);
            _scriptsChanged = false;
            PrepareLocalization(components);
            GenerateLocaleScripts();
            foreach (var component in components)
                GenerateScript(component);

            AssetDatabase.Refresh();
            // 编译是异步启动的，脚本一有变化就必须等域重载后再构建 prefab，否则会用到旧类型。
            if (_scriptsChanged || EditorApplication.isCompiling)
            {
                SessionState.SetBool(Pending, true);
                return;
            }

            BuildPrefabs(components.GetRange(0, prerequisiteCount));
            BuildPrefabs(components.GetRange(prerequisiteCount, components.Count - prerequisiteCount));
            SaveLocaleAssets($"{OutputRoot}/Locales");
            AssetDatabase.SaveAssets();
            // 通用导出器不认识任何具体工程；工程特有的收尾（如示例脚本挂载）通过此扩展点自行注册。
            foreach (var method in TypeCache.GetMethodsWithAttribute<MigratePostProcessAttribute>())
                method.Invoke(null, null);
            Debug.Log($"OpenFairy migrated {components.Count} components to {OutputRoot}.");
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (!SessionState.GetBool(Pending, false))
                return;
            SessionState.SetBool(Pending, false);
            EditorApplication.delayCall -= Execute;
            EditorApplication.delayCall += Execute;
        }

        private static void IndexPackage(string packagePath)
        {
            ClassNames.Clear();
            var package = Schema.Package.Load(packagePath);
            var packageName = Path.GetFileName(Path.GetDirectoryName(packagePath));

            foreach (var resource in package.Resources)
                Resources[package.Id + resource.Id] = new Resource
                {
                    Id = resource.Id,
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

        private static MatchCollection RuntimeResourceReferences(string file)
        {
            var xml = FairyXml.LoadPublishedComponentXml(file);
            xml.Attribute("designImage")?.Remove();
            return Regex.Matches(xml.ToString(SaveOptions.DisableFormatting), @"ui://[\w/]+");
        }

        private static void Collect(Resource component, List<Resource> components, HashSet<Resource> images)
        {
            if (components.Contains(component))
                return;

            var level = 0;
            void Include(Resource dependency)
            {
                Collect(dependency, components, images);
                level = Math.Max(level, dependency.BuildLevel + 1);
            }
            var xml = component.ComponentXml;
            foreach (var child in xml.DisplayList)
            {
                var src = child.Source;
                if (src != null && TryResolve(src, component.PackageId, out var dep))
                {
                    if (dep.Type == Schema.ResourceKind.Component)
                        Include(dep);
                    else if (dep.Type is Schema.ResourceKind.Image or Schema.ResourceKind.MovieClip)
                        images.Add(dep);
                }
                if (child.ComboBox?.Dropdown is { } childDropdown && TryResolve(childDropdown, component.PackageId, out var childDropdownResource)
                    && childDropdownResource.Type == Schema.ResourceKind.Component)
                    Include(childDropdownResource);
                if (child.DefaultItem is { } defaultItem && TryResolve(defaultItem, component.PackageId, out var item))
                    Include(item);
                foreach (var entry in child.Items)
                    if (entry.Url is { } itemUrl && TryResolve(itemUrl, component.PackageId, out var itemResource))
                        Include(itemResource);
                foreach (var uri in child.ScrollBarRes ?? Array.Empty<string>())
                    if (TryResolveScrollBar(uri, out var bar)) Include(bar);
            }
            foreach (var uri in xml.ScrollBarRes ?? Array.Empty<string>())
                if (TryResolveScrollBar(uri, out var bar)) Include(bar);
            // ComboBox 的 dropdown 组件只在根 <ComboBox dropdown> 属性引用（displayList 里没有），单独跟随。
            if (xml.ComboBox?.Dropdown is { } dropdown
                && TryResolve(dropdown, component.PackageId, out var dropdownRes) && dropdownRes.Type == Schema.ResourceKind.Component)
                Include(dropdownRes);
            // 覆盖 icon 属性、富文本内嵌图、动效 Sound 之外的一切 ui:// 引用。
            // 编辑器源 XML 里的引用一律是 hex id（ui://<pkgId><itemId>）；命名引用（ui://Pkg/Name）只出现在运行时代码、这里读不到。
            // token 用 [\w/] 兜住命名形式的完整串（TryResolve 解析不到就跳过），避免在 '/' 处截断成错误的部分匹配。
            foreach (Match match in RuntimeResourceReferences(component.File))
                if (TryResolve(match.Value, component.PackageId, out var embedded) && embedded.Type is Schema.ResourceKind.Image or Schema.ResourceKind.MovieClip)
                    images.Add(embedded);
            component.BuildLevel = level;
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
            var bytes = File.ReadAllBytes(image.File);
            var border = Vector4.zero;
            if (image.Scale9Grid != null)
            {
                var texture = new Texture2D(2, 2);
                texture.LoadImage(bytes);
                var grid = image.Scale9Grid.Split(',').AsValueEnumerable().Select(int.Parse).ToArray();
                border = new Vector4(grid[0], texture.height - grid[1] - grid[3], texture.width - grid[0] - grid[2], grid[1]);
                Object.DestroyImmediate(texture);
            }
            if (image.Scale == Schema.ImageScale.Tile)
                bytes = TrimTransparent(bytes); // Match the authored atlas's transparent-edge trimming.
            ImportSprite(target, image.Scale == Schema.ImageScale.Tile, border, WriteFileIfChanged(target, bytes));
        }

        private static bool WriteFileIfChanged(string target, byte[] bytes)
        {
            if (File.Exists(target) && File.ReadAllBytes(target).AsSpan().SequenceEqual(bytes)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllBytes(target, bytes);
            return true;
        }

        private static void ImportSprite(string target, bool tile = false, Vector4 border = default, bool contentChanged = false)
        {
            var importer = AssetImporter.GetAtPath(target) as TextureImporter;
            if (importer == null || contentChanged)
            {
                AssetDatabase.ImportAsset(target);
                importer = (TextureImporter)AssetImporter.GetAtPath(target);
            }
            var wrap = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            var changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.mipmapEnabled || !importer.alphaIsTransparency
                || importer.npotScale != TextureImporterNPOTScale.None
                || importer.wrapMode != wrap
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.spriteBorder != border;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = wrap;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spriteBorder = border;
            }
            changed |= SetMinimumMaxTextureSize(importer);
            if (changed) importer.SaveAndReimport();
        }

        private static bool SetMinimumMaxTextureSize(TextureImporter importer)
        {
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            var maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(width, height)), 32, 16384);
            var changed = importer.maxTextureSize != maxTextureSize;
            if (changed) importer.maxTextureSize = maxTextureSize;
            // Keep each platform's format and other settings; only correct its existing size override.
            var serializedImporter = new SerializedObject(importer);
            var platformSettings = serializedImporter.FindProperty("m_PlatformSettings");
            if (platformSettings != null && platformSettings.isArray)
                for (var i = 0; i < platformSettings.arraySize; i++)
                {
                    var settings = platformSettings.GetArrayElementAtIndex(i);
                    var overridden = settings.FindPropertyRelative("m_Overridden");
                    var platformMaxSize = settings.FindPropertyRelative("m_MaxTextureSize");
                    if (overridden?.boolValue == true && platformMaxSize != null && platformMaxSize.intValue != maxTextureSize)
                        platformMaxSize.intValue = maxTextureSize;
                }
            return serializedImporter.ApplyModifiedPropertiesWithoutUndo() || changed;
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
                var target = FramePath(basePath, i);
                var changed = WriteFileIfChanged(target, canvas.EncodeToPNG());
                Object.DestroyImmediate(canvas);
                ImportSprite(target, contentChanged: changed);
            }
            foreach (var image in images)
                Object.DestroyImmediate(image);

            MovieClips[movieClip] = new MovieClipData
            {
                Size = new Vector2(width, height),
                Interval = I32(7) / 1000f,
                AddDelays = frames.AsValueEnumerable().Select(frame => frame.AddDelay / 1000f).ToArray(),
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
            var path = ScriptPath(component);
            PreserveScriptGuid(component, path);
            foreach (var controller in component.ComponentXml.Controllers)
                DeleteScript($"{Path.GetDirectoryName(path)}/{ClassName(component)}_{ControllerEnumName(controller.Name)}.cs".Replace('\\', '/'));
            WriteScript(path, ScriptCode(component));
        }

        private static string ScriptCode(Resource component)
        {
            var xml = component.ComponentXml;
            var name = ClassName(component);
            var names = new GeneratedMembers(xml, name);
            var fields = new List<string>();
            var controllerBindings = new List<(int Index, string Type, string Field)>();

            for (var controllerIndex = 0; controllerIndex < xml.Controllers.Length; controllerIndex++)
            {
                var controller = xml.Controllers[controllerIndex];
                var cname = names.controllerEnums[controller];
                var field = names.controllerFields[controller];
                fields.Add(EnumDeclaration(cname));
                fields.Add("        {");
                foreach (var page in PageMembers(controller))
                    fields.Add($"            {page.Member},");
                fields.Add("        }");
                if (controller.Name == "button" && xml.Extension is Schema.ComponentExtension.Button or Schema.ComponentExtension.ComboBox)
                    fields.Add($"        public ref Controller<{cname}> {field} => ref controller;");
                else
                    fields.Add($"        public Controller<{cname}> {field};");
                controllerBindings.Add((controllerIndex, cname, field));
            }

            // 字段可能与这些方法的参数同名（如名为 page/controller 的控制器），一律经 this 访问。
            if (controllerBindings.Count > 0)
            {
                fields.Add("");
                fields.Add("        protected override int GetControllerPage(int controller) => controller switch");
                fields.Add("        {");
                foreach (var binding in controllerBindings)
                    fields.Add($"            {binding.Index} => (int)this.{binding.Field}.page,");
                fields.Add("            _ => -1,");
                fields.Add("        };");
                fields.Add("");
                fields.Add("        protected override bool IsControllerChanging(int controller) => controller switch");
                fields.Add("        {");
                foreach (var binding in controllerBindings)
                    fields.Add($"            {binding.Index} => this.{binding.Field}.changing,");
                fields.Add("            _ => false,");
                fields.Add("        };");
                fields.Add("");
                fields.Add("        protected override void SetControllerPage(int controller, int page)");
                fields.Add("        {");
                fields.Add("            switch (controller)");
                fields.Add("            {");
                foreach (var binding in controllerBindings)
                    fields.Add($"                case {binding.Index}: this.{binding.Field}.page = ({binding.Type})page; break;");
                fields.Add("            }");
                fields.Add("        }");
                fields.Add("");
                fields.Add("        protected override void InitializeControllers()");
                fields.Add("        {");
                foreach (var binding in controllerBindings)
                    fields.Add($"            this.{binding.Field}.Initialize();");
                fields.Add("        }");
            }

            foreach (var (child, field) in PublishedChildFields(xml, name))
            {
                var fieldType = FieldType(child, component.PackageId);
                if (fieldType != null)
                    fields.Add($"        public {fieldType} {field};");
            }

            foreach (var transition in xml.Transitions)
            {
                var field = names.transitionFields[transition];
                fields.Add($"        public global::OpenFairy.UGUI.Transition {field};");
                var hooks = TransitionHooks(xml, transition);
                if (hooks.Count == 0) continue;
                var hookType = names.hookTypes[transition];
                fields.Add($"        public enum {hookType} {{ {string.Join(", ", hooks.AsValueEnumerable().Select(hook => hook.Name).ToArray())} }}");
                fields.Add($"        public void {names.hookMethods[transition]}({hookType} label, global::System.Action callback)");
                fields.Add("        {");
                fields.Add("            switch (label)");
                fields.Add("            {");
                foreach (var hook in hooks)
                    fields.Add($"                case {hookType}.{hook.Name}: {field}.SetHook({hook.Item}, {(hook.End ? "true" : "false")}, callback); break;");
                fields.Add("            }");
                fields.Add("        }");
            }

            // FairyGUI 的 GButton/GComboBox 状态控制器固定名为 "button"（GetController("button")），不靠位置。
            // 无该控制器时补一个占位 enum，使 Button/ComboBox 仍是可交互的泛型面（Button<button>/ComboBox<button>），
            // 而不是退化成 Component 丢掉点击/下拉。占位 enum 是嵌套类型 `button`，与"button"子节点的 m_button 字段
            // 不同名（type vs m_ 前缀字段），不冲突——Dropdown（有 button 子节点、无 button 控制器、但有 dropdown 资源）正属此例。
            var buttonController = xml.Controllers.AsValueEnumerable().FirstOrDefault(c => c.Name == "button");
            var isButtonFamily = xml.Extension is Schema.ComponentExtension.Button or Schema.ComponentExtension.ComboBox;
            if (isButtonFamily && buttonController == null)
                fields.InsertRange(0, new[]
                {
                    EnumDeclaration(names.buttonEnum),
                    "        {",
                    "            up,",
                    "        }",
                });
            var stateEnum = names.buttonEnum;
            if (isButtonFamily && buttonController != null)
                fields.AddRange(StatePageCode(buttonController, stateEnum));
            var baseType = xml.Extension switch
            {
                Schema.ComponentExtension.Button => $"global::OpenFairy.UGUI.Button<{name}.{stateEnum}>",
                Schema.ComponentExtension.ComboBox => $"global::OpenFairy.UGUI.ComboBox<{name}.{stateEnum}>",
                Schema.ComponentExtension.ProgressBar => "global::OpenFairy.UGUI.ProgressBar",
                Schema.ComponentExtension.Slider => "global::OpenFairy.UGUI.Slider",
                Schema.ComponentExtension.Label => "global::OpenFairy.UGUI.Label",
                _ => "global::OpenFairy.UGUI.Component",
            };
            var code = new StringBuilder();
            if (xml.Controllers.Length > 0)
                code.AppendLine("using OpenFairy.UGUI;");
            code.AppendLine("using UnityEngine;");
            code.AppendLine("using UnityEngine.UI;");
            if (xml.DisplayList.AsValueEnumerable().Any(child => child.Kind is Schema.DisplayKind.Text or Schema.DisplayKind.RichText or Schema.DisplayKind.InputText))
            {
                code.AppendLine("#if FAIRYGUI_TMPRO");
                code.AppendLine("using OpenFairyTextComponent = TMPro.TextMeshProUGUI;");
                code.AppendLine("using OpenFairyInputComponent = TMPro.TMP_InputField;");
                code.AppendLine("#else");
                code.AppendLine("using OpenFairyTextComponent = OpenFairy.UGUI.TextField;");
                code.AppendLine("using OpenFairyInputComponent = OpenFairy.UGUI.TextInput;");
                code.AppendLine("#endif");
            }
            code.AppendLine();
            code.AppendLine($"namespace {GeneratedNamespace(component)}");
            code.AppendLine("{");
            code.AppendLine($"    public partial class {name} : {baseType}");
            code.AppendLine("    {");
            foreach (var line in fields)
                code.AppendLine(line);
            code.AppendLine(LocalizationCode(component));
            code.AppendLine("    }");
            code.AppendLine("}");

            return code.ToString();
        }

        // 复刻 GButton.SetState 按页名选页：生成期把各视觉状态静态映射到 "button" 控制器的首个同名页。
        private static IEnumerable<string> StatePageCode(Schema.Controller controller, string stateEnum)
        {
            var members = PageMembers(controller).AsValueEnumerable().Select(page => page.Member).ToArray();
            yield return "";
            yield return $"        protected override {stateEnum}? StatePage(VisualState state) => state switch";
            yield return "        {";
            foreach (var (state, page) in new[] { ("Up", "up"), ("Down", "down"), ("Over", "over"),
                ("SelectedOver", "selectedOver"), ("Disabled", "disabled"), ("SelectedDisabled", "selectedDisabled") })
            {
                var index = Array.FindIndex(controller.Pages, authored => authored.Name == page);
                if (index >= 0)
                    yield return $"            VisualState.{state} => {stateEnum}.{members[index]},";
            }
            yield return "            _ => null,";
            yield return "        };";
        }

        // GeneratedMembers 已避开基类成员名，生成的 enum 不会遮蔽 runtime 成员。
        private static string EnumDeclaration(string name) => $"        public enum {name}";

        private static bool _scriptsChanged;

        private static void PreserveScriptGuid(Resource component, string path)
        {
            if (File.Exists(path))
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path.ChangeExtension(AssetPath(component.File), ".prefab"));
            var script = prefab != null && prefab.GetComponent<Component>() is { } view
                ? MonoScript.FromMonoBehaviour(view)
                : null;
            var previousPath = script != null ? AssetDatabase.GetAssetPath(script) : null;
            if (string.IsNullOrEmpty(previousPath))
                return;
            if (!previousPath.StartsWith($"{OutputRoot}/Scripts/", StringComparison.Ordinal))
                throw new InvalidOperationException($"OpenFairy cannot rename a script outside its generated directory: {previousPath}");

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var error = AssetDatabase.MoveAsset(previousPath, path);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"OpenFairy failed to rename {previousPath} to {path}: {error}");
            _scriptsChanged = true;
        }

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
            // A current component can own the former standalone controller-enum path.
            if (Resources.Values.AsValueEnumerable().Any(resource => resource.Type == Schema.ResourceKind.Component && ScriptPath(resource) == path))
                return;
            AssetDatabase.DeleteAsset(path);
            _scriptsChanged = true;
        }

        private static string FieldType(Schema.Display child, string packageId) => child.Kind switch
        {
            Schema.DisplayKind.Image => "global::UnityEngine.UI.Image",
            Schema.DisplayKind.Graph when child.Type == Schema.ShapeType.None => "global::UnityEngine.RectTransform",
            Schema.DisplayKind.Graph => "global::OpenFairy.UGUI.Graph",
            Schema.DisplayKind.Text or Schema.DisplayKind.RichText or Schema.DisplayKind.InputText => TextFieldType(child),
            Schema.DisplayKind.Loader => "global::OpenFairy.UGUI.Loader",
            Schema.DisplayKind.Component when TryResolve(child.Source, packageId, out var dep) =>
                $"global::{GeneratedNamespace(dep)}.{ClassName(dep)}",
            Schema.DisplayKind.Component => null,
            _ when IsMovieClip(child, packageId) => "global::OpenFairy.UGUI.MovieClip",
            _ => "global::UnityEngine.RectTransform",
        };

        private static string TextFieldType(Schema.Display child) =>
            child.Input ? "OpenFairyInputComponent" : "OpenFairyTextComponent";

        private static bool IsMovieClip(Schema.Display child, string packageId) =>
            child.Source != null && TryResolve(child.Source, packageId, out var resource) && resource.Type == Schema.ResourceKind.MovieClip;

        private static void BuildPrefab(Resource component)
        {
            var root = CreatePrefab(component);
            try
            {
                var path = Path.ChangeExtension(AssetPath(component.File), ".prefab");
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved || prefab == null)
                    throw new InvalidOperationException($"OpenFairy failed to save prefab '{path}'.");
                Prefabs[path] = prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject CreatePrefab(Resource component)
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

                var componentType = FindType($"{GeneratedNamespace(component)}.{ClassName(component)}");
                var names = new GeneratedMembers(xml, ClassName(component));
                var comp = (Component)root.AddComponent(componentType);

                var controllers = new Dictionary<string, ControllerData>();
                foreach (var element in xml.Controllers)
                {
                    var name = element.Name;
                    var pages = PageMembers(element).AsValueEnumerable().ToArray();
                    var pageType = componentType.GetNestedType(names.controllerEnums[element]);
                    controllers[element.Name] = new ControllerData
                    {
                        Index = controllers.Count,
                        FieldName = names.controllerFields[element],
                        PageType = pageType,
                        PageIds = pages.AsValueEnumerable().Select(page => page.Id).ToArray(),
                        Source = element,
                        PageValues = pages.AsValueEnumerable().Select(page => Enum.Parse(pageType, page.Member)).ToArray(),
                        Selected = element.Selected,
                    };
                }

                var children = new List<(Schema.Display Xml, GameObject Go)>();
                foreach (var element in xml.DisplayList)
                    children.Add((element, CreateChild(element, rt, component, controllers)));

                foreach (var (element, go) in children)
                    if (element.Kind == Schema.DisplayKind.Group)
                    {
                        foreach (var (member, memberGo) in children)
                            if (member.Group == element.Id)
                                ReparentPreservingRect((RectTransform)memberGo.transform, (RectTransform)go.transform);
                        GroupLayoutBuilder.Configure(go, element);
                    }

                if (xml.Mask is { } mask)
                {
                    var maskGo = children.AsValueEnumerable().First(child => child.Xml.Id == mask).Go;
                    maskGo.AddComponent<Mask>().showMaskGraphic = false;
                    foreach (var (_, childGo) in children)
                        if (childGo != maskGo && childGo.transform.parent == rt)
                            ReparentPreservingRect((RectTransform)childGo.transform, (RectTransform)maskGo.transform);
                }

                var relationSpace = rt;
                if (xml.Overflow == Schema.Overflow.Scroll)
                {
                    var view = BuildScrollView(root, xml, rt.sizeDelta);
                    relationSpace = view.Content;
                    foreach (var (_, childGo) in children)
                        if (childGo.transform.parent == rt)
                            ReparentPreservingRect((RectTransform)childGo.transform, view.Content, false);
                    SetGrips(view, ContentBounds(children));
                }

                var byId = new Dictionary<string, (Schema.Display Xml, GameObject Go)>();
                foreach (var (element, go) in children)
                    byId.TryAdd(element.Id, (element, go));
                foreach (var (element, go) in children)
                    BuildRelations((RectTransform)root.transform, relationSpace, element, go, byId);

                BuildRelations(rt, rt, new Schema.Display { Relations = xml.Relations }, root, byId);

                BuildTransitions(root, xml, byId, component.PackageId);
                BuildActions(component, comp, controllers, byId);
                foreach (var (element, go) in children)
                    if (element.PageController is { } page && controllers.TryGetValue(page, out var data)
                        && go.TryGetComponent<ScrollPane>(out var pane))
                    {
                        pane.pageOwner = comp;
                        pane.pageController = data.Index;
                        pane.pageCount = data.PageValues.Length;
                        var type = typeof(GearScrollPage<>).MakeGenericType(data.PageType);
                        var gear = Activator.CreateInstance(type);
                        type.GetField("target").SetValue(gear, go);
                        type.GetField("pane").SetValue(gear, pane);
                        var pages = Array.CreateInstance(data.PageType, data.PageValues.Length);
                        for (var i = 0; i < pages.Length; i++) pages.SetValue(data.PageValues[i], i);
                        type.GetField("pages").SetValue(gear, pages);
                        data.Gears.Add(gear);
                    }

                foreach (var (element, go) in children)
                    if (element.SelectionController is { } name && controllers.TryGetValue(name, out var data)
                        && go.TryGetComponent<ListSelection>(out var selection))
                        BuildSelectionController(comp, data, selection);

                // 按钮关联控制器（<Button controller=.. page=..>）：点击换 owner 上该控制器的页，实现 tab/radio 组
                //（复刻 relatedController）；并给该控制器烘 GearButton，使换页（含程序化）反向同步按钮选中态
                //（复刻 GButton.HandleControllerChanged）。须在 BuildController 之前，gear 才进控制器。
                foreach (var (element, go) in children)
                    if (element.Button?.Controller is { } ctrlName && controllers.TryGetValue(ctrlName, out var ctrlData)
                        && go.GetComponent<ButtonBase>() is { } relatedButton)
                    {
                        relatedButton.relatedOwner = comp;
                        relatedButton.relatedController = ctrlData.Index;
                        relatedButton.relatedPage = Array.IndexOf(ctrlData.PageIds, element.Button.Page);
                        if (relatedButton.relatedPage >= 0)
                        {
                            var gearType = typeof(GearButton<>).MakeGenericType(ctrlData.PageType);
                            var gear = Activator.CreateInstance(gearType);
                            gearType.GetField("target").SetValue(gear, go);
                            gearType.GetField("button", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(gear, relatedButton);
                            var pages = Array.CreateInstance(ctrlData.PageType, 1);
                            pages.SetValue(ctrlData.PageValues[relatedButton.relatedPage], 0);
                            gearType.GetField("pages").SetValue(gear, pages);
                            ctrlData.Gears.Add(gear);
                        }
                    }

                foreach (var controller in controllers.Values)
                    BuildController(controller);

                // 复刻 Container.opaque（组件缺省不穿透）：整块矩形可命中，与内部图形无关。
                // uGUI 需要一张透明的 raycast 面盖住根矩形，否则未被图形覆盖处的点击会穿透到下层。
                if (xml.Opaque && root.GetComponent<Graphic>() == null)
                    root.AddComponent<UnityEngine.UI.Image>().color = Color.clear;

                if (comp is ButtonBase buttonComp)
                {
                    buttonComp.sound = LoadButtonSound(Settings().buttonClickSound, null);
                    if (controllers.TryGetValue("button", out var buttonData))
                        comp.GetType().GetField("controller").SetValue(comp, buttonData.Value); // Controller<T> 泛型字段，仅此保留反射
                    buttonComp.titleText = Child(children, "title")?.GetComponent<TextComponent>();
                    buttonComp.iconLoader = IconLoader(Child(children, "icon"));
                    if (buttonComp.iconLoader != null && buttonComp.iconLoader.content == null)
                        buttonComp.icon = buttonComp.iconLoader.sprite;
                    if (xml.Button is { } buttonEl)
                        ConfigureButton(root, buttonEl, component.PackageId);
                    // 复刻 GButton.SetState 的 Dark 效果：作用于直接子级里非文本的可着色对象。
                    if (buttonComp.downEffect == ButtonDownEffect.Dark)
                        buttonComp.downEffectTargets = children.AsValueEnumerable()
                            .Where(child => child.Xml.Kind is Schema.DisplayKind.Image or Schema.DisplayKind.MovieClip
                                or Schema.DisplayKind.Graph or Schema.DisplayKind.Loader)
                            .Select(child => child.Go.GetComponent<Graphic>()).Where(graphic => graphic != null).ToArray();
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
                    BindLabelTitle(label, Child(children, "title")?.transform);
                    label.iconLoader = IconLoader(Child(children, "icon"));
                }
                else if (xml.Extension == Schema.ComponentExtension.ScrollBar)
                    SetupScrollBar(root, children);

                var byName = new Dictionary<string, GameObject>();
                foreach (var (element, field) in PublishedChildFields(xml, ClassName(component)))
                    byName.Add(field, children.AsValueEnumerable().First(child => child.Xml == element).Go);

                foreach (var field in comp.GetType().GetFields())
                {
                    var controller = controllers.Values.AsValueEnumerable().FirstOrDefault(data => data.FieldName == field.Name);
                    if (controller != null)
                        field.SetValue(comp, controller.Value);
                    else if (byName.TryGetValue(field.Name, out var go))
                        field.SetValue(comp, field.FieldType == typeof(RectTransform) ? go.transform : (object)go.GetComponent(field.FieldType));
                }

                foreach (var transition in root.GetComponents<Transition>())
                    comp.GetType().GetField(names.transitionFields[Array.Find(xml.Transitions, item => item.Name == transition.transitionName)]).SetValue(comp, transition);
                comp.controllerChildren = children.AsValueEnumerable().Select(child => child.Go.GetComponent<Component>())
                    .Where(child => child != null).ToArray();
                comp.Initialize();
                BindLocalization(component, comp, byId, controllers);

                var prefabPath = Path.ChangeExtension(AssetPath(component.File), ".prefab");
                Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
                InputEditingBuilder.Bake(root);
                foreach (var fitter in root.GetComponentsInChildren<ContentSizeFitter>())
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)fitter.transform);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                SyncRelations(root);
                return root;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        }

        private static GameObject CreateChild(Schema.Display element, RectTransform parent, Resource owner,
            Dictionary<string, ControllerData> controllers)
        {
            PrepareTextRelations(element);
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
#if FAIRYGUI_TMPRO
                    go = CreateTmpTextElement(name, parent, element, owner);
#else
                    go = NewChild(name, parent, typeof(TextField));
                    var textComponent = go.GetComponent<TextField>();
                    ConfigureText(textComponent, element, owner);
                    if (element.StrokeColor is { } stroke)
                    {
                        var effect = go.AddComponent<TextStroke>();
                        effect.effectColor = ParseColor(stroke);
                        effect.effectDistance = new Vector2(element.StrokeSize, element.StrokeSize);
                        effect.useGraphicAlpha = false;
                    }
                    if (element.ShadowColor is { } shadow)
                    {
                        // FairyGUI 投影是 y 向下的偏移、不随文字透明度变化，与 uGUI Shadow 的顶点流一致。
                        var effect = go.AddComponent<Shadow>();
                        var offset = element.ShadowOffset ?? new Vector2(1, 1);
                        effect.effectColor = ParseColor(shadow);
                        effect.effectDistance = new Vector2(offset.x, -offset.y);
                        effect.useGraphicAlpha = false;
                    }
                    if (element.Input)
                        ConfigureInput(go, textComponent, element);
#endif
                    break;
                }
                case Schema.DisplayKind.Loader:
                {
                    go = NewChild(name, parent, typeof(Loader));
                    var loader = go.GetComponent<Loader>();
                    loader.autoSize = element.LoaderAutoSize;
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
                    else if (url != null && TryResolve(url, owner.PackageId, out content) && content.Type == Schema.ResourceKind.Component)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(content), go.transform);
                        loader.componentContent = instance.GetComponent<Component>();
                        var contentRect = (RectTransform)instance.transform;
                        contentRect.anchorMin = contentRect.anchorMax = contentRect.pivot = new Vector2(0, 1);
                        loader.componentSize = contentRect.rect.size;
                        loader.ShowContent();
                    }
                    else
                        loader.enabled = false;
                    break;
                }
                case Schema.DisplayKind.Component:
                {
                    var dep = Resolve(element.Source, owner.PackageId);
                    // GButton 的 Scale 按下效果在构造时把轴心设为中心，实例未写 pivot 时保留该轴心。
                    if (dep.ComponentXml.Button?.DownEffect == ButtonDownEffect.Scale)
                        element.Pivot ??= new Vector2(.5f, .5f);
                    go = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(dep), parent);
                    go.name = name;
                    size ??= ((RectTransform)go.transform).sizeDelta;
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
            // 复刻 GObject.SetSize 的 restrictSize：最小尺寸约束内容自适应，文字按最大宽度换行。
            if (element.RestrictSize is { } restrict)
            {
                if (restrict[0] > 0 || restrict[2] > 0)
                {
                    var minimum = go.AddComponent<LayoutElement>();
                    minimum.minWidth = restrict[0];
                    minimum.minHeight = restrict[2];
                }
#if !FAIRYGUI_TMPRO
                if (go.TryGetComponent(out TextField restricted))
                    restricted.maxWidth = restrict[1];
#endif
            }
            if (go.GetComponent<Loader>() is { autoSize: true } autoLoader)
                autoLoader.SetNativeSize();
            if (go.GetComponent<TextField>() is { } createdText)
                createdText.RebuildImages(); // 依赖最终 rect 的排版，须在 SetRect 之后烘焙
            ConfigureTextLayout(go, element);
            ApplyElement(go, element, owner);
            if (element.Kind == Schema.DisplayKind.Component)
            {
                ConfigureInstanceControllers(go.GetComponent<Component>(), Resolve(element.Source, owner.PackageId).ComponentXml, element.Controller);
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
            }
            if (!element.Visible)
                go.SetActive(false);

            object display = null, display2 = null;
            foreach (var gearXml in element.Gears)
            {
                var kind = gearXml.Kind;
                var controller = controllers[gearXml.Controller];
                DropStalePages(controller, gearXml); // pages 与各 kind 分支的 values 平行，须在解析前同步剔除陈旧槽
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
                    gearType.GetField("values").SetValue(gear, gearXml.Values.Split('|').AsValueEnumerable().Select(value => value == "-" ? defaultValue : Convert(value)).ToArray());
                    gearType.GetField("defaultValue").SetValue(gear, defaultValue);
                }
                else if (kind == Schema.GearKind.Color)
                {
                    var graphic = go.GetComponent<Graphic>();
                    var stroke = go.GetComponent<TextStroke>();
                    (Color Color, Color Stroke) Parse(string value, (Color Color, Color Stroke) fallback)
                    {
                        var parts = value.Split(',');
                        return (ParseColor(parts[0]), parts.Length > 1 ? ParseColor(parts[1]) : fallback.Stroke);
                    }
                    (Color Color, Color Stroke) fallback = (graphic.color, graphic.GetOutlineColor());
                    (Color Color, Color Stroke) def = gearXml.Default is { } defaultValue ? Parse(defaultValue, fallback) : fallback;
                    var source = gearXml.Values.Split('|');
                    var colors = new Color[source.Length];
                    var strokes = new Color[source.Length];
                    for (var i = 0; i < source.Length; i++)
                    {
                        var parsed = source[i] == "-" ? def : Parse(source[i], def);
                        colors[i] = parsed.Color;
                        strokes[i] = parsed.Stroke;
                    }
                    gearType.GetField("values").SetValue(gear, colors);
                    gearType.GetField("defaultValue").SetValue(gear, def.Color);
                    gearType.GetField("strokeValues").SetValue(gear, strokes);
                    gearType.GetField("defaultStroke").SetValue(gear, def.Stroke);
                }
                else if (kind == Schema.GearKind.Look)
                    ConfigureGearLook(gearType, gear, gearXml, go);
                else if (kind == Schema.GearKind.Size)
                    ConfigureGearSize(gearType, gear, gearXml, (RectTransform)go.transform);
                else if (kind == Schema.GearKind.Ani)
                    ConfigureGearAni(gearType, gear, gearXml);
                else if (kind == Schema.GearKind.FontSize)
                {
                    var text = go.GetComponentInChildren<TextComponent>(true);
#if FAIRYGUI_TMPRO
                    var defaultSize = text.enableAutoSizing ? text.fontSizeMax : text.fontSize;
#else
                    var defaultSize = text.fontSize;
#endif
                    var def = gearXml.Default is { } value ? int.Parse(value) : (int)defaultSize;
                    gearType.GetField("values").SetValue(gear, gearXml.Values.Split('|').AsValueEnumerable().Select(value => value == "-" ? def : int.Parse(value)).ToArray());
                    gearType.GetField("defaultValue").SetValue(gear, def);
                }
                else if (kind == Schema.GearKind.Text)
                {
                    var def = gearXml.Default ?? go.GetComponentInChildren<TextComponent>(true).text;
                    gearType.GetField("values").SetValue(gear, gearXml.Values.Split('|').AsValueEnumerable().Select(value => value == "-" ? def : value).ToArray());
                    gearType.GetField("defaultValue").SetValue(gear, def);
                }
                else if (kind == Schema.GearKind.Icon)
                {
                    const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    gearType.GetField("button", Any).SetValue(gear, go.GetComponent<ButtonBase>());
                    gearType.GetField("label", Any).SetValue(gear, go.GetComponent<Label>());
                    gearType.GetField("loader", Any).SetValue(gear, go.GetComponent<Loader>());
                    var def = gearXml.Default is { } value ? LoadSprite(value, owner.PackageId) : null;
                    gearType.GetField("values").SetValue(gear, gearXml.Values.Split('|').AsValueEnumerable().Select(value => value == "-" ? def : LoadSprite(value, owner.PackageId)).ToArray());
                    gearType.GetField("defaultValue").SetValue(gear, def);
                }
                else if (kind == Schema.GearKind.Display)
                    display = gear;
                else if (kind == Schema.GearKind.Display2)
                {
                    display2 = gear;
                    gearType.GetField("condition").SetValue(gear, (DisplayCondition)gearXml.Condition);
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

        private static void BuildSelectionController(Component owner, ControllerData data, ListSelection selection)
        {
            selection.selectionOwner = owner;
            selection.selectionController = data.Index;
            selection.selectionPageCount = data.PageValues.Length;
            var gearType = typeof(GearListSelection<>).MakeGenericType(data.PageType);
            var gear = Activator.CreateInstance(gearType);
            gearType.GetField("target").SetValue(gear, selection.gameObject);
            gearType.GetField("selection").SetValue(gear, selection);
            var pages = Array.CreateInstance(data.PageType, data.PageValues.Length);
            for (var i = 0; i < pages.Length; i++) pages.SetValue(data.PageValues[i], i);
            gearType.GetField("pages").SetValue(gear, pages);
            data.Gears.Add(gear);
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
            controllerType.GetMethod("Bake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(data.Value, new[] { data.PageValues[data.Selected] });
            var actions = Array.CreateInstance(typeof(ControllerAction<>).MakeGenericType(data.PageType), data.Actions.Count);
            for (var i = 0; i < data.Actions.Count; i++)
                actions.SetValue(data.Actions[i], i);
            controllerType.GetField("actions").SetValue(data.Value, actions);
        }

        private static void BuildActions(Resource owner, Component comp, Dictionary<string, ControllerData> controllers,
            Dictionary<string, (Schema.Display Xml, GameObject Go)> byId)
        {
            var transitions = comp.GetComponents<Transition>();
            foreach (var source in controllers.Values)
                foreach (var xml in source.Source.Actions)
                {
                    object action;
                    if (xml.Type == Schema.ControllerActionType.ChangePage)
                    {
                        var target = comp;
                        var targetXml = owner.ComponentXml;
                        if (!string.IsNullOrEmpty(xml.ObjectId))
                        {
                            if (!byId.TryGetValue(xml.ObjectId, out var child) || child.Xml.Source == null
                                || !TryResolve(child.Xml.Source, owner.PackageId, out var resource)
                                || resource.Type != Schema.ResourceKind.Component)
                                continue; // A deleted target is a no-op in the authored action.
                            target = child.Go.GetComponent<Component>();
                            targetXml = resource.ComponentXml;
                        }
                        var controllerIndex = Array.FindIndex(targetXml.Controllers, controller => controller.Name == xml.Controller);
                        if (controllerIndex < 0 || target == comp && controllerIndex == source.Index)
                            continue;
                        var map = ChangePageMap(source.Source, targetXml.Controllers[controllerIndex], xml.TargetPage);
                        var type = typeof(ChangePageAction<>).MakeGenericType(source.PageType);
                        action = Activator.CreateInstance(type);
                        type.GetField("target").SetValue(action, target);
                        type.GetField("controller").SetValue(action, controllerIndex);
                        var pages = Array.CreateInstance(source.PageType, source.PageValues.Length);
                        for (var i = 0; i < pages.Length; i++)
                            pages.SetValue(source.PageValues[i], i);
                        type.GetField("sourcePages").SetValue(action, pages);
                        type.GetField("targetPages").SetValue(action, map);
                    }
                    else
                    {
                        var transition = Array.Find(transitions, value => value.transitionName == xml.Transition);
                        if (transition == null)
                            continue;
                        var type = typeof(PlayTransitionAction<>).MakeGenericType(source.PageType);
                        action = Activator.CreateInstance(type);
                        type.GetField("transition").SetValue(action, transition);
                        type.GetField("playTimes").SetValue(action, xml.Repeat);
                        type.GetField("delay").SetValue(action, xml.Delay);
                        type.GetField("stopOnExit").SetValue(action, xml.StopOnExit);
                    }
                    var baseType = action.GetType().BaseType;
                    baseType.GetProperty("fromPages").SetValue(action, ActionPages(source, xml.FromPage));
                    baseType.GetProperty("toPages").SetValue(action, ActionPages(source, xml.ToPage));
                    source.Actions.Add(action);
                }
        }

        private static Array ActionPages(ControllerData controller, string ids)
        {
            if (string.IsNullOrEmpty(ids))
                return null;
            var indices = ids.Split(',').AsValueEnumerable().Select(id => Array.IndexOf(controller.PageIds, id))
                .Where(index => index >= 0).ToArray();
            var pages = Array.CreateInstance(controller.PageType, indices.Length);
            for (var i = 0; i < indices.Length; i++)
                pages.SetValue(controller.PageValues[indices[i]], i);
            return pages;
        }

        private static int[] ChangePageMap(Schema.Controller source, Schema.Controller target, string targetPage)
        {
            var map = new int[source.Pages.Length];
            for (var i = 0; i < map.Length; i++)
                map[i] = targetPage switch
                {
                    "~1" => i < target.Pages.Length ? i : -1,
                    "~2" => Math.Max(0, Array.FindIndex(target.Pages, page => page.Name == source.Pages[i].Name)),
                    _ => Array.FindIndex(target.Pages, page => page.Id == targetPage),
                };
            return map;
        }

        // gear 的 pages 引用控制器已删的陈旧页 id 时，把该槽连同平行的 values 槽一并剔除——
        // 留 default(T)（= 枚举第 0 页）会让切到第 0 页时误命中陈旧槽、应用已删页的值。
        private static void DropStalePages(ControllerData controller, Schema.Gear gearXml)
        {
            if (gearXml.Pages is not { } ids)
                return;
            var parts = ids.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.AsValueEnumerable().All(id => Array.IndexOf(controller.PageIds, id) >= 0))
                return;
            var values = gearXml.Values?.Split('|');
            var keptIds = new List<string>();
            var keptValues = values != null ? new List<string>() : null;
            for (var i = 0; i < parts.Length; i++)
                if (Array.IndexOf(controller.PageIds, parts[i]) >= 0)
                {
                    keptIds.Add(parts[i]);
                    keptValues?.Add(i < values.Length ? values[i] : "-");
                }
            gearXml.Pages = string.Join(",", keptIds);
            if (keptValues != null)
                gearXml.Values = string.Join("|", keptValues);
        }

        private static Array PageValues(ControllerData controller, string ids)
        {
            var parts = ids?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            var values = Array.CreateInstance(controller.PageType, parts.Length);
            for (var i = 0; i < parts.Length; i++)
                values.SetValue(controller.PageValues[Array.IndexOf(controller.PageIds, parts[i])], i);
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
                // FairyGUI 的 Origin 枚举顺序与 uGUI 不同，按语义逐一映射。
                var origin = element.FillOrigin;
                image.fillOrigin = image.fillMethod switch
                {
                    UnityEngine.UI.Image.FillMethod.Vertical => (int)(origin == 0 ? UnityEngine.UI.Image.OriginVertical.Top : UnityEngine.UI.Image.OriginVertical.Bottom),
                    UnityEngine.UI.Image.FillMethod.Radial90 => (int)new[] { UnityEngine.UI.Image.Origin90.TopLeft, UnityEngine.UI.Image.Origin90.TopRight,
                        UnityEngine.UI.Image.Origin90.BottomLeft, UnityEngine.UI.Image.Origin90.BottomRight }[origin],
                    UnityEngine.UI.Image.FillMethod.Radial180 => (int)new[] { UnityEngine.UI.Image.Origin180.Top, UnityEngine.UI.Image.Origin180.Bottom,
                        UnityEngine.UI.Image.Origin180.Left, UnityEngine.UI.Image.Origin180.Right }[origin],
                    UnityEngine.UI.Image.FillMethod.Radial360 => (int)new[] { UnityEngine.UI.Image.Origin360.Top, UnityEngine.UI.Image.Origin360.Bottom,
                        UnityEngine.UI.Image.Origin360.Left, UnityEngine.UI.Image.Origin360.Right }[origin],
                    _ => origin,
                };
                image.fillClockwise = element.FillClockwise;
                image.fillAmount = element.FillAmount / 100;
            }
            if (image is Image flip && element.Flip != Schema.Flip.None)
            {
                flip.flipX = element.Flip is Schema.Flip.Horizontal or Schema.Flip.Both;
                flip.flipY = element.Flip is Schema.Flip.Vertical or Schema.Flip.Both;
            }
        }

        private static void ConfigureInstanceControllers(Component component, Schema.Component source, string values)
        {
            if (string.IsNullOrEmpty(values))
                return;
            var pairs = values.Split(',');
            for (var i = 0; i < pairs.Length; i += 2)
            {
                var controller = Array.FindIndex(source.Controllers, item => item.Name == pairs[i]);
                if (controller < 0)
                    continue;
                var page = Array.FindIndex(source.Controllers[controller].Pages, item => item.Id == pairs[i + 1]);
                if (page >= 0)
                    component.SetControllerPage(controller, page);
            }
        }

        private static Loader IconLoader(GameObject child)
        {
            if (child == null) return null;
            if (child.TryGetComponent<Loader>(out var loader)) return loader;
            if (child.TryGetComponent<Label>(out var label)) return label.iconLoader;
            return child.GetComponent<ButtonBase>()?.iconLoader;
        }

        private static void ConfigureButton(GameObject go, Schema.Extension buttonXml, string packageId)
        {
            var button = go.GetComponent<ButtonBase>();
            if (button == null)
                return; // 非 button 项（plain component/label 作 list item）：无按钮面可配，跳过
            if (buttonXml.Title is { } title)
                button.title = title;
            if (buttonXml.SelectedTitle is { } selectedTitle)
                button.selectedTitle = selectedTitle;
            if (buttonXml.Icon is { } icon)
                button.icon = LoadSprite(icon, packageId);
            if (buttonXml.SelectedIcon is { } selectedIcon)
            {
                button.selectedIcon = LoadSprite(selectedIcon, packageId);
                button.useSelectedIcon = true;
            }
            if (buttonXml.Mode is { } mode)
                button.mode = mode;
            if (buttonXml.Sound is { } sound) button.sound = LoadButtonSound(sound, packageId);
            if (buttonXml.SoundVolume is { } volume) button.soundVolumeScale = volume;
            if (buttonXml.DownEffect is { } downEffect) button.downEffect = downEffect;
            if (buttonXml.DownEffectValue is { } downEffectValue) button.downEffectValue = downEffectValue;
            if (buttonXml.TitleColor is { } titleColor)
                ConfigureTitleColor(button, ParseColor(titleColor));
            if (buttonXml.TitleFontSize is { } titleFontSize && button.titleText != null)
                SetTitleFontSize(button.titleText, titleFontSize);
            button._selected = buttonXml.Checked && button.mode != ButtonMode.Common; // 烘焙直写（复刻 GButton.selected：Common 忽略选中态），不触发关联控制器
            button.RefreshState();
            if (button.useSelectedIcon) button.RefreshIcon();
        }

        // 复刻 GButton/GLabel.titleFontSize：实例覆盖标题文字字号。
#if FAIRYGUI_TMPRO
        private static void SetTitleFontSize(TMP_Text text, int size) => TextAutoSize.SetFontSize(text, size);
#else
        private static void SetTitleFontSize(UnityEngine.UI.Text text, int size) => text.fontSize = size;
#endif

        private static void ConfigureTitleColor(ButtonBase button, Color color)
        {
            var text = button.titleText;
            if (text == null) return; // A button without a text title has no title color.
            text.color = color;
            foreach (var field in button.GetType().GetFields())
            {
                var type = field.FieldType;
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Controller<>)) continue;
                var controller = field.GetValue(button);
                if (type.GetField("gears").GetValue(controller) is not Array gears) continue;
                var page = type.GetProperty("page").GetValue(controller);
                foreach (var gear in gears)
                {
                    var gearType = gear.GetType();
                    if (!gearType.IsGenericType || gearType.GetGenericTypeDefinition() != typeof(GearColor<>)
                        || (GameObject)gearType.GetField("target").GetValue(gear) != text.gameObject) continue;
                    var pagesField = gearType.GetField("pages");
                    var pages = (Array)pagesField.GetValue(gear);
                    var index = Array.IndexOf(pages, page);
                    var valuesField = gearType.GetField("values");
                    var values = (Color[])valuesField.GetValue(gear);
                    if (index < 0)
                    {
                        index = pages.Length;
                        var expanded = Array.CreateInstance(type.GetGenericArguments()[0], index + 1);
                        Array.Copy(pages, expanded, index); expanded.SetValue(page, index); pagesField.SetValue(gear, expanded);
                        Array.Resize(ref values, index + 1); valuesField.SetValue(gear, values);
                        var strokesField = gearType.GetField("strokeValues");
                        if (strokesField.GetValue(gear) is Color[] strokes)
                        {
                            Array.Resize(ref strokes, index + 1); strokes[index] = text.GetOutlineColor(); strokesField.SetValue(gear, strokes);
                        }
                    }
                    values[index] = color;
                }
            }
        }

        private static void BindLabelTitle(Label label, Transform title)
        {
            label.titleText = title?.GetComponent<TextComponent>();
#if FAIRYGUI_TMPRO
            label.titleInput = title?.GetComponent<TMP_InputField>();
#else
            label.titleInput = title?.GetComponent<TextInput>();
#endif
        }

        private static void ConfigureLabel(GameObject go, Schema.Extension labelXml, string packageId)
        {
            var titleObject = FindChild(go.transform, "title");
#if FAIRYGUI_TMPRO
            var input = titleObject?.GetComponent<TMP_InputField>();
            var titleText = input != null ? input.textComponent : titleObject?.GetComponent<TextComponent>();
            if (input != null)
            {
                if (labelXml.MaxLength is { } maxLength) input.characterLimit = maxLength;
                if (labelXml.Password is { } password) input.contentType = password
                    ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
                if (labelXml.Prompt is { } prompt) ConfigureTmpPrompt(input, prompt);
                if (labelXml.Title is { } inputTitle) input.SetTextWithoutNotify(inputTitle);
            }
#else
            var input = titleObject?.GetComponent<TextInput>();
            UnityEngine.UI.Text titleText = input != null ? input.field.textComponent : titleObject?.GetComponent<TextComponent>();
            if (input != null)
            {
                if (labelXml.MaxLength is { } maxLength) input.maxLength = maxLength;
                if (labelXml.Password is { } password) input.password = password;
                if (labelXml.Prompt is { } prompt && input.field.placeholder is TextField placeholder)
                    placeholder.text = prompt;
                if (labelXml.Title is { } inputTitle) input.text = inputTitle;
            }
#endif
            if (input == null && labelXml.Title is { } title && titleText != null)
                titleText.text = title;
            if (labelXml.TitleColor is { } titleColor && titleText != null)
                titleText.color = ParseColor(titleColor);
            if (labelXml.TitleFontSize is { } titleFontSize && titleText != null)
                SetTitleFontSize(titleText, titleFontSize);
            if (labelXml.Icon is { } icon)
                go.GetComponent<Label>().icon = LoadSprite(icon, packageId);
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
                case Schema.ShapeType.Polyline:
                case Schema.ShapeType.Polygon:
                    shape.kind = element.Type == Schema.ShapeType.Polyline ? Graph.Kind.Polyline : Graph.Kind.Polygon;
                    shape.roundEdge = element.RoundEdge;
                    var values = element.Points.Split(',').AsValueEnumerable().Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                    shape.points = ValueEnumerable.Range(0, values.Length / 2).Select(i => new Vector2(values[i * 2], values[i * 2 + 1])).ToArray();
                    break;
                case Schema.ShapeType.RegularPolygon:
                    shape.kind = Graph.Kind.RegularPolygon;
                    shape.sides = element.Sides;
                    shape.startAngle = element.StartAngle;
                    shape.distances = element.Distances?.Split(',').AsValueEnumerable().Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                    break;
                default:
                    if (element.Corner is { } corner)
                    {
                        shape.kind = Graph.Kind.RoundedRect;
                        var radii = corner.Split(',').AsValueEnumerable().Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                        shape.corners = ValueEnumerable.Range(0, 4).Select(i => radii[Mathf.Min(i, radii.Length - 1)]).ToArray();
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
            movieClip.frames = ValueEnumerable.Range(0, data.FrameCount)
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
            progress.title = Child(children, "title")?.GetComponent<TextComponent>();
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
            slider.title = Child(children, "title")?.GetComponent<TextComponent>();
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
            children.AsValueEnumerable().FirstOrDefault(child => child.Xml.Name == name).Go;

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
            if (go.GetComponent<ButtonBase>() is not { } combo || combo is not IComboBox)
                return;
            const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var type = combo.GetType();
            if (xml.Dropdown is { } dropdown && TryResolve(dropdown, packageId, out var dropRes))
                type.GetField("dropdownPrefab", Any).SetValue(combo, LoadPrefab(dropRes));
            type.GetField("visibleItemCount", Any).SetValue(combo, xml.VisibleItemCount);
            var items = xml.Items;
            if (xml.AutoClearItems || items.Length > 0)
            {
                type.GetField("_items", Any).SetValue(combo, items);
                if (xml.AutoClearItems || xml.Values.Length > 0)
                    type.GetField("_values", Any).SetValue(combo, xml.Values);
                if (items.Length > 0) combo.title = items[0]; // 默认选中项 0
            }
        }

        private static float FloatValue(string value, float defaultValue) =>
            value == null ? defaultValue : float.Parse(value, CultureInfo.InvariantCulture);

        private static void SyncRelations(GameObject go)
        {
            foreach (var relation in go.GetComponentsInChildren<Relation>(true))
                relation.Sync();
        }

        private static void ApplyElement(GameObject go, Schema.Display element, Resource owner)
        {
            var rt = (RectTransform)go.transform;
            if (element.Rotation is { } rotation)
                rt.localEulerAngles = new Vector3(0, 0, -rotation);
            if (element.BlendMode != Schema.BlendMode.Normal && element.Kind is Schema.DisplayKind.Image
                    or Schema.DisplayKind.MovieClip or Schema.DisplayKind.Graph or Schema.DisplayKind.Loader
                    or Schema.DisplayKind.Text or Schema.DisplayKind.RichText or Schema.DisplayKind.InputText)
                foreach (var graphic in go.GetComponentsInChildren<Graphic>(true))
                {
                    var blend = graphic.gameObject.AddComponent<BlendModeEffect>();
                    blend.shader = BlendShader;
                    blend.blendMode = (OpenFairy.UGUI.BlendMode)element.BlendMode;
                }
            // authored alpha 烘进 CanvasGroup（FairyGUI object.alpha 语义）：GearLook/Transition 的 alpha 也走同一通道
            // 且是绝对值——若乘进各子 Graphic 颜色，会与 gear/动效通道相乘（0.5 页值显示成 0.25）。
            if (element.Alpha != null || !element.Touchable)
            {
                if (!go.TryGetComponent(out CanvasGroup group))
                    group = go.AddComponent<CanvasGroup>();
                if (element.Alpha is { } alpha)
                    group.alpha = alpha;
                if (!element.Touchable)
                    group.blocksRaycasts = false;
            }
            if (!string.IsNullOrEmpty(element.Tooltips))
            {
                var tooltip = go.GetComponent<Tooltip>();
                if (tooltip == null) tooltip = go.AddComponent<Tooltip>();
                tooltip.text = element.Tooltips;
                if (!string.IsNullOrEmpty(Settings().tipsRes))
                    tooltip.template = LoadPrefab(Resolve(Settings().tipsRes, null)).GetComponent<Label>();
                if (element.Touchable)
                {
                    var hit = go.GetComponent<Graphic>();
                    if (hit == null)
                    {
                        hit = go.AddComponent<UnityEngine.UI.Image>();
                        hit.color = Color.clear;
                    }
                    hit.raycastTarget = true;
                }
            }
            if (element.Filter == Schema.FilterKind.Color)
            {
                var filter = ConfigureColorFilter(go, element.Kind);
                var data = element.FilterData;
                filter.Set(data.Brightness, data.Contrast, data.Saturation, data.Hue);
            }
            if (element.Grayed)
            {
                // 复刻 GButton/GComponent.HandleGrayedChanged：按钮有 disabled 页只换页；
                // 否则存在名为 grayed 的控制器时切到其页 1；都没有才套灰度滤镜。
                var button = go.GetComponent<ButtonBase>();
                var controllers = element.Kind == Schema.DisplayKind.Component
                    ? Resolve(element.Source, owner.PackageId).ComponentXml.Controllers : Array.Empty<Schema.Controller>();
                var gray = Array.FindIndex(controllers, controller => controller.Name == "grayed");
                var disabledPage = button != null && controllers.AsValueEnumerable().Any(controller => controller.Name == "button"
                    && controller.Pages.AsValueEnumerable().Any(page => page.Name == "disabled"));
                if (button != null)
                    button.grayed = true;
                if (!disabledPage && gray >= 0)
                    go.GetComponent<Component>().SetControllerPage(gray, 1);
                else if (!disabledPage)
                    ConfigureGrayed(go, true);
            }
        }

        private static ColorAdjust ConfigureColorFilter(GameObject go, Schema.DisplayKind kind)
            => ConfigureColorFilter(go, kind is Schema.DisplayKind.Component or Schema.DisplayKind.List
                or Schema.DisplayKind.Text or Schema.DisplayKind.RichText or Schema.DisplayKind.InputText
                or Schema.DisplayKind.Group || go.GetComponent<Graphic>() == null);

        private static ColorAdjust ConfigureColorFilter(GameObject go, bool capture)
        {
            Material presentation = null;
            if (capture)
            {
                var path = $"{OutputRoot}/ObjectFilterPresentation.mat";
                presentation = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (presentation == null)
                {
                    Directory.CreateDirectory(OutputRoot);
                    presentation = new Material(Shader.Find("OpenFairy/UI Premultiplied"));
                    AssetDatabase.CreateAsset(presentation, path);
                }
            }
            return ColorFilterBuilder.Configure(go, capture, ColorMatrixShader,
                Shader.Find("OpenFairy/UI Capture"), presentation, Settings().captureLayer);
        }

        private static Grayed ConfigureGrayed(GameObject go, bool enabled)
        {
            var hasColorFilter = go.TryGetComponent(out ColorAdjust _);
            // Capture the final subtree, including component-loader content and native text materials.
            var filter = ConfigureColorFilter(go, capture: true);
            if (!hasColorFilter) filter.enabled = false;
            return GrayedBuilder.Configure(filter, enabled);
        }

        private static void ConfigureGearLook(Type gearType, object gear, Schema.Gear xml, GameObject go)
        {
            (float Alpha, float Rotation, bool Grayed, bool Touchable) Parse(string value, (float Alpha, float Rotation, bool Grayed, bool Touchable) def)
            {
                if (value == "-")
                    return def;
                var parts = value.Split(',');
                return (float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture),
                    parts[2] == "1", parts.Length < 4 || parts[3] != "0");
            }

            var def = Parse(xml.Default ?? "1,0,0,1", (1, 0, false, true));
            var values = (xml.Values?.Split('|') ?? Array.Empty<string>()).AsValueEnumerable().Select(value => Parse(value, def)).ToArray();
            gearType.GetField("alphas").SetValue(gear, values.AsValueEnumerable().Select(value => value.Alpha).ToArray());
            gearType.GetField("defaultAlpha").SetValue(gear, def.Alpha);
            gearType.GetField("rotations").SetValue(gear, values.AsValueEnumerable().Select(value => value.Rotation).ToArray());
            gearType.GetField("defaultRotation").SetValue(gear, def.Rotation);
            gearType.GetField("grayed").SetValue(gear, values.AsValueEnumerable().Select(value => value.Grayed).ToArray());
            gearType.GetField("defaultGrayed").SetValue(gear, def.Grayed);
            gearType.GetField("touchables").SetValue(gear, values.AsValueEnumerable().Select(value => value.Touchable).ToArray());
            gearType.GetField("defaultTouchable").SetValue(gear, def.Touchable);
            if (!go.TryGetComponent(out CanvasGroup _))
                go.AddComponent<CanvasGroup>();
            // The initial controller page enables the pre-baked effect; authored graying is already enabled.
            if ((def.Grayed || values.AsValueEnumerable().Any(value => value.Grayed)) && !go.TryGetComponent(out Grayed _))
                ConfigureGrayed(go, false);
        }

        private static void ConfigureGearSize(Type gearType, object gear, Schema.Gear xml, RectTransform rt)
        {
            (Vector2 Size, Vector2 Scale) Parse(string value, (Vector2 Size, Vector2 Scale) def)
            {
                if (value == "-")
                    return def;
                var parts = value.Split(',');
                return (new Vector2(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture)),
                    parts.Length >= 4 // 老格式只有 "w,h" 无 scale 分量（复刻 GearSize.Init）
                        ? new Vector2(float.Parse(parts[2], CultureInfo.InvariantCulture), float.Parse(parts[3], CultureInfo.InvariantCulture))
                        : def.Scale);
            }

            var fallback = (Size: rt.sizeDelta, Scale: Vector2.one);
            var def = xml.Default is { } defaultValue ? Parse(defaultValue, fallback) : fallback;
            var values = xml.Values.Split('|').AsValueEnumerable().Select(value => Parse(value, def)).ToArray();
            gearType.GetField("sizes").SetValue(gear, values.AsValueEnumerable().Select(value => value.Size).ToArray());
            gearType.GetField("defaultSize").SetValue(gear, def.Size);
            gearType.GetField("scales").SetValue(gear, values.AsValueEnumerable().Select(value => value.Scale).ToArray());
            gearType.GetField("defaultScale").SetValue(gear, def.Scale);
        }

        private class ScrollView
        {
            public RectTransform Viewport, Content;
            public ScrollPane Pane;
            public Vector2 ViewportSize;
        }

        private static ScrollView BuildScrollView(GameObject root, Schema.Component element, Vector2 size) =>
            BuildScrollView(root, size, element.Margin, element.ScrollBarMargin, element.Scroll, element.ScrollBar,
                element.ScrollBarRes, element.ScrollBarFlags, element.ClipSoftness);

        private static ScrollView BuildScrollView(GameObject root, Schema.Display element, Vector2 size) =>
            BuildScrollView(root, size, element.Margin, element.ScrollBarMargin, element.Scroll, element.ScrollBar,
                element.ScrollBarRes, element.ScrollBarFlags, element.ClipSoftness);

        private static ScrollView BuildScrollView(GameObject root, Vector2 size, string marginValue, string barMarginValue,
            Schema.Scroll scroll, Schema.ScrollBarDisplay display, string[] barResources, Schema.ScrollFlags flags, string clipSoftness)
        {
            var margin = ParseMargin(marginValue);
            var barMargin = ParseMargin(barMarginValue);
            var defaults = Settings().scrollBars;
            if (display == Schema.ScrollBarDisplay.Default)
                display = string.IsNullOrEmpty(defaults?.defaultDisplay) ? Schema.ScrollBarDisplay.Visible
                    : Enum.Parse<Schema.ScrollBarDisplay>(defaults.defaultDisplay, true);
            var floating = flags.HasFlag(Schema.ScrollFlags.Floating);
            var left = flags.HasFlag(Schema.ScrollFlags.DisplayOnLeft);
            var vtRef = barResources is { Length: > 0 } && !string.IsNullOrEmpty(barResources[0]) ? barResources[0] : defaults?.vertical;
            var hzRef = barResources is { Length: > 1 } && !string.IsNullOrEmpty(barResources[1]) ? barResources[1] : defaults?.horizontal;
            var show = display != Schema.ScrollBarDisplay.Hidden;
            var vt = show && scroll is Schema.Scroll.Vertical or Schema.Scroll.Both ? InstantiateScrollBar(vtRef, root.transform) : null;
            var hz = show && scroll is Schema.Scroll.Horizontal or Schema.Scroll.Both ? InstantiateScrollBar(hzRef, root.transform) : null;
            var vtWidth = vt != null ? ((RectTransform)vt.transform).rect.width : 0;
            var hzHeight = hz != null ? ((RectTransform)hz.transform).rect.height : 0;
            var insetX = floating ? 0 : vtWidth;
            var insetY = floating ? 0 : hzHeight;
            var viewport = NewChild("viewport", (RectTransform)root.transform, typeof(RectMask2D), typeof(Image));
            var vr = (RectTransform)viewport.transform;
            vr.pivot = new Vector2(0, 1);
            vr.anchorMin = Vector2.zero;
            vr.anchorMax = Vector2.one;
            vr.offsetMin = new Vector2(margin.Left + (left ? insetX : 0), margin.Bottom + insetY);
            vr.offsetMax = new Vector2(-(margin.Right + (left ? 0 : insetX)), -margin.Top);
            viewport.GetComponent<Image>().color = Color.clear;
            viewport.GetComponent<RectMask2D>().enabled = !flags.HasFlag(Schema.ScrollFlags.MaskDisabled);
            viewport.transform.SetSiblingIndex(0);
            if (clipSoftness is { } softness)
            {
                var parts = softness.Split(',');
                viewport.GetComponent<RectMask2D>().softness = new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
            }
            var content = (RectTransform)NewChild("content", vr).transform;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            content.anchoredPosition = Vector2.zero;
            var pane = root.AddComponent<ScrollPane>();
            pane.viewport = vr;
            pane.content = content;
            pane.horizontal = scroll != Schema.Scroll.Vertical;
            pane.vertical = scroll != Schema.Scroll.Horizontal;
            pane.scrollStep = 25;
            pane.decelerationRate = Mathf.Pow(.967f, 60);
            pane.touchEffect = !flags.HasFlag(Schema.ScrollFlags.TouchOff) || flags.HasFlag(Schema.ScrollFlags.TouchOn);
            pane.bounceEffect = !flags.HasFlag(Schema.ScrollFlags.BounceOff) || flags.HasFlag(Schema.ScrollFlags.BounceOn);
            pane.inertia = !flags.HasFlag(Schema.ScrollFlags.InertiaDisabled);
            pane.snapToItem = flags.HasFlag(Schema.ScrollFlags.SnapToItem);
            pane.pageMode = flags.HasFlag(Schema.ScrollFlags.PageMode);
            pane.mouseWheelEnabled = show;
            var demand = flags.HasFlag(Schema.ScrollFlags.DisplayInDemand);
            // Native expansion owns the entire viewport rect. Keep authored margins when it cannot express them.
            var expand = demand && !floating && !left && margin == (0f, 0f, 0f, 0f);
            var visibility = expand ? ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport
                : demand ? ScrollRect.ScrollbarVisibility.AutoHide : ScrollRect.ScrollbarVisibility.Permanent;
            pane.verticalScrollbarVisibility = pane.horizontalScrollbarVisibility = visibility;
            var hidden = new List<CanvasGroup>();
            if (vt != null)
            {
                var rt = (RectTransform)vt.transform;
                rt.anchorMin = new Vector2(left ? 0 : 1, 0);
                rt.anchorMax = new Vector2(left ? 0 : 1, 1);
                rt.offsetMin = new Vector2(left ? 0 : -vtWidth, barMargin.Bottom + insetY);
                rt.offsetMax = new Vector2(left ? vtWidth : 0, -barMargin.Top);
                pane.verticalScrollbar = BindScrollBar(vt, pane, false, display, hidden);
            }
            if (hz != null)
            {
                var rt = (RectTransform)hz.transform;
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.offsetMin = new Vector2(barMargin.Left + (left ? insetX : 0), 0);
                rt.offsetMax = new Vector2(-(barMargin.Right + (left ? 0 : insetX)), hzHeight);
                pane.horizontalScrollbar = BindScrollBar(hz, pane, true, display, hidden);
            }
            pane.autoHideBars = hidden.ToArray();
            return new ScrollView { Viewport = vr, Content = content, Pane = pane,
                ViewportSize = new Vector2(size.x - margin.Left - margin.Right - insetX, size.y - margin.Top - margin.Bottom - insetY) };
        }

        private static AuthoredScrollbar BindScrollBar(GameObject root, ScrollPane pane, bool horizontal,
            Schema.ScrollBarDisplay display, List<CanvasGroup> hidden)
        {
            var bar = root.GetComponent<AuthoredScrollbar>();
            bar.pane = pane;
            bar.horizontal = horizontal;
            bar.direction = horizontal ? Scrollbar.Direction.LeftToRight : Scrollbar.Direction.BottomToTop;
            bar.value = horizontal ? 0 : 1;
            if (display == Schema.ScrollBarDisplay.Auto)
            {
                if (!root.TryGetComponent<CanvasGroup>(out var group))
                    group = root.AddComponent<CanvasGroup>();
                group.alpha = 0;
                group.interactable = group.blocksRaycasts = false;
                hidden.Add(group);
            }
            return bar;
        }

        private static void SetGrips(ScrollView view, Vector2 size)
        {
            if (view.Pane == null) return;
            view.Content.sizeDelta = size;
            if (view.Pane.verticalScrollbar != null)
                view.Pane.verticalScrollbar.size = Mathf.Min(1, view.ViewportSize.y / Mathf.Max(1, size.y));
            if (view.Pane.horizontalScrollbar != null)
                view.Pane.horizontalScrollbar.size = Mathf.Min(1, view.ViewportSize.x / Mathf.Max(1, size.x));
        }

        private static void SetupScrollBar(GameObject root, List<(Schema.Display Xml, GameObject Go)> children)
        {
            var track = (RectTransform)Child(children, "bar").transform;
            var grip = (RectTransform)Child(children, "grip").transform;
            var horizontal = ((RectTransform)root.transform).rect.width > ((RectTransform)root.transform).rect.height;
            var size = grip.rect.size;
            var point = track.InverseTransformPoint(grip.TransformPoint(new Vector3(grip.rect.xMin, grip.rect.yMax)));
            var extent = horizontal ? track.rect.width : track.rect.height;
            var handle = horizontal ? size.x : size.y;
            var start = horizontal ? point.x - track.rect.xMin : track.rect.yMax - point.y;
            var percent = extent > handle ? start / (extent - handle) : 0;
            var bar = root.AddComponent<AuthoredScrollbar>();
            bar.direction = horizontal ? Scrollbar.Direction.LeftToRight : Scrollbar.Direction.BottomToTop;
            bar.size = handle / extent;
            bar.value = horizontal ? percent : 1 - percent;
            ReparentPreservingRect(grip, track);
            grip.offsetMin = horizontal ? new Vector2(0, point.y - size.y - track.rect.yMin) : new Vector2(point.x - track.rect.xMin, 0);
            grip.offsetMax = horizontal ? new Vector2(0, point.y - track.rect.yMax) : new Vector2(point.x + size.x - track.rect.xMax, 0);
            bar.handleRect = grip;
            bar.targetGraphic = grip.GetComponent<Graphic>();
            bar.transition = Selectable.Transition.None;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            if (Child(children, "arrow1")?.GetComponent<ButtonBase>() is { } back)
                UnityEditor.Events.UnityEventTools.AddPersistentListener(back.onClick, bar.StepBackward);
            if (Child(children, "arrow2")?.GetComponent<ButtonBase>() is { } forward)
                UnityEditor.Events.UnityEventTools.AddPersistentListener(forward.onClick, bar.StepForward);
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
            if (element.Overflow == Schema.Overflow.Hidden)
                go.AddComponent<RectMask2D>();
            var size = (Vector2)element.Size;
            var prefab = element.DefaultItem != null ? LoadPrefab(Resolve(element.DefaultItem, owner.PackageId)) : null;
            var itemSize = prefab != null ? ((RectTransform)prefab.transform).rect.size : Vector2.zero;
            var lineGap = element.LineGap;
            var colGap = element.ColGap;
            var layout = element.Layout;

            // 烘焙动态实例化描述：运行时（PopupMenu/ComboBox 下拉/Window1/Grid）据此从 defaultItem 建项。
            var source = go.AddComponent<ListSource>();
            source.itemPrefab = prefab;
            source.count = element.Items.Length;
            source.itemSize = itemSize;
            source.autoItemSize = element.AutoItemSize;
            source.lineGap = lineGap;
            source.colGap = colGap;
            source.lineItemCount = element.LineItemCount;
            source.alignment = TextAlignment(element);
            source.layout = layout switch
            {
                Schema.ListLayout.SingleRow => ListLayoutType.SingleRow,
                Schema.ListLayout.FlowHorizontal => ListLayoutType.FlowHorizontal,
                Schema.ListLayout.FlowVertical => ListLayoutType.FlowVertical,
                Schema.ListLayout.Pagination => ListLayoutType.Pagination,
                _ => ListLayoutType.SingleColumn,
            };
            source.renderOrder = element.RenderOrder;
            source.apexIndex = element.Apex;

            var view = element.Overflow == Schema.Overflow.Scroll
                ? BuildScrollView(go, element, size)
                : new ScrollView { Viewport = (RectTransform)go.transform, Content = (RectTransform)go.transform };
            var viewSize = view.Viewport == go.transform ? size : view.ViewportSize;
            source.content = view.Content;
            source.scrollPane = view.Pane;
            if (view.Pane != null) view.Pane.source = source;

            var variants = new List<GameObject>();
            var content = Vector2.zero;
            float x = 0, y = 0, lineExtent = 0;
            var lineCount = 0;
            foreach (var item in element.Items)
            {
                var resource = Resolve(item.Url ?? element.DefaultItem, owner.PackageId);
                var itemPrefab = LoadPrefab(resource);
                var itemGo = (GameObject)PrefabUtility.InstantiatePrefab(itemPrefab, view.Content);
                if (itemPrefab != prefab) variants.Add(itemGo);
                ConfigureButton(itemGo, item, owner.PackageId);
                ConfigureInstanceControllers(itemGo.GetComponent<Component>(), resource.ComponentXml, item.Controllers);
                var itemRect = (RectTransform)itemGo.transform;
                if (element.AutoItemSize && layout == Schema.ListLayout.SingleColumn)
                    itemRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewSize.x);
                else if (element.AutoItemSize && layout == Schema.ListLayout.SingleRow)
                    itemRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewSize.y);
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemRect);
                var actualSize = itemRect.rect.size;
                Vector2 position;
                switch (layout)
                {
                    case Schema.ListLayout.SingleRow:
                        position = new Vector2(x, 0);
                        x += actualSize.x + colGap;
                        break;
                    case Schema.ListLayout.FlowHorizontal:
                    case Schema.ListLayout.Pagination: // 分页吸附不做：按 flow_hz 排布
                        if (lineCount > 0 && (element.LineItemCount > 0
                            ? lineCount == element.LineItemCount : x + actualSize.x > viewSize.x))
                        {
                            x = 0;
                            y += lineExtent + lineGap;
                            lineExtent = 0;
                            lineCount = 0;
                        }
                        position = new Vector2(x, y);
                        x += actualSize.x + colGap;
                        lineExtent = Mathf.Max(lineExtent, actualSize.y);
                        lineCount++;
                        break;
                    case Schema.ListLayout.FlowVertical:
                        if (lineCount > 0 && (element.LineItemCount > 0
                            ? lineCount == element.LineItemCount : y + actualSize.y > viewSize.y))
                        {
                            y = 0;
                            x += lineExtent + colGap;
                            lineExtent = 0;
                            lineCount = 0;
                        }
                        position = new Vector2(x, y);
                        y += actualSize.y + lineGap;
                        lineExtent = Mathf.Max(lineExtent, actualSize.x);
                        lineCount++;
                        break;
                    default: // SingleColumn
                        position = new Vector2(0, y);
                        y += actualSize.y + lineGap;
                        break;
                }
                content = Vector2.Max(content, position + actualSize);
                itemRect.anchoredPosition = new Vector2(position.x, -position.y);
            }
            source.variantItems = variants.ToArray();
            SetGrips(view, content);
            ListLayoutBuilder.Configure(source);
            if (element.SelectionMode != ListSelectionMode.None)
                go.AddComponent<ListSelection>().selectionMode = element.SelectionMode; // 点项选中/发 onClickItem
        }

        // 默认滚动条来自 Common.json 的 ui:// 引用；未配置则返回 null，viewport 退化为满宽。
        private static GameObject InstantiateScrollBar(string uiRef, Transform parent)
        {
            if (!TryResolveScrollBar(uiRef, out var resource))
                return null;
            return (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(resource), parent);
        }

        private static bool TryResolveScrollBar(string uiRef, out Resource resource)
        {
            resource = null;
            return !string.IsNullOrEmpty(uiRef) && TryResolve(uiRef, null, out resource)
                && resource.Type == Schema.ResourceKind.Component
                && resource.ComponentXml.Extension == Schema.ComponentExtension.ScrollBar;
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
            var parts = value.Split(',').AsValueEnumerable().Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
            return (parts[0], parts[1], parts[2], parts[3]);
        }

        // Timeline time and tween duration are frames at the transition frame rate.
        private static TextAnchor TextAlignment(Schema.Display element) => (element.VAlign, element.Align) switch
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

        private static string TransitionField(string name) => "t_" + Identifier(name);

        private static IEnumerable<Schema.TransitionItem> TransitionItems(Schema.Component component, Schema.Transition transition) =>
            transition.Items.AsValueEnumerable().Where(item => ParseTransitionType(item.Type) != null
                && (item.Target == "" || component.DisplayList.AsValueEnumerable().Any(child => child.Id == item.Target))).ToArray();

        private static void BuildTransitions(GameObject root, Schema.Component xml, Dictionary<string, (Schema.Display Xml, GameObject Go)> byId, string packageId)
        {
            // 先建齐本组件的全部动效，嵌套项才能直接引用同组件内的其它动效。
            foreach (var transitionXml in xml.Transitions)
                root.AddComponent<Transition>().transitionName = transitionXml.Name;
            foreach (var transitionXml in xml.Transitions)
            {
                var transition = Array.Find(root.GetComponents<Transition>(), value => value.transitionName == transitionXml.Name);
                transition.autoPlay = transitionXml.AutoPlay;
                transition.autoPlayTimes = transitionXml.AutoPlayRepeat;
                transition.autoPlayDelay = transitionXml.AutoPlayDelay;

                var items = new List<TransitionItem>();
                foreach (var itemXml in TransitionItems(xml, transitionXml))
                {
                    var target = itemXml.Target != "" ? byId[itemXml.Target] : default;
                    var type = ParseTransitionType(itemXml.Type).Value;

                    var item = new TransitionItem
                    {
                        time = itemXml.Time / (float)transitionXml.FrameRate,
                        target = target.Go != null ? (RectTransform)target.Go.transform : null,
                        type = type,
                        tween = itemXml.Tween,
                        duration = itemXml.Duration / (float)transitionXml.FrameRate,
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
                                var soundParts = value?.Split(',');
                                if (soundParts is { Length: > 0 } && TryResolve(soundParts[0], packageId, out var soundResource))
                                {
                                    item.sound = ImportSound(soundResource);
                                    if (soundParts.Length > 1)
                                        item.volume = float.Parse(soundParts[1], CultureInfo.InvariantCulture);
                                    if (transition.audioSource == null)
                                    {
                                        if (!root.TryGetComponent(out AudioSource source))
                                            source = root.AddComponent<AudioSource>();
                                        source.playOnAwake = false;
                                        transition.audioSource = source;
                                    }
                                }
                                break;
                            case TransitionItemType.Nested:
                                var transitionParts = value.Split(',');
                                // 复刻 Transition.Play：嵌套目标的同名动效，指向自身或已被作者删除时为空（该项不生效，保留项序号供 Hook 使用）。
                                item.component = Array.Find((target.Go != null ? target.Go : root).GetComponents<Transition>(),
                                    nested => nested.transitionName == transitionParts[0] && nested != transition);
                                if (transitionParts.Length > 1)
                                    item.playTimes = int.Parse(transitionParts[1]);
                                break;
                            case TransitionItemType.Text:
                                item.stringValue = value;
                                break;
                            case TransitionItemType.Icon:
                                item.icon = LoadSprite(value, packageId);
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
                            item.pathData = path.Split(',').AsValueEnumerable().Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                    }
                    else if (item.type == TransitionItemType.Alpha)
                    {
                        var alphaGo = target.Go != null ? target.Go : root;
                        if (!alphaGo.TryGetComponent(out CanvasGroup _))
                            alphaGo.AddComponent<CanvasGroup>();
                    }
                    else if (item.type == TransitionItemType.ColorFilter)
                    {
                        var filterGo = target.Go != null ? target.Go : root;
                        // A static filter remains active before its first transition sample.
                        if (!filterGo.TryGetComponent<ColorAdjust>(out _))
                            ConfigureColorFilter(filterGo, target.Xml?.Kind ?? Schema.DisplayKind.Component).enabled = false;
                    }

                    var targetGo = target.Go != null ? target.Go : root;
                    item.component = item.type switch
                    {
                        TransitionItemType.Alpha => targetGo.GetComponent<CanvasGroup>(),
                        TransitionItemType.Animation => targetGo.GetComponent<MovieClip>(),
                        TransitionItemType.Text => targetGo.GetComponent<TextComponent>(),
                        TransitionItemType.Skew => targetGo.GetComponent<Graph>(),
                        TransitionItemType.Color => targetGo.GetComponent<Graphic>(),
                        TransitionItemType.ColorFilter => targetGo.GetComponent<ColorAdjust>(),
                        TransitionItemType.Icon => targetGo.TryGetComponent(out ButtonBase iconButton) ? iconButton
                            : targetGo.TryGetComponent(out Label iconLabel) ? iconLabel : targetGo.GetComponent<Loader>(),
                        _ => item.component,
                    };
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
            "Icon" => TransitionItemType.Icon,
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
                    return value.Split(',').AsValueEnumerable()
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

        private static AudioClip LoadButtonSound(string value, string packageId)
        {
            if (string.IsNullOrEmpty(value)) return null;
            var resource = Resolve(value, packageId);
            if (resource.Type != Schema.ResourceKind.Sound)
                throw new InvalidDataException($"Button sound '{value}' is not a sound resource.");
            return ImportSound(resource)
                ?? throw new InvalidDataException($"Unable to import button sound '{value}'.");
        }

        private static AudioClip ImportSound(Resource resource)
        {
            if (Sounds.TryGetValue(resource, out var clip))
                return clip;
            var target = AssetPath(resource.File);
            if (WriteFileIfChanged(target, File.ReadAllBytes(resource.File)) || AssetImporter.GetAtPath(target) == null)
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
            var values = xml.Values.Split('|').AsValueEnumerable().Select(value => Parse(value, def)).ToArray();
            gearType.GetField("frames").SetValue(gear, values.AsValueEnumerable().Select(value => value.Frame).ToArray());
            gearType.GetField("defaultFrame").SetValue(gear, def.Frame);
            gearType.GetField("playings").SetValue(gear, values.AsValueEnumerable().Select(value => value.Playing).ToArray());
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

        private static void ReparentPreservingRect(RectTransform child, RectTransform parent, bool worldPositionStays = true)
        {
            var size = child.rect.size;
            var position = worldPositionStays ? child.position : child.localPosition;
            child.SetParent(parent, worldPositionStays);
            child.anchorMin = child.anchorMax = new Vector2(0, 1);
            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            if (worldPositionStays) child.position = position;
            else child.localPosition = position;
            if (child.TryGetComponent<ObjectFilter>(out var filter))
                ObjectFilterBuilder.ReparentPresentation(filter, worldPositionStays);
        }

        private static void BuildRelations(RectTransform reference, RectTransform coordinateSpace, Schema.Display element, GameObject go,
            Dictionary<string, (Schema.Display Xml, GameObject Go)> byId)
        {
            var bindings = new List<RelationBinding>();
            foreach (var relation in element.Relations)
            {
                if (relation.Rules.Length == 0)
                    continue;
                if (relation.Target == "")
                {
                    var native = go.transform.parent == reference && RelationAnchorBaker.TryBake(element,
                        ((RectTransform)go.transform).rect.size, reference.rect.size, out _, out _);
                    bindings.Add(new RelationBinding { target = reference, rules = relation.Rules, nativeAnchors = native });
                }
                else if (byId.TryGetValue(relation.Target, out var target))
                    bindings.Add(new RelationBinding { target = (RectTransform)target.Go.transform,
                        pivotAsAnchor = target.Xml.Anchor, rules = relation.Rules });
            }
            if (bindings.Count == 0)
                return;
            var host = go.AddComponent<Relation>();
            host.reference = reference;
            host.coordinateSpace = coordinateSpace;
            host.pivotAsAnchor = element.Anchor;
            host.bindings = bindings.ToArray();
            host.Record();
        }

        private static bool AutoSizeText(Schema.Display element) => !element.Input
            && element.Kind is Schema.DisplayKind.Text or Schema.DisplayKind.RichText
            && element.AutoSize is Schema.TextAutoSize.Both or Schema.TextAutoSize.Height;

        private static void ConfigureTextLayout(GameObject go, Schema.Display element)
        {
            if (!AutoSizeText(element)) return;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = element.AutoSize == Schema.TextAutoSize.Both
                ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void PrepareTextRelations(Schema.Display element)
        {
            if (!AutoSizeText(element)) return;
            // Content owns automatic axes. Parent size constraints on those axes
            // must not feed back into a parent whose size follows this text.
            foreach (var relation in element.Relations)
                relation.Rules = relation.Rules.AsValueEnumerable().Where(rule =>
                    !(rule.side == RelationSide.Height || rule.side >= RelationSide.TopExtTop
                        || element.AutoSize == Schema.TextAutoSize.Both
                        && (rule.side == RelationSide.Width || rule.side >= RelationSide.LeftExtLeft))).ToArray();
        }

        private static void SetRect(RectTransform rt, Schema.Display element, Vector2 size, RectTransform parent)
        {
            var xy = element.Position ?? Vector2.zero;
            if (element.Anchor && element.Pivot is { } anchorPivot)
                xy -= anchorPivot * size;
            Vector2 anchorMin = new(0, 1), anchorMax = new(0, 1);
            RelationAnchorBaker.TryBake(element, size, parent.rect.size, out anchorMin, out anchorMax);

            var pw = parent.rect.width;
            var ph = parent.rect.height;
            rt.pivot = new Vector2(0, 1);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(xy.x - anchorMin.x * pw, ph - xy.y - size.y - anchorMin.y * ph);
            rt.offsetMax = new Vector2(xy.x + size.x - anchorMax.x * pw, ph - xy.y - anchorMax.y * ph);
            if (element.Pivot is { } pivot)
            {
                // Pivot changes also move the anchor reference point; only the residual size needs compensation.
                var newPivot = new Vector2(pivot.x, 1 - pivot.y);
                var delta = newPivot - rt.pivot;
                rt.pivot = newPivot;
                rt.anchoredPosition += Vector2.Scale(delta, rt.sizeDelta);
            }
            if (element.Scale is { } scale)
                rt.localScale = new Vector3(scale.x, scale.y, 1);
        }

        // 把显示 Text 升级为可编辑输入框（复刻 FairyGUI GTextInput）：走原生 uGUI InputField。
        // 不复用 OpenFairy.UGUI.TextField 作输入面——它的 OnEnable 会按 onClickLink 把 raycastTarget 清零（Text.cs），运行时把烘焙的
        // raycastTarget 覆盖成 false → GraphicRaycaster 跳过 → 输入框永远无法聚焦；且它自绘 OnPopulateMesh 不填
        // cachedTextGenerator，InputField 光标定位/选区失效。故：targetGraphic 用一张透明常驻 Image（空文本时 Text 无网格
        // → 单靠 Text 不可点），textComponent 用普通 UI.Text（原生渲染 + 填充 generator，输入框功能完整）。
        // 输入框不追求 FairyGUI 字体排版一致（项目允许）；prompt 仍用 OpenFairy.UGUI.TextField 以复刻 UBB 斜体灰样式，
        // 空文本静置时页面显示的正是 placeholder，故静态 golden 不受影响。
        private static void ConfigureInput(GameObject go, TextField display, Schema.Display element)
        {
            var font = display.font;
            var fontNames = display.fontNames;
            var fontSize = display.fontSize;
            var color = display.color;
            var alignment = display.alignment;
            Object.DestroyImmediate(display); // 卸掉 OpenFairy.UGUI.TextField 显示面，改用原生结构

            var bg = go.AddComponent<UnityEngine.UI.Image>();
            bg.color = Color.clear; // 透明但常驻 raycast，保证整框可点/可聚焦

            var field = go.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.lineType = element.SingleLine ? InputField.LineType.SingleLine : InputField.LineType.MultiLineNewline;
            if (element.Password)
                field.contentType = InputField.ContentType.Password;
            if (element.MaxLength > 0)
                field.characterLimit = element.MaxLength;

            // 系统字体按 fontNames 在运行时解析，烘焙期 font 为空；用 TextField 承载输入文字，与其它文本同源取字体。
            var textGo = NewChild("Text", (RectTransform)go.transform, typeof(TextField));
            var text = textGo.GetComponent<TextField>();
            text.font = font;
            text.fontNames = fontNames;
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
                ph.font = font;
                ph.fontNames = fontNames;
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
            UnityEditor.Events.UnityEventTools.AddPersistentListener(field.onSubmit, submit.Submit);
            var input = go.AddComponent<TextInput>();
            input.field = field;
            input.submit = submit;
        }

#if FAIRYGUI_TMPRO
        // FAIRYGUI_TMPRO selects native TMP components for every text element.
        private static GameObject CreateTmpTextElement(string name, RectTransform parent, Schema.Display element, Resource owner)
        {
            if (!element.Input)
            {
                var displayGo = NewChild(name, parent, typeof(TextMeshProUGUI));
                ConfigureTmpText(displayGo.GetComponent<TextMeshProUGUI>(), element, owner, false);
                return displayGo;
            }

            var root = NewChild(name, parent);
            var background = root.AddComponent<UnityEngine.UI.Image>();
            background.color = Color.clear; // FairyGUI 输入文本没有底框，透明面只承接射线

            var field = root.AddComponent<TMP_InputField>();
            field.targetGraphic = background;
            field.lineType = element.SingleLine
                ? TMP_InputField.LineType.SingleLine
                : TMP_InputField.LineType.MultiLineNewline;
            if (element.Password)
                field.contentType = TMP_InputField.ContentType.Password;
            if (element.MaxLength > 0)
                field.characterLimit = element.MaxLength;

            var textArea = NewChild("Text Area", (RectTransform)root.transform);
            textArea.AddComponent<RectMask2D>();
            var textAreaRect = (RectTransform)textArea.transform;
            StretchFull(textAreaRect);

            var textGo = NewChild("Text", textAreaRect, typeof(TextMeshProUGUI));
            var text = textGo.GetComponent<TextMeshProUGUI>();
            ConfigureTmpText(text, element, owner, true);
            text.text = "";
            text.raycastTarget = false;
            StretchFull((RectTransform)textGo.transform);

            field.textViewport = textAreaRect;
            field.textComponent = text;
            field.fontAsset = text.font;
            field.richText = false;
            if (element.Prompt != null) ConfigureTmpPrompt(field, element.Prompt);
            field.text = element.Text;
            return root;
        }

        private static void ConfigureTmpPrompt(TMP_InputField field, string prompt)
        {
            if (!(field.placeholder is TMP_Text placeholder))
            {
                var go = NewChild("Placeholder", field.textViewport, typeof(TextMeshProUGUI));
                placeholder = go.GetComponent<TextMeshProUGUI>();
                var text = field.textComponent;
                placeholder.font = text.font;
                placeholder.fontSharedMaterial = text.fontSharedMaterial;
                placeholder.fontSize = text.fontSize;
                placeholder.fontStyle = text.fontStyle;
                placeholder.color = text.color;
                placeholder.alignment = text.alignment;
                placeholder.characterSpacing = text.characterSpacing;
                placeholder.lineSpacing = text.lineSpacing;
                placeholder.textWrappingMode = text.textWrappingMode;
                placeholder.overflowMode = text.overflowMode;
                StretchFull((RectTransform)go.transform);
                field.placeholder = placeholder;
            }
            placeholder.raycastTarget = false;
            placeholder.richText = true;
            if (!placeholder.TryGetComponent(out TmpTextMarkup markup))
                markup = placeholder.gameObject.AddComponent<TmpTextMarkup>();
            markup.ubb = true;
            markup.html = false;
            markup.Bind();
            placeholder.text = prompt;
        }

        private static void ConfigureTmpText(TextMeshProUGUI text, Schema.Display element, Resource owner, bool inputText)
        {
            text.font = LoadTmpFont(element, owner);
            text.text = element.Text;
            text.fontSize = element.FontSize ?? Settings().fontSize;
            text.color = ParseColor(element.Color ?? Settings().textColor);
            text.richText = !inputText && (element.Ubb || element.Kind == Schema.DisplayKind.RichText);
            if (text.richText)
            {
                var inlinePath = Path.ChangeExtension(AssetPath(owner.File), null) + "_" + Identifier(element.Id) + "_InlineSprites.asset";
                text.text = TmpSpriteAssetBuilder.BakeInlineImages(inlinePath, text.text, element.Ubb,
                    element.Kind == Schema.DisplayKind.RichText,
                    uri => TryResolve(uri, owner.PackageId, out var resource)
                        ? AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(resource)) : null,
                    out var spriteAsset);
                text.spriteAsset = spriteAsset;
                var markup = text.gameObject.AddComponent<TmpTextMarkup>();
                markup.ubb = element.Ubb;
                markup.html = element.Kind == Schema.DisplayKind.RichText;
                markup.Bind();
            }
            text.extraPadding = true;
            // TMP 的字距与行距以 em/100 为单位；FairyGUI 以像素计，行高为 round(1.25*size) + leading - 1。
            var em = 100f / text.fontSize;
            var face = text.font.faceInfo;
            text.characterSpacing = element.LetterSpacing * em;
            text.lineSpacing = (Mathf.Round(text.fontSize * 1.25f) + element.Leading - 1
                - face.lineHeight * face.scale * text.fontSize / face.pointSize) * em;
            text.fontStyle = (element.Bold, element.Italic) switch
            {
                (true, true) => FontStyles.Bold | FontStyles.Italic,
                (true, false) => FontStyles.Bold,
                (false, true) => FontStyles.Italic,
                _ => FontStyles.Normal,
            };
            text.textWrappingMode = element.SingleLine || element.AutoSize == Schema.TextAutoSize.Both
                ? TextWrappingModes.NoWrap
                : TextWrappingModes.Normal;
            text.overflowMode = inputText || element.AutoSize != Schema.TextAutoSize.None
                ? TextOverflowModes.Overflow
                : TextOverflowModes.Truncate;
            if (!inputText)
            {
                var authoredFontSize = element.FontSize ?? Settings().fontSize;
                if (element.AutoSize == Schema.TextAutoSize.Shrink)
                    TextAutoSize.ConfigureShrink(text, authoredFontSize);
                else if (element.AutoSize == Schema.TextAutoSize.Ellipsis)
                    TextAutoSize.ConfigureEllipsis(text, authoredFontSize);
            }
            text.alignment = (element.VAlign, element.Align) switch
            {
                (Schema.VAlign.Middle, Schema.Align.Center) => TextAlignmentOptions.Center,
                (Schema.VAlign.Middle, Schema.Align.Right) => TextAlignmentOptions.Right,
                (Schema.VAlign.Middle, _) => TextAlignmentOptions.Left,
                (Schema.VAlign.Bottom, Schema.Align.Center) => TextAlignmentOptions.Bottom,
                (Schema.VAlign.Bottom, Schema.Align.Right) => TextAlignmentOptions.BottomRight,
                (Schema.VAlign.Bottom, _) => TextAlignmentOptions.BottomLeft,
                (_, Schema.Align.Center) => TextAlignmentOptions.Top,
                (_, Schema.Align.Right) => TextAlignmentOptions.TopRight,
                _ => TextAlignmentOptions.TopLeft,
            };
            text.raycastTarget = false;
            var materialPath = Path.ChangeExtension(AssetPath(owner.File), null) + "_" + Identifier(element.Id) + "_TextMaterial.mat";
            if (element.StrokeColor is { } stroke)
                TmpTextMaterialBuilder.ConfigureStroke(text, materialPath, ParseColor(stroke), element.StrokeSize);
            if (element.ShadowColor is { } shadow)
                TmpTextMaterialBuilder.ConfigureShadow(text, materialPath, ParseColor(shadow), element.ShadowOffset ?? new Vector2(1, 1));
        }

        private static TMP_FontAsset LoadTmpFont(Schema.Display element, Resource owner)
        {
            if (!string.IsNullOrEmpty(element.TmpFont))
            {
                var path = element.TmpFont;
                var explicitFont = path.StartsWith("Assets/", StringComparison.Ordinal)
                    ? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path)
                    : UnityEngine.Resources.Load<TMP_FontAsset>(path);
                return explicitFont != null ? explicitFont
                    : throw new InvalidOperationException($"TMP font asset '{path}' was not found.");
            }

            var name = element.Font;
            if (!string.IsNullOrEmpty(name))
            {
                if (TryResolve(name, owner.PackageId, out var resource))
                    name = Path.GetFileNameWithoutExtension(resource.File);
                var font = UnityEngine.Resources.Load<TMP_FontAsset>(name);
                if (font == null)
                    font = UnityEngine.Resources.Load<TMP_FontAsset>("Fonts/" + name);
                if (font != null)
                    return font;
                foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
                {
                    font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    if (string.Equals(font.name, name, StringComparison.OrdinalIgnoreCase)
                        || font.sourceFontFile != null && string.Equals(font.sourceFontFile.name, name, StringComparison.OrdinalIgnoreCase))
                        return font;
                }
                throw new InvalidOperationException($"No TMP font asset matches FairyGUI font '{name}'.");
            }

            return TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset
                : throw new InvalidOperationException("Assign a default font in TMP Settings before running OpenFairy Migrate.");
        }
#endif

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
            // 与 TextField.Parse 相同的标签口径按出现序绑定图片；解析不到的陈旧图片标签在烘焙期移除。
            var images = new List<Sprite>();
            var pattern = (text.ubb ? @"\[img\](?<url>.*?)\[/img\]" : "(?!)") + "|"
                + (text.html ? @"<img\b[^>]*?src\s*=\s*['""]?(?<url>[^'"" >]+)[^>]*>" : "(?!)");
            text.text = Regex.Replace(text.text, pattern, match =>
            {
                if (!TryResolve(match.Groups["url"].Value, owner.PackageId, out var res)
                    || AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(res)) is not { } sprite)
                    return "";
                images.Add(sprite);
                return match.Value;
            }, RegexOptions.IgnoreCase);
            text.imageSprites = images.ToArray();
            var bold = element.Bold;
            var italic = element.Italic;
            text.fontStyle = bold && italic ? FontStyle.BoldAndItalic : bold ? FontStyle.Bold : italic ? FontStyle.Italic : FontStyle.Normal;
            text.overflowMode = element.AutoSize switch
            {
                Schema.TextAutoSize.Shrink => TextField.OverflowMode.Shrink,
                Schema.TextAutoSize.Ellipsis => TextField.OverflowMode.Ellipsis,
                _ => TextField.OverflowMode.Default,
            };
            text.horizontalOverflow = element.AutoSize == Schema.TextAutoSize.Both || element.SingleLine
                ? HorizontalWrapMode.Overflow
                : HorizontalWrapMode.Wrap;
            text.verticalOverflow = element.AutoSize is Schema.TextAutoSize.None or Schema.TextAutoSize.Ellipsis
                ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
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
            if ((element.Font ?? Settings().font) is { } font)
            {
                // 位图字体与原生动态字体都在导入期保存直接引用。
                if (TryResolve(font, owner.PackageId, out var fontResource) && fontResource.Type == Schema.ResourceKind.Font)
                    if (fontResource.File.EndsWith(".fnt"))
                        text.bitmapFont = ImportFont(fontResource);
                    else
                    {
                        text.fontNames = Path.GetFileNameWithoutExtension(fontResource.File);
                        text.font = ImportDynamicFont(fontResource);
                    }
                else
                    text.fontNames = font;
            }
            // 运行时字体也烘定：通用工程无需业务胶水设 TextField.defaultFont 静态量。
            if (text.bitmapFont == null && string.IsNullOrEmpty(text.fontNames))
                text.fontNames = TextField.defaultFont;
        }

        private static Font ImportDynamicFont(Resource resource)
        {
            if (DynamicFonts.TryGetValue(resource, out var font))
                return font;
            var target = AssetPath(resource.File);
            if (WriteFileIfChanged(target, File.ReadAllBytes(resource.File)) || AssetImporter.GetAtPath(target) == null)
                AssetDatabase.ImportAsset(target);
            return DynamicFonts[resource] = AssetDatabase.LoadAssetAtPath<Font>(target);
        }

        // 解析 FairyGUI 编辑器工程里的 .fnt：标准 BMFont（atlas 坐标 + texture 属性指向图集）
        // 或 UIBuilder 逐字图片格式（img=资源id，打包成横向图集）。
        private static BitmapFont ImportFont(Resource fontResource)
        {
            if (!fontResource.File.EndsWith(".fnt")) // ttf/otf（如 TextMeshPro 包的 SDF 字体）不是位图字体
                return null;
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
                ImportSprite(texturePath, contentChanged: WriteFileIfChanged(texturePath, File.ReadAllBytes(atlas.File)));
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
                var textures = charLines.AsValueEnumerable()
                    .Select(attrs => Resources[fontResource.PackageId + attrs["img"]])
                    .Select(image =>
                    {
                        var texture = new Texture2D(2, 2);
                        texture.LoadImage(File.ReadAllBytes(image.File));
                        return texture;
                    })
                    .ToArray();
                var width = textures.AsValueEnumerable().Sum(texture => texture.width + 1);
                var height = textures.AsValueEnumerable().Max(texture => texture.height);
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
                var changed = WriteFileIfChanged(texturePath, canvas.EncodeToPNG());
                Object.DestroyImmediate(canvas);
                ImportSprite(texturePath, contentChanged: changed);
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
            var textureAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var glyphArray = glyphs.ToArray();
            var canTint = fontResource.Texture != null;
            if (asset.size != size || asset.canTint != canTint || asset.texture != textureAsset
                || asset.glyphs == null || !asset.glyphs.AsValueEnumerable().SequenceEqual(glyphArray))
            {
                asset.size = size;
                asset.canTint = canTint;
                asset.texture = textureAsset;
                asset.glyphs = glyphArray;
                asset.InvalidateLookup();
                EditorUtility.SetDirty(asset);
            }
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
            var used = new HashSet<string>(StringComparer.Ordinal) { "value__" };
            foreach (var (id, name) in controller.Pages)
                yield return (id, UniqueName(used, Identifier(string.IsNullOrEmpty(name) ? id : name)));
        }

        private static GameObject NewChild(string name, RectTransform parent, params Type[] components)
        {
            // GameObject 构造器不处理 RequireComponent，Graphic 需要显式补 CanvasRenderer。
            var types = components.Length == 0
                ? new[] { typeof(RectTransform) }
                : components.AsValueEnumerable().Prepend(typeof(CanvasRenderer)).Prepend(typeof(RectTransform)).ToArray();
            var go = new GameObject(name, types);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Sprite LoadSprite(string src, string packageId)
        {
            if (string.IsNullOrWhiteSpace(src)) return null;
            if (TryResolve(src, packageId, out var resource))
                return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(resource));
            Debug.LogWarning($"Unresolved FairyGUI icon: {src}");
            return null;
        }

        private static Resource Resolve(string value, string packageId) =>
            Resources[value.StartsWith("ui://") ? value[5..] : packageId + value];

        private static bool TryResolve(string value, string packageId, out Resource resource) =>
            Resources.TryGetValue(value.StartsWith("ui://") ? value[5..] : packageId + value, out resource);

        private static Type FindType(string fullName) =>
            TypeCache.GetTypesDerivedFrom<MonoBehaviour>().AsValueEnumerable().First(type => type.FullName == fullName);

        // 编辑器期查找、序列化进 prefab；运行时组件不做 Shader.Find。
        private static Shader ColorMatrixShader => Shader.Find("OpenFairy/UI ColorMatrix");
        private static Shader BlendShader => Shader.Find("OpenFairy/UI Blend");

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
            "__arglist", "__makeref", "__reftype", "__refvalue",
        };

        private static string Identifier(string name)
        {
            static bool Letter(char c) => char.IsLetter(c) || char.GetUnicodeCategory(c) == UnicodeCategory.LetterNumber;
            static bool Part(char c) => Letter(c) || char.GetUnicodeCategory(c) is UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;
            var id = new string((name ?? "").AsValueEnumerable().Select(c => Part(c) ? c : '_').ToArray());
            return id.Length == 0 || !Letter(id[0]) && id[0] != '_' || Keywords.Contains(id) ? "_" + id : id;
        }

        private static readonly HashSet<string> CodeTypeNames = new(StringComparer.Ordinal)
        {
            "UnityEngine", "OpenFairy", "System", "OpenFairyTextComponent", "OpenFairyInputComponent",
            "LocalizedTexts", "ApplyLocale", "LocaleTable", "LocaleSet",
        };

        private static string ClassName(Resource component)
        {
            PrepareCodeNames(component);
            return ClassNames[component];
        }

        private static readonly HashSet<string> SerializedBaseFields = new(StringComparer.Ordinal)
        {
            "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
            "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier",
            "controllerChildren",
        };

        private static string Field(string name)
        {
            var field = Identifier(CodeSettings().memberNamePrefix + Identifier(name));
            return SerializedBaseFields.Contains(field) ? field + "_" : field;
        }

        private static IEnumerable<(Schema.Display Element, string Field)> PublishedChildFields(Schema.Component xml, string className = "")
            => new GeneratedMembers(xml, className).children;

        // 空成员前缀时，控制器字段与内嵌枚举不能同名。
        private static string ControllerEnumName(string name) =>
            Field(name) == Identifier(name) ? Identifier(name) + "Pages" : Identifier(name);

        private static IEnumerable<string> SourceRoots()
        {
            yield return UiRoot;
            foreach (var root in MigrationSettings.instance.dependencyDirectories) yield return root;
        }

        private static void IndexSources()
        {
            Resources.Clear();
            foreach (var root in SourceRoots())
                foreach (var path in Directory.EnumerateFiles($"{root}/assets", "package.xml", SearchOption.AllDirectories))
                    IndexPackage(path.Replace('\\', '/'));
        }

        private static bool WithinAssets(string root, string file, out string relative)
        {
            relative = Path.GetRelativePath($"{root}/assets", file).Replace('\\', '/');
            return relative != ".." && !relative.StartsWith("../", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
        }

        private static string SourceRelativePath(string file)
        {
            foreach (var root in SourceRoots())
                if (WithinAssets(root, file, out var relative)) return relative;
            throw new InvalidOperationException($"Resource '{file}' is outside the configured source directories.");
        }

        private static string AssetPath(string file) => $"{OutputRoot}/Assets/{SourceRelativePath(file)}";

        private static string SpritePath(Resource resource) =>
            resource.Type == Schema.ResourceKind.MovieClip ? FramePath(AssetPath(resource.File), 0) : AssetPath(resource.File);

        private static string ScriptPath(Resource component)
        {
            var relativePath = SourceRelativePath(component.File);
            var name = ClassName(component);
            return $"{OutputRoot}/Scripts/{Path.GetDirectoryName(relativePath)}/{name}.cs".Replace('\\', '/');
        }

        private static CodeGenerationSettings CodeSettings()
        {
            if (_codeGenerationSettings != null)
                return _codeGenerationSettings;
            var path = $"{UiRoot}/settings/Publish.json";
            var publish = File.Exists(path) ? JsonUtility.FromJson<PublishSettings>(File.ReadAllText(path)) : null;
            return _codeGenerationSettings = publish?.codeGeneration ?? new CodeGenerationSettings();
        }

        private static string GeneratedNamespace(Resource resource) =>
            string.IsNullOrEmpty(CodeSettings().packageName) ? PackageName(resource)
                : string.Join(".", Array.ConvertAll(CodeSettings().packageName.Split('.'), Identifier)) + "." + PackageName(resource);

        [Serializable]
        private class PublishSettings
        {
            public CodeGenerationSettings codeGeneration = new();
        }

        [Serializable]
        private class CodeGenerationSettings
        {
            // 缺少配置时沿用旧版 OpenFairy 的命名和不过滤行为。
            public string classNamePrefix = "";
            public string packageName = "";
            public string memberNamePrefix = "m_";
            public bool ignoreNoname;
        }

        private static CommonSettings Settings() =>
            _settings ??= File.Exists($"{UiRoot}/settings/Common.json")
                ? JsonUtility.FromJson<CommonSettings>(File.ReadAllText($"{UiRoot}/settings/Common.json"))
                : new CommonSettings();

        // Common.json 支持字体资源 URL，也保留系统字体族名。
        private static string DefaultFont()
        {
            var designFont = Settings().font;
            if (string.IsNullOrEmpty(designFont)) return "Arial";
            if (TryResolve(designFont, null, out var resource) && resource.Type == Schema.ResourceKind.Font)
                return Path.GetFileNameWithoutExtension(resource.File);
            return designFont.Split(',')[0].Trim();
        }

        // 缺省值硬编码自 FairyGUI 新建工程的 settings/Common.json（Common.json 允许不存在）。
        [Serializable]
        private class CommonSettings
        {
            public string tipsRes = "";
            public string buttonClickSound = "";
            public int captureLayer = 31;
            public int fontSize = 12;
            public string textColor = "#000000";
            public string font = "";
            public ScrollBars scrollBars = new();
        }

        [Serializable]
        private class ScrollBars
        {
            public string vertical;
            public string horizontal;
            public string defaultDisplay = "visible";
        }
    }
}
