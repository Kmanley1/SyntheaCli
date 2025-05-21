
# Codex Task: Implement Random Seed Control (`-s` argument)

This document outlines the task to enhance the `.NET CLI` wrapper around Synthea by allowing users to specify a random seed for deterministic synthetic patient generation. This leverages Synthea's existing `-s` CLI argument.

---

## 🎯 Objective

Allow users to provide a random seed via the CLI, enabling repeatable and deterministic patient data generation across multiple runs.

---

## ✅ Acceptance Criteria

- [ ] The CLI accepts a numeric argument via `-s` or `--seed`.
- [ ] The argument is correctly passed to Synthea’s `-s` parameter.
- [ ] Input validation ensures the argument provided is a valid integer.
- [ ] Clear, informative error messages are displayed for invalid inputs.
- [ ] Using the same seed consistently produces identical patient data.

---

## 🛠 Implementation Steps

### Step 1: Argument Definition
- Add a new argument option (`-s | --seed`) using System.CommandLine in `Program.cs`.

### Step 2: Input Validation
- Implement validation logic to confirm the seed is an integer.
- Gracefully handle invalid inputs, providing clear feedback to the user.

### Step 3: Integration with Synthea
- Pass the validated seed to the underlying Java Synthea call using the `-s` flag.

---

## 🚀 Example Usage

```bash
# Generate deterministic synthetic patients
synthea run --output ./data --seed 12345

# Short-form argument usage
synthea run -o ./data -s 98765
```

---

## ⚠️ Expected Error Handling

```bash
# Example error for non-integer input
Error: Random seed must be an integer.
```

---

## 🧪 Unit Testing Requirements

Unit tests must verify the following:

- Proper parsing and handling of valid integer seeds.
- Graceful handling of non-integer inputs.
- Deterministic generation: ensuring the same seed consistently yields identical output.

### Suggested Unit Test Cases:
- `TestSeedArgument_ValidInteger`
- `TestSeedArgument_InvalidNonInteger`
- `TestSeedArgument_DeterministicOutput`

Example:

```csharp
[Fact]
public void TestSeedArgument_ValidInteger()
{
    // Arrange
    var args = new[] { "run", "-s", "12345" };
    
    // Act
    var result = ArgumentParser.Parse(args);
    
    // Assert
    Assert.True(result.IsValid);
    Assert.Equal(12345, result.Seed);
}
```

---

## 📌 Definition of Done

- [ ] Feature implementation code checked into source control.
- [ ] Unit tests passing with code coverage ≥ 90%.
- [ ] Updated documentation clearly describing the new seed functionality.
- [ ] Peer-reviewed and approved Pull Request (PR).

---

## 📖 References

- [Synthea CLI Documentation](https://github.com/synthetichealth/synthea/wiki)
- [System.CommandLine Documentation](https://github.com/dotnet/command-line-api)

---
