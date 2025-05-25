# Codex Task – Verify Automation End‑to‑End

**Background / Purpose**

This task intentionally exercises the full Codex automation pipeline—including pre/post context execution, log capture, feedback generation, and artefact placement in `tasks/staged`—without making any code changes to the main project.

---

## Steps Codex Must Perform

1. **Echo a Message**  
   Execute a simple shell/PowerShell command that prints `AUTOMATION_TEST_ONE_OK`. This guarantees predictable log output.

2. **Verify Pre/Post Context Hooks**  
   Confirm that any markdown files in `tasks/context/pre` ran **before** this task and any in `tasks/context/post` ran **after**.

3. **Generate Artefacts**  
   After execution, the automation should:

   - Rename this markdown file with timestamp prefix and move it to `tasks/implemented`.
   - Create two files in `tasks/staged`:
     - `<timestamp>-codex-automation-test-task-log.md`
     - `<timestamp>-codex-automation-test-task-feedback.md`
   - Insert a `## Post‑run Artefacts` section into the archived task linking to those files.

4. **Feedback Content**  
   Ensure the feedback file contains at least one bullet acknowledging that `AUTOMATION_TEST_ONE_OK` appeared in the logs.

---

## Acceptance Criteria

- The archived task resides in `tasks/implemented/` with correct timestamp naming.
- The log file in `tasks/staged/` includes the line `AUTOMATION_TEST_ONE_OK`.
- The feedback file in `tasks/staged/` references the successful echo.
- Pointer links in the archived task are valid relative paths.

---

### Commit Message Example

```text
chore(test): successful run of automation smoke‑test task
```

