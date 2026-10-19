using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class DependencyCollectionTests
    {
        private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;

        private static HashSet<string> CollectImages(string xml, params string[] names)
        {
            var file = Path.GetTempFileName();
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", Static).GetValue(null);
            var resourceType = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
            var prefix = "sample" + Guid.NewGuid().ToString("N");
            object Resource(Schema.ResourceKind kind)
            {
                var resource = Activator.CreateInstance(resourceType, true);
                resourceType.GetField("File").SetValue(resource, file);
                resourceType.GetField("PackageId").SetValue(resource, prefix);
                resourceType.GetField("Type").SetValue(resource, kind);
                return resource;
            }
            try
            {
                File.WriteAllText(file, xml.Replace("sample", prefix));
                foreach (var name in names)
                {
                    var image = Resource(Schema.ResourceKind.Image);
                    resourceType.GetField("Id").SetValue(image, name);
                    resources.Add(prefix + name, image);
                }
                var root = Resource(Schema.ResourceKind.Component);
                var components = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(resourceType));
                var images = Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(resourceType));
                typeof(Migrate).GetMethod("Collect", Static).Invoke(null, new[] { root, components, images });
                Assert.That(components.Count, Is.EqualTo(1));
                var collected = new HashSet<string>();
                foreach (var image in (IEnumerable)images)
                    collected.Add((string)resourceType.GetField("Id").GetValue(image));
                return collected;
            }
            finally
            {
                foreach (var name in names) resources.Remove(prefix + name);
                File.Delete(file);
            }
        }

        [Test]
        public void Collection_ignores_only_root_preview_and_keeps_typed_and_unknown_runtime_references()
        {
            var images = CollectImages(@"<component size='100,80' designImage='ui://samplePreview' extensionData='ui://sampleUnknown'>
 <displayList>
  <image id='shown' name='Shown' src='Shown'/>
  <richtext id='rich' name='Rich' text='&lt;img src=&quot;ui://sampleInline&quot;/&gt;'/>
  <text id='vars' name='Variables' vars='true' text='{picture=ui://sampleVariable}'/>
 </displayList>
 <Button selectedIcon='ui://sampleSelected'/>
</component>", "Preview", "Unknown", "Shown", "Inline", "Variable", "Selected");
            CollectionAssert.AreEquivalent(new[] { "Unknown", "Shown", "Inline", "Variable", "Selected" }, images);
        }

        [Test]
        public void A_preview_resource_stays_collected_when_a_runtime_property_also_references_it()
        {
            var images = CollectImages("<component size='100,80' designImage='ui://sampleShared'><Button selectedIcon='ui://sampleShared'/></component>", "Shared");
            CollectionAssert.AreEquivalent(new[] { "Shared" }, images);
        }

        [Test]
        public void Unknown_or_namespaced_properties_and_complete_named_uris_are_preserved()
        {
            var images = CollectImages(@"<component xmlns:custom='urn:custom' size='100,80' designImage='ui://samplePreview'
 designimage='ui://sampleCase' custom:designImage='ui://sampleNamespace'>
 <displayList><text id='text' name='Text' designImage='ui://sampleChild' text='ui://sampleNamed/Child'/></displayList>
</component>", "Preview", "Case", "Namespace", "Child", "Named", "Named/Child");
            CollectionAssert.AreEquivalent(new[] { "Case", "Namespace", "Child", "Named/Child" }, images);
        }

        [Test]
        public void Shared_scanner_retains_sound_font_and_variable_uris_for_early_import()
        {
            var file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, "<component designImage='ui://preview' font='ui://font'><Button sound='ui://sound'/><text text='{asset=ui://variable}'/></component>");
                var matches = (MatchCollection)typeof(Migrate).GetMethod("RuntimeResourceReferences", Static).Invoke(null, new object[] { file });
                var values = new List<string>();
                foreach (Match match in matches) values.Add(match.Value);
                CollectionAssert.AreEquivalent(new[] { "ui://font", "ui://sound", "ui://variable" }, values);
            }
            finally { File.Delete(file); }
        }
    }
}
