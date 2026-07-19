# Contracts

Plume is a **REST client** of Kithara. Path map: [uri-routing](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/uri-routing.md). API sketch: [rest-api](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/rest-api.md). Auth: [auth](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/auth.md).

## Edge routes (Plume)

| Path | Role |
|------|------|
| `/` | List/create Strunas (auth required when Plume is used) |
| `/player/{slug}` | Queue / control UI |

## Auth flows

1. `GET /api/auth/discovery` → render `form_schema` (Bes) or start redirect (Argus later).
2. `POST /api/auth/authenticate` or callback on **Kithara** → store Bearer user JWT (+ refresh).
3. All control calls: `Authorization: Bearer <user JWT>`.

Never call auth-adapter containers from the browser.

## Guest control (protected Struna)

Short guest code is **exchange-only**: `POST /api/streams/{id}/guest/exchange` → Bearer **guest control JWT** for that Struna. Do not send the code on every play/queue call. See [struna-access](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/domains/struna-access.md).

## Playback control UI

Wire to Kithara: play / quickplay, pause, skip, queue / quickqueue, quicksearch / search, delete, now-playing. Browser player **off by default**; optional listen via `/stream/{slug}` (listen token query when protected).

## Registration

Client module may register with Kithara (join secret + `user-aware`) — sketch in Kithara [clients](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/domains/clients.md). Day-to-day work is still REST as the logged-in user.

**Read next:** [mvp/v0.1-scope.md](mvp/v0.1-scope.md)
