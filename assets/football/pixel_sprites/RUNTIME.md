# Live-match sprite trial

The match view enables sprite players and a sprite ball by default. The original simulation,
rules and pitch projection are unchanged. UI controls can toggle sprites or choose the user's
home/away palette for direct comparison.

## Reusable assets

- Player source: `modular_preview/neutral_eight_directions_v1.png`.
- Ball source: the two ball cells in `sprite_preview_v1.png`.
- Runtime atlas: `MatchSpriteAtlas` trims each source cell, fits it into a 48 × 72 texture,
  and aligns its feet at (24, 70). Textures use nearest filtering.
- Kit mask: generated in memory from neutral cloth pixels and calibrated garment boundaries for
  each direction. Shirt, shorts and socks are independent mask channels. Skin, hair, fixed white
  trim, dark outlines and boots are excluded. The generated concept-board mask is not used.
- Recoloring: `kit_recolor.gdshader` applies the current palette while retaining the base shading.

The palette comes from `FootballTeam.primary_color` and `secondary_color`. The trial away palette
swaps these colors; opponent shirts use the alternative palette when the colors are close.
Goalkeepers have separate yellow/orange palettes. No team-colored player PNG is generated at runtime.
New kit colors reuse the same base textures and masks.

## Motion and layering

`MatchSpriteMotion` selects eight facing directions from actual movement in pitch meters, keeps
the last facing while stationary and resets stride on teleports. Stride and ball rotation advance
from distance travelled rather than wall-clock time, so pausing also freezes the poses.

The trial shader offsets alternate legs slightly using the approved idle artwork. This is a stepping
effect, not a hand-drawn running atlas. Goalkeepers currently share the same body artwork; dedicated
gloves, dives and kick/pass animations remain future art work.

Player sprites sort by their foot positions. The ground shadows stay on the pitch; aerial balls
retain their height offset and draw above players. Labels sit below the feet.

## Verification

Build the solution, then run the focused presentation suite:

```sh
dotnet build FootballManager.sln --no-restore
/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --path . res://tests/DotNetTestRunner.tscn -- --suite=pitch-presentation
```

The full suite uses the same runner without the `--suite` argument. To render live UI captures:

```sh
/Applications/Godot_mono.app/Contents/MacOS/Godot --path . res://tests/support/SpriteVisualPreviewRunner.tscn -- --capture
```

Captures go under `.artifacts/test-reports/sprite-presentation/runtime-v1/`, which is Git-ignored.
Run the same scene without `--capture` for a ready-to-start match preview.
