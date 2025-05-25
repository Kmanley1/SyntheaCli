# Codex Prompt – Integration Test Using **Synthea CLI Wrapper**

**Background / Context**

The repository’s .NET wrapper project **`Synthea.Cli`** should correctly invoke the Java Synthea generator.  
We need an integration test that exercises the real wrapper binary (or global tool) with:

```bash
synthea run --output ./output --population 1
```

---

## Task / Goal

Write a cross‑platform integration test that:

1. Builds or locates the `Synthea.Cli` wrapper.  
2. Runs the command above through the wrapper.  
3. Asserts exit code 0, output directory creation, and presence of at least one patient record.

---

## Specific Requirements

| Section | Requirement |
|---------|-------------|
| **Framework** | .NET 8 + xUnit in `tests/SyntheaCli.IntegrationTests/`. |
| **Execution Strategy** | Prefer `dotnet <published‑dll> run …`; fallback to global tool `synthea …` if DLL not present. |
| **Assertions** | Exit code, directory exists, `GetFiles().Any()` > 0. |
| **Cleanup** | Delete temp working directory in `finally`. |
| **CI Integration** | Add project to solution so GitHub Actions/Azure Pipelines run it. |
| **Trait** | `[Trait("Category","Integration")]` to allow filtering. |

---

### Sample Skeleton

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task Synthea_CLI_Wrapper_Generates_Output()
{
    string temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(temp);

    // Attempt to run via published DLL first
    string dllPath = Path.GetFullPath(Path.Combine("..","..","..","..","Synthea.Cli","bin","Release","net8.0","Synthea.Cli.dll"));
    string command = File.Exists(dllPath)
        ? $"dotnet \"{dllPath}\" run --output ./output --population 1"
        : "synthea run --output ./output --population 1";

    var psi = OperatingSystem.IsWindows()
        ? new ProcessStartInfo("cmd.exe", $"/c {command}")
        : new ProcessStartInfo("bash", $"-c \"{command}\"");

    psi.WorkingDirectory      = temp;
    psi.RedirectStandardError = psi.RedirectStandardOutput = true;

    using var proc = Process.Start(psi)!;
    await proc.WaitForExitAsync();

    Assert.Equal(0, proc.ExitCode);

    var outDir = Path.Combine(temp, "output");
    Assert.True(Directory.Exists(outDir));
    Assert.NotEmpty(Directory.GetFiles(outDir, "*", SearchOption.AllDirectories));

    Directory.Delete(temp, true); // cleanup
}
```

Codex should flesh out platform specifics and add helper utilities.

---

## Commit Message

```
test(integration): verify Synthea CLI wrapper produces output for population=1
```

---

## Additional Instructions

* Skip test with `[SkippableFact]` if Java or Synthea assets missing to avoid false negatives in lightweight CI images.  
* Keep external packages minimal.  
* Add a short “Running Integration Tests” section to `README.md`.

