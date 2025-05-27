## 1 Overview
This document outlines recommended practices for organizing Visual Studio solutions and VS Code workspaces in .NET repositories. It distills lessons from common conventions and the current state of the `synthea-cli` project.

The supplied best-practices PDF could not be accessed; PDF-based comparison is therefore omitted. Recommendations are based on the current repository layout and established community norms.

## 2 Why Organization Matters
- Predictable layout accelerates onboarding and reduces time spent searching for files.
- Consistent structure simplifies CI/CD configuration and keeps builds reproducible.
- Isolated projects and tests minimize side effects and allow parallel development.

## 3 Directory & File Layout
```
/ (repo root)
  ├─ src/
  │   └─ ProjectName/
  │       ├─ ProjectName.csproj
  │       └─ ... source files ...
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
  │   └─ recommended settings/tasks.json
  ├─ Project.sln
  └─ README.md
```
## 4 Solution (.sln) Best Practices
- Keep a single authoritative solution file at the repository root.
- Name the solution after the product (e.g., `Project.sln`).
- Use solution folders only for grouping related projects or shared items.
- Avoid referencing files that do not exist; remove placeholder entries.

## 5 Project (.csproj) Best Practices
- Place each project under its own folder within `src/` or `tests/`.
- Set `RootNamespace` and `AssemblyName` explicitly for clarity.
- Use SDK-style projects and inherit common properties via `Directory.Build.props`.
- Keep output paths consistent and do not commit generated binaries.

## 6 VS Code Workspace Best Practices
- Provide a `.vscode/` directory with `extensions.json`, `tasks.json`, and `launch.json`.
- Capture recommended extensions such as `csharp`, `vscode-dotnet-runtime`, and Markdown linters.
- Configure build and test tasks to mirror the commands in CI pipelines.
- Encourage consistent formatting with an editorconfig in the workspace.

## 7 Onboarding Essentials
- A comprehensive `README.md` describing build instructions and project purpose.
- `CONTRIBUTING.md` for workflow guidance and a `CODE_OF_CONDUCT.md` if applicable.
- `CODEOWNERS` file listing maintainers for each area of the repository.
- Diagrams or architecture overviews in `docs/` to speed up understanding.

## 8 Anti-Patterns to Avoid
| Anti-Pattern | Why It Hurts | Better Alternative |
| --- | --- | --- |
| Committing build outputs like `bin/` or `nupkgs/` | Bloats repository and causes merge conflicts | Add to `.gitignore` and publish packages via CI |
| Multiple solution files with overlapping projects | Leads to confusion about the canonical build | Maintain a single solution and reference all projects |
| Projects nested directly under root without a `src/` folder | Harder to manage as solution scales | Use `src/ProjectName/ProjectName.csproj` |
| Missing workspace settings in VS Code | Inconsistent dev environments | Include `.vscode/` with recommended extensions and tasks |
| Referencing non-existent files in the solution | Breaks IDE builds and discourages trust | Clean out stale or placeholder items |
| Mixed script locations without convention | Developers must search across folders | Centralize build and utility scripts under `build/` or `scripts/` |
| Using shared project files across unrelated solutions | Creates hidden dependencies | Keep each repository self-contained |
| Untracked configuration or secrets checked into source | Security risk and environment coupling | Use template config files and environment variables |

## 9 Die-Hard Requirements
- Repository and solution names should match and use PascalCase.
- No secrets or personal data committed anywhere in the repo.
- Builds must be reproducible via a single `dotnet build` from the root.
- Unit and integration tests should run locally and in CI with equal options.
- The root solution file is the authoritative source for all projects.

## 10 Gap Analysis & Recommendations
| Issue | Current Path | Recommended Change | Rationale |
| --- | --- | --- | --- |
| Build artifact stored in `nupkgs/` | `docs/deliverables/project-structure.md` lines show `nupkgs/synthea-cli.0.1.0.nupkg` | Remove and add `nupkgs/` to `.gitignore` | Keeps repo lean and avoids outdated packages |
| Solution references `tests/placeholder.txt` that does not exist | `Synthea.Cli.sln` lines 10-14 | Delete placeholder entry from solution | Prevents build warnings and confusion |
| Minimal VS Code workspace with no settings | `synthea-cli.code-workspace` lines 1-9 | Add `.vscode/` folder with tasks and extensions | Aligns editor experience across team |
| Duplicate `setup.sh` in `run/` and root | `project-structure.md` lines 69-79 show `run/setup.sh` | Keep one canonical script under `scripts/` or `build/` | Removes ambiguity about setup process |
| Lack of `CONTRIBUTING.md` or `CODEOWNERS` | Not present in repository tree | Create contribution guide and ownership file | Clarifies workflow and review expectations |
| Numerous documentation drafts in `docs/deliverables/` | `project-structure.md` lines 10-22 | Move finalized docs to `docs/` and trim drafts | Streamlines onboarding materials |
| Missing `.vscode/tasks.json` for tests | No `.vscode` directory | Provide tasks for `dotnet test` | Makes running tests easier for new contributors |
| `scripts/windows/` contains single-use helpers | `project-structure.md` lines 71-80 | Consolidate scripts and document usage | Reduces clutter and encourages cross-platform scripts |

## 11 References
- Current repository tree excerpt from `docs/deliverables/project-structure.md` lines 1‑22 shows the overall folder layout.
- Additional paths such as `setup.sh` appear around lines 69‑80 and lines 106‑125 in the same file.
