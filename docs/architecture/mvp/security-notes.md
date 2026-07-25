# Security notes (Plume)

Plume-specific session / XSS / guest UX notes. **Do not fork** Kithara `SEC-*` — mesh, guest token semantics, and adapter trust live in [kithara security-audit](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/mvp/security-audit.md).

## BFF cookie session

- Session cookie name: **`plume.sid`** (override via `Session:CookieName`). Always **httpOnly**; `Secure` when the request is HTTPS; `SameSite` from `Session:SameSite` (default `Lax`) to match how the edge serves Plume vs `/api`.
- Access + refresh JWTs live **only** in Plume’s server-side store — never in island JS, `localStorage`, query strings, or the cookie value.
- Bind tokens to the opaque session id. `EstablishAsync` issues a **new** session id and drops any prior cookie binding (anti-fixation). `ClearAsync` removes the store entry and expires the cookie.
- Refresh: on upstream `401`, BFF calls Kithara `POST /api/auth/refresh` once, updates the store, and retries. If the refresh response omits `refresh_token`, keep the prior refresh token. Refresh failure clears the session.
- Idle TTL: in-memory entries expire after `Session:IdleTimeout` of no access (sliding); see [operations.md](../operations.md).

### PLUME-SESS-001 — single-replica MVP limit (**deferred**)

`MemorySessionTokenStore` is process-local. Multi-replica Plume or a cold restart drops sessions (users re-login). **Accepted for MVP** — no Redis / shared session store in this phase. Operators run one Plume replica (or sticky sessions) until a durable store lands.

## XSS blast radius

Islands run with user-controlled strings (titles, search hits). XSS in a widget must **not** yield a Bearer token — that is the point of BFF. Still treat XSS as account-session theft (cookie): CSP and careful encoding matter.

### PLUME-SEC-001 — Content-Security-Policy

Plume sets a default CSP on responses (`default-src 'self'`, no third-party script CDNs). Style keeps `'unsafe-inline'` for Tailwind-built CSS. Tune if islands need more.

### PLUME-SEC-002 — BFF antiforgery

Unsafe `/bff/*` methods (`POST` / `PUT` / `PATCH` / `DELETE`) require an antiforgery token (`X-CSRF-TOKEN` header or form `__RequestVerificationToken`). Clients obtain a token via `GET /bff/auth/csrf` (islands) or Razor `@Html.AntiForgeryToken()` (logout form). SameSite=Lax remains; antiforgery covers cross-site POST with cookies.

## Guest exchange UX

- Short guest code → `POST …/guest/exchange` once; then BFF holds the guest JWT like any other session principal.
- Respect Kithara rate limits **and** failure lockout on exchange ([GUEST-XCHG-001](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/mvp/security-audit.md) / GUEST-XCHG-002) — do not retry-spam from the UI.
- Do not put the raw guest code on every play/queue call.

## Adapters and edge

- Browser and Plume UI code must **not** call Bes/Argus/Hecate containers.
- Plume must not mint JWTs; Kithara / auth modules do.
- Plume must not serve `/api` or `/stream` — edge targets Kithara. Authenticated browser REST goes through Plume `/bff/*`.

## Listen tokens

Optional `/stream/{slug}?token=…` for protected playback is a **Kithara** listen secret (VLC / opt-in `<audio>`). Do not confuse it with the BFF session cookie or guest control JWT. UI session ≠ listen ACL ([DOC-STREAM-001](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/mvp/security-audit.md)).

## Related Kithara findings (consume, don’t duplicate)

| ID | Plume impact |
|----|----------------|
| **GUEST-REF-001** | Guest refresh — BFF must use host guest refresh |
| **GUEST-XCHG-001/002** | Exchange rate-limit + lockout — UX + retry policy |
| **AUTH-ROLE-001** | Roles from binding — UI must not assume forever-admin |
| **MESH-REG-*** | Join secret / Register — ops for Plume’s client-module attach |

**Related:** [02-contracts.md](../02-contracts.md) · [known-issues.md](known-issues.md) · [operations.md](../operations.md) · Kithara [auth](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/auth.md) · [struna-access](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/domains/struna-access.md)

**Read next:** [../ideas.md](../ideas.md)
