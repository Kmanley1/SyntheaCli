# Codex Task: Automate Implementation and File Movement for Synthea CLI Tasks

---

## Objective
Iterate through a folder with codex tasks

```
\synthea-cli\docs\tasks
```

Evaluate if the task has been completed, then implement each task sequentially, then move the markdown file upon completion to:

```
\synthea-cli\docs\tasks
```

---

## Acceptance Criteria
- [ ] Iterates through each markdown (`*.md`) task file.
- [ ] Implements tasks sequentially.
- [ ] Moves the markdown file after successful completion.
- [ ] Logs clear and informative status messages.
- [ ] Gracefully handles exceptions and errors.
- [ ] Full unit test coverage

---

## Implementation Steps

### Step 1: File Enumeration
- Identify all markdown files (`*.md`) in the source tasks folder.

### Step 2: Task Implementation
- Clearly defined logic or placeholder for implementing tasks.
- Read and understand the task, if you do not understand stop and ask questions.

### Step 3: File Movement
- Upon successful task completion, move files to the target directory.
- Go to next task

---

## Example PowerShell Script To Help You Understand

```powershell
# Define source and target directories
$sourceDir = "C:\_Template\Projects\_code\synthea-cli\docs\tasks"
$targetDir = "C:\_Template\Projects\_codeC\synthea-cli\docs\tasks"

# Iterate through each markdown task file
Get-ChildItem -Path $sourceDir -Filter "*.md" | ForEach-Object {
    Write-Host "Implementing task:" $_.Name

    # TODO: Implement the task logic here.
    # Example: Implement-Task $_.FullName

    # Assuming implementation success for demo purposes
    $implementationSuccessful = $true

    if ($implementationSuccessful) {
        # Move completed task file
        Move-Item $_.FullName -Destination $targetDir
        Write-Host "Task completed and file moved:" $_.Name
    } else {
        Write-Error "Failed to implement task:" $_.Name
    }
}
```

---

## Testing Requirements
- Verify file moves occur only upon successful task completion.
- Confirm error handling for invalid paths or inaccessible files.
- Confirm good unit test coverage
---

## Definition of Done
- [ ] Script checked into source control.
- [ ] Successfully unit tested.
- [ ] Documentation updated.
- [ ] Peer-reviewed and merged PR.