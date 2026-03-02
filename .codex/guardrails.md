# Budget Repo Guardrails For Future Codex Work

## Current Implementation Baseline
- Monorepo services: `web` (Next.js 14 + TypeScript), `api` (ASP.NET Core Minimal API on `net10.0`), `worker` (`net10.0` background service), `db` (PostgreSQL), `adminer`.
- Runtime wiring is through `docker-compose.yml` and environment variables (`.env`).
- Auth flow is owner-based with JWT in API and `budget_session` cookie handling in web middleware/server components.

## Guardrails
1. Keep architecture boundaries:
- `web` handles UI, middleware, and proxy/session interactions.
- `api` owns auth endpoints and budgeting domain logic.
- `worker` stays focused on background/scheduled tasks and health checks.

2. Preserve existing conventions:
- Keep API routes under `/api/*` and auth routes under `/api/auth/*`.
- Keep protected-route behavior in web middleware aligned with current public paths and redirect-to-login flow.
- Keep target frameworks as `net10.0` unless explicitly requested.

3. Configuration and secret safety (mandatory):
- Do not enter real values in `.env` or any `appsettings.json` file.
- If new configuration is required, add the new variable/key with an empty value placeholder.
- Wire new configuration through environment variables and placeholders only, to avoid committing sensitive data.

4. Authorization test policy (mandatory):
- Do not create unit tests for authorization methods.
- Specifically avoid unit tests that require storing or exposing passwords in test code, fixtures, snapshots, or logs.

5. Test scope guidance:
- Prefer tests for non-auth domain logic (budget calculations, data workflows, worker options/scheduling behavior, UI rendering/utility logic).
- Keep tests free of secret material and hardcoded credentials.

6. Change hygiene:
- Prefer modifying source files only; avoid editing generated outputs (`bin/`, `obj/`, `.next/`, `node_modules/`).
- When adding configuration keys, keep `.env.example` aligned with empty placeholders.
