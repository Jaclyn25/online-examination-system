# Actual application screenshots

Captured from the running production Docker application on **7 November 2026**, using a fresh isolated `aperture-capture` instance at `http://127.0.0.1:18083`. The main verified application remains on port 18082. Screens show fictional seeded records plus an actual student attempt. They were captured and visually inspected, not generated or reconstructed.

| Required surface | File |
| --- | --- |
| Login | [login.png](../docs/screenshots/login.png) |
| Instructor dashboard | [instructor-dashboard.png](../docs/screenshots/instructor-dashboard.png) |
| Exam management | [exam-management.png](../docs/screenshots/exam-management.png) |
| Question editor | [question-editor.png](../docs/screenshots/question-editor.png) |
| Student exam player | [student-exam-player.png](../docs/screenshots/student-exam-player.png) |
| Results | [results.png](../docs/screenshots/results.png) |
| Reports | [reports.png](../docs/screenshots/reports.png) |
| Mobile interface | [mobile.png](../docs/screenshots/mobile.png) |
| Additional mobile navigation | [mobile-navigation.png](../docs/screenshots/mobile-navigation.png) |

`docs/screenshots/capture-record.json` records the base URL, individual timestamps and viewports. The result capture asserts the real 20/30, 66.67% server score. The question-editor capture shows a real unsaved authoring form; no screenshot data was inserted into the UI.

To reproduce on an isolated seeded instance without touching main data, set `WEB_PORT=18083` and `DB_PORT=54332` in the current process, then run `docker compose -p aperture-capture up --build --detach --wait --wait-timeout 180`. The distinct project creates its own network and volumes. From `frontend`, set `SCREENSHOT_BASE_URL=http://127.0.0.1:18083`, optionally `PLAYWRIGHT_CHANNEL=msedge`, then run `node scripts/capture.mjs`. It reads the instructor credential from the environment or ignored local `.env`, creates a new fictional student and captures real pages. Browser binaries must be installed. Do not target a real student dataset.

After capture, `docker compose -p aperture-capture stop` stops only that isolated instance and preserves its data. Counts may grow if capture is repeated because it performs genuine registration and submission.
