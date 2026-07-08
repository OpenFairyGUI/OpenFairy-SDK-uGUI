using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;
using UnityEngine;
using Schema = NanamiUI.Editor.Schema;

namespace NanamiUI.Editor
{
    public static class FairyXml
    {
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
            foreach (var field in type.GetFields())
            {
                if (field.GetCustomAttributes(typeof(XmlAttributeAttribute), true).FirstOrDefault() is XmlAttributeAttribute attribute)
                {
                    if (xml.Attribute(attribute.AttributeName) is { } value)
                        field.SetValue(result, Convert(value.Value, field.FieldType));
                    continue;
                }

                var array = field.GetCustomAttributes(typeof(XmlArrayAttribute), true).FirstOrDefault() as XmlArrayAttribute;
                if (array != null)
                {
                    var container = xml.Element(array.ElementName);
                    field.SetValue(result, ReadChildren(field, container?.Elements() ?? Enumerable.Empty<XElement>(), typeof(XmlArrayItemAttribute)));
                    continue;
                }

                var elements = field.GetCustomAttributes(typeof(XmlElementAttribute), true).Cast<XmlElementAttribute>().ToArray();
                if (elements.Length == 0)
                    continue;

                if (field.FieldType.IsArray)
                    field.SetValue(result, ReadChildren(field, xml.Elements(), typeof(XmlElementAttribute)));
                else
                {
                    var element = elements.FirstOrDefault(e => xml.Element(e.ElementName) != null);
                    if (element != null)
                        field.SetValue(result, Read(SchemaType(element.Type, field.FieldType), xml.Element(element.ElementName)));
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
                    extension.Items = extension.ItemNodes.Select(item => item.Title ?? "").ToArray();
                    break;
            }
        }

        private static Array ReadChildren(System.Reflection.FieldInfo field, IEnumerable<XElement> elements, Type attributeType)
        {
            var itemType = field.FieldType.GetElementType();
            var bindings = ChildBindings(field, attributeType, itemType).ToArray();
            var items = elements.Select(element =>
            {
                var binding = bindings.FirstOrDefault(binding => binding.ElementName == element.Name.LocalName);
                return binding.Type == null ? null : Read(binding.Type, element);
            }).Where(item => item != null).ToArray();
            var array = Array.CreateInstance(itemType, items.Length);
            for (var i = 0; i < items.Length; i++)
                array.SetValue(items[i], i);
            return array;
        }

        private static IEnumerable<(string ElementName, Type Type)> ChildBindings(System.Reflection.FieldInfo field, Type attributeType, Type itemType)
        {
            if (attributeType == typeof(XmlArrayItemAttribute))
                return field.GetCustomAttributes(typeof(XmlArrayItemAttribute), true)
                    .Cast<XmlArrayItemAttribute>()
                    .Select(attribute => (attribute.ElementName, SchemaType(attribute.Type, itemType)));
            return field.GetCustomAttributes(typeof(XmlElementAttribute), true)
                .Cast<XmlElementAttribute>()
                .Select(attribute => (attribute.ElementName, SchemaType(attribute.Type, itemType)));
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
                return ParseEnum(type, value);
            throw new NotSupportedException($"FairyXml cannot convert {type.Name}");
        }

        private static object ParseEnum(Type type, string value)
        {
            foreach (var field in type.GetFields())
                if (field.GetCustomAttributes(typeof(XmlEnumAttribute), true).FirstOrDefault() is XmlEnumAttribute xmlEnum && xmlEnum.Name == value)
                    return Enum.Parse(type, field.Name);
            return Enum.Parse(type, value, true);
        }

        private static Vector2 ParsePair(string value)
        {
            var parts = value.Split(',');
            return new Vector2(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture));
        }

        private static (string Id, string Name)[] ParsePages(string value)
        {
            var parts = value.Split(',');
            return Enumerable.Range(0, parts.Length / 2).Select(i => (parts[i * 2], parts[i * 2 + 1])).ToArray();
        }
    }
}

namespace NanamiUI.Editor.Schema
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

        public static Package Load(string file) => NanamiUI.Editor.FairyXml.LoadPackage(file);
    }

    public class Resource
    {
        [XmlAttribute("id")] public string Id;
        [XmlAttribute("name")] public string FileName;
        [XmlAttribute("path")] public string Path = "";
        [XmlAttribute("scale")] public string Scale;
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

        public static Component Load(string file) => NanamiUI.Editor.FairyXml.LoadComponent(file);
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
        [XmlAttribute("layout")] public string Layout = "column";
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
        [XmlAttribute("autoSize")] public string AutoSize = "both";
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
        [XmlAttribute("sidePair")] public string[] SidePairs = Array.Empty<string>();
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
        [XmlAttribute("mode")] public string Mode;
        [XmlAttribute("checked")] public bool Checked;
        [XmlAttribute("titleColor")] public string TitleColor;
        [XmlAttribute("value")] public float Value = 50;
        [XmlAttribute("max")] public float Max = 100;
        [XmlAttribute("titleType")] public string TitleType;
        [XmlAttribute("reverse")] public bool Reverse;
        [XmlAttribute("dropdown")] public string Dropdown;
        [XmlAttribute("visibleItemCount")] public int VisibleItemCount = 10;
        [XmlElement("item")] public Extension[] ItemNodes = Array.Empty<Extension>();

        public string[] Items = Array.Empty<string>();
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

