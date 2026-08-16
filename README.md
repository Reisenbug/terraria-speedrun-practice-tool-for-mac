# Terraria Speedrun Practice Tool for Mac

Proof-of-concept: launching Terraria's own `Terraria.exe` (the same managed
assembly the macOS build ships) under Mono on Apple Silicon, and modifying
game state at runtime via plain .NET reflection — no Harmony, no runtime
method patching.

## Why not Harmony

Harmony/MonoMod need to rewrite already-JITed machine code in memory at
runtime. On Apple Silicon macOS this is blocked at the OS level (see
[pardeike/Harmony#607](https://github.com/pardeike/Harmony/issues/607),
[#679](https://github.com/pardeike/Harmony/issues/679)) — there is no known
non-invasive workaround (short of disabling SIP, which isn't worth it for
a game mod).

## What works instead

Load `Terraria.exe` as a library via `Assembly.UnsafeLoadFrom`, invoke its
entry point manually, and reflectively read/write public static fields
(`Terraria.Main.time`, boss-downed flags, etc.) from a background thread.
Verified working: forcing `Main.time` visibly freezes/skews in-game time
of day.

## Status

Early validation only. `src/Poc.cs` proves the loading + reflection path
works end-to-end (SDL3/FNA3D/FAudio/Steam API all initialize, window opens,
reflection writes succeed every tick). No practice-tool features
(god mode, boss flag editor, item/NPC spawner, timer/splits UI) are
implemented yet.

## Not affiliated with / derived from any existing mod

This is an independent reimplementation written from scratch against
Terraria's own reflected type names. It does not include, copy, or link
against any third-party mod's code or assets.

## Running

Requires Mono (`brew install mono`) and native libs (`libSDL3.0.dylib`,
`libFNA3D.0.dylib`, `libFAudio.0.dylib`) copied next to the compiled
`Poc.exe` from `Terraria.app/Contents/MacOS/osx/`.

```
mcs -platform:anycpu src/Poc.cs -out:Poc.exe
# copy Poc.exe + the dylibs above into Terraria.app/Contents/Resources/
mono Poc.exe Terraria.exe
```
