# Codex Prompt – Update Automation to Run Context Tasks

**Background/Context:**  
Your repository uses an automation script described in **`prompts\codex-task-template-automation.md`**.  
Current workflow:

* All individual task specs live in `tasks\` (one *.md* file per task).  
* When Codex completes a task it moves the spec file from `tasks\` to `tasks\implemented\`.  
* *Context* helpers (common setup, shared variables, helper snippets) live in `tasks\context\`.

We discovered two issues:

1. Files inside `tasks\context\` must be executed **every time** a task in `tasks\` runs.  
2. Context files should **never** be moved to `tasks\implemented\`. They must remain in place for future tasks.

---

**Task/Goal:**  
Update the automation defined in `prompts\codex-task-template-automation.md` so that:

* Each run includes (sources and executes) every `*.md` file in `tasks\context\` **before** processing individual task files.  
* Files under `tasks\context\` are excluded from the “move to implemented” step.  
* All other behaviour (logging, commit workflow, etc.) remains unchanged.

---

**Specific Requirements:**

1. **Remember existing automation** – Load and respect all logic already specified in `prompts\codex-task-template-automation.md`.  
2. **Execution order** – Ensure context tasks execute first; subsequent tasks may depend on variables/functions they define.  
3. **No relocation** – Skip move/rename operations for any file whose path matches `tasks\context\*`.  
4. **Idempotency** – Running the automation multiple times must not duplicate context execution nor produce errors if context files were already sourced.  
5. **Commit message** – Use the conventional commit  
   ```
   chore(automation): ensure context tasks always run & stay in place
   ```  
6. **Validation** – Update or create unit/integration tests to verify:  
   * Context tasks are executed on every run.  
   * Context files remain in their original folder.  
   * Non‑context tasks are still moved after completion.  
7. **Documentation** – Append a short “Context Tasks” subsection to `README.md` explaining the new behaviour.

---

**Examples (pseudo‑code snippets):**

```bash
# Pseudocode – iterate context tasks first
for f in $(ls tasks/context/*.md | sort); do
  run_codex_task "$f"
done

# Then handle normal tasks
for f in $(ls tasks/*.md | grep -v 'context/' | sort); do
  run_codex_task "$f"
  mv "$f" tasks/implemented/
done
```

---

**Output Format:**  
Commit the updated automation script(s) and any new tests. No console output required unless run in dry‑run mode, in which case print a summary of operations.

---

**Additional Instructions:**  
* Minimise external dependencies; prefer standard shell/Python/PowerShell available in CI image.  
* Fail fast with clear error messages if context directory is missing.  
* Use cross‑platform path handling (Windows & *nix).  
* Keep the solution within the existing project layout; do not introduce new top‑level directories.

