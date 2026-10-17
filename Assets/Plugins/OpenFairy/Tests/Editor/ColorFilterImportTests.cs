using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEngine;
using UnityEngine.UI;
using Schema = OpenFairy.UGUI.Editor.Schema;
#if FAIRYGUI_TMPRO
using TMPro;
#endif

namespace OpenFairy.UGUI.Tests
{
    public class ColorFilterImportTests
    {
        [Test]
        public void Schema_color_filter_is_typed_and_uses_invariant_numbers()
        {
            var path = Path.GetTempFileName();
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                File.WriteAllText(path, "<component size='100,100'><displayList><image id='picture' filter='color' filterData='0.12,0.25,-0.5,1'/><component id='panel' filter='color' filterData='0,0,0,-0.12'/></displayList></component>");
                var component = Schema.Component.Load(path);
                Assert.That(component.DisplayList[0].Filter, Is.EqualTo(Schema.FilterKind.Color));
                Assert.That(component.DisplayList[0].FilterData.Brightness, Is.EqualTo(.12f));
                Assert.That(component.DisplayList[0].FilterData.Contrast, Is.EqualTo(.25f));
                Assert.That(component.DisplayList[0].FilterData.Saturation, Is.EqualTo(-.5f));
                Assert.That(component.DisplayList[1].FilterData.Hue, Is.EqualTo(-.12f));
            }
            finally { CultureInfo.CurrentCulture = culture; File.Delete(path); }
        }

        [Test]
        public void Schema_rejects_a_color_filter_with_missing_channels()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "<component><displayList><image filter='color' filterData='0,0,0'/></displayList></component>");
                Assert.Throws<FormatException>(() => Schema.Component.Load(path));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void Builder_bakes_direct_image_and_container_references_without_replacing_the_source()
        {
            var parent = new GameObject("Parent", typeof(RectTransform));
            var material = new Material(Shader.Find("OpenFairy/UI Premultiplied"));
            try
            {
                var source = new GameObject("Source", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                source.transform.SetParent(parent.transform, false);
                var graphic = source.GetComponent<Graphic>();
                var filter = ColorFilterBuilder.Configure(source, false, Shader.Find("OpenFairy/UI ColorMatrix"), null, null, 31);
                Assert.That(filter.graphic, Is.SameAs(graphic));
                Assert.That(filter.presentation, Is.Null);
                ColorFilterBuilder.Configure(source, true, filter.shader, Shader.Find("OpenFairy/UI Capture"), material, 31);
                var presentation = filter.presentation;
                Assert.That(filter.graphic, Is.Null);
                Assert.That(presentation.transform.parent, Is.SameAs(source.transform.parent));
                Assert.That(presentation.raycastTarget, Is.False);
                Assert.That(source.GetComponent<Graphic>(), Is.SameAs(graphic));
                Assert.That(source.GetComponent<ObjectFilterSourceVisibility>(), Is.Not.Null);
                ColorFilterBuilder.Configure(source, true, filter.shader, filter.captureShader, material, 31);
                Assert.That(filter.presentation, Is.SameAs(presentation));
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); UnityEngine.Object.DestroyImmediate(material); }
        }

#if FAIRYGUI_TMPRO
        [Test]
        public void Builder_keeps_a_native_text_material_when_capturing_text()
        {
            var parent = new GameObject("Parent", typeof(RectTransform));
            var material = new Material(Shader.Find("OpenFairy/UI Premultiplied"));
            try
            {
                var source = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                source.transform.SetParent(parent.transform, false);
                var text = source.GetComponent<TextMeshProUGUI>();
                var original = text.fontSharedMaterial;
                var filter = ColorFilterBuilder.Configure(source, true, Shader.Find("OpenFairy/UI ColorMatrix"),
                    Shader.Find("OpenFairy/UI Capture"), material, 31);
                Assert.That(filter.graphic, Is.Null);
                Assert.That(text.fontSharedMaterial, Is.SameAs(original));
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); UnityEngine.Object.DestroyImmediate(material); }
        }
#endif
    }
}
