# Codex Prompt – Troubleshoot Why Automation Didn’t Move Last Task

**Background/Context**

* **Specification** – `prompts\codex-task-template-automation.md` (remember this as the intended behaviour).  
* **Symptom** – The recently executed task file `codex-prompt-sync-automation-doc.md` remains in `tasks\` instead of being renamed with a timestamp and moved to `tasks\implemented\`.

---

## Task / Goal

Conduct a root‑cause analysis of the automation code to determine **why normal tasks are not being archived** as specified.

1. **Load & remember** the instructions in `prompts\codex-task-template-automation.md`.  
2. **Examine** the current automation scripts / CI job that processes tasks.  
3. **Identify** the reason `codex-prompt-sync-automation-doc.md` was left unmoved.  
4. **Propose and implement** a fix so that future normal tasks are timestamp‑prefixed and relocated correctly.

---

### Deliverables

1. **Diagnosis report** (brief comment in commit message or log) stating root cause.  
2. **Patch** to automation code with clear inline comments.  
3. **Updated tests** verifying that a sample task is now renamed and moved to `tasks\implemented\`.  
4. **Commit message**

   ```
   fix(automation): move tasks to implemented after execution
   ```

---

## Investigation Guide

Consider (non‑exhaustive):

| Potential Issue | Quick Check |
|-----------------|-------------|
| File‑filter regex inadvertently excludes new file name | Inspect the `Where-Object` / `grep -v` filter. |
| Timestamp prefix check blocking move | Ensure prefix test runs *after* task executed. |
| Move‑Item path or permissions | Validate destination folder exists and is writable in CI. |
| Early error stops post‑move logic | Confirm errors aren’t swallowed before rename step. |
| CI job path context | Make sure script runs from repo root where relative paths resolve. |

---

## Validation

* Run the automation locally (`--dry-run`) and show that `codex-prompt-sync-automation-doc.md` is now moved & renamed.  
* CI test suite must pass.

---

**Additional Instructions**

* Keep solution cross‑platform (PowerShell & Bash).  
* Preserve existing features: pre/post context run, timestamp format, double‑prefix guard.  
* Log before/after filenames for easier debugging.

