# Codex Prompt – Update Automation for Pre/Post Context Tasks

**Background/Context:**  
Your repository contains an automation script defined in **`prompts\codex-task-template-automation.md`**.  
Current behaviour:

* Normal task specs live in `tasks\` (one *.md* per task).  
* Helper tasks live in `tasks\context\` and are executed each run, never moved.  

We now split context helpers into **pre‑tasks** and **post‑tasks**:

```
tasks\context\pre\   # must run *before* every normal task
tasks\context\post\  # must run *after* every normal task
```

---

**Task/Goal:**  
For **each** task in `tasks\`:

1. Execute every Markdown file in `tasks\context\pre\` (sorted, alphanumeric) **first**.  
2. Execute the current normal task file.  
3. Execute every Markdown file in `tasks\context\post\` (sorted, alphanumeric) **last**.  

Additional rule: files anywhere under `tasks\context\` are **never** moved to `tasks\implemented\`.

---

**Specific Requirements:**

1. **Respect existing logic** – Load and preserve all capabilities already described in `prompts\codex-task-template-automation.md` (logging, change‑log update, etc.).  
2. **Pre/Post execution**  
   * Guarantee pre‑tasks run before *each* normal task.  
   * Guarantee post‑tasks run after *each* normal task, even if the normal task fails (use `finally` or similar).  
3. **Skip relocation** – Exclude any path matching `tasks\context\**` from the “move to implemented” step.  
4. **Idempotent & deterministic** – Running automation twice should not duplicate pre/post execution; order should be consistent (sort filenames).  
5. **Commit message**  
   ```
   chore(automation): enforce pre/post context task execution order
   ```  
6. **Tests / Validation** – Extend or add tests that assert:  
   * Pre‑tasks executed before every normal task.  
   * Post‑tasks executed after every normal task.  
   * Context files never moved.  
7. **Documentation** – Update `README.md` (or create a `docs/automation.md`) with a “Pre/Post Context Tasks” section explaining the new flow.

---

**Execution flow (pseudocode):**

```bash
for normal in $(ls tasks/*.md | grep -v '^tasks/context/' | sort); do
  # run pre‑tasks
  for pre in $(ls tasks/context/pre/*.md | sort); do
    run_codex_task "$pre"
  done

  # run the normal task
  run_codex_task "$normal"

  # run post‑tasks
  for post in $(ls tasks/context/post/*.md | sort); do
    run_codex_task "$post"
  done

  # move completed normal task
  mv "$normal" tasks/implemented/
done
```

---

**Output Format:**  
Commit the updated automation script(s) and tests. If run with a `--dry-run` flag, print a summary showing the order tasks would execute.

---

**Additional Instructions:**  
* Use cross‑platform path handling (works on Windows PowerShell and *nix shell).  
* Fail fast with clear errors if `tasks/context/pre/` or `tasks/context/post/` is missing.  
* Keep dependencies minimal; prefer native shell, Python, or PowerShell that exists in CI.
