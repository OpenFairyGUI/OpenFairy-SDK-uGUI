using System;
using System.Collections.Generic;
using System.Xml.Linq;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;

namespace OpenFairy.UGUI.Tests
{
    public sealed class TextTemplateTests
    {
#if FAIRYGUI_TMPRO
        [Test]
        public void Image_variants_without_images_preserve_text_and_do_not_resolve_resources()
        {
            var source = new[] { "plain {value}", null, "" };
            var result = TmpSpriteAssetBuilder.BakeInlineImageVariants("unused.asset", source, true, true,
                _ => throw new InvalidOperationException("No image was authored."), out var sprites);
            CollectionAssert.AreEqual(source, result);
            Assert.That(sprites, Is.Null);
        }
#endif
        [TestCase("plain", "plain")]
        [TestCase("{name=Guest}", "Guest")]
        [TestCase("{name}", "")]
        [TestCase("{}", "{}")]
        [TestCase("a{broken", "a{broken")]
        [TestCase(@"\{name}", "{name}")]
        [TestCase(@"\\{name}", @"\{name}")]
        [TestCase("{name=a=b}", "a=b")]
        [TestCase("{=empty}", "empty")]
        public void DefaultsAndEscapes(string source, string expected)
        {
            var variables = new List<string>();
            var template = TextTemplateCompiler.Compile(source, variables);
            Assert.That(new TextTemplateState(variables.Count).Format(template), Is.EqualTo(expected));
        }

        [Test]
        public void AssignmentsPreserveNullEmptyAndMissing()
        {
            var variables = new List<string>();
            var template = TextTemplateCompiler.Compile("{x=first}/{x=second}", variables);
            var state = new TextTemplateState(variables.Count);
            Assert.That(state.Format(template), Is.EqualTo("first/second"));
            state.Set(0, null);
            Assert.That(state.Format(template), Is.EqualTo("/"));
            state.Set(0, "");
            Assert.That(state.Format(template), Is.EqualTo("/"));
            state.Set(0, "value");
            Assert.That(state.Format(template), Is.EqualTo("value/value"));
            state.Clear(0);
            Assert.That(state.Format(template), Is.EqualTo("first/second"));
        }

        [Test]
        public void LocaleAndGearChoicesShareVariablesWithoutLeakingOtherTextState()
        {
            var translations = new FairyTranslations("<resources><string name='pkgpanel-label'>Hello {name=Guest}</string><string name='pkgpanel-label-texts_0'>{count=3} items</string></resources>");
            var family = TextFamilyCompiler.Compile("pkgpanel", new[] {
                new TextChoice("label", "Welcome {name=Visitor}", true),
                new TextChoice("label-texts_0", "Count {count=1}", true),
            }, new[] { translations });
            var state = new TextTemplateState(family.variables.Length);
            state.Set(Array.IndexOf(family.variables, "name"), "Ada");
            Assert.That(state.Format(family.authored[0]), Is.EqualTo("Welcome Ada"));
            Assert.That(state.Format(family.translations[0][0]), Is.EqualTo("Hello Ada"));
            state.Set(Array.IndexOf(family.variables, "count"), "8");
            Assert.That(state.Format(family.translations[0][1]), Is.EqualTo("8 items"));
            Assert.That(new TextTemplateState(family.variables.Length).Format(family.translations[0][0]), Is.EqualTo("Hello Guest"));
        }

        [Test]
        public void LiteralTextWithoutVarsDoesNotParseBraces()
        {
            var family = TextFamilyCompiler.Compile("pkgpanel", new[] { new TextChoice("label", "{literal}", false) }, Array.Empty<FairyTranslations>());
            Assert.That(family.variables, Is.Empty);
            Assert.That(new TextTemplateState(0).Format(family.authored[0]), Is.EqualTo("{literal}"));
        }

        [Test]
        public void InstanceOverrideSharesChildSlotsAndUsesOwningTranslationKey()
        {
            var catalog = new FairyTranslations("<resources><string name='parent-child-cp-title'>Override {extra=More}</string><string name='child-title'>Hello {name=Guest}</string><string name='child-title-texts_0'>Page {name=Guest}</string></resources>");
            var schema = new List<string>();
            var original = TextFamilyCompiler.Compile("child", new[] { new TextChoice("title", "Title {name=Guest}", true) }, new[] { catalog }, schema);
            var instance = TextFamilyCompiler.Compile("child", new[] {
                new TextChoice("child-cp-title", "Override {extra=More}", true, "parent"),
                new TextChoice("title-texts_0", "Page {name=Guest}", true),
            }, new[] { catalog }, schema);
            Assert.That(original.variables, Is.EqualTo(new[] { "name", "extra" }));
            var state = new TextTemplateState(schema.Count);
            state.Set(0, "Ada");
            state.Set(1, "Custom");
            Assert.That(state.Format(original.translations[0][0]), Is.EqualTo("Hello Ada"));
            Assert.That(state.Format(instance.translations[0][0]), Is.EqualTo("Override Custom"));
            Assert.That(state.Format(instance.translations[0][1]), Is.EqualTo("Page Ada"));
        }

        [Test]
        public void EmptyComponentHasNoTextBindings()
        {
            Assert.That(FairyTextSlots.Read(XElement.Parse("<component size='20,20'/>")), Is.Empty);
        }

        [Test]
        public void TranslationMissingFallsBackButPresentEmptyWinsAndLastDuplicateWins()
        {
            var translations = new FairyTranslations("<resources><string name='ignored'>X</string><string name='pkgpanel-a'></string><string name='pkgpanel-b'>First</string><string name='pkgpanel-b'>Last</string></resources>");
            Assert.That(translations.Resolve("pkgpanel", "a", "Source"), Is.Empty);
            Assert.That(translations.Resolve("pkgpanel", "missing", "Source"), Is.EqualTo("Source"));
            Assert.That(translations.Resolve("pkgpanel", "b", "Source"), Is.EqualTo("Last"));
        }

        [Test]
        public void RawTranslationXmlPreservesAuthoredLineEndings()
        {
            var translations = new FairyTranslations("<resources><string name='panel-label'>One\rTwo\r\nThree\nFour</string></resources>");
            Assert.That(translations.Resolve("panel", "label", ""), Is.EqualTo("One\rTwo\r\nThree\nFour"));
        }

        [Test]
        public void NativeSlotKeysIncludeOverridesListsGearDefaultsAndPrompts()
        {
            var slots = FairyTextSlots.Read(XElement.Parse("<component><displayList>"
                + "<text id='label' vars='true' text='{n=1}' prompt='Hint' tooltips='Tip'><gearText controller='Mode' pages='a,b' values='One|Two' default='Other'/></text>"
                + "<component id='button'><Button title='Title' selectedTitle='Selected'/><property target='caption' name='text' value='Override'/></component>"
                + "<component id='combo'><ComboBox title='Pick'><item title='Alpha'/><item title='Beta'/></ComboBox></component>"
                + "<list id='list'><item title='Row' selectedTitle='Chosen'><property target='caption' name='text' value='Nested'/></item></list>"
                + "</displayList></component>"), (element, target) => (string)element.Attribute("id") == "button" && target == "caption");
            var keys = new List<string>();
            foreach (var slot in slots) keys.Add(slot.suffix);
            Assert.That(keys, Does.Contain("label-tips"));
            Assert.That(keys, Does.Contain("label-texts_0"));
            Assert.That(keys, Does.Contain("label-texts_1"));
            Assert.That(keys, Does.Contain("label-texts_def"));
            Assert.That(keys, Does.Contain("label-prompt"));
            Assert.That(keys, Does.Contain("button-0"));
            Assert.That(keys, Does.Contain("button-cp-caption"));
            Assert.That(keys, Does.Contain("combo-1"));
            Assert.That(keys, Does.Contain("list-0-0"));
            Assert.That(keys, Does.Contain("list-0-caption"));
            Assert.That(slots.Find(slot => slot.suffix == "button-cp-caption").variables, Is.True);
        }
    }
}
