# Security notes (Plume)

Plume-specific session / XSS / guest UX notes. **Do not fork** Kithara `SEC-*` — mesh, guest token semantics, and adapter trust live in [kithara security-audit](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/mvp/security-audit.md).

## BFF cookie session

- Session cookie name: **`plume.sid`** (override via `Session:CookieName`). Always **httpOnly**; `Secure` when the request is HTTPS; `SameSite` from `Session:SameSite` (default `Lax`) to match how the edge serves Plume vs `/api`.
- Access + refresh JWTs live **only** in Plume’s server-side store — never in island JS, `localStorage`, query strings, or the cookie value.
- Bind tokens to the opaque session id. `EstablishAsync` issues a **new** session id and drops any prior cookie binding (anti-fixation). `ClearAsync` removes the store entry and expires the cookie.
- Refresh: on upstream `401`, BFF calls Kithara `POST /api/auth/refresh` once, updates the store, and retries. If the refresh response omits `refresh_token`, keep the prior refresh token. Refresh failure clears the session.
- Idle TTL: in-memory entries expire after `Session:IdleTimeout` of no access (sliding); see [operations.md](../operations.md).

## XSS blast radius

Islands run with user-controlled strings (titles, search hits). XSS in a widget must **not** yield a Bearer token — that is the point of BFF. Still treat XSS as account-session theft (cookie): CSP and careful encoding matter.

## Guest exchange UX

- Short guest code → `POST …/guest/exchange` once; then BFF holds the guest JWT like any other session principal.
- Respect Kithara rate limits on exchange ([SEC-05](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/mvp/security-audit.md) and related) — do not retry-spam from the UI.
- Do not put the raw guest code on every play/queue call.

## Adapters and edge

- Browser and Plume UI code must **not** call Bes/Argus/Hecate containers.
- Plume must not mint JWTs; Kithara / auth modules do.
- Plume must not serve `/api` or `/stream` — edge targets Kithara. Authenticated browser REST goes through Plume `/bff/*`.

## Listen tokens

Optional `/stream/{slug}?token=…` for protected playback is a **Kithara** listen secret (VLC / opt-in `<audio>`). Do not confuse it with the BFF session cookie or guest control JWT.

## Related Kithara findings (consume, don’t duplicate)

| ID | Plume impact |
|----|----------------|
| **SEC-01** | Guest refresh — BFF must use host guest refresh once fixed |
| **SEC-05** | Exchange rate-limit — UX + retry policy |
| **SEC-07** | Every mint is admin today — UI must not assume forever-admin once roles land |
| **MESH-REG-*** | Join secret / Register — ops for Plume’s client-module attach |

**Related:** [02-contracts.md](../02-contracts.md) · [known-issues.md](known-issues.md) · [operations.md](../operations.md) · Kithara [auth](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/auth.md) · [struna-access](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/domains/struna-access.md)

**Read next:** [../ideas.md](../ideas.md)
