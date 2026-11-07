# Verification

Tests exercise the real API and PostgreSQL. There is no in-memory provider or database mock used to claim integration coverage.

## Backend

From the repository root, with the Aperture database running and local `.env` generated:

```powershell
./scripts/test.ps1
```

Alternatively set `EXAMS_TEST_CONNECTION` to a PostgreSQL connection with CREATEDB permission and run `dotnet test -c Release`. Each factory creates a unique database, applies every migration and drops its own database on disposal. User creation in setup uses Identity; tested flows use HTTP clients with real cookies/CSRF. Injected `TimeProvider` clocks allow exact deadline tests without sleeping.

The **44 tests** cover registration/login/logout, failures, hashing, lockout, role injection, CSRF, rate limits, exam creation/editing/configuration/ownership, question validation and lifecycle, publishing rules, immutable snapshots, protected keys, start/restore, answer changes/clearing, invalid choices, student isolation, concurrent start, exact weighted scoring/pass boundaries, unanswered questions, exact expiration, worker finalization, duplicate/concurrent submissions, expiration/save races, policy-controlled feedback, reporting ownership/reconciliation and database history tampering/deletion.

## Frontend

From `frontend`, run `npm ci`, `npm run typecheck`, `npm run build`, `npm run lint` and `npm test`. The **16 Vitest/React Testing Library tests** cover authentication forms, server errors, CSRF-aware API requests, exam configuration validation, question keys/choices, question navigation, answer selection, saved state, countdown/one-time expiration, final confirmation, result display and loading/retry states. Frontend unit tests use controlled callbacks/fetch stubs; they do not stand in for database tests.

## Browser

Start the native API with `scripts/start-api.ps1`. From `frontend`, install Chromium with `npx playwright install chromium`, then run `npm run test:e2e`. Playwright starts Vite if needed. Local Edge is supported with `PLAYWRIGHT_CHANNEL=msedge`.

Set `E2E_BASE_URL=http://127.0.0.1:18082` to target the production Docker app. Demo credentials come from the environment or ignored local `.env`. Every test-generated student credential is cryptographically generated at runtime. Four independent flows verify:

1. Instructor creates a draft, authors a question and publishes through the UI.
2. Student registers, takes a two-question exam, restores a saved answer after refresh, submits and receives 5/5 and 100%.
3. Anonymous/role denial, protected keys before finalization and cross-student attempt/read/save/submit isolation.
4. Mobile navigation and exam answering/navigation without horizontal document overflow at 390×844.

Browser tests create fictional exams and learners. Run against an isolated dataset when preserving a pristine demo matters. Traces are retained only for failures and are ignored/excluded from source packages because they can contain ephemeral session material.

## Containers and CI

`docker compose up --build --detach --wait --wait-timeout 180` builds production images and validates health. `node scripts/smoke.mjs` verifies web, database API, OpenAPI, registration/login/logout, role denial, key projection, unanswered scoring and duplicate submission. Set `SMOKE_RESTART_CHECK=true` to additionally restart the API and verify session-key persistence. This restart interrupts only the Aperture API.

GitHub Actions runs backend build/PostgreSQL tests, frontend install/typecheck/build/lint/RTL, real Chromium flows and Docker build/smoke/restart in separate jobs. Backend TRX, frontend JUnit, browser JSON/report and container diagnostics are uploaded for 14 days. There is no public deployment job. Persistent reviewed evidence is included in `submission/verification`; final run links are in the delivery record/completion report.

These checks are functional/security regression evidence, not a formal penetration test, accessibility certification, load test or disaster-recovery certification.
