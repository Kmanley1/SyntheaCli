# Codex Prompt – Update Automation for New Context Folder Path

**Background/Context:**  
The automation logic defined in **`prompts\codex-task-template-automation.md`** was written when context helpers lived under  
`tasks\implemented\context\`.  
The entire **context folder has been moved** to the project‑root path:

```
tasks\context\          # now the one and only context root
│   ├─ pre\   # always-run tasks executed before each normal task
│   └─ post\  # always-run tasks executed after each normal task
```

All previous path references need to be updated.

---

**Task/Goal:**  
Modify the existing automation so that it correctly:

1. Locates context helpers in `tasks\context\` (and its `pre\` / `post\` subfolders).  
2. Executes **pre‑tasks first**, the normal task, then **post‑tasks last** for every file in `tasks\`.  
3. Excludes every file under `tasks\context\**` from being moved to `tasks\implemented\`.  
4. Removes any obsolete references to `tasks\implemented\context\`.

---

**Specific Requirements:**

1. **Refactor path constants** – Replace hard‑coded `tasks/implemented/context/…` with `tasks/context/…`.  
2. **Execution order** – Maintain the pre‑task ➜ normal‑task ➜ post‑task sequence already established.  
3. **Migration safety** – If the old folder still exists, ignore it; do not delete it automatically.  
4. **Commit message**  
   ```
   chore(automation): point context paths to tasks/context
   ```  
5. **Tests / Validation** – Add or update tests to assert:  
   * Pre‑tasks and post‑tasks found in the new location run in the correct order.  
   * Normal tasks still move to `tasks/implemented/`.  
   * No file under `tasks/context/` is moved.  
6. **Documentation** – Update README or docs to reflect the new context folder location.

---

**Suggested Implementation Snippet (pseudo‑code):**

```bash
CONTEXT_ROOT="tasks/context"

PRE_DIR="$CONTEXT_ROOT/pre"
POST_DIR="$CONTEXT_ROOT/post"

for task in $(ls tasks/*.md | grep -v '^tasks/context/' | sort); do
  # run pre‑tasks
  for pre in $(ls "$PRE_DIR"/*.md 2>/dev/null | sort); do
      run_codex_task "$pre"
  done

  run_codex_task "$task"

  # run post‑tasks
  for post in $(ls "$POST_DIR"/*.md 2>/dev/null | sort); do
      run_codex_task "$post"
  done

  mv "$task" tasks/implemented/
done
```

---

**Output Format:**  
Commit updated automation script(s) and tests. With `--dry-run`, print the order of execution for one sample task.

---

**Additional Instructions:**  
* Keep the solution cross‑platform (bash & PowerShell).  
* Fail fast with clear errors if `tasks/context/` is missing.  
* Minimise external dependencies; rely on built‑in shell/Python/PowerShell utilities only.
