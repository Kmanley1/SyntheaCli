# Codex Task: Automate Task Processing, Context Execution, and Timestamped Archiving for **Synthea CLI**

---

## Objective
Fully automate the workflow for all Markdown task files in `docs\tasks\`.  
For **every** normal task, the automation must:

1. **Run pre‑context helpers** found in `tasks\context\pre\*.md`.
2. **Execute the task** itself.
3. **Run post‑context helpers** found in `tasks\context\post\*.md`.
4. **Archive the task file** by renaming it with its UTC **start‑time** prefix  
   `YYYY-MM-DD_HH-MM-SS-` and moving it into `tasks\implemented\`.

*Context files* (`tasks\context\**`) must **never** be moved or renamed.

---

## Acceptance Criteria
- [ ] Pre‑context files execute **before** each normal task.
- [ ] Post‑context files execute **after** each normal task; use `finally` or equivalent so they run even if the task fails.
- [ ] Normal task files are renamed to `YYYY-MM-DD_HH-MM-SS-<original>.md` using the task’s **start time (UTC)**.
- [ ] Renamed files reside in `tasks\implemented\`.
- [ ] Files under `tasks\context\` remain untouched.
- [ ] Script logs clear status messages and handles errors gracefully.
- [ ] Idempotent: rerunning does not double‑prefix filenames or re‑execute completed tasks.
- [ ] Unit‑test coverage confirms ordering, renaming, idempotency, and context protection.

---

## Implementation Steps

### 1  File Enumeration
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
```

### 3  Logging & Error Handling
- Use `Write-Host` or a logger to announce each phase.
- On fatal error inside task implementation, write an error entry and continue to next task.

### 4  Unit Tests
- Mock filesystem to assert:
  * Execution order: pre ➜ task ➜ post.
  * Correct timestamp prefix.
  * No double‑prefix on rerun.
  * Context files remain in place.

---

## Testing Requirements
- Confirm timestamp uses **UTC** and correct format.
- Validate that pre/post helpers run exactly once per task.
- Ensure renamed files land under `tasks\implemented\`.
- Achieve ≥ 90 % code‑coverage.

---

## Definition of Done
- [ ] Automation script committed.
- [ ] CI tests pass.
- [ ] Documentation (`README.md` or `docs/automation.md`) updated.
- [ ] Peer review complete & branch merged.

