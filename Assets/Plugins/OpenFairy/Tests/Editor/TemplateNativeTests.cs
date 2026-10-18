#if FAIRYGUI_TMPRO
using System.Collections.Generic;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using TMPro;
using UnityEngine;

namespace OpenFairy.UGUI.Tests
{
    // Native editor tests: run after integrating the candidate; never replace with offline claims.
    public sealed class TemplateNativeTests
    {
        [Test]
        public void CompiledTemplateSurvivesNativeSerialization()
        {
            var variables = new List<string>();
            var source = TextTemplateCompiler.Compile("Value {count=2}", variables);
            var restored = JsonUtility.FromJson<TextTemplate>(JsonUtility.ToJson(source));
            var state = new TextTemplateState(variables.Count);
            state.Set(0, "7");
            Assert.That(state.Format(restored), Is.EqualTo("Value 7"));
        }

        [Test]
        public void GeneratedNativeBindingUpdatesGearChoicesWithoutChangingControllerPage()
        {
            var go = new GameObject("Template panel");
            try
            {
                var panel = go.AddComponent<TemplateGeneratedPanel>();
                var caption = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
                caption.transform.SetParent(go.transform, false);
                panel.caption = caption.GetComponent<TextMeshProUGUI>();
                panel.gear = new GearText<TemplateGeneratedPanel.Page> {
                    target = caption, pages = new[] { TemplateGeneratedPanel.Page.First }, values = new[] { "" }, defaultValue = "",
                };
                panel.mode = new Controller<TemplateGeneratedPanel.Page> { gears = new Gear<TemplateGeneratedPanel.Page>[] { panel.gear } };
                int changed = 0;
                panel.mode.onChanged += () => changed++;
                TextTemplate Compile(string text) => TextTemplateCompiler.Compile(text, new List<string>());
                panel.ApplyCaptionTexts(new TemplateGeneratedPanel.CaptionTexts {
                    Base = Compile("Count {count=1}"), Page = Compile("First {count=2}"), Default = Compile("Other {count=3}"),
                });
                panel.SetCaptionVariable(TemplateGeneratedPanel.CaptionVariable.count, "9");
                panel.RefreshCaptionText();
                Assert.That(panel.caption.text, Is.EqualTo("First 9"));
                Assert.That(panel.mode.page, Is.EqualTo(TemplateGeneratedPanel.Page.First));
                Assert.That(changed, Is.Zero);
                panel.ApplyCaptionTexts(new TemplateGeneratedPanel.CaptionTexts {
                    Base = Compile("Total {count=4}"), Page = Compile("Primary {count=5}"), Default = Compile("Alternate {count=6}"),
                });
                Assert.That(panel.caption.text, Is.EqualTo("Primary 9"));
                Assert.That(changed, Is.Zero);
                panel.gear.Apply(TemplateGeneratedPanel.Page.Other);
                Assert.That(panel.caption.text, Is.EqualTo("Alternate 9"));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ExistingLabelReceivesFormattedNativeText()
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Label));
            try
            {
                var text = new GameObject("title", typeof(RectTransform), typeof(TextMeshProUGUI));
                text.transform.SetParent(go.transform, false);
                var label = go.GetComponent<Label>();
                label.titleText = text.GetComponent<TextMeshProUGUI>();
                var names = new List<string>();
                var template = TextTemplateCompiler.Compile("Hello {name=Guest}", names);
                var state = new TextTemplateState(names.Count);
                state.Set(0, "Ada");
                label.title = state.Format(template);
                Assert.That(label.title, Is.EqualTo("Hello Ada"));
                Assert.That(label.titleText.text, Is.EqualTo("Hello Ada"));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SelectedButtonTitleRefreshPreservesPressedVisualPage()
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(TemplateGeneratedButton));
            try
            {
                var text = new GameObject("title", typeof(RectTransform), typeof(TextMeshProUGUI));
                text.transform.SetParent(go.transform, false);
                var button = go.GetComponent<TemplateGeneratedButton>();
                button.titleText = text.GetComponent<TextMeshProUGUI>();
                button.mode = ButtonMode.Radio;
                button.selected = true;
                button.controller.page = TemplateGeneratedButton.Mode.over;
                TextTemplate Compile(string source) => TextTemplateCompiler.Compile(source, new List<string>());
                button.ApplyButtonTexts(new TemplateGeneratedButton.ButtonTexts {
                    Title = Compile("Hello {name=Guest}"), Selected = Compile("Chosen {name=Guest}"),
                });
                button.SetButtonVariable(TemplateGeneratedButton.ButtonVariable.name, "Ada");
                button.RefreshButtonText();
                Assert.That(button.titleText.text, Is.EqualTo("Chosen Ada"));
                Assert.That(button.controller.page, Is.EqualTo(TemplateGeneratedButton.Mode.over));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
#endif
