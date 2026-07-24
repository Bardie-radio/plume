# Implementation plan (Plume v0.1)

Plume-owned delivery order. **Plume Phases 1–6** are the real work packages. **Kithara Phase 7** is the stack umbrella that closes when these exit — do not renumber Plume work as “Kithara 7.1”.

**Scope:** [v0.1-scope.md](v0.1-scope.md) · **Milestones:** [v0.1-milestones.md](v0.1-milestones.md) · **UI:** [../03-ui-stack.md](../03-ui-stack.md)

## Phase map

| Phase | Name | Outcome |
|-------|------|---------|
| **1** | Host baseline | Strip Identity/EF/SQLite/Bootstrap; Razor Pages host; Tailwind (+ Vite); empty `/` |
| **2** | BFF + session | httpOnly cookie; server-side access/refresh store; `/bff/*` → Kithara Bearer; no JWT in browser |
| **3** | Discovery login | Razor renders `form_schema` (Bes); authenticate/refresh via BFF; never branch on provider id |
| **4** | Struna home | `/` list/create; links to **control** + **player** |
| **5** | Control desk | `/control/{slug}` — queue + search + transport (+ compact now-playing); poll; BFF mutations |
| **6** | Player, guest, mesh | `/player/{slug}` prominent now-playing; audio **off by default**; guest exchange; Register + OTel; edge `/control/*` + `/player/*` |

**Plume MVP exit (closes Kithara Phase 7):** human can login → create Struna → search Magpie → play → hear in VLC via `/stream/{slug}`; removing Plume from Compose leaves API + stream + modules working.

Tracking issues:

| Phase | Issue |
|-------|-------|
| 1 | [plume#1](https://github.com/Bardie-radio/plume/issues/1) |
| 2 | [plume#2](https://github.com/Bardie-radio/plume/issues/2) |
| 3 | [plume#3](https://github.com/Bardie-radio/plume/issues/3) |
| 4 | [plume#4](https://github.com/Bardie-radio/plume/issues/4) |
| 5 | [plume#5](https://github.com/Bardie-radio/plume/issues/5) |
| 6 | [plume#6](https://github.com/Bardie-radio/plume/issues/6) |

---

## Phase 1 — Host baseline

**Tracking:** [plume#1](https://github.com/Bardie-radio/plume/issues/1)

**Why:** The ASP.NET Identity + SQLite + Bootstrap template is not product design. Start from a thin Razor host.

### Work

1. Remove Identity, EF Core Identity stores, SQLite, Bootstrap scaffolding.
2. Razor Pages host with empty `/`.
3. Wire Tailwind (+ Vite) for later islands; no product widgets yet.

### Exit criteria

- App boots without Identity DB.
- `/` returns a minimal Razor page.
- Tailwind/Vite pipeline builds assets the host can serve.

### Cross-repo

| Repo | Follow-up |
|------|-----------|
| **plume** | Scaffold teardown only |
| **org** | None |

---

## Phase 2 — BFF + session

**Tracking:** [plume#2](https://github.com/Bardie-radio/plume/issues/2)

**Why:** Browser must never hold Kithara JWTs. Plume is the session boundary.

### Work

1. httpOnly session cookie (Secure / SameSite per deploy).
2. Server-side access + refresh store keyed by session.
3. `/bff/*` (or equivalent) proxies authenticated REST to Kithara with Bearer.
4. Refresh handling without exposing tokens to islands.

### Exit criteria

- No access/refresh JWT readable from browser JS.
- BFF can call a protected Kithara probe (`GET /api/auth/me` via `/bff/auth/me`) with stored tokens.

### Cross-repo

| Repo | Follow-up |
|------|-----------|
| **kithara** | Auth + rest-api contracts unchanged |
| **plume** | [security-notes.md](security-notes.md) |

---

## Phase 3 — Discovery login

**Tracking:** [plume#3](https://github.com/Bardie-radio/plume/issues/3)

**Why:** Plume must not special-case Bes; discovery `ui` oneof drives the form.

### Work

1. BFF `GET` discovery → Razor renders `form_schema` fields (or starts `redirect`).
2. Authenticate / refresh via BFF; store tokens server-side.
3. Never `if (provider.id == "bes")`.

### Exit criteria

- Operator can log in with Bes through Plume.
- Failed auth shows a usable error without leaking tokens.

### Cross-repo

| Repo | Follow-up |
|------|-----------|
| **kithara** | [auth](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/auth.md) |
| **bes** | form_schema fields stable |

---

## Phase 4 — Struna home

**Tracking:** [plume#4](https://github.com/Bardie-radio/plume/issues/4)

**Why:** Need a place to create/list Strunas and jump into desk or player.

### Work

1. `/` lists Strunas the principal may control / listen (via BFF → Kithara lists).
2. Create Struna (access modes; **no** encode-mode toggle).
3. Links to `/control/{slug}` and `/player/{slug}`.

### Exit criteria

- Authenticated user creates a Struna and opens both surfaces’ URLs.

### Cross-repo

| Repo | Follow-up |
|------|-----------|
| **kithara** | rest-api create / listen / control lists |
| **org** | Edge `/` → Plume |

---

## Phase 5 — Control desk

**Tracking:** [plume#5](https://github.com/Bardie-radio/plume/issues/5)

**Why:** DJ workbench is the primary control UX — not the listen page.

### Work

1. `/control/{slug}` page composing queue, search, transport, compact now-playing.
2. Poll now-playing / queue on an MVP interval.
3. Mutations (play, queue, skip, …) through BFF.

### Exit criteria

- User can search Magpie, queue/play, skip; now-playing updates via poll.
- Guest path deferred to Phase 6 if needed for private control demos — protected control UX lands with Phase 6.

### Cross-repo

| Repo | Follow-up |
|------|-----------|
| **kithara** | [playback-control](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/domains/playback-control.md) |
| **org** | Edge `/control/*` → Plume |

---

## Phase 6 — Player, guest, mesh

**Tracking:** [plume#6](https://github.com/Bardie-radio/plume/issues/6)

**Why:** Listen surface + guest + Register close the optional-client story for Kithara Phase 7.

### Work

1. `/player/{slug}` prominent now-playing; audio widget **off by default**; opt-in `/stream/{slug}`.
2. Guest code entry → exchange → guest session in BFF store.
3. Client module Register + OTel `bardie.plume`.
4. Confirm edge `/control/*` + `/player/*` in Compose narrative.

### Exit criteria

- Full happy path: login → create → search → play → hear in VLC.
- Guest can control a protected Struna after exchange.
- Removing Plume from Compose leaves API + stream + modules working.

### Cross-repo

| Repo | Follow-up |
|------|-----------|
| **kithara** | Phase 7 exit · [uri-routing](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/uri-routing.md) |
| **org** | [05-deployment](https://github.com/Bardie-radio/.github/blob/main/profile/docs/architecture/05-deployment.md) · [06-client-modules](https://github.com/Bardie-radio/.github/blob/main/profile/docs/architecture/06-client-modules.md) |

---

## Post-MVP (not Plume Phase 7+)

Park in [ideas.md](../ideas.md) / GitHub — do **not** extend this MVP plan with Phase 7+:

- Browser → Kithara event channel (SSE/WS); auth ticket compatible with BFF — [kithara#28](https://github.com/Bardie-radio/kithara/issues/28)
- Argus redirect login UX
- PWA / themes
- Vite build in CI (when application code lands)

**Related:** Kithara [Phase 7](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/mvp/implementation-plan.md#phase-7--plume-mvp-optional-client) · [known-issues.md](known-issues.md) · [security-notes.md](security-notes.md)

**Read next:** [v0.1-milestones.md](v0.1-milestones.md)
