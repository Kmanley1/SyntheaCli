# Codex Prompt – Automate AI Change‑Log Updates

**Background/Context:**  
Your repository already contains two key templates:

* `prompts\codex-task-template-ai-change-log.md` – defines the Markdown layout for a single change‑log entry (Task Name, Date, Summary, Acceptance Criteria, etc.).  
* `deliverables\ai-change-log.md` – a master log file whose *Task Entries* section must grow every time an automated task completes.

Codex will act as the automation agent that appends a correctly‑formatted entry to `deliverables\ai-change-log.md` whenever it finishes a task.

---

**Task/Goal:**  
Implement an automated routine that, upon completing *this* Codex task, uses the template in `prompts\codex-task-template-ai-change-log.md` to create a new entry **at the very top** of the *Task Entries* section in `deliverables\ai-change-log.md`, then commits and pushes the updated file.

---

**Specific Requirements:**

1. **Read template** – Parse `prompts\codex-task-template-ai-change-log.md` to understand the required headings and field order.  
2. **Generate entry** – Populate the template with:  
   * **Task Name** – A concise, imperative title of the task Codex just executed.  
   * **Date** – Today’s date in `YYYY-MM-DD` format.  
   * **Author** – `Codex-Automation` (or another identifier you choose).  
   * **Summary** – One- or two-sentence description referencing the commit hash or PR number if applicable.  
   * **Acceptance Criteria** – Bullet list of verifiable outcomes; mark them complete with `[x]` if satisfied.  
3. **Insert position** – Place the new entry immediately **below** the `<!-- New entries must be inserted directly below this line -->` marker in `deliverables\ai-change-log.md`.  
4. **Preserve history** – Do not modify or remove existing entries; only add.  
5. **Validation** – Ensure Markdown is valid and respects the template header (`### Task Name:` etc.) so CI linters pass.  
6. **Commit & push** – Create a commit with message  
   ```
   docs(ai-change-log): add entry for <Task Name> – <YYYY-MM-DD>
   ```  
   Push to the current working branch.

---

**Examples:**  
*(Example values—replace with real data when executing)*

```markdown
### Task Name: Implement NuGet Packaging
**Date:** 2025-05-24
**Author:** Codex-Automation
**Summary:** Added GitHub Action to pack and publish Synthea.Cli as a global tool (commit abc1234).
**Acceptance Criteria:**
- [x] Pipeline packs `.nupkg` on every tag
- [x] Artifact appears in GitHub Release page
---
```

---

**Output Format:**  
*No console output is required.* Codex should perform the file edit and git commit/push. If run in dry-run mode, print the would-be entry to stdout in valid Markdown.

---

**Additional Instructions:**  
* Assume repository root as working directory.  
* Fail gracefully if either file path is missing.  
* If multiple tasks run in one execution, loop and create an entry per task.  
* Keep the code self-contained; use Node.js or PowerShell if scripting is required, but minimize external dependencies.
