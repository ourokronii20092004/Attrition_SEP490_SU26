# Generated API types

These files are generated from the backend's OpenAPI (Swagger) specs in `frontend/openapi/`,
via `npm run gen:types` (uses `openapi-typescript`). **Do not edit by hand.**

## What this is — and isn't

This is a **drift-detection reference**, not a full replacement for `src/lib/types.ts` (yet).

- The backend wraps almost every response in `ApiResponse<T>`, and Swashbuckle only emits named
  component schemas for DTOs that appear directly in a request/response body. So generic envelopes
  (`ApiResponse<T>`, `PaginatedResponse<T>`) are inlined, and some DTOs returned only inside a
  wrapper don't surface as named components.
- Coverage today: ~35 schemas across the 9 services vs ~72 hand-maintained types in `types.ts`.
  Admin and Search emit **zero** (they return loosely-typed aggregate shapes).
- Therefore `types.ts` is still the source of truth for the frontend. These generated types let you
  **diff against reality** for the request/response bodies that ARE captured (the highest-drift-risk
  ones: create/update request shapes, list item DTOs).

## How to regenerate the specs (when the backend DTOs change)

Swagger is dev-only and there's no aggregated spec endpoint, so specs are captured from each service
running with `ASPNETCORE_ENVIRONMENT=Development` against a throwaway database:

```bash
# 1. throwaway Postgres (services run MigrateAsync on boot, so they need a reachable DB)
docker run -d --rm --name attr-spec-pg -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=attrition -p 55432:5432 postgres:16-alpine

# 2. build the backend, then per service (run from inside each service dir so appsettings resolves):
cd backend/services/Enemy.Service
ConnectionStrings__DefaultConnection="Host=localhost;Port=55432;Database=attrition;Username=postgres;Password=postgres" \
  ASPNETCORE_ENVIRONMENT=Development DOTNET_ROLL_FORWARD=LatestMajor \
  dotnet swagger tofile --output ../../../frontend/openapi/Enemy.json bin/Debug/net9.0/Enemy.Service.dll v1

# 3. tear down, then regenerate TS:
docker stop attr-spec-pg
cd frontend && npm run gen:types
```

Tooling notes (learned the hard way): the global Swashbuckle CLI v10 needs a newer `Microsoft.OpenApi`
than the app's Swashbuckle 6.5.0 ships; the version-matched CLI 6.5.0 targets a .NET 7 runtime that
isn't installed. `DOTNET_ROLL_FORWARD=LatestMajor` runs it on .NET 9 and sidesteps both.

## The real fix (deferred — backend work)

To make codegen able to *replace* `types.ts`, the backend would need explicit
`[ProducesResponseType(typeof(...))]` annotations on every action so Swashbuckle emits complete,
named schemas (including the `ApiResponse<T>`/`PaginatedResponse<T>` envelopes). That's a change
across 9 services — tracked, not done. Until then: hand-written `types.ts` is canonical; these
generated files are the cross-check.
