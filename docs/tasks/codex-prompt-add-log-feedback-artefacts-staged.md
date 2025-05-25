# Codex Prompt – Generate Log & Feedback Files in **tasks\staged**

**Context**

Current automation archives completed tasks in `tasks\implemented\` after renaming them.  
We now want two additional artefacts—log & feedback files—created in **`tasks\staged\`** instead of alongside the implemented task.

---

## Goal

For every executed normal task:

1. Capture execution logs.
2. Generate a feedback summary analysis of the task.
   1. Analyze your codex logs and provide feedback to be used in continious improvements.
3. Create **two Markdown files** in `tasks\staged\`:

   | Purpose | File name pattern | Example |
   |---------|------------------|---------|
   | Execution log | `<timestamp>-<task-name>-log.md` | `2025-05-24_23-28-34-architecture_review_task-log.md` |
   | Codex feedback | `<timestamp>-<task-name>-feedback.md` | `2025-05-24_23-28-34-architecture_review_task-feedback.md` |

4. Insert a pointer section into the original task (before it is moved) that links to these staged artefacts.

---

### Pointer Section Template

```markdown
## Post‑run Artefacts
- [Execution Log](../../tasks/staged/<timestamp>-<task-name>-log.md)
- [Codex Feedback](../../tasks/staged/<timestamp>-<task-name>-feedback.md)
```

*(Adjust relative path if needed in code.)*

---

## Detailed Requirements

- **Timestamp**: UTC start, `YYYY-MM-DD_HH-MM-SS`.  
- **Name derivation**: task filename without extension, spaces→underscores.  
- **Log capture**: last 200 StdOut/StdErr lines, ANSI stripped, fenced ```text```.  
- **Feedback**: bullet list summarising WARN/ERROR counts and duration.  
- **Idempotency**: if same filenames exist in `tasks\staged\`, add `-v2`, `-v3`, …  
- **Tests**: ensure staged files are created and pointer links resolve.  
- **Commit message**:  
  ```
  feat(automation): output log & feedback artefacts to tasks\staged
  ```

---

## Validation Checklist

- [ ] Renamed task moved to `tasks\implemented\`.  
- [ ] Corresponding `*-log.md` and `*-feedback.md` created in `tasks\staged\`.  
- [ ] Task file contains `## Post‑run Artefacts` with correct paths.  
- [ ] Subsequent runs version or skip duplicates appropriately.  
- [ ] CI tests pass on all OS targets.

---

**Implementation Notes**

- Use `Path.Combine("tasks","staged", fileName)` for portability.  
- Ensure `tasks\staged\` exists (create if missing).  
- Update `docs/codex-automation.md` to describe new staged artefacts flow.
