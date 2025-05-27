# Directory tree for `C:\_Template\Projects\_code\synthea-cli`

synthea-cli/
.github/
└─ workflows/
│   ├─ ci.yml
│   └─ nuget.yml
.vscode/
├─ extensions.json
├─ launch.json
├─ settings.json
└─ tasks.json
build/
└─ stub.md
docs/
├─ deliverables/
│   ├─ _my-notes.md
│   ├─ action-plan-with-timelines.md
│   ├─ ai-change-log.md
│   ├─ architectural-eval.md
│   ├─ Architecture.md
│   ├─ codex-automation.md
│   ├─ directory-tree.md
│   ├─ health-check.md
│   ├─ iterative-ai-enabled-process.md
│   ├─ on-going-scorecard-metrics.md
│   ├─ project-structure-recommendations.md
│   ├─ project-structure.md
│   ├─ prompt-generator.md
│   ├─ roadmap.md
│   └─ vs-solution-organization-guidelines.md
├─ prompts/
│   ├─ codex-task-template-ai-change-log.md
│   ├─ codex-task-template-prompt.md
│   ├─ codex-task-template-run-codex-automation.md
│   ├─ codex-task-template-smoke-test-task.md
│   ├─ codex-task-template-solution-critique.md
│   ├─ codex-task-update-architectural-eval.md
│   ├─ codex-task-update-architecture.md
│   ├─ codex-task-update-health-check.md
│   └─ codex-task-update-roadmap.md
├─ reference/
│   ├─ Azure-well-architected-framework.md
│   └─ synthea-commands.md
├─ research/
│   ├─ AI_Code_Health_Playbook.md
│   ├─ Comprehensive Codex Prompt Template and Best Practices.pdf
│   ├─ NET Core CLI GitHub Repository Best Practices.pdf
│   ├─ Source_Code_Documentation_Playbook.md
│   ├─ Synthea - An approach, method, and software mechanism for generating synthetic patients and the synthetic electronic health care record.pdf
│   └─ Using Synthea and Synthetic Data Generators in a .NET CLI for HL7 v2, FHIR, and CCDA.pdf
└─ tasks/
│   ├─ context/
│   │   └─ post/
│   │   │   ├─ 0010-codex-task-update-repo-docs.md
│   │   │   └─ 0020-codex-task-update-ai-change-log.md
│   ├─ implemented/
│   │   ├─ 2025-05-24_23-28-34-architecture_review_task.md
│   │   ├─ 2025-05-24_23-28-34-automate_task_implementation.md
│   │   ├─ 2025-05-24_23-28-34-implement_age_range_filter.md
│   │   ├─ 2025-05-24_23-28-34-implement_city_level_filtering.md
│   │   ├─ 2025-05-24_23-28-34-implement_custom_modules_directory.md
│   │   ├─ 2025-05-24_23-28-34-implement_disease_module_selection.md
│   │   ├─ 2025-05-24_23-28-34-implement_gender_filter.md
│   │   ├─ 2025-05-24_23-28-34-implement_output_directory_customization.md
│   │   ├─ 2025-05-24_23-28-34-implement_output_format_selection.md
│   │   ├─ 2025-05-24_23-28-34-implement_population_size.md
│   │   ├─ 2025-05-24_23-28-34-implement_random_seed_control.md
│   │   ├─ 2025-05-24_23-28-34-implement_snapshot_management_time_advancement.md
│   │   ├─ 2025-05-24_23-28-34-implement_state_selection.md
│   │   ├─ 2025-05-25_00-50-27-codex-prompt-update-automation-context.md
│   │   ├─ 2025-05-25_01-02-55-codex-task-update-ai-change-log.md
│   │   ├─ 2025-05-25_01-18-40-codex-prompt-update-automation-pre-post-context.md
│   │   ├─ 2025-05-25_01-27-47-codex-prompt-update-automation-context-path-change.md
│   │   ├─ 2025-05-25_03-03-58-codex-prompt-sync-automation-doc.md
│   │   ├─ 2025-05-25_03-21-14-codex-prompt-update-automation-prepend-timestamp.md
│   │   ├─ 2025-05-25_03-21-19-codex-prompt-troubleshoot-automation-not-moving-task.md
│   │   ├─ 2025-05-25_03-48-56-codex-prompt-create-integration-test-synthea-run.md
│   │   └─ 2025-05-25_04-17-53-codex-prompt-create-integration-test-synthea-cli-wrapper-run.md
│   ├─ codex-prompt-generate-directory-report.md
│   ├─ codex-task-template-smoke-test-task-one.md
│   └─ codex-task-template-smoke-test-task-two.md
src/
└─ Synthea.Cli/
│   ├─ CodexTaskProcessor.cs
│   ├─ JarManager.cs
│   ├─ Program.cs
│   └─ Synthea.Cli.csproj
TestResults/
└─ test-results.trx
tests/
├─ Synthea.Cli.IntegrationTests/
│   ├─ TestResults/
│   │   └─ TestResults.trx
│   ├─ ScaffoldingSmokeTest.cs
│   ├─ SkipTestException.cs
│   ├─ Synthea.Cli.IntegrationTests.csproj
│   ├─ SyntheaCliWrapperRunTests.cs
│   └─ SyntheaRunTests.cs
└─ Synthea.Cli.UnitTests/
│   ├─ CliTests.cs
│   ├─ CodexTaskProcessorTests.cs
│   ├─ JarManagerTests.cs
│   ├─ ProgramHandlerTests.cs
│   ├─ ProgramRefactorTests.cs
│   └─ Synthea.Cli.UnitTests.csproj
tools/
├─ windows/
│   ├─ temp/
│   │   └─ rename-implemented.ps1
│   ├─ install-vscode-extensions.ps1
│   ├─ nuget-helper.ps1
│   ├─ synthea-cli-create.ps1
│   └─ Write-DirectoryTreeMarkdown.ps1
└─ setup.sh
.gitattributes
.gitignore
AGENTS.md
CHANGELOG.md
CODEOWNERS
CONTRIBUTING.md
Directory.Build.props
LICENSE
README.md
scorecard.json
synthea-cli.code-workspace
Synthea.Cli.sln
