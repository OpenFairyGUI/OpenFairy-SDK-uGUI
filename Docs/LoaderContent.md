# Static component content in Loader

A Loader whose authored URL points to a component now contains a nested prefab instance baked by Migrate. The instance, its generated component and its original rectangle are serialized directly. Runtime code does not resolve resource URLs or create the content hierarchy.

`Loader.content` is a read-only `OpenFairy.UGUI.Component` reference to the current component content. Cast it to the generated type to access its typed fields. Assign an inner Loader or Button's sprite/icon normally; no runtime child-name search is required.

The content keeps its authored rectangle. Loader fill and alignment scale and position the complete subtree, matching the original default `useResize=false` behavior: fonts, borders and children scale together rather than reflowing into a resized content rectangle. Parent RectTransform dimension changes update the scale through native lifecycle callbacks. There is no Update loop. Auto-size uses the original content size when the source is applied or `SetNativeSize` is called.

Assigning `loader.sprite` switches to an image and pauses any preceding movie. Assigning null clears the display; an empty Loader emits no default white image quad. Existing `MovieClip.frame` and `Play` calls switch to movie frames through the same image path. `content` is null outside component mode. The fixed nested prefab remains inactive and can be restored by calling `ShowContent()`, without destroying or re-instantiating it. Baked movie frame references are also retained; call `Play` to explicitly start them again. Use the Loader API rather than casting it to the non-virtual base Image.sprite setter.

Loader fill/align/vAlign/autoSize remain authored fields. Set them before applying the source or call `ShowContent` again to apply changed component options. `ShowContent` requires a component reference baked into that Loader; it is not a generic prefab loading API. Show or hide the whole control with its native GameObject. Disabling an Image-derived component follows native Graphic semantics and does not deactivate child GameObjects.

Loader.color changes its own image surface, not the component's individual colors. Component opacity and clipping remain native CanvasGroup/Mask/RectMask2D behavior. The Loader has no image quad in component mode, so it does not add a second color or alpha layer. `useResize` and shrink-only are not newly introduced; this feature implements the existing fill choices with the original default scaling behavior.

Migrate already collects component references in dependency order. The new branch reuses those prefabs and leaves code generation, resource indexing and non-component import paths in place.

The supplied tests cover all fill types, authored geometry preservation, native parent resize, prefab save/reopen and repeated bake, image/movie/empty/component switching, stale movie cancellation, and a GPU comparison against a plain native Image under the same mask and opacity. Execute the Editor and PlayMode suites on the actual Unity version; offline compilation does not establish rendering or lifecycle correctness.

Button import preserves an authored component-valued icon Loader. It does not assign a null sprite merely because that Loader has no image. A native importer regression saves and instantiates this actual Button prefab, toggles its activity, and verifies that explicit sprite and null assignments still replace and clear the component normally.
