# Budget App (Epic 1 + Epic 2 + Epic 4)

Local-first budgeting app with secure owner login, annual planning matrix, monthly execution workspace, and account/investment tracking.

## Services
- `web`: Next.js UI with login, annual planner, monthly workspace, accounts/investments dashboard, and audit panel.
- `api`: ASP.NET Core API (`net10.0`) with JWT auth and budgeting + assets endpoints.
- `worker`: background worker service with heartbeat endpoint.
- `db`: PostgreSQL.
- `adminer`: DB UI.

## Quick start
1. Create env file:
   - `cp .env.example .env`
2. Update at least:
   - `OWNER_PASSWORD`
   - `OWNER_PASSWORD_HASH` (recommended for production; keep legacy plaintext only during transition)
   - `JWT_SECRET`
   - (optional) host ports like `WEB_HOST_PORT`, `API_HOST_PORT`, `DB_HOST_PORT` if you want custom mappings
3. Start stack:
   - `docker compose up --build`

## URLs
- Web app: `http://localhost:13000`
- Annual Planner page: `http://localhost:13000/annual`
- Monthly Workspace page: `http://localhost:13000/monthly`
- Accounts page: `http://localhost:13000/accounts`
- API health: `http://localhost:18080/healthz`
- Worker health: `http://localhost:18082/healthz`
- Adminer: `http://localhost:18081`

Use `OWNER_EMAIL` and your configured owner password from `.env` to sign in.

## Access from mobile (same Wi-Fi)
1. Keep the stack running with `docker compose up --build`.
2. Find your computer LAN IP (macOS):
   - `ipconfig getifaddr en0`
3. Open the web app on your phone:
   - `http://<YOUR_LAN_IP>:13000`

The web container binds on `0.0.0.0:3000` internally and is published on `WEB_HOST_PORT` (default `13000`), so it is reachable from other devices on your network.

## Epic 2 workflow
1. Open **Annual Planner** and enter planned amounts for Jan-Dec.
2. Save annual plan, or copy from previous year.
3. Open **Monthly Workspace**, choose year/month, click **Generate from Annual Plan**.
4. Execute the month by setting statuses: `PLANNED`, `DONE`, `PARTIAL`, `SKIPPED`.
5. Update actual amounts and save action rows.
6. Review summary cards and recent audit trail changes.

## Epic 4 workflow
1. Open **Accounts** and create bank/savings/brokerage/cash accounts.
2. Log account transfers to keep balances synchronized.
3. Save monthly account snapshots (planned vs actual).
4. Add brokerage holdings and refresh market prices.
5. Optionally set manual price overrides for holdings.
6. Create savings goals and track progress.

If routes/features do not appear after code changes, rebuild containers:
- `docker compose up --build`
