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
turned that into `If / ElseIf`, so in practice only the previous-neighbor spring ever fires — the
chain is pulled from the head only, not from both ends. That reads as an unintentional regression
introduced in the 2005 port, not a deliberate design change, so the Unity version restores the
JS original: both-neighbor pull.

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
  in Chain.vb but only links 0..`Mace` are drawn. Because the port pulls each link by both
  neighbors, an undrawn tail would drag on the mace, so the Unity version instead simulates only
  links 0..`Mace` (`ChainSimulation.ActiveLinkCount`) and parks the rest on the mace, so
  lengthening pays a new link out from the mace.
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
- **In Unity** (`Assets/Unity.Chain/Runtime/Enemies/`): `MaceHitDetector` replaces the AABB with
  Physics2D: each step it sweeps a mace-sized circle (`Physics2D.CircleCast`) from the mace link's
  previous position to its current one, plus an `OverlapCircle` at the end, so fast swings cannot
  tunnel. `Enemy` (kinematic `Rigidbody2D` + any `Collider2D`, its own `IGrappleTarget`) runs
  `EnemyBrain`, the Seek/Hurt/Grabbed/Dead rules above. One deliberate change: damage is mace
  speed x `damagePerSpeed`, not Engine.vb's `|dX + dY/2|`, which cancels to zero on some
  diagonal swings. Enemy-vs-enemy flocking separation (`checkFlocking`) is ported: each `Enemy` step it is pushed
  `flockSpeed` (default 1.5 u/s) per axis away from every live, non-grabbed enemy its collider
  overlaps (`EnemyBrain.FlockPush`; a perfect stack splits by instance id). Not ported here: enemy-vs-angel damage
  (`checkAngelHit`, part of the HUD/health work).
- **Audio** (Sound.vb): DirectSound buffers loaded from `sfx\` — `sword_swing_1..7`,
  `sword_hit_1..3`, `defend`, `footstep_1..2`, `death_1..4` — at `sfxVolume` -1000 mB, and one
  looping `music\music.wma` (DirectX AudioVideoPlayback) that `rndMusicPos` can start at a random
  point; `bSound`/`bMusic` toggle each. **In Unity** (`Assets/Unity.Chain/Runtime/Audio/`):
  `ChainAudio` sits on the chain object and plays one-shots from clip sets (a random clip each
  time, never the same twice running): a swing when the mace link's speed *relative to the hand*
  rises past `swingSpeed` (default 6 u/s, re-armed below 3 u/s, repeated every 0.6 s while it keeps
  whirling) — relative so flying with a trailing mace is not a swing; an impact per
  `MaceHitDetector.Hit`, louder with mace speed, plus a death sound when the hit kills; and a catch /
  throw sound from `GrappleController.Caught`/`Thrown`. `MusicPlayer` loops one track on its own
  2D `AudioSource` with its own volume/mute and the optional random start. No clips ship in the
  repo yet: the original WAVs can be dropped into the clip sets as they are, but `music.wma` has to
  be converted (Unity does not import WMA).
- **HUD, bounds and camera** (Engine.vb `renderDisplay`/`checkAngelHit`, Angel.vb `checkBounds`):
  the original wrote `HP : ` as one `|` per hit point, `Score : ` (each mace hit adds
  `damage * 1000`) and `Grappled: <hp>` in plain text, paused with a `- PAUSE -` banner, took one
  HP per enemy touch at most every `hurtDelay` (0.25 s) from `maxHP` 30, and clamped the angel to a
  fixed rect under a fixed 4:3 camera; the chain's wall bounce was commented out. **In Unity**
  (`Runtime/Level/`, `Runtime/UI/`): `LevelBounds` is the play-space rect; `AngelController`
  clamps to it inset by its sprite margin (camera view only as a fallback); `ChainController`
  bounces simulated links off it (`ChainSimulation.Bounds`, default bounce 0.5, `keepInLevel` to
  turn off); `CameraRig` fits the whole level on any aspect (`FitLevel`) or follows the angel with
  a dead zone and frame-rate independent smoothing, clamped inside the level (`Follow`);
  `GameSession` (pure rules in `GameState`) checks enemy contact with a Physics2D circle, ignoring
  grabbed and dead enemies, scores `MaceHitDetector.Hit`, pauses on P/Escape and on game over
  fades out and restarts by itself (see Game over below); `GameHud` draws the original's lines with IMGUI. One deliberate
  change: the original drew `HP + 1` bars and ended the game below zero (31 hits from 30 HP); the
  port shows exactly HP bars and ends at 0.
- Two things in `renderChain` look like leftover hacks rather than intended behavior and are
  **not** being ported as-is: `link(Mace).X/Y += maceOffset` is applied unconditionally every
  frame (a cumulative drift bug, not a one-time sprite-alignment offset), and a stray
  `link(1).Y -= 0.0004` is marked `'fix` in the source with no explanation. The Unity version
  applies the mace sprite offset as a render-only transform, not a position mutation, and has no
  equivalent fudge term — if the real game turns out to need one, it'll be re-derived from how it
  actually looks in-engine, not copied blind.
- Frame-rate dependence (no `dt` scaling) is also not being carried forward: Unity's physics core
  runs on `Time.fixedDeltaTime` so chain behavior stays consistent across refresh rates.

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

These numbers are tuned for the original's fixed small-NDC coordinate space and will be re-derived
in Unity world units / pixels-per-unit rather than copied literally.

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
