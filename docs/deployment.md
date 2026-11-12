# Deployment

The app ships as three container images, built and pushed to GitHub Container
Registry by CI on every push to `main` (after tests pass):

| Image | Purpose |
|---|---|
| `ghcr.io/<owner>/finance-manager-web` | nginx: serves the Angular app and proxies `/api` and `/health` to the API |
| `ghcr.io/<owner>/finance-manager-api` | ASP.NET Core API and background jobs (port 8080) |
| `ghcr.io/<owner>/finance-manager-migrator` | One-off job: applies pending database migrations, then exits |

Each image is tagged with the commit SHA (pin deploys to this) and `latest`.

```
internet ──TLS──> load balancer / TLS proxy ──> web (nginx :80) ──/api──> api (:8080) ──> PostgreSQL
```

The browser only ever talks to `web`, so the auth and CSRF cookies stay
same-origin. **The API must not be reachable directly from the internet**: it
trusts `X-Forwarded-For` from the proxy for rate limiting.

## Deploy order

1. Run the **migrator** with the new tag and wait for it to exit successfully.
2. Roll out the **api**, then the **web** image with the same tag.

Migrations must be backwards compatible with the version still running during
the rollout (add columns before using them; drop them in a later release).

## Configuration (API)

Set as environment variables (`__` separates sections). `.env.example` lists
them all.

| Variable | Required | Notes |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | yes | Managed Postgres with daily backups and point-in-time recovery |
| `Frontend__BaseUrl` | yes | Public URL, used in emails and Stripe redirects |
| `DataProtection__CertificatePath` / `__CertificatePassword` | yes | See below. The API refuses to start without it |
| `Resend__ApiKey`, `Resend__From` | yes | `From` must use your verified domain |
| `Plaid__ClientId`, `Plaid__Secret`, `Plaid__Environment` | yes | `production` once Plaid approves you |
| `Plaid__WebhookUrl` | recommended | `https://<domain>/api/v1/bank/plaid/webhook`. New transactions sync within seconds and expired bank logins are flagged immediately; the scheduled sync remains as a fallback. Applies to banks linked or reconnected after it is set |
| `Billing__StripeSecretKey`, `Billing__StripeWebhookSecret`, `Billing__*__MonthlyPriceId` | yes | Webhook URL: `https://<domain>/api/v1/billing/webhook` with `checkout.session.completed` and `customer.subscription.*` |
| `Sentry__Dsn` | recommended | Error tracking; off when empty |
| `ForwardedHeaders__ForwardLimit` | if needed | Number of proxies in front of the API (default 1: nginx). Use 2 when a load balancer sits in front of nginx |

Never set `Billing__DefaultPlan` in production; the API refuses to start if
it is anything but `Free` outside development.

### Data Protection certificate

Encrypts the keys that protect auth cookies and stored bank tokens. Create it
once and keep it in your host's secret store, never in the repository:

```sh
openssl req -x509 -newkey rsa:4096 -nodes -days 730 \
  -subj "/CN=FinanceManager DataProtection" -keyout key.pem -out cert.pem
openssl pkcs12 -export -out dataprotection.pfx -inkey key.pem -in cert.pem
```

Losing it logs everyone out and makes stored bank tokens unreadable (users
must reconnect their banks), so back it up. Before it expires, add the new
certificate and keep the old one readable until existing keys rotate out.

## Monitoring

- **Uptime**: point an uptime monitor (e.g. Better Stack, UptimeRobot) at
  `https://<domain>/health/ready`. It checks the API and the database.
- **Load balancer health checks**: `/health/live` (process up) for restarts,
  `/health/ready` for routing traffic.
- **Errors**: set `Sentry__Dsn`. Error responses include a `traceId` that
  matches the logs.
- **Logs**: the API writes one JSON object per line to stdout outside
  development; ship stdout to your log platform.

## Scaling

Several API instances can run at once: bank sync and savings goal jobs take a
Postgres advisory lock so each runs on one instance per tick, and recurring
posting locks rows. The Data Protection keys are stored in the database, so
every instance shares them.

## Storage

Receipt photos and PDFs are stored in Postgres (`TransactionAttachments`,
up to 5 MB each and 10 per transaction; the app shrinks large photos before
upload). They are included in database backups and deleted with their
transaction. Watch database storage as usage grows; if it becomes large,
move the file bytes to object storage (e.g. S3) and keep the metadata.

## Run the production stack locally

```sh
cp .env.example .env              # fill in at least POSTGRES_PASSWORD
mkdir -p secrets                  # put dataprotection.pfx here (git-ignored)
docker compose up --build         # http://localhost:8080
```

Production cookies are `Secure`, so signing in needs HTTPS. Over plain
`http://localhost` the app loads but login cookies are not sent.
