# Unity.Chain

A flying angel swings a spiked mace on a physics chain. Unity port of a 2005 DirectX 9/VB.NET
prototype (`Chain.vb`/`Angel.vb`), which was itself built on Philip Winston's 2000 **Elastic
Trail** DHTML script — a mouse-follow chain of spring-linked dots.

[![Unity](https://img.shields.io/badge/Unity-6000.4.3f1-222222)](https://unity.com/releases/editor/archive)
[![C#](https://img.shields.io/badge/language-C%23-512BD4)](https://learn.microsoft.com/dotnet/csharp/)
[![Status](https://img.shields.io/badge/status-scaffold-lightgrey)](#status)

## The idea

Move the mouse and an angel flies; trailing behind it is a chain ending in a mace, simulated as a
spring chain so it lags, swings, and whips around with real momentum — circle the mouse and the
mace builds up speed like a flail. The original let you grapple and throw enemies with it,
lengthen/shorten the chain, and swap the mace for a magnet.

See [`docs/chain-physics.md`](docs/chain-physics.md) for the full reverse-engineering notes: the
source algorithm, what the 2005 port got right and wrong, and the tuning table carried into Unity.

## Status

Scaffold stage: the project, package manifest, and a corrected, unit-tested port of the core
spring-chain physics (`Assets/Unity.Chain/Runtime/Chain/ChainSimulation.cs`) are in place. Player
control, rendering, the mace/grapple mechanic, enemies and everything else are tracked as open
tickets in the MindAttic ticket database (`tickets ticket list --repo Unity.Chain`).

## Project layout

- `Assets/Unity.Chain/Runtime/` — gameplay code (`Unity.Chain.Runtime` asmdef).
- `Assets/Unity.Chain/Tests/EditMode/` — NUnit edit-mode tests via the Unity Test Framework.
- `docs/chain-physics.md` — the reverse-engineering writeup this port is based on.
