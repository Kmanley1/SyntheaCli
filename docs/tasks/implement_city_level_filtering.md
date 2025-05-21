
# Codex Task: Implement City-Level Filtering Argument

This document outlines the task to enhance the `.NET CLI` wrapper around Synthea, allowing users to specify a city for targeted synthetic patient generation, leveraging Synthea's existing `CITY` argument.

---

## 🎯 Objective

Enable users to specify a city within a U.S. state for geographic targeting, creating location-specific synthetic patient data.

---

## ✅ Acceptance Criteria

- [ ] CLI accepts a city name argument via `--city`.
- [ ] Argument maps correctly to Synthea's `CITY` parameter.
- [ ] Argument works only in conjunction with a valid state (`--state`).
- [ ] Graceful handling and clear error messages for missing or invalid city/state combinations.

---

## 🛠 Implementation Steps

### Step 1: Argument Definition
- Add the `--city` option using System.CommandLine in `Program.cs`.

### Step 2: Input Validation
- Validate city input is a non-empty string.
- Ensure the city argument is used in combination with a state argument; throw an error if used alone.

### Step 3: Integration with Synthea
- Pass the validated city parameter to Synthea CLI calls alongside the state parameter.

---

## 🚀 Example Usage

```bash
synthea run --output ./data --state OH --city "Columbus"
synthea run -o ./data --state TX --city "Austin"
```

---

## ⚠️ Expected Error Handling

```bash
# Missing state argument
Error: "--city" requires "--state" to be specified.

# Empty city argument
Error: City name cannot be empty.
```

---

## 🧪 Unit Testing Requirements

Unit tests must verify:

- Correct handling of valid city names with state arguments.
- Error handling when city is provided without state.
- Error handling for empty city name.

### Suggested Unit Test Cases:
- `TestCityArgument_ValidCityWithState`
- `TestCityArgument_MissingState`
- `TestCityArgument_EmptyCity`

Example:

```csharp
[Fact]
public void TestCityArgument_ValidCityWithState()
{
    // Arrange
    var args = new[] { "run", "--state", "OH", "--city", "Columbus" };
    
    // Act
    var result = ArgumentParser.Parse(args);
    
    // Assert
    Assert.True(result.IsValid);
    Assert.Equal("OH", result.State);
    Assert.Equal("Columbus", result.City);
}
```

---

## 📌 Definition of Done

- [ ] Implementation checked into source control.
- [ ] Unit tests passing with coverage ≥ 90%.
- [ ] Documentation updated clearly for this functionality.
- [ ] Peer-reviewed and approved Pull Request (PR).

---

## 📖 References

- [Synthea CLI Documentation](https://github.com/synthetichealth/synthea/wiki)
- [System.CommandLine Documentation](https://github.com/dotnet/command-line-api)

---
