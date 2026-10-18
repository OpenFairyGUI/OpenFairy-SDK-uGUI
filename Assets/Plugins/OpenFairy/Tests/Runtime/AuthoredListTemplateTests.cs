using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public sealed class AuthoredListTemplateTests
    {
        [TestCase(ListLayoutType.SingleColumn)]
        [TestCase(ListLayoutType.SingleRow)]
        [TestCase(ListLayoutType.FlowHorizontal)]
        [TestCase(ListLayoutType.FlowVertical)]
        public void Authored_variants_never_enter_the_default_refill_pool(ListLayoutType layout)
        {
            var root = new GameObject("List", typeof(RectTransform), typeof(ListSource));
            var template = new GameObject("Default", typeof(RectTransform), typeof(LayoutElement));
            template.GetComponent<LayoutElement>().preferredWidth = 40;
            template.SetActive(false);
            try
            {
                var source = root.GetComponent<ListSource>();
                source.content = (RectTransform)root.transform;
                source.content.sizeDelta = new Vector2(100, 100);
                source.itemPrefab = template;
                source.itemSize = new Vector2(40, 20);
                source.layout = layout;
                var authored = Object.Instantiate(template, source.content);
                authored.SetActive(true);
                var variant = new GameObject("Variant", typeof(RectTransform), typeof(LayoutElement));
                variant.transform.SetParent(source.content, false);
                variant.GetComponent<LayoutElement>().preferredWidth = 60;
                source.variantItems = new[] { variant };
                source.count = 2;
                Assert.That(source.GetItemAt(0), Is.SameAs(authored));
                Assert.That(source.GetItemAt(1), Is.SameAs(variant));
                source.Fill(4, (item, _) => Assert.That(item.GetComponent<LayoutElement>().preferredWidth, Is.EqualTo(40)));
                source.Fill(2, (item, _) => Assert.That(item.GetComponent<LayoutElement>().preferredWidth, Is.EqualTo(40)));
                Assert.That(source.count, Is.EqualTo(2));
                source.Fill(4, (item, index) => item.name = "Bound" + index);
                for (var i = 0; i < 4; i++) Assert.That(source.GetItemAt(i).name, Is.EqualTo("Bound" + i));
                source.Fill(2, (item, index) => item.name = "Refill" + index);
                for (var i = 0; i < 2; i++) Assert.That(source.GetItemAt(i).name, Is.EqualTo("Refill" + i));
                Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => source.GetItemAt(3));
                Assert.That(variant.activeInHierarchy, Is.False);
                Assert.That(variant.transform.IsChildOf(root.transform), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(template);
            }
        }
    }
}
