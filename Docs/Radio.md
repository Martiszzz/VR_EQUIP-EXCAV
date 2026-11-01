# Cab radio

Both training levels have a `Cab Radio` anchor under `Excav / Base / Head`, over the radio in the right-hand console. The fascia, display and XR controls are created when entering Play mode. The anchor's position and rotation can be adjusted in the scene.

Start the engine, then select **PWR**. Grab and twist **VOL** to adjust volume. Grab and twist **TUNE**, or select **< / >**, to change stations. Dials read wrist rotation only while selected. Switching stations produces a short static burst; programmes continue while switching away. Turning off the engine cuts radio power, retaining the chosen station, volume and power switch for the current scene session.

The three stations are offline demonstrations with original synthesized instrumental loops, not live FM broadcasts. Replace their `clip`, `name` and `frequency` entries on the anchor's `ExcavatorRadio` component with your own licensed recordings. Each station loops one clip. No internet connection or additional packages are needed.

Audio is spatial, attached to the cab, filtered to resemble a small radio speaker and has no Doppler pitch shift. There are two reusable AudioSources, no streaming requests and no audio synthesis in Update. Interactive colliders are triggers so the radio adds no solid contacts to vehicle physics.

Unity and automated tests were not run, following the requested workflow. The console alignment, text legibility, controller selection and sound balance need an in-headset check.
