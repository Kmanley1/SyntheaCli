## 1 Overview
The `synthea-cli` repository houses a .NET command-line tool built around the Synthea project. It offers a wrapper to generate synthetic patients through a simple interface. The repository contains source code, unit and integration tests, scripts, and extensive documentation. Developers primarily work with `Synthea.Cli.sln`, which references the production code in `src/` and accompanying tests in `tests/`. Build helpers and PowerShell utilities live under `tools/`, while `docs/` collects guides and design notes. The purpose of this guideline is to outline best practices for organizing a Visual Studio solution and VS Code workspace so contributors can navigate the codebase efficiently, minimize merge conflicts, and keep the project maintainable. The official best-practices PDF could not be accessed; the recommendations here rely on the repository tree and broadly accepted .NET conventions.

## 2 Why Organization Matters
- **Faster Onboarding:** A predictable folder layout helps new contributors find build scripts, configuration files, and documentation quickly.
- **Consistent Builds:** Aligning local development with the CI pipeline ensures that builds are reproducible regardless of environment.
- **Reduced Coupling:** Separating production code, tests, and utilities makes dependencies obvious and prevents unrelated changes from leaking between components.
- **Streamlined Code Review:** Clear organization simplifies pull requests. Reviewers can easily identify where changes belong and verify that no extraneous files were modified.

## 3 Directory & File Layout
```text
repo-root/
  build/                       # CI scripts and packaging helpers
  docs/                        # Project documentation and ADRs
  src/
    Synthea.Cli/               # Application source
      Synthea.Cli.csproj
  tests/
    Synthea.Cli.UnitTests/     # Unit tests
      Synthea.Cli.UnitTests.csproj
    Synthea.Cli.IntegrationTests/  # Integration tests
      Synthea.Cli.IntegrationTests.csproj
  tools/                       # Utility scripts and tools
    windows/
      install-vscode-extensions.ps1
  .vscode/                     # Shared VS Code settings
    launch.json
    settings.json
    tasks.json
    extensions.json
  Synthea.Cli.sln
  README.md
  CONTRIBUTING.md
  CODEOWNERS
```
This structure places all source code under `src/` and tests under `tests/`. Build scripts are isolated in `build/` or `tools/`, and documentation has a dedicated `docs/` folder. The `.vscode/` directory holds shared editor configuration so all developers run the same tasks. Keeping the solution file at the root signals that it is the canonical entry point.

## 4 Solution (.sln) Best Practices
A single solution file should be located at the repository root. Its name should match the repository (e.g., `Synthea.Cli.sln`). Every project that builds the product, runs tests, or generates tooling should be included. Solution folders may group projects by domain or function, but they should not mirror the directory structure exactly. Keep solution items to essential files only—scripts run during the build, key documentation, or configuration files. Remove any stale references, as they cause warnings in Visual Studio and confuse new contributors. For large codebases, solution filter files (`.slnf`) can provide a subset of projects to load quickly, but they should never replace the primary solution.

## 5 Project (.csproj) Best Practices
Each project should reside in its own folder under `src/` or `tests/`. SDK-style project files are concise and allow properties to inherit from `Directory.Build.props`. Explicitly set `RootNamespace` and `AssemblyName` so the generated namespaces are predictable. Keep project references straightforward and avoid cyclic dependencies. Output paths should be relative to the repository root (for example, `bin/Debug/net8.0/`). Generated code, intermediate artifacts, and packages should not be committed to version control. Instead, rely on the build pipeline to produce NuGet packages or other outputs. If multiple projects share common settings or analyzers, configure them in `Directory.Build.props` so each project stays minimal.

## 6 VS Code Workspace Best Practices
The `.vscode/` directory plays an important role in aligning developers’ tools. The `extensions.json` file should list recommended extensions such as `ms-dotnettools.csharp` for C# support and `davidanson.vscode-markdownlint` for Markdown style checking. `tasks.json` can define `build` and `test` tasks that call `dotnet build` and `dotnet test`, mirroring the commands run in CI. `launch.json` should include at least one debugging profile that points to the built CLI assembly. Settings in `settings.json`—for example enabling `editor.formatOnSave`—help keep code style consistent. Because these files are checked in, all team members share the same baseline configuration without needing to manually tweak their environment. Optional workspace settings can also be captured in `synthea-cli.code-workspace`; however, with `.vscode/` present, the workspace file may be redundant unless it aggregates multiple folders.

## 7 Onboarding Essentials
Documentation is central to a good first impression. The repository should contain a thorough `README.md` describing the tool’s purpose, prerequisites (such as .NET SDK version), and instructions for running a sample generation command. `CONTRIBUTING.md` explains the workflow for submitting changes, including branching strategy, commit message style, and how to run tests. A `CODEOWNERS` file specifies the maintainers responsible for reviewing pull requests in various areas. Architecture decision records (ADRs) live under `docs/architecture` and chronicle major design choices. Diagrams or flowcharts illustrating the CLI’s interaction with the Synthea JAR aid new contributors in understanding the high-level architecture. Together, these resources drastically reduce ramp-up time.

## 8 Ignore-File (.gitignore) Best Practices
An effective `.gitignore` prevents noisy diffs and protects sensitive information. Organize patterns in logical sections with comments for readability. The file should ignore common build artifacts such as `bin/`, `obj/`, `TestResults/`, and generated logs. IDE-specific directories (`.vs/`, `.vscode/` when local settings are not shared) should also be listed. Temporary user files like `*.user`, `*.suo`, and `*.userprefs` must be excluded. Avoid referencing files that no longer exist—for example, the current `.gitignore` includes two PDF file paths that are no longer part of the repository. Duplicated patterns (e.g., multiple entries for `node_modules/` or `.vscode/`) should be consolidated to keep the file concise. Comments above each section help future maintainers understand why a pattern is present.

## 9 Anti-Patterns to Avoid
| Anti-Pattern | Why It Hurts | Better Alternative |
| --- | --- | --- |
| Committing build outputs such as `bin/` or NuGet packages | Bloats history and causes merge conflicts | Ignore these directories and produce packages via CI |
| Maintaining multiple solution files without clear purpose | Confuses developers about which file to open | Keep one solution at the repository root and consider solution filters for subsets |
| Mixing source code and scripts in the same folder | Obscures boundaries and complicates search | Keep source projects under `src/` and scripts under `tools/` or `build/` |
| Omitting shared editor configuration | Developers configure tasks differently, leading to "it works on my machine" problems | Provide `.vscode/` with tasks, launch profiles, and extensions |
| Duplicate or outdated `.gitignore` patterns | Hard to maintain and may hide important files | Review `.gitignore` periodically and remove obsolete entries |
| Hard-coded paths in scripts or tasks | Breaks on different operating systems or user setups | Use relative paths and environment variables |

## 10 Die-Hard Requirements
- Repository and solution names must match in PascalCase (e.g., `Synthea.Cli`).
- No secrets or sensitive data should ever be committed. Use environment variables or local configuration files outside the repository.
- The project must build reproducibly via `dotnet build` or the provided `build` task at the root, without additional steps.
- Unit and integration tests run with `dotnet test` must succeed both locally and in CI.
- The root solution file is the single source of truth for which projects compose the CLI and its tests.
- `.gitignore` must exclude standard build artifacts and personal IDE files while remaining concise and up to date.
- Documentation should explain how to reproduce a build and where to place additional tooling.

## 11 Gap Analysis & Recommendations
| Issue | Current Path / Pattern | Recommended Change | Rationale |
| --- | --- | --- | --- |
| Duplicate `node_modules/` patterns | Lines 221 and 272 in `.gitignore` list the same folder twice【F:.gitignore†L221-L272】 | Keep a single entry under the Node section | Simplifies maintenance and reduces file length |
| Duplicate `.vscode/` ignores | The file lists `.vscode/*` at line 159 and again at line 335 with more specific allow rules【F:.gitignore†L159-L340】 | Remove the earlier generic entry and keep the allowlist version | Prevents accidental exclusion of shared settings |
| Duplicate `.vs/` patterns | `.vs/` appears at lines 78 and 309【F:.gitignore†L78-L309】 | Retain one entry near the Visual Studio cache section | Clarity and less churn |
| Outdated PDF references | Lines 352-353 ignore PDFs not present in `docs/reference`【F:.gitignore†L350-L353】 | Delete these lines | Keep ignore file scoped to actual repository files |
| Minimal workspace file | `synthea-cli.code-workspace` only defines folders without tasks【F:synthea-cli.code-workspace†L1-L8】 | Either extend it with meaningful settings or rely solely on `.vscode/` | Avoids confusion about configuration sources |
| Numerous document drafts | `project-structure.md` shows many old deliverables under `docs/deliverables`【F:docs/deliverables/project-structure.md†L15-L35】 | Archive or remove outdated versions, keeping only current docs | Makes it easier to find definitive guidance |
| Setup script at root | `setup.sh` is located at the repository root and referenced by the tree【F:docs/deliverables/project-structure.md†L122-L131】 | Move this script into `tools/` and update documentation | Centralizes onboarding commands |
| Two patterns for test results | `.gitignore` lists `**/TestResults/` and `[Tt]est[Rr]esult*/` separately【F:.gitignore†L3-L105】 | Keep one well-scoped pattern such as `**/TestResults/` | Avoid confusion and reduce file size |

Additional notes on repository context: the solution interacts with the Java-based Synthea generator. Managing the dependencies correctly requires clear documentation about where to obtain the JAR file and how to run integration tests. When organizing the repository, it is beneficial to keep the Java artifacts separate from the .NET build output so that cross-platform developers can set up quickly. The CLI itself acts as a thin wrapper, so future maintainers may extend it with new commands or options. A well-structured solution will make such extensions straightforward.

A consistent layout also improves automation. Build pipelines rely on known paths to restore packages, compile projects, and execute tests. When those paths are stable, the CI configuration remains simple, and new pipelines—such as release or benchmarking workflows—can reuse the same scripts with minor adjustments.

Beyond team onboarding, good organization boosts long-term maintainability. When older contributors leave, new developers can rely on the folder structure and naming conventions to decipher which projects correspond to which features. Documentation that lives next to the code, such as design diagrams or architecture notes, helps preserve context that would otherwise be lost. Over time the project may accumulate new modules, and having a clear pattern for adding them—complete with tests and scripts—reduces the likelihood of ad-hoc structures.
When configuring solution folders, prefer logical groupings over mirroring disk paths. For instance, group all test projects under a solution folder named `tests`. Shared utilities or sample projects might go under `examples`. Avoid placing non-existent placeholder items in the solution because Visual Studio attempts to load them, resulting in unnecessary warnings. Periodically check the solution file into source control after adding or removing projects so that team members stay in sync.

For individual projects, adopt a consistent naming convention. Production projects live under `src/ProjectName/ProjectName.csproj`, while their corresponding tests live under `tests/ProjectName.UnitTests/ProjectName.UnitTests.csproj`. Integration tests can use a similar pattern. This convention allows glob patterns in CI scripts to discover and run all tests without manually updating the pipeline when new modules are added. If a project is experimental or unsupported, consider placing it under a separate folder such as `samples/` to signal its status.

The VS Code workspace can include additional tasks beyond build and test. For example, a task may download the Synthea JAR or other dependencies. Another task might run code formatters or static analyzers. Publishing these tasks ensures all developers run the same steps before committing. When customizing `launch.json`, use variables like `${workspaceFolder}` rather than absolute paths so that the configuration works on any machine. If your repository uses a dev container, store the `.devcontainer` folder at the root and reference it in your workspace settings.

Onboarding documentation benefits from real-world examples. Provide a copy-paste command to run the CLI with typical parameters. Mention how to clean up generated data or where to store large temporary files outside the repo. Include troubleshooting tips for common issues, such as missing Java or insufficient memory. When you add features, update the documentation so that historical references do not mislead new contributors.

`.gitignore` should evolve alongside the project. As new tools or directories appear, add patterns with clear comments. For example, if you use a coverage tool that outputs to `coverage/`, place that pattern near the test results section. Organize entries by category—user files, build outputs, logs, IDE artifacts—so future maintainers can scan quickly. Remove obsolete patterns once the referenced files disappear to avoid confusion.

The table below summarizes observed gaps between the current repository and the practices described above. Addressing these items will help the project scale and minimize friction for future contributors.

Adhering to these die-hard requirements fosters reliability across the life of the project. Naming the repository and solution consistently helps external tooling—such as NuGet packaging scripts or deployment pipelines—locate the correct build artifacts. Guarding against secrets prevents accidental leaks of credentials and ensures compliance with security policies. Reproducible builds mean that a developer or automated system can pull the repository at a tag and produce identical binaries every time, which is essential for traceability. Tests run in both CI and local environments detect regression before changes reach production. Declaring a single authoritative solution avoids the confusion that arises when multiple solution files diverge. Finally, an up-to-date `.gitignore` protects the repository from noise, letting diffs focus on real code changes.

Even with a solid baseline, teams should revisit these practices periodically. As tooling evolves, new recommended extensions might appear. The .NET SDK version might change, requiring an update to `global.json` or the CI pipeline. When new directories or build steps emerge, update the `.gitignore` accordingly. Continuous improvement keeps the repository manageable as the project grows.
A well-maintained solution file also improves discoverability for IDE users. Visual Studio and VS Code's C# tools rely on the solution to understand project relationships. By loading the solution, developers can navigate to any project or file with minimal friction. Keeping the file under version control ensures everyone uses the same set of projects and reduces the chance of stray local changes causing conflicts. When a project is removed from the repository, also remove it from the solution to prevent broken references.

If the solution grows significantly, consider grouping related projects under solution folders such as `Libraries`, `Applications`, and `Tests`. This organization helps developers collapse irrelevant sections and focus on their current area of interest. Avoid deeply nested folder hierarchies, as they slow navigation and obscure relationships. In small repositories like `synthea-cli`, one or two levels of grouping is usually sufficient.

Project files benefit from keeping dependencies explicit. Use `<PackageReference>` for NuGet packages and `<ProjectReference>` for internal dependencies. When referencing tools or analyzers, pin versions so builds remain reproducible. Setting `TreatWarningsAsErrors` encourages quality by ensuring that new warnings are addressed promptly. Some teams place code analyzers or formatting settings in a shared `.editorconfig` so that style enforcement is consistent across all projects.
Developers often use different operating systems, so tasks should rely on cross-platform commands whenever possible. Shell scripts can detect the OS and call platform-specific tools when necessary. Provide instructions for enabling a dev container if one is present; this ensures that contributors without a local .NET installation can still run the code through Docker or a similar environment. Listing recommended extensions also highlights linter or formatter tools that maintain consistent style across pull requests.

VS Code's `settings.json` can also configure code formatting rules, indentation width, and line endings. Pair this with an `.editorconfig` at the repository root to standardize the conventions across other editors and IDEs. Encouraging developers to enable format-on-save or run `dotnet format` as part of the CI pipeline helps reduce extraneous whitespace-only changes and keeps diffs focused on meaningful code updates.
New contributors should find everything they need within the repository. A short `setup.sh` or PowerShell script can bootstrap dependencies, verify the installed .NET SDK version, and download the Synthea JAR if it is not already present. The README should link to this script so the initial setup involves just a single command. Provide a section outlining the directory structure and where to find key files. For example, point users to `src/` for the main CLI code, `tests/` for unit and integration tests, and `tools/` for scripts. Include contact information or links to issue templates in case newcomers encounter problems.

Documentation should follow a clear style. Headings should increment logically, code blocks should specify a language for syntax highlighting, and diagrams should include alt text or captions. Keep an index of documents in `docs/README.md` so readers can quickly locate tutorials, design discussions, and release notes. Because the repository aims to be self-documenting, avoid burying important instructions in ephemeral wikis.
Sometimes a repository includes generated files that are expensive to reproduce. In those cases, consider using Git LFS or an external artifact store rather than committing binaries directly. Document how to fetch these files when needed. For example, if the project periodically publishes a compiled JAR for Synthea, store it in a release package and provide a PowerShell or Bash script in `tools/` to download it. This keeps the repository lightweight and ensures that only source code and scripts are versioned. Periodically audit the repository with a tool such as `git lfs ls-files` or `git-fat` to verify no large binaries have slipped through.

Another common source of churn comes from IDE-generated files. Visual Studio and Rider create folders like `.vs/` or `.idea/`. Add explicit patterns for these directories to `.gitignore` if they are not already present. When new versions of the IDE introduce additional files, update the ignore list accordingly. Encourage developers to review their `git status` output before committing to ensure only the intended files are staged.
| Neglecting to run `dotnet restore` before build | Leads to confusing compiler errors when packages are missing | Include restore steps in build scripts and documentation |
| Keeping unrelated code in the same repository | Makes history harder to navigate and bloats solution load time | Split large or unrelated modules into separate repos or packages |
