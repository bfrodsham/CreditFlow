# CreditFlow — Feature Slices

Each slice is a complete, end-to-end, demoable increment: touches Domain → Application → Infrastructure → Web together, includes tests, and should be built on its own branch and merged via its own PR. Work through them in order — each one builds on the ledger/account foundation established in Slice 1.

For each slice: create the branch yourself first, then run the agent prompt in **Agent mode** on top of it. Review the full diff before committing. Use **Ask mode** for any "why does this work" questions along the way — that's separate from these implementation prompts.

---

## Slice 1 — Walking Skeleton *(reference — already complete)*

**Goal:** Prove the entire path from code to a running, deployed app before any business logic exists.

**Scope:** Minimal Blazor Server app, GitHub Actions CI (build + test), Docker-based deploy to Render gated on tests passing.

**Definition of done:** A visitor can load the deployed URL and see a running placeholder page. CI fails the pipeline if any test fails.

*(Included here for completeness of the sequence — you've already built this.)*

---

## Slice 2 — Account & Balance Display

**Goal:** The core read path: an account has a derived balance, and it's visible end-to-end.

**Scope:**
- `Account` entity, EF Core DbContext using **SQLite** (`Microsoft.EntityFrameworkCore.Sqlite`), migration, seed data (2-3 demo accounts)
- SQLite file path read from configuration (`appsettings.json` / environment variable), not hardcoded or `:memory:` — see ARCHITECTURE.md section 8 for the reasoning behind SQLite
- Migrations applied automatically at application startup (e.g. `context.Database.Migrate()` in `Program.cs`) — no manual CLI step should be required to get a working database on a fresh environment
- `CreditLedgerEntry` entity (immutable, no balance field on `Account` itself)
- `CreditLedgerService.GetBalance(accountId)` — sums ledger entries
- `GET /api/accounts/{id}/balance` endpoint
- Blazor Dashboard page showing the selected account's balance (account-switcher dropdown, no real auth yet)

**Tests to include:** balance sums correctly across multiple entry types; balance is zero for an account with no entries; balance handles negative (consume) and positive (grant) entries correctly.

**Agent prompt:**
> "Add an `Account` entity and a `CreditLedgerEntry` entity to CreditFlow.Domain, matching the shapes in ARCHITECTURE.md section 2. Set up EF Core in Infrastructure using the SQLite provider (`Microsoft.EntityFrameworkCore.Sqlite`), with the database file path read from configuration rather than hardcoded. Create the initial migration and seed data for 3 demo accounts with a mix of ledger entries. Apply migrations automatically at application startup (e.g. `context.Database.Migrate()` in `Program.cs`) rather than requiring a manual `dotnet ef database update` step. Add `CreditLedgerService.GetBalance(accountId)` in Application that sums ledger entries — do not add a mutable balance field anywhere. Add a `GET /api/accounts/{id}/balance` endpoint and a Blazor Dashboard page with an account-switcher dropdown showing the selected account's balance. Add xUnit tests covering balance calculation across entry types, zero-entry accounts, and mixed positive/negative entries. Don't run any git commands; I'll handle commits myself."

---

## Slice 3 — Usage Event Consumption

**Goal:** Credits can be consumed, with idempotency and insufficient-balance protection.

**Scope:**
- `UsageEvent` entity
- `UsageService.RecordUsageEvent(accountId, eventType, idempotencyKey)` — checks balance, atomically writes a Consume ledger entry, throws `InsufficientCreditsException` if balance too low
- Idempotency check: duplicate `IdempotencyKey` for the same account returns the existing result rather than double-processing
- `POST /api/accounts/{id}/usage-events` endpoint
- `POST /api/simulate/usage-event` dev-only endpoint
- "Simulate Usage Event" button on the Dashboard, updating the visible balance

**Tests to include:** successful consumption debits correctly; insufficient balance throws and writes no entry; duplicate idempotency key does not double-debit; concurrent-looking requests with the same key resolve to one entry.

**Agent prompt:**
> "Add a `UsageEvent` entity to CreditFlow.Domain. Implement `UsageService.RecordUsageEvent(accountId, eventType, idempotencyKey)` in Application: check the current balance via `CreditLedgerService`, throw `InsufficientCreditsException` if the cost exceeds it, otherwise atomically write a Consume-type `CreditLedgerEntry`. Before processing, check for an existing ledger entry with the same `IdempotencyKey` for that account and short-circuit to it if found — don't double-process. Add `POST /api/accounts/{id}/usage-events` and a dev-only `POST /api/simulate/usage-event` endpoint. Add a 'Simulate Usage Event' button to the Dashboard page that calls the simulate endpoint and refreshes the displayed balance. Add xUnit tests for: successful debit, insufficient-balance rejection (and that no entry is written), and duplicate idempotency key handling. Don't run any git commands; I'll handle commits myself."

---

## Slice 4 — Billing Webhook

**Goal:** Credits can be granted from an external event, securely and idempotently.

**Scope:**
- `BillingWebhookService.ProcessPaymentEvent(payload, signature)` — HMAC signature validation (reject before touching the ledger if invalid), then idempotency check, then writes a Grant-type ledger entry
- `POST /api/webhooks/billing` endpoint
- `POST /api/simulate/payment-webhook` dev-only endpoint (signs its own payload so it can be tested without a real payment provider)
- "Simulate Payment Webhook" button on an Admin/Demo page

**Tests to include:** valid signature + new idempotency key grants credits; invalid signature is rejected and writes no entry; duplicate idempotency key does not double-grant; malformed payload is rejected cleanly.

**Agent prompt:**
> "Implement `BillingWebhookService.ProcessPaymentEvent(payload, signature)` in Application. Validate the HMAC signature first — if invalid, reject immediately without touching the ledger. Then check for an existing ledger entry with the payload's idempotency key for that account, short-circuiting if found. Otherwise write a Grant-type `CreditLedgerEntry`. Add `POST /api/webhooks/billing` for real webhook calls and a dev-only `POST /api/simulate/payment-webhook` that constructs and signs a valid test payload itself. Add an Admin/Demo Blazor page with a 'Simulate Payment Webhook' button wired to the simulate endpoint. Add xUnit tests for: valid signature grants correctly, invalid signature is rejected with no entry written, duplicate idempotency key doesn't double-grant, and malformed payloads are rejected cleanly. Don't run any git commands; I'll handle commits myself."

---

## Slice 5 — Plans, Subscriptions & Renewal

**Goal:** Recurring plan-based credit allowances, with expiration/rollover handled automatically.

**Scope:**
- `Plan` and `Subscription` entities, seed data (Free/Pro/Enterprise plans)
- `PlanService` — plan lookup, upgrade/downgrade
- `PlanRenewalJob : BackgroundService` — on a timer, for subscriptions past their period end: expire non-rollover leftover credits (Expire-type entry), grant the new period's allowance (Grant-type entry), advance the period dates
- `GET /api/plans`, `POST /api/accounts/{id}/plan`, `GET /api/accounts/{id}/subscription`
- Blazor Plans page: current plan, upgrade/downgrade controls

**Tests to include:** renewal expires non-rollover credits correctly; renewal grants the new allowance; rollover-enabled plans carry credits forward instead of expiring them; upgrade/downgrade changes future allowance without retroactively altering past ledger entries.

**Agent prompt:**
> "Add `Plan` and `Subscription` entities to CreditFlow.Domain, seed Free/Pro/Enterprise plans (Enterprise with rollover enabled, others without). Implement `PlanService` for plan lookup and account upgrade/downgrade. Implement `PlanRenewalJob` as a `BackgroundService` that, on a timer, finds subscriptions past their `CurrentPeriodEnd` and: writes an Expire-type ledger entry for any non-rollover leftover balance, writes a Grant-type entry for the new period's allowance, and advances the subscription's period dates. Add `GET /api/plans`, `POST /api/accounts/{id}/plan`, and `GET /api/accounts/{id}/subscription` endpoints. Add a Blazor Plans page showing the current plan with upgrade/downgrade buttons. Add xUnit tests for: renewal expiring non-rollover credits, renewal granting new allowance, rollover plans carrying credits forward, and upgrade/downgrade not altering historical ledger entries. Don't run any git commands; I'll handle commits myself."

---

## Slice 6 — Usage History & Polish

**Goal:** Full transaction visibility, and the project is demo-ready end to end.

**Scope:**
- `GET /api/accounts/{id}/ledger` — paginated, filterable by entry type
- Blazor Usage History page: paginated table, filter by type (Grant/Consume/Expire/Rollover/Adjustment)
- Final pass: error handling/edge cases across all endpoints, loading states in the UI, README/architecture docs finalized

**Tests to include:** pagination returns correct page boundaries; filtering by type returns only matching entries; combined filter + pagination behaves correctly.

**Agent prompt:**
> "Add a `GET /api/accounts/{id}/ledger` endpoint supporting pagination (page/pageSize) and an optional type filter. Add a Blazor Usage History page with a paginated, filterable table showing ledger entries (date, type, amount, source, description). Add loading states for all data-fetching components across the app. Add xUnit tests for: correct pagination boundaries, type filtering, and combined filter+pagination. Don't run any git commands; I'll handle commits myself."

---

## Notes for Each Session

- Point the agent at `ARCHITECTURE.md` at the start of each session so it has the ledger pattern, naming conventions, and project structure without re-explaining it every time.
- Review the full diff before committing — this is also where you're building the "what I reviewed/changed" section of each PR description.
- If a slice's agent output includes changes outside its stated scope (e.g., Slice 3 touching webhook code), that's a signal to scope the next prompt tighter, not to accept the extra work as a bonus.
