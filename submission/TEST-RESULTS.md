# Actual verification results

Verified **7 November 2026**. Results below describe executed tests and real running services, not inferred passes.

| Gate | Observed result | Persistent evidence |
| --- | --- | --- |
| Backend Release build | Pass, zero warnings/errors locally | CI backend job |
| xUnit / real PostgreSQL | **44 passed, 0 failed, 0 skipped** | `verification/backend-ci.trx`, local `backend-local.trx` |
| Frontend TypeScript / production build / ESLint | Pass locally and CI | CI frontend job |
| Vitest / RTL | **16 passed** | `verification/frontend.xml` |
| Playwright against native API | **4 passed** locally and CI Chromium | `verification/playwright-ci.json` |
| Playwright through Docker/Nginx | **4 passed** locally with Edge | `verification/playwright-docker.json` |
| Docker builds / startup | API and web images built; PostgreSQL/API/web healthy | `verification/docker-status.json`, CI container artifact |
| Authenticated Docker smoke | Pass, including role/key protection, scoring, duplicate submission and logout | `verification/docker-smoke.json` |
| Session persistence after API restart | Pass | `restartSessionVerified: true` in smoke evidence |
| npm / NuGet audit | No known advisories reported by the executed audits | `verification/npm-audit.json`, `nuget-audit.json` |
| Actual screenshots | Nine captured and visually inspected | `docs/screenshots/` and its capture record |
| Repository privacy observation | Initially private; final GitHub read reports public; not changed by this implementation | `verification/repository-state.json`; delivery record |

Verified complete application-code CI: [Validation run 42404079799](https://github.com/Jaclyn25/online-examination-system/actions/runs/42404079799), PR #13. Backend, frontend, Chromium, container/restart and GitGuardian checks passed. The subsequent documentation PR and final main push have their own complete workflow; the final run is linked in the completion report and available in [workflow history](https://github.com/Jaclyn25/online-examination-system/actions/workflows/ci.yml).

The first browser suite run caught an overly exact history locator: the accessible cell includes its version label. The locator was corrected and the full suite rerun successfully. A question-choice persistence issue was caught and fixed before its feature PR. Secret scanning flagged two non-secret expressions (Compose required-variable explanation and dynamic test-password interpolation); both were rewritten and rescanned successfully. No failure was bypassed for merge.

TRX output/log payloads were removed from committed copies to keep evidence compact and avoid carrying verbose runtime material; test identities, outcomes, counters and timings remain. Original CI artifacts retain logs for 14 days. JSON/JUnit evidence contains actual test cases/timestamps. Test-created fictional records cause counts to differ from the clean screenshot instance.

The screenshot scenario deliberately submits four correct, one incorrect and one unanswered five-mark questions: **20/30 = 66.67%, Passed** at a 60% threshold. This was asserted against the real result before capture. The illustration on the login page is application UI; every screenshot itself is a browser capture.

No formal accessibility certification, penetration-test certification, performance/load guarantee or backup/recovery certification is claimed.
