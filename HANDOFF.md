# StarlightBreaker Handoff

## Entry Points
- [README.md](README.md)
- [StarlightBreaker.Dalamud/Plugin.cs](StarlightBreaker.Dalamud/Plugin.cs)

## Summary
- Maintenance target is `StarlightBreaker.Dalamud`
- This round of work focused on making the Dalamud plugin load correctly on the current `XIVLauncherCN` environment and wiring up automatic GitHub releases
- The plugin now builds against the active `XIVLauncherCN` dev assemblies and the latest tested output is:
  - `Output/StarlightBreaker.dll`
  - `Output/StarlightBreaker.deps.json`
  - `Output/StarlightBreaker.json`
- GitHub Actions now builds and publishes release assets when a matching `v*` tag is pushed
- A pull request to upstream was opened from fork `main`:
  - `https://github.com/Loskh/StarlightBreaker/pull/20`
- The current crash investigation found the Party Finder path to be the highest-risk area, and the fix replaced the destructive callsite hook with a standard Dalamud `Hook<T>` plus exception guards

## What Was Fixed Today
- Adapted the plugin to the current API 15 environment used by `XIVLauncherCN`
- Fixed the plugin loading failure caused by building against the wrong local Dalamud installation
- Removed direct plugin-service injection of the scanner type, because the runtime-exposed scanner API differs across local Dalamud builds
- Switched signature resolution to `IGameInteropProvider.InitializeFromAttributes(...)` with `SignatureAttribute`, which uses the documented public API and avoids private reflection over internal scanner fields
- Fixed local reference resolution by pinning the project to `$(AppData)\XIVLauncherCN\addon\Hooks\dev\`
- Corrected windowing compatibility so the built assembly now targets `WindowSystem.AddWindow(IWindow)` instead of the old `Window` signature
- Updated new-config defaults so these options start enabled:
  - `ChatLogConfig.EnableColor`
  - `FontConfig.Italics`
  - `FontConfig.EnableColor`
  - `PartyFinderConfig.Enable` was already enabled by default
- Reworked the Party Finder detailed-window hook to avoid the custom `AsmHook`/`DoNotExecuteOriginal` path
- Added try/catch fallbacks around the chat log and Party Finder detours so hook failures fall back to the original call instead of crashing the process
- Removed the obsolete `CallHook.cs` helper

## Root Cause
- The machine had multiple local Dalamud installations:
  - `XIVLauncher\addon\Hooks\dev`
  - `XIVLauncherCN\addon\Hooks\dev`
- The game was running with `XIVLauncherCN`, but the project was originally compiling against the other local `Dalamud.dll`
- Because of that mismatch, the plugin compiled successfully but failed at runtime with API-shape differences such as:
  - missing `Dalamud.Game.ISigScanner`
  - old/new `WindowSystem.AddWindow(...)` signature mismatch
- The recruitment-panel crash path likely came from the custom callsite hook around Party Finder text filtering, which used a destructive patch and was the most brittle part of the plugin

## Current Build Configuration
- Project file: [StarlightBreaker.Dalamud/StarlightBreaker.csproj](StarlightBreaker.Dalamud/StarlightBreaker.csproj)
- Important settings:
  - `DalamudLibPath` prefers `$(AppData)\XIVLauncherCN\addon\Hooks\dev\` when that local path exists
  - If `DALAMUD_HOME` is set, the SDK uses that path instead
  - Otherwise CI and other environments fall back to the SDK default `$(AppData)\XIVLauncher\addon\Hooks\dev\`
  - `TargetFramework` is `net10.0-windows`
  - Dalamud-related references use explicit `HintPath` values rooted at `$(DalamudLibPath)`
  - Output is written to `Output/`
  - Release workflow file is `.github/workflows/release.yml`

## Key Files
- [StarlightBreaker.Dalamud/Plugin.cs](StarlightBreaker.Dalamud/Plugin.cs)
- [StarlightBreaker.Dalamud/Configuration.cs](StarlightBreaker.Dalamud/Configuration.cs)
- [StarlightBreaker.Dalamud/StarlightBreaker.csproj](StarlightBreaker.Dalamud/StarlightBreaker.csproj)
- [.github/workflows/release.yml](.github/workflows/release.yml)

## Validation Performed
- `dotnet build StarlightBreaker.Dalamud\StarlightBreaker.csproj`
- Verified the produced assembly references the active CN dev environment
- Verified the built plugin no longer exposes scanner injection properties that break current runtime loading
- Verified the built plugin references `WindowSystem.AddWindow(IWindow)` in assembly metadata
- Verified the project still builds successfully after removing `CallHook.cs`

## Remaining Notes
- Existing local user config is not force-migrated; default option changes only affect new configs
- The repo still contains legacy non-Dalamud targets, but the active work path is the Dalamud plugin
- GitHub release publishing is tag-driven; push a tag that matches the project version in `StarlightBreaker.Dalamud/StarlightBreaker.csproj`, for example `v2.15.0`
- If a future loading error appears, check these first:
  - whether `XIVLauncherCN` is still the actual runtime host
  - whether local overrides or `DALAMUD_HOME` still point to the same host's `addon\Hooks\dev`
  - whether the built output in `Output/` is the file actually loaded by Dalamud
- If the recruitment panel crashes again, the next suspect is another plugin with hook verification failures, especially `Saucy`
