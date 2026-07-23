# CreditFlow — Architecture

A SaaS usage-credits platform built to demonstrate clean architecture, financial-grade data integrity, and iterative, test-driven delivery in ASP.NET Core.

---

## 1. Solution Structure

```
src/CreditFlow             → Single ASP.NET Core + Blazor Server project
	/Domain                  → Entities + enums
	/Application             → Services + query/read logic
	/Infrastructure          → EF Core DbContext, migrations, seed data
	/Components              → Blazor UI
tests/CreditFlow.Tests     → xUnit tests
```

The original plan described separate projects (`Domain`, `Application`, `Infrastructure`, `Web`).
Current implementation keeps those boundaries as folders inside one project. This is an intentional simplification for early slices, while preserving the same conceptual architecture.

---

## 2. Domain Model

### Account
Customer/organization. `Id`, `Name`, `Email`, `CreatedAt`.

Status: implemented.

### Plan
`Id`, `Name` (Free / Pro / Enterprise), `MonthlyCreditAllowance`, `AllowsRollover`, `PricePerExtraCredit`.

Status: planned (Slice 5), not implemented yet.

### Subscription
`Id`, `AccountId`, `PlanId`, `CurrentPeriodStart`, `CurrentPeriodEnd`, `Status`.

Status: planned (Slice 5), not implemented yet.

### CreditLedgerEntry — the architectural core
**Design decision:** balance is never stored as a mutable field. It is always derived by summing ledger entries for an account. This mirrors how real financial/billing systems work: every state change is an immutable, auditable fact, not an overwrite. It also eliminates a whole category of race-condition bugs that a `Balance` column invites.

Fields: `Id`, `AccountId`, `Amount` (signed), `Type` (Grant / Consume / Expire / Rollover / Adjustment), `Source` (SubscriptionRenewal / CreditPackPurchase / UsageEvent / ManualAdjustment), `IdempotencyKey`, `ReferenceId`, `Description`, `CreatedAt`.

Status: implemented.

Note: `Type` and `Source` enums already include future-slice values. This was done early so later slices can build without enum churn.

### CreditPack
One-time purchasable top-ups. `Id`, `Name`, `CreditAmount`, `Price`.

Status: planned, not implemented yet.

### UsageEvent
A simulated feature-use event that consumes credits. `Id`, `AccountId`, `EventType`, `CreditCost`, `IdempotencyKey`, `OccurredAt`.

Status: planned (Slice 3), not implemented yet.

---

## 3. Core Services

| Service | Responsibility |
|---|---|
| `CreditLedgerService.GetBalance(accountId)` | Implemented as pure in-memory ledger summation for core balance rules and tests |
| `AccountQueryService.GetAccountsAsync()` | Implemented read query for account dropdown data |
| `AccountQueryService.GetBalanceAsync(accountId)` | Implemented EF-backed read query used by API/UI |
| `BillingWebhookService.ProcessPaymentEvent(payload, signature)` | Planned (Slice 4) |
| `UsageService.RecordUsageEvent(accountId, eventType, idempotencyKey)` | Planned (Slice 3) |
| `PlanRenewalJob : BackgroundService` | Planned (Slice 5) |
| `PlanService` | Planned (Slice 5) |

**Idempotency pattern:** every entry point that writes a ledger entry (webhook, usage event) first checks whether an entry with the same `IdempotencyKey` already exists for that account, and short-circuits to the existing result if so. This guards against duplicate webhook delivery and retried requests double-crediting or double-charging — a correctness requirement in any real billing system, not an edge case.

**Design decision — signature check ordering:** HMAC validation always happens before the idempotency check, so an unsigned or forged request never touches the ledger regardless of what key it presents.

---

## 4. API Surface

```
GET    /api/accounts                       → implemented (added to support dashboard account switcher)
GET    /api/accounts/{id}/balance         → implemented

POST   /api/webhooks/billing              → planned (Slice 4)
GET    /api/accounts/{id}/ledger          → planned (Slice 6)
POST   /api/accounts/{id}/usage-events    → planned (Slice 3)
GET    /api/plans                         → planned (Slice 5)
POST   /api/accounts/{id}/plan            → planned (Slice 5)
GET    /api/accounts/{id}/subscription    → planned (Slice 5)
POST   /api/simulate/payment-webhook      → planned (Slice 4)
POST   /api/simulate/usage-event          → planned (Slice 3)
```

The `/api/simulate/*` endpoints are still planned. Their purpose remains unchanged: make signed-webhook and usage flows demoable without external systems.

---

## 5. UI

Blazor Server, single project, no separate frontend stack:
- **Dashboard** — balance, plan, renewal date
- **Usage History** — paginated, filterable ledger table
- **Plans** — current plan, upgrade/downgrade, credit pack purchase
- **Admin/Demo** — simulate-webhook and simulate-usage-event controls

Current implementation status:
- **Dashboard** is implemented with account switcher + balance display.
- **Usage History**, **Plans**, and **Admin/Demo** pages are planned and not implemented yet.

Auth is a lightweight account-switcher for demo purposes, not full identity — a deliberate scope decision, not an oversight.

---

## 6. Delivery Approach: Vertical Slices

Rather than building all of Domain → Application → Infrastructure → UI before anything is runnable, the project is built as a walking skeleton (deployment pipeline + minimal app, proven end-to-end first) followed by thin vertical feature slices — each one runnable, tested, and demoable on its own:

1. Skeleton: minimal app + CI/CD pipeline, deployed
2. Balance display, end-to-end
3. Usage event consumption, end-to-end (idempotency, insufficient-balance handling)
4. Billing webhook, end-to-end (HMAC validation, idempotency)
5. Plans & subscription renewal, end-to-end
6. Usage history with filtering/pagination, polish

This mirrors how a real team iterates against a working system, and it means the project is never in a state of "half-built and undemoable" for more than a day.

---

## 7. Testing

xUnit, focused on `Domain` and `Application` logic rather than framework plumbing:
- Ledger balance calculation
- Duplicate idempotency key handling (webhook and usage event paths)
- Insufficient-credit rejection
- Renewal job correctness (expiration vs. rollover)

Current implementation status:
- Implemented now: balance-calculation tests.
- Planned for later slices: idempotency, insufficient-credit, and renewal tests.

---

## 8. CI/CD & Deployment

**Pipeline (GitHub Actions):** two jobs — `build-and-test` (restore, build, `dotnet test`) and `deploy` (gated on `build-and-test` succeeding and only on push to `main`). Tests are a hard gate; a failing test blocks deployment.

**Hosting:** Render, Docker-based (ASP.NET Core has no native Render buildpack, so a multi-stage Dockerfile builds and publishes the app). Deploy is triggered via a Render Deploy Hook called from the GitHub Actions `deploy` job, rather than Render's default auto-deploy-on-push — this keeps the test gate meaningful instead of bypassable.

**Data:** SQLite, chosen deliberately for a zero-infrastructure-cost demo context. Render's free tier does not guarantee filesystem persistence across deploys, so the database resets to seed data on each deploy — an accepted tradeoff for a portfolio project, not a production data strategy. A production deployment would use managed PostgreSQL or SQL Server instead.

**Git workflow:** short-lived feature branches per slice, merged via PR into `main`. Each PR includes a summary, the prompts used with AI assistance, what was reviewed/changed from the AI's output, testing notes, and any relevant design decisions — see `README.md` for the fuller rationale.
