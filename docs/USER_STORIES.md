---
codex: 1
project: Unity.Chain
code: UNI
layer: stories
status: living
updated: 2026-10-21
---
<!-- GENERATED from MindAttic.Tickets/data/Tickets.db by `tickets db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the tickets CLI. -->

# Unity.Chain — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| UNI-T1 | 2026-10-21 | docs | closed | Start the permanent ticket log in the ticket database | — |
| UNI-T2 | 2026-10-21 | chore | closed | Scaffold Unity.Chain project: Unity 6 project settings, trimmed 2D package manifest, and REA writeup of the original Elastic Trail / Chain.vb spring-chain algorithm | — |
| UNI-T3 | 2026-10-21 | feature | closed | Port the spring-chain physics core (ChainSimulation): both-neighbor Hooke's-law pull, drag, gravity, jitter-stop, dt-scaled integration; NUnit edit-mode tests | — |
| UNI-T4 | 2026-10-21 | feature | open | Angel/player flight controller: mouse-steered flight, hover bob, left/right facing + sprite animation, screen bounds | — |
| UNI-T5 | 2026-10-21 | feature | open | ChainController MonoBehaviour: drive ChainSimulation from FixedUpdate, anchor link 0 to the player's hand socket, render the chain | — |
| UNI-T6 | 2026-10-21 | feature | open | Mace: render the heavy end link at its configurable index, adjustable chain length (shorten/lengthen), overhead-swing boost tuned in-engine | — |
| UNI-T7 | 2026-10-21 | feature | open | Grapple/throw mechanic: arm on input hold, release throws the grappled target using the mace's current velocity | — |
| UNI-T8 | 2026-10-21 | feature | open | Enemies + Physics2D hit detection against the mace, replacing the legacy single AABB check | — |
| UNI-T9 | 2026-10-21 | feature | open | Audio: SFX for mace swing/impact/grapple and background music hookup | — |
| UNI-T10 | 2026-10-21 | feature | open | HUD/UI, level bounds and camera framing for the angel+chain play space | — |
