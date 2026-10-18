using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;

namespace OpenFairy.UGUI.Tests
{
    internal sealed class ImportLocalizationFixture : IDisposable
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly Type Migrator = typeof(Migrate);
        private readonly FieldInfo _translations = Migrator.GetField("_translations", PrivateStatic);
        private readonly object _previousTranslations;
        private readonly IDictionary _views = (IDictionary)Migrator.GetField("LocalViews", PrivateStatic).GetValue(null);
        private readonly Dictionary<object, object> _previousViews = new();

        internal ImportLocalizationFixture()
        {
            _previousTranslations = _translations.GetValue(null);
            foreach (DictionaryEntry entry in _views) _previousViews.Add(entry.Key, entry.Value);
            _translations.SetValue(null, Array.Empty<FairyTranslations>());
        }

        internal void Prepare(object resource)
        {
            var resources = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(resource.GetType()));
            resources.Add(resource);
            Migrator.GetMethod("BuildLocalizationPlans", PrivateStatic).Invoke(null, new object[] { resources });
        }

        internal void AssertGenerated(object resource, Type fixture)
        {
            var guid = Array.Find(AssetDatabase.FindAssets(fixture.Name + " t:MonoScript"),
                item => Path.GetFileName(AssetDatabase.GUIDToAssetPath(item)) == fixture.Name + ".cs");
            var frozen = File.ReadAllText(AssetDatabase.GUIDToAssetPath(guid));
            var generated = (string)Migrator.GetMethod("ScriptCode", PrivateStatic).Invoke(null, new[] { resource });
            Assert.That(generated.Replace("\r\n", "\n"),
                Is.EqualTo(frozen.Replace("protected internal override", "protected override").Replace("\r\n", "\n")),
                "The compiled fixture must match the real generator, including static text and gear fields.");
        }

        public void Dispose()
        {
            _views.Clear();
            foreach (var entry in _previousViews) _views.Add(entry.Key, entry.Value);
            _translations.SetValue(null, _previousTranslations);
        }
    }
}
