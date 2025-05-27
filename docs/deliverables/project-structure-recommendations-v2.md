# Project Structure Recommendations

The table below summarizes structural issues found in the `synthea-cli` repository and suggested improvements. The best-practices PDF was not accessible, so recommendations are drawn from community standards and the current layout.

| Issue | Current Path | Recommended Change | Rationale |
| --- | --- | --- | --- |
| Build artifact committed | `nupkgs/synthea-cli.0.1.0.nupkg` lines 70‑72 in `project-structure.md` | Remove directory and ignore via `.gitignore` | Prevents repository bloat |
| Stale placeholder in solution | `tests/placeholder.txt` reference around line 10 of `Synthea.Cli.sln` | Delete entry | Avoids confusion and warnings |
| Duplicate setup scripts | `run/setup.sh` and root `setup.sh` lines 73‑74 and 114 | Consolidate under `tools/` | Single onboarding command |
| Minimal workspace file | `synthea-cli.code-workspace` line 115 | Expand with `.vscode/` tasks, launch, and extensions | Aligns editor experience |
| No `CODEOWNERS` file | Not present in repo tree | Create one mapping maintainers | Clarifies responsibility |
| Numerous draft docs | Many entries under `docs/deliverables/` lines 10‑22 | Archive or move finalized docs to `docs/` | Reduces clutter |
| Committed test results | `tests/Synthea.Cli.IntegrationTests/TestResults/` lines 85‑88 | Remove and add to `.gitignore` | Keeps history clean |
| Scripts scattered | `tools/windows/` plus root-level scripts lines 100‑106 | Move under `tools/` or `build/` with clear names | Easier discovery |
