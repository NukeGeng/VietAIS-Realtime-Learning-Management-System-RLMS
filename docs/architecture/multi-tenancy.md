# Multi-tenancy and database strategy

RLMS uses one tenant model across deployments. `Organization` is the business
entity; `TenantId` is the technical isolation key. The current convention maps the
two as `TenantId = OrganizationId.ToString("D")`, but application code resolves that
mapping through `ITenantContext` at the infrastructure boundary.

## Current database decision

The GOAL implementation uses a shared PostgreSQL database with Marten conjoined
multi-tenancy:

```text
Authenticated request
        ↓
HttpTenantContext
        ↓
organization-scoped IDocumentSession
        ↓
Marten tenant_id on every tenant-owned document
```

The database has one `rlms` schema. Marten owns document tables and their
`(tenant_id, id)` identity. Tenant-owned documents include courses, classes,
enrollments, schedules, live sessions, and future activities, submissions, grades,
and projections. The control tenant stores the organization registry and bootstrap
state. Control-plane reads are explicit and are never served through an arbitrary
organization session.

Business keys that are local to an organization use a tenant-scoped unique index,
for example `(tenant_id, code)` for `Course` and `LearningClass`. A course code may
therefore be reused by two organizations while remaining unique inside each one.

## Storage boundaries

| Data | Primary storage | Tenant rule |
| --- | --- | --- |
| Organization registry and bootstrap marker | PostgreSQL/Marten control tenant | Platform/control data only |
| Users, roles, courses, classes, enrollment, schedules, LiveSession, AttendanceRecord, RaiseHand, ChatMessage | PostgreSQL/Marten | Conjoined tenant session |
| Event-sourced activities and projections | PostgreSQL/Marten | Event stream and projection carry tenant context |
| Presence, connection state, snapshots and short-lived counters | Redis | Every key starts with the tenant ID |
| File metadata and ownership | PostgreSQL/Marten | Metadata is tenant-scoped |
| PDF, DOCX, images, video and recordings | Object Storage | Object key and signed access are tenant-owned |

The LiveSession implementation uses keys such as
`rlms:live-presence:{tenantId}:{sessionId}:{userId}` and SignalR groups such as
`tenant:{tenantId}:live-session:{sessionId}`. A client never supplies a tenant ID to
select a session or group; the server resolves it from the authenticated principal
and verifies resource membership. Attendance message contracts carry the same
organization/tenant ID so background handlers open the correct Marten session.

## Placement options

The shared database is the only placement implemented and operated by the current
GOAL. The same business modules can later run with:

```text
Hosted SaaS       → shared PostgreSQL + Marten conjoined tenants
Hosted Enterprise → one dedicated PostgreSQL store per tenant
On-Premise        → one customer PostgreSQL store
```

Dedicated database provisioning, migration orchestration, and tenant placement
registries are intentionally later work. They belong in Infrastructure. Business
modules must continue to consume the current tenant/session boundary and must not
branch on commercial plan, shared versus dedicated database, or deployment mode.

## Audit classification

### KEEP

- Marten conjoined multi-tenancy for the current hosted SaaS path.
- Explicit control tenant for organization/platform state.
- Tenant-scoped document sessions and unique indexes.
- Permission and resource checks after tenant resolution.
- Redis for ephemeral state and Object Storage for binary data.

### PATCH NOW

- Tenant resolution is centralized in `ITenantContext`.
- LiveSession realtime groups and presence keys include tenant scope.
- Cross-tenant access and same-key-per-tenant behavior are integration-tested.

### MIGRATE

There is no production data migration in this repository. Local and integration data
can be recreated. If durable tenant data is introduced before a placement change,
the migration must preserve tenant ownership, rebuild tenant-scoped uniqueness, and
verify projections before switching reads.

### LATER

- Dedicated-store selection and provisioning for enterprise/on-premise deployments.
- Tenant-prefixed object keys and signed file access when file features are added.
- Tenant-aware event streams, projections, reporting jobs, and platform reporting as
  their business modules are implemented.

## Verification

The current foundation verifies that an authenticated tenant determines the
Marten session, a request header cannot override it, documents are isolated across
organizations, course keys can repeat across tenants, LiveSession access is
membership-scoped, and Redis/realtime keys are tenant-scoped.
