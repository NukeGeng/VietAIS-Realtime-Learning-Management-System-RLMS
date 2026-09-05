# Foundation implementation

Scope: GOAL Phase 0 through the Phase 3 learning-activity slice. Business modules are added only when a use case exists.
The initial projects are the API composition root, technical Infrastructure,
and Identity (authenticated actor and permission contract). No empty modules.

## Decisions

- .NET 10, using the installed SDK; package versions and restore lock files are committed.
- ASP.NET Core cookie authentication for the first-party web application. Cookies are
  HttpOnly, Secure and SameSite=Lax. Unsafe API requests require an antiforgery token.
- Identity uses a one-time, explicitly configured bootstrap token to create the first
  organization and administrator. The token is never a fallback credential and an empty
  token disables bootstrap.
- Login resolves the organization in the control tenant, verifies the account password,
  and issues a first-party cookie containing a security stamp and permission claims.
  Cookie validation reloads the organization, account, and role permissions so disabling
  either the account or organization revokes existing sessions.
- Organization and user IDs must be non-empty GUIDs in the authenticated principal.
  `ITenantContext` resolves the authenticated organization once at the infrastructure
  boundary and exposes the conjoined tenant ID to the session factory. Never accept a
  tenant selector from an arbitrary request header.
- PostgreSQL/Marten stores durable data. Wolverine/Marten integration supplies a
  durable messaging boundary without introducing a broker. Business handlers will
  opt into transactional messaging as their use cases are implemented.
  Wolverine 6's separate RuntimeCompilation package is required to compile discovered
  handlers during startup; it is included explicitly for the development foundation.
- Redis is independently configured and is not a dependency of database writes.
- S3-compatible storage holds binary data. AWS SDK is used for signing and object
  access rather than implementing the S3 signature protocol. Credentials come from
  environment/configuration, never source control.
- Liveness only checks that the API is running. Core readiness checks PostgreSQL.
  Storage/Redis dependency health is reported separately so those failures do not
  remove answer/submission APIs from service.
- Media configuration defines an external SFU endpoint; LiveSession join returns only
  session metadata and an opaque access token. No media payload is stored in the API.

## Verification cases, defined before implementation

1. Anonymous `/api/identity/me` returns 401, not an HTML redirect.
2. Valid authenticated actor returns only its own user/organization/permissions.
3. Missing, malformed or empty identity/organization claims fail authorization.
4. Permission checks use permission claims, not role names.
5. Tenant A cannot load/query Tenant B's document, even for the same document ID.
6. A committed document survives a new database session.
7. Redis round-trip and S3 binary round-trip work against real local services.
8. Liveness still works when a peripheral dependency is unavailable; health output
   does not expose credentials or exception details.
9. Cookie authentication rejects a modified cookie. Unsafe authenticated requests
   without an antiforgery token are rejected.

## Phase 1 course slice

The Academics slice now owns `Course`, `CourseModule`, and `Lesson` documents. Course
codes are unique within an organization, and course structure is loaded only for the
requested course. Course creation, editing, publishing, and content authoring use
permission policies; the student role is not granted an unfiltered course catalogue
before enrollment filtering exists.

`LearningClass`, `Enrollment`, `ClassTeacherAssignment`, and `ClassSchedule` are
separate tenant-scoped documents. A class may reference only a published course,
student class reads are filtered through active enrollment, and schedule creation
requires an active teacher assignment. Enrollment and teacher assignment IDs are
deterministic so retries cannot create multiple active records for the same pair.

## Phase 2 LiveSession foundation

The Classroom slice now owns a tenant-scoped `LiveSession` document with a small
durable lifecycle: scheduled, started, and ended. Session creation requires a class
teacher assignment; teachers can manage only their assigned class, while
administrators retain organization management access. Students and assigned teachers
must pass the class membership check before reading or joining a session.

Join returns the session metadata and an opaque media access token for the configured
external SFU boundary. It does not carry media payloads. HTTP join/leave and the
SignalR hub use Redis only for short-lived presence keys, with organization and
session IDs in the key. Redis failures are logged and reported as
`PresenceRegistered = false` without failing the durable session or join response.
The hub is scoped to one session route and broadcasts participant metadata only.

## Phase 2 classroom interactions

Attendance is a tenant-scoped durable Marten document keyed by
`(LiveSessionId, StudentId)`. HTTP and SignalR join/leave paths enqueue small
Wolverine messages after presence registration; handlers update first/last timestamps,
accumulate duration, classify late/absent records at session end, and remain
idempotent when delivery is retried. The three attendance message types share a
durable sequential local queue, preserving join/leave/end order for a session while
allowing the live request to return immediately. Teacher adjustments keep the actor
and reason for audit. Redis presence is never used as the attendance source of truth.

Chat messages are owned by the Communication module and stored as tenant-scoped
documents. The API persists a message before broadcasting it to the session group;
clients can reload the message snapshot if SignalR is unavailable. Raise-hand state
is a small tenant-scoped Classroom document with deterministic identity per student
and session, and its broadcast is best-effort after persistence.

## Identity verification

The Identity slice verifies one-time bootstrap and secret handling, normalized account
validation, password login, permission-scoped account creation, duplicate protection,
disabled-account and suspended-organization revocation, and role lookup without a
test-only authentication bypass.

## Remaining system work

The foundation, identity, academics, and LiveSession flows are covered by
unit/integration tests, including durable local message delivery, tenant isolation,
authenticated SignalR negotiation, session membership, lifecycle transitions, and
ephemeral presence. No test authentication bypass is exposed by the application.
Phase 2 chat, raise hand, and attendance are covered by integration tests. Phase 3
learning activity is covered by integration tests for question versioning, published
activity snapshots, direct in-class publish, pause/resume, event-sourced attempts,
answer changes, and submission. Phases 4–8 and GOAL flows A–F remain required.

## Phase 3 learning activity

Assessment owns the tenant-scoped `QuestionBankItem` and immutable
`QuestionVersion` documents, plus `AssessmentActivity` documents. Adding a new
question version never mutates an existing version. Publishing copies the selected
version content into an immutable `ActivityQuestion` snapshot, so later question-bank
changes cannot rewrite an activity already released to students. Teachers can publish
an activity for a class or directly into a started LiveSession, then pause, resume, or
close it.

Classroom owns the `StudentActivity` event-sourced aggregate. Its inline Marten
snapshot is the read model used by the attempt API; the event stream uses conjoined
tenant storage. Start, question-open, answer-submit/change, and activity-submit are
the durable transitions in this slice. The final `Submission` document stores a
durable answer snapshot in the same tenant-scoped session. Correct answers remain
inside the teacher-facing question-bank/activity model and are not returned in the
student activity response.

Review findings to resolve within their owning slices: snapshot/subscription gap
recovery; immutable essay submission membership; validation bound to file/topic/policy
versions at final submission.

## Implementation references

- `docs/architecture/multi-tenancy.md` records the database placement decision and
  audit classification for the current foundation.
- https://martendb.io/documents/multi-tenancy.html
- https://martendb.io/configuration/hostbuilder
- https://wolverinefx.io/guide/durability/marten/
- https://wolverinefx.net/guide/codegen.html
