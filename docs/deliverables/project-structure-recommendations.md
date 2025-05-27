# Project Structure Recommendations

The following table lists structural issues observed in the current repository and actionable fixes.

| Issue | Current Path | Recommended Change | Rationale |
| --- | --- | --- | --- |
| Build artifacts checked in | `nupkgs/synthea-cli.0.1.0.nupkg` as seen in `docs/deliverables/project-structure.md` | Remove the directory and ignore with `.gitignore` | Prevents repository bloat |
| Stale placeholder referenced by solution | `Synthea.Cli.sln` lines 10‑14 include `tests/placeholder.txt` | Delete the solution item | Avoids confusion and warning messages |
| Sparse VS Code workspace | `synthea-cli.code-workspace` has only a folder reference | Add `.vscode/` with tasks and recommended extensions | Provides consistent development environment |
| Duplicate setup scripts | `run/setup.sh` and `setup.sh` | Consolidate under `scripts/` | Single entry point for environment prep |
| Missing contribution guide | Not present | Add `CONTRIBUTING.md` with workflow, style, and test instructions | Streamlines onboarding |
| No CODEOWNERS file | Not present | Create `CODEOWNERS` mapping maintainers to paths | Clarifies responsibility |
| Mixed script locations | `scripts/windows/` vs. root scripts | Group under `build/` or clearly named directories | Simplifies discovery |
| No `.vscode/tasks.json` for tests | Absent | Provide tasks to run `dotnet test` and format code | Encourages repeatable commands |
| Many draft documents under `docs/deliverables/` | Lines 10‑22 of `project-structure.md` | Archive or prune drafts, keep only finalized docs | Reduces clutter |
