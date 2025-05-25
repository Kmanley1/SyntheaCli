# Codex Task: Implement Population Size Argument

## 🎯 Objective
Enable specifying the number of synthetic patients to generate.

## ✅ Acceptance Criteria
- [ ] CLI accepts `--population` (`-p`) argument.
- [ ] Argument correctly maps to Synthea's `-p` parameter.
- [ ] Validate input as positive integer.
- [ ] Clear error messages for invalid inputs.

## 🛠 Implementation Steps
- Define `--population | -p` argument.
- Validate positive integer.
- Integrate argument into Synthea invocation.

## 🧪 Unit Testing Requirements
- `TestPopulationArgument_ValidInput`
- `TestPopulationArgument_InvalidInput`

## 📌 Definition of Done
- Code committed, unit tests passing, documentation updated.