---
name: football-game-architecture
description: Enforce the architecture, C# coding conventions, and safe-refactoring rules for this Godot 4 C# football-management game. Use when adding or changing gameplay simulation, match rules, player AI, UI scenes/controllers, domain models, data factories, folder structure, namespaces, tests, naming, public APIs, or when a C# file grows large or mixes responsibilities.
---

# Football Game Architecture

Keep the project feature-oriented, testable, and fully C#/.NET. Preserve gameplay behavior during structural refactors.

## Required structure

Place code by responsibility:

```text
scripts/
  domain/          # Stable football entities and formation/squad rules
  data/            # Sample data and later repositories/loaders
  services/        # Cross-feature application services
  match/core/      # Match state, statistics, events, shared match API
  match/fast/      # Non-visual instant simulation
  match/live/      # Real-time pitch simulation and rules
  ui/              # Godot Controls, view controllers, and rendering
```

Put tests under `tests/unit`, `tests/integration`, or `tests/support` when the suite grows.

## Dependency direction

Use this direction only:

```text
UI -> services/match -> domain
data -> domain
match/live -> match/core + domain
match/fast -> match/core + domain
```

Do not make domain types depend on UI or Godot scene nodes. Prefer pure C# logic for decisions and rule resolution; keep Godot nodes as lifecycle, input, signal, and rendering adapters.

## File responsibility rules

- Give each file one primary reason to change.
- Keep `MatchPitch2D` responsible for Godot rendering and orchestration, not every football rule.
- Put team shape and off-ball movement in a team-shape component.
- Put pass/dribble/action selection in a decision component.
- Put shots and goalkeeping in a shot resolver.
- Put tackles, fouls, cards, and interceptions in a duel/rules component.
- Put corners, free kicks, throw-ins, goal kicks, and kickoffs in a restart component.
- Separate fast statistical simulation from live pitch simulation.
- Keep UI construction/presentation separate from match rules.
- Treat 400 lines as a review signal, not an absolute failure. Split earlier when a file mixes unrelated responsibilities.

Use partial classes only as a safe intermediate refactor. New football logic should move toward explicit collaborators with narrow APIs rather than adding more shared partial state.

## C# coding standard

Apply these rules to all new or edited code. Do not copy a legacy style merely because an adjacent file uses it.

### Naming and language

- Use English for identifiers and code comments. Keep player-facing text in Vietnamese until localization is introduced.
- Use `PascalCase` for types, methods, properties, events, signals, enum members, and constants.
- Use `camelCase` for parameters and local variables.
- Use `_camelCase` for private instance fields. Do not prefix names with type abbreviations.
- Prefix interfaces with `I`; suffix asynchronous methods with `Async`.
- Name files after their primary type. Name partial files as `Type.Responsibility.cs`.
- Give booleans affirmative names such as `IsPlaying`, `CanShoot`, or `HasPossession`.
- Do not add new `snake_case` C# APIs. Existing `snake_case` members are legacy compatibility APIs; rename them only in a dedicated, fully tested migration.
- Keep types in the global namespace for now. Do not introduce isolated namespaces. Perform namespace adoption later as one deliberate project-wide migration.

### Formatting and readability

- Use four spaces, Allman braces, one statement per line, and UTF-8.
- Keep lines near 120 characters when practical; wrap conditions and argument lists by logical group.
- Prefer guard clauses over deeply nested conditionals.
- Use expression-bodied members only for simple one-line behavior.
- Use `var` only when the assigned type is immediately obvious; use explicit types when they improve domain clarity.
- Keep methods focused. Review methods over roughly 40 lines and split methods that mix decisions, state mutation, and presentation.
- Comment why a non-obvious rule or tradeoff exists; do not narrate what readable code already says.
- Do not leave dead code, commented-out code, placeholder TODOs without context, or unrelated cleanup in a feature change.

### APIs, state, and dependencies

- Make members `private` by default and expose the smallest API required by callers.
- Prefer immutable inputs, `readonly` dependencies, and read-only collection views. Do not expose mutable fields in new code.
- Keep nullable reference types enabled. Model absence with nullable types and handle it explicitly; avoid the null-forgiving operator `!` unless an invariant is proven at that line.
- Validate public method inputs with clear guard clauses. Never swallow exceptions or use an empty `catch`.
- Inject or pass dependencies explicitly. Avoid service locators, hidden singletons, and mutable global state.
- Prefer `System.Collections.Generic` inside pure C# logic. Use `Godot.Collections` only at Godot serialization or engine boundaries.
- Keep UI state, match state, and domain state separate. A UI node may request an action but must not implement a football rule.
- Use named constants or configuration objects for football thresholds, durations, probabilities, pitch dimensions, and tuning values. Do not scatter unexplained magic numbers.
- Use deterministic seeded randomness in match simulation. Do not base gameplay outcomes on wall-clock time or unseeded global randomness.

### Godot-specific code

- Follow Godot's exact callback names such as `_Ready`, `_Process`, and `_Draw`; all project-owned methods still use `PascalCase`.
- Cache stable node references during initialization instead of repeatedly resolving node paths.
- Use signals/events to notify outward; do not make child UI nodes reach into unrelated parent internals.
- Avoid allocations, LINQ queries, string formatting, and new collections inside per-frame or hot AI loops unless measured and justified.
- Keep drawing methods free of football decisions and keep domain services free of drawing or node-tree access.
- Keep `[Export]` properties and serialized names stable unless the scene migration is part of the task.

### Testing standard

- Make tests deterministic and independent. Use fixed seeds and do not depend on test execution order.
- Name tests by behavior and expected result, not by private method name.
- Cover the normal case, important boundary, and rejected/invalid case for each football rule.
- Add a regression test for every bug fix and update tests whenever a public contract changes.
- Assert observable state, events, statistics, or UI outcome rather than private implementation details.
- Keep test setup in helpers when repetition obscures the behavior under test.

## Godot rules

- Remain on Godot Mono with C#; do not introduce GDScript.
- Use `.tscn` scenes for stable UI hierarchy when practical and C# for behavior.
- Preserve `[GlobalClass]`, signal names, scene script paths, and serialized property compatibility during moves.
- Update scene paths after moving scripts and check case sensitivity.
- Do not hand-edit `.godot/`; let Godot regenerate it.

## Change workflow

1. Inspect the target feature and its callers.
2. Move files without changing behavior first.
3. Build after path/namespace changes.
4. Extract one responsibility at a time behind a small API.
5. Run the .NET build and the Godot headless integration suite.
6. Add or update tests for any changed rule.
7. Confirm no `.gd` files or stale old paths remain.

Use these verification commands with the project-local environment:

```bash
HOME=/private/tmp/codex-dotnet-fresh \
DOTNET_CLI_HOME=/private/tmp/codex-dotnet-fresh \
NUGET_PACKAGES=/private/tmp/codex-nuget-fresh \
dotnet build FootballManager.sln --no-restore

HOME=/private/tmp/codex-dotnet-fresh \
DOTNET_CLI_HOME=/private/tmp/codex-dotnet-fresh \
NUGET_PACKAGES=/private/tmp/codex-nuget-fresh \
/Applications/Godot_mono.app/Contents/MacOS/Godot \
--headless --path . res://tests/DotNetTestRunner.tscn
```

## Safety constraints

- Preserve user work and avoid broad rewrites unrelated to the requested feature.
- Do not combine architecture refactoring with gameplay balance changes unless requested.
- Keep the fast-simulation and live-simulation contracts explicit.
- A refactor is incomplete until build and headless tests pass.
