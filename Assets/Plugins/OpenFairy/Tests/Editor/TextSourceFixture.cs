using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace OpenFairy.UGUI.Editor
{
    public enum FairyTextProperty
    {
        Text,
        Prompt,
        Tooltip,
        GearText,
        GearTextDefault,
        Title,
        SelectedTitle,
        ComboItem,
        InstanceText,
        ListItemTitle,
        ListItemSelectedTitle,
        ListItemText,
    }

    public readonly struct FairyTextSlot
    {
        public readonly string element;
        public readonly string suffix;
        public readonly FairyTextProperty property;
        public readonly string authored;
        public readonly string target;
        public readonly string controller;
        public readonly int index;
        public readonly bool variables;

        public FairyTextSlot(string element, string suffix, FairyTextProperty property, string authored,
            string target = null, string controller = null, int index = -1, bool variables = false)
        {
            this.element = element;
            this.suffix = suffix;
            this.property = property;
            this.authored = authored;
            this.target = target;
            this.controller = controller;
            this.index = index;
            this.variables = variables;
        }
    }

    public static class FairyTextSlots
    {
        public static List<FairyTextSlot> Read(XElement component, Func<XElement, string, bool> variableTarget = null)
        {
            var result = new List<FairyTextSlot>();
            if (component.Element("displayList") is not { } displayList)
                return result;
            foreach (var child in displayList.Elements())
            {
                var id = (string)child.Attribute("id");
                var variables = (string)child.Attribute("vars") == "true";
                if (child.Attribute("tooltips") is { } tips)
                    result.Add(new(id, id + "-tips", FairyTextProperty.Tooltip, tips.Value));
                foreach (var gear in child.Elements("gearText"))
                {
                    var values = ((string)gear.Attribute("values") ?? "").Split('|');
                    var pages = ((string)gear.Attribute("pages") ?? "").Split(',');
                    for (var i = 0; i < values.Length && i < pages.Length; i++)
                        result.Add(new(id, id + "-texts_" + i, FairyTextProperty.GearText, values[i],
                            controller: (string)gear.Attribute("controller"), index: i, variables: variables));
                    if (gear.Attribute("default") is { } fallback)
                        result.Add(new(id, id + "-texts_def", FairyTextProperty.GearTextDefault, fallback.Value,
                            controller: (string)gear.Attribute("controller"), variables: variables));
                }

                switch (child.Name.LocalName)
                {
                    case "text":
                    case "richtext":
                    case "inputtext":
                        result.Add(new(id, id, FairyTextProperty.Text, (string)child.Attribute("text") ?? "", variables: variables));
                        if (child.Attribute("prompt") is { } prompt)
                            result.Add(new(id, id + "-prompt", FairyTextProperty.Prompt, prompt.Value));
                        break;
                    case "component":
                        foreach (var extension in child.Elements())
                        {
                            if (extension.Name.LocalName is "Label" or "Button" or "ComboBox")
                            {
                                if (extension.Attribute("title") is { } title)
                                    result.Add(new(id, id, FairyTextProperty.Title, title.Value, variables: variableTarget?.Invoke(child, "title") == true));
                                if (extension.Attribute("selectedTitle") is { } selected)
                                    result.Add(new(id, id + "-0", FairyTextProperty.SelectedTitle, selected.Value, variables: variableTarget?.Invoke(child, "title") == true));
                                if (extension.Attribute("prompt") is { } labelPrompt)
                                    result.Add(new(id, id + "-prompt", FairyTextProperty.Prompt, labelPrompt.Value));
                                var index = 0;
                                foreach (var item in extension.Elements("item"))
                                {
                                    result.Add(new(id, id + "-" + index, FairyTextProperty.ComboItem,
                                        (string)item.Attribute("title") ?? "", index: index));
                                    index++;
                                }
                            }
                            if (extension.Name.LocalName == "property" && (string)extension.Attribute("name") == "text")
                            {
                                var target = (string)extension.Attribute("target");
                                result.Add(new(id, id + "-cp-" + target, FairyTextProperty.InstanceText,
                                    (string)extension.Attribute("value") ?? "", target: target, variables: variableTarget?.Invoke(child, target) == true));
                            }
                        }
                        break;
                    case "list":
                    case "tree":
                        var itemIndex = 0;
                        foreach (var item in child.Elements("item"))
                        {
                            if (item.Attribute("title") is { } title)
                                result.Add(new(id, id + "-" + itemIndex, FairyTextProperty.ListItemTitle, title.Value, index: itemIndex, variables: variableTarget?.Invoke(item, "title") == true));
                            if (item.Attribute("selectedTitle") is { } selected)
                                result.Add(new(id, id + "-" + itemIndex + "-0", FairyTextProperty.ListItemSelectedTitle, selected.Value, index: itemIndex, variables: variableTarget?.Invoke(item, "title") == true));
                            foreach (var property in item.Elements("property"))
                                if ((string)property.Attribute("name") == "text")
                                {
                                    var target = (string)property.Attribute("target");
                                    result.Add(new(id, id + "-" + itemIndex + "-" + target, FairyTextProperty.ListItemText,
                                        (string)property.Attribute("value") ?? "", target: target, index: itemIndex, variables: variableTarget?.Invoke(item, target) == true));
                                }
                            itemIndex++;
                        }
                        break;
                }
            }
            return result;
        }
    }
}
