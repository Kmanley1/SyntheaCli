# 0005. Passthru token ordering

## Status
Accepted — reordering shipped in `eb95460` (PR #58).

## Context
`synthea run` accepts arbitrary trailing tokens and forwards them to
Synthea unchanged (the passthru argument). The historical
`BuildArgumentList` emission order was:

```
[option-derived flags] + [passthru tokens] + [state] + [city] + [zip]
```

This is unsafe whenever a passthru token is itself a flag that takes
a value. Consider:

```
synthea run --output ./out --state OH -- --some-synthea-flag
```

The historical order produced `... --some-synthea-flag OH` — Synthea,
not knowing that `OH` was meant as the positional state, would
consume it as the value of `--some-synthea-flag`. Subtle, silent, hard
to debug.

Two possible fixes:

1. Require `--` as a separator before any passthru tokens, validate it,
   and reject when missing.
2. Move state/city/zip *before* the passthru tokens so they can't be
   captured as a flag value.

Option 1 is more explicit but breaks every existing invocation that
didn't use `--`. Option 2 is purely structural and has no user-visible
flag change.

## Decision
**Option 2.** Emit in this order:

```
[option-derived flags] + [state] + [city] + [zip] + [passthru tokens]
```

A passthru flag with a value still works — it just can't accidentally
consume the positional codes. A regression test
(`Passthru_DoesNotSwallowPositionalState`) pins the order.

## Consequences
- No user-visible flag change. No CI / script churn.
- Synthea sees positional codes in the same lexical position as before
  (top of arg list before passthru), so all existing invocations
  behave identically.
- Future passthru-related changes have a structural invariant to
  preserve: positional codes precede passthru.
