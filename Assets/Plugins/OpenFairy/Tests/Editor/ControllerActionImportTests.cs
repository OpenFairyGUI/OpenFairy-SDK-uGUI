using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class ControllerActionImportTests
    {
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
                Assert.IsEmpty(component.DisplayList[1].Relations[0].SidePairs);
                Assert.AreEqual(Schema.ListLayout.SingleColumn, component.DisplayList[2].Layout);
                Assert.AreEqual("Begin", component.Transitions[0].Items[0].Label);
                Assert.AreEqual("End", component.Transitions[0].Items[0].EndLabel);
            }
            finally { File.Delete(file); }
        }
    }
}
