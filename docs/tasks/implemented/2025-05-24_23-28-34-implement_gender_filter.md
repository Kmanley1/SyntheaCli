# Codex Task: Implement Gender Filter Argument

## 🎯 Objective
Enhance the `.NET CLI` wrapper around Synthea to allow filtering synthetic patient data generation by gender.

## ✅ Acceptance Criteria
- [ ] CLI accepts `--gender` argument (`M` or `F`).
- [ ] Argument correctly maps to Synthea's gender filter.
- [ ] Proper input validation and error messaging.

## 🛠 Implementation Steps
- Define the `--gender` argument.
- Validate gender is either `M` or `F`.
- Integrate with Synthea CLI call.

## 🧪 Unit Testing Requirements
- `TestGenderArgument_ValidInput`
- `TestGenderArgument_InvalidInput`

## 📌 Definition of Done
- Implementation in source control, unit tests ≥ 90% coverage, documentation updated.