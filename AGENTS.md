# AGENTS.md

# Quy tắc làm việc cho Coding Agent — RLMS

# 1. Vai trò

Bạn đang làm việc trên **RLMS — Realtime Learning Management System**.

Mục tiêu là xây đúng hệ thống đã được đặc tả, không tự tái thiết kế sản phẩm.

Coding Agent phải:
- tuân thủ scope;
- giữ boundary;
- làm theo workflow;
- không overengineering;
- verify trước khi kết luận Done.

---

# 2. Thứ tự tài liệu bắt buộc đọc

Trước khi code một task mới, đọc theo thứ tự:

1. `GOAL.md`
2. `SYSTEM_MASTER.md`
3. `UX_STRUCTURE.md`
4. `DOMAIN_MODEL.md`
5. `PROJECT_STRUCTURE.md`
6. `SYSTEM_ARCHITECTURE.md`
7. `ARCHITECTURE_RULES.md`
8. `FUNCTIONS.md`
9. `WORKFLOW.md`
10. `GIT_RULES.md`
11. `AGENTS.md`

Sau đó đọc code/module liên quan.

Nếu tài liệu mâu thuẫn:
- `GOAL.md` quyết định scope;
- `ARCHITECTURE_RULES.md` quyết định architecture rule;
- `DOMAIN_MODEL.md` quyết định domain ownership/model đã khóa;
- `UX_STRUCTURE.md` quyết định UX flow;
- không tự chọn interpretation có scope lớn hơn.

---

# 3. Không được tự phát minh

Không tự:
- thêm feature;
- thêm role/business flow;
- thêm module top-level;
- thêm microservice;
- đổi database strategy;
- thêm broker;
- thêm AI;
- thêm payment/certificate;
- thay architecture vì sở thích.

Nếu task không nằm trong GOAL:
- không implement phần đó;
- tiếp tục phần nằm trong scope nếu có.

---

# 4. Coming Soon tuyệt đối không implement

GOAL hiện tại không triển khai:

- AI Essay Review;
- RAG;
- embedding/vector database;
- AI grading;
- suggested score;
- percentage correctness từ AI;
- payment;
- commercial packages;
- certificate;
- full microservice migration.

Không tạo placeholder production code kiểu:

```text
AiEssayReviewService
VectorStore
SuggestedScore
```

để “chuẩn bị trước”.

---

# 5. Kiến trúc bắt buộc

Business Core:

```text
ASP.NET Core
+ Modular Monolith
+ Marten/PostgreSQL
+ Wolverine khi phù hợp
+ CQRS
+ Event Sourcing có chọn lọc
```

Realtime:
- Presence;
- Class Overview;
- Student Detail on-demand.

Media:
- WebRTC/SFU/TURN-STUN boundary riêng.

Storage:
- PostgreSQL/Marten: durable business state;
- Redis: cache/presence/ephemeral;
- Object Storage: binary.

Không biến Redis thành source of truth.

---

# 6. Module Structure

Backend chỉ có các business area cấp cao:

```text
Identity
Academics
Classroom
Assessment
Learning
Communication
Reporting
```

Không tạo một top-level module mới nếu feature có thể nằm đúng trong một khu vực hiện có.

Bounded Context không đồng nghĩa top-level folder.

Frontend tổ chức theo user-facing feature, không mirror backend.

---

# 7. Domain Rules

## 7.1. Không dùng class tên `Class`

Dùng:

```csharp
LearningClass
```

## 7.2. Không thiết kế 1 class = 1 table

Trước khi tạo persistence phải xác định:
- Document?
- Event-sourced Aggregate?
- Projection?
- Redis state?
- Object Storage metadata?

## 7.3. StudentActivity là core aggregate

Không phá semantics:

```text
StudentActivity
= một student attempt trên một learning activity
```

Event history có thể gồm:
- StudentActivityStarted;
- QuestionOpened;
- AnswerSubmitted;
- AnswerChanged;
- StudentBecameIdle;
- StudentActivityResumed;
- ActivitySubmitted.

Không event source:
- heartbeat;
- ping;
- từng keystroke;
- mouse move.

## 7.4. Question version phải bất biến với activity đã publish

Không để sửa Question Bank làm thay đổi một activity/exam đã phát hành trước đó.

## 7.5. Essay canonical topic

Sau khi topic được approve/locked:
- canonical title là source để validate report;
- không sửa âm thầm;
- đổi đề tài phải qua business flow.

---

# 8. Aggregate Rules

Không tạo monster aggregate.

Không:
```text
Course
 ├── Classes
 ├── Students
 ├── Activities
 ├── Submissions
 └── Grades
```

Reference aggregate khác bằng ID/contract phù hợp.

Không kéo transaction qua nhiều aggregate nếu business không yêu cầu atomicity.

---

# 9. Marten Rules

Không tạo generic repository chỉ để bọc:
- `IDocumentSession`;
- `IQuerySession`;
- Event APIs.

Dùng Marten theo convention của project.

Document model cho CRUD/state đơn giản.

Event Sourcing chỉ khi:
- event history có business value;
- state transition có ý nghĩa;
- projection/replay/audit thực sự cần.

UI query Read Model, không replay Event Stream.

---

# 10. Wolverine Rules

Handler:
- nhận Command/Message;
- điều phối use case;
- gọi domain;
- persist/publish theo convention.

Không nhét toàn bộ business rule vào handler.

Command là yêu cầu:
- `SubmitAnswer`

Event là việc đã xảy ra:
- `AnswerSubmitted`

Không đặt:
- `SubmitAnswerEvent`.

---

# 11. Realtime Rules — Bắt buộc

## Overview
Teacher luôn có thể subscribe metadata nhẹ:

```text
StudentId
CurrentQuestion
AnsweredCount
TotalQuestions
Progress
Status
LastActivityAt
HelpRequested
```

Không đưa answer content vào overview.

## Student Detail

Khi teacher mở A:

```text
GET snapshot A
→ subscribe A
```

Chuyển B:

```text
unsubscribe A
→ GET snapshot B
→ subscribe B
```

Không broadcast detail của toàn bộ class.

## Snapshot + Delta

Không gửi full submission sau mỗi thay đổi.

Gửi delta:
```text
AnswerChanged(QuestionId, Value, Version)
```

## Reconnect
Không giả định client còn state đúng.
Có khả năng reload snapshot rồi subscribe lại.

---

# 12. Mass Join Rules

Không để N student cùng join gây N lần query lại cùng shared data.

Shared:
- class metadata;
- lesson;
- teacher summary;
- current activity;
- resources;
- common session state.

User-specific:
- authentication;
- enrollment;
- permission;
- attendance identity;
- connection;
- student attempt.

Join critical path phải ngắn.

Không đưa analytics/report/notification nặng vào critical join path.

Chống cache stampede khi cần bằng single-flight/request coalescing hoặc giải pháp tương đương.

---

# 13. Performance Rules

Tối ưu bằng:
1. loại bỏ xử lý dư;
2. cache shared data;
3. giảm payload;
4. delta thay full state;
5. batching hợp lý;
6. index theo query thật;
7. projection đúng;
8. scale đúng workload.

Không “tối ưu” bằng cách cố tình:
- refresh chậm;
- bắt user chờ;
- tắt realtime;
- giảm UX để che bottleneck.

Không tối ưu sâu khi chưa đo.

---

# 14. Failure Isolation Rules

Phải giữ:

```text
Control Plane
Media Plane
Class State Plane
Interaction Plane
Student Detail Plane
```

Acceptance:
- chat lỗi vẫn làm bài;
- notification lỗi vẫn submit;
- analytics lỗi vẫn save answer;
- media lỗi không làm mất submission;
- student-detail realtime lỗi không kéo overview chết theo.

---

# 15. Assessment Rules

Objective question:
- được auto-grade nếu deterministic.

Subjective/Essay:
- teacher chấm;
- teacher dùng rubric;
- teacher nhập/xác nhận điểm.

Không tạo AI grading.

Không tạo hidden “confidence score” rồi map thành grade.

---

# 16. Gradebook Rules

Không tạo một aggregate chứa toàn bộ gradebook của class nếu không cần.

Ưu tiên:
- GradeItem;
- GradeEntry;
- GradebookView projection.

Điểm unpublished không được hiển thị cho student.

Điểm điều chỉnh phải giữ audit phù hợp theo business requirement.

---

# 17. Essay Rules

Pre-submit validation:
- file type;
- readable;
- extracted content;
- empty/near-empty;
- configured threshold;
- required sections;
- canonical topic match.

Validation config theo assignment.

Không hard-code minimum page/word toàn hệ thống.

Nếu validation fail:
- trả lỗi cụ thể;
- không tạo final submission.

MVP không bắt buộc OCR.

---

# 18. File Rules

Binary:
- PDF;
- DOCX;
- video;
- recording;
- slide;
- image;

không lưu trong Event Store.

Lưu Object Storage.

PostgreSQL chỉ giữ metadata/reference phù hợp.

---

# 19. Frontend UX Rules

Không dùng một dashboard chung cho mọi role rồi chỉ đổi menu.

Phải tôn trọng:
- Student Workspace;
- Teacher Workspace;
- Admin Workspace;
- Live Classroom Layout.

Teacher Live Workspace phải có:
- Overview;
- Student Detail on-demand.

Student phải thấy:
- next class;
- deadlines;
- save state;
- progress;
- published results.

Admin không mặc định được xem live student answer detail.

---



# 20. Frontend Data Rules

Không fetch:
- full student answers;
- heavy analytics;
- resources;

chỉ vì page có thể cần trong tương lai.

Fetch theo current screen/use.

Khi switch Student Detail:
- cancel/ignore stale request;
- tránh response A ghi đè màn hình B.

Shared frontend state chỉ chứa thứ thực sự cross-feature.

---

# 21. Permission Rules

Backend luôn enforce permission.

Frontend hide/disable không phải security boundary.

Không hard-code role name nếu permission là rule thật.

Realtime subscribe cũng phải permission check.

Organization scoping phải áp dụng cho:
- query;
- command;
- realtime group/subscription;
- cache key cần tenant scope.

---

# 22. Naming Rules

Code:
- English;
- domain terminology thống nhất.

Docs/business explanation:
- có thể tiếng Việt.

Naming:
- Command: imperative/intention.
- Event: past tense.
- Query/View: mô tả data được đọc.

Ví dụ:

```text
StartLiveSession
LiveSessionStarted
GetClassOverview
ClassOverviewView
```

Không dùng tên generic:
- Manager;
- Helper;
- CommonService;

nếu business meaning cụ thể tồn tại.

---

# 23. Feature Folder Rules

Ưu tiên vertical slice:

```text
SubmitAnswer/
├── SubmitAnswerCommand.cs
├── SubmitAnswerHandler.cs
├── SubmitAnswerValidator.cs
└── SubmitAnswerEndpoint.cs
```

Không tạo mặc định:
- Commands/
- Handlers/
- Services/
- Repositories/
- Interfaces/
- Mappings/

cho mọi feature nếu chưa cần.

Không tạo folder rỗng để “chuẩn bị”.

---

# 24. Task Workflow bắt buộc

Mỗi task:

## 1. Đọc scope
Xác định feature nằm ở đâu trong GOAL.

## 2. Đọc UX/Domain
Xác định:
- screen/user flow;
- owner module;
- aggregate/document;
- read model.

## 3. Event Storming nhỏ nếu có business behavior
Xác định:
- Actor;
- Command;
- Rule;
- Event;
- Projection.

## 4. Viết test cases
Ít nhất:
- happy path;
- validation;
- permission;
- invariant;
- failure case quan trọng.

## 5. Implement tối thiểu
Không mở rộng scope.

## 6. Verify
- build;
- tests;
- smoke flow;
- logs/errors;
- relevant performance/realtime behavior.

## 7. Review diff
Kiểm tra:
- scope creep;
- boundary leak;
- unnecessary dependency;
- event noise;
- payload thừa;
- permission.

## 8. Git workflow
Phải tuân thủ `GIT_RULES.md`:
- branch từ `develop`;
- dùng `module/(fe-be-ai)-noi-dung`;
- không push feature trực tiếp vào `develop` hoặc `main`;
- review diff;
- Draft PR về `develop`;
- build/test trước Ready PR;
- merge xong phải dọn branch nếu không còn dùng.

---

# 25. Build Pass != Done

Không được kết luận task Done chỉ vì:
- compile thành công;
- endpoint trả 200;
- test cũ pass.

Done khi:
- business flow đúng;
- rule đúng;
- test mới đủ;
- permission đúng;
- persistence đúng;
- projection đúng;
- error path chính đúng;
- không phá flow khác.

---

# 26. Test Rules

Không:
- xóa test để pipeline pass;
- disable test khó;
- sửa assertion để hợp bug;
- mock hết business behavior đến mức test vô nghĩa.

Với bug:
- thêm regression test nếu hợp lý.

Với realtime:
- test A -> B unsubscribe/subscribe;
- reconnect;
- stale snapshot/version;
- duplicate/out-of-order concern khi relevant.

Với mass join:
- test concurrent join;
- cache hit/miss;
- stampede;
- DB pressure;
- error rate.

---

# 27. Public Contract / Event Change

Không đổi public contract/event meaning âm thầm.

Nếu thay:
- update producer;
- update consumer;
- update tests;
- migration/versioning nếu dữ liệu/consumer cũ cần tương thích;
- update docs khi business semantics đổi.

Không dựng framework versioning phức tạp trước khi có nhu cầu thật.

---

# 28. Dependency Rules

Không thêm package/dependency nếu:
- framework hiện tại đã giải quyết;
- code đơn giản có thể giải quyết;
- chỉ dùng cho một tiện ích nhỏ.

Nếu thêm dependency:
- nêu use case;
- lý do cần;
- phạm vi sử dụng.

Không thêm:
- RabbitMQ;
- Kafka;
- Kubernetes;
- service mesh;
- API Gateway;

chỉ để “production-ready”.

---

# 29. Data Query Rules

Không query toàn bộ object graph.

Không N+1 nếu query có thể được thiết kế hợp lý.

Không query event history cho list/dashboard.

Reporting dùng projection/read model.

Class Overview không được join/load answer detail.

---

# 30. Documentation Rules

Khi thay:
- scope -> cập nhật GOAL;
- architecture -> cập nhật SYSTEM_ARCHITECTURE/ARCHITECTURE_RULES;
- domain ownership/model -> cập nhật DOMAIN_MODEL;
- UI flow -> cập nhật UX_STRUCTURE;
- folder convention -> cập nhật PROJECT_STRUCTURE;
- chức năng -> cập nhật FUNCTIONS/SYSTEM_MASTER khi phù hợp.

Không sửa docs chỉ để hợp code sai.
Code phải bám docs đã chốt trừ khi quyết định thay docs trước.

---

# 31. Không Overengineering

Trước khi tạo:
- abstraction;
- service;
- event;
- background worker;
- cache;
- index;
- distributed component;

phải trả lời:
1. vấn đề cụ thể là gì?
2. simplest solution là gì?
3. tại sao cần thêm complexity?

Nếu không trả lời được:
- không thêm.


## 31.1. Thứ tự ưu tiên khi ra quyết định

Coding Agent phải ưu tiên theo thứ tự:

```text
Correct Business Behavior
        ↓
Data Integrity
        ↓
Permission / Isolation
        ↓
Clear Maintainable Code
        ↓
User Experience
        ↓
Performance based on real workload
        ↓
Scalability when evidence requires
```

Không đánh đổi business correctness hoặc UX chỉ để architecture nhìn “xịn”.

---
---

# 32. UI Reference Rules

Frontend RLMS phải sử dụng source local sau làm nguồn tham chiếu thiết kế bắt buộc:

```text
/Users/dangvyhao/Documents/NLT_Tranning/front-end/nlt-smart-gas
```

Source này chỉ dùng để học:
- design language;
- layout pattern;
- interaction pattern;
- component convention;
- design token/convention phù hợp.

Không được copy business logic từ source sang RLMS.

Trước khi tạo hoặc chỉnh sửa frontend UI, Coding Agent phải:

1. Đọc `UX_STRUCTURE.md`.
2. Inspect source local `nlt-smart-gas`.
3. Tìm component/layout/interaction pattern tương tự trước khi tạo mới.
4. Áp dụng design language của source vào đúng nghiệp vụ RLMS.
5. Không thay đổi business flow của RLMS chỉ để giống source tham chiếu.

## 32.1. Sidebar theo role và nghiệp vụ

Sidebar phải học đúng pattern từ source local, đặc biệt:

- nested menu;
- dropdown / collapsible group;
- active child;
- active parent;
- icon convention;
- spacing;
- collapse / expand behavior.

Sidebar phải phản ánh đúng workspace hiện tại, không nhồi toàn bộ chức năng của mọi role vào một menu.

### Student Workspace

Ví dụ:

```text
Trang chủ

Học tập
├── Khóa học / Lớp học
├── Lịch học
└── Tiến độ

Đánh giá
├── Bài tập
├── Bài thi
├── Tiểu luận
└── Điểm số

Cá nhân
└── Thông báo
```

### Teacher Workspace

Phải ưu tiên group nghiệp vụ dạng dropdown giống source:

```text
Tổng quan

Giảng dạy
├── Lớp giảng dạy
├── Khóa học
└── Lịch giảng

Đánh giá
├── Bài tập
├── Bài thi
├── Tiểu luận
├── Chấm bài
└── Sổ điểm

Theo dõi
├── Tiến độ lớp
└── Báo cáo

Giao tiếp
└── Thông báo
```

Không flatten tất cả chức năng thành nhiều menu ngang hàng nếu source có hierarchy rõ.

### Admin Workspace

Ví dụ:

```text
Tổng quan

Hệ thống
├── Tổ chức
├── Người dùng
└── Vai trò & Quyền

Học vụ
├── Khóa học
├── Lớp học
└── Lịch

Vận hành
├── Báo cáo
└── Cấu hình
```

Admin không mặc định được thấy Teacher Live Student Detail nếu không có permission/business use case rõ.

## 32.2. Header Opened Tabs

Header RLMS phải học pattern quản lý các trang đang mở từ source local, tương tự tab trong trình duyệt nhưng là application-level tabs.

Ví dụ:

```text
Dashboard | Lớp giảng dạy | SE01 | Assignment 05 | Gradebook
```

Một opened tab tối thiểu gồm:

```text
key
title
route
closable
```

Khi mở lại cùng một route/business object:
- activate tab hiện có;
- không tạo duplicate tab.

Khi đóng active tab:
- chuyển sang tab hợp lệ gần nhất;
- không để app rơi vào route không hợp lệ.

Router phải là nguồn sự thật của current page.

Sidebar active state, route hiện tại và opened tab active phải luôn đồng bộ.

## 32.3. Dynamic Tab Title

Tab title phải phản ánh business object đang mở.

Đúng:

```text
SE01
Assignment 05
Gradebook
```

Không nên dùng:

```text
Class Detail
Assignment Detail
Page Detail
```

nếu người dùng có thể mở nhiều object cùng loại.

## 32.4. Live Classroom

Live Classroom có thể xuất hiện như một application tab:

```text
SE01 Live
```

Nhưng các state bên trong:

```text
Class Overview
Student Detail
Chat
Participants
Question Heatmap
```

không được tự động tạo thành header tab riêng.

Khi Teacher chuyển:

```text
Student A
→ Student B
```

vẫn ở cùng `SE01 Live` tab.

Chỉ đổi detail subscription:

```text
unsubscribe A
→ load snapshot B
→ subscribe B
```

Không tạo một header tab cho từng sinh viên.

## 32.5. Không Copy Business Logic

Không được copy từ `nlt-smart-gas`:

- API;
- business service;
- domain model;
- entity;
- role;
- permission;
- route nghiệp vụ;
- store nghiệp vụ;
- business text/data.

Chỉ được học:

```text
Visual Pattern
Layout Pattern
Interaction Pattern
Component Convention
Design Tokens
```

## 32.6. Design Priority

Khi có xung đột:

```text
GOAL.md
    ↓
UX_STRUCTURE.md
    ↓
RLMS Business Rules
    ↓
Local UI Reference
```

Source local chỉ quyết định cách trình bày và interaction pattern.

Source local không được phép thay đổi nghiệp vụ RLMS.

## 32.7. Frontend Implementation Rule

Trước khi tạo component mới:

```text
Inspect source
    ↓
Find similar pattern
    ↓
Reuse/adapt pattern
    ↓
Map to RLMS business
    ↓
Implement
    ↓
Compare behavior/style
```

Không:
- tự tạo design system khác nếu source đã có pattern phù hợp;
- mix nhiều UI/icon library không cần thiết;
- tạo folder/component rỗng để chuẩn bị tương lai;
- tải dữ liệu nặng chỉ để giữ UI state;
- biến mỗi Student Detail thành một header tab.

Nếu source local không tồn tại hoặc Coding Agent không đọc được:
- không được tự đoán source đang thiết kế thế nào;
- vẫn phải tuân thủ `UX_STRUCTURE.md`;
- giữ implementation tối thiểu và báo rõ source reference chưa được inspect.
