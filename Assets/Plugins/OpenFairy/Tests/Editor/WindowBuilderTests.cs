using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;

namespace OpenFairy.UGUI.Tests
{
    public class WindowBuilderTests
    {
        [Test]
        public void Native_window_references_survive_prefab_save_and_reload_without_name_lookup()
        {
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/TemporaryWindow.prefab");
            var go = new GameObject("Panel", typeof(RectTransform));
            try
            {
                var window = go.AddComponent<Window>();
                var show = go.AddComponent<Transition>();
                show.items = System.Array.Empty<TransitionItem>();
                var hide = go.AddComponent<Transition>();
                hide.items = System.Array.Empty<TransitionItem>();
                var area = new GameObject("Header", typeof(RectTransform));
                area.transform.SetParent(go.transform, false);
                var close = new GameObject("Close", typeof(RectTransform));
                close.transform.SetParent(go.transform, false);
                var closeButton = close.AddComponent<ButtonAliasTestPanel>();
                WindowBuilder.Configure(window, show, hide, closeButton, (RectTransform)area.transform);
                PrefabUtility.SaveAsPrefabAsset(go, path);
                var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var stored = saved.GetComponent<Window>();
                Assert.That(stored.showTransition, Is.Not.Null);
                Assert.That(stored.hideTransition, Is.Not.SameAs(stored.showTransition));
                Assert.That(stored.canvasGroup, Is.SameAs(saved.GetComponent<CanvasGroup>()));
                Assert.That(stored.closeButton, Is.SameAs(saved.GetComponentInChildren<ButtonAliasTestPanel>()));
                Assert.That(saved.GetComponentInChildren<WindowDragArea>().window, Is.SameAs(stored));
                Assert.That(saved.GetComponentInChildren<UnityEngine.UI.Graphic>().raycastTarget, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
