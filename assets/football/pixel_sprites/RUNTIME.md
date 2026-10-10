# Live-match sprite trial

The match view enables sprite players and a sprite ball by default. The original simulation,
rules and pitch projection are unchanged. UI controls can toggle sprites or choose the user's
home/away palette for direct comparison.

## Reusable assets

The match view loads three pre-normalised sheets from `runtime/`. Nothing is cropped, scaled or generated
when a match opens.

| File | Size | Content |
| --- | --- | --- |
| `runtime/player_sheet_48x72.png` | 192 × 144 | 8 facings, 48 × 72 each, feet at (24, 70) in every frame |
| `runtime/player_kit_mask_48x72.png` | 192 × 144 | Same layout: red = shirt, green = shorts, blue = socks, transparent black elsewhere |
| `runtime/ball_sheet_16x16.png` | 32 × 16 | 2 ball frames, 16 × 16 each |
| `runtime/ball_shadow_16x8.png` | 16 × 8 | Pixel-art contact shadow for aerial and ground ball rendering |
| `runtime/frames/` | Various | Individual frame PNG exports of players, masks, balls and shadow |

Frame index equals facing: column = facing % 4, row = facing / 4 (top row: down, down-left, left, up-left;
bottom row: up, up-right, right, down-right).

- Sources: `modular_preview/neutral_eight_directions_v1.png` (players) and the two ball cells in
  `sprite_preview_v1.png`.
- Export: `tools/assets/PlayerSpriteSheetExporter` trims each source cell, fits it into its frame, aligns the
  feet and derives the kit mask from neutral cloth pixels and per-facing garment boundaries. Skin, hair, fixed
  white trim, dark outlines and boots stay outside the mask.
- Runtime: `MatchSpriteAtlas` loads the sheets and exposes each frame as a region of them. Textures use
  nearest filtering.
- Recoloring: `kit_recolor.gdshader` applies the current palette while retaining the base shading. It reads the
  whole mask sheet with the player sheet's UV, so both sheets must keep the same layout.

### Re-exporting

Run the exporter after changing a source board, the frame size or the garment boundaries, then commit the
three PNG files:

```sh
dotnet build FootballManager.sln --no-restore
/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --path . res://tools/assets/PlayerSpriteSheetExporter.tscn
/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --path . --import
```

Add `-- --check` to the exporter command to verify the committed sheets against their sources without writing.
The presentation test suite performs the same check.

The `.import` files of the three sheets set `process/fix_alpha_border=false`. Keep it: with the default, Godot
copies neighbouring colours into transparent pixels, and the shader would then recolour the outline around
the kit.

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

Captures go under `.artifacts/test-reports/pitch-layers/v2/`, which is Git-ignored.
Run the same scene without `--capture` for a ready-to-start match preview.
