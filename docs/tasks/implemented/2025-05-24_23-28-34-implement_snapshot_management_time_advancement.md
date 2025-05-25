# Codex Task: Implement Snapshot Management and Time Advancement

## 🎯 Objective
Support initial snapshot loading (`-i`), updated snapshot saving (`-u`), and time advancement (`-t`).

## ✅ Acceptance Criteria
- [ ] CLI accepts arguments for initial snapshot (`--initial-snapshot`).
- [ ] CLI accepts arguments for updated snapshot (`--updated-snapshot`).
- [ ] CLI accepts argument for time advancement in days (`--days-forward`).
- [ ] Proper validation and integration with Synthea.

## 🛠 Implementation Steps
- Define arguments for snapshots and days forward.
- Validate files and numerical inputs.
- Integrate these parameters with Synthea CLI.

## 🧪 Unit Testing Requirements
- `TestSnapshotArguments_ValidInput`
- `TestSnapshotArguments_InvalidInput`

## 📌 Definition of Done
- Code checked in, thoroughly tested, documentation complete.