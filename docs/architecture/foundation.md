# Foundation implementation

Scope: GOAL Phase 0. Business modules are added only when a use case exists.
The initial projects are the API composition root, technical Infrastructure,
and Identity (authenticated actor and permission contract). No empty modules.

## Decisions

- .NET 10, using the installed SDK; package versions and restore lock files are committed.
- ASP.NET Core cookie authentication for the first-party web application. Cookies are
  HttpOnly, Secure and SameSite=Lax. Unsafe API requests require an antiforgery token.
  Login/account management is the next Identity slice; there is no development bypass.
- Organization and user IDs must be non-empty GUIDs in the authenticated principal.
  Marten sessions use the authenticated organization as the conjoined tenant ID.
  Never accept a tenant selector from an arbitrary request header.
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
- Media configuration defines an external SFU endpoint; session-specific access
  tokens belong to the upcoming LiveSession use case. No media payload in the API.

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

## Remaining system work

The bootstrap is covered by unit/integration tests, including durable local message
delivery and authenticated SignalR negotiation/message exchange using test-only
endpoints. No test authentication bypass is exposed by the application.
Phases 1–8 and GOAL flows A–F remain required; this
foundation does not claim any learning, grading, essay, or live teaching flow.

Review findings to resolve within their owning slices: snapshot/subscription gap
recovery; immutable essay submission membership; validation bound to file/topic/policy
versions at final submission.

## Implementation references

- https://martendb.io/documents/multi-tenancy.html
- https://martendb.io/configuration/hostbuilder
- https://wolverinefx.io/guide/durability/marten/
- https://wolverinefx.net/guide/codegen.html
