#if FAIRYGUI_TMPRO
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Editor
{
    public static partial class Migrate
    {
        public static int RefreshTextShadows()
        {
            _codeGenerationSettings = null;
            var pending = new List<(string Prefab, Schema.Component Xml, List<(Schema.Display Element, string Material)> Bindings)>();
            var total = 0;
            foreach (var package in Directory.EnumerateFiles($"{UiRoot}/assets", "package.xml", SearchOption.AllDirectories))
                foreach (var resource in Schema.Package.Load(package).Resources)
                {
                    if (resource.Kind != Schema.ResourceKind.Component) continue;
                    var path = Path.ChangeExtension(AssetPath(resource.File), ".prefab");
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    var bindings = new List<(Schema.Display, string)>();
                    var xml = Schema.Component.Load(resource.File);
                    foreach (var element in xml.DisplayList)
                    {
                        if (element.Kind is not (Schema.DisplayKind.Text or Schema.DisplayKind.RichText) || element.ShadowColor == null) continue;
                        var text = PublishedShadowText(prefab, xml, element);
                        var material = Path.ChangeExtension(AssetPath(resource.File), null) + "_" + Identifier(element.Id) + "_TextMaterial.mat";
                        TmpTextMaterialBuilder.BakeShadow(text.fontSharedMaterial, text.font, element.FontSize ?? Settings().fontSize,
                            material, ParseColor(element.ShadowColor), element.ShadowOffset ?? new Vector2(1, 1));
                        bindings.Add((element, material));
                    }
                    if (bindings.Count != 0) pending.Add((path, xml, bindings));
                }
            AssetDatabase.SaveAssets();
            foreach (var (path, xml, bindings) in pending)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var changed = false;
                foreach (var (element, material) in bindings)
                {
                    var text = PublishedShadowText(prefab, xml, element);
                    var preset = AssetDatabase.LoadAssetAtPath<Material>(material);
                    total++;
                    if (text.fontSharedMaterial == preset) continue;
                    text.fontSharedMaterial = preset;
                    text.UpdateMeshPadding();
                    EditorUtility.SetDirty(text);
                    changed = true;
                }
                if (changed) PrefabUtility.SavePrefabAsset(prefab);
            }
            Debug.Log($"OpenFairy refreshed {total} text shadows in {pending.Count} published prefabs.");
            return total;
        }

        private static TextMeshProUGUI PublishedShadowText(GameObject prefab, Schema.Component xml, Schema.Display element)
        {
            // The generated public field belongs to the first authored name. Repeated names
            // remain distinct display objects, including those reparented into authored groups.
            var resolved = new Dictionary<string, Transform>();
            string ParentId(Schema.Display child) => !string.IsNullOrEmpty(child.Group) ? child.Group
                : !string.IsNullOrEmpty(xml.Mask) && child.Id != xml.Mask ? xml.Mask : null;
            Transform Resolve(Schema.Display child)
            {
                if (resolved.TryGetValue(child.Id, out var found)) return found;
                var parentId = ParentId(child);
                Transform parent = prefab.GetComponent<ScrollPane>()?.content ?? prefab.transform;
                if (parentId != null)
                {
                    var parentXml = Array.Find(xml.DisplayList, candidate => candidate.Id == parentId);
                    if (parentXml == null) throw new InvalidOperationException($"Authored parent '{parentId}' is missing from '{prefab.name}'.");
                    parent = Resolve(parentXml);
                }
                var ordinal = 0;
                var expectedCount = 0;
                foreach (var sibling in xml.DisplayList)
                    if (sibling.Name == child.Name && ParentId(sibling) == parentId)
                    {
                        if (sibling.Id == child.Id) ordinal = expectedCount;
                        expectedCount++;
                    }
                var actualCount = 0;
                for (var index = 0; index < parent.childCount; index++)
                {
                    var candidate = parent.GetChild(index);
                    if (candidate.name != child.Name) continue;
                    if (actualCount++ == ordinal) found = candidate;
                }
                if (actualCount != expectedCount || found == null)
                    throw new InvalidOperationException($"Authored display '{child.Id}' does not match the published hierarchy in '{prefab.name}'. Regenerate its bindings before refreshing shadows.");
                resolved.Add(child.Id, found);
                return found;
            }
            var node = Resolve(element);
            var text = node.GetComponent<TMP_InputField>() is { } input
                ? input.textComponent as TextMeshProUGUI : node.GetComponent<TextMeshProUGUI>();
            if (text == null)
                throw new InvalidOperationException($"Authored text '{element.Id}' is missing from '{prefab.name}'.");
            var field = Field(element.Name);
            var first = Array.Find(xml.DisplayList, child => Field(child.Name) == field);
            var published = first == element && !Array.Exists(xml.Controllers, controller => Field(controller.Name) == field)
                && !(CodeSettings().ignoreNoname && System.Text.RegularExpressions.Regex.IsMatch(element.Name ?? "", @"\An[0-9]+\z"));
            if (published)
            {
                var owner = prefab.GetComponent<Component>();
                var value = owner.GetType().GetField(field)?.GetValue(owner);
                var direct = value is TMP_InputField directInput ? directInput.textComponent : value as TMP_Text;
                if (direct != text)
                    throw new InvalidOperationException($"Published text field '{field}' is missing or mismatched in '{prefab.name}'. Regenerate its bindings before refreshing shadows.");
            }
            return text;
        }
    }
}
#endif
