# WORKFLOW.md

# Quy trình làm việc của dự án

## 1. Mục tiêu

Workflow này quy định cách phân tích, triển khai, kiểm tra và hoàn tất từng phần của hệ thống.

Mục tiêu:
- code đúng `GOAL.md`;
- không tự mở rộng scope;
- giữ đúng boundary;
- áp dụng Event Storming trước business flow;
- không overengineering;
- build pass không được coi là hoàn thành;
- các hot path realtime/scale phải được kiểm thử đúng workload.

---

# 2. Thứ tự đọc tài liệu trước khi làm việc

Trước khi bắt đầu task/module, phải đọc theo thứ tự:

1. `GOAL.md`
2. `SYSTEM_MASTER.md`
3. `UX_STRUCTURE.md`
4. `DOMAIN_MODEL.md`
5. `PROJECT_STRUCTURE.md`
6. `SYSTEM_ARCHITECTURE.md`
7. `ARCHITECTURE_RULES.md`
8. `FUNCTIONS.md`
9. `WORKFLOW.md`
10. `AGENTS.md`

Nếu task liên quan một module cụ thể, chỉ cần đọc kỹ phần liên quan sau khi đã hiểu các rule chung.

Không triển khai theo suy đoán nếu tài liệu đã có rule rõ.

---

# 3. Nguyên tắc scope

## Được làm
- chức năng nằm trong `GOAL.md`;
- refactor cần thiết để hoàn thành chức năng;
- test;
- fix bug;
- performance work trực tiếp liên quan acceptance của task.

## Không tự làm
- Coming Soon;
- payment/certificate;
- AI RAG;
- microservice hóa;
- dependency mới không cần thiết;
- rewrite module khác không thuộc task;
- đổi public contract không có lý do;
- thay architecture chỉ vì sở thích.

Nếu phát hiện cần thay architecture:
1. mô tả vấn đề;
2. chứng minh rule hiện tại không đủ;
3. cập nhật tài liệu kiến trúc trước hoặc cùng thay đổi;
4. chỉ thay phần tối thiểu cần thiết.

---

# 4. Quy trình cho mỗi module / feature

## Bước 1 — Xác định business goal
Ghi rõ:
- actor;
- mục tiêu;
- input;
- output;
- business rule;
- permission;
- failure cases chính.

Không bắt đầu từ table/entity trước business flow.

## Bước 2 — Event Storming nhỏ cho feature
Xác định khi phù hợp:

```text
Actor
  ↓
Command
  ↓
Aggregate / Domain
  ↓
Business Rule
  ↓
Domain Event
  ↓
Policy / Handler
  ↓
Projection / Read Model
```

Không ép feature CRUD đơn giản phải có event nếu không có business meaning.

Phải phân biệt:
- Command = yêu cầu làm việc;
- Event = việc đã xảy ra;
- Query = đọc state;
- Projection = read model phục vụ query/UI.

## Bước 3 — Xác định ownership
Trước code phải biết:
- module nào sở hữu data;
- aggregate/document nào sở hữu invariant;
- module khác được phép gọi contract nào;
- có cần event hay chỉ direct in-process contract.

Không query internal data của module khác tùy tiện.

## Bước 4 — Chọn persistence pattern
Chỉ chọn Event Sourcing khi:
- event history có ý nghĩa;
- state transition quan trọng;
- replay/projection/audit mang giá trị thật.

Nếu CRUD/document đủ đúng, dùng Marten document model.

## Bước 5 — Xác định Read Model
Nếu UI/query có shape khác write model, tạo projection/read model phù hợp.

Không query Event Stream trực tiếp để render UI.

## Bước 6 — Viết/định nghĩa test cases trước hoặc song song implementation
Tối thiểu:
- happy path;
- permission;
- validation;
- invariant;
- failure case quan trọng.

Với bug:
- cố gắng có regression test nếu hợp lý.

## Bước 7 — Implement tối thiểu đủ scope
- không thêm feature ngoài task;
- không tạo abstraction chưa cần;
- không tạo service mới nếu module hiện tại đủ;
- không sửa module không liên quan nếu không bắt buộc.

## Bước 8 — Verify
Phải chạy:
- format/lint nếu repo có;
- build;
- unit/integration test liên quan;
- smoke test flow;
- kiểm tra log/error.

## Bước 9 — Review boundary
Kiểm tra lại:
- có leak data ownership không;
- có hard-code role thay vì permission không;
- có monster aggregate không;
- có event noise không;
- có query event stream cho UI không;
- có dependency mới không cần không.

## Bước 10 — Hoàn tất
Task chỉ Done khi Definition of Done trong `GOAL.md` đạt.

---

# 5. Workflow theo Phase

Không nhảy vào AI hoặc performance optimization sâu trước khi business flow nền chạy được.

## Phase 0 — Foundation
Mục tiêu:
- app chạy;
- persistence/realtime/storage foundation hoạt động;
- test foundation có sẵn.

Kiểm tra:
- PostgreSQL/Marten;
- Wolverine;
- Redis;
- auth/authz;
- realtime connection;
- object storage integration;
- media boundary;
- health/error/logging.

## Phase 1 — Learning Foundation
Thứ tự khuyến nghị:
1. Organization/User/Permission
2. Course
3. Class/Enrollment
4. Scheduling
5. Resources

Kết quả:
Student có thể được enroll và biết mình học gì/lúc nào.

## Phase 2 — Live Classroom
Thứ tự:
1. LiveSession
2. Join/Leave
3. Presence
4. Media access
5. Chat/Raise Hand
6. Attendance

Kết quả:
Một buổi học thật có thể bắt đầu, sinh viên join và kết thúc.

## Phase 3 — Activity
Thứ tự:
1. Question Bank
2. Assignment/Quiz/Exercise
3. Publish Activity
4. StudentActivity
5. Answer save
6. Submission

Kết quả:
Teacher giao bài và student làm/nộp được.

## Phase 4 — Realtime Core
Thứ tự:
1. Student Activity projection
2. Class Overview
3. Idle/Resume
4. Help Request
5. Student Detail Snapshot
6. Detail Subscription
7. Snapshot + Delta
8. Reconnect
9. switch subscription verification

Đây là phase quan trọng nhất tạo khác biệt sản phẩm.

## Phase 5 — Assessment
1. objective auto-grade;
2. manual grading;
3. rubric;
4. feedback;
5. Gradebook;
6. result publishing;
7. Learning Progress.

## Phase 6 — Exam
Xây trên Question/Activity/Submission đã ổn.

Không tạo một engine hoàn toàn tách biệt nếu có thể reuse domain concept đúng nghĩa.

## Phase 7 — Essay
1. essay assignment;
2. group;
3. topic registration;
4. approval;
5. locked canonical topic;
6. file upload;
7. extraction;
8. pre-submit validation;
9. submission/version;
10. manual grading.

Không triển khai AI.

## Phase 8 — Reporting & Hardening
Sau khi Golden Path chạy ổn:
- async projections;
- reports;
- load tests;
- failure isolation;
- profiling;
- tối ưu dựa trên measurement.

---

# 6. Workflow riêng cho Realtime

## 6.1. Class Overview
Khi student thay đổi trạng thái:
1. business action được xử lý;
2. state/projection được cập nhật;
3. tạo overview delta tối thiểu;
4. push tới overview subscribers.

Không gửi answer content.

## 6.2. Student Detail
Khi teacher mở student:
1. permission check;
2. GET current snapshot;
3. lấy version;
4. join detail subscription;
5. nhận delta sau snapshot.

Khi đổi student:
1. leave group cũ;
2. ngừng nhận detail cũ;
3. load snapshot mới;
4. join group mới.

## 6.3. Reconnect
Client mất kết nối:
1. reconnect;
2. reload snapshot/read model nếu cần;
3. lấy version hiện tại;
4. subscribe lại;
5. không giả định client vẫn có state hoàn chỉnh.

## 6.4. Text/Draft
Không phát event theo từng ký tự.

Autosave/delta phải:
- đủ an toàn;
- không flood server;
- không biến transient typing thành domain history.

---

# 7. Workflow riêng cho Mass Join

Trước khi tối ưu:
1. đo baseline;
2. tìm query trùng;
3. tìm payload trùng;
4. tìm bước đồng bộ không cần thiết.

Join flow:
1. auth;
2. enrollment/permission;
3. session rule;
4. minimal presence;
5. shared snapshot;
6. return;
7. realtime/media connect.

Background:
- attendance aggregation;
- analytics;
- reporting;
- non-critical notification.

## Cache
Khi cùng một snapshot được yêu cầu:
- cache shared data;
- có invalidation rule rõ;
- chống stampede;
- không cache user-specific authorization result để dùng lẫn user.

---

# 8. Workflow riêng cho Essay Submission

## Khi tạo assignment
Teacher cấu hình:
- file types;
- deadline;
- group rules;
- min page/text/content rule nếu có;
- required sections nếu có;
- resubmit policy.

## Khi đăng ký đề tài
```text
Register
→ Pending
→ Teacher Review
→ Approved
→ Canonical Topic Locked
```

Đổi đề tài phải qua flow mới, không sửa âm thầm canonical topic.

## Khi upload report
Chạy pre-validation:
1. file tồn tại;
2. format hợp lệ;
3. đọc được;
4. extract text;
5. content threshold;
6. required sections;
7. extract/locate title;
8. normalize;
9. compare canonical topic.

Nếu fail:
- trả lỗi rõ;
- không tạo final submission.

Nếu pass:
- cho phép final submit;
- lưu version/history theo policy.

---

# 9. Workflow riêng cho Assessment

## Objective questions
Có thể auto-grade nếu:
- đáp án rõ;
- rule deterministic;
- không cần đánh giá chủ quan.

## Subjective / Essay
Chỉ:
- teacher đọc;
- teacher dùng rubric;
- teacher feedback;
- teacher nhập/xác nhận điểm.

AI không nằm trong pipeline hiện tại.

Không được tạo placeholder AI service rồi nối vào grading "để sau dùng".

---

# 10. Workflow kiểm thử

## Mỗi feature
Tối thiểu:
- unit test domain rule quan trọng;
- integration test persistence/handler nếu cần;
- permission test;
- invalid input;
- happy path.

## Realtime
Phải test:
- overview join/leave;
- correct group;
- student A detail;
- A -> B unsubscribe/subscribe;
- reconnect;
- stale version/snapshot;
- nhiều update liên tục.

## Mass Join
Phải test:
- nhiều join đồng thời;
- cache hit;
- concurrent cache miss;
- DB query count/pressure;
- latency distribution;
- error rate.

Không đặt con số SLO tùy ý nếu chưa có requirement. Ghi baseline và cải thiện dựa trên measurement.

## Failure Isolation
Giả lập:
- chat unavailable;
- notification unavailable;
- analytics worker unavailable;
- realtime detail unavailable;
- media failure.

Xác minh core flow còn hoạt động đúng như `GOAL.md`.

## Projection
Kiểm tra:
- inline state đúng ngay sau command;
- async projection có thể catch up;
- lag được quan sát;
- rebuild không phá source of truth.

---

# 11. Workflow performance

Không tối ưu theo cảm giác.

Chu trình:

```text
Measure
  ↓
Find Bottleneck
  ↓
Remove Redundant Work
  ↓
Optimize
  ↓
Load Test Again
  ↓
Compare
```

Ưu tiên:
1. xóa query dư;
2. cache shared data;
3. giảm payload;
4. delta thay full state;
5. batching hợp lý;
6. đúng index;
7. đúng projection;
8. scale component khi thật sự cần.

Không ưu tiên:
- thêm microservice;
- thêm broker;
- thêm database;
- thêm Kubernetes;

nếu bottleneck chưa chứng minh cần chúng.

---

# 12. Workflow thay đổi Event/Contract

Trong giai đoạn phát triển ban đầu, có thể chỉnh contract khi chưa có data cần giữ.

Khi event/public contract đã được dùng bởi dữ liệu hoặc consumer cần tương thích:
- không âm thầm đổi meaning;
- đánh giá migration/versioning;
- cập nhật producer/consumer/test;
- cập nhật docs nếu business meaning thay đổi.

Không tạo versioning framework phức tạp trước khi có nhu cầu thật.

---

# 13. Workflow thay đổi kiến trúc

Một architecture change chỉ hợp lệ khi có:
- business requirement mới;
- bottleneck đã đo;
- reliability issue;
- module ownership issue;
- deployment requirement thực tế.

Trước khi thay:
1. ghi current problem;
2. ghi evidence;
3. đề xuất simplest solution;
4. kiểm tra có vi phạm Architecture Rules không;
5. cập nhật `SYSTEM_ARCHITECTURE.md` / `ARCHITECTURE_RULES.md` nếu quyết định thay.

---

# 14. Quy tắc commit / review

Nếu repository có Git Rulebase riêng, phải theo Git Rulebase đó.

Nếu chưa có:
- commit theo thay đổi logic rõ ràng;
- không gom rất nhiều module không liên quan vào một commit;
- không commit generated/build artifacts không cần thiết;
- review diff trước khi hoàn tất;
- không xóa test chỉ để pipeline pass;
- không disable validation/security để "fix nhanh".

PR/review phải trả lời:
- làm chức năng nào trong GOAL;
- business flow nào;
- test gì;
- có thay contract/schema/event không;
- có ảnh hưởng realtime/performance không;
- có thay architecture không.

---

# 15. Checklist trước khi đánh dấu Done

## Scope
- [ ] Chức năng nằm trong GOAL
- [ ] Không vô tình làm Coming Soon
- [ ] Không mở rộng scope

## Domain
- [ ] Business rule đúng
- [ ] Aggregate/document ownership đúng
- [ ] Không monster aggregate
- [ ] Event chỉ dùng khi có meaning

## Data
- [ ] Module ownership đúng
- [ ] Không query internal data module khác sai boundary
- [ ] Read model đúng use case

## Realtime
- [ ] Không broadcast detail dư thừa
- [ ] Snapshot + Delta nếu là detail
- [ ] Unsubscribe đúng
- [ ] Reconnect đúng

## Performance
- [ ] Không query/shared compute dư
- [ ] Payload hợp lý
- [ ] Cache chỉ dùng đúng loại data
- [ ] Không hạ UX để né bottleneck

## Security/Permission
- [ ] Auth/authz đúng
- [ ] Không hard-code role nếu permission mới là rule
- [ ] Student không xem data ngoài quyền

## Verification
- [ ] Build pass
- [ ] Test pass
- [ ] Smoke flow pass
- [ ] Error case chính pass
- [ ] Diff được review
- [ ] Docs cập nhật nếu cần

---

# 16. Quy tắc dừng

Nếu task yêu cầu một trong các mục sau mà chưa có GOAL mới, phải dừng phần đó và không tự code:
- AI RAG;
- AI grading;
- payment;
- certificate;
- full microservices;
- thay toàn bộ architecture;
- thêm major platform/dependency không có requirement.

Tiếp tục hoàn thành phần còn lại nằm trong scope thay vì tự mở rộng dự án.
