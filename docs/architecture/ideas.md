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
| **Browser ICY demux** | MVP keeps Icecast opt-in ICY (`Icy-MetaData: 1` → VLC; plain MP3 for `<audio>`). Post-MVP: either [icecast-metadata-player](https://www.npmjs.com/package/icecast-metadata-player) (**LGPL-3.0+** — CORS + expose `Icy-MetaInt`; license note vs Plume MPL-2.0) **or** a small in-house icy-metaint strip → MSE. Now-playing already polls BFF — demux mainly for stream-synced titles / always-on ICY. Prefer custom demuxer if avoiding LGPL. Tracking: [plume#11](https://github.com/Bardie-radio/plume/issues/11) |
| **Alpine final image** | Phase 8 ops — shrink ~242MB Ubuntu aspnet image — [META-OPS-002 / plume#13](https://github.com/Bardie-radio/plume/issues/13) (canonical [kithara#33](https://github.com/Bardie-radio/kithara/issues/33)) |

## Promoted

| Item | Where |
|------|-------|
| Razor + Vue widgets + BFF + poll + `/control` vs `/player` | [03-ui-stack.md](03-ui-stack.md) · [mvp/implementation-plan.md](mvp/implementation-plan.md) |
| Plume Phases 1–6 | [mvp/implementation-plan.md](mvp/implementation-plan.md) |

**Related:** [mvp/known-issues.md](mvp/known-issues.md) · [mvp/security-notes.md](mvp/security-notes.md)

**Read next:** [mvp/v0.1-scope.md](mvp/v0.1-scope.md)
