# Security model

## Authentication and provisioning

ASP.NET Core Identity hashes passwords with its versioned salted password hasher. Registration requires a unique valid email, a display name and a 12–128-character password containing uppercase, lowercase, a digit and a symbol. Registration DTOs never accept a role assignment. Every public registration is assigned Student.

The local demo instructor is created only when the environment is Development and `Demo:Enabled=true`. Its strong password must come from external configuration. `.env` is generated locally and excluded from Git, Docker contexts and source archives. Existing accounts are never silently reset on restart. There is no public instructor-registration endpoint.

Sessions use a four-hour, non-sliding HttpOnly, SameSite=Strict cookie. Production requires Secure cookies; the local HTTP profile uses SameAsRequest on loopback. No authentication token is kept in localStorage. Every request validates the Identity security stamp. Logout changes the stamp before clearing the cookie, invalidating previously issued cookies and all sessions for that account. Five failed password attempts cause a ten-minute lockout.

## Request and data boundaries

- Same-origin browser/API access; no permissive CORS policy. Every POST/PUT/PATCH/DELETE requires the antiforgery cookie plus a fresh `X-CSRF-TOKEN`. Tokens are refreshed after authentication changes.
- Auth endpoints allow 20 requests per minute per connection IP; attempt endpoints allow 120 per minute per authenticated user, with no request queue. The local Nginx proxy shares an auth IP bucket. Production proxy trust and edge limiting need explicit deployment configuration.
- Kestrel and Nginx bound request bodies to 128 KiB. The server independently bounds strings, duration, percentages, choices, question counts and marks.
- Instructor endpoint groups require Instructor; student groups require Student. Ownership checks use authenticated claims. Unknown/foreign resource IDs return 404; unauthenticated/role-denied requests return 401/403.
- Player responses explicitly project text/choice IDs and saved selections, omitting keys. Result feedback requires finalization and the version's `ShowFeedback` policy. Instructor summaries require ownership.
- All scoring ignores body-provided marks, percentages, user IDs or privileges. Parameterized EF/SQL queries prevent SQL injection. React renders text without raw HTML injection.
- Row locks, revisions, unique constraints, transactions and immutable snapshots protect publication, answer writes and finalization. Results and finalized answers are guarded at the database layer.

Unexpected errors return safe problem details without stack traces or connection strings. JSON structured logs omit request bodies and sensitive-data logging is not enabled. Nginx sends CSP, anti-framing, nosniff and referrer headers. Responses containing API data use no-store caching.

## Verified controls and operating boundary

The real PostgreSQL tests exercise registration role injection, null input, login failure, lockout, CSRF denial, logout cookie replay, rate limiting, ownership, answer-key protection, version isolation, server-only scoring, expiration, duplicate/concurrent finalization and direct database tampering. Playwright additionally verifies unauthorized browser/API access and cross-student attempt isolation.

The supplied deployment is local HTTP on loopback with development-only provisioning. There is no public deployment or internship submission. Public operation needs managed HTTPS, production account provisioning, least-privilege database access, encrypted/protected data-protection keys, trusted proxy configuration, monitoring and backup/recovery validation. Password recovery, email verification, MFA, SSO and formal penetration-test certification are outside this task's scope.
