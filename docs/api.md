# API guide

Base URL: `http://127.0.0.1:18082/api`. Machine-readable OpenAPI: `http://127.0.0.1:18082/openapi/v1.json`.

JSON request/response properties use camelCase. IDs are UUIDs except Identity user IDs. Timestamps are ISO-8601 UTC. Authenticated access uses the `aperture.session` cookie. Before every write, GET `/auth/csrf`, retain its cookie and send the returned `token` as `X-CSRF-TOKEN`. Refresh it after login/logout. Do not send score values or user/instructor identities to select authorization context.

| Method/path | Role | Behavior |
| --- | --- | --- |
| GET `/auth/csrf` | Public | Antiforgery request token and cookie |
| POST `/auth/register` | Public | `email`, `password`, `displayName`; creates Student only |
| POST `/auth/login` | Public | `email`, `password`; session cookie, 204 |
| POST `/auth/logout` | Authenticated | Revoke sessions and clear cookie, 204 |
| GET `/auth/me` | Authenticated | ID, email, display name and role |
| GET/POST `/instructor/exams` | Instructor | List owned exams or create validated draft |
| GET/PUT `/instructor/exams/{id}` | Owner | Read/update draft metadata |
| GET/POST `/instructor/exams/{id}/questions` | Owner | Read keys for own exam; add draft question |
| PUT/DELETE `/instructor/exams/{id}/questions/{questionId}` | Owner | Edit/delete draft question; DELETE uses `?revision=N` |
| PUT `/instructor/exams/{id}/questions/order` | Owner | Complete distinct `questionIds` list and current `revision` |
| POST `/instructor/exams/{id}/publish` | Owner | `revision`, `showFeedback`; create immutable version |
| PUT `/instructor/exams/{id}/status` | Owner | `status` Draft or Closed and current `revision` |
| GET `/student/exams` | Student | Available latest published versions, instructions/counts, no keys |
| POST `/student/exams/{id}/attempts` | Student | Create one attempt or restore active attempt |
| GET `/student/attempts/{id}` | Attempt owner | Player projection, saved answers and authoritative times |
| PUT `/student/attempts/{id}/answers/{questionId}` | Attempt owner | `choiceId` or null, current attempt `revision` |
| POST `/student/attempts/{id}/submit` | Attempt owner | Atomic server scoring; duplicate submissions replay result |
| GET `/student/attempts/{id}/result` | Attempt owner | Final result and policy-authorized feedback; active returns 409 |
| GET `/student/history` | Student | Own active/completed attempts and results |
| GET `/instructor/reports` | Instructor | Owned aggregate metrics, distributions and exam performance |
| GET `/instructor/exams/{id}/report` | Owner | Exam metrics and individual summaries |
| GET `/instructor/attempts/{id}/result` | Exam owner | Authorized finalized attempt feedback |
| GET `/health` | Public | Database connectivity/readiness; 200 or 503 |

Create/update examination:

```json
{"title":"HTTP Foundations","description":"Choose one answer for every question.","durationMinutes":30,"passingPercentage":60,"revision":1}
```

Create/update question:

```json
{"text":"Which method retrieves a resource?","marks":5,"choices":[{"text":"GET","isCorrect":true},{"text":"POST","isCorrect":false}],"revision":1}
```

Every metadata/question/status change increments the exam revision. Question writes return the new revision. Reload the exam after each successful write before preparing another write. Answer writes return the incremented attempt revision. A stale revision returns 409. Publishing requires 1–100 valid questions, with 2–8 distinct nonempty choices and exactly one key per question.

Attempt responses contain `serverNow`, `deadline`, `startedAt`, `status`, `revision` and ordered questions with `selectedChoiceId`. They never contain `isCorrect` or `correctChoiceId`. Result responses include earned/possible marks, decimal percentage, pass/fail and correct/incorrect/unanswered counts. Feedback is null if disabled for that version.

Error status codes: **400** invalid input/CSRF, **401** no valid session/bad credentials, **403** wrong role, **404** missing or foreign resource, **409** stale revision/unavailable exam/finalized attempt, **413** excessive request size, **429** rate limit, **500** safe unexpected-error response. Problem responses include a title and, for form validation, an `errors` dictionary. No stack trace or answer key is included in unauthorized responses.
