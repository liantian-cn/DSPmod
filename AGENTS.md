# Repository Guidelines

## Project Structure & Module Organization
This repo is a set of Dyson Sphere Program mod projects. `HardFog/` is the consolidated HardFog plugin and contains several formerly standalone features such as vein placement, faster research, smart relay dispatch, fog threat dampening, and stronger mecha fighters.

Typical module contents:
- `*.cs` for the mod source
- `Properties/AssemblyInfo.cs` for assembly metadata
- `*.slnx` or `*.sln` for local builds
- `*.Tests.ps1` when the module has a PowerShell test harness
- `README.md` for module-specific behavior notes
- `Package/` for release/share metadata such as `manifest.json`, `README.md`, and `icon.png`

Shared game references are kept under `DSPmod/` and `Assembly-CSharp/`, and the BepInEx runtime references live under `DSPmod/BepInEx/core/`.

## Build, Test, and Development Commands
Build a module with the solution that sits in that module directory:

```powershell
dotnet build HardFog\HardFog.slnx
dotnet build Harmony\Harmony.sln
```

Build and package HardFog for sharing:

```powershell
.\HardFog\Pack-HardFog.ps1
```

The pack script performs a Release rebuild, stages `HardFog.dll` with `HardFog\Package\` contents, and writes `HardFog\bin\Release\package\liantian-HardFog-<version>.zip`.

Run module checks with the matching PowerShell script when present:

```powershell
.\SmartRelayDispatch\SmartRelayDispatch.Tests.ps1
.\FogThreatDampener\FogThreatDampener.Tests.ps1
```

Use the module README before changing behavior; many folders document the exact patch target and runtime assumptions.

When changing HardFog user-facing behavior or plugin version, update `HardFog\Package\manifest.json` and `HardFog\Package\README.md` in the same task. Keep `manifest.json` `version_number` aligned with the `BepInPlugin` version in `HardFog\HardFogWindow.cs`.

## Coding Style & Naming Conventions
Follow the surrounding C# style in each file rather than introducing new formatting rules. Keep edits small and local. Use PascalCase for public types and members, camelCase for locals and private fields, and keep filenames aligned with the primary class or feature they contain.

PowerShell scripts should stay in the module root and use the same naming pattern as the module, such as `ModuleName.Tests.ps1`.

## Testing Guidelines
Prefer the narrowest verification that covers the change: run the module’s `.Tests.ps1` script, then build the affected solution. For changes that touch shared game references or multiple mods, verify every impacted module separately.

For HardFog packaging changes, run `.\HardFog\HardFog.Tests.ps1` and `.\HardFog\Pack-HardFog.ps1`. Confirm the generated zip name matches `liantian-HardFog-<version>.zip` and contains `HardFog.dll`, `manifest.json`, `README.md`, and `icon.png` at the zip root.

## Commit & Pull Request Guidelines
Commit history uses short, prefixed messages such as `feat:`, `fix:`, and `chore:`. Follow that style and keep the subject specific.

Pull requests should state which mod folders changed, what gameplay behavior moved, and how you verified it. Mention any dependency on a specific DSP game version or BepInEx reference set.

For HardFog releases, also mention the package version and the generated zip path.

## 第三方库

- UXAssist是UI基座，第三方，不要修改。
- CheatEnabler是第三方mod，和UXAssist一样，仅供参考，不要修改。
- Harmony是mod基座。
- Assembly-CSharp是游戏源代码的dump
