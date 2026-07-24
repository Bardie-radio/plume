# Role and boundaries

Plume is the **reference user-aware** web UI for MVP. It is optional at the edge — Kithara still owns `/api` and `/stream`, and login/callback can work without Plume.

Plume is a **thin UI client + BFF**: Razor SSR pages, Vue CSR widgets, server-side session toward Kithara. It does not own users, Strunas, or library data.

## Owns

- Routes `/`, `/control/{slug}`, `/player/{slug}` (edge routes those paths here)
- **BFF session**: httpOnly cookie → Plume → Kithara Bearer (no JWT in the browser)
- Rendering login UI from Kithara **discovery** (`form_schema` / `redirect` oneof — never branch on provider id)
- Remote **control desk** and **listen / player** surfaces composed from shared widgets
- Optional in-browser listen (opt-in to `/stream/{slug}`) — **audio off by default**
- OTLP as `bardie.plume`
- Client module **Register** (join secret + `user-aware`) when running in mesh

## Does not own

- A user/stream/library database (Kithara is source of truth; Identity/EF/SQLite scaffold is throwaway)
- Auth adapters (never call Bes/Argus directly from browser **or** Plume-as-IdP)
- Minting user JWTs or guest JWTs (auth modules / Kithara)
- Source modules, FFmpeg, Stream Server
- Serving `/api` or `/stream` (Kithara)
- Live push fan-out (MVP **polls**; Browser→Kithara events are a later Kithara concern)

**Read next:** [02-contracts.md](02-contracts.md)
