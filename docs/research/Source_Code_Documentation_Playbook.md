# Source‑Code Repository Documentation Playbook

## 1. Core principles

1. **Docs‑as‑Code** – every artifact lives in the repo, versioned, review‑gated and testable just like source.  
2. **Single Source of Truth** – the `README.md` links to everything; no stale wiki copies.  
3. **Architecture is executable** – C4, PlantUML or Mermaid specs generate diagrams automatically in CI.  
4. **Decisions are immutable** – Architecture Decision Records (ADRs) log *why* a design exists.  
5. **AI augments, not replaces, human reviewers** – LLM agents propose docs; maintainers approve.  

---

## 2. Repository documentation set

| File / Folder | Purpose | Update trigger | AI‑assist pattern |
|---------------|---------|----------------|-------------------|
| **README.md** | Elevator pitch, quick‑start, badges | Feature or CI change | LLM summarises latest diff for *Usage* section |
| **CONTRIBUTING.md** | Dev env, branch model, PR checklist | Tooling / process change | Derive steps from `.devcontainer.json` and CI workflow YAML |
| **CODE_OF_CONDUCT.md** | Community rules (CNCF template) | Rare | Static |
| **LICENSE** | Legal | Rare | Static |
| **docs/ARCHITECTURE.md** | C4 level‑1+2 diagrams, context, data flows | New subsystem / refactor | AI converts updated PlantUML/Mermaid to PNG + refreshes MD |
| **docs/adr/**`NNNN-title.md` | Architecture Decision Records | Significant decision | `adr-bot` GPT drafts ADR from PR title & diff |
| **docs/API/** | OpenAPI / GraphQL schema, protobufs | Schema change | AI explains spec diff |
| **BUILD.md** | Local build & CI matrix | Build‑tool version bump | LLM reads `Dockerfile` & workflow YAML, rewrites examples |
| **DEPLOYMENT.md / ops/** | Runbooks, IaC diagrams | Infra change | GPT summarises diff of IaC files |
| **TESTING.md** | Test strategy, coverage badge | Test harness change | AI inserts new smoke‑test names & purpose |
| **CHANGELOG.md** | Human‑readable history (Keep‑a‑Changelog) | Every release | Conv‑commit bot + GPT turns commit messages into prose |
| **SECURITY.md** | Vulnerability report process | Policy change | Static |
| **docs/SBOM/** | CycloneDX JSON + HTML report | Each CI run | Syft→Grype; AI surfaces new CVEs in PR comment |
| **docs/metrics/** | Quality / security KPI snapshots | Nightly | Export Sonar API; GPT writes trend commentary |

---

## 3. Automation blueprint

```text
PR opened / push
   ┌──────────────┐
   │ GitHub Action│
   └──────────────┘
       ├─ static analysis (SARIF)
       ├─ run tests / build
       ├─ gen docs (OpenAPI, SBOM, PlantUML)
       ├─ ai-doc-bot (LLM container)
       │     • scans diff & modified docs
       │     • generates / updates Markdown
       │     • drafts ADR if tag “adr:” present
       │     • adds PR comment with preview
       └─ doc lint (markdown-lint, adr-tools verify)
```

### Key components

* **ai-doc-bot** – GitHub Copilot Agent, Graphite AI Docs plugin, *code‑narrator*  
* **ADR tooling** – `adr-tools`, `adr-log`, `adr-ci`  
* **Diagram generation** – Structurizr CLI, PlantUML Action, Mermaid‑CLI  
* **Doc lint** – `markdownlint-cli2`, `adr-lint`, `readme-lint`  

---

## 4. Quality gates & metrics

| Gate | Fail condition |
|------|----------------|
| **Doc coverage** | `README`, `ARCHITECTURE.md`, and ≥1 ADR missing |
| **ADR freshness** | ADR timestamp > 90 days *and* > 300 LOC changed without new ADR |
| **Diagram drift** | Stored diagram hash ≠ regenerated PNG hash |
| **Changelog completeness** | Release branch has commits without conventional prefix |

---

## 5. Adoption roadmap

| Phase | ≤ 30 days | 30‑90 days | Continuous |
|-------|-----------|-----------|------------|
| **People** | Doc style guide | Train devs via “write the first ADR” workshop | Rotate “Doc Champ” role each sprint |
| **Process** | Add doc files to PR template | Enable `ai-doc-bot` on main repo | Quarterly doc‑health OKR |
| **Tech** | Lint README/CONTRIBUTING in CI | Integrate ADR bot + diagram CI | Auto‑publish docs site (MkDocs + GitHub Pages) |

---

## Take‑away

Treat documentation as **executable artifacts owned by the same pipelines** that build and test your code.  
LLM agents excel at drafting or updating Markdown, explaining schema changes, and templating ADRs – **but final approval must remain part of code review** to keep docs correct and trusted.
