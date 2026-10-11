# PROJECT_STRUCTURE.md

# Cấu trúc thư mục chuẩn của dự án

Tài liệu này định nghĩa cấu trúc thư mục chuẩn cho toàn bộ dự án.

Mục tiêu:
- dễ maintain;
- dễ tìm business code;
- không tạo quá nhiều module ngang hàng;
- vẫn giữ boundary theo DDD/Event Storming;
- không overengineering;
- frontend và backend tổ chức theo nhu cầu riêng, không mirror nhau 1:1.

---

# 1. Cấu trúc từ root

```text
learning-platform/
│
├── README.md
├── GOAL.md
├── WORKFLOW.md
├── AGENTS.md
├── ARCHITECTURE_RULES.md
├── SYSTEM_ARCHITECTURE.md
├── SYSTEM_MASTER.md
├── FUNCTIONS.md
├── PROJECT_STRUCTURE.md
├── UX_STRUCTURE.md
├── DOMAIN_MODEL.md
│
├── frontend/
│   └── src/
│       ├── app/
│       ├── features/
│       └── shared/
│
├── backend/
│   ├── LearningPlatform.slnx
│   ├── src/
│   │   ├── Host/
│   │   ├── BuildingBlocks/
│   │   ├── Tests/
│   │   └── Modules/
│   │       ├── Identity/
│   │       ├── Academics/
│   │       ├── Classroom/
│   │       ├── Assessment/
│   │       ├── Learning/
│   │       ├── Communication/
│   │       └── Reporting/
│   │
│   └── Directory.Packages.props
│
├── infra/
│
├── docs/
│
└── scripts/
```

---

# 2. Quy tắc tổ chức tổng thể

## 2.1. Bounded Context không đồng nghĩa với folder module cấp cao

Event Storming có thể xác định nhiều bounded context.

Tuy nhiên:

```text
Bounded Context
        !=
Physical Top-level Module
```

Không tạo 15–20 folder ngang hàng chỉ vì có 15–20 bounded context.

Các context gần nhau về nghiệp vụ được gom vào một khu vực lớn hơn để dễ maintain.

Ví dụ:

```text
Course Management
Class Management
Scheduling
Enrollment
```

được tổ chức dưới:

```text
Academics/
```

Business boundary vẫn tồn tại bên trong.

## 2.2. Không tạo cấu trúc generic toàn hệ thống

Không tổ chức backend thành:

```text
Controllers/
Services/
Repositories/
Entities/
Dtos/
```

cho toàn bộ application.

Business code phải nằm gần business feature.

## 2.3. Không tạo folder rỗng để dự phòng

Chỉ tạo folder khi có code/use case thực tế.

Không tạo trước:
- AI;
- microservices;
- payment;
- certificate;
- abstraction chưa dùng.

---

# 3. Frontend

Frontend tổ chức theo **user-facing feature**, không mirror backend 1:1.

```text
frontend/
│
├── package.json
├── tsconfig.json
├── vite.config.ts
├── .env.example
│
├── public/
│
└── src/
    │
    ├── main.ts
    ├── App.vue
    │
    ├── app/
    │   ├── router/
    │   ├── layouts/
    │   └── providers/
    │
    ├── features/
    │   ├── auth/
    │   ├── dashboard/
    │   ├── courses/
    │   ├── classrooms/
    │   ├── assignments/
    │   ├── exams/
    │   ├── essays/
    │   ├── gradebook/
    │   ├── calendar/
    │   └── reports/
    │
    └── shared/
        ├── ui/
        ├── api/
        ├── realtime/
        ├── utils/
        └── types/
```

---

# 4. Frontend — app

```text
src/app/
│
├── router/
│   ├── index.ts
│   └── routes.ts
│
├── layouts/
│   ├── AdminLayout.vue
│   ├── TeacherLayout.vue
│   ├── StudentLayout.vue
│   └── LiveClassroomLayout.vue
│
└── providers/
```

`app/` chỉ chứa application-level concern:
- router;
- layouts;
- providers;
- bootstrap.

Không chứa business feature cụ thể.

---

# 5. Frontend — features

## 5.1. classrooms

Đây là feature quan trọng nhất phía frontend.

```text
features/
└── classrooms/
    │
    ├── pages/
    │   ├── ClassroomPage.vue
    │   └── TeacherWorkspacePage.vue
    │
    ├── components/
    │   ├── Media/
    │   ├── Chat/
    │   ├── Participants/
    │   ├── ActivityOverview/
    │   ├── QuestionHeatmap/
    │   └── StudentDetail/
    │
    ├── realtime/
    │   ├── presence.ts
    │   ├── overview.ts
    │   └── student-detail.ts
    │
    ├── api/
    ├── stores/
    └── types/
```

### Realtime rule

`overview.ts`
- nhận metadata nhẹ cho cả lớp.

`student-detail.ts`
- chỉ subscribe student đang được mở.

Không gộp hai channel thành một payload lớn.

## 5.2. assignments

```text
features/assignments/
├── pages/
├── components/
├── api/
├── stores/
└── types/
```

Bao gồm UI cho:
- assignment;
- exercise;
- quiz flow cơ bản;
- submission.

## 5.3. essays

```text
features/essays/
├── pages/
├── components/
├── api/
└── types/
```

Bao gồm:
- nhóm;
- đăng ký đề tài;
- duyệt đề tài;
- upload report;
- validation result;
- submission history.

Không tạo AI review UI trong GOAL hiện tại.

---

# 6. Frontend — shared

```text
shared/
├── ui/
├── api/
├── realtime/
├── utils/
└── types/
```

Chỉ đưa vào `shared/` khi:
- được dùng bởi nhiều feature;
- không thuộc ownership của một feature cụ thể.

Không biến `shared/` thành nơi chứa business code chung chung.

---

# 7. Backend — FullStackHero organization

```text
backend/
├── LearningPlatform.slnx
├── Directory.Build.props
├── Directory.Packages.props
└── src/
    ├── Host/
    │   ├── ModuleCatalog.cs
    │   ├── LearningPlatform.Api/
    │   ├── LearningPlatform.AppHost/
    │   └── LearningPlatform.DbMigrator/
    ├── BuildingBlocks/
    │   ├── Core/
    │   ├── Web/
    │   ├── Persistence/
    │   ├── Caching/
    │   ├── Storage/
    │   └── ServiceDefaults/
    ├── Modules/
    │   ├── Identity/
    │   ├── Academics/
    │   ├── Classroom/
    │   ├── Assessment/
    │   ├── Learning/
    │   ├── Communication/
    │   └── Reporting/
    └── Tests/
        ├── UnitTests/
        ├── IntegrationTests/
        └── ArchitectureTests/
```

Business Core vẫn là Modular Monolith. Mỗi module có project runtime
`Modules.<Name>/Modules.<Name>.csproj`. Các contract được sử dụng xuyên module
nằm trong `Modules.<Name>.Contracts`; không tạo contract project rỗng.
Tên assembly runtime vẫn là `LearningPlatform.<Name>` để tương thích Marten/events.
Các mục module bên dưới mô tả nghiệp vụ, không phải yêu cầu tạo thêm folder rỗng.

# 8. Backend — Host

`LearningPlatform.Api` chỉ composition, HTTP middleware, health và mapping module.
Authentication/policy implementation thuộc Identity; SignalR hub thuộc module sở hữu.
`ModuleCatalog.cs` là danh sách module tường minh dùng chung cho API và migrator.
`IModule` cung cấp ConfigureServices / MapEndpoints / ConfigureMessaging.

`LearningPlatform.DbMigrator` áp dụng schema document, event và durable message
trước API. API không tự sửa schema khi startup. Không seed hoặc reset dữ liệu.

`LearningPlatform.AppHost` điều phối migrator, API, frontend và observability.
Existing mode sử dụng infrastructure đang cấu hình. Managed mode điều phối cả
PostgreSQL, Redis, MinIO và bucket init với volume hiện có của RLMS.

# 9. Backend — BuildingBlocks

BuildingBlocks chứa technical implementation và contract trung lập. Không được
tham chiếu runtime module hoặc Host. Marten schema registration nằm trong từng
module `Infrastructure/Persistence`; global tenant/schema policy nằm ở Persistence.
Core chỉ có tenant/storage capability contract. Module adapter kết nối essay và
learning-resource storage với S3 implementation, không làm BuildingBlocks phụ thuộc
nghiệp vụ Assessment/Learning.

# 10. Backend — Modules and features

```text
Modules/<Name>/
├── Modules.<Name>/
│   ├── <Name>Module.cs
│   ├── Domain/
│   ├── Features/<Area>/<UseCase>/
│   │   └── <UseCase>Endpoint.cs
│   ├── Infrastructure/Persistence/SchemaRegistration.cs
│   ├── Api/                    # compatibility routing + shared endpoint support
│   └── ...                    # realtime/attendance/projections where needed
└── Modules.<Name>.Contracts/   # only actual public contracts; no runtime dependencies
```

Endpoint registration, handler và permission metadata nằm theo use case. Request/
response records nằm cạnh feature area, giữ wire contract. Domain business rules
và existing Wolverine handlers được giữ nguyên. Không tạo mặc định tầng generic
repository, mediator mới hoặc folder trống.

Existing cross-runtime dependencies là compatibility baseline được architecture
tests khóa lại; không phải convention cho code mới. Migration sang read/command
ports phải theo đúng ownership và kiểm thử semantic từng boundary.

---

# 11. Identity

```text
Identity/
├── Users/
├── Organizations/
└── Authorization/
```

Bao gồm:
- user;
- teacher/student identity/profile;
- organization;
- role;
- permission.

Business permission không được hard-code theo tên role nếu rule thật là permission.

---

# 12. Academics

```text
Academics/
├── Courses/
├── Classes/
├── Enrollments/
└── Scheduling/
```

Bao gồm:

### Courses
- Course;
- Module;
- Lesson;
- cấu trúc nội dung.

### Classes
- Class;
- roster;
- teacher assignment.

### Enrollments
- student enrollment.

### Scheduling
- schedule;
- recurring schedule;
- class session;
- reschedule;
- cancel.

---

# 13. Classroom

Đây là khu vực realtime core.

```text
Classroom/
├── LiveSessions/
├── Attendance/
├── StudentActivities/
├── HelpRequests/
└── Realtime/
```

## 13.1. LiveSessions

```text
LiveSessions/
├── StartSession/
├── EndSession/
├── JoinSession/
├── LeaveSession/
└── ...
```

Bao gồm:
- LiveSession;
- participant;
- presence business flow;
- join/leave.

## 13.2. Attendance

```text
Attendance/
├── RecordAttendance/
├── AdjustAttendance/
├── AttendanceQueries/
└── ...
```

## 13.3. StudentActivities

```text
StudentActivities/
│
├── Domain/
│
├── StartActivity/
├── OpenQuestion/
├── SubmitAnswer/
├── ChangeAnswer/
├── SubmitActivity/
│
└── Projections/
    ├── StudentActivityProjection.cs
    ├── ClassOverviewProjection.cs
    └── StudentAttemptDetailProjection.cs
```

Core state có thể gồm:
- current question;
- progress;
- status;
- last activity;
- idle/resume.

## 13.4. HelpRequests

```text
HelpRequests/
├── RequestHelp/
├── AcknowledgeHelp/
├── ResolveHelp/
└── Queries/
```

Help request nằm trong `Classroom` vì nó gắn trực tiếp với learning activity trong buổi học.

## 13.5. Realtime

```text
Realtime/
├── Presence/
├── ClassOverview/
└── StudentDetail/
```

### ClassOverview
Chỉ gửi:
- StudentId;
- CurrentQuestion;
- AnsweredCount;
- Progress;
- Status;
- LastActivity;
- HelpRequested.

### StudentDetail
Chỉ được activate khi teacher mở đúng student attempt.

---

# 14. Assessment

```text
Assessment/
├── Activities/
├── QuestionBank/
├── Exams/
├── Submissions/
├── Grading/
├── Gradebook/
└── Essays/
```

Đây là khu vực quản lý assessment lifecycle.

## 14.1. Activities

Bao gồm:
- Assignment;
- Exercise;
- Quiz;
- Question usage.

## 14.2. QuestionBank

Bao gồm:
- question definition;
- classification;
- reuse;
- random selection;
- version.

## 14.3. Exams

Bao gồm:
- exam;
- attempt rules;
- time;
- shuffle;
- pass score;
- retake.

## 14.4. Submissions

Bao gồm:
- answer;
- submission;
- submission state;
- publish result link khi phù hợp.

## 14.5. Grading

Bao gồm:
- objective auto-grading;
- manual grading;
- rubric;
- feedback.

AI không nằm trong grading hiện tại.

## 14.6. Gradebook

Bao gồm:
- grade;
- component grade;
- weight;
- average;
- final grade.

## 14.7. Essays

```text
Essays/
├── Groups/
├── Topics/
├── Reports/
└── Validation/
```

### Groups
- essay group;
- member management.

### Topics
- register topic;
- review;
- approve/reject;
- canonical locked topic;
- topic change request.

### Reports
- upload;
- resubmit;
- version history;
- group submission.

### Validation
- file validation;
- text extraction;
- empty/near-empty detection;
- topic validation;
- required sections.

Không tạo:

```text
Essays/AiReview/
```

trong GOAL hiện tại.

---

# 15. Learning

```text
Learning/
├── Progress/
├── History/
└── Resources/
```

## Progress
- course progress;
- lesson progress;
- completion.

## History
- learning history.

## Resources
- resource metadata;
- lesson file;
- slide;
- video;
- attachment.

Binary thực tế nằm ở Object Storage.

---

# 16. Communication

```text
Communication/
├── Announcements/
├── Chat/
└── Notifications/
```

Bao gồm:
- class announcement;
- chat;
- in-app notification;
- deadline notification;
- schedule notification;
- result notification.

`HelpRequest` không nằm ở Communication.

---

# 17. Reporting

```text
Reporting/
├── ClassReports/
├── CourseReports/
├── StudentReports/
├── AssessmentReports/
└── AttendanceReports/
```

Reporting chủ yếu đọc từ:
- projections;
- read models;
- async projections.

Không để reporting query/replay raw Event Stream cho request UI thông thường.

---

# 18. Cấu trúc feature backend

Không bắt buộc mọi feature phải có nhiều layer folder.

Ví dụ:

```text
SubmitAnswer/
├── SubmitAnswerCommand.cs
├── SubmitAnswerHandler.cs
├── SubmitAnswerValidator.cs
└── SubmitAnswerEndpoint.cs
```

Hoặc nếu convention project dùng tên ngắn:

```text
SubmitAnswer/
├── Command.cs
├── Handler.cs
├── Validator.cs
└── Endpoint.cs
```

Ưu tiên vertical slice.

Không tạo mặc định:

```text
Commands/
Requests/
Responses/
Handlers/
Services/
Repositories/
Interfaces/
Mappings/
```

nếu feature chưa cần.

---

# 19. Tests

```text
backend/src/Tests/
├── UnitTests/
├── IntegrationTests/
├── ArchitectureTests/
└── LoadTests/
    ├── MassJoin/
    ├── RealtimeOverview/
    ├── StudentDetail/
    ├── AnswerBurst/
    ├── Reconnect/
    └── CacheStampede/
```

Test structure không cần mirror 100% source tree.

Ưu tiên tổ chức theo use case và workload dễ tìm.

---

# 20. Infra

```text
infra/
├── docker/
│   ├── postgres/
│   ├── redis/
│   ├── media/
│   └── object-storage/
│
├── compose/
│   ├── docker-compose.dev.yml
│   └── docker-compose.test.yml
│
├── nginx/
└── scripts/
```

Không thêm Kubernetes/service mesh trong GOAL hiện tại.

---

# 21. Docs

```text
docs/
├── architecture/
├── audits/
├── reference/
├── modules/
└── adr/
```

Chỉ tạo tài liệu module/ADR khi có quyết định thực sự cần lưu.

Không tạo hàng chục file docs rỗng từ đầu.

---

# 22. Scripts

```text
scripts/
├── build.sh
├── test.sh
├── format.sh
└── load-test.sh
```

Chỉ bổ sung script khi workflow thực sự sử dụng.

---

# 23. Nguyên tắc cuối

Cấu trúc thư mục phải phục vụ việc tìm code theo business.

Ưu tiên:

```text
Business Area
    ↓
Feature
    ↓
Command / Handler / Domain / Projection
```

Không ưu tiên:

```text
Technical Type
    ↓
tất cả Controller
tất cả Service
tất cả Repository
```

Khi một folder bắt đầu quá lớn:
1. xem business concept bên trong;
2. tách theo feature/subdomain;
3. không vội tạo module top-level mới;
4. chỉ tạo module top-level mới khi boundary thực sự độc lập và có giá trị maintain rõ ràng.


## FullStackHero backend adoption (2026-10-11)

See [backend adoption decision](docs/architecture/fullstackhero-backend.md). Backend now follows `src/Host`, `src/BuildingBlocks`, `src/Modules/<Name>/Modules.<Name>` and `src/Tests`. Runtime modules retain RLMS domain ownership, Marten/Wolverine and assembly identities. Modules register their schemas and message routes; shared technical BuildingBlocks must not depend on runtime business modules. Aspire manages application startup after the explicit database migrator. Existing cross-module runtime dependencies are an explicit compatibility baseline, not permission to introduce new dependencies.
