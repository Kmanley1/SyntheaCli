# Codex Prompt – VS Solution & VS Code Organization Analysis

*Last Updated (UTC): 2025-05-27*

---

## 1  Background / Context

This task evaluates our Visual Studio **solution**, VS Code **workspace**, **and repository ignore rules** against commonly accepted .NET project‑layout standards.

**Inputs provided to Codex**

| Input                           | Path                                                              | Purpose                                             |
| ------------------------------- | ----------------------------------------------------------------- | --------------------------------------------------- |
| Current structure               | `docs/deliverables/project-structure.md`                          | Markdown tree of the repo’s actual folders/projects |
| Repository ignore rules         | `.gitignore`                                                      | Determines what gets excluded from source control   |
| Best-practice guide (PDF)       | `docs/research/NET Core CLI GitHub Repository Best Practices.pdf` | Reference standards                                 |
| Highlight fallback (plain-text) | `docs/research/pdf-highlights.md` *(may be absent)*               | Extracted bullet points if PDF OCR fails            |

> **PDF fallback rule**: If the PDF **or** `pdf-highlights.md` cannot be read, **do not guess or invent its contents**. Simply note in the final document: “*(Best-practices PDF could not be accessed; PDF-based comparison omitted.)*”. All recommendations must then rely on the current structure plus broadly accepted .NET conventions.

## 2  Task / Goal

1. **Analyse** the current repo layout **and `.gitignore` patterns**.  
2. **Compare** them to the available best-practice material (PDF or highlights).  
3. **Produce** the deliverables listed below.

## 3  Deliverables (save in `docs/deliverables/`)

| # | File                                      | Description |
| - | ----------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1 | `vs-solution-organization-guidelines.md`  | Best-practice guideline following §4 outline. |
| 2 | `vs-solution-organization-guidelines.pdf` | Generated with **pandoc**:<br>`pandoc vs-solution-organization-guidelines.md -o vs-solution-organization-guidelines.pdf`.<br>If `pandoc` is unavailable, **do not create a PDF**. Instead return the literal line **`PDF-SKIPPED`** in the output order below. |
| 3 | `project-structure-recommendations.md`    | Gap-analysis table (issues ➜ fixes). **Include a subsection that lists any missing, redundant, or mis-scoped patterns in `.gitignore`, with recommended changes.** |

> **Versioning**: Always overwrite `project-structure-recommendations.md`—rely on Git history to track revisions. For the other deliverables, if a filename already exists, append **`-v{N}`** (plain integer, e.g., `-v2`, `-v3`).

## 4  Required Content Structure (guideline .md)

### 4.1 Sections

1. **Overview** – one-paragraph summary of solution purpose and scope.  
2. **Why Organization Matters** – 2-3 bullets (onboarding speed, CI, isolation).  
3. **Directory & File Layout** – ideal tree in a fenced block:

   ```text
   (tree here)
   ```

   *(Make sure the block is closed with three back-ticks.)*  
4. **Solution (.sln) Best Practices** – naming, granularity, solution-folder usage.  
5. **Project (.csproj) Best Practices** – folder-per-project, `RootNamespace`, output paths.  
6. **VS Code Workspace Best Practices** – `.vscode/`, dev-container, extension list.  
7. **Onboarding Essentials** – README, CONTRIBUTING, CODEOWNERS, diagrams.  
8. **Ignore-File (.gitignore) Best Practices** – structure, ordering, comments, typical .NET patterns (e.g., `bin/`, `obj/`, `*.user`, `*.suo`).  
9. **Anti-Patterns to Avoid** – Markdown table with **≥ 6 distinct** structural issues. Columns: *Anti-Pattern* | *Why It Hurts* | *Better Alternative*.  
10. **Die-Hard Requirements** – non-negotiable checklist (naming, secrets, reproducible build, test parity, single authoritative .sln, correct ignore rules).  
11. **Gap Analysis & Recommendations** – table comparing current vs. ideal. Columns: *Issue* | *Current Path / Pattern* | *Recommended Change* | *Rationale*.  
12. **References** *(optional)* – include only if explicit citations \[1] are present.

### 4.2 Formatting rules

* Headings must follow the numbering above (`##` primary, `###` secondary).  
* Inline file paths & names must be wrapped in back-ticks.  
* Use **Markdown tables** with a header row (`| --- |`) and **no hard wraps** inside a row.  
* Indent directory tree with **two spaces per level**.

## 5  Writing Guidelines

* Target ≈ 3 000 words across sections 1–11; References (if present) are extra.  
* Use bullet lists over prose where practical; keep paragraphs ≤ 4 sentences.  
* ONE blank line between major sections; *no* blank lines between bullets or table rows.  
* Encode Markdown in **UTF-8 (no BOM)**.  
* No subjective phrases (“I think”); maintain neutral, factual tone.  
* **Do not** cite or speculate about best practices beyond provided material; if PDF missing, explicitly note omission.  
* No shell, PowerShell, or code snippets except the single `pandoc` command shown.

## 6  Output to User

1. **Git staging example** — include these two lines verbatim (Codex does not execute them):

   ```text
   git add docs/deliverables/*
   git commit -m "docs: add updated org guidelines & gap analysis (auto-generated)"
   ```
2. Print **exactly three lines** **in this order** (include any `-v{N}` suffixes):

   1. `./docs/deliverables/vs-solution-organization-guidelines.md`  
   2. `./docs/deliverables/vs-solution-organization-guidelines.pdf` **or** `PDF-SKIPPED`  
   3. `./docs/deliverables/project-structure-recommendations.md`
3. If file creation fails, output **`CODEX-ERROR:`** followed by the message *and nothing else*.

---

> **Reminder to Codex**: rely **only** on supplied artefacts. If the best-practices PDF cannot be parsed, state that fact and omit PDF-specific comparisons. No guessing, no extra code blocks.