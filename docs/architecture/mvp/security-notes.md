# Security notes (Plume)

Plume-specific session / XSS / guest UX notes. **Do not fork** Kithara `SEC-*` — mesh, guest token semantics, and adapter trust live in [kithara security-audit](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/mvp/security-audit.md).

## BFF cookie session

- Session cookie: **httpOnly**; use `Secure` on HTTPS edges; pick `SameSite` that matches how the edge serves Plume vs `/api`.
- Access + refresh JWTs live **only** in Plume’s server-side store — never in island JS, `localStorage`, or query strings.
- Refresh rotation / fixation: bind tokens to the session id; invalidate session on logout; avoid session fixation on login (issue a new session id after authenticate).

## XSS blast radius

Islands run with user-controlled strings (titles, search hits). XSS in a widget must **not** yield a Bearer token — that is the point of BFF. Still treat XSS as account-session theft (cookie): CSP and careful encoding matter.

## Guest exchange UX

- Short guest code → `POST …/guest/exchange` once; then BFF holds the guest JWT like any other session principal.
- Respect Kithara rate limits on exchange ([SEC-05](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/mvp/security-audit.md) and related) — do not retry-spam from the UI.
- Do not put the raw guest code on every play/queue call.

## Adapters and edge

- Browser and Plume UI code must **not** call Bes/Argus/Hecate containers.
- Plume must not mint JWTs; Kithara / auth modules do.
- Plume must not serve `/api` or `/stream` — edge targets Kithara.

## Listen tokens

Optional `/stream/{slug}?token=…` for protected playback is a **Kithara** listen secret (VLC / opt-in `<audio>`). Do not confuse it with the BFF session cookie or guest control JWT.

## Related Kithara findings (consume, don’t duplicate)

| ID | Plume impact |
|----|----------------|
| **SEC-01** | Guest refresh — BFF must use host guest refresh once fixed |
| **SEC-05** | Exchange rate-limit — UX + retry policy |
| **SEC-07** | Every mint is admin today — UI must not assume forever-admin once roles land |
| **MESH-REG-*** | Join secret / Register — ops for Plume’s client-module attach |

**Related:** [02-contracts.md](../02-contracts.md) · [known-issues.md](known-issues.md) · Kithara [auth](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/auth.md) · [struna-access](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/domains/struna-access.md)

**Read next:** [../ideas.md](../ideas.md)
