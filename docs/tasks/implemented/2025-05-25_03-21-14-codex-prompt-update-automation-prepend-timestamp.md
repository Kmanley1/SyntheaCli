# Codex Prompt – Add Timestamp Prefix to Implemented Task Filenames

**Background/Context:**  
Your automation, defined in **`prompts\codex-task-template-automation.md`**, currently moves a completed task file from  
`tasks\` → `tasks\implemented\` without renaming it.  
To improve chronological tracking we want each file in `tasks\implemented\` to begin with the UTC start‑time of the task in sortable format:

```
YYYY-MM-DD_HH-MM-SS-<original-file-name>.md
```

Example:  

```
2025-05-24_21-05-00-update-readme.md
```

---

**Task/Goal:**  
Update the existing automation so that, **at the moment a task starts**, it captures that timestamp and, after successful completion, renames (or moves) the task file into `tasks\implemented\` with the timestamp prefix plus a trailing hyphen.

---

**Specific Requirements:**

1. **Timestamp capture**  
   * Determine the task’s *start* time in **UTC**.  
   * Format with `.ToString("yyyy-MM-dd_HH-mm-ss")` (PowerShell / .NET) or `strftime('%Y-%m-%d_%H-%M-%S')` (Python/Bash).  
2. **Prefix & hyphen** – Construct new filename: `<timestamp>-<originalName>` (single hyphen after seconds).  
3. **No double‑prefixing** – If the file already matches `^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}-`, skip adding another timestamp.  
4. **Path logic** – File must reside in `tasks/implemented/` after rename, preserving any subfolder structure.  
5. **Commit message**  
   ```
   chore(automation): prefix implemented tasks with start timestamp
   ```  
6. **Tests / Validation** – Extend CI tests to assert:  
   * A newly implemented file gets the correct prefix format.  
   * Running the automation twice does not alter already‑prefixed files.  
   * Files under `tasks/context/` remain untouched.  
7. **Documentation** – Update README/docs to describe the new naming scheme.

---

**Suggested Implementation Snippet (PowerShell‑style pseudocode):**

```powershell
$startUtc  = Get-Date -AsUTC
$prefix    = $startUtc.ToString("yyyy-MM-dd_HH-mm-ss")
$newName   = "${prefix}-$($taskFile.Name)"

if ($taskFile.Name -notmatch '^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}-') {
    $destPath = "tasks\implemented\$newName"
    Move-Item $taskFile.FullName $destPath
}
```

---

**Output Format:**  
Automation commits updated scripts and tests. In `--dry-run` mode, output a table mapping old ➜ new filenames.

---

**Additional Instructions:**  
* Ensure cross‑platform compatibility (PowerShell & Bash).  
* Fail gracefully if `tasks/implemented/` is missing.  
* Keep dependencies minimal; rely on built‑in shell/Python/PowerShell utilities only.
