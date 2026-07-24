# Operations

Env and runtime knobs for the **Plume** container.

| Variable / concern | Role |
|--------------------|------|
| `Kithara:BaseUrl` / `Kithara__BaseUrl` | Upstream Kithara origin (no trailing path). BFF calls `{BaseUrl}/api/…` (Compose internal DNS; same public host at the edge for `/api`) |
| `Session:CookieName` | httpOnly session cookie name — default `plume.sid` |
| `Session:SameSite` | `Lax` (default), `Strict`, or `None` — match how the edge serves Plume vs `/api` |
| Session cookie `Secure` | Set automatically when the request is HTTPS |
| `Session:IdleTimeout` | Sliding idle lifetime for in-memory token entries (default `1.00:00:00` / 24h). Abandoned sessions drop on next access after the window; `0` disables. Not multi-replica safe |
| Session / token store | Server-side access + refresh (memory ok for early MVP; durable store before multi-replica) |
| Join secret | If Plume registers as a client module |
| Vite assets | Built islands served as static files from the Razor host |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | External collector |

Published behind the edge for `/`, `/control/*`, and `/player/*` only — not `/api` or `/stream`. Browser UI also hits Plume `/bff/*` with the session cookie; that path is not a public edge route for Kithara.

## Observability

- `service.name=bardie.plume`
- Propagate trace context on outbound BFF calls to Kithara

**Related:** Kithara [uri-routing](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/uri-routing.md) · [observability](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/operations/observability.md) · [03-ui-stack.md](03-ui-stack.md) · [mvp/security-notes.md](mvp/security-notes.md)

**Read next:** [mvp/v0.1-scope.md](mvp/v0.1-scope.md)
