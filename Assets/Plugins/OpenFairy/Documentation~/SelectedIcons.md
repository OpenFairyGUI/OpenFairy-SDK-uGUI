# Selected button icons

Button icons use direct `Sprite` references. The importer retains both the ordinary icon and the optional selected-state icon in the baked prefab, including when the button starts unchecked.

`button.icon` always reads or writes the ordinary icon. While selected, assigning another ordinary icon preserves the selected image until the button is deselected. `button.selectedIcon` changes the selected image immediately when applicable.

Assigning `selectedIcon = null` removes the selected override and returns to the ordinary image. An intentional empty selected image is different:

```csharp
button.selectedIcon = null;
button.useSelectedIcon = true;
```

The importer makes this distinction automatically: a missing `selectedIcon` attribute retains the component definition, while an explicitly empty or unresolved value hides the selected icon. An unresolved resource keeps the existing import warning. No resource URL is retained or resolved at runtime.

Common buttons do not become selected when pressed. Check/Radio selection and controller-driven selection use the existing native button path. Ordinary visual-controller icon gears remain effective when there is no selected override. An icon gear targeting a button changes its ordinary icon through the button property; it does not overwrite the selected display by finding an arbitrary descendant Loader.

Republish existing generated prefabs when upgrading from an exporter that did not bake the ordinary icon and icon-gear receiver references. Runtime controls use those explicit references and do not infer them from hierarchy names.
