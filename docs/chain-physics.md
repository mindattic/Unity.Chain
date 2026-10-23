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
  diagonal swings. Not ported here: enemy-vs-enemy flocking separation and enemy-vs-angel damage
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
7. Audio, HUD/UI, level bounds and camera.
