# Button sound

Button sound URLs are resolved during import into serialized `AudioClip` references. The optional `volume` attribute sets `soundVolumeScale`; an omitted instance value preserves the component definition, while an explicit empty `sound` clears it. `settings/Common.json` may supply `buttonClickSound` as the default clip. The runtime does not resolve URLs or load audio resources.

`ButtonBase.sound` and `soundVolumeScale` can also be set directly with native values. A left click plays its one-shot before controller changes and business click events. Visual graying does not disable the click. A right click and a missing clip are silent. Combo buttons use the same playback operation without calling the ordinary button's selection/event path.

`Root.Create` owns one native 2D `AudioSource`. Every button click uses `PlayOneShot` on that source; clicks do not allocate source objects. The host configures the native source once:

```csharp
var root = Root.Create(designRoot);
root.soundSource.outputAudioMixerGroup = uiMixerGroup;
root.soundSource.volume = uiVolume;
root.soundSource.mute = muted;
```

The SDK does not overwrite these host controls when playing a click. Per-click volume is supplied to `PlayOneShot`, so native source volume and mixer routing remain effective. Destroying the root destroys its owned source. This button path does not alter existing transition-owned audio sources.

`UiSoundTests` checks native playback state, source reuse, event ordering and mute/volume preservation. `ButtonSoundImportTests` checks real native clip import, stable asset identity, and definition/instance override semantics. Playback-state checks do not measure physical speaker output.
