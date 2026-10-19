using System.IO;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests.Editor
{
    public sealed class NativeRowLayoutImportTests
    {
        [TestCase("", true)]
        [TestCase(" autoItemSize=\"false\"", false)]
        public void AuthoredAutoItemSizeDefaultsAndExplicitValuesAreParsed(string attribute, bool expected)
        {
            var file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, "<component size=\"200,60\"><displayList><list id=\"row\" size=\"200,60\" layout=\"row\"" + attribute + "/></displayList></component>");
                Assert.That(FairyXml.LoadComponent(file).DisplayList[0].AutoItemSize, Is.EqualTo(expected));
            }
            finally { File.Delete(file); }
        }
        [Test]
        public void BuilderBakesNativeReferencesAndRepeatedConfigurationReusesThem()
        {
            var root = new GameObject("Row", typeof(RectTransform));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(200, 60);
                var source = root.AddComponent<ListSource>();
                source.layout = ListLayoutType.SingleRow; source.content = (RectTransform)root.transform;
                source.colGap = 7; source.alignment = TextAnchor.LowerCenter;
                ListLayoutBuilder.Configure(source);
                var layout = source.rowLayout;
                ListLayoutBuilder.Configure(source);
                Assert.That(source.rowLayout, Is.SameAs(layout));
                Assert.That(root.GetComponents<HorizontalLayoutGroup>().Length, Is.EqualTo(1));
                Assert.That(source.rowLayout.childControlWidth || source.rowLayout.childControlHeight, Is.False);
                Assert.That(source.rowLayout.spacing, Is.EqualTo(7));
                Assert.That(source.rowLayout.childAlignment, Is.EqualTo(TextAnchor.LowerCenter));
                Assert.That(source.rowSize.horizontalFit, Is.EqualTo(ContentSizeFitter.FitMode.Unconstrained));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
