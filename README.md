# Order Ingest + React Admin

One webhook endpoint ingests orders from **Uber Eats** and **DoorDash Marketplace**,
normalizes them into a single internal model, and a **React admin** displays them.

- `POST /webhooks/orders` — accepts both providers, detected from the payload shape
  (no `"provider"` discriminator). Uber requests are verified against `X-Uber-Signature`
  (HMAC-SHA256 of the raw body). The endpoint returns `200` with an empty body
  immediately; mapping and persistence happen on a background worker.
- `GET /orders`, `GET /orders/{id}` — internal DTOs only; no provider field names leak.
- Retries never duplicate: one row per `(provider, external_order_id)` with a
  monotonic status guard.

## Prerequisites

| Tool     | Version |
| -------- | ------- |
| .NET SDK | 10.x    |
| Node.js  | 20+     |

Nothing else — the database is a local SQLite file created automatically at startup.

## Run it

Two terminals, both from the repo root.

**Terminal 1 — API** (http://localhost:5080):

```bash
dotnet run --project src/OrderIngest.Api
```

**Terminal 2 — admin UI** (http://localhost:5173):

```bash
cd client
npm install
npm run dev
```

The Vite dev server proxies `/api/*` to the API, so there is no CORS setup.

## Send the two webhooks

From the repo root, with the API running.

**Uber Eats** (`orders.notification`, signed with the dev-only secret from
`appsettings.Development.json`):

macOS / Linux:

```bash
curl -i -X POST 'http://localhost:5080/webhooks/orders' \
  -H 'Content-Type: application/json' \
  -H 'X-Uber-Signature: d3f2587df0b8d5371ca6480f9d2ab4fe2ae2e4d4fb3b41df0304b346319c2eaa' \
  --data-binary @fixtures/uber-notification.json
```

Windows PowerShell:

```powershell
curl.exe -i -X POST "http://localhost:5080/webhooks/orders" `
   -H "Content-Type: application/json" `
   -H "X-Uber-Signature: d3f2587df0b8d5371ca6480f9d2ab4fe2ae2e4d4fb3b41df0304b346319c2eaa" `
   --data-binary "@fixtures/uber-notification.json"
```

**DoorDash Marketplace** (`{ event, order }`):

macOS / Linux:

```bash
curl -i -X POST 'http://localhost:5080/webhooks/orders' \
  -H 'Content-Type: application/json' \
  --data-binary @fixtures/doordash-order.json
```

Windows PowerShell:

```powershell
curl.exe -i -X POST "http://localhost:5080/webhooks/orders" `
   -H "Content-Type: application/json" `
   --data-binary "@fixtures/doordash-order.json"
```

Both return `200` with an empty body. Open **http://localhost:5173** — the list shows
both orders (DoorDash US$23.00, Uber US$13.99); click a row for customer, line items,
and status. Or check from the terminal:

macOS / Linux:

```bash
curl -s http://localhost:5080/orders
```

Windows PowerShell:

```powershell
curl.exe -s http://localhost:5080/orders
```

**Replay = no duplicate.** Send the Uber curl again — still `200`, still one Uber row
(the API logs `Skipped`). The unique index on `(provider, external_order_id)` plus a
monotonic status guard make webhook retries idempotent.

> The signature above is precomputed for this exact fixture + dev secret. If you edit
> `fixtures/uber-notification.json`, regenerate it: `./scripts/sign-uber.sh` prints a
> fresh signature and ready-to-run curl. Note `--data-binary` everywhere — plain
> `curl -d` strips newlines, which changes the signed bytes and breaks the HMAC.

## Run the tests

```bash
dotnet test          # 44 tests: detection, mappers, HMAC, retry/drop paths, idempotency
cd client && npm run lint
```

The integration tests boot the real API via `WebApplicationFactory` on a throwaway
SQLite file and prove the core contract end to end — including that posting the same
fixture twice yields exactly one row.

## Architecture

```
React admin (Vite, :5173)
      │  /api/* proxy
      ▼
OrderIngest.Api            controllers, DTOs, DI wiring — no business logic
      ▼
OrderIngest.Business       provider detection · HMAC verification · Channel queue
                           background processor (3 retries, transient-only)
                           mappers · IUberOrderClient (fixture ⇄ real HTTP via config)
      ▼
OrderIngest.Data           EF Core + SQLite · unique (provider, external_order_id)
                           monotonic upsert
      ▼
OrderIngest.Domain         Order, OrderStatus (+rank), CustomerInfo, LineItem
```

Dependencies point strictly downward. The request flow:

1. **Receive** — read the raw body once (buffered); detect the provider
   (`event_type == "orders.notification"` → Uber, `event.type` → DoorDash; neither → 400).
2. **Verify** — Uber only: constant-time compare of `X-Uber-Signature` against
   HMAC-SHA256(raw body, client secret), lowercase hex; mismatch → 401. DoorDash
   webhook auth is agreed during merchant onboarding and not publicly documented —
   deliberately out of scope; its check would plug in at the same point.
3. **Ack** — enqueue on a bounded `Channel` and return `200` empty (Uber's contract:
   respond first, process after).
4. **Process** — background service: Uber fetches the full order via `resource_href`
   (`IUberOrderClient`; the demo serves `fixtures/uber-get-order.json`, production
   swaps to the real HTTP client with one config value — `Uber:OrderClientMode`),
   DoorDash unwraps the embedded `order`. Map to the internal shape, money in cents.
5. **Persist** — upsert: new `(provider, external_order_id)` inserts; a replay or
   out-of-order event is skipped; only a strictly higher-ranked status advances the
   row (terminal states never transition). Transient failures (db contention,
   network/IO) retry 3× with backoff; anything else is logged and dropped.
