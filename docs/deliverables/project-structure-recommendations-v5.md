# Project Structure Recommendations

The table below identifies structural issues found in the repository along with suggested improvements. Because the best-practices PDF could not be read, recommendations are based on the repository tree and standard .NET guidelines.

| Issue | Current Path / Pattern | Recommended Change | Rationale |
| --- | --- | --- | --- |
| Minimal VS Code workspace | `synthea-cli.code-workspace` only defines folders【F:synthea-cli.code-workspace†L1-L8】 | Expand with tasks or remove in favor of `.vscode/` | Provides consistent tooling |
| Root setup script | `setup.sh` at line 129 of the tree【F:docs/deliverables/project-structure.md†L129-L129】 | Move to `tools/` and document in README | Centralizes onboarding |
| Numerous historical docs | Many files under `docs/deliverables` lines 16‑35【F:docs/deliverables/project-structure.md†L16-L35】 | Archive or prune outdated versions | Reduce clutter |
| Duplicate profiler patterns | `*.psess`, `*.vsp`, `*.vspx` repeated in `.gitignore` lines 53‑152【F:.gitignore†L53-L152】 | Keep one copy of each pattern | Simplifies maintenance |
| Missing coverage directory ignore | No `coverage/` pattern in `.gitignore` | Add `coverage/` entry near test results section | Prevents accidental commits |
| Missing package ignore | `.gitignore` lacks `*.nupkg` | Add rule to exclude generated NuGet packages | Avoids binary bloat |

## `.gitignore` Review
The current `.gitignore` follows common Visual Studio patterns but contains duplicates and entries for missing files.

**Redundant patterns:**
- Profiler logs appear more than once (`*.psess`, `*.vsp`, `*.vspx`). Consolidate them.
- Two patterns for test results are present: `**/TestResults/` (line 3) and `[Tt]est[Rr]esult*/` (lines 101‑105). Keep one.

**Mis-scoped or outdated patterns:**
None noted after cleanup.

**Missing patterns:**
- Add a rule for `coverage/` or other directories produced by coverage tools.
- Add a pattern for `*.nupkg` if packages are generated locally.

Updating `.gitignore` to address these points will prevent accidental commits and make the file easier to maintain.
