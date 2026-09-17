# MULTI_TENANCY_AUDIT.md

# RLMS — Multi-Tenancy Architecture Audit & Correction

## 1. Mục tiêu

RLMS phải hỗ trợ nhiều mô hình triển khai/thương mại bằng cùng một business codebase.

Không rewrite project từ đầu. Audit implementation hiện tại trước, sau đó chỉ patch phần cần thiết.

```text
Audit
  ↓
Classify
  ↓
Patch tối thiểu
  ↓
Build / Test
  ↓
Tiếp tục GOAL
```

---

## 2. Target Architecture

```text
TenantId / OrganizationId
        ↓
CurrentTenant / TenantContext
        ↓
Tenant Resolution
        ↓
Persistence Strategy
```

Business code không được phụ thuộc trực tiếp vào tenant đang dùng shared database, dedicated database hay on-premise database.

---

## 3. Các mô hình phải hỗ trợ

### Hosted SaaS

```text
Shared PostgreSQL
→ Marten Conjoined Multi-Tenancy
→ tenant isolation bằng TenantId
```

### Hosted Enterprise

```text
Dedicated PostgreSQL
→ Database-per-Tenant
```

Chưa cần triển khai provisioning database-per-tenant trong GOAL hiện tại nếu chưa có requirement thực tế.

### On-Premise / Source License

```text
Customer Deployment
→ Dedicated PostgreSQL
→ Single-customer deployment
```

Vẫn giữ `TenantContext`, không fork business code riêng.

---

## 4. Tenant vs Organization

Hiện tại có thể mapping:

```text
TenantId == OrganizationId
```

nhưng không hard-code hai khái niệm này thành một ở mọi nơi.

- `Organization` = business entity.
- `Tenant` = technical/data isolation context.

---

## 5. Step 1 — Audit Current Setup

Kiểm tra và phân loại:

```text
KEEP
PATCH NOW
MIGRATE
LATER
```

Không sửa phần đã đúng.

---

## 6. Audit Tenant Context

Kiểm tra có:

```text
TenantId
OrganizationId
CurrentTenant
TenantContext
TenantResolver
```

Tenant phải được resolve từ authenticated context phù hợp.

Không tin trực tiếp TenantId từ request body.

---

## 7. Audit Authorization Boundary

Flow phải tương đương:

```text
Authentication
      ↓
Tenant Resolution
      ↓
Tenant Boundary
      ↓
Permission
      ↓
Resource Ownership
```

Có permission nhưng sai tenant vẫn phải bị từ chối.

---

## 8. Audit Domain Ownership

Kiểm tra tenant ownership của:

```text
Course
LearningClass
Enrollment
ClassSchedule
LiveSession
AssessmentActivity
StudentActivity
Submission
GradeEntry
EssayAssignment
EssayGroup
EssayTopic
EssaySubmission
LearningProgress
LearningResource
```

Không bắt buộc mọi object duplicate `OrganizationId` nếu ownership derive an toàn, nhưng persistence/query/projection/cache/realtime phải tenant-safe.

---

## 9. Audit Platform-level Data

Có thể là global:

```text
Organizations
Platform Admin
Global Configuration
Tenant Registry
License Metadata
Commercial Plan Metadata
```

Không áp tenant filter mù quáng cho dữ liệu global.

---

## 10. Audit Marten

Hosted SaaS foundation phải hỗ trợ:

```text
Marten Conjoined Multi-Tenancy
```

Không hard-code architecture khiến tenant sau này không thể chuyển sang dedicated database.

---

## 11. Audit Marten Session

Mọi session tenant-owned phải chạy trong đúng tenant context.

Không dựa hoàn toàn vào việc developer nhớ thêm:

```csharp
.Where(x => x.OrganizationId == currentOrganizationId)
```

ở từng query.

Ưu tiên enforce ở session/persistence boundary.

---

## 12. Audit Event Store

Các stream/event tenant-owned như:

```text
StudentActivityStarted
QuestionOpened
AnswerSubmitted
AnswerChanged
StudentActivityResumed
ActivitySubmitted
```

phải thuộc đúng tenant.

Không để event của nhiều trường bị mất tenant ownership.

---

## 13. Audit Projection / Read Model

Kiểm tra:

```text
ClassOverviewView
StudentAttemptDetailView
GradebookView
StudentLearningProgressView
TeacherDashboardView
StudentDashboardView
Reporting Views
```

đều tenant-safe.

Không trộn dữ liệu nhiều tenant trong một projection.

---

## 14. Audit Unique Constraint

Nếu business key chỉ unique trong tenant thì semantics phải tương đương:

```text
TenantId + BusinessKey
```

Ví dụ:

```text
TenantId + CourseCode
TenantId + LearningClassCode
TenantId + StudentCode
```

---

## 15. Audit Realtime

Conceptual groups:

```text
tenant:{tenantId}:class:{classId}:overview
tenant:{tenantId}:attempt:{attemptId}:detail
```

Trước khi subscribe phải verify:

- authenticated;
- đúng tenant;
- có permission;
- resource thuộc tenant.

Không cho client tự gửi TenantId rồi join group mà không verify.

---

## 16. Audit Redis

Cache/presence key tenant-owned phải có tenant scope:

```text
tenant:{tenantId}:session:{sessionId}:snapshot
tenant:{tenantId}:presence:{sessionId}:{studentId}
tenant:{tenantId}:connection:{userId}
```

Không để collision/leak giữa tenant.

---

## 17. Audit Object Storage

File tenant-owned phải có ownership rõ.

Có thể dùng logical key:

```text
tenants/{tenantId}/reports/...
tenants/{tenantId}/resources/...
tenants/{tenantId}/recordings/...
tenants/{tenantId}/attachments/...
```

Không bắt buộc đúng format này nếu convention khác tốt hơn.

---

## 18. Audit Background Jobs

Background worker phải giữ tenant context.

Không xử lý tenant-owned data theo kiểu global không scope.

Nếu là platform job cross-tenant thì phải phân biệt rõ.

---

## 19. Audit Reporting

Tách semantics:

```text
Tenant Reporting
Platform Reporting
```

Tenant reporting phải tenant-scoped.

Platform reporting chỉ dành cho Platform Admin/system operation.

---

## 20. Step 2 — Classify Findings

Báo cáo theo:

```text
KEEP
- đã đúng

PATCH NOW
- foundation assumption sai cần sửa ngay

MIGRATE
- đã có data cần migration/backfill

LATER
- dedicated DB / enterprise capability chưa cần triển khai
```

---

## 21. Step 3 — Patch Current Foundation

GOAL hiện tại ưu tiên chạy được:

```text
Hosted SaaS
→ Shared PostgreSQL
→ Marten Conjoined Multi-Tenancy
```

Architecture phải không khóa chết `Database-per-Tenant`, nhưng chưa cần xây provisioning phức tạp.

---

## 22. Business Layer không biết Tenant Placement

Không viết:

```text
if enterprise:
    use database X
else:
    use shared database
```

Business chỉ biết:

```text
CurrentTenant
```

Infrastructure quyết định tenant nằm ở shared hay dedicated database.

---

## 23. Existing Development Data

Nếu chỉ là local/dev data chưa cần giữ:

```text
patch model/config
→ reset local DB nếu cần
→ apply schema
→ rerun seed
```

Không viết migration phức tạp cho seed/test data không quan trọng.

Nếu có data cần giữ:
- không destructive reset;
- migration/backfill tenant ownership;
- verify unique constraint/projection.

---

## 24. Nếu đã làm module rồi

Không xóa làm lại.

Ví dụ:

```text
Course
LearningClass
Enrollment
```

thì audit từng phần:
- tenant-aware chưa?
- tenant-safe uniqueness chưa?
- query/session đúng tenant chưa?

Patch tối thiểu.

---

## 25. Commercial Model != Database Strategy

Ví dụ:

```text
SaaS Standard
→ Shared Database

SaaS Enterprise
→ Dedicated Database

On-Premise License
→ Customer Database

Source License
→ Customer Deployment / Customer Database
```

Source License không phải một database architecture mới.

---

## 26. Multi-Tenancy Strategy cho cả 3 mô hình

Kiến trúc phải hỗ trợ:

```text
Hosted SaaS
Hosted Enterprise
On-Premise / Source License
```

theo nguyên tắc:

```text
One Tenant Architecture
        ↓
Multiple Tenant Placement Strategies
        ↓
Multiple Commercial Deployment Models
```

Không ép tất cả dùng cùng một database vật lý.

---

## 27. Security Rules

Không được:

- tin TenantId từ client;
- filter tenant ở frontend;
- có permission nhưng bỏ qua resource tenant;
- cache key không tenant scope;
- subscribe realtime không tenant validation;
- expose Object Storage chỉ bằng raw ID;
- cho Organization Admin query global platform data.

Backend phải enforce tenant isolation.

---

## 28. Test Requirements

### Tenant Isolation

```text
Tenant A user
→ không đọc Tenant B Course

Tenant A teacher
→ không join Tenant B LiveSession

Tenant A teacher
→ không subscribe Tenant B Student Detail

Tenant A student
→ không tải Tenant B FileAsset
```

### Shared Database

```text
Tenant A + Tenant B
→ cùng DB
→ dữ liệu tách đúng
```

### Unique Rule

Ví dụ:

```text
Tenant A CourseCode = CS101
Tenant B CourseCode = CS101
```

nếu business cho phép thì cả hai phải tạo được.

### Realtime / Cache

Không leak:
- overview;
- detail;
- presence;
- LiveSessionSnapshot;

cross-tenant.

---

## 29. Documentation Update

Sau audit/patch, cập nhật nếu cần:

```text
GOAL.md
DOMAIN_MODEL.md
SYSTEM_ARCHITECTURE.md
ARCHITECTURE_RULES.md
AGENTS.md
```

Không sửa UX/FUNCTIONS nếu tenancy change không làm đổi user-facing feature.

---

## 30. Không Overengineering

Không tự thêm:

- schema-per-tenant;
- database fleet orchestration;
- Kubernetes;
- service mesh;
- tenant migration automation phức tạp;

chỉ để future-proof.

GOAL hiện tại ưu tiên:

```text
Shared Hosted SaaS
+
Correct Tenant Isolation
+
Clean Tenant Context
```

---

## 31. Final Acceptance

Foundation chỉ tenancy-ready khi đạt:

```text
Authenticated Request
        ↓
Tenant Resolved
        ↓
Tenant-aware Session
        ↓
Tenant-safe Command / Query
        ↓
Tenant-safe Event / Projection
        ↓
Tenant-safe Realtime / Cache / File
```

Và business code không phụ thuộc trực tiếp vào:

```text
Shared DB
Dedicated DB
On-Prem DB
```

Audit trước.

Patch tối thiểu.

Build/test lại foundation.

Sau đó tiếp tục GOAL hiện tại.
