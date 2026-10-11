# Backend FullStackHero adoption

## Decision — 2026-10-11

User requested adoption of the latest FullStackHero .NET starter structure and Aspire. The verified stable release is [10.0.0](https://github.com/fullstackhero/dotnet-starter-kit/releases/tag/10.0.0), commit `44412b23675b76fc38ddc8eefba93f60f0127be0`. Its .NET 10 modular monolith, Host / BuildingBlocks / Modules organization, module registration contract, feature endpoints, central package management, architecture tests and Aspire AppHost are suitable for RLMS.

This is an architecture/organization change, not a replacement of RLMS business behavior. We do not copy FSH Identity, EF Core, billing, multitenancy, database model or domain logic. Marten/PostgreSQL, Wolverine, seven existing modules, tenant isolation, private object storage, immutable question versions and snapshot/delta observation remain the RLMS contracts.

## Audit

| Area | Decision | Evidence / action |
|---|---|---|
| Business modules and aggregates | KEEP | Seven bounded areas already exist. StudentActivity event history has business value. Preserve domain namespaces and assembly identities. |
| Persistence and messaging | KEEP | Existing tests cover tenant scope, durable attendance, attempts, grade visibility and realtime subscriptions. Do not substitute EF/Mediator just to match a template. |
| API host | PATCH NOW | Program currently registers every schema and message route indirectly through a shared Infrastructure project. Reduce to composition, middleware and module endpoint mapping. |
| Infrastructure | PATCH NOW | Shared Infrastructure references all business modules. Split technical persistence/cache/storage/web concerns into BuildingBlocks, put schema ownership and message routes in modules. |
| Endpoint files | PATCH NOW | Assessment endpoints ~1,900 lines, essays ~1,400, learning ~1,400. Split use cases into feature folders while preserving route, authorization, response and business rule behavior. |
| Public integration contracts | PATCH NOW | Existing contract folders compile into runtime assemblies. Extract independent contract assemblies and preserve persisted Wolverine type compatibility. Remove all six runtime-to-Identity edges and Assessment-to-Learning; consumers use public contract assemblies. |
| Orchestration | PATCH NOW | Replace manual API/frontend process management with Aspire AppHost; retain Compose as the infrastructure compatibility path. Do not reset existing Docker volumes or demo data. |
| Database migration | PATCH NOW | Introduce explicit DbMigrator before API startup. Keep existing schema aliases, tenant policy and inline projections. Tests create isolated databases. |
| Tests and scripts | PATCH NOW | Move tests under src/Tests; update all build/test/CI/local config paths, add architectural and route-preservation checks. |
| Cross-module document reads | MIGRATE | Current reporting and learning use internal documents of other areas. This is existing architectural debt, not solved by relabeling documents as contracts. Remove unused runtime references now. Keep the remaining compatibility graph explicit and test it; introduce domain-specific read/command ports separately with semantic tests rather than moving all documents into a shared assembly. |
| Full domain rewrite / database strategy | LATER / out of scope | No product requirement justifies changing aggregate ownership, event meaning or persistence strategy. |

## Compatibility constraints

- Preserve public API paths and policy metadata. No frontend navigation/UI redesign.
- Preserve runtime assembly names (`LearningPlatform.*`) and domain/event full names despite moving project paths.
- Extracted integration messages require type forwarders from their previous runtime assembly so durable queued messages continue resolving.
- Modules own Marten schema configuration; BuildingBlocks must not reference runtime modules.
- AppHost must not seed, delete, reinitialize or anonymously publish existing data.
- Only PostgreSQL is core readiness; Redis and object storage remain peripheral failure boundaries.
- Separate migrator from API host; deployment must run migration before API startup.

## Verification

Baseline: 44 unit tests pass. 68 integration tests fail during fixture construction because PostgreSQL at localhost:55432 is not running. Restore development dependencies and rerun after migration. Record final results below when verification is complete.


## Result and verification

- 154 route features extracted from 14 large endpoint files. Shared publication, attempt transitions/access/snapshot and curriculum/resource logic have dedicated feature support files. API Program is composition/middleware only.
- Six contract assemblies: Identity, Academics, Classroom, Assessment, Learning, Communication. No empty Reporting contract project. Original integration message namespaces remain, with runtime type forwarders.
- Removed seven runtime dependencies: every consumer -> Identity (six edges), Assessment -> Learning. Remaining direct reads across Academics/Classroom/Assessment/Learning/Reporting are explicitly bounded compatibility debt; a full domain-port migration is **not** claimed complete.
- Technical projects: Core, Web, Persistence, Caching, Storage, ServiceDefaults. None references a business module or Host. Contract projects only reference other contracts or neutral Core, never Marten or runtime module implementation.
- Stable release baseline: FSH 10.0.0, Aspire SDK/integrations 13.4.0 from that release. This deliberately matches the starter release rather than silently upgrading to the separate Aspire 13.6.1 advertised by Dashboard.
- MessagePack 2.5.301 explicitly overrides the vulnerable 2.5.192 transitive dependency from Aspire's RPC graph. NuGet vulnerability audit of AppHost reports no vulnerable packages; warnings remain errors. [Upstream advisory](https://github.com/MessagePack-CSharp/MessagePack-CSharp/security/advisories/GHSA-vh6j-jc39-fggf).
- AST/token comparison confirms all 304 original endpoint/helper/DTO method bodies are preserved after the move (apart from the renamed realtime-state factory call).
- Build succeeds, locked restore succeeds, 44 unit + 68 integration + 11 architecture tests pass (123 .NET tests total), plus two isolated Python configuration-upgrade tests. Architecture tests compare all module HTTP routes/policies against the pre-migration develop snapshot (157 route registrations, including the host baseline) and resolve seven persisted message types using their old assembly names.
- Fixed a test harness race: the media signaling test could consume the initial teacher-only peer snapshot before its student connected. It now waits for the intended joined-peer snapshot within the same timeout; no product assertion was relaxed.
- DbMigrator applied existing schemas and ran again successfully without replacing data. API disables automatic Marten/Wolverine schema creation; isolated test databases explicitly enable setup in their fixtures.
- Aspire Existing mode: API ready 200, migrator completed; managed mode: PostgreSQL, Redis, MinIO, API and frontend running, bucket initialization/migrator completed. Vite uses the API HTTPS endpoint and correct npm script separator.
- Existing volumes verified unchanged: `rlms-dev_postgres-data:/var/lib/postgresql/data`, `rlms-dev_object-data:/data`. Data counts before/after managed startup: 35 accounts / 3 courses / 2 classes. No seeding, truncate, reset or down-v operation was used.
- Release API publish succeeds; published output contains no appsettings.Local.json.
- Smoke: live/ready 200 even with managed Redis/MinIO containers temporarily stopped and then restarted; unauthenticated identity/dependency-detail access 401. Existing tests continue to cover cookie/CSRF, tenant isolation, grade visibility, reconnect, student A -> B detail subscription, attendance and concurrent joins.
- Dashboard verified in Chrome. API, frontend and database resources are running; one-shot migrator is Finished. Local screenshot: `artifacts/aspire-dashboard.jpg` (not committed).
- Final restart log inspection: explicitly set Wolverine's application assembly to the composing Host rather than the Persistence registration assembly. A retained previous-node leader record produced two StopRemoteAgent acknowledgement timeout logs after rapid restart; the new node successfully assumed leadership and started its PostgreSQL agent. This restart residue is recorded rather than hidden or removed by resetting message storage.
- CI portability: Aspire injects host-platform Dashboard/DCP packages, so AppHost uses `packages.$(NETCoreSdkRuntimeIdentifier).lock.json`. Checked-in macOS ARM64 and Linux x64 lock files support locked restore on the development machine and Ubuntu CI respectively. Other host platforms must generate their matching lock file with an unlocked restore before using locked mode. Package locks also reflect the reduced module project-reference graph.

## Run and deployment

```bash
./run-dev.sh
# Or manage infrastructure containers through Aspire as well:
RLMS_INFRASTRUCTURE_MODE=Managed ./run-dev.sh
```

Default Existing mode starts/reuses Compose services, then AppHost manages the applications. Managed mode first stops only RLMS Compose containers and reuses their volumes. Do not start both modes concurrently. On an existing checkout, ignored local config is moved to `backend/src/Host/LearningPlatform.Api/appsettings.Local.json`; setup-local also uses that path and copies any ignored legacy config without overwriting custom settings. Secret local settings are excluded from published API/migrator output.

For deployment without Aspire, run `LearningPlatform.DbMigrator` before starting `LearningPlatform.Api`, with the same production connection/configuration environment. The new solution is `backend/LearningPlatform.slnx`; build/test/CI/load-test paths are updated.
