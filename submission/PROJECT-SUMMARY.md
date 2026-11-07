# Task 5 delivery: Aperture Exams

**Project:** Online Examination System, Auspify Technologies Internship. **Repository:** [Jaclyn25/online-examination-system](https://github.com/Jaclyn25/online-examination-system), existing and private at initial inspection. GitHub reported public visibility at final verification; no visibility change was performed by this implementation. **Local application:** [localhost:18082](http://127.0.0.1:18082).

The company's corrected requirements replace the unrelated original PDF workflow. All five corrected areas are implemented end to end:

| Official requirement | Delivered behavior |
| --- | --- |
| User authentication and registration | Identity student registration, secure login/logout, profile, controlled local instructor provisioning and role authorization |
| Examination creation and management | Owned drafts, validated metadata, question authoring/reordering, immutable publication and Draft/Published/Closed lifecycle |
| Question display and navigation | Responsive focused player, question map, previous/next, saved answer changes, refresh recovery and authoritative deadlines |
| Automated result calculation | Server-only weighted scoring, exact pass boundaries, unanswered handling, atomic duplicate/concurrent submission and expiration |
| Score reports and examination summaries | Student history/feedback and instructor dashboard, distributions, per-exam reports and individual summaries |

The stack includes ASP.NET Core/C#/EF Core/PostgreSQL/Identity/OpenAPI/structured logging; React/TypeScript/Vite/Tailwind/Router/Query/Hook Form/Zod/Recharts; xUnit/PostgreSQL/Vitest/RTL/Playwright; Docker Compose and GitHub Actions.

Validation evidence records **44 backend tests, 16 frontend tests, four native/CI browser flows and four Docker browser flows**, successful production builds/lint/TypeScript, healthy containers, authenticated smoke checks and persisted sessions after restart. See [TEST-RESULTS](TEST-RESULTS.md) for executed evidence and [DELIVERY](DELIVERY.md) for the actual PR chain.

Nine actual application captures, including the eight required surfaces and an additional mobile-navigation view, are linked in [SCREENSHOTS](SCREENSHOTS.md). They use a fresh isolated production Docker instance with fictional data and a real student attempt, not generated artwork or fabricated metrics.

Start with the root [README](../README.md). Architecture, database design, API, local/demo setup, security and testing guides live in `docs/`. Generate the reproducible source ZIP and checksum using `scripts/package-source.ps1`; output goes to ignored `artifacts/`. Secrets, build artifacts, database contents and browser traces are excluded.

No repository was created, visibility was not changed, reference repositories were not modified, no public deployment occurred and nothing was submitted to Auspify automatically. Known policy/operational limitations are documented in the README.
