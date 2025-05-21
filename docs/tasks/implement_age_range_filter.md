# Codex Task: Implement Age Range Filter Argument

## 🎯 Objective
Add functionality to filter synthetic patient generation based on specified age ranges.

## ✅ Acceptance Criteria
- [ ] CLI accepts `--age-range` argument (e.g., `30-40`).
- [ ] Validate input format and values.
- [ ] Integration with Synthea CLI.

## 🛠 Implementation Steps
- Define argument `--age-range`.
- Validate format (`min-max`) and numerical validity.
- Pass to Synthea CLI.

## 🧪 Unit Testing Requirements
- `TestAgeRangeArgument_ValidRange`
- `TestAgeRangeArgument_InvalidRange`

## 📌 Definition of Done
- Code committed, tests pass, documentation complete.