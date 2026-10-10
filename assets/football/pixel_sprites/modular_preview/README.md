# Modular football sprite — visual preview v1

Generated with the built-in ImageGen tool. Exact prompts and the mask correction are saved in `PROMPTS.md`.
The approved earlier football sprite is the character/style reference.

## Kit separation preview

`kit_separation_preview_v1.png` has four columns: neutral-gray base, proposed kit-region mask,
blue kit illustration, red kit illustration. The first row faces down/front; the second faces up/back.
The mask colors represent shirt (red), shorts (green) and socks (blue).

This is a concept board. The mask is not a pixel-aligned production texture; generated edges,
trim and shoe boundaries still require cleanup. The colored figures are generated illustrations,
not outputs of an implemented recoloring shader. They demonstrate the intended appearance.

## Eight directions

`neutral_eight_directions_v1.png` shows the same gray-kit character in eight idle-facing directions.
Coordinates refer to the screen:

| Row | Column 1 | Column 2 | Column 3 | Column 4 |
| --- | --- | --- | --- | --- |
| Top | Down | Down-left | Left | Up-left |
| Bottom | Up | Up-right | Right | Down-right |

## Production follow-up after visual approval

Use a neutral base plus a precisely aligned mask to recolor only kit fabric. Keep hair, skin,
trim and boots independent of team colors. Align sprite cells and foot pivots, normalize the pixel grid,
and create running frames. The idle approval boards are not final animation atlases.

The neutral eight-direction sheet is now used by the live-match sprite trial. The kit separation
board remains an illustration; the game generates an aligned mask from the neutral sprite instead.
See `../RUNTIME.md` for the integration and its animation limitations.
