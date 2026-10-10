# Pixel pitch 2.5D — v1

Generated using the built-in ImageGen tool. The exact prompt is in `generation-prompt.txt`.

`pitch_2_5d_v1.png` is a single environment image with transparent surroundings, intended as a visual prototype for the football game. It combines the grass, field markings, goals, corner flags, walkway and fence. It is not a tile atlas or a set of independently sortable sprites.

The live view uses nearest texture filtering. The projection aligns normalized match coordinates with the
painted touchlines and center spot; the generated markings are an artistic approximation of regulation
dimensions. Separate goals and foreground fence into sprites when player occlusion is needed.

The live match view uses this asset by default, with nearest filtering and a calibrated projection in
`PixelPitchLayout`. The “Sân pixel 2.5D” control switches back to the procedural pitch for comparison.
The pitch remains visible before a match starts. Player markers and the ball retain their existing appearance.
