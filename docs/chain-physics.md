# Chain physics: reverse-engineering notes

Source material, read directly (no binary RE needed — both are original source):

- `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\chain\default.htm` — Philip Winston's
  **Elastic Trail** script (2000, published on Dynamic Drive): a row of `<img>` dots follows the
  mouse on a Hookean spring chain, with gravity, air drag and wall bounce. This is the original
  algorithm; the 2005 game is a direct port of it.
- `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\Chain Game\Chain.vb` and `Angel.vb` — the
  VB.NET/DirectX 9 game built on top of it: a flying angel (`Angel.vb`, mouse-steered flight sprite)
  drags a chain (`Chain.vb`) ending in a mace that can be swung, thrown as a grapple, shortened or
  lengthened, and used against enemies (`Enemy.vb`).

## The core algorithm (per link, per step)

For each link `i` (1..N), pulled by its neighbor(s):

1. **Spring force** toward each adjacent link, Hooke's law past a rest length:
   `len = |pos[i] - pos[neighbor]|`; if `len > segLength`, `F = springK * (len - segLength)` along
   the normalized direction.
2. **Air drag**: `resist = velocity * resistance` (opposes motion).
3. **Gravity**: constant downward acceleration added to every link.
4. **Integration** (semi-implicit Euler, no `dt` term — one step == one frame):
   `accel = (spring - resist) / mass + gravity`; `velocity += accel`; `position += velocity`.
5. **Jitter stop**: zero the velocity when both velocity and acceleration fall under small
   thresholds, so a resting chain doesn't vibrate.
6. Link 0 isn't simulated — every frame it's snapped to the mouse (Elastic Trail) or to the angel's
   hand socket (Chain.vb), which is what makes the rest of the chain swing like a flail when you
   move fast and settle into a hang when you stop.

The original JS pulls each link toward **both** neighbors (two independent `if`s). The VB.NET port
turned that into `If / ElseIf`, so in practice only the previous-neighbor spring ever fires (`l`
only ever runs 1..`maxLinks`, so the `If` always takes the first branch and the `ElseIf` is dead
code) — the chain is pulled from the head only, not from both ends. **Update, U.CHN-T45 (BIBLE
D7):** this reads like an unintentional regression against the JS original, but Chain.vb is itself
the game the rebuild ports, not *Elastic Trail*, so this ticket settles it as a data default
matching Chain.vb's own shipped behaviour (`ChainSimulation.Settings.PullBothNeighbors`, false by
default), per the rebuild standard's own rule (`AGENTS.md`): everything that works matches the
original exactly, and only a difference the owner actually calls for is kept. The JS original's
both-neighbor pull is kept as an explicit opt-in, not the port's default.

## Game-specific layers in Chain.vb / Angel.vb

- **Angel** flies by mouse delta (`Angel.X += mouseX * Ratio3D`, inverted on Y), has a small
  sinusoidal hover bob independent of the chain, and faces left/right based on mouse direction.
- **Chain** has up to 25 links (`maxLinks`); one link index (`Mace`, default 6, adjustable 1–12 at
  runtime) is rendered bigger (3.5x) as the business end — a spiked ball by default, swapped to a
  "grapple" texture while the grab button is held.
- **Mace-overhead boost**: when the mace's spring pulls upward *and* it's above the angel, the
  upward pull is doubled — keeps the mace from drooping weirdly when swung overhead instead of
  trailing naturally. (The source comment says "mace", but the code applies it to every link
  whose spring pulls up while it is above `link(0).Y - linkSize`; the port does the same.)
- **Shorten/lengthen**: `[`/`]` move `Mace` down/up by one (1–12). All 25 links keep simulating
  in Chain.vb but only links 0..`Mace` are drawn. **Update, U.CHN-T45 (BIBLE V3):** the Unity
  version now matches that by default (`ChainSimulation.Settings.SimulateAllLinks`, true by
  default): every link keeps simulating past `ActiveLinkCount`, and only drawing stops at the
  mace. Parking the inactive tail on the mace and paying a new link out from there (false) is kept
  as an explicit opt-in, needed only if `PullBothNeighbors` (above) is also opted into, since an
  undrawn both-pulled tail would otherwise drag on the mace.
- **In Unity**: `MaceController` owns the mace index (default 6, 1–12, `[`/`]`, `-`/`=`), the
  mace link's mass (default 1, as in Chain.vb) and the spiked-ball visual (3.5x the chain's line
  width, render-only offset). The overhead boost (default 2x) and its margin (default 0.25 world
  units) are tuned on `ChainController`.
- **Grapple**: holding the action button arms the grapple; on release, if an enemy was caught, it's
  thrown using the mace's current velocity as launch velocity.
- **Hit detection** is a simple AABB (`rectMace`) against enemies, not per-link, rebuilt once a
  frame at the mace link, so a fast swing can pass clean through an enemy between frames.
  Engine.vb's `checkMaceImpact`: an enemy that is alive, not Hurt and not grabbed, when touched,
  is grabbed if the grapple is armed, otherwise gets `velocity = mace velocity / mass`, loses HP
  and goes Hurt (slows by `resistSPD` per axis, then seeks again); a grabbed enemy takes the same
  damage. A thrown enemy knocks any enemy it touches (`checkThrowImpact`) without damage.
- **In Unity** (`Assets/Unity.Chain/Core/World/`): the `GameWorld` step replaces the AABB with a
  swept circle: each step it sweeps a mace-sized circle (`Sweep.CircleCast`) from the mace link's
  previous position to its current one against every enemy's circle, plus an overlap at the end, so
  fast swings cannot tunnel. Each `EnemyEntity` (its own `IGrappleTarget`) runs `EnemyBrain`, the
  Seek/Hurt/Grabbed/Dead rules above; the `Enemy` component only draws it. **Update, U.CHN-T45
  (BIBLE D11):** damage is a data setting (`EnemyBrain.MaceDamageFormula`): `Original` by default,
  Engine.vb's own `|dX + dY/2|` of the mass-divided impact velocity (converted back to the
  original's per-frame units, `PlayerSetup.VelocityToOriginalScale`), cancelling to zero on the
  same diagonal swings the original did; mace speed x `damagePerSpeed` (`SpeedTimesFactor`) is kept
  as an explicit opt-in. Enemy-vs-enemy flocking separation (`checkFlocking`) is ported: each `Enemy` step it is pushed
  `flockSpeed` (default 0.06 u/s: `flockSpd` 0.0005 per frame x 2 because `checkFlocking` loops every ordered pair so each overlapping pair is pushed twice a frame, x 60 fps; the port's units are the original's, as `seekSpd` 0.006 is 0.36) per axis away from every live, non-grabbed enemy its circle
  overlaps (the original's range test is commented out and `flockRange` 0.08 is never read, so any rectangle overlap pushes, as here; `EnemyBrain.FlockPush`; a perfect stack splits by enemy id). Enemy-vs-angel damage
  (`checkAngelHit`) is the same step's contact check.
- **Audio** (Sound.vb): DirectSound buffers loaded from `sfx\` — `sword_swing_1..7`,
  `sword_hit_1..3`, `defend`, `footstep_1..2`, `death_1..4` — at `sfxVolume` -1000 mB, and one
  looping `music\music.wma` (DirectX AudioVideoPlayback) that `rndMusicPos` can start at a random
  point; `bSound`/`bMusic` toggle each. **In Unity** (`Assets/Unity.Chain/Runtime/Audio/`):
  `ChainAudio` sits on the chain object and plays one-shots from clip sets (a random clip each
  time, never the same twice running): a swing when the mace link's speed *relative to the hand*
  rises past `swingSpeed` (default 6 u/s, re-armed below 3 u/s, repeated every 0.6 s while it keeps
  whirling) — relative so flying with a trailing mace is not a swing; an impact per
  `EnemyHit` event of its player, louder with mace speed, plus a death sound when the hit kills; and a catch /
  throw sound from the `Grabbed`/`Thrown` events. `MusicPlayer` loops one track on its own
  2D `AudioSource` with its own volume/mute and the optional random start. No clips ship in the
  repo yet: the original WAVs can be dropped into the clip sets as they are, but `music.wma` has to
  be converted (Unity does not import WMA).
- **HUD, bounds and camera** (Engine.vb `renderDisplay`/`checkAngelHit`, Angel.vb `checkBounds`):
  the original wrote `HP : ` as one `|` per hit point, `Score : ` (each mace hit adds
  `damage * 1000`) and `Grappled: <hp>` in plain text, paused with a `- PAUSE -` banner, took one
  HP per enemy touch at most every `hurtDelay` (0.25 s) from `maxHP` 30, and clamped the angel to a
  fixed rect under a fixed 4:3 camera; the chain's wall bounce was commented out. **In Unity**
  (`Runtime/Level/`, `Runtime/UI/`): `LevelBounds` is the play-space rect; `AngelController`
  clamps to it inset by its sprite margin (camera view only as a fallback); the world
  bounces simulated links off it (`ChainSimulation.Bounds`, default bounce 0.5, `ChainController.keepInLevel` to
  turn off); `CameraRig` fits the whole level on any aspect (`FitLevel`) or follows the angel with
  a dead zone and frame-rate independent smoothing, clamped inside the level (`Follow`);
  the `GameWorld` (pure rules in `GameState`) checks enemy contact with a circle, ignoring
  grabbed and dead enemies, scores every mace hit, pauses on the Pause/P command and on game over
  fades out and resets itself (see Game over below); `GameHud` draws the original's lines with IMGUI.
  **Update, U.CHN-T45 (BIBLE D10):** HP overshoot is a data setting (`GameState.Settings.HpOvershoot`):
  1 by default, matching the original exactly (the HUD shows `HP + 1` bars and the game ends below
  zero, 31 hits from 30 HP); 0 shows exactly HP bars and ends at 0, kept as an explicit opt-in.
- Two things in `renderChain` look like leftover hacks rather than intended behavior: `link(Mace).X/Y
  += maceOffset` is applied unconditionally every frame (a cumulative drift, not a one-time
  sprite-alignment offset), and a stray `link(1).Y -= 0.0004` is marked `'fix` in the source with no
  explanation. **Update, U.CHN-T45 (BIBLE D8):** both are now data settings
  (`ChainSimulation.Settings.MaceOffsetPerSecond` and `Link1DropPerSecond`): zero by default (the
  mace sprite offset stays a render-only transform, no position mutation, with no fudge term), but
  chain.json maps the original's own figures onto them, so the shipped game carries both drifts, as
  the original does. The stray line sits inside `renderChain`'s own draw loop ("For l = 0 To Mace"),
  so it re-runs once per drawn link -- `ActiveLinkCount` times a frame -- and `ChainSimulation.Step`
  reproduces that literally, not just the figure once a step.
- Frame-rate dependence (no `dt` scaling) is also settled as a data default, not simply dropped:
  **update, U.CHN-T45 (BIBLE D9)**, `GameWorld.Settings.PhysicsStepRate` (60 by default, from
  space.json's `framesPerSecond`) feeds `GameWorld.Step`'s own fixed-step accumulator: it banks real
  time and runs as many whole `1 / PhysicsStepRate`-second sub-steps as that has filled, carrying
  any leftover to the next call, so the chain's discrete per-frame integration keeps the original's
  feel at any host frame rate, not only at exactly 60 fps or at one that happens to divide evenly
  into it. (An earlier version of this rounded how many sub-steps to split each call's own `dt`
  into, which drifted off the original's frame length at a rate that was not an exact multiple or
  divisor of 60, e.g. 144, 90 or 50 Hz; the accumulator does not.)

## Default tuning (starting point, to be re-tuned by feel in-engine)

| Constant | VB.NET value | Meaning |
|---|---|---|
| `maxLinks` | 25 | link count |
| `segLength` | 0.002 | rest length per segment (NDC-ish units) |
| `springK` | 0.1 | stiffness — "less = looser, more = bouncier" (original comment) |
| `resistance` | 0.25 | drag coefficient |
| `gravity` | (0, -0.0025) | per-step downward accel |
| `stopVel` / `stopAcc` | 1e-7 | jitter-stop thresholds |
| `Mace` | 6 | index of the heavy end link |
| `maceSize` | 0.075 | mace sprite scale factor (3.5x the link quad) |

These numbers are in the original's screen space (-0.5..0.5 is the screen height). Since UNI-T33 they live, exactly
as above, in `Assets/StreamingAssets/Settings/chain.json`, and Core converts them to world units per second in one
place (`OriginalSpace`, from `space.json`; length scale 9 since UNI-T66: screen height 1.0 is the 9-unit arena height, so on-screen speeds match the original). See BIBLE §6.

## Port plan (see tickets)

1. Project scaffold (this commit).
2. `ChainSimulation` — pure C# port of the corrected per-link physics (no `MonoBehaviour`
   dependency), unit-tested with the Unity Test Framework (NUnit).
3. `ChainController` (MonoBehaviour) — drives `ChainSimulation` from `FixedUpdate`, renders the
   chain with a `LineRenderer`/sprites, and anchors link 0 to the player's hand socket.
4. Angel/player controller — mouse-steered flight, hover bob, facing flip, sprite animation.
5. Mace rendering + grapple/throw mechanic (arm on input, release throws using mace velocity).
6. Enemies + Physics2D hit detection against the mace (swept circle, replacing the AABB).
7. Audio, HUD/UI, level bounds and camera (done: `Runtime/Audio/`, `Runtime/Level/`, `Runtime/UI/`).

## Port verification (UNI-T12, 2026-10-23)

- **Original GameMaker project** (`GameMaker\gmlchain\ChainGame.gmx`): a stub. It has one room,
  `room0` (640x480, 30 fps, grey `#C0C0C0` background, no views), one `obj_link` instance that
  each step heads for the mouse at speed 1 under GameMaker gravity 10 (direction 270), and an
  `obj_gameloop` whose `gameloop` script only fills an unused `dot[1..9]` array (`globals`
  declares `dotx/doty/dotdx/dotdy` and never uses them). There are no other rooms, menus or modes
  and no chain, so the playable reference for the port is the VB.NET Chain Game (one endless
  arena: 14 enemies, one heart pickup, HP/score HUD, game over then reset). Its `gmlchain` folder
  holds the art the port should use (`angel\`, `chain\`, `enemy\Shadow\`, `item\`, `area\`).
- **Mechanics**: the code matches the tuning and rules recorded above (25 links, mace index 6 in
  1-12, overhead boost, grapple/throw, enemy seek/hurt/grab, 30 HP with a 0.25 s hurt delay,
  damage x 1000 score); the edit-mode tests cover them.
- **Windows build**: Unity 6000.4.3f1 batch builds. The project as committed has no scene, so
  `BuildPlayer` fails. A scene put together in code from the runtime components builds for
  StandaloneWindows64 and runs headless for 15 s without any script exceptions.
- **Gaps, each filed as a fix ticket**: no scene or build-settings entry (UNI-T16); `.meta` files
  not committed (UNI-T17); URP installed but its global settings and pipeline asset missing
  (UNI-T18); no art imported (UNI-T19); no audio clips (UNI-T20); no enemy spawner (UNI-T21);
  game over fade and auto-reset ported exactly (UNI-T28).

## Game over (matches the original, UNI-T28)

The original VB.NET `Engine.vb` `GameOver` fades the ambient light (`fadeOut`, starts at 255) down
5 per frame while it is above 6, then calls `Reset()` on its own (enemies, HP, score, light back to
255), with no click. The port does the same: `GameState.AdvanceGameOver` (engine-free Core) holds the
`Ambient` level and steps it once per fixed step (about 1 s at 50 Hz, not tied to the display rate) and reports when the fade is done;
`GameSession.StepGameOver` calls it from `FixedUpdate` and then restarts, the one place that resets HP, score and enemies (scene reload, or an in-place
reset that raises `Restarted`); `GameHud` plays the fade as a black overlay of `1 - AmbientLevel`.
