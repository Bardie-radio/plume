# Plume

Web UI **client module** for Bardie — user-aware reference client for MVP (optional; the stack works without it).

| | |
|--|--|
| **Status** | **MVP** v0.1 |
| **Image / Compose** | `plume` |
| **OTel** | `bardie.plume` |
| **Auth mode** | User-aware (BFF session → Bearer JWT from auth modules via Kithara) |
| **UI** | Razor SSR + Vue CSR widgets; `/control/{slug}` desk · `/player/{slug}` listen |

Architecture: [docs/architecture](docs/architecture/README.md).

Org catalog: [client modules](https://github.com/Bardie-radio/.github/blob/main/profile/docs/architecture/06-client-modules.md). Kithara contracts: [clients](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/domains/clients.md) · [uri-routing](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/uri-routing.md) · [rest-api](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/rest-api.md) · [auth](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/interfaces/auth.md) · [struna-access](https://github.com/Bardie-radio/kithara/blob/main/docs/architecture/domains/struna-access.md)

Org: [Bardie architecture](https://github.com/Bardie-radio/.github/tree/main/profile/docs/architecture)
