# Cab radio

Both training levels use the radio already built into the `Head2.fbx` cab mesh. There is no additional case or replacement knob/button geometry. `Cab Radio` under `Excav / Base / Head` positions invisible interaction zones over the original controls. Only the printed LCD content is covered with a live screen. Coordinates follow the original two knobs, eight buttons and FM 100.1 screen.

From left to right, the eight original bottom-row buttons provide:

1. Power on/off.
2. Previous station.
3. Next station.
4. Worksite FM preset.
5. Cabin Chill preset.
6. Night Shift preset.
7. Volume down.
8. Volume up.

Grab and twist the large left knob for volume; the smaller upper knob changes stations. The merged original knob geometry stays in place; wrist rotation changes the radio setting, with volume feedback on the LCD. The radio works with the engine off, like a battery-powered accessory. Enable `requiresEngine` on its component if engine-dependent power is preferred. Settings persist only during the current scene session. Switching stations produces a short static burst; programmes continue while switching away.

The three stations are offline demonstrations with original synthesized instrumental loops, not live FM broadcasts. Replace their `clip`, `name` and `frequency` entries on the anchor's `ExcavatorRadio` component with your own licensed recordings. Each station loops one clip. No internet connection or additional packages are needed.

Interaction colliders are explicitly assigned before the interactables are enabled. Each control has a non-trigger query collider so the project's existing XR rays can hit it; all its physics contact layers are excluded using Unity 6's `Collider.excludeLayers`. A separate trigger with normal contact filtering supports direct hand interactors using trigger events. Neither collider adds collision forces, and global ray/collision settings remain unchanged. The original cab mesh is unchanged. Audio is spatial, attached to the cab, filtered to resemble a small radio speaker and has no Doppler pitch shift. There are two reusable AudioSources, no streaming requests and no audio synthesis in Update.

Unity and automated tests were not run, following the requested workflow. The console alignment, text legibility, controller selection and sound balance need an in-headset check.
