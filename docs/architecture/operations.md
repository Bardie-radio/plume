# Operations

Env and runtime knobs for the **Plume** container.

| Variable / concern | Role |
|--------------------|------|
| Kithara base URL | Server-side BFF calls `/api` (internal DNS in Compose; same public host at the edge) |
| Session cookie | httpOnly; Prefer `Secure` + `SameSite` appropriate to the edge deploy |
| Session / token store | Server-side access + refresh (memory ok for early MVP; durable store before multi-replica) |
| Join secret | If Plume registers as a client module |
| Vite assets | Built islands served as static files from the Razor host |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | External collector |

Published behind the edge for `/`, `/control/*`, and `/player/*` only — not `/api` or `/stream`.

## Observability

- `service.name=bardie.plume`
- Propagate trace context on outbound BFF calls to Kithara

**Related:** Kithara [uri-routing](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/uri-routing.md) · [observability](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/operations/observability.md) · [03-ui-stack.md](03-ui-stack.md)

**Read next:** [mvp/v0.1-scope.md](mvp/v0.1-scope.md)
