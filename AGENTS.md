# AGENTS

> **Status:** **Stub**  
> This document is only a placeholder. Full details will be added in later iterations.
> The **CI & Local Verification** section below is real and load-bearing — read it
> before pushing.

## CI & Local Verification

**Run the exact coverage-gated command CI runs — not a lighter local filter — before
you push.**

CI's unit-test step runs with the Coverlet coverage gate enabled
(`-p:CollectCoverage=true … -p:Threshold=80%2C75 -p:ThresholdType=line%2Cbranch`, see
`.github/workflows/ci.yml`). A bare `dotnet test --no-build --filter "Category!=Integration"`
does **not** build, does **not** collect coverage, and can pass locally while CI goes red.

This bit us once (v1.0.x): a `Assert.DoesNotContain("/0", stderr)` assertion passed on
Windows but failed on the Linux runner because an unlucky temp-path GUID (`/tmp/0…`)
contained `/0`. The local `--no-build --filter` run never exercised the gated path that
surfaced it. The fix was to assert on a specific token (`"Generated 1/"`), and the
*process* fix is this rule: reproduce CI locally with the real command —

```bash
dotnet test tests/Synthea.Cli.UnitTests/Synthea.Cli.UnitTests.csproj \
  -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura \
  -p:Threshold=80%2C75 -p:ThresholdType=line%2Cbranch -p:ThresholdStat=total
```

And confirm a workflow's *real* result with `gh run view <id> --json conclusion,jobs`
— `gh run watch --exit-status` has returned 0 on a run whose job failed.

## Purpose  
`AGENTS.md` will eventually catalog every **Codex agent** in this repository—covering each agent’s role, responsibilities, interfaces, and configuration guidelines.

## To-Do (expand in future)  
- [ ] List all current and planned agents  
- [ ] Describe the responsibilities and data flows for each agent  
- [ ] Document configuration options & environment variables  
- [ ] Add usage examples and cross-references to code
