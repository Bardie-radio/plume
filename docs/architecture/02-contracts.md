# Contracts

Plume is a **REST client of Kithara via BFF**. Path map: [uri-routing](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/uri-routing.md). API sketch: [rest-api](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/rest-api.md). Auth: [auth](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/auth.md). UI composition: [03-ui-stack.md](03-ui-stack.md).

## Edge routes (Plume)

| Path | Role |
|------|------|
| `/` | List/create Strunas (auth required when Plume is used) |
| `/control/{slug}` | Remote control desk — queue, search, transport |
| `/player/{slug}` | Listen / player surface — prominent now-playing; anonymous for **public** / **hidden**; audio off by default |

Browser hits **Plume** for UI. `/api/*` and `/stream/*` stay on **Kithara** at the edge — Plume’s BFF calls `/api` server-side with Bearer tokens. There is **no** `/listen` UI path.

## BFF vs edge `/api`

| Surface | Who calls | Credential |
|---------|-----------|------------|
| Browser → Plume pages / `/bff/*` | Browser | httpOnly session cookie |
| Plume → Kithara `/api/*` | Plume process | `Authorization: Bearer <access JWT>` |
| Browser → Kithara `/stream/{slug}` | Optional player / VLC | Listen token query when protected |

Never put access/refresh JWTs in `localStorage` or island JS.

## Auth flows

1. `GET /api/auth/discovery` (via BFF) → switch on `ui` case: render `form_schema` fields or start `redirect` (never `if provider.id == "bes"`).
2. `POST /api/auth/authenticate` or callback on **Kithara** → Plume stores access + refresh **server-side**; sets session cookie.
3. All control calls: BFF attaches Bearer user JWT (or guest control JWT after exchange).

Never call auth-adapter containers from the browser.

## Guest control (protected Struna)

Short guest code is **exchange-only**: BFF `POST` → Kithara `POST /api/streams/by-slug/{slug}/guest/exchange` (or `{id}`) → Plume holds the **guest control JWT** server-side for that Struna. Share `/control/{slug}` + the code separately — never put the code in the URL. Do not send the code on every play/queue call. Respect exchange rate limits ([SEC-05](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/mvp/security-audit.md)). See [struna-access](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/domains/struna-access.md) · [security-notes](mvp/security-notes.md).

## Playback control UI

Wire to Kithara: play / quickplay, pause, skip, queue / quickqueue, quicksearch / search, delete, now-playing. **Poll** now-playing / queue for MVP. Browser audio **off by default**; optional listen via `/stream/{slug}` (listen token query when protected). Public and **hidden** Strunas use unauthenticated `GET /bff/streams/by-slug/{slug}/now-playing` (Kithara open-playback); control and locked playback stay session-backed.

## Registration

Client module **Registers** with Kithara over gRPC (join secret + `user-aware`) — [clients](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/domains/clients.md) · [module registry](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/grpc-module-registry.md). Day-to-day work is still REST as the logged-in user (via BFF).

**Read next:** [03-ui-stack.md](03-ui-stack.md)
