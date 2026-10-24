---
codex: 1
project: Unity.Chain
code: UNI
layer: reconstruction
status: living
updated: 2026-10-23
---

# Unity.Chain: reconstruction of the original (REA) and parity audit

Written for UNI-T25 (owner 2026-10-23). It records the reverse engineering of the original prototype
in full, compares it with the rebuild as it stands on main, audits the project against Unity
best practice and the shared MindAttic standards, and lists in build order every story and ticket
that still separates the rebuild from a complete proof of concept.

The earlier, narrower notes stay in [chain-physics.md](chain-physics.md) (the spring algorithm and its
tuning table). Where the two disagree, this file is the newer reading.

## 1. What the original is

Two source trees are the original (both read directly as source; there was no binary to decompile, though
`bin\Chain.exe` was checked for the shipped asset set):

| Path | What it is |
|---|---|
| `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\Chain Game\` | The VB.NET / DirectX 9 game (2005). The reference for the rebuild. Modules: `Root`, `Engine`, `Chain`, `Angel`, `Enemy`, `Item`, `Input`, `Sound`, `Camera`, `Background`, `Cube`, `Lights`, `Gui`, `Text`, `Zone`, `Terrain`, `Textures`, `Script`, `Character` (empty), `Ref\Time`; forms `Form1`, `frmMain` (an older `Form1`), `frmConfig`. `Backup`, `Backup1` and `Chain Game - Copy\Backup*` are earlier snapshots. |
| `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\chain\default.htm` | Philip Winston's *Elastic Trail* (2000), the chain algorithm the game ported. |
| `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\GameMaker\gmlchain\ChainGame.gmx` | A GameMaker stub: one room, one object heading for the mouse. Nothing to port (see chain-physics.md, UNI-T12). |
| `...\Chain Game - Copy\sfx\` and `bin\Chain\sfx\` | The audio: 17 WAVs and `music\music.wma`. The working copy `Chain Game\` itself has no `sfx` folder. |

Inventory of the shipped game: 27 source modules/forms, 70 image files under `gfx\` (angel 10, area 10,
brick 1, chain 9, cursor 9, enemy 8, gui 20 incl. 12 loading pictures, item 1, `angel_dev`, `enemy`,
`terrain`), 17 WAVs, one WMA, one icon (`battle.ico`). No data files: the Jet `.mdb` databases are commented
out and `loadTerrain` reads an unset `DataRow`. The only persisted data is the registry key
`HKCU\Software\MindAttic\Engine` (read) and `...\War` (written) holding Width, Height, ColorDepth and Fullscreen.

## 2. How the original runs

`Form1.Startup` shows a random `gfx\gui\load\load640_<1..12>.jpg` while it builds the `Engine`, hides the
Windows cursor, takes the mouse exclusively through DirectInput, then loops
`getInput()` / `oneFrameRender()` / `Application.DoEvents()` with no frame cap and **no time step**: every
physics number is "per frame". The window is a 640x480 borderless black form titled `*** UNDER DEVELOPMENT ***`.

`Engine.initDevice` creates the D3D device (16-bit depth buffer, alpha blending on, linear filters, lighting
on, ambient 64/64/64), then initialises in this order: camera, input, sound, fonts, textures, game clock,
cube, background, enemies, angel, item, text, zone, menu, status, chain, `rndMusicPos`.

Frame order in `oneFrameRender` (paused frames only draw `- PAUSE -`):

1. clear white, `renderCube`, `renderBackground`
2. `playMusic`, `renderFPS`, `renderMenu`, `renderStatus`, `renderLight`
3. `checkFlocking`, `checkMaceImpact`, `checkThrowImpact`, `checkAngelHit`, `checkItemCollect`
4. `renderAngel` (steer + hover + frame), `renderChain` (anchor, simulate, draw), `renderEnemy`, `renderItem`
5. `showScripter`, `updateClock`, `renderDisplay` (HP/score/readouts), `levelText`, `GameOver`, `initCamera`, `renderFPS`

World units: the camera is a 45 degree perspective at z = -1 looking at the z = 0 plane with a 4:3
aspect, so the visible half-height is tan(22.5) = 0.414 and half-width 0.552. All sizes below are in those units.

## 3. Module-by-module reverse engineering and rebuild status

Status key: **ported** (in main, tested), **partial**, **missing** (ticket filed), **dead** (in the source
but unreachable or does nothing; not ported, recorded here), **n/a** (DirectX plumbing with no Unity meaning).

### 3.1 Chain (`Chain.vb`)

| Behaviour | Original | Rebuild | Status / ticket |
|---|---|---|---|
| Links | 26 (`maxLinks` 25, index 0..25), each with X, Y, dX, dY, texture, mass 1; link 0 is the anchor | `ChainSimulation`, up to 25 links, only 0..mace simulated | ported, deviation (UNI-T45) |
| Spring | Hooke past `segLength` 0.002, `springK` 0.1, from `link(l-1)` **only** (the `If l > 0 / ElseIf` always takes the first branch); JS original pulled from both | pulls from both neighbours | deliberate deviation, settle in UNI-T45 |
| Integration | semi-implicit Euler per frame, drag `resistance` 0.25, gravity (0, -0.0025), jitter stop 1e-7 | per fixed step, scaled | ported, deviation (UNI-T45) |
| Mace | link index `Mace` (6; keys `[` / `]` 1..12), drawn at 3.5x scale, hit rectangle `rectMace`; `texMace`, `texMaceGrapple` while the button is held | `MaceController`, ball visual, grapple swap | ported (art UNI-T19) |
| Overhead boost | spring.Y doubled when pulling up and above `link(0).Y - linkSize` | `ChainController` 2x boost | ported |
| Drift hacks | `link(Mace).X/Y += maceOffset` every frame (cumulative); `link(1).Y -= 0.0004` per frame | not ported (render-only offset) | deliberate deviation (UNI-T45) |
| Wall bounce | commented out | `LevelBounds` bounce 0.5 | deliberate deviation (UNI-T45) |
| `followMouse` (F key) | starts `True`, never reset; when false `animateChain` leaves every spring force zero (gravity and drag still act, so the chain falls away from the hand) and `renderChain` skips the mace's 3.5x scale, offset and `rectMace`, which is left where it last was | `ChainSimulation.IsFollowing`, `ChainFollow` (Core: `ToggleFollowCommand` with a player id -> `FollowChangedEvent`, the 3.5x and 1x mace scales), F action on `ChainController`; off, the mace draws at link size and neither hits nor catches (BIBLE D17) | ported, UNI-T47 |
| Live tuning | Ins/Del resistance +/-0.001, Home/End gravity -/+0.00001, PgUp/PgDn spring +/-0.001, `changeBounce` unused | none | missing, UNI-T51 |
| Link skins | U key sets links 1-5 to aqua, emerald, teardrop, tablet, pearl | none | missing, UNI-T41 |
| `theMaceMode.Magnet` + `magnet.PNG` | declared, texture loaded, **never selected** | none | dead in original; README promised it, UNI-T43 |
| `bRicochet`, `grappleMax` | `bRicochet` unused; `grappleMax` 1 | `grappleMax` honoured | ported |

### 3.2 Angel (`Angel.vb`)

| Behaviour | Original | Rebuild | Status |
|---|---|---|---|
| Steering | `X += mouseX * 0.00171`, `Y -= mouseY * 0.00171` from the exclusive mouse delta | `AngelFlight` | ported |
| Facing | right when delta.x > 0, left when < 0 (kept otherwise); hand offset 0 right / 0.033 left | `AngelController` | ported |
| Hover bob | triangle wave +/-0.01 in 0.001 steps per frame, applied to the draw offset | `AngelFlight` | ported |
| Flap | 4 frames per facing, advance every 0.1 s, loops | frame logic in place, no art | partial, UNI-T19 |
| Bounds | X -0.6..0.5, Y -0.6..0.4 | `LevelBounds` | ported |
| HP / hurt | 30 HP, 1 per enemy contact at most every 0.25 s, game over below 0 | `GameState` | ported (HP+1 quirk, UNI-T45) |
| Contact rect | X..X+0.06 by Y+0.03..Y+0.15 | physics circle | deliberate deviation |
| `angelAI.Hurt` | sinks 0.01/frame; nothing ever sets it | none | dead |

### 3.3 Enemies (`Enemy.vb`, `Engine.checkFlocking/checkMaceImpact/checkThrowImpact`)

| Behaviour | Original | Rebuild | Status / ticket |
|---|---|---|---|
| Count and spawn | 14 (`maxEnemy` 13). `getRndNum(-1, 1)` returns the integers -1, 0 or 1 and 0 is rejected, so every enemy spawns at one of the four corners (+/-1, +/-1) | no spawner | missing, UNI-T21 |
| Start values | `loadEnemy`: HP 0.2, mass 1 + rand(0..1), random frame 0..6. `Reset`: HP 1.0, mass unchanged | n/a | UNI-T21 (note both) |
| Seek | `d = (angel - offset - pos) * 0.006` (proportional, so it eases in) | `EnemyBrain` | ported |
| Hurt | decays velocity by `resistSPD` 0.001 per axis, back to Seek when both are near zero | `EnemyBrain` | ported |
| Grabbed | follows the mace link (+0.03, small random jitter), spins | `Grapple` in `GameWorld` | ported |
| Thrown | velocity = mace velocity, **no drag, no end state** (it leaves the arena) | thrown rules | ported, check in UNI-T45 |
| Dead | HP <= 0: skipped entirely (vanishes) | dead state | ported |
| Mace hit | alive, not Hurt, not Grabbed: velocity = mace velocity / mass, damage = abs(dX + dY/2), score += damage * 1000; a held enemy takes the same | `GameWorld` mace step (swept circle, speed-based damage) | deliberate deviation (UNI-T45) |
| Throw impact | thrown enemy knocks any overlapping enemy: v = thrown v / mass, Hurt | `EnemyBrain` | ported |
| Flocking | overlapping live, not-grabbed pairs pushed apart by 0.0005 per axis, each pair visited twice a frame (so 0.001); range test commented out, `flockRange` unused | `EnemyBrain.FlockPush`, 0.06 u/s | ported, UNI-T23, UNI-T35 |
| Animation | 7 frames (`Shadow\enemy1..7`), 0.1 s per frame | no art | partial, UNI-T19 |
| AI states `Cautious`, `Align` | coded but never selected. `Cautious`: `d = 6 * seekSpd * -getSlope(angel, pos) / mass`, and `getSlope` returns pos - angel, so as written it charges; `Align`: `+/-seekSpd` per axis toward the angel (y with an `angelSize / 3` offset) | `EnemyBrain` Cautious/Align behind `GameWorld`'s Seek cap | fixed defect D18 (UNI-T65) |
| AI states `Retreat`, `Wander`, `Limbo`, `Death` | empty or just sink | none | dead |

### 3.4 Item (`Item.vb`)

One heart (`maxItem` 0): Health10, starts at x 0, y 0.4 (sprite `gfx\item\heart.png`, 16 x 16, magenta colour key, drawn
0.05 wide with its left edge on X and centred on Y). Each frame: falls `fallRate` 0.001 (not `floatSPD`), wraps to 0.5 below
-0.5, and sways by six sequential `If` blocks on `floatSPD` (0.002 from `loadItem`, 0.001 from `Reset`), `floatAcc` 0.00001
and `floatMaxSPD` 0.001, each block seeing what the ones above changed (so with the 0.002 start the first ~100 frames flip
direction every frame and net about -0.00001 a frame). Its rectangle is X..X+0.045 by Y-0.02..Y+0.03; the angel's is
X..X+0.06 by Y+0.03..Y+0.15; `rectIntersect` tests only the angel's edges against the heart's, so an angel box straddling
the heart does not touch it. Touch gives +10 HP capped at 30 and the heart is consumed: it neither moves nor draws until
`Reset`. At the fixed 60 fps step (D9) the fall is 0.06 u/s. `ExtraLife`, `Health5`, `Health20`, `Money` and the `Center`
and `Wander` motions are declared and empty. **Ported**, UNI-T22 then UNI-T44 (`HeartFloat`, `HeartPickup`), figures through the one space mapping (length scale 9, BIBLE V7).

### 3.5 Input (`Input.vb`, `Form1.OnKeyDown`)

Every binding of the original, with its state:

| Input | Effect | Status / ticket |
|---|---|---|
| Mouse delta | steer | ported (Steer action, UNI-T34) |
| Left button | arm grapple while held; release throws | ported (Grapple action; gamepad right trigger or A) |
| Right, wheel button | right: nothing; wheel: set an unused blank cursor | dead |
| `[` `]` | mace index -1/+1 (1..12) | ported (ChainShorter / ChainLonger actions; gamepad shoulders) |
| Minus / Equals | `gameSpeed` -/+0.01 (min 0); only displayed, never applied | missing, UNI-T52 (no longer bound to chain length, UNI-T34) |
| Ins/Del, Home/End, PgUp/PgDn | tune resistance, gravity, spring | missing, UNI-T51 |
| F | `followMouse` toggle | ported (UNI-T47; ToggleFollow action, gamepad Y, UNI-T34) |
| U | gem link skins | missing, UNI-T41 |
| R | `Reset` | ported (Reset action sends `ResetGame`, gamepad Back, UNI-T34), restoring everything `Reset` does through the one `GameWorld.Reset` (UNI-T48) |
| 0 | `shakeScreen(0.02)` | missing, UNI-T49 |
| L | lighting toggle | ported (`SceneLight`, the Debug map's ToggleLighting); off is unlit rather than frozen (BIBLE D16) |
| B / N | beach / utopia background (a second `Case Keys.B` for the background toggle is unreachable) | missing, UNI-T40 |
| E | zone (hit box) overlay, default on | missing, UNI-T56 |
| F1, F2, F3 | wireframe, textures, culling (D3D render states) | missing, UNI-T56 |
| F4 | FPS counter | missing, UNI-T50 |
| Tab | menu panel | missing, UNI-T55 |
| Pause | pause | ported (Pause action: Pause, P, Esc, gamepad Start); Esc's quit, UNI-T54 |
| Esc | quit | missing, UNI-T54 |
| F12 | toggle fullscreen and restart the engine (config) | missing, UNI-T59 |
| J | joystick toggle; the joystick code reads axes and buttons and binds nothing | fixed: the gamepad drives the actions, its stick steers like the mouse, J switches the stick (BIBLE D14, UNI-T34) |
| Numpad + Enter / 4 6 / 8 2, X, Z | debug camera | missing, UNI-T57 |
| ` (grave) | scripter console | missing, UNI-T60, UNI-T61 |
| Arrow keys, `btnAttack`..`btnDashRight` | declared; no effect | dead |

### 3.6 Game rules, HUD and clock (`Engine.vb`, `Gui.vb`, `Root.vb`)

| Behaviour | Original | Status / ticket |
|---|---|---|
| HUD text | `HP : ` as `|` per point, `Score : `, `Grappled: <hp>`, `Res/Grav/Spring`, game speed, FPS. **In the final source `Root.Write` has its `DrawText` commented out, so no text is drawn at all**; the earlier backups drew it. The rebuild follows the intent | partial (IMGUI), UNI-T50 |
| Clock | `m:ss`, one count per second at (300, 5), reset by `Reset` | counted and reset (`GameWorld.RoundTime`, UNI-T48); the readout, UNI-T53 |
| Status box | `status_box.png` 96x48 at the top left | missing, UNI-T50 |
| Pause | stops everything, draws `- PAUSE -` | ported (P/Esc), UNI-T54 |
| Game over | HP < 0: fade the ambient light 5 per frame, then `Reset` with no click | missing, UNI-T28 |
| `Reset` | score, timer, fade, ambient, speed, flags, camera, spring/bounce/resistance/gravity, angel HP and position, all enemies (HP 1.0, Seek, corners), the heart, music start | ported, UNI-T48: one `GameWorld.Reset` for R and game over restores score, `RoundTime`, the fade, game speed (`game.json`), pause, the chain's spring/resistance/gravity, angel HP 30 and home, a fresh wave (HP 1.0, Seek, real random points, BIBLE D1) and the world's hearts (y 0.4, sway left, `floatSPD` 0.001; `turnAround` kept); its `WorldReset` recentres the camera, jumps the music (`rndMusicPos`) and turns lighting on (the time of day runs on, the stray `b = True` is not ported, BIBLE D16). The render flags wait on their own tickets; `Bounce` is only read by commented-out code (BIBLE V15) |
| `renderTime`, `renderDate`, `levelText` | never called, or only move a matrix (no text drawn) | dead |
| `Score` | += damage * 1000 | ported |
| `SetupLights` | never called | dead |
| Window and options | 640x480 borderless form; registry options; default 540x448/512x448, 16-bit | missing, UNI-T38, UNI-T59 |

### 3.7 Lighting, camera, background (`Lights.vb`, `Camera.vb`, `Background.vb`, `Cube.vb`, `Text.vb`)

| Behaviour | Original | Status / ticket |
|---|---|---|
| Ambient + material tint | `AmbientColor` (white) and an r/g/b material colour set each frame | ported: `LightingState` tint times `GameState.Ambient`, on a URP 2D global light (`SceneLight`) |
| Day cycle | Day, Dusk, Night, Morning with `dayLength` 1e10 s, so it never advances in play | ported (`LightingState`) at a 1440 s day (BIBLE D6) |
| Camera | perspective 45 degrees, z -1, 4:3, rebuilt each frame from `cam` and `target` | rebuild uses an orthographic fit | deliberate deviation; debug camera UNI-T57 |
| Background | 2.4 x 0.83 quad at x -0.56, `beach.png` default | missing, UNI-T40 |
| Brick room | `Cube.vb` cube 67x55x38 around the camera, textured `brick.jpg`; hidden behind the background | missing, UNI-T40 |
| Screen shake | alternates a random offset and a restore | missing, UNI-T49 |
| Terrain lines (`Terrains`) | loader dereferences an unset `DataRow`; never called | dead |
| Zones (`Zone.vb`) | one box per enemy and the mace rect, drawn when `bZone` (default True) | missing, UNI-T56 |

### 3.8 Sound (`Sound.vb`)

DirectSound `SecondaryBuffer`s are loaded for `sword_swing_1..7`, `sword_hit_1..3`, `defend`, `footstep_1..2`,
`death_1..4` (11 copies each) and `music\music.wma` plays through `AudioVideoPlayback` with a random start
(`rndMusicPos`, at init and on `Reset`). `playSFX` is **never called**, and `bSound` and `bMusic` both default to
`False`, so the shipped game is silent. Volumes: music 0, sfx -1000, ambient -1500 (unused). The rebuild already
plays swing, hit, death, catch and throw sounds and loops music (more than the original), with no clips shipped:
UNI-T20. `defend` and `footstep` are unused in both. Volumes and mutes are the shared profile's since UNI-T36, with these as the stock ones; the options screen is UNI-T59.

### 3.9 Script language (`Script.vb`)

`Command Type Item Parameter` split on spaces. Commands: `CHANGE` (`CAMERA|CAM` X/Y with `+n`/`-n` arithmetic,
`LIGHT` AMBIENT, RED/R, GREEN/G, BLUE/B, `TIME` DAYPERIOD, `SCRIPT` COLOR and RUNNING, `HERO`/`ENEMY` stubs), `MAKE`
and `MOVE` (accepted, do nothing), `RUN` (`NIGHT`, `HEAVEN`). There is a generic `Make` that grows parallel arrays;
no caller. Missing, UNI-T60, UNI-T61.

### 3.10 Assets

| Group | Files | Used by the original | Status |
|---|---|---|---|
| `gfx\angel` | `angel_left1-4`, `angel_right1-4` (+ `angel_l`, `angel_r`, `angel_dev` unused) | flap frames | UNI-T19 |
| `gfx\chain` | `link`, `aqua`, `emerald`, `teardrop`, `tablet`, `pearl`; `mace\mace`, `mace2`, `magnet` | link, U skins, mace/grapple; magnet loaded not used | UNI-T19, UNI-T41, UNI-T43 |
| `gfx\enemy` | `Shadow\enemy1-7` (`enemy.PNG` duplicates 1) | 7-frame loop | UNI-T19 |
| `gfx\item` | `heart` | pickup | UNI-T19, UNI-T22 |
| `gfx\area` | `beach`, `utopia` used; `01`, `02`, `512`, `landscape`, `landscape2` unused; `brick.jpg` | background, brick room | UNI-T40 |
| `gfx\gui` | `status_box`, `menu.jpg` used; `load640_1-12` (loading); `ap_bar`, `event` (`event` used by zones), `terrain`, `talkbox`, `load.PNG`, `menu.PNG` unused | HUD, menu, loading | UNI-T50, UNI-T55, UNI-T58 |
| `gfx\cursor` | 9 cursors loaded, none drawn | unused | dead |
| `sfx` | 17 WAV, `music.wma` | see 3.8 | UNI-T20 |
| `battle.ico` | window icon | UNI-T38 |

## 4. Observed quirks worth keeping in mind

- Every enemy spawns on top of the others at a corner; flocking is what spreads them (UNI-T21 before UNI-T23).
- A thrown enemy never slows. The only ways it ends are leaving the play space (no bounds for enemies) or dying.
- `getRndNum` is integer valued, so "random" positions are only -1, 0, 1.
- Damage uses `abs(dX + dY/2)` of the *enemy's new* velocity (mace velocity / mass), so heavier enemies take less.
- No step is time based: the original runs faster on a faster machine. The rebuild fixes the step; UNI-T45 matches
  the felt speed at 60 fps.
- The HUD text, clock, date and level text are all drawn through a `Write` that does nothing in the final source.

## 5. Standards audit (Unity best practice and the shared standards)

Checked against `AGENTS.md` (the owner's rebuild standard) and the reference repos Unity.SpaceGod and Unity.GridGame.

| Area | Finding | Ticket |
|---|---|---|
| Assembly definitions | Three asmdefs exist (Runtime, Editor, EditMode tests) plus PlayMode; the whole game, rules included, is one Runtime assembly with engine references. No Core. | UNI-T30 |
| Engine-free Core | Absent. `ChainSimulation`, `AngelFlight`, `EnemyBrain`, `GameState` are mostly pure but sit in Runtime and use `UnityEngine` types | UNI-T30 |
| Tests outside Unity | Done: `Tests/Core.Tests`, run by `run-core-tests.ps1` | UNI-T31 |
| Commands and events | Done: one `GameWorld` in Core, stepped only by `GameSession`; players send `GameCommand`s with a `PlayerId`, presentation plays `GameEvent`s (BIBLE §5.3) | UNI-T32 |
| Data-driven content | Done: JSON data files in `StreamingAssets/Settings` holding the original's figures, validated on load and read by Core's `GameSettings` through `ISettingsSource`, one mapping (`space.json`) to the port's units (BIBLE §6) | UNI-T33 |
| Input System | Done: one `InputActionAsset` built in code (`ChainControls`), keyboard, mouse and gamepad bindings, read per seat by `SeatInput` into that seat's commands; nothing polls `Keyboard.current` or `Mouse.current` | UNI-T34 |
| URP | Done: a URP pipeline with the 2D renderer (`Assets/Unity.Chain/Rendering`, built by `Editor/RenderPipelineBuilder`) is assigned in GraphicsSettings and every quality level beside the committed global settings and volume profile; a headless import leaves them unchanged | UNI-T18 |
| Unity.Shared profiles and settings | Done: `com.mindattic.unity.shared` referenced; volumes, mutes and reduce motion in the profile's common block, resolution, window mode and tuning overrides in its game block, through Core's `IPreferencesStore`; music and SFX play at the profile's volumes (BIBLE §6) | UNI-T36 |
| UI | `GameHud` is IMGUI (`OnGUI`), not UI Toolkit or the shared components | UNI-T37 |
| Project settings | 1600x900 resizable, no icon, no cursor policy, `applicationIdentifier` empty | UNI-T38 |
| Build, test and deploy scripts | None (`build.bat`, `test.bat`, `deploy.bat` exist in the reference repos) | UNI-T39 |
| README | Rewritten: status, layout, build and test (UNI-T29) | done |
| BIBLE | `docs/BIBLE.md` written: rules, architecture, data, fixed defects, deviations (UNI-T29) | done |
| Built in code | `SceneBuilder`/`PlayerBuilder` generate the scene (good); keep it that way | ok |
| Multiplayer seam | Not started | UNI-T26 |
| Asset and meta hygiene | `.meta` files committed (UNI-T17); art and audio absent | UNI-T19, UNI-T20 |

## 6. Build order

Each ticket requires the one above it (ticket links), and the queue priority descends the same way, so
building them top to bottom reaches full parity. Existing open tickets are placed where they belong rather
than refiled. Every row has its own story.

| # | Ticket | Story | What it builds |
|---|---|---|---|
| 1 | UNI-T29 | UNI-US-F1 | docs/BIBLE.md and a current README |
| 2 | UNI-T30 | UNI-US-F2 | engine-free `Unity.Chain.Core` assembly, Runtime reduced to adapters |
| 3 | UNI-T31 | UNI-US-F3 | `dotnet test` project for Core, pure tests moved out of EditMode |
| 4 | UNI-T32 | UNI-US-F4 | commands with a player id producing events; presentation only reads |
| 5 | UNI-T33 | UNI-US-F5 | tuning and content as validated data files and a settings registry |
| 6 | UNI-T34 | UNI-US-F6 | Input System action asset, gamepad, J toggle |
| 7 | UNI-T18 | UNI-US-F7 | URP 2D pipeline, renderer and global settings committed |
| 8 | UNI-T36 | UNI-US-F8 | Unity.Shared profiles and settings |
| 9 | UNI-T37 | UNI-US-F9 | UI Toolkit HUD root replacing IMGUI |
| 10 | UNI-T38 | UNI-US-F10 | window, icon, cursor and project metadata |
| 11 | UNI-T39 | UNI-US-F11 | build, test and deploy scripts |
| 12 | UNI-T19 | UNI-US-A1 | the original's sprites imported and wired |
| 13 | UNI-T20 | UNI-US-A2 | the original's sound and music imported (WMA converted) |
| 14 | UNI-T40 | UNI-US-A3 | beach and utopia backgrounds, B/N keys, brick room |
| 15 | UNI-T41 | UNI-US-A4 | U-key gem link skins |
| 16 | UNI-T43 | UNI-US-A5 | magnet mace skin |
| 17 | UNI-T45 | UNI-US-G1 | settle every physics and combat deviation to the original's behaviour |
| 18 | UNI-T21 | UNI-US-G2 | 14 enemies spawned at the corners, respawned on reset |
| 19 | UNI-T23 | UNI-US-G3 | enemy flocking separation |
| 20 | UNI-T35 | (fix of UNI-T23) | flocking uses the original's value |
| 21 | UNI-T22 | UNI-US-G4 | the heart pickup |
| 22 | UNI-T44 | (fix of UNI-T22) | heart matches Item.vb exactly |
| 23 | UNI-T28 | UNI-US-G5 | game over fade and automatic reset |
| 24 | UNI-T46 | UNI-US-G6 | ambient light, material tint, day cycle, L toggle |
| 25 | UNI-T47 | UNI-US-G7 | F key follow-the-mouse toggle |
| 26 | UNI-T48 | UNI-US-G8 | R key and the full Reset, random music start |
| 27 | UNI-T49 | UNI-US-G9 | 0 key screen shake |
| 28 | UNI-T50 | UNI-US-D1 | HUD layout, status box, readouts, FPS counter |
| 29 | UNI-T51 | UNI-US-D2 | live resistance, gravity and spring keys |
| 30 | UNI-T52 | UNI-US-D3 | game speed keys and readout |
| 31 | UNI-T53 | UNI-US-D4 | m:ss round clock |
| 32 | UNI-T54 | UNI-US-D5 | Pause key and Escape to quit |
| 33 | UNI-T55 | UNI-US-D6 | Tab menu panel |
| 34 | UNI-T56 | UNI-US-D7 | hit box overlay and F1-F3 render toggles |
| 35 | UNI-T57 | UNI-US-D8 | numpad and X/Z debug camera, perspective data |
| 36 | UNI-T58 | UNI-US-D9 | random loading picture on start |
| 37 | UNI-T59 | UNI-US-D10 | resolution, window mode and volume settings screen |
| 38 | UNI-T60 | UNI-US-C1 | script language in Core |
| 39 | UNI-T61 | UNI-US-C2 | scripter console and its key handling |
| 40 | UNI-T26 | UNI-US-V1 | Unity.Multiplayer kit scaffolding |
| 41 | UNI-T62 | UNI-US-V2 | built player played by a scripted run, captured |
| 42 | UNI-T63 | UNI-US-V3 | final parity pass; every story done; repo finished |

Rows 20 and 22 were filed by the owner's standard sweep (UNI-T35, UNI-T44) after the first flocking and
heart versions guessed values; they are slotted straight after the tickets they correct. UNI-T24's note in
chain-physics.md (game over freezes until a click) is superseded by UNI-T28.
