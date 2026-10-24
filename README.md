# Unity.Chain

A mouse-steered angel swings a spiked mace on a spring chain, knocking down, grabbing and throwing a flock of
enemies. A Unity 6 rebuild of the 2005 VB.NET / DirectX 9 *Chain Game*, which was itself built on Philip Winston's
2000 *Elastic Trail* script, a mouse-follow chain of spring-linked dots.

[![Unity](https://img.shields.io/badge/Unity-6000.4.3f1-222222)](ProjectSettings/ProjectVersion.txt)
[![C#](https://img.shields.io/badge/language-C%23-512BD4)](Assets/Unity.Chain)
[![Status](https://img.shields.io/badge/status-in%20development-orange)](docs/BIBLE.md)

There is no public build yet. Build it from source with the steps below.

## The game

Move the mouse and the angel flies; behind it hangs a chain ending in a mace, simulated link by link as springs
with drag and gravity, so it lags, swings and whips. Circle the mouse and the mace winds up like a flail. A hit
knocks an enemy back and scores; hold the left button and the mace grabs instead, and letting go throws the enemy
into the others. Fourteen enemies home in on you, each touch costs one of your 30 HP, a heart drifts down for +10,
and at zero the light fades and the round starts again by itself.

## Status

In development, rebuilding the original to full parity. On main and tested:

- the spring chain (`ChainSimulation`, `ChainController`), the mace with `[` / `]` chain length and the overhead
  boost (`MaceController`)
- the angel's flight: mouse steering, hover bob, facing, flap frames, bounds (`AngelFlight`, `AngelController`)
- one `GameWorld` in Core that only `GameSession` steps: players send commands carrying their player id, and the
  enemies, audio and HUD play the events it produces (`Core/World/`, docs/BIBLE.md §5.3)
- grapple and throw (`Core/Grapple/`), enemies with seek, knockback, grab, throw and flocking, hit by a swept circle so
  fast swings never tunnel (`Enemies/`), the enemy spawner and the heart pickup (`Items/`)
- HP, score, pause, and the original's game-over fade and automatic reset (`GameState`, `GameSession`), with an
  interim IMGUI HUD (`GameHud`)
- the original's lighting: the ambient fade times the r/g/b tint the Day, Dusk, Night, Morning cycle walks (now on a
  24-minute day; the original's never moved), on a URP 2D global light, L toggling it (`LightingState`, `SceneLight`;
  it shows once UNI-T18 commits the 2D pipeline, until then the HUD shows the game-over fade)
- level bounds and a camera that fits or follows (`Level/`), sound and music players (`Audio/`, no clips yet)
- the scene and the Windows player generated in code (`Editor/SceneBuilder`, `Editor/PlayerBuilder`)

Every tuning figure is data: the original's own figures in `Assets/StreamingAssets/Settings/*.json`, validated
on load and mapped to world units in one place (`Core/Settings`, see [BIBLE §6](docs/BIBLE.md)).

Still to come, in build order:
the Input System action asset, URP assets, Unity.Shared settings, a UI Toolkit HUD, the original's art and sound,
and the remaining keys and screens. The full list, one ticket per step, is
[docs/RECONSTRUCTION.md §6](docs/RECONSTRUCTION.md); open tickets are in the MindAttic ticket database
(`tickets ticket list --repo Unity.Chain`).

## Build and test

Prerequisites: Unity 6000.4.3f1. Close the Editor before any batchmode run.

In the Editor, open the project and use the **MindAttic > Chain** menu:

- **Build Main Scene** regenerates `Assets/Scenes/Main.unity` and the build settings from code.
- **Build Windows Player** writes `Builds/Windows/Unity.Chain.exe`.

Play `Assets/Scenes/Main.unity`, or run the built player. Run the tests from **Window > General > Test Runner**
(EditMode and PlayMode).

From the command line (PowerShell, from the repo root):

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe"

# regenerate the scene, then build the Windows player
& $unity -batchmode -quit -projectPath . -executeMethod MindAttic.Chain.EditorTools.SceneBuilder.BuildMainScene -logFile -
& $unity -batchmode -quit -projectPath . -executeMethod MindAttic.Chain.EditorTools.PlayerBuilder.BuildWindows -logFile -

# edit-mode and play-mode tests
& $unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults-EditMode.xml -logFile -
& $unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults-PlayMode.xml -logFile -
```

`build.bat`, `test.bat` and `deploy.bat` like the reference repos' are planned (UNI-T39). The engine-free rules are
tested by `Tests/Core.Tests` (`dotnet test Tests/Core.Tests`, or `./run-core-tests.ps1`).

## Controls

| Input | Effect |
|---|---|
| Mouse | fly |
| Left button (hold, release) | arm the grapple, throw what it holds |
| `[` / `]` | shorten / lengthen the chain |
| P or Escape | pause |
| L | lighting on / off |

The original's other keys (live spring, drag and gravity tuning, game speed, gem skins, backgrounds,
shake, reset, debug views, the script console) are listed with their tickets in
[docs/RECONSTRUCTION.md §3.5](docs/RECONSTRUCTION.md).

## Project layout

```
Assets/
  Scenes/Main.unity                generated by SceneBuilder; do not edit by hand
  StreamingAssets/Settings/*.json  every tuning figure, the original's own (BIBLE §6)
  Unity.Chain/
    Core/                          Unity.Chain.Core asmdef (no engine): the GameWorld and every rule
      World/  Chain/  Player/  Enemies/  Grapple/  Game/  Level/  Audio/  Maths/  Settings/
    Runtime/                       Unity.Chain.Runtime asmdef: the authority, input and presentation
      Chain/  Player/  Enemies/  Grapple/  Items/  Level/  UI/  Audio/
    Editor/                        Unity.Chain.Editor asmdef: SceneBuilder, PlayerBuilder
    Tests/EditMode/                NUnit edit-mode tests (Unity Test Framework)
    Tests/PlayMode/                the main-scene smoke test
    Materials/
docs/
  BIBLE.md                         design rules, architecture, data, fixed defects and deviations
  RECONSTRUCTION.md                the original reverse-engineered in full, parity audit, build order
  chain-physics.md                 the spring algorithm and its tuning
  USER_STORIES.md                  generated from the ticket database
```

The target architecture (an engine-free Core assembly under Runtime, with its own `dotnet test` project) is in
[docs/BIBLE.md §5](docs/BIBLE.md).

## Documentation

- [docs/BIBLE.md](docs/BIBLE.md): what the game is, the design rules, the architecture, the data files, every
  defect of the original that the rebuild fixes and every deliberate deviation, with the reason.
- [docs/RECONSTRUCTION.md](docs/RECONSTRUCTION.md): the reverse engineering of the original, module by module,
  and the build order to parity.
- [docs/chain-physics.md](docs/chain-physics.md): the chain algorithm from *Elastic Trail* to Unity.
- [AGENTS.md](AGENTS.md): the rebuild standard and where the original source lives.
