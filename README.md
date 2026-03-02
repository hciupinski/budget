# Budget App (Epic 1 + Epic 2)

Local-first budgeting app with secure owner login, annual planning matrix, and monthly execution workspace.

## Services
- `web`: Next.js UI with login, annual planner, monthly workspace, and audit panel.
- `api`: ASP.NET Core API (`net10.0`) with JWT auth and Epic 2 budgeting endpoints.
- `worker`: background worker service with heartbeat endpoint.
- `db`: PostgreSQL.
- `adminer`: DB UI.

## Quick start
1. Create env file:
   - `cp .env.example .env`
2. Update at least:
   - `OWNER_PASSWORD`
   - `JWT_SECRET`
3. Start stack:
   - `docker compose up --build`

## URLs
- Web app: `http://localhost:3000`
- Annual Planner page: `http://localhost:3000/annual`
- Monthly Workspace page: `http://localhost:3000/monthly`
- API health: `http://localhost:8080/healthz`
- Worker health: `http://localhost:8082/healthz`
- Adminer: `http://localhost:8081`

Use `OWNER_EMAIL` and `OWNER_PASSWORD` from `.env` to sign in.

## Epic 2 workflow
1. Open **Annual Planner** and enter planned amounts for Jan-Dec.
2. Save annual plan, or copy from previous year.
3. Open **Monthly Workspace**, choose year/month, click **Generate from Annual Plan**.
4. Execute the month by setting statuses: `PLANNED`, `DONE`, `PARTIAL`, `SKIPPED`.
5. Update actual amounts and save action rows.
6. Review summary cards and recent audit trail changes.

If routes/features do not appear after code changes, rebuild containers:
- `docker compose up --build`
