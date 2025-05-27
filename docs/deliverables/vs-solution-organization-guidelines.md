## 1 Overview
The `synthea-cli` repository hosts a .NET command-line wrapper around the Synthea project. It includes source code under `src/`, tests under `tests/`, build and helper scripts, and extensive documentation. This guideline summarizes best practices for organizing Visual Studio solutions and VS Code workspaces, referencing the current project structure. The supplied PDF guide could not be accessed; comparisons are therefore based on repository files and standard conventions.

## 2 Why Organization Matters
- Predictable layout shortens onboarding time for new developers.
- Consistent project structure simplifies CI configuration and tool integration.
- Clear separation of concerns (source, tests, docs) reduces accidental coupling.

## 3 Directory & File Layout
```
/ (repo root)
  ├─ src/
  │   └─ ProjectName/
  │       ├─ ProjectName.csproj
  │       └─ ...source files...
  ├─ tests/
  │   ├─ ProjectName.UnitTests/
  │   │   └─ ProjectName.UnitTests.csproj
  │   └─ ProjectName.IntegrationTests/
  │       └─ ProjectName.IntegrationTests.csproj
  ├─ build/
  │   └─ scripts and CI helpers
  ├─ docs/
  │   └─ project documentation
  ├─ .vscode/
  │   └─ launch, tasks, and settings JSON
  ├─ Project.sln
  └─ README.md
```

## 4 Solution (.sln) Best Practices
- Maintain a single authoritative solution at the repository root.
- Name the solution after the product (e.g., `Project.sln`).
- Use solution folders only to group related projects or scripts.
- Remove stale or missing file references such as `tests/placeholder.txt` shown around line 10 of `Synthea.Cli.sln`【F:Synthea.Cli.sln†L10-L16】.

## 5 Project (.csproj) Best Practices
- Each project lives under its own folder within `src/` or `tests/`.
- Declare `RootNamespace` and `AssemblyName` explicitly for clarity.
- Inherit common settings from `Directory.Build.props` to avoid repetition.
- Do not check in generated artifacts; ignore `bin/`, `obj/`, and any `nupkgs/` directories like the one listed in the repository tree【F:docs/deliverables/project-structure.md†L70-L72】.

## 6 VS Code Workspace Best Practices
- Provide a `.vscode/` folder with recommended extensions, tasks, and launch settings. Example entries appear in `extensions.json` lines 1‑9【F:.vscode/extensions.json†L1-L9】 and `tasks.json` lines 1‑19【F:.vscode/tasks.json†L1-L19】.
- Align tasks with CI commands to ensure identical local and automated builds.
- Enable formatting and file exclusions via `settings.json` for consistency.

## 7 Onboarding Essentials
- A detailed `README.md` describing installation and usage.
- A `CONTRIBUTING.md` covering workflow, testing, and style guides.
- `CODEOWNERS` to map maintainers to key paths.
- Architecture diagrams or process flowcharts in `docs/` to aid orientation.

## 8 Anti-Patterns to Avoid
| Anti-Pattern | Why It Hurts | Better Alternative |
| --- | --- | --- |
| Committing build outputs like `bin/` or `nupkgs/` | Bloats history and causes merge conflicts | Ignore these paths and publish via CI |
| Multiple `.sln` files with overlapping projects | Confuses the build pipeline | Keep one canonical solution |
| Projects at the repo root | Makes large solutions hard to navigate | Keep under `src/ProjectName/` |
| Missing VS Code workspace settings | Leads to inconsistent tools | Provide tasks, launch configs, and extension recommendations |
| Stale placeholder files referenced in the solution | Breaks IDE builds | Remove placeholder entries |
| Scattered scripts across many folders | Hard to discover and maintain | Centralize under `build/` or `tools/` |
| Ignoring test parity between CI and local environments | Causes inconsistent results | Use the same commands in tasks and pipelines |
| Committing user-specific IDE files | Clutters repository | Add `.vscode/` and `.vs/` to `.gitignore` |

## 9 Die-Hard Requirements
- Repository and solution names should match in PascalCase.
- Do not commit secrets or personal data.
- Builds must be reproducible via `dotnet build` from the root.
- Tests must run identically in CI and developer machines.
- The root solution is the single source of truth for included projects.

## 10 Gap Analysis & Recommendations
| Issue | Current Path | Recommended Change | Rationale |
| --- | --- | --- | --- |
| Build artifact stored in repo | `nupkgs/synthea-cli.0.1.0.nupkg` listed near lines 70‑72 of `project-structure.md` | Delete and ignore with `.gitignore` | Keeps repository lean |
| Placeholder file in solution | `tests/placeholder.txt` reference around line 10 of `Synthea.Cli.sln` | Remove the entry | Prevents build warnings |
| Duplicate setup scripts | `setup.sh` appears twice (lines 73‑74 and 114‑114 of `project-structure.md`) | Consolidate under `tools/` | Single entry point for environment prep |
| Minimal workspace file | `synthea-cli.code-workspace` at line 115 of `project-structure.md` | Expand to include `.vscode/` tasks and launch configs | Provides consistent tooling |
| Numerous draft docs | Many items under `docs/deliverables/` lines 10‑22 | Archive or prune drafts | Reduces clutter |
| No explicit ownership file | No `CODEOWNERS` noted in tree | Create one mapping maintainers | Clarifies review paths |
| Mixed script locations | `tools/windows/` plus root-level scripts | Group under `build/` or `tools/` | Simplifies discovery |
| Test results committed | `TestResults/` folder under `tests/` lines 85‑88 | Remove and ignore | Prevents noise in version control |

## 11 References
- Repository tree excerpt from `docs/deliverables/project-structure.md` lines 1‑116 shows the overall folder layout.
