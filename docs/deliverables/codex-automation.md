# Codex Automation Overview

## Pre/Post Context Tasks

The Codex automation processes tasks under `docs/tasks`. For each normal task file it:

1. Runs all pre-task markdown files from `tasks/context/pre/` in sorted order.
2. Executes the normal task file.
3. Runs all post-task markdown files from `tasks/context/post/` in sorted order.

The automation ensures the context directories exist. If either `tasks/context/pre/` or
`tasks/context/post/` is missing, it is automatically created so processing can continue.

Files anywhere under `tasks/context` are never moved to `tasks/implemented`.

When a task is successfully implemented, its markdown file is renamed with the UTC start time of that task:

```
YYYY-MM-DD_HH-MM-SS-original-name.md
```

The renamed file is then moved into `tasks/implemented/`. If a file already starts with that timestamp pattern it is left unchanged so rerunning the automation is safe.

Use `CodexTaskProcessor.ProcessTasks` to run the automation. Pass `--dry-run` to print the execution order without performing moves.
