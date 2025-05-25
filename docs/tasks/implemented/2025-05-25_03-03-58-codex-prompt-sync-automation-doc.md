# Codex Prompt – Sync Automation Documentation with Reality

**Background/Context:**  
* `prompts\codex-task-template-automation.md` — ground‑truth specification for our automation.  
* `docs\codex-automation.md` — human‑readable documentation that *should* mirror the implementation.

---

**Task/Goal:**  
Analyse the current automation implementation, compare it with both the specification and existing docs, and update `docs\codex-automation.md` so it accurately reflects reality.

1. **Remember** the instructions in `prompts\codex-task-template-automation.md`.  
2. **Inspect** automation scripts/code in the repo to derive actual behaviour.  
3. **Compare** implementation vs. documentation.  
4. **Edit `docs\codex-automation.md`** to fix any mismatches, adding new sections or amending outdated ones.

---

**Specific Requirements**

1. **Discrepancy detection**  
   * Flag differences in paths, task flow (pre/post), timestamp handling, error handling, etc.  
2. **Documentation update**  
   * Keep existing correct content; insert or modify only what’s needed.  
   * Use headings, bullet lists, and code snippets mirroring current code.  
3. **Commit** with message:  
   ```
   docs(automation): sync codex-automation.md with current implementation
   ```  
4. **Validation**  
   * Ensure Markdown passes lint/CI checks.

---

**Output Format**  
*Normal run:* commit changes.  
*Dry‑run (`--dry-run`):* output unified diff.

---

**Additional Instructions**  
* Do **not** modify automation code in this task.  
* Keep examples cross‑platform (PowerShell & Bash).  
* Maintain the doc’s existing tone and structure.
