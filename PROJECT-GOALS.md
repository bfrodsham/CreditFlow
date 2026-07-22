# About This Project

## What This Is

CreditFlow is a SaaS usage-credits platform — the pattern behind API billing, cloud compute credits, and metered subscription usage. Accounts hold a credit balance that's granted through subscription renewals or one-time credit pack purchases, and consumed through usage events, with automatic expiration on unused credits at the end of each billing period.

It's built with ASP.NET Core, EF Core, and Blazor Server, with a CI/CD pipeline deploying to a live environment.

## Why I Built It

My background is 3.5 years of professional experience in Java, and I built this project to demonstrate two things side by side: that I can work effectively in .NET and C#, and that I can use AI-assisted development tools deliberately and well — not just to generate code, but as part of a disciplined engineering workflow.

I chose a usage-credits/ledger domain specifically because it's technically meatier than a typical CRUD demo. It required real decisions about data integrity (an immutable, append-only ledger instead of a mutable balance field), idempotency (safely handling retried webhook deliveries and requests), and background processing (scheduled credit expiration and renewal) — the kind of problems that come up in production systems, not just tutorials.

## What I Was Aiming to Demonstrate

- **Transferable engineering judgment, not just new syntax.** The architecture decisions here — layered/clean architecture, an immutable ledger pattern, idempotent webhook handling — are things I'd apply in any language. Learning .NET's specific idioms (EF Core vs. Hibernate, LINQ vs. JPQL, `BackgroundService` vs. Spring's scheduled tasks) was the new part; the underlying engineering instincts carried over directly.
- **Deliberate, accountable use of AI tools.** I used GitHub Copilot's agent mode for scoped, bounded implementation tasks — one feature slice at a time, each reviewed in full before merging — and its ask mode for learning .NET-specific patterns along the way. Every pull request includes the prompts I used, what I changed or corrected from the AI's output, and the reasoning behind non-obvious decisions, so the process is as visible as the code.
- **Iterative delivery.** The project was built as a walking skeleton (deployment pipeline proven first) followed by thin vertical slices — each one a complete, tested, demoable increment — rather than building the entire backend before anything was runnable. That's deliberately closer to how a team ships incrementally than to a single big-bang build.
- **Pragmatic tradeoffs under real constraints.** A few decisions here were shaped by building this solo, in a short timeframe, at zero cost — SQLite instead of a managed database, a lightweight account-switcher instead of full identity/auth, Render instead of Azure after hitting a subscription-level quota limitation partway through. Each of those is called out explicitly in `ARCHITECTURE.md` as a deliberate scope decision, along with what a production version would do differently.

## Tech Stack

- **Backend:** ASP.NET Core, EF Core, xUnit
- **Frontend:** Blazor Server
- **CI/CD:** GitHub Actions (build, test, gated deploy)
- **Hosting:** Render (Docker-based deployment)
- **AI tooling:** GitHub Copilot (agent mode for implementation, ask mode for learning)

See `ARCHITECTURE.md` for the full technical design.
