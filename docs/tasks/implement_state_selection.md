
# Codex Task: Implement State Selection Argument

This document details the task to enhance the `.NET CLI` wrapper around Synthea to allow specifying a U.S. state for geographic targeting, utilizing Synthea's existing `STATE` argument.

---

## 🎯 Objective

Allow users to specify a U.S. state for patient demographic targeting, enabling state-specific synthetic patient data generation.

---

## ✅ Acceptance Criteria

- [ ] The CLI accepts a state abbreviation argument via `--state`.
- [ ] The argument correctly maps to Synthea's `STATE` parameter.
- [ ] Input validation ensures the argument is a valid U.S. state abbreviation.
- [ ] Clear error messages provided for invalid state inputs.
- [ ] Generated patients correctly reflect demographics from the specified state.

---

## 🛠 Implementation Steps

### Step 1: Argument Definition
- Add a CLI option (`--state`) using System.CommandLine in `Program.cs`.

### Step 2: Input Validation
- Validate that the state input matches a valid U.S. state abbreviation (e.g., "OH", "TX").
- Provide clear error handling for invalid abbreviations.

### Step 3: Integration with Synthea
- Map the argument to Synthea's command invocation (`STATE` parameter).

---

## 🚀 Example Usage

```bash
synthea run --output ./data --state OH
synthea run -o ./data --state TX
```

---

## ⚠️ Expected Error Handling

```bash
# Invalid state input
Error: "XY" is not a valid U.S. state abbreviation.
```

---

## 🧪 Unit Testing Requirements

Unit tests should verify:

- Valid state abbreviations are correctly parsed.
- Invalid state inputs are handled gracefully.
- Generated data aligns with the demographic profile of the provided state.

### Suggested Unit Test Cases:
- `TestStateArgument_ValidAbbreviation`
- `TestStateArgument_InvalidAbbreviation`
- `TestStateArgument_StateSpecificDemographics`

Example:

```csharp
[Fact]
public void TestStateArgument_ValidAbbreviation()
{
    // Arrange
    var args = new[] { "run", "--state", "OH" };
    
    // Act
    var result = ArgumentParser.Parse(args);
    
    // Assert
    Assert.True(result.IsValid);
    Assert.Equal("OH", result.State);
}
```

---

## 📌 Definition of Done

- [ ] Implementation code checked into source control.
- [ ] Unit tests pass with coverage ≥ 90%.
- [ ] Documentation clearly updated to reflect this functionality.
- [ ] Peer-reviewed and approved Pull Request (PR).

---

## 📖 References

- [Synthea CLI Documentation](https://github.com/synthetichealth/synthea/wiki)
- [System.CommandLine Documentation](https://github.com/dotnet/command-line-api)

---
