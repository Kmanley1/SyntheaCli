# Codex Task: Run Automated Task Processing, Context Execution, and Timestamped Archiving for **Synthea CLI** Codex Tasks

---

## Objective
Use the automated workflow documented in deliverables\codex-automation.md for all Codex Markdown task files in `docs\tasks\`. Iterate through a folder with codex tasks and evaluate if the task has been completed, then implement each task sequentially, then rename and move the markdown file, if it is in the \tasks directory and not the \tasks\context sub-directory.

For **every** normal task, the automation must:

1. **Run pre‑context helpers** found in `tasks\context\pre\*.md`.
2. **Execute the task** itself and evaluate if the task has been completed.
3. **Run post‑context helpers** found in `tasks\context\post\*.md`.
4. **Archive the task file** by renaming it with its UTC **start‑time** prefix  
   `YYYY-MM-DD_HH-MM-SS-` and moving it into `tasks\implemented\`.

*Context files* (`tasks\context\**`) must **never** be moved or renamed.

---

## Acceptance Criteria
- [ ] Iterates through each markdown (`*.md`) task file.
- [ ] Pre‑context files execute **before** each normal task.
- [ ] Post‑context files execute **after** each normal task; use `finally` or equivalent so they run even if the task fails.
- [ ] Normal task files are renamed to `YYYY-MM-DD_HH-MM-SS-<original>.md` using the task’s **start time (UTC)**.
- [ ] Renamed files reside in `tasks\implemented\`.
- [ ] Files under `tasks\context\` remain untouched.
- [ ] Script logs clear status messages and handles errors gracefully.
- [ ] Idempotent: rerunning does not double‑prefix filenames or re‑execute completed tasks.
- [ ] Unit‑test coverage confirms ordering, renaming, idempotency, and context protection.
- [ ] Logs clear and informative status messages.
- [ ] Gracefully handles exceptions and errors.

---

## Implementation Steps

### Step 1: File Enumeration
- Identify all markdown files (`*.md`) in the source tasks folder.

### Step 2: Task Implementation
- Read and understand the task, if you do not understand stop and ask questions.
- Implement the task.

### Step 3: File Renaming and Movement
- Upon successful task completion, move files to the target directory.
- Go to next task

---

## Example PowerShell Script To Help You Understand

```powershell
$contextPre  = Get-ChildItem "tasks/context/pre"  -Filter *.md -File | Sort-Object Name
$contextPost = Get-ChildItem "tasks/context/post" -Filter *.md -File | Sort-Object Name
$tasks       = Get-ChildItem "tasks" -Filter *.md -File |
               Where-Object { $_.FullName -notmatch 'tasks[\/](context)[\/]' } |
               Sort-Object Name
```

### 2  Task Loop
```powershell
foreach ($task in $tasks) {
    $startUtc = Get-Date -AsUTC
    try {
        $contextPre  | ForEach-Object { run_codex_task $_.FullName }

        run_codex_task $task.FullName
    }
    finally {
        $contextPost | ForEach-Object { run_codex_task $_.FullName }
    }

    # Rename + move
    $prefix  = $startUtc.ToString('yyyy-MM-dd_HH-mm-ss')
    if ($task.Name -notmatch '^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}-') {
        $newName = "${prefix}-$($task.Name)"
        Move-Item $task.FullName -Destination "tasks/implemented/$newName"
    }
}

---

## Testing Requirements
- Verify file moves occur only upon successful task completion.
- Confirm error handling for invalid paths or inaccessible files.
- Confirm good unit test coverage
---

## Definition of Done
- [ ] Script checked into source control.
- [ ] Successfully unit tested.
- [ ] Documentation updated.
- [ ] Peer-reviewed and merged