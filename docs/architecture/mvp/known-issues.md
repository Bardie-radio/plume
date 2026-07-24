# Known issues (Plume)

Non-security footguns and doc debt. Security / mesh / guest token semantics stay in Kithara [security-audit](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/mvp/security-audit.md) — do not fork `SEC-*` here. BFF notes: [security-notes.md](security-notes.md).

| ID | Status | Summary |
|----|--------|---------|
| **PLUME-SCAF-001** | Open | Identity + EF + SQLite + Bootstrap scaffold contradicts thin-client / BFF design — teardown is [plume#1](https://github.com/Bardie-radio/plume/issues/1) (Phase 1) |
| **PLUME-DOC-001** | Mitigated in docs | Older scope mentioned encode-mode on create — removed from [v0.1-scope](v0.1-scope.md); do not reintroduce in UI |
| **PLUME-ORG-001** | Mitigated in docs | Org `bardie-*` repo URLs fixed; [06-client-modules](https://github.com/Bardie-radio/.github/blob/main/profile/docs/architecture/06-client-modules.md) exists |
| **PLUME-POLL-001** | Accepted MVP | Poll lag / multi-controller staleness — accepted until [kithara#28](https://github.com/Bardie-radio/kithara/issues/28) (not a Plume Phase) |
| **PLUME-PATH-001** | Mitigated in docs | `/control` desk + `/player` listen remapped across Plume / kithara / org |

GitHub tracking issues for Phases 1–6 are linked from [implementation-plan.md](implementation-plan.md).

**Related:** [ideas.md](../ideas.md) · [security-notes.md](security-notes.md) · Kithara [known-issues](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/mvp/known-issues.md)

**Read next:** [security-notes.md](security-notes.md)
