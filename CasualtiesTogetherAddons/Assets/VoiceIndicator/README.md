# Voice indicator images

Editable, transparent 18 x 22 PNG assets, exported from the original microphone design.

- `microphone-idle.png`: open microphone with no voice level.
- `microphone-level-1.png` through `microphone-level-8.png`: eight rising fill levels.
- `microphone-muted.png`: crossed-out microphone.

All frames share the same canvas and alignment. Keep the alpha channel and pixel edges when editing. The canvas includes a one-pixel keyline around the original 16 x 20 icon footprint.

The build embeds these files in the DLL. The game loads and caches them as point-filtered textures, selects a frame from the existing outgoing voice state, and draws one image per repaint. It does not generate or paint icon pixels at runtime. Textures are destroyed when the feature shuts down.
