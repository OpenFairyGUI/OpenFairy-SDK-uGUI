using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace NanamiUI
{
    public static class Migrate
    {
        private const string UiRoot = "Assets/UIProject";
        private const string OutputRoot = "Assets/UI";
        private const string DefaultFont = "Microsoft YaHei";

        private static readonly XNamespace Ui = "UnityEngine.UIElements";
        private static readonly XNamespace Nanami = "NanamiUI";
        private static readonly Dictionary<string, (string Asset, string Uxml, bool Component)> Assets = new();
        private static readonly Dictionary<string, string> ComponentPackages = new();
        private static CommonSettings _settings;

        [MenuItem("Tools/Migrate %e")]
        public static void Execute()
        {
            Assets.Clear();
            ComponentPackages.Clear();
            Directory.CreateDirectory(OutputRoot);

            foreach (var package in Directory.EnumerateFiles($"{UiRoot}/assets", "package.xml", SearchOption.AllDirectories))
                IndexPackage(package.Replace('\\', '/'));

            foreach (var xml in ComponentPackages.Keys.OrderBy(x => x))
            {
                var uxml = ChangeExtension(OutputPath(xml), ".uxml");
                Directory.CreateDirectory(Path.GetDirectoryName(uxml));
                File.WriteAllText(uxml, ToUxml(xml).ToString());
            }

            AssetDatabase.Refresh();
            Debug.Log($"NanamiUI migrated {ComponentPackages.Count} FairyGUI components to {OutputRoot}.");
        }

        private static void IndexPackage(string packagePath)
        {
            var package = XDocument.Load(packagePath).Root;
            var packageId = package.Attribute("id").Value;
            var packageDir = Path.GetDirectoryName(packagePath).Replace('\\', '/');

            foreach (var resource in package.Element("resources").Elements())
            {
                var asset = $"{packageDir}/{resource.Attribute("path").Value.Trim('/')}/{resource.Attribute("name").Value}".Replace("//", "/");
                var isComponent = resource.Name.LocalName == "component";
                var key = packageId + resource.Attribute("id").Value;
                Assets[key] = (asset, ChangeExtension(OutputPath(asset), ".uxml"), isComponent);
                if (isComponent)
                    ComponentPackages[asset] = packageId;
            }
        }

        private static XDocument ToUxml(string xml)
        {
            var component = XDocument.Load(xml).Root;
            var root = Element(component, ComponentPackages[xml], true);
            root.SetAttributeValue("name", Path.GetFileNameWithoutExtension(xml));

            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(Ui + "UXML",
                    new XAttribute(XNamespace.Xmlns + "ui", Ui),
                    new XAttribute(XNamespace.Xmlns + "nanami", Nanami),
                    root));
        }

        private static XElement Element(XElement fairy, string packageId, bool root = false)
        {
            var element = fairy.Name.LocalName switch
            {
                "component" when !root => Component(fairy, packageId),
                "text" or "richtext" or "inputtext" => new XElement(Nanami + "Text",
                    Attr("text", fairy.Attribute("text")?.Value),
                    TextFont(fairy, packageId),
                    ColorAttr("stroke-color", fairy.Attribute("strokeColor")?.Value),
                    Attr("stroke-size", fairy.Attribute("strokeColor") == null ? null : fairy.Attribute("strokeSize")?.Value ?? "1")),
                "list" => new XElement(Ui + "ScrollView"),
                _ => new XElement(Ui + "VisualElement")
            };

            AddIdentity(element, fairy);
            AddStyle(element, fairy, root, fairy.Name.LocalName == "component" && !root ? ComponentSize(fairy, packageId) : null);
            AddVisualStyle(element, fairy, packageId);

            var displayList = fairy.Element("displayList");
            foreach (var child in displayList?.Elements() ?? Enumerable.Empty<XElement>())
                if (child.Name.LocalName != "group")
                    element.Add(Element(child, packageId));

            if (fairy.Name.LocalName == "list")
                foreach (var _ in fairy.Elements("item"))
                    if (TryResolve(fairy.Attribute("defaultItem").Value, packageId, out var item))
                        element.Add(ReferencedComponent(item));

            return element;
        }

        private static XElement Component(XElement fairy, string packageId)
        {
            var button = fairy.Element("Button");
            return TryResolve(fairy.Attribute("src").Value, packageId, out var component)
                ? ReferencedComponent(component, button?.Attribute("title")?.Value)
                : new XElement(Ui + "VisualElement");
        }

        private static XElement ReferencedComponent((string Asset, string Uxml, bool Component) component, string title = null)
        {
            var xml = XDocument.Load(component.Asset).Root;
            if (xml.Attribute("extention")?.Value == "Button")
                return new XElement(Nanami + "Button",
                    new XAttribute("src", RelativeOutput(component.Uxml)),
                    Attr("title", title));

            return Element(xml, ComponentPackages[component.Asset], true);
        }

        private static void AddIdentity(XElement element, XElement fairy)
        {
            if (fairy.Attribute("name") != null)
                element.SetAttributeValue("name", fairy.Attribute("name").Value);
            element.SetAttributeValue("class", fairy.Name.LocalName);
        }

        private static void AddStyle(XElement element, XElement fairy, bool root, double[] fallbackSize = null)
        {
            var style = new List<string> { $"position: {(root ? "relative" : "absolute")}" };

            if (!root && Pair(fairy, "xy") is { } xy)
            {
                style.Add($"left: {Px(xy[0])}");
                style.Add($"top: {Px(xy[1])}");
            }

            var size = Pair(fairy, "size") ?? fallbackSize;
            if (size != null)
            {
                style.Add($"width: {Px(size[0])}");
                style.Add($"height: {Px(size[1])}");
            }

            if (fairy.Attribute("alpha") is { } alpha)
                style.Add($"opacity: {alpha.Value}");
            if (fairy.Attribute("visible")?.Value == "false")
                style.Add("display: none");

            element.SetAttributeValue("style", string.Join("; ", style) + ";");
        }

        private static void AddVisualStyle(XElement element, XElement fairy, string packageId)
        {
            var style = element.Attribute("style").Value.TrimEnd(';');
            var append = new List<string>();

            switch (fairy.Name.LocalName)
            {
                case "image":
                case "loader":
                    var image = fairy.Attribute("src")?.Value ?? fairy.Attribute("url")?.Value;
                    if (image != null && TryResolve(image, packageId, out var resolved))
                    {
                        if (!resolved.Component)
                            append.Add($"background-image: url(\"project://database/{resolved.Asset}\")");
                    }
                    break;
                case "graph":
                    if (fairy.Attribute("fillColor") is { } fill)
                        append.Add($"background-color: {Color(fill.Value)}");
                    if (fairy.Attribute("lineSize") is { Value: not "0" } line)
                        append.AddRange(Border(line.Value, Color(fairy.Attribute("lineColor")?.Value ?? "#ff000000")));
                    break;
                case "text":
                case "richtext":
                case "inputtext":
                    append.Add("margin: 0");
                    append.Add("padding: 0");
                    append.Add($"font-size: {Px(fairy.Attribute("fontSize")?.Value ?? Settings().fontSize.ToString(CultureInfo.InvariantCulture))}");
                    if (fairy.Attribute("color") is { } textColor)
                        append.Add($"color: {Color(textColor.Value)}");
                    append.Add($"-unity-text-align: {TextAlign(fairy.Attribute("align")?.Value, fairy.Attribute("vAlign")?.Value)}");
                    if (fairy.Attribute("bold")?.Value == "true")
                        append.Add("-unity-font-style: bold");
                    if (fairy.Attribute("font") is { } font && TryResolve(font.Value, packageId, out var fontAsset) && Path.GetExtension(fontAsset.Asset) is ".ttf" or ".otf")
                        append.Add($"-unity-font-definition: url(\"project://database/{fontAsset.Asset}\")");
                    break;
            }

            if (append.Count > 0)
                element.SetAttributeValue("style", style + "; " + string.Join("; ", append) + ";");
        }

        private static IEnumerable<string> Border(string width, string color)
        {
            yield return $"border-left-width: {Px(width)}";
            yield return $"border-right-width: {Px(width)}";
            yield return $"border-top-width: {Px(width)}";
            yield return $"border-bottom-width: {Px(width)}";
            yield return $"border-left-color: {color}";
            yield return $"border-right-color: {color}";
            yield return $"border-top-color: {color}";
            yield return $"border-bottom-color: {color}";
        }

        private static (string Asset, string Uxml, bool Component) Resolve(string value, string packageId)
        {
            var key = value.StartsWith("ui://") ? value[5..] : packageId + value;
            return Assets[key];
        }

        private static bool TryResolve(string value, string packageId, out (string Asset, string Uxml, bool Component) asset)
        {
            var key = value.StartsWith("ui://") ? value[5..] : packageId + value;
            return Assets.TryGetValue(key, out asset);
        }

        private static double[] ComponentSize(XElement fairy, string packageId) =>
            TryResolve(fairy.Attribute("src").Value, packageId, out var component)
                ? Pair(XDocument.Load(component.Asset).Root, "size")
                : null;

        private static double[] Pair(XElement element, string attribute) =>
            element.Attribute(attribute)?.Value.Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();

        private static XAttribute Attr(string name, string value) =>
            value == null ? null : new XAttribute(name, value);

        private static XAttribute ColorAttr(string name, string value) =>
            value == null ? null : new XAttribute(name, Color(value));

        private static XAttribute TextFont(XElement fairy, string packageId)
        {
            var font = fairy.Attribute("font")?.Value ?? DefaultFont;
            return font != "" && !TryResolve(font, packageId, out _)
                ? new XAttribute("font", font)
                : null;
        }

        private static CommonSettings Settings() =>
            _settings ??= JsonUtility.FromJson<CommonSettings>(File.ReadAllText($"{UiRoot}/settings/Common.json"));

        private static string OutputPath(string asset) =>
            $"{OutputRoot}/{Path.GetRelativePath($"{UiRoot}/assets", asset).Replace('\\', '/')}";

        private static string RelativeOutput(string asset) =>
            Path.GetRelativePath(OutputRoot, asset).Replace('\\', '/');

        private static string ChangeExtension(string path, string extension) =>
            Path.ChangeExtension(path, extension).Replace('\\', '/');

        private static string Px(double value) =>
            value.ToString("0.###", CultureInfo.InvariantCulture) + "px";

        private static string Px(string value) =>
            double.Parse(value, CultureInfo.InvariantCulture).ToString("0.###", CultureInfo.InvariantCulture) + "px";

        private static string Color(string value) =>
            value.Length == 9 ? $"#{value[3..]}{value[1..3]}" : value;

        private static string TextAlign(string horizontal, string vertical) =>
            $"{(vertical == "middle" ? "middle" : vertical == "bottom" ? "lower" : "upper")}-{(horizontal == "center" ? "center" : horizontal == "right" ? "right" : "left")}";

        [System.Serializable]
        private class CommonSettings
        {
            public string font;
            public int fontSize;
        }
    }
}
