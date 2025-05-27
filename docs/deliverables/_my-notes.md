# Iterative AI-Enabled Process

This deliverable explains how the project will use AI/ML techniques for continuous improvement. It follows an iterative Plan–Do–Check–Act cycle.

## Plan
- Leverage AI tooling to analyze code quality, test coverage, and issue history.
- Generate quarterly roadmaps automatically from repository metrics and backlog trends.

## Do
- Implement tasks from the roadmap during each iteration.
- Instrument the CLI to emit telemetry (opt-in) for usage and errors.

## Check
- Apply machine learning models to detect anomalies in telemetry and to predict areas of high risk or technical debt.
- Update the scorecard metrics to track progress over time.

## Act
- Feed insights back into the next planning cycle, adjusting priorities based on AI-driven recommendations.


include in the health check prompt:

ai-change-log.md --> always updated by codex-task

Prompt Used to Update Deliverables        --> Name of Deliverable File to Update
codex-task-update-ai-change-log.md        --> ai-change-log.md
codex-task-update-architectural-eval.md   --> architectural-eval.md
codex-task-update-architecture.md         --> architecture.md
codex-task-update-health-check.md         --> health-check.md
codex-task-update-roadmap.md              --> roadmap.md


todo:
update codex automation to append codex logs to the task
update codex automation to analyze the logs and provide feedback on the codex task 
add single use context, so we can add prompts that relate to a task??

dotnet test tests/Synthea.Cli.IntegrationTests/Synthea.Cli.IntegrationTests.csproj