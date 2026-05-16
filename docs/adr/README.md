# Architecture Decision Records

This directory captures the major design decisions in `Synthea.Cli`.
Each ADR is a short markdown file in the Michael Nygard template
(Context / Decision / Status / Consequences). New decisions go in a
numbered file; superseded ADRs stay in place with their status updated.

## Index

| # | Title | Status | Finding |
|---|-------|--------|---------|
| 0001 | [Static seams and their removal](0001-static-seams-and-removal.md) | Accepted | A-12 |
| 0002 | [JAR caching strategy](0002-jar-caching-strategy.md) | Accepted | A-14 |
| 0003 | [System.CommandLine GA migration](0003-system-commandline-ga.md) | Accepted | A-15 |
| 0004 | [`--format` semantics](0004-format-semantics.md) | Accepted | A-8 |
| 0005 | [Passthru token ordering](0005-passthru-token-ordering.md) | Accepted | A-9 |

## Template

```markdown
# NNNN. Short title

## Status
Accepted | Proposed | Superseded by NNNN

## Context
What forces are at play? What problem is the decision addressing?

## Decision
What did we decide?

## Consequences
What gets easier? What gets harder? What does this lock in?
```
