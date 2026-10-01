# Order Ingest + React Admin — Decision Record

Take-home: single webhook endpoint ingesting Uber Eats and DoorDash Marketplace orders
into one internal model, with a React admin to view them. This file records every
decision made before implementation so any change is deliberate, not accidental.

## Stack decisions (Phase 0)

| # | Decision | Choice | Why |
|---|----------|--------|-----|
| 1 | .NET version | .NET 10 (LTS) | Current LTS; the only SDK required on a fresh clone |
| 2 | Database | SQLite file | Zero setup, real unique index for idempotency; Postgres is the prod swap |
| 3 | Data access | EF Core + `EnsureCreated()` at startup | One table, demo scope; migrations are the prod swap |
| 4 | Layout | n-tier: Api → Business → Data → Domain under `src/`, one test project; API uses MVC controllers | Dependencies flow downward only; Api has no business/data logic |
| 5 | Webhook processing | `Channel<T>` + `BackgroundService` | Spec says respond 200 *then* process; both providers share one processing path |
| 6 | Uber GET stub | `IUberOrderClient` interface; `FixtureUberOrderClient` (dev) vs `HttpUberOrderClient` (prod) selected by config `UberClient:Mode` | Prod-ready with one config change |
| 7 | Signature verification | `UberSignatureVerifier` service called at the top of the webhook controller action; `Request.EnableBuffering()`, raw body read once; `CryptographicOperations.FixedTimeEquals`; **401** on mismatch | 401 = caller not proven to be Uber; 400 is for malformed requests |
| 8 | Duplicate handling | Upsert on `(provider, external_order_id)` with monotonic status guard (see below) | Replays are no-ops; real state changes are applied; never a duplicate row |
| 9 | Status normalization | Internal enum + rank; `raw_status` preserved | See mapping table below |
| 10 | Customer / line items | JSON columns on the single `orders` row (EF owned types + `ToJson()`) | Spec says "one internal row"; admin only reads whole orders |
| 11 | Frontend | Vite + React Router + TanStack Query + CSS Modules; `Intl.NumberFormat` with the row's currency | Query implements the required loading/error/empty states |
| 12 | Run story | Two terminals: `dotnet run` + `npm run dev` with Vite proxy (no CORS config needed) | Shortest, least fragile README |
| 13 | Tests | xUnit: provider detection, both mappers, HMAC verification, DTO-leak check, idempotency integration test (WebApplicationFactory + SQLite, same fixture posted twice → one row) | Each test maps to a stated requirement |

## Additional decisions

- **Malformed JSON / unknown provider** → 400 problem-details. Invalid Uber signature → 401 before parsing continues. Replay/stale status → still 200 (webhook contract), no state change.
- **Index on `received_at`** (list sorts newest-first) in addition to the unique index on `(provider, external_order_id)`.
- **Transient processing failures** (DB lock/IO) retried up to 3 times with short backoff in the background processor; final failure logged. Dead-letter queue is a documented production note, not built.
- **Monotonic status guard**: accept an incoming status iff `rank(incoming) > rank(current)`. Terminal states (Completed, Denied, Cancelled) never transition. `Unknown` (rank 0) never overwrites a known status. Accepted events refresh `status`, `raw_status`, `raw_payload`, `last_updated_at`.
- **Uber dev secret** lives in `appsettings.Development.json`, labelled dev-only. No other secrets in git.
- **DoorDash webhook auth** is configured out-of-band with DoorDash and not publicly documented ("same authentication header as Menu Status Updates") — out of scope; the endpoint filter is the seam where it would plug in.
- `.gitignore` excludes the SQLite `*.db` files so a fresh clone starts empty.
- README prerequisites pin .NET 10 SDK and Node 20+.

## Status mapping (documented provider values only)

| Internal | Rank | Uber `current_state` | DoorDash |
|----------|------|----------------------|----------|
| Unknown | 0 | `UNKNOWN` / unmapped | unmapped |
| Created | 1 | `CREATED` | `event.type=OrderCreate`, `event.status=NEW` |
| Accepted | 2 | `ACCEPTED` | — |
| Completed | 3 (terminal) | `FINISHED` | — |
| Denied | 3 (terminal) | `DENIED` | — |
| Cancelled | 3 (terminal) | `CANCELED` | — |

## Provider detection

- Root field `event_type` == `orders.notification` → **Uber**
- Root object `event` with `event.type` → **DoorDash**
- Neither → 400. No `"provider"` discriminator is required or accepted.

## Field mapping

**Uber** (order fetched via `resource_href`, fixture `fixtures/uber-get-order.json`):
`id` → external_order_id · `eater.{first_name,phone}` → customer ·
`cart.items[].{title,quantity,price.total_price.amount}` → line_items ·
`current_state` → status · `payment.charges.total.{amount,currency_code}` → total_cents, currency.
All Uber money amounts are integer cents.

**DoorDash** (unwrap `order` from `{event, order}`, fixture `fixtures/doordash-order.json`):
`order.id` → external_order_id · `consumer.{first_name,last_name,email,phone}` → customer ·
flatten `categories[].items[].{name,quantity,price}` → line_items ·
`subtotal + tax` → total_cents · currency not present in payload → **assumed USD** (explicit assumption).

## Fixture provenance (Phase 1)

- `fixtures/uber-notification.json` — documented example from Uber's Order Notification
  Webhook reference; the doc shows exactly these five fields (its example ends in `...`).
  Confirmed sufficient: detection (`event_type`), order id (`meta.resource_id`), GET target
  (`resource_href`).
- `fixtures/uber-get-order.json` — complete example response from Uber's Get Order v2
  reference, unchanged.
- `fixtures/doordash-order.json` — `{event, order}` wrapper from DoorDash's order
  integration guide + complete sample Order from DoorDash's `reference/sample_order` page.
  Two corrections to DoorDash's own page: a missing comma after `"tax": 300` (doc typo),
  and the consumer email de-obfuscated from Cloudflare email protection
  (`support@doordash.com`).
- X-Uber-Signature (verbatim from Uber docs): "a lowercased hexadecimal HMAC signature of
  the webhook HTTP request body, using the client secret as a key and SHA256 as the hash
  function."

## Out of scope (per the brief)

Swiggy, DoorDash Drive, Kafka, extra providers, custom request bodies, DoorDash webhook
auth, order accept/deny calls back to providers.
