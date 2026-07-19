# Operations

Env and runtime knobs for the **Plume** container.

| Variable | Role |
|----------|------|
| Kithara public/base URL | Browser calls `/api` via edge (same host preferred) |
| Join secret | If Plume registers as a client module |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | External collector |

Published behind the edge for `/` and `/player/*` only — not `/api` or `/stream`.

## Observability

- `service.name=bardie.plume`
- Propagate trace context on outbound calls to Kithara when applicable

**Related:** Kithara [uri-routing](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/uri-routing.md) · [observability](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/operations/observability.md)

**Read next:** [ideas.md](ideas.md)
