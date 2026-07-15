using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using System.Xml.Serialization;
using UnityEngine;
using ZLinq;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Editor
{
    public static class FairyXml
    {
        private readonly struct ChildBinding
        {
            public readonly string ElementName;
            public readonly Type Type;

            public ChildBinding(string elementName, Type type)
            {
                ElementName = elementName;
                Type = type;
            }
        }

        private sealed class FieldBinding
        {
            public readonly System.Reflection.FieldInfo Field;
            public readonly XmlAttributeAttribute Attribute;
            public readonly XmlArrayAttribute Array;
            public readonly ChildBinding[] Elements;
            public readonly ChildBinding[] Items;

            public FieldBinding(System.Reflection.FieldInfo field)
            {
                Field = field;
                Attribute = field.GetCustomAttributes(typeof(XmlAttributeAttribute), true).AsValueEnumerable()
                    .FirstOrDefault() as XmlAttributeAttribute;
                Array = field.GetCustomAttributes(typeof(XmlArrayAttribute), true).AsValueEnumerable()
                    .FirstOrDefault() as XmlArrayAttribute;
                var itemType = field.FieldType.IsArray ? field.FieldType.GetElementType() : field.FieldType;
                Elements = field.GetCustomAttributes(typeof(XmlElementAttribute), true).AsValueEnumerable()
                    .Cast<XmlElementAttribute>()
                    .Select(attribute => new ChildBinding(attribute.ElementName, SchemaType(attribute.Type, itemType)))
                    .ToArray();
                Items = field.GetCustomAttributes(typeof(XmlArrayItemAttribute), true).AsValueEnumerable()
                    .Cast<XmlArrayItemAttribute>()
                    .Select(attribute => new ChildBinding(attribute.ElementName, SchemaType(attribute.Type, itemType)))
                    .ToArray();
            }
        }

        private static readonly Dictionary<Type, FieldBinding[]> TypeBindings = new();
        private static readonly Dictionary<Type, Dictionary<string, object>> EnumAliases = new();

        private static FieldBinding[] Bindings(Type type)
        {
            if (!TypeBindings.TryGetValue(type, out var bindings))
                TypeBindings[type] = bindings = type.GetFields().AsValueEnumerable()
                    .Select(field => new FieldBinding(field)).ToArray();
            return bindings;
        }

        public static Schema.Package LoadPackage(string file)
        {
            var package = Deserialize<Schema.Package>(file);
            var directory = Path.GetDirectoryName(file).Replace('\\', '/');
            foreach (var resource in package.Resources)
                resource.File = $"{directory}{resource.Path.TrimEnd('/')}/{resource.FileName}";
            return package;
        }

        public static Schema.Component LoadComponent(string file) =>
            Deserialize<Schema.Component>(file);

        private static T Deserialize<T>(string file) where T : new() =>
            (T)Read(typeof(T), XDocument.Load(file).Root);

        private static object Read(Type type, XElement xml)
        {
            var result = Activator.CreateInstance(type);
            foreach (var binding in Bindings(type))
            {
                var field = binding.Field;
                if (binding.Attribute is { } attribute)
                {
                    if (xml.Attribute(attribute.AttributeName) is { } value)
                        field.SetValue(result, Convert(value.Value, field.FieldType));
                    continue;
                }

                if (binding.Array is { } array)
                {
                    var container = xml.Element(array.ElementName);
                    field.SetValue(result, ReadChildren(field, container?.Elements() ?? Array.Empty<XElement>(), binding.Items));
                    continue;
                }

                var elements = binding.Elements;
                if (elements.Length == 0)
                    continue;

                if (field.FieldType.IsArray)
                    field.SetValue(result, ReadChildren(field, xml.Elements(), elements));
                else
                {
                    foreach (var element in elements)
                        if (xml.Element(element.ElementName) is { } child)
                        {
                            field.SetValue(result, Read(element.Type, child));
                            break;
                        }
                }
            }
            Finish(result, xml);
            return result;
        }

        private static void Finish(object result, XElement xml)
        {
            switch (result)
            {
                case Schema.Resource resource:
                    resource.Kind = (Schema.ResourceKind)ParseEnum(typeof(Schema.ResourceKind), xml.Name.LocalName);
                    break;
                case Schema.Display display:
                    // <jta> 是 movieclip 实例的扩展名标签，归一到 movieclip 类型
                    var tag = xml.Name.LocalName == "jta" ? "movieclip" : xml.Name.LocalName;
                    display.Kind = (Schema.DisplayKind)ParseEnum(typeof(Schema.DisplayKind), tag);
                    display.Source = display.Src ?? display.Url;
                    break;
                case Schema.Gear gear:
                    gear.Kind = (Schema.GearKind)ParseEnum(typeof(Schema.GearKind), xml.Name.LocalName);
                    break;
                case Schema.Extension extension:
                    extension.Items = extension.ItemNodes.AsValueEnumerable().Select(item => item.Title ?? "").ToArray();
                    extension.Values = extension.ItemNodes.AsValueEnumerable().Any(item => item.Value != null)
                        ? extension.ItemNodes.AsValueEnumerable().Select(item => item.Value ?? item.Title ?? "").ToArray()
                        : Array.Empty<string>();
                    break;
            }
        }

        private static Array ReadChildren(System.Reflection.FieldInfo field, IEnumerable<XElement> elements, ChildBinding[] bindings)
        {
            var itemType = field.FieldType.GetElementType();
            var items = elements.AsValueEnumerable().Select(element =>
            {
                var binding = bindings.AsValueEnumerable().FirstOrDefault(binding => binding.ElementName == element.Name.LocalName);
                return binding.Type == null ? null : Read(binding.Type, element);
            }).Where(item => item != null).ToArray();
            var array = Array.CreateInstance(itemType, items.Length);
            for (var i = 0; i < items.Length; i++)
                array.SetValue(items[i], i);
            return array;
        }

        private static Type SchemaType(Type type, Type fallback) =>
            type == null || type == typeof(object) ? fallback : type;

        private static object Convert(string value, Type type)
        {
            var nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null)
                return value == "" ? null : Convert(value, nullable);
            if (type == typeof(string))
                return value;
            if (type == typeof(bool))
                return value == "true";
            if (type == typeof(int))
                return int.Parse(value, CultureInfo.InvariantCulture);
            if (type == typeof(float))
                return float.Parse(value, CultureInfo.InvariantCulture);
            if (type == typeof(Vector2))
                return ParsePair(value);
            if (type == typeof(string[]))
                return value.Split(',');
            if (type == typeof(ValueTuple<string, string>[]))
                return ParsePages(value);
            if (type.IsEnum)
                return value == "" ? Enum.ToObject(type, 0) : ParseEnum(type, value); // 空属性值（如 scale=""）= 缺省
            if (type.IsArray && type.GetElementType().IsEnum)
            {
                var parts = value.Split(',');
                var array = Array.CreateInstance(type.GetElementType(), parts.Length);
                for (var i = 0; i < parts.Length; i++)
                    array.SetValue(ParseEnum(type.GetElementType(), parts[i]), i);
                return array;
            }
            throw new NotSupportedException($"FairyXml cannot convert {type.Name}");
        }

        private static object ParseEnum(Type type, string value)
        {
            if (!EnumAliases.TryGetValue(type, out var aliases))
            {
                aliases = new Dictionary<string, object>();
                foreach (var field in type.GetFields())
                    if (field.GetCustomAttributes(typeof(XmlEnumAttribute), true).AsValueEnumerable().FirstOrDefault() is XmlEnumAttribute xmlEnum)
                        aliases[xmlEnum.Name] = Enum.Parse(type, field.Name);
                EnumAliases[type] = aliases;
            }
            if (aliases.TryGetValue(value, out var alias))
                return alias;
            // XML 词表的标点风格（left-left / multiple_singleclick）折叠成成员名，忽略大小写（titleType="valueAndmax" 等杂写）。
            return Enum.Parse(type, value.Replace("-", "").Replace("_", ""), true);
        }

        private static Vector2 ParsePair(string value)
        {
            var parts = value.Split(',');
            return new Vector2(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture));
        }

        private static (string Id, string Name)[] ParsePages(string value)
        {
            var parts = value.Split(',');
            return ValueEnumerable.Range(0, parts.Length / 2).Select(i => (parts[i * 2], parts[i * 2 + 1])).ToArray();
        }
    }
}

namespace OpenFairy.UGUI.Editor.Schema
{
    public enum ResourceKind
    {
        Unknown,
        [XmlEnum("image")]
        Image,
        [XmlEnum("movieclip")]
        MovieClip,
        [XmlEnum("sound")]
        Sound,
        [XmlEnum("component")]
        Component,
        [XmlEnum("atlas")]
        Atlas,
        [XmlEnum("font")]
        Font,
        [XmlEnum("swf")]
        Swf,
    }

    public enum DisplayKind
    {
        [XmlEnum("image")]
        Image,
        [XmlEnum("movieclip")]
        MovieClip,
        [XmlEnum("graph")]
        Graph,
        [XmlEnum("loader")]
        Loader,
        [XmlEnum("group")]
        Group,
        [XmlEnum("text")]
        Text,
        [XmlEnum("richtext")]
        RichText,
        [XmlEnum("inputtext")]
        InputText,
        [XmlEnum("component")]
        Component,
        [XmlEnum("list")]
        List,
    }

    public enum ComponentExtension
    {
        None,
        Button,
        ComboBox,
        ProgressBar,
        Slider,
        Label,
        ScrollBar,
    }

    public enum Overflow
    {
        Visible,
        [XmlEnum("hidden")] Hidden,
        [XmlEnum("scroll")] Scroll,
    }

    public enum Scroll
    {
        [XmlEnum("vertical")] Vertical,
        [XmlEnum("horizontal")] Horizontal,
        [XmlEnum("both")] Both,
    }

    public enum LoaderFill
    {
        None,
        [XmlEnum("scale")] Scale,
        [XmlEnum("scaleMatchHeight")] ScaleMatchHeight,
        [XmlEnum("scaleMatchWidth")] ScaleMatchWidth,
        [XmlEnum("scaleFree")] ScaleFree,
        [XmlEnum("scaleNoBorder")] ScaleNoBorder,
    }

    public enum ImageFillMethod
    {
        None,
        [XmlEnum("hz")] Horizontal,
        [XmlEnum("vt")] Vertical,
        [XmlEnum("radial90")] Radial90,
        [XmlEnum("radial180")] Radial180,
        [XmlEnum("radial360")] Radial360,
    }

    public enum Flip
    {
        None,
        [XmlEnum("hz")] Horizontal,
        [XmlEnum("vt")] Vertical,
        [XmlEnum("both")] Both,
    }

    public enum Align
    {
        Left,
        [XmlEnum("center")] Center,
        [XmlEnum("right")] Right,
    }

    public enum VAlign
    {
        Top,
        [XmlEnum("middle")] Middle,
        [XmlEnum("bottom")] Bottom,
    }

    public enum ShapeType
    {
        None,
        [XmlEnum("rect")] Rect,
        [XmlEnum("eclipse")] Ellipse,
        [XmlEnum("polygon")] Polygon,
        [XmlEnum("regular_polygon")] RegularPolygon,
    }

    // package.xml 图片资源的缩放方式（缺省/空 = 整图）。
    public enum ImageScale
    {
        None,
        [XmlEnum("9grid")] NineGrid,
        [XmlEnum("tile")] Tile,
    }

    // 列表排布的 XML 词表 → 运行时 ListLayoutType 在 Migrate 映射。
    public enum ListLayout
    {
        [XmlEnum("column")] SingleColumn,
        [XmlEnum("row")] SingleRow,
        [XmlEnum("flow_hz")] FlowHorizontal,
        [XmlEnum("flow_vt")] FlowVertical,
        [XmlEnum("pagination")] Pagination,
    }

    // 文本自动尺寸。Shrink/Ellipsis 未实现，转换时按 Height 处理。
    public enum TextAutoSize
    {
        Both,
        None,
        Height,
        Shrink,
        Ellipsis,
    }

    public enum GearKind
    {
        [XmlEnum("gearDisplay")]
        Display,
        [XmlEnum("gearDisplay2")]
        Display2,
        [XmlEnum("gearXY")]
        XY,
        [XmlEnum("gearColor")]
        Color,
        [XmlEnum("gearLook")]
        Look,
        [XmlEnum("gearSize")]
        Size,
        [XmlEnum("gearAni")]
        Ani,
        [XmlEnum("gearFontSize")]
        FontSize,
        [XmlEnum("gearText")]
        Text,
        [XmlEnum("gearIcon")]
        Icon,
    }

    [XmlRoot("packageDescription")]
    public class Package
    {
        [XmlAttribute("id")] public string Id;

        [XmlArray("resources")]
        [XmlArrayItem("image")]
        [XmlArrayItem("movieclip")]
        [XmlArrayItem("sound")]
        [XmlArrayItem("component")]
        [XmlArrayItem("atlas")]
        [XmlArrayItem("font")]
        [XmlArrayItem("swf")]
        public Resource[] Resources = Array.Empty<Resource>();

        public static Package Load(string file) => OpenFairy.UGUI.Editor.FairyXml.LoadPackage(file);
    }

    public class Resource
    {
        [XmlAttribute("id")] public string Id;
        [XmlAttribute("name")] public string FileName;
        [XmlAttribute("path")] public string Path = "";
        [XmlAttribute("scale")] public ImageScale Scale;
        [XmlAttribute("scale9grid")] public string Scale9Grid;
        [XmlAttribute("texture")] public string Texture;
        [XmlAttribute("exported")] public bool Exported;

        public ResourceKind Kind;
        public string File;
    }

    [XmlRoot("component")]
    public class Component
    {
        [XmlAttribute("size")] public Vector2 Size;
        [XmlAttribute("extention")] public ComponentExtension Extension;
        [XmlAttribute("overflow")] public Overflow Overflow;
        [XmlAttribute("mask")] public string Mask;
        [XmlAttribute("scroll")] public Scroll Scroll = Scroll.Vertical;
        [XmlAttribute("scrollBar")] public string ScrollBar;
        [XmlAttribute("scrollBarFlags")] public int ScrollBarFlags;
        [XmlAttribute("margin")] public string Margin;
        [XmlAttribute("scrollBarMargin")] public string ScrollBarMargin;
        [XmlAttribute("clipSoftness")] public string ClipSoftness;

        [XmlElement("controller")] public Controller[] Controllers = Array.Empty<Controller>();

        [XmlArray("displayList")]
        [XmlArrayItem("image")]
        [XmlArrayItem("movieclip")]
        [XmlArrayItem("jta")] // FairyGUI 编辑器工程里 movieclip 实例的标签取资源扩展名(.jta)，与 <movieclip> 同义
        [XmlArrayItem("graph")]
        [XmlArrayItem("loader")]
        [XmlArrayItem("group")]
        [XmlArrayItem("text")]
        [XmlArrayItem("richtext")]
        [XmlArrayItem("inputtext")]
        [XmlArrayItem("component")]
        [XmlArrayItem("list")]
        public Display[] DisplayList = Array.Empty<Display>();

        [XmlElement("transition")] public Transition[] Transitions = Array.Empty<Transition>();
        [XmlElement("Button")] public Extension Button;
        [XmlElement("ComboBox")] public Extension ComboBox;
        [XmlElement("ProgressBar")] public Extension ProgressBar;
        [XmlElement("Slider")] public Extension Slider;

        public static Component Load(string file) => OpenFairy.UGUI.Editor.FairyXml.LoadComponent(file);
    }

    public class Controller
    {
        [XmlAttribute("name")] public string Name;
        [XmlAttribute("selected")] public int Selected;
        [XmlAttribute("pages")] public (string Id, string Name)[] Pages = Array.Empty<(string Id, string Name)>();
    }

    public class Display
    {
        [XmlAttribute("id")] public string Id;
        [XmlAttribute("name")] public string Name;
        [XmlAttribute("src")] public string Src;
        [XmlAttribute("url")] public string Url;
        [XmlAttribute("group")] public string Group;
        [XmlAttribute("defaultItem")] public string DefaultItem;
        [XmlAttribute("xy")] public Vector2? Position;
        [XmlAttribute("size")] public Vector2? Size;
        [XmlAttribute("pivot")] public Vector2? Pivot;
        [XmlAttribute("scale")] public Vector2? Scale;
        [XmlAttribute("anchor")] public bool Anchor;
        [XmlAttribute("visible")] public bool Visible = true;
        [XmlAttribute("touchable")] public bool Touchable = true;
        [XmlAttribute("grayed")] public bool Grayed;
        [XmlAttribute("rotation")] public float? Rotation;
        [XmlAttribute("alpha")] public float? Alpha;
        [XmlAttribute("type")] public ShapeType Type;
        [XmlAttribute("color")] public string Color;
        [XmlAttribute("fillMethod")] public ImageFillMethod FillMethod;
        [XmlAttribute("fillClockwise")] public bool FillClockwise = true;
        [XmlAttribute("fillAmount")] public float FillAmount = 100;
        [XmlAttribute("flip")] public Flip Flip;
        [XmlAttribute("strokeColor")] public string StrokeColor;
        [XmlAttribute("strokeSize")] public float StrokeSize = 1;
        [XmlAttribute("shadowColor")] public string ShadowColor;
        [XmlAttribute("shadowOffset")] public Vector2? ShadowOffset;
        [XmlAttribute("fill")] public LoaderFill Fill;
        [XmlAttribute("align")] public Align Align;
        [XmlAttribute("vAlign")] public VAlign VAlign;
        [XmlAttribute("lineSize")] public float LineSize = 1;
        [XmlAttribute("lineColor")] public string LineColor = "#ff000000";
        [XmlAttribute("fillColor")] public string FillColor = "#ffffffff";
        [XmlAttribute("skew")] public Vector2? Skew;
        [XmlAttribute("points")] public string Points;
        [XmlAttribute("sides")] public int Sides;
        [XmlAttribute("startAngle")] public float StartAngle;
        [XmlAttribute("distances")] public string Distances;
        [XmlAttribute("corner")] public string Corner;
        [XmlAttribute("playing")] public bool Playing = true;
        [XmlAttribute("frame")] public int Frame;
        [XmlAttribute("overflow")] public Overflow Overflow;
        [XmlAttribute("scroll")] public Scroll Scroll = Scroll.Vertical;
        [XmlAttribute("scrollBar")] public string ScrollBar;
        [XmlAttribute("scrollBarFlags")] public int ScrollBarFlags;
        [XmlAttribute("margin")] public string Margin;
        [XmlAttribute("scrollBarMargin")] public string ScrollBarMargin;
        [XmlAttribute("clipSoftness")] public string ClipSoftness;
        [XmlAttribute("lineGap")] public float LineGap;
        [XmlAttribute("colGap")] public float ColGap;
        [XmlAttribute("layout")] public ListLayout Layout;
        [XmlAttribute("selectionMode")] public OpenFairy.UGUI.ListSelectionMode SelectionMode = OpenFairy.UGUI.ListSelectionMode.Single; // FairyGUI 列表默认单选
        [XmlAttribute("text")] public string Text = "";
        [XmlAttribute("fontSize")] public int? FontSize;
        [XmlAttribute("leading")] public int Leading = 3;
        [XmlAttribute("letterSpacing")] public int LetterSpacing;
        [XmlAttribute("ubb")] public bool Ubb;
        [XmlAttribute("input")] public bool Input;
        [XmlAttribute("password")] public bool Password;
        [XmlAttribute("maxLength")] public int MaxLength;
        [XmlAttribute("underline")] public bool Underline;
        [XmlAttribute("prompt")] public string Prompt;
        [XmlAttribute("bold")] public bool Bold;
        [XmlAttribute("italic")] public bool Italic;
        [XmlAttribute("autoSize")] public TextAutoSize AutoSize = TextAutoSize.Both;
        [XmlAttribute("singleLine")] public bool SingleLine;
        [XmlAttribute("font")] public string Font;

        [XmlElement("relation")] public Relation[] Relations = Array.Empty<Relation>();
        [XmlElement("gearDisplay")]
        [XmlElement("gearDisplay2")]
        [XmlElement("gearXY")]
        [XmlElement("gearColor")]
        [XmlElement("gearLook")]
        [XmlElement("gearSize")]
        [XmlElement("gearAni")]
        [XmlElement("gearFontSize")]
        [XmlElement("gearText")]
        [XmlElement("gearIcon")]
        public Gear[] Gears = Array.Empty<Gear>();

        [XmlElement("item")] public Extension[] Items = Array.Empty<Extension>();
        [XmlElement("Button")] public Extension Button;
        [XmlElement("Label")] public Extension Label;
        [XmlElement("ProgressBar")] public Extension ProgressBar;
        [XmlElement("Slider")] public Extension Slider;
        [XmlElement("ComboBox")] public Extension ComboBox;

        public DisplayKind Kind;
        public string Source;
    }

    public class Relation
    {
        [XmlAttribute("target")] public string Target = "";
        [XmlAttribute("sidePair")] public OpenFairy.UGUI.RelationSide[] SidePairs = Array.Empty<OpenFairy.UGUI.RelationSide>();
    }

    public class Gear
    {
        [XmlAttribute("controller")] public string Controller;
        [XmlAttribute("pages")] public string Pages;
        [XmlAttribute("tween")] public bool Tween;
        [XmlAttribute("duration")] public float Duration = 0.3f;
        [XmlAttribute("ease")] public string Ease;
        [XmlAttribute("delay")] public float Delay;
        [XmlAttribute("default")] public string Default;
        [XmlAttribute("values")] public string Values;
        [XmlAttribute("condition")] public int Condition;

        public GearKind Kind;
    }

    public class Extension
    {
        [XmlAttribute("title")] public string Title;
        [XmlAttribute("selectedTitle")] public string SelectedTitle;
        [XmlAttribute("icon")] public string Icon;
        [XmlAttribute("mode")] public OpenFairy.UGUI.ButtonMode? Mode; // 实例级缺省时不覆盖定义级
        [XmlAttribute("checked")] public bool Checked;
        [XmlAttribute("titleColor")] public string TitleColor;
        // 按钮关联控制器（FairyGUI relatedController/relatedPageId）：点击换该控制器的页，实现 tab/radio 组。
        [XmlAttribute("controller")] public string Controller;
        [XmlAttribute("page")] public string Page;
        [XmlAttribute("value")] public string Value;
        [XmlAttribute("max")] public float Max = 100;
        [XmlAttribute("min")] public float Min;
        [XmlAttribute("titleType")] public OpenFairy.UGUI.ProgressTitleType TitleType;
        [XmlAttribute("reverse")] public bool Reverse;
        [XmlAttribute("wholeNumbers")] public bool WholeNumbers;
        [XmlAttribute("changeOnClick")] public bool ChangeOnClick = true;
        [XmlAttribute("dropdown")] public string Dropdown;
        [XmlAttribute("visibleItemCount")] public int VisibleItemCount = 10;
        [XmlElement("item")] public Extension[] ItemNodes = Array.Empty<Extension>();

        public string[] Items = Array.Empty<string>();
        public string[] Values = Array.Empty<string>();
    }

    public class Transition
    {
        [XmlAttribute("name")] public string Name;
        [XmlAttribute("autoPlay")] public bool AutoPlay;
        [XmlAttribute("autoPlayRepeat")] public int AutoPlayRepeat = 1;
        [XmlAttribute("autoPlayDelay")] public float AutoPlayDelay;
        [XmlElement("item")] public TransitionItem[] Items = Array.Empty<TransitionItem>();
    }

    public class TransitionItem
    {
        [XmlAttribute("target")] public string Target = "";
        [XmlAttribute("type")] public string Type;
        [XmlAttribute("time")] public int Time;
        [XmlAttribute("tween")] public bool Tween;
        [XmlAttribute("duration")] public int Duration;
        [XmlAttribute("ease")] public string Ease;
        [XmlAttribute("repeat")] public int Repeat;
        [XmlAttribute("yoyo")] public bool Yoyo;
        [XmlAttribute("startValue")] public string StartValue;
        [XmlAttribute("endValue")] public string EndValue;
        [XmlAttribute("value")] public string Value;
        [XmlAttribute("path")] public string Path;
    }
}
