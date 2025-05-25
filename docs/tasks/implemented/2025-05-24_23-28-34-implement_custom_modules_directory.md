# Codex Task: Implement Custom Modules Directory Argument

## 🎯 Objective
Allow specification of a directory containing custom JSON-defined clinical modules.

## ✅ Acceptance Criteria
- [ ] CLI accepts `--module-dir` argument.
- [ ] Directory existence validation.
- [ ] Pass directory to Synthea.

## 🛠 Implementation Steps
- Define `--module-dir` argument.
- Validate directory path.
- Integrate with Synthea CLI.

## 🧪 Unit Testing Requirements
- `TestModuleDirArgument_ValidPath`
- `TestModuleDirArgument_InvalidPath`

## 📌 Definition of Done
- Integrated, tested, and documented.