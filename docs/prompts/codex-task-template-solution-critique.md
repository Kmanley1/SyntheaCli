# Codex Prompt – VS Solution & VS Code Organization Analysis (Markdown‑Only)

*Last Updated (UTC): 2025‑05‑27*

---

## 1  Background / Context

This task evaluates our Visual Studio **solution**, VS Code **workspace**, and **repository ignore rules** against recognised .NET project‑layout standards. Codex will analyse the current structure, compare it to the supplied best‑practice guidance, and output updated documentation **entirely in Markdown**.

**Inputs provided to Codex**

| Input                   | Path                                                              | Purpose                                             |
| ----------------------- | ----------------------------------------------------------------- | --------------------------------------------------- |
| Current structure       | `docs/deliverables/project-structure.md`                          | Markdown tree of the repo’s actual folders/projects |
| Repository ignore rules | `.gitignore`                                                      | Determines what gets excluded from source control   |
| Best‑practice guide     | `docs/research/NET Core CLI GitHub Repository Best Practices.pdf` | Reference standards                                 |

> **Note:** If the PDF cannot be read, rely on broadly accepted .NET conventions and the current project structure; do **not** invent its contents.

## 2  Task / Goal

1. **Analyse** the current repo layout in `docs/deliverables/project-structure.md` **and** `.gitignore` patterns.
2. **Compare** findings to the best‑practice guide (or, if unreadable, to accepted .NET conventions).
3. **Produce** the Markdown deliverables listed below.

## 3  Deliverables (saved to `docs/deliverables/` and **always updated, not versioned**)

| # | File                                     | Purpose                                                                               |
| - | ---------------------------------------- | ------------------------------------------------------------------------------------- |
| 1 | `vs-solution-organization-guidelines.md` | Updated best‑practice guideline (structure defined in §4).                            |
| 2 | `project-structure-recommendations.md`   | Gap‑analysis table of issues → fixes, including recommended `.gitignore` adjustments. |

*No additional files or version‑suffixed copies are created; Git history tracks all revisions.*

## 4  Required Content Structure (`vs-solution-organization-guidelines.md`)

*(Same numbered sections and formatting rules as previous prompt, with all PDF references removed.)*

## 5  Writing Guidelines

- Target ≈ 3 000 words across sections 1–11; References, if present, are extra.
- Bullet > prose where practical; paragraphs ≤ 4 sentences.
- ONE blank line between major sections; *no* blank lines between bullets or table rows.
- Encode Markdown in **UTF‑8 (no BOM)**.
- Neutral, factual tone; no speculation beyond provided material.
- **No shell or code blocks** except the Git example in §6.

## 6  Output to User

1. **Git staging example** – include verbatim:
   ```text
   git add docs/deliverables/*
   git commit -m "docs: update guidelines & gap analysis (auto-generated)"
   ```
2. Print **exactly two repo‑relative paths** in this order:
   1. `./docs/deliverables/vs-solution-organization-guidelines.md`
   2. `./docs/deliverables/project-structure-recommendations.md`
3. If file creation fails, output **`CODEX-ERROR:`** followed by the message *and nothing else*.

---

> **Reminder to Codex:** use only supplied artefacts and well‑known .NET conventions. No PDF handling, no extra files, and no code snippets other than Git .
