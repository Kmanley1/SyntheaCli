# Codex Automation Definition & High‑Level Workflow

1. **Author initiates**

   - Copies the full task block from **`codex-task-template-run-codex-automation.md`**.
   - Pastes it directly into the Codex Chat prompt.

2. **Codex interprets task**

   - Reads the instructions and identifies `CodexTaskProcessor` as the execution engine.
   - Validates folder structure (`docs/tasks`, `tasks/context/*`, etc.).
   - **Loads `prompts/codex-task-template-automation.md` into its context memory** so all subsequent sub‑tasks remain aligned with the master specification.

3. **Automation kickoff**

   - Codex triggers the **automation process** described in the task template.
   - Pre‑context → Task → Post‑context flow executes for each task file.

4. **Artefact generation**

   - Renamed task file is moved to `tasks/implemented/`.
   - Log and feedback files are created in `tasks/staged/`.
   - Original task gains a `## Post‑run Artefacts` section linking to the new files.

## Pre/Post Context Tasks

The Codex automation processes tasks under `docs/tasks`. For each normal task file it:

1. Runs all pre-task markdown files from `tasks/context/pre/` in sorted order.
2. Executes the normal task file.
3. Runs all post-task markdown files from `tasks/context/post/` in sorted order.

The automation ensures the context directories exist. If either `tasks/context/pre/` or
`tasks/context/post/` is missing, it is automatically created so processing can continue.
All context and task files are processed in alphanumeric order for deterministic results.

Files anywhere under `tasks/context` are never moved to `tasks/implemented`.

When a task is successfully implemented, its markdown file is renamed with the UTC start time of that task:

```
YYYY-MM-DD_HH-MM-SS-original-name.md
```

The renamed file is then moved into `tasks/implemented/`. If a file already starts with that timestamp pattern it is left unchanged so rerunning the automation is safe.

For each completed task the automation also creates two artefacts in `tasks/staged/`:

* `<timestamp>-<task-name>-log.md` – last 200 lines of stdout/stderr.
* `<timestamp>-<task-name>-feedback.md` – summary with WARN/ERROR counts and duration.

The original task file receives a `## Post‑run Artefacts` section linking to these files before it is moved.

Use `CodexTaskProcessor.ProcessTasks` to run the automation.
