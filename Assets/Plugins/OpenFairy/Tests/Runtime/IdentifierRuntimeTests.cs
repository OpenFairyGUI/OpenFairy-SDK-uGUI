#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if FAIRYGUI_TMPRO
using TextComponent = TMPro.TextMeshProUGUI;
#else
using TextComponent = OpenFairy.UGUI.TextField;
#endif

namespace OpenFairy.UGUI.Tests
{
    public sealed class IdentifierRuntimeTests
    {
        [UnityTest]
        public IEnumerator Generated_pages_gears_and_hooks_survive_prefab_instantiation()
        {
            var fixtureType = Type.GetType("OpenFairy.UGUI.Tests.IdentifierFixture, OpenFairy.UGUI.Tests.Editor", true);
            var fixture = (IDisposable)Activator.CreateInstance(fixtureType);
            GameObject canvas = null;
            try
            {
                var asset = (GameObject)fixtureType.GetMethod("Bake").Invoke(fixture, null);
                canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var instance = Object.Instantiate(asset, canvas.transform, false);
                yield return null;
                var panel = instance.GetComponent<Component>();
                var text = instance.GetComponentInChildren<TextComponent>();
                panel.SetControllerPage(3, 1); yield return null;
                Assert.That(text.text, Is.EqualTo("Second"));
                Assert.That(panel.GetControllerPage(3), Is.EqualTo(1));
                var hook = panel.GetType().GetMethod("Set_classHook");
                var hookEnum = hook.GetParameters()[0].ParameterType;
                var labels = Enum.GetValues(hookEnum); Assert.That(labels.Length, Is.EqualTo(6));
                var hits = 0;
                foreach (var label in labels) hook.Invoke(panel, new object[] { label, (Action)(() => hits++) });
                yield return instance.GetComponents<Transition>()[0].Play().ToCoroutine();
                Assert.That(hits, Is.EqualTo(6));
                canvas.SetActive(false); yield return null; canvas.SetActive(true); yield return null;
                Assert.That(text.text, Is.EqualTo("Second"));
                Assert.That(panel.GetControllerPage(3), Is.EqualTo(1));
            }
            finally { if (canvas != null) Object.DestroyImmediate(canvas); fixture.Dispose(); }
        }
    }
}
#endif
