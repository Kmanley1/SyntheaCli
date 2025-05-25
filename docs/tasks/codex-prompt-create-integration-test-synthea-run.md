# Codex Prompt – Create Integration Test for `synthea run`

**Background / Context**

Our Synthea CLI executable exposes:

```bash
synthea run --output ./output --population 1
```

This command should:

1. Exit with code 0.  
2. Create the output directory (`./output`) if it does not exist.  
3. Generate at least one patient record file (FHIR JSON, CSV, or CCDA) inside that directory.

---

## Task / Goal

Write a **cross‑platform integration test** that invokes the command above and verifies the three behaviours listed.

* **Preferred stack** – C# / .NET 8 with xUnit (fits existing solution).  
* The test should run under `dotnet test` in CI and work on Windows, Linux, and macOS agents.

---

## Specific Requirements

1. **Test project**  
   * Create `tests/SyntheaCli.IntegrationTests/SyntheaCli.IntegrationTests.csproj` targeting `net8.0`.  
2. **Test case**  
   * Use `System.Diagnostics.Process` to execute the CLI:  
     ```csharp
     synthea run --output ./output --population 1
     ```  
   * Set `WorkingDirectory` to a temp folder created via `Path.GetTempPath()`.  
   * Capture `StandardOutput` and `StandardError` for debugging.  
3. **Asserts**  
   * Exit code == 0.  
   * Directory `output` exists.  
   * `Directory.GetFiles("output", "*", SearchOption.AllDirectories).Length > 0`.  
4. **Cleanup** – Remove temp folder in `Dispose()` or `finally`.  
5. **CI wiring** – Add test project to `Synthea.Cli.sln` so it runs automatically in pipelines.  
6. **Commit message**  
   ```
   test(integration): verify synthea run generates output for population 1
   ```

---

## Implementation Hints

```csharp
[Fact]
public async Task Synthea_Run_Generates_Output()
{
    var workDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(workDir);

    var psi = new ProcessStartInfo("synthea", "run --output ./output --population 1")
    {
        WorkingDirectory = workDir,
        RedirectStandardOutput = true,
        RedirectStandardError  = true
    };

    var proc = Process.Start(psi);
    await proc.WaitForExitAsync();

    Assert.Equal(0, proc.ExitCode);
    Assert.True(Directory.Exists(Path.Combine(workDir, "output")));
    Assert.NotEmpty(Directory.GetFiles(Path.Combine(workDir, "output"), "*", SearchOption.AllDirectories));
}
```

---

## Deliverables

* New test project files committed.  
* CI passes on all supported OSes.

---

**Additional Instructions**

* Keep external dependencies minimal (only xUnit + FluentAssertions if desired).  
* Use `[Trait("Category","Integration")]` attribute so tests can be filtered.  
* Fail fast with informative messages if `synthea` tool is not on `$PATH`.  
* Provide a `README.md` snippet explaining how to run integration tests locally.

