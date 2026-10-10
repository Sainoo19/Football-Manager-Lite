# Pixel pitch 2.5D — layered v2

The live view now loads `pitch_ground_v2.png` (1024 × 512), containing only grass and painted markings.
`tools/assets/PitchAssetGenerator` produces this PNG deterministically using `FootballPitchDimensions`
and the same `PixelPitchLayout` projection used for player feet and the ball. Both halves are mirrored
exactly, including integer pixels. The center circle, penalty boxes, goal areas, penalty spots/arcs and
corner arcs use pitch dimensions in meters. Nearest filtering preserves the pixel style.

The goals are independent 128 × 128 transparent sprites in `goals/`:

- `goal_rear.png`: rear support frame.
- `goal_net.png`: translucent net threads with transparent holes.
- `goal_front.png`: the mouth posts and crossbar.

The right goal mirrors the same assets; no separately generated approximation is used. `MatchGoalRenderer`
places the layers at the mathematical goal line and scales them with the ground image. The actors share
a Y-sort root. Explicit depth indices distinguish the goal volume, the front of the mouth and the space
behind the net, because Y alone does not distinguish these positions at the side of a pitch. Ball height
uses the same vertical scale as the 2.44 m crossbar, so balls above it are not hidden by the net. Circle
markers also participate in this ordering when player sprites are disabled. This is rendering only;
simulation positions, boundaries and collision/rule behavior are unchanged.

Flags, fences and walkways are omitted from this clean ground. Future props should be separate nodes.

Regenerate after editing the source geometry:

```sh
dotnet build FootballManager.sln --no-restore
/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --path . res://tools/assets/PitchAssetGenerator.tscn
/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --path . --editor --import --quit
```

Validate with `res://tests/DotNetTestRunner.tscn -- --suite=pitch-presentation`. The GPU capture helper
`res://tests/support/SpriteVisualPreviewRunner.tscn -- --capture` saves real match screenshots and four
goal occlusion examples under the ignored `.artifacts/test-reports/pitch-layers/v2/` directory.

`pitch_2_5d_v1.png` remains as the original ImageGen concept, with the prompt in `generation-prompt.txt`.
It contains baked goals, flags and fencing and is no longer used by the live match view.
