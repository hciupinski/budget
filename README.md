# Budget App (Epic 1 Foundation)

Monorepo baseline for the budgeting app with secure owner login, protected routes/APIs, and local infrastructure.

## Included
- `web`: Next.js app (App Router) with shadcn-style UI base, login page, and route guard.
- `api`: ASP.NET Core API (`net10.0`) with JWT auth, single-owner credentials from env, and health endpoints.
- `worker`: ASP.NET Core hosted worker service with structured logs and heartbeat.
- `infra`: Infrastructure support folder (Docker Compose is at repo root).
- PostgreSQL + Adminer via Docker Compose.

## Quick start
1. Create env file:
   - `cp .env.example .env`
2. Update `OWNER_PASSWORD` and `JWT_SECRET` in `.env`.
3. Start everything:
   - `docker compose up --build`

## URLs
- Web app: `http://localhost:3000`
- API health: `http://localhost:8080/healthz`
- Worker health: `http://localhost:8082/healthz`
- Adminer: `http://localhost:8081`

Use `OWNER_EMAIL` and `OWNER_PASSWORD` from `.env` to sign in.
