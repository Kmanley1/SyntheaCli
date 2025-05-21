# Codex Task: Implement Output Directory Customization Argument

## 🎯 Objective
Allow users to specify a custom output directory for generated synthetic data.

## ✅ Acceptance Criteria
- [ ] CLI accepts `--output` (`-o`) argument.
- [ ] Validate directory existence or create if missing.
- [ ] Pass output directory to Synthea.

## 🛠 Implementation Steps
- Define argument `--output | -o`.
- Implement directory validation and creation logic.
- Integrate with Synthea CLI call.

## 🧪 Unit Testing Requirements
- `TestOutputDirectory_ValidPath`
- `TestOutputDirectory_InvalidPath`

## 📌 Definition of Done
- Fully implemented, documented, and tested.