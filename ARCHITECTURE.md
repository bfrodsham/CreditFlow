# CreditFlow — Architecture

A SaaS usage-credits platform built to demonstrate clean architecture, financial-grade data integrity, and iterative, test-driven delivery in ASP.NET Core.

---

## 1. Solution Structure

```
CreditFlow.Domain          → Entities, enums, pure domain logic (no dependencies)
CreditFlow.Application     → Services, DTOs, interfaces, validation
CreditFlow.Infrastructure  → EF Core, repositories, webhook handling, background jobs
CreditFlow.Web             → Blazor Server UI + minimal API endpoints
CreditFlow.Tests           → xUnit — Domain + Application coverage
```

Dependencies flow inward: `Web` → `Infrastructure` → `Application` → `Domain`. `Domain` has no outward dependencies, which keeps core business rules testable in isolation from EF Core, HTTP, or any framework concern.

---

## 2. Domain Model

### Account
Customer/organization. `Id`, `Name`, `Email`, `CreatedAt`.

### Plan
`Id`, `Name` (Free / Pro / Enterprise), `MonthlyCreditAllowance`, `AllowsRollover`, `PricePerExtraCredit`.

### Subscription
`Id`, `AccountId`, `PlanId`, `CurrentPeriodStart`, `CurrentPeriodEnd`, `Status`.

### CreditLedgerEntry — the architectural core
**Design decision:** balance is never stored as a mutable field. It is always derived by summing ledger entries for an account. This mirrors how real financial/billing systems work: every state change is an immutable, auditable fact, not an overwrite. It also eliminates a whole category of race-condition bugs that a `Balance` column invites.

Fields: `Id`, `AccountId`, `Amount` (signed), `Type` (Grant / Consume / Expire / Rollover / Adjustment), `Source` (SubscriptionRenewal / CreditPackPurchase / UsageEvent / ManualAdjustment), `IdempotencyKey`, `ReferenceId`, `Description`, `CreatedAt`.

### CreditPack
One-time purchasable top-ups. `Id`, `Name`, `CreditAmount`, `Price`.

### UsageEvent
A simulated feature-use event that consumes credits. `Id`, `AccountId`, `EventType`, `CreditCost`, `IdempotencyKey`, `OccurredAt`.

---

## 3. Core Services

| Service | Responsibility |
|---|---|
| `CreditLedgerService.GetBalance(accountId)` | Sums all ledger entries — single source of truth for balance |
| `BillingWebhookService.ProcessPaymentEvent(payload, signature)` | Validates HMAC signature, checks idempotency key, grants credits |
| `UsageService.RecordUsageEvent(accountId, eventType, idempotencyKey)` | Checks balance, atomically debits credits, throws on insufficient balance |
| `PlanRenewalJob : BackgroundService` | On a timer: expires non-rollover leftover credits, grants the next period's allowance |
| `PlanService` | Plan lookup, upgrade/downgrade logic |

**Idempotency pattern:** every entry point that writes a ledger entry (webhook, usage event) first checks whether an entry with the same `IdempotencyKey` already exists for that account, and short-circuits to the existing result if so. This guards against duplicate webhook delivery and retried requests double-crediting or double-charging — a correctness requirement in any real billing system, not an edge case.

**Design decision — signature check ordering:** HMAC validation always happens before the idempotency check, so an unsigned or forged request never touches the ledger regardless of what key it presents.

---

## 4. API Surface

```
POST   /api/webhooks/billing              → inbound payment event, HMAC-signed, idempotent
GET    /api/accounts/{id}/balance
GET    /api/accounts/{id}/ledger          → paginated transaction history
POST   /api/accounts/{id}/usage-events    → simulate a credit-consuming action
GET    /api/plans
POST   /api/accounts/{id}/plan            → upgrade/downgrade
GET    /api/accounts/{id}/subscription
POST   /api/simulate/payment-webhook      → dev-only: fires a fake signed webhook for demo purposes
POST   /api/simulate/usage-event          → dev-only: fires a fake usage event for demo purposes
```

The `/api/simulate/*` endpoints exist because a live demo can't easily fire a real signed webhook — they make the whole earn/consume/expire flow visible and clickable from the UI.

---

## 5. UI

Blazor Server, single project, no separate frontend stack:
- **Dashboard** — balance, plan, renewal date
- **Usage History** — paginated, filterable ledger table
- **Plans** — current plan, upgrade/downgrade, credit pack purchase
- **Admin/Demo** — simulate-webhook and simulate-usage-event controls

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

---

## 8. CI/CD & Deployment

**Pipeline (GitHub Actions):** two jobs — `build-and-test` (restore, build, `dotnet test`) and `deploy` (gated on `build-and-test` succeeding and only on push to `main`). Tests are a hard gate; a failing test blocks deployment.

**Hosting:** Render, Docker-based (ASP.NET Core has no native Render buildpack, so a multi-stage Dockerfile builds and publishes the app). Deploy is triggered via a Render Deploy Hook called from the GitHub Actions `deploy` job, rather than Render's default auto-deploy-on-push — this keeps the test gate meaningful instead of bypassable.

**Data:** SQLite, chosen deliberately for a zero-infrastructure-cost demo context. Render's free tier does not guarantee filesystem persistence across deploys, so the database resets to seed data on each deploy — an accepted tradeoff for a portfolio project, not a production data strategy. A production deployment would use managed PostgreSQL or SQL Server instead.

**Git workflow:** short-lived feature branches per slice, merged via PR into `main`. Each PR includes a summary, the prompts used with AI assistance, what was reviewed/changed from the AI's output, testing notes, and any relevant design decisions — see `README.md` for the fuller rationale.
