# Football sprite preview — v1

Created with the built-in ImageGen tool using the existing pixel pitch as a visual reference.
The exact prompt is saved in `generation-prompt-v1.txt`.

`sprite_preview_v1.png` is a transparent art-direction approval sheet, not a completed animation atlas.
Its character appearance was approved. The two ball cells are now used by the match renderer;
player sprites use the separate neutral eight-direction sheet with runtime kit recoloring.

Reading left to right:

- Top row: blue outfield player front, red outfield player front, yellow goalkeeper front, orange goalkeeper front.
- Bottom row: blue outfield player back, red outfield player back, ball pose A, ball pose B.

See `RUNTIME.md` for the sprite integration. The game normalizes foot pivots and in-game scale;
the current stepping effect uses the approved idle artwork, with dedicated running frames still pending.
