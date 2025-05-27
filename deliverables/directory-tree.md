# Directory tree for `C:\_Template\Projects\_code\synthea-cli`

synthea-cli/
.github/
└─ workflows/
│   ├─ ci.yml
│   └─ nuget.yml
deliverables/
└─ Generate-DirectoryReport.ps1
docs/
├─ deliverables/
│   ├─ _my-notes.md
│   ├─ action-plan-with-timelines.md
│   ├─ ai-change-log.md
│   ├─ architectural-eval.md
│   ├─ Architecture.md    # CLI flow diagrams & overview
│   ├─ codex-automation.md
│   ├─ health-check.md
│   ├─ iterative-ai-enabled-process.md
│   ├─ on-going-scorecard-metrics.md
│   ├─ prompt-generator.md
│   └─ roadmap.md
├─ prompts/
│   ├─ codex-task-template-ai-change-log.md
│   ├─ codex-task-template-prompt.md
│   ├─ codex-task-template-run-codex-automation.md
│   ├─ codex-task-template-smoke-test-task.md
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
nupkgs/
└─ synthea-cli.0.1.0.nupkg
run/
└─ setup.sh    # thin wrapper for Codex harness
scripts/
├─ windows/
│   ├─ temp/
│   │   └─ rename-implemented.ps1
│   ├─ install-vscode-extensions.ps1
│   ├─ nuget-helper.ps1
│   └─ Write-DirectoryTreeMarkdown.ps1
└─ synthea-cli-create.ps1    # helper to scaffold new CLI repo
Synthea.Cli/
├─ CodexTaskProcessor.cs
├─ JarManager.cs    # JAR download & cache helper
├─ Program.cs    # System.CommandLine entry point
└─ Synthea.Cli.csproj
tests/
├─ Synthea.Cli.IntegrationTests/
│   ├─ TestResults/
│   │   └─ TestResults.trx
│   ├─ ScaffoldingSmokeTest.cs
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
.gitattributes    # enforce LF for shell scripts
.gitignore
CHANGELOG.md
Directory.Build.props
LICENSE
README.md
scorecard.json
setup.sh    # thin wrapper for Codex harness
synthea-cli.code-workspace    # VS Code workspace file
Synthea.Cli.sln    # Visual Studio solution
