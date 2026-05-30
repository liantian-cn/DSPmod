# Repository Guidelines

## Project Structure & Module Organization
This repo is a set of independent Dyson Sphere Program mod projects. Each mod lives in its own folder, for example `VeinPlacement/`, `SmartRelayDispatch/`, `FasterResearch/`, `FogThreatDampener/`, and `OverpoweredMechaFighters/`.

Typical module contents:
- `*.cs` for the mod source
- `Properties/AssemblyInfo.cs` for assembly metadata
- `*.slnx` or `*.sln` for local builds
- `*.Tests.ps1` when the module has a PowerShell test harness
- `README.md` for module-specific behavior notes

Shared game references are kept under `DSPmod/` and `Assembly-CSharp/`, and the BepInEx runtime references live under `DSPmod/BepInEx/core/`.

## Build, Test, and Development Commands
Build a module with the solution that sits in that module directory:

```powershell
dotnet build VeinPlacement\VeinPlacement.slnx
dotnet build Harmony\Harmony.sln
```

Run module checks with the matching PowerShell script when present:

```powershell
.\SmartRelayDispatch\SmartRelayDispatch.Tests.ps1
.\FogThreatDampener\FogThreatDampener.Tests.ps1
```

Use the module README before changing behavior; many folders document the exact patch target and runtime assumptions.

## Coding Style & Naming Conventions
Follow the surrounding C# style in each file rather than introducing new formatting rules. Keep edits small and local. Use PascalCase for public types and members, camelCase for locals and private fields, and keep filenames aligned with the primary class or feature they contain.

PowerShell scripts should stay in the module root and use the same naming pattern as the module, such as `ModuleName.Tests.ps1`.

## Testing Guidelines
Prefer the narrowest verification that covers the change: run the module’s `.Tests.ps1` script, then build the affected solution. For changes that touch shared game references or multiple mods, verify every impacted module separately.

## Commit & Pull Request Guidelines
Commit history uses short, prefixed messages such as `feat:`, `fix:`, and `chore:`. Follow that style and keep the subject specific.

Pull requests should state which mod folders changed, what gameplay behavior moved, and how you verified it. Mention any dependency on a specific DSP game version or BepInEx reference set.

## 第三方库

- UXAssist是UI基座，第三方，不要修改。
- Harmony是mod基座。
- Assembly-CSharp是游戏源代码的dump