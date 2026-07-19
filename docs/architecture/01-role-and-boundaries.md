# Role and boundaries

Plume is the **reference user-aware** web UI for MVP. It is optional at the edge — Kithara still owns `/api` and `/stream`, and login/callback can work without Plume.

Plume is a **thin UI client**: it renders surfaces and calls Kithara REST. It does not own users, Strunas, or library data.

## Owns

- Routes `/` and `/player/{slug}` (served by Plume; edge routes those paths here)
- Rendering login UI from Kithara **discovery** (`form_schema` / redirect hints)
- Calling Kithara REST with Bearer **user JWT** (and guest control JWT after exchange when control is protected)
- Optional in-browser listen (redirect or embed to `/stream/{slug}`) — **player off by default**
- OTLP as `bardie.plume`

## Does not own

- A user/stream/library database (Kithara is source of truth)
- Auth adapters (never call Bes/Argus directly)
- Minting user JWTs or guest JWTs (auth modules / Kithara)
- Source modules, FFmpeg, Stream Server
- Serving `/api` or `/stream` (Kithara)

**Read next:** [02-contracts.md](02-contracts.md)
