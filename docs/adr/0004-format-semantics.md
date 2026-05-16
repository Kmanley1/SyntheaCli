# 0004. `--format` semantics: exclusive plus `--add-format`

## Status
Accepted — `--add-format` added in `218838b` (PR #59).

## Context
`synthea run --format CSV` had a surprising side effect: it didn't
*add* CSV to Synthea's default exporters; it *exclusively* enabled CSV
and disabled every other format. A user reading the flag name
expected the opposite. Two clean fixes:

1. Flip `--format` to be additive, add a new `--only-format` (or
   `--format-only`) for the exclusive behavior, and document a
   migration window.
2. Keep `--format` exclusive (existing behavior), add a new
   `--add-format` (repeatable) for additive behavior, and document
   loudly that `--format` excludes.

Option 1 is "morally right" but breaks every existing script and CI
pipeline that already learned the exclusive behavior. Option 2 keeps
existing scripts working.

## Decision
**Option 2.** Keep `--format` exclusive. Add `--add-format` as a
repeatable additive switch. README features table and Quick Start
examples document the distinction.

The `BuildArgumentList` emission order matters: exclusive `--format`
emits `--exporter.X.export=true|false` for every known exporter
*first*, then `--add-format` entries emit a follow-up `=true` per
named format. Synthea reads command-line arguments left-to-right; the
later `=true` wins. This means `--format CSV --add-format JSON` does
the natural thing: only CSV and JSON exporters run.

## Consequences
- No breaking change. All existing scripts work as-is.
- The flag pair is asymmetric and needs documentation. The README
  features table calls this out.
- A future redesign could deprecate `--format` and rename it
  `--only-format`; deferred until/unless a real user asks for it.
