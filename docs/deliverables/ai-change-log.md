# AI Change Log

This log records every automated task completed by Codex/GitHub Copilot or other AI agents for the **Synthea CLI** repository. Automations must append a new entry at the top of **Task Entries** using the template below.

---

## How to Use
1. **Automation** – After a task finishes, the automation script opens this file, inserts a new entry at the top of *Task Entries*, commits, and pushes the change.
2. **Manual fixes** – Developers may correct typos in past entries but **must never** reorder or delete historical items.
3. **Validation** – The CI pipeline fails if the file is missing required fields or the Markdown is malformed.

### Entry Template <!-- do not remove; used by validation script -->
```markdown
### Task Name: <short, imperative>
**Date:** YYYY-MM-DD
**Author:** <automation id or dev>
**Summary:** <1-2 sentence overview of what the task accomplished, referencing PR/commit if applicable>
**Acceptance Criteria:**
- [ ] Criterion 1
- [ ] Criterion 2
---
```

---

## Task Entries
<!-- New entries must be inserted directly below this line -->

### Task Name: Sync automation docs with code
**Date:** 2025-05-25
**Author:** Codex-Automation
**Summary:** Updated codex-automation.md and archived task file, commit 1aa0e98.
**Acceptance Criteria:**
- [x] Documentation matches actual behaviour
- [x] Task file moved to implemented with timestamp
---

