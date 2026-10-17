# Baked controller actions and transition hooks

Controllers keep authored page identifiers in the importer. The generated enums use page order; a fixed action target uses a page ID, `~1` copies the page index, and `~2` matches the page name. These are resolved while baking. Runtime actions hold component, controller-index, transition, and typed page references.

Changing `controller.page` applies non-display gears, display gears, controller actions, and finally `controller.onChanged`, in that order. Assigning the current page does nothing. `controller.changing` remains true during this work, including notification callbacks. A change-page action skips a target controller that is already changing.

Generated components initialize authored child components before their own controllers. Initial actions have no previous page and do not emit `onChanged`. The editor resolves initial page links after assigning the baked references; it does not play transitions. Run Migrate again after installing this change so existing prefabs receive the new references.

Transition actions start on matching page changes. Re-entering a matching action while its transition is playing changes the remaining play count without restarting it. A negative count repeats indefinitely. An action with `stopOnExit` stops when its page condition ceases to match.

Each authored transition is exposed as a generated `t_Name` field. Named hooks also generate an enum and a setter, for example:

```csharp
view.SetFadeHook(View.FadeHook.Begin, OnFadeBegin);
view.SetFadeHook(View.FadeHook.End, OnFadeEnd);
await view.t_Fade.Play();
view.t_Fade.ClearHooks();
```

Generated hook setters bind directly to baked item indices. `Transition.SetHook(string, Action)` is available for code that receives a label dynamically; it searches only that transition once when binding. `label` fires at item start; a tween's `label2` fires after its final repeat. Hooks run once per timeline cycle, including reverse playback. `Stop` applies the terminal values without invoking hooks. Hook callbacks can stop or replace playback; the previous step then stops evaluating the replaced timeline.

Button state-controller accessors return a reference to the existing `Button<T>.controller` field. Pointer state and generated controller access therefore share one controller, its actions, and its subscribers.

The importer also distinguishes loader `autoSize` (boolean) from text `autoSize` (enum), preserves cross-package `pkg` references, accepts empty relation lists and group `hz`/`vt` layouts, and retains fixed list-count and alignment metadata. Same-package component names that collide after identifier normalization receive resource-ID suffixes. Font family matching is case insensitive; resource identifiers remain case sensitive.

Editor tests cover XML and page-mapping rules. PlayMode tests cover action ordering, cycle suppression, initial application, button identity, enter/leave behavior, repeat counts, reverse hooks, and replacement playback. Execute them with Unity Test Runner after migration; compiling them alone does not verify interaction or rendering.
