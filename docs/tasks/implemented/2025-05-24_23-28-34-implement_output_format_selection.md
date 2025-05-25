# Codex Task: Implement Output Format Selection

## 🎯 Objective
Allow enabling/disabling specific Synthea output formats (FHIR, CSV, CCDA, Bulk FHIR, CPCDS).

## ✅ Acceptance Criteria
- [ ] CLI accepts `--format` argument (multiple formats supported).
- [ ] Argument maps to appropriate Synthea configuration (`exporter.*`).
- [ ] Clear error handling for unsupported formats.

## 🛠 Implementation Steps
- Define argument `--format`.
- Validate formats against allowed list.
- Pass configuration parameters to Synthea CLI.

## 🧪 Unit Testing Requirements
- `TestFormatArgument_ValidFormat`
- `TestFormatArgument_InvalidFormat`

## 📌 Definition of Done
- Code committed, unit tests complete, documented fully.