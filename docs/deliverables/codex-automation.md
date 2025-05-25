# Codex Automation Overview

## Pre/Post Context Tasks

The Codex automation processes tasks under `docs/tasks`. For each normal task file it:

1. Runs all pre-task markdown files from `tasks/context/pre/` in sorted order.
2. Executes the normal task file.
3. Runs all post-task markdown files from `tasks/context/post/` in sorted order.

Pre- and post-task directories must exist. If either is missing, the automation fails with a clear `DirectoryNotFoundException` message.

Files anywhere under `tasks/context` are never moved to `tasks/implemented`.

Use `CodexTaskProcessor.ProcessTasks` to run the automation. Pass `--dry-run` to print the execution order without performing moves.
