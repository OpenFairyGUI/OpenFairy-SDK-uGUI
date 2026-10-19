using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using Page = OpenFairy.UGUI.Tests.ControllerActionTestPanel.Page;

namespace OpenFairy.UGUI.Tests
{
    public class ControllerActionTests
    {
        private readonly List<GameObject> _objects = new();

        [UnityTest]
        public IEnumerator Reopening_a_fading_target_does_not_deactivate_it_between_display_locks()
        {
            var go = new GameObject("Animated visibility", typeof(RectTransform), typeof(CanvasGroup), typeof(CaptureLifecycleWitness));
            _objects.Add(go);
            var display = new GearDisplay<Page> { target = go, pages = new[] { Page.First } };
            var look = new GearLook<Page> {
                target = go, pages = new[] { Page.First, Page.Second }, alphas = new[] { 1f, 0f },
                rotations = new[] { 0f, 0f }, grayed = new[] { false, false }, touchables = new[] { true, false },
                tween = true, duration = .1f,
            };
            var controller = new Controller<Page> { gears = new Gear<Page>[] { display, look } };
            controller.page = Page.First;
            var enables = CaptureLifecycleWitness.enables;
            controller.page = Page.Second;
            Assert.IsTrue(go.activeSelf, "The outgoing tween holds its display lock.");
            controller.page = Page.First;
            Assert.IsTrue(go.activeSelf);
            Assert.AreEqual(enables, CaptureLifecycleWitness.enables, "Replacing the outgoing tween must not disable and re-enable the target.");
            controller.page = Page.Second;
            var deadline = Time.realtimeSinceStartup + 5;
            while (go.activeSelf && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsFalse(go.activeSelf, "The final outgoing tween still releases visibility normally.");
        }

        [Serializable]
        private sealed class RecordGear : Gear<Page>
        {
            public Action apply;
            public override void Apply(Page page) => apply();
        }

        [Serializable]
        private sealed class RecordAction : ControllerAction<Page>
        {
            public Action enter, leave;
            protected override void Enter(Page page) => enter?.Invoke();
            protected override void Leave() => leave?.Invoke();
        }

        private ControllerActionTestPanel Panel()
        {
            var go = new GameObject("Panel", typeof(RectTransform));
            _objects.Add(go);
            return go.AddComponent<ControllerActionTestPanel>();
        }

        private Transition Timeline()
        {
            var go = new GameObject("Timeline", typeof(RectTransform), typeof(CanvasGroup));
            _objects.Add(go);
            var timeline = go.AddComponent<Transition>();
            timeline.items = new[] { new TransitionItem {
                type = TransitionItemType.Alpha, tween = true, duration = 1,
                ease = Ease.Linear, start = new[] { 0f }, end = new[] { 1f },
            } };
            return timeline;
        }

        private static ChangePageAction<Page> Copy(ControllerActionTestPanel target, int controller) => new() {
            target = target, controller = controller,
            sourcePages = new[] { Page.First, Page.Second, Page.Third }, targetPages = new[] { 0, 1, 2 },
        };

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go.TryGetComponent<Transition>(out var timeline)) timeline.Stop();
                UnityEngine.Object.DestroyImmediate(go);
            }
            _objects.Clear();
        }

        [UnityTest]
        public IEnumerator Button_pointer_state_and_generated_reference_share_one_controller()
        {
            var go = new GameObject("Button", typeof(RectTransform));
            _objects.Add(go);
            var button = go.AddComponent<ButtonAliasTestPanel>();
            button.controller.Bake(ButtonAliasTestPanel.State.up);
            var changed = 0;
            button.state.onChanged += () => changed++;
            button.OnPointerDown(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            Assert.AreEqual(ButtonAliasTestPanel.State.down, button.state.page);
            Assert.AreEqual(button.state.page, button.controller.page);
            Assert.AreEqual(1, changed);
            button.state.page = ButtonAliasTestPanel.State.over;
            Assert.AreEqual(ButtonAliasTestPanel.State.over, button.controller.page);
            Assert.AreEqual(2, changed);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Page_change_applies_gears_actions_then_changed_and_skips_same_page()
        {
            var panel = Panel();
            var order = new List<string>();
            panel.first.gears = new Gear<Page>[] { new RecordGear { apply = () => order.Add("gear") } };
            panel.second.gears = new Gear<Page>[] { new RecordGear { apply = () => order.Add("target") } };
            panel.first.Bake(Page.First);
            panel.second.Bake(Page.First);
            panel.first.actions = new ControllerAction<Page>[] { Copy(panel, 1) };
            panel.first.Initialize();
            panel.first.onChanged += () => {
                Assert.IsTrue(panel.first.changing);
                Assert.AreEqual(Page.Second, panel.second.page);
                order.Add("changed");
            };
            order.Clear();
            panel.first.page = Page.Second;
            CollectionAssert.AreEqual(new[] { "gear", "target", "changed" }, order);
            Assert.IsFalse(panel.first.changing);
            panel.first.page = Page.Second;
            Assert.AreEqual(3, order.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Change_page_cycle_does_not_reenter_a_changing_controller()
        {
            var panel = Panel();
            panel.first.Bake(Page.First);
            panel.second.Bake(Page.First);
            var back = Copy(panel, 0);
            back.targetPages[1] = 2;
            panel.first.actions = new ControllerAction<Page>[] { Copy(panel, 1) };
            panel.second.actions = new ControllerAction<Page>[] { back };
            panel.first.page = Page.Second;
            Assert.AreEqual(Page.Second, panel.first.page);
            Assert.AreEqual(Page.Second, panel.second.page);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Initial_actions_have_no_previous_page_and_empty_conditions_match_nothing()
        {
            var controller = new Controller<Page>();
            controller.Bake(Page.First);
            var entered = 0;
            var left = 0;
            controller.actions = new ControllerAction<Page>[] {
                new RecordAction { fromPages = new[] { Page.First }, enter = () => entered++, leave = () => left++ },
                new RecordAction { toPages = Array.Empty<Page>(), enter = () => entered++, leave = () => left++ },
            };
            controller.Initialize();
            Assert.AreEqual(0, entered);
            Assert.AreEqual(2, left);
            controller.page = Page.Second;
            Assert.AreEqual(1, entered);
            Assert.AreEqual(3, left);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Initial_actions_initialize_children_before_parent_and_do_not_emit_changed()
        {
            var parentGo = new GameObject("Parent", typeof(RectTransform));
            var childGo = new GameObject("Child", typeof(RectTransform));
            _objects.Add(parentGo);
            parentGo.SetActive(false);
            childGo.transform.SetParent(parentGo.transform, false);
            var parent = parentGo.AddComponent<ControllerActionTestPanel>();
            var child = childGo.AddComponent<ControllerActionTestPanel>();
            parent.controllerChildren = new Component[] { child };
            var order = new List<string>();
            child.first.Bake(Page.First);
            parent.first.Bake(Page.First);
            child.first.actions = new ControllerAction<Page>[] { new RecordAction { enter = () => order.Add("child") } };
            var setChild = Copy(child, 0);
            setChild.targetPages[0] = 1;
            parent.first.actions = new ControllerAction<Page>[] { setChild, new RecordAction { enter = () => order.Add("parent") } };
            var changes = 0;
            parent.first.onChanged += () => changes++;
            parentGo.SetActive(true);
            CollectionAssert.AreEqual(new[] { "child", "child", "parent" }, order);
            Assert.AreEqual(Page.Second, child.first.page);
            Assert.AreEqual(0, changes);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Matching_transition_action_keeps_progress_and_stop_on_exit_stops_without_hooks()
        {
            var timeline = Timeline();
            var controller = new Controller<Page>();
            controller.Bake(Page.First);
            controller.actions = new ControllerAction<Page>[] { new PlayTransitionAction<Page> {
                transition = timeline, toPages = new[] { Page.Second, Page.Third }, playTimes = -1, stopOnExit = true,
            } };
            var ended = 0;
            timeline.SetHook(0, true, () => ended++);
            controller.page = Page.Second;
            timeline.Step(.5f);
            var alpha = timeline.GetComponent<CanvasGroup>().alpha;
            controller.page = Page.Third;
            Assert.AreEqual(alpha, timeline.GetComponent<CanvasGroup>().alpha);
            Assert.IsTrue(timeline.playing);
            controller.page = Page.First;
            Assert.IsFalse(timeline.playing);
            Assert.AreEqual(0, ended);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Negative_play_times_repeat_until_changed_and_hooks_repeat_once_per_cycle()
        {
            var timeline = Timeline();
            var starts = 0;
            var ends = 0;
            timeline.SetHook(0, false, () => starts++);
            timeline.SetHook(0, true, () => ends++);
            timeline.Play(-1).Forget();
            timeline.Step(1);
            Assert.IsTrue(timeline.playing);
            timeline.Step(0);
            Assert.AreEqual(2, starts);
            Assert.AreEqual(1, ends);
            timeline.ChangePlayTimes(1);
            timeline.Step(1);
            Assert.IsFalse(timeline.playing);
            Assert.AreEqual(2, ends);
            yield return null;
        }

        [UnityTest]
        public IEnumerator End_hook_waits_for_all_item_repeats_and_clear_hooks_removes_both()
        {
            var timeline = Timeline();
            timeline.items[0].repeat = 1;
            var starts = 0;
            var ends = 0;
            timeline.SetHook(0, false, () => starts++);
            timeline.SetHook(0, true, () => ends++);
            timeline.Play().Forget();
            timeline.Step(1.1f);
            Assert.AreEqual(1, starts);
            Assert.AreEqual(0, ends);
            timeline.Step(.9f);
            Assert.AreEqual(1, ends);
            timeline.ClearHooks();
            timeline.Play().Forget();
            timeline.Step(2);
            Assert.AreEqual(1, starts);
            Assert.AreEqual(1, ends);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Hook_replacing_playback_does_not_complete_the_new_playback_from_the_old_step()
        {
            var timeline = Timeline();
            var ended = 0;
            timeline.SetHook(0, true, () => ended++);
            timeline.SetHook(0, false, () => {
                timeline.SetHook(0, false, null);
                timeline.Play(-1, 1).Forget();
            });
            timeline.Play().Forget();
            Assert.IsTrue(timeline.playing);
            Assert.AreEqual(0, ended);
            timeline.Step(.5f);
            Assert.AreEqual(0, ended);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Reverse_hooks_follow_the_reversed_item_window()
        {
            var timeline = Timeline();
            var late = timeline.items[0];
            late.time = 1;
            var early = new TransitionItem { type = TransitionItemType.Alpha, tween = true,
                duration = .5f, start = new[] { 0f }, end = new[] { 1f }, ease = Ease.Linear };
            timeline.items = new[] { early, late };
            var order = new List<string>();
            timeline.SetHook(0, false, () => order.Add("early-start"));
            timeline.SetHook(0, true, () => order.Add("early-end"));
            timeline.SetHook(1, false, () => order.Add("late-start"));
            timeline.SetHook(1, true, () => order.Add("late-end"));
            timeline.PlayReverse().Forget();
            CollectionAssert.AreEqual(new[] { "late-start" }, order);
            timeline.Step(1.6f);
            CollectionAssert.AreEqual(new[] { "late-start", "late-end", "early-start" }, order);
            timeline.Step(.4f);
            Assert.AreEqual("early-end", order[3]);
            yield return null;
        }
    }
}
