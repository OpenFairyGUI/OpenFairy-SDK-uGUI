#if UNITY_EDITOR && FAIRYGUI_TMPRO
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using OpenFairyTextFixtures.Localization;
using OpenFairyTextFixtures.NativeTextFixture;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public sealed class DuplicateCaptionLocalizationTests
    {
        [UnityTest]
        public IEnumerator Persisted_duplicate_caption_targets_switch_languages_independently_in_play()
        {
            var fixtureType = Type.GetType("OpenFairy.UGUI.Tests.LocalizationImportTests, OpenFairy.UGUI.Tests.Editor", true);
            var fixture = Activator.CreateInstance(fixtureType);
            GameObject instance = null;
            try
            {
                fixtureType.GetMethod("Bake_real_prefabs_and_language_assets").Invoke(fixture, null);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UIProject/Assets/NativeTextFixture/NativeTextPanel.prefab");
                instance = Object.Instantiate(prefab);
                var view = instance.GetComponent<NativeTextPanel>();
                var first = view.m_Main.m_title;
                var targets = new TMP_Text[4];
                for (var i = 0; i < targets.Length; i++)
                    targets[i] = (TMP_Text)typeof(NativeTextButton).GetField("_localizedText" + (i + 2), BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view.m_Main);
                var french = AssetDatabase.LoadAssetAtPath<LocaleTable>("Assets/__OpenFairyTextFixture/Locales/French.asset");
                var english = AssetDatabase.LoadAssetAtPath<LocaleTable>("Assets/__OpenFairyTextFixture/Locales/English.asset");
                view.ApplyLocale(french);
                yield return null;
                Assert.That(first.text, Is.EqualTo("Bonjour Auteur"));
                Assert.That(Array.ConvertAll(targets, t => t.text), Is.EqualTo(new[] { "Deuxième traduit", "Troisième traduit", "Quatrième traduit", "Cinquième traduit" }));
                view.m_Main.title = "Runtime primary"; view.m_Main.RefreshState();
                yield return null;
                Assert.That(first.text, Is.EqualTo("Runtime primary"));
                Assert.That(targets[3].text, Is.EqualTo("Cinquième traduit"));
                view.ApplyLocale(english);
                yield return null;
                Assert.That(Array.ConvertAll(targets, t => t.text), Is.EqualTo(new[] { "Second English", "Third English", "Fourth English", "Fifth English" }));
                view.ApplyLocale(french);
                yield return null;
                Assert.That(first, Is.SameAs(view.m_Main.m_title));
                Assert.That(first.text, Is.EqualTo("Bonjour Auteur"));
                Assert.That(targets[0].transform.parent, Is.Not.SameAs(targets[3].transform.parent));
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                fixtureType.GetMethod("Remove_only_the_owned_fixture_and_restore_importer_state").Invoke(fixture, null);
            }
        }
    }
}
#endif
