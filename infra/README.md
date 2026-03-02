# Infra Notes

- PostgreSQL and Adminer are provisioned in `docker-compose.yml`.
- Runtime configuration is sourced from the root `.env` file.
- Use strong values for `OWNER_PASSWORD` and `JWT_SECRET` before regular usage.
