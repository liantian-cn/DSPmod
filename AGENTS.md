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

When changing HardFog user-facing behavior or plugin version, update `HardFog\Package\manifest.json` and `HardFog\Package\README.md` in the same task. Keep `manifest.json` `version_number` aligned with the `BepInPlugin` version in `HardFog\HardFogWindow.cs`. For meaningful release changes, update both root `CHANGELOG.md` and `HardFog\Package\CHANGELOG.md`; include only key changes, and skip minor bookkeeping such as renaming a setting label.

## Coding Style & Naming Conventions
Follow the surrounding C# style in each file rather than introducing new formatting rules. Keep edits small and local. Use PascalCase for public types and members, camelCase for locals and private fields, and keep filenames aligned with the primary class or feature they contain.

PowerShell scripts should stay in the module root and use the same naming pattern as the module, such as `ModuleName.Tests.ps1`.

## Testing Guidelines
Prefer the narrowest verification that covers the change: run the module’s `.Tests.ps1` script when one exists, then build the affected solution. For changes that touch shared game references or multiple mods, verify every impacted module separately.

Do not create, restore, or maintain `HardFog\HardFog.Tests.ps1`. The removed script only performed source-text assertions and low-value metadata checks, and it caused repeated encoding/token churn. Do not replace meaningful HardFog verification with PowerShell checks that only assert text snippets, version strings, README or manifest wording, description text, or similar bookkeeping.

For HardFog code changes, build `.\HardFog\HardFog.slnx`. For HardFog packaging changes, run `.\HardFog\Pack-HardFog.ps1`. Confirm the generated zip exists and contains `HardFog.dll`, `manifest.json`, `README.md`, and `icon.png` at the zip root.

## Commit & Pull Request Guidelines
Commit history uses short, prefixed messages such as `feat:`, `fix:`, and `chore:`. Follow that style and keep the subject specific.

Pull requests should state which mod folders changed, what gameplay behavior moved, and how you verified it. Mention any dependency on a specific DSP game version or BepInEx reference set.

For HardFog releases, also mention the package version and the generated zip path.

## Changelog 规范

- HardFog 的 CHANGELOG.md（根目录和 `HardFog/Package/` 下各一份，内容相同）使用双语格式：上面中文，下面英文。每个版本的每条变更都需要同时提供中英文条目。
- 标题使用 `# 更新日志 / Changelog`。

## 版本号与发布规范

- **未明确指令更新版本号时**：不要更新版本号（`HardFogWindow.cs` 中的 `BepInPlugin` version、`manifest.json` 的 `version_number`）。但 `CHANGELOG.md` 需要写入最新的变更内容（在现有版本号条目下追加，或新建版本号条目但暂不更新其它文件中的版本号）。
- **用户下指令更新版本号后**：将所有 CHANGELOG.md 中积累的最新变更纳入新版本号条目，更新 `HardFogWindow.cs` 的 `BepInPlugin` version、`manifest.json` 的 `version_number`，并执行编译打包：
  ```powershell
  dotnet build HardFog\HardFog.slnx
  .\HardFog\Pack-HardFog.ps1
  ```
  确认生成的 zip 文件存在且包含 `HardFog.dll`、`manifest.json`、`README.md`、`icon.png`。

## 第三方库

- UXAssist是UI基座，第三方，不要修改。
- CheatEnabler是第三方mod，和UXAssist一样，仅供参考，不要修改。
- Harmony是mod基座。
- Assembly-CSharp是游戏源代码的dump
