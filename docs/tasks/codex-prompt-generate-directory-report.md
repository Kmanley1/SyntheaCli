
# Codex Prompt – Generate-DirectoryReport.ps1

**Background/Context:**  
We need an automation utility, **Generate-DirectoryReport.ps1**, that accepts a root directory path, walks its entire hierarchy, and emits two companion artifacts:  

1. **directory-structure.md** — a human‑readable Markdown tree that lists every folder and file.  
2. **directory-structure.json** — a machine‑readable JSON representation of the same hierarchy (objects with `name`, `type`, optional `children`).  

Both files must be written to a `deliverables` sub‑folder in the repository root; create the folder if it doesn’t exist. The script must run on PowerShell 7.2+ and use only built‑in cmdlets (e.g., `Get-ChildItem`, `ConvertTo-Json`). Provide descriptive inline comments and a usage example.

```powershell
.\Generate-DirectoryReport.ps1 -RootPath "C:\Projects\MyRepo"
```

**Task/Goal:**  
Write the complete PowerShell script **Generate-DirectoryReport.ps1** that performs the duties above.

**Specific Requirements:**  
- Accept a single parameter `-RootPath` (default: current directory).  
- Validate the path exists and is a directory; throw a clear error otherwise.  
- Traverse recursively while skipping hidden/system items unless `-IncludeHidden` switch is used.  
- Build the Markdown tree with indentation based on depth (use 2‑space indents) and back‑tick escape any special Markdown characters in names.  
- Build the JSON structure with proper depth nesting.  
- Ensure UTF‑8 encoding for both output files.  
- Commit the file in the `deliverables` folder.  
- Return the full paths of the generated files as the script’s output object for easy piping.  
- Follow this coding style: PascalCase function names, Cmdlet‑naming convention, comment‑based help at top.

**Examples (for reference):**

_Input directory sample_

```
MyRepo/
 ├─ src/
 │  └─ app.ps1
 ├─ README.md
 └─ .gitignore
```

_Expected snippet in `directory-structure.md`_

```
- MyRepo
  - src
    - app.ps1
  - README.md
  - .gitignore
```

_Expected JSON fragment_

```json
{
  "name": "MyRepo",
  "type": "folder",
  "children": [
    {
      "name": "src",
      "type": "folder",
      "children": [
        { "name": "app.ps1", "type": "file" }
      ]
    },
    { "name": "README.md", "type": "file" },
    { "name": ".gitignore", "type": "file" }
  ]
}
```

**Output Format:**  
A PowerShell file checked into git in the deliverables folder.

**Additional Instructions:**  
- Include comment‑based help with synopsis, parameter description, and examples.  
- Add error handling for file I/O operations.  
- Do **not** include any explanation outside the code block.
