# Ideas inbox

Parking lot for Plume notes. Promote into [mvp/implementation-plan.md](mvp/implementation-plan.md) or Kithara indexes when locked.

## Backlog

| Idea | Notes |
|------|-------|
| **Browser → Kithara events** | SSE/WS for now-playing/queue; auth ticket compatible with BFF; **no** Plume push proxy — [kithara#28](https://github.com/Bardie-radio/kithara/issues/28) · [playback-control](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/domains/playback-control.md) |
| **Argus redirect login** | Discovery `redirect` UI path; Plume starts IdP dance without provider-id branches |
| **PWA / themes** | Post-MVP installability and theming |
| **Richer search UX** | Filters, source badges, keyboard nav — stay on shared search widget |
| **Encode-mode UI** | Only if product revisits encode-alive create toggles; currently out of Plume scope |
| **Vite build in CI** | Add when application islands land — not part of this docs pass |

## Promoted

| Item | Where |
|------|-------|
| Razor + Vue widgets + BFF + poll + `/control` vs `/player` | [03-ui-stack.md](03-ui-stack.md) · [mvp/implementation-plan.md](mvp/implementation-plan.md) |
| Plume Phases 1–6 | [mvp/implementation-plan.md](mvp/implementation-plan.md) |

**Related:** [mvp/known-issues.md](mvp/known-issues.md) · [mvp/security-notes.md](mvp/security-notes.md)

**Read next:** [mvp/v0.1-scope.md](mvp/v0.1-scope.md)
