# Transition sounds

Sound items use the existing `Root.soundSource` when a Root is present, preserving the host's mixer, mute, volume and pitch settings. A standalone view without a Root uses the AudioSource already baked into its prefab. Playing a transition does not create a Root or another audio source.

Sound is skipped when the Transition is inactive or disabled, or when the selected AudioSource is inactive or disabled. The sound item is still consumed at its authored time; showing the view later does not replay it. Other transition tracks continue to evaluate normally.

This follows the shared stage-audio design of FairyGUI. Its automatic transitions start when their owner enters the stage and normally stop when it leaves; OpenFairy also supports explicit transition evaluation during native view initialization, so sound checks the current native active state directly.
