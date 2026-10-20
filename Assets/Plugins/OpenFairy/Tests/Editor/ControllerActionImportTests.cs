using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using Schema = OpenFairy.UGUI.Editor.Schema;
using Page = OpenFairy.UGUI.Tests.ControllerActionTestPanel.Page;

namespace OpenFairy.UGUI.Tests
{
    public class ControllerActionImportTests
    {
        public enum Condition { Any, Deleted, Specific }

        [TestCase(Condition.Any, Condition.Any)]
        [TestCase(Condition.Any, Condition.Deleted)]
        [TestCase(Condition.Any, Condition.Specific)]
        [TestCase(Condition.Deleted, Condition.Any)]
        [TestCase(Condition.Deleted, Condition.Deleted)]
        [TestCase(Condition.Deleted, Condition.Specific)]
        [TestCase(Condition.Specific, Condition.Any)]
        [TestCase(Condition.Specific, Condition.Deleted)]
        [TestCase(Condition.Specific, Condition.Specific)]
        public void Saved_actions_preserve_wildcard_deleted_and_specific_conditions(Condition from, Condition to)
        {
            var folder = "Assets/OpenFairyActionConditions_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var source = new GameObject("Panel", typeof(RectTransform));
            GameObject saved = null;
            try
            {
                var panel = source.AddComponent<ControllerActionTestPanel>();
                panel.first.Bake(Page.First);
                panel.second.Bake(Page.First);
                Page[] Filter(Condition condition, Page page) => condition switch {
                    Condition.Any => null,
                    Condition.Deleted => Array.Empty<Page>(),
                    _ => new[] { page },
                };
                var condition = new ChangePageAction<Page> {
                    target = panel, controller = 1,
                    sourcePages = new[] { Page.First, Page.Second, Page.Third }, targetPages = new[] { 0, 1, 2 },
                };
                if (from != Condition.Any) condition.fromPages = Filter(from, Page.First);
                if (to != Condition.Any) condition.toPages = Filter(to, Page.Second);
                panel.first.actions = new ControllerAction<Page>[] { condition };
                var path = folder + "/Panel.prefab";
                PrefabUtility.SaveAsPrefabAsset(source, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                saved = PrefabUtility.LoadPrefabContents(path);
                void Check(ControllerActionTestPanel loaded)
                {
                    var action = (ChangePageAction<Page>)loaded.first.actions[0];
                    Assert.AreEqual(from == Condition.Any, action.fromPages == null);
                    Assert.AreEqual(to == Condition.Any, action.toPages == null);
                    CollectionAssert.AreEqual(Filter(from, Page.First), action.fromPages);
                    CollectionAssert.AreEqual(Filter(to, Page.Second), action.toPages);
                    Assert.AreSame(loaded, action.target);
                    loaded.first.Initialize();
                    loaded.first.page = Page.Second;
                    Assert.AreEqual(from == Condition.Deleted || to == Condition.Deleted ? Page.First : Page.Second,
                        loaded.second.page);
                }
                Check(saved.GetComponent<ControllerActionTestPanel>());
                PrefabUtility.UnloadPrefabContents(saved); saved = null;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                for (int depth = 0; depth <= 2; depth++)
                {
                    var instance = UnityEngine.Object.Instantiate(prefab);
                    try { Check(instance.GetComponentInChildren<ControllerActionTestPanel>()); }
                    finally { UnityEngine.Object.DestroyImmediate(instance); }
                    if (depth == 2) break;
                    var container = new GameObject("Container");
                    try
                    {
                        var nested = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        nested.transform.SetParent(container.transform, false);
                        path = folder + "/Container" + depth + ".prefab";
                        prefab = PrefabUtility.SaveAsPrefabAsset(container, path);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(container); }
                }
            }
            finally
            {
                if (saved != null) PrefabUtility.UnloadPrefabContents(saved);
                UnityEngine.Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static readonly MethodInfo PageMap = typeof(Migrate).GetMethod("ChangePageMap", BindingFlags.NonPublic | BindingFlags.Static);

        [Test]
        public void Action_mapping_uses_page_ids_names_and_indices_as_different_concepts()
        {
            var source = new Schema.Controller { Pages = new[] { ("8", "Idle"), ("2", "Active"), ("5", "Other") } };
            var target = new Schema.Controller { Pages = new[] { ("9", "Active"), ("3", "Idle") } };
            CollectionAssert.AreEqual(new[] { 1, 1, 1 }, (int[])PageMap.Invoke(null, new object[] { source, target, "3" }));
            CollectionAssert.AreEqual(new[] { 0, 1, -1 }, (int[])PageMap.Invoke(null, new object[] { source, target, "~1" }));
            CollectionAssert.AreEqual(new[] { 1, 0, 0 }, (int[])PageMap.Invoke(null, new object[] { source, target, "~2" }));
            CollectionAssert.AreEqual(new[] { -1, -1, -1 }, (int[])PageMap.Invoke(null, new object[] { source, target, "missing" }));
        }

        [Test]
        public void Schema_preserves_actions_labels_and_loader_boolean_without_changing_text_enum()
        {
            var file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, @"
<component size='100,100'>
 <controller name='State' pages='8,Idle,2,Active'>
  <action type='change_page' objectId='child' controller='State' targetPage='~2' fromPage='' toPage='2'/>
  <action type='play_transition' transition='Fade' repeat='-1' delay='0.25' stopOnExit='true'/>
 </controller>
 <displayList>
  <loader id='image' name='Image' autoSize='true' src='image' pkg='fixture2'/>
  <text id='text' name='Text' autoSize='none'><relation target='' sidePair=''/></text>
  <group id='group' name='Group' layout='vt'/>
 </displayList>
 <transition name='Fade'><item type='Alpha' time='0' tween='true' duration='24' label='Begin' label2='End'/></transition>
</component>");
                var component = Schema.Component.Load(file);
                var actions = component.Controllers[0].Actions;
                Assert.AreEqual(2, actions.Length);
                Assert.AreEqual(Schema.ControllerActionType.ChangePage, actions[0].Type);
                Assert.AreEqual("~2", actions[0].TargetPage);
                Assert.AreEqual(Schema.ControllerActionType.PlayTransition, actions[1].Type);
                Assert.AreEqual(-1, actions[1].Repeat);
                Assert.AreEqual(.25f, actions[1].Delay);
                Assert.IsTrue(actions[1].StopOnExit);
                Assert.IsTrue(component.DisplayList[0].LoaderAutoSize);
                Assert.AreEqual("ui://fixture2image", component.DisplayList[0].Source);
                Assert.AreEqual(Schema.TextAutoSize.None, component.DisplayList[1].AutoSize);
                Assert.IsEmpty(component.DisplayList[1].Relations[0].Rules);
                Assert.AreEqual(Schema.ListLayout.SingleColumn, component.DisplayList[2].Layout);
                Assert.AreEqual("Begin", component.Transitions[0].Items[0].Label);
                Assert.AreEqual("End", component.Transitions[0].Items[0].EndLabel);
            }
            finally { File.Delete(file); }
        }
    }
}
