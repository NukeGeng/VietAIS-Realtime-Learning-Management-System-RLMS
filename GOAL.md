# GOAL.md

# Mục tiêu triển khai hệ thống học trực tuyến

## 1. Mục đích của tài liệu

`GOAL.md` là nguồn xác định **phạm vi phải triển khai** của dự án.

Khi code, nếu một ý tưởng hoặc chức năng:
- không có trong `GOAL.md`;
- nằm trong `Coming Soon`;
- hoặc mâu thuẫn với `ARCHITECTURE_RULES.md`;

thì **không được tự triển khai**.

Các tài liệu phải đọc cùng:
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

Nếu tài liệu mô tả rộng hơn `GOAL.md`, `GOAL.md` quyết định scope triển khai hiện tại.

---

# 2. Mục tiêu sản phẩm

Xây dựng một nền tảng học trực tuyến kết hợp:

- LMS;
- lớp học trực tuyến realtime;
- lịch học;
- quản lý lớp và khóa học;
- bài tập, quiz, thi/kiểm tra;
- nộp bài và chấm điểm;
- sổ điểm;
- tiến độ học tập;
- tiểu luận theo nhóm;
- báo cáo/phân tích cơ bản;
- theo dõi trạng thái học tập realtime trong buổi học.

Điểm khác biệt cốt lõi:

> Giảng viên có thể vừa dạy trực tuyến, vừa giao hoạt động cho lớp, xem tiến độ của nhiều sinh viên theo thời gian thực và chỉ tải chi tiết bài làm của đúng sinh viên đang được quan sát.

Hệ thống phải hướng tới **Realtime Learning Workspace**, không chỉ là “Google Meet + LMS”.

---

# 3. Nguyên tắc sản phẩm bắt buộc

1. LMS và Live Classroom phải thuộc cùng một learning flow.
2. Realtime dùng để hỗ trợ giảng dạy, không biến hệ thống thành công cụ giám sát xâm lấn.
3. Tối ưu chịu tải bằng kiến trúc và giảm xử lý dư thừa, **không cố tình hạ trải nghiệm**.
4. Dữ liệu dùng chung phải tái sử dụng theo nguyên tắc **Shared Once, Fan-out Many**.
5. Dữ liệu chi tiết bài làm chỉ truyền khi có người thực sự đang xem.
6. Media, chat, notification, analytics và business core phải được cô lập lỗi hợp lý.
7. Business data quan trọng phải có source of truth rõ ràng.
8. Không overengineering.
9. Business Core bắt đầu bằng **Modular Monolith**.
10. Không tự tách microservice nếu chưa có bằng chứng về nhu cầu scale, deployment hoặc failure domain.
11. Không dùng AI để chấm điểm sinh viên.
12. Không triển khai chức năng `Coming Soon` trong GOAL hiện tại.

---

# 4. Vai trò chính

## 4.1. Quản trị viên
Quản lý:
- tổ chức;
- tài khoản;
- vai trò;
- quyền;
- cấu hình hệ thống thuộc phạm vi quản trị.

Không hard-code business permission dựa vào tên role nếu rule thực sự là permission.

## 4.2. Giảng viên
Có thể:
- quản lý khóa học/lớp trong phạm vi được cấp quyền;
- lên lịch;
- tổ chức lớp trực tuyến;
- giao activity;
- quan sát tiến độ;
- mở chi tiết bài làm của sinh viên;
- quản lý bài tập/quiz/thi/tiểu luận;
- chấm bài;
- quản lý điểm và feedback;
- xem báo cáo.

## 4.3. Học viên / Sinh viên
Có thể:
- xem khóa học/lớp được ghi danh;
- xem lịch học;
- tham gia lớp trực tuyến;
- làm activity;
- gửi yêu cầu hỗ trợ;
- nộp bài;
- đăng ký đề tài khi được phép;
- xem tiến độ, điểm và feedback đã được công bố.

---

# 5. Phạm vi chức năng PHẢI triển khai

## 5.1. Tài khoản & Tổ chức
Phải có:
- quản lý tài khoản;
- giảng viên;
- học viên/sinh viên;
- tổ chức;
- vai trò và quyền.

## 5.2. Quản lý Khóa học
Phải có:
- khóa học;
- chương/module;
- bài học;
- cấu trúc nội dung;
- trạng thái nháp/xuất bản phù hợp.

## 5.3. Quản lý Lớp học
Phải có:
- lớp học;
- roster;
- ghi danh;
- phân công giảng viên;
- trạng thái lớp.

## 5.4. Lịch học & Lịch đào tạo
Phải có:
- tạo lịch;
- lịch lặp lại;
- đổi lịch;
- hủy lịch;
- lịch kiểm tra/thi;
- lịch cá nhân.

## 5.5. Lớp học Trực tuyến
Phải hỗ trợ:
- tạo/mở phiên học;
- bắt đầu/kết thúc;
- join/leave;
- participant/presence;
- video;
- audio;
- screen share;
- chat;
- raise hand.

Media phải đi qua Media Layer phù hợp, không truyền media payload qua Business API.

## 5.6. Điểm danh & Chuyên cần
Phải có:
- ghi nhận tham gia/rời;
- thời lượng;
- đi muộn;
- vắng;
- chỉnh sửa bởi người có quyền;
- tỷ lệ chuyên cần.

Attendance không được làm critical join path nặng một cách không cần thiết.

## 5.7. Hoạt động Học tập
Phải có:
- assignment;
- exercise;
- quiz;
- question;
- giao bài cho lớp;
- phát hành activity ngay trong buổi học;
- giới hạn thời gian;
- pause/resume/close khi loại activity hỗ trợ.

## 5.8. Quá trình Học tập Realtime
Đây là **Core Domain**.

Phải có:
- bắt đầu activity;
- current question;
- answered count;
- progress;
- status;
- last activity;
- idle/resume;
- help status;
- realtime overview cho cả lớp;
- question-progress overview/heatmap ở mức phù hợp;
- teacher mở chi tiết một student attempt theo yêu cầu.

### Quy tắc bắt buộc
`Class Overview` chỉ gửi metadata nhẹ.

Không được gửi full answer của cả lớp tới teacher.

`Student Detail`:
1. mở student A;
2. load snapshot A;
3. subscribe detail A;
4. đổi sang B;
5. unsubscribe A;
6. load snapshot B;
7. subscribe B.

Sử dụng **Snapshot + Delta**, không gửi lại full submission sau mỗi thay đổi.

## 5.9. Yêu cầu Hỗ trợ
Phải có:
- request help;
- teacher xem hàng chờ;
- acknowledge;
- resolve;
- thời gian chờ cơ bản.

## 5.10. Nộp bài & Chấm điểm
Phải có:
- lưu câu trả lời;
- chỉnh sửa khi còn được phép;
- submit;
- manual grading;
- feedback;
- rubric;
- publish result.

### Auto grading
Chỉ được tự động chấm các loại câu hỏi có **đáp án xác định và business rule rõ ràng**, ví dụ trắc nghiệm.

Không tự động chấm bài tự luận.

## 5.11. Sổ điểm
Phải có:
- điểm từng bài;
- điểm thành phần;
- trọng số;
- điểm trung bình;
- điểm tổng kết;
- điều chỉnh điểm có lưu vết phù hợp;
- teacher/student grade view theo permission.

## 5.12. Tiến độ Học tập
Phải có:
- course progress;
- lesson progress;
- completion;
- learning history;
- teacher overview theo lớp.

## 5.13. Giao tiếp & Thông báo
Phải có:
- announcement;
- chat;
- thông báo giao bài;
- nhắc deadline;
- đổi/hủy lịch;
- thông báo kết quả;
- in-app notification.

Email notification có thể triển khai nếu hạ tầng/config đã có và không làm thay đổi scope lõi.

Notification lỗi không được chặn business action chính.

## 5.14. Học liệu & Đa phương tiện
Phải hỗ trợ:
- tài liệu;
- slide;
- attachment;
- video bài học;
- recording metadata;
- xem lại recording theo permission.

Binary lớn phải nằm ở Object Storage hoặc storage phù hợp, không lưu trực tiếp trong Event Store.

## 5.15. Ngân hàng Câu hỏi
Phải có:
- lưu/reuse câu hỏi;
- phân loại;
- chọn câu hỏi;
- random theo rule;
- versioning đủ để không làm sai các bài đã phát hành.

## 5.16. Thi & Kiểm tra
Phải có:
- tạo bài thi/kiểm tra;
- thời gian mở/đóng;
- time limit;
- attempt limit;
- shuffle;
- passing score khi cần;
- retake rule;
- result release.

## 5.17. Đề tài & Tiểu luận
Phải có:
- nhóm thực hiện;
- thành viên nhóm;
- đăng ký đề tài theo nhóm;
- giảng viên duyệt/yêu cầu chỉnh sửa/từ chối;
- khóa tên đề tài chính thức sau duyệt;
- quy trình xin đổi đề tài;
- upload báo cáo;
- pre-submission validation;
- nộp lại khi được phép;
- version history;
- group submission;
- trạng thái tiểu luận.

### Kiểm tra tên đề tài
Tên đề tài trong báo cáo phải khớp tên chính thức sau normalization hợp lý:
- trim;
- normalize khoảng trắng/xuống dòng;
- xử lý khác biệt hoa/thường;
- normalize ký tự trình bày phù hợp.

Không fuzzy match quá mạnh làm thay đổi meaning của đề tài.

Sai đề tài -> không tạo submission chính thức.

### Kiểm tra báo cáo rỗng/gần rỗng
Không chỉ kiểm tra file size.

Phải có rule cấu hình cho từng assignment, có thể gồm:
- định dạng đọc được;
- page count;
- extracted text;
- word/character count;
- non-empty content;
- required sections;
- content threshold.

MVP không bắt buộc OCR. Có thể yêu cầu PDF/DOCX trích xuất được text.

## 5.18. Báo cáo & Phân tích
Phải có analytics cơ bản:
- lớp;
- khóa học;
- học viên;
- assignment;
- question;
- attendance;
- historical learning data;
- realtime class summary.

Analytics không nằm trên critical write path của answer/submission.

---

# 6. Chức năng COMING SOON — KHÔNG TRIỂN KHAI

## 6.1. AI hỗ trợ chấm bài tự luận bằng RAG

Chỉ ghi nhận thiết kế để mở rộng sau.

Không được code trong GOAL hiện tại:
- RAG pipeline;
- embedding/vector store;
- AI essay analysis;
- AI evidence review;
- prompt system;
- AI grading.

Khi GOAL tương lai bật chức năng này, nguyên tắc bất biến:
- AI chỉ hỗ trợ giảng viên;
- AI không chấm điểm;
- AI không đề xuất điểm;
- AI không xếp loại;
- AI không tạo percentage correctness có thể quy đổi thành điểm;
- AI không tự ghi Gradebook;
- teacher là người duy nhất xác nhận điểm;
- student submission là untrusted input.

## 6.2. Ngoài scope hiện tại
Không tự triển khai:
- payment;
- gói học thương mại;
- certificate;
- marketplace;
- các chức năng B2C thương mại chưa được GOAL mới chấp thuận;
- microservice hóa toàn hệ thống.

---

# 7. Kiến trúc triển khai bắt buộc

## 7.1. Business Core
- ASP.NET Core;
- Modular Monolith;
- module boundary rõ;
- Wolverine cho command/message flow khi phù hợp;
- Marten + PostgreSQL;
- CQRS;
- Event Sourcing **chỉ cho context có lợi ích thực sự từ event history/state transition**;
- Inline/Async Projection theo nhu cầu.

Không ép mọi module dùng Event Sourcing.

## 7.2. Realtime
- SignalR hoặc realtime transport tương đương theo kiến trúc đã chốt;
- Presence / Overview / Detail tách logic;
- on-demand subscription cho Student Detail;
- reconnect phải có khả năng reload snapshot rồi tiếp tục delta.

## 7.3. Media
- WebRTC;
- SFU;
- TURN/STUN hoặc hạ tầng tương đương;
- scale/failure domain tách Business Core.

## 7.4. Redis
Dùng cho:
- cache;
- LiveSessionSnapshot;
- presence;
- last seen;
- connection mapping;
- ephemeral state;
- short-lived counters khi cần.

Redis không phải source of truth cho:
- grade;
- submission;
- domain state quan trọng.

## 7.5. Object Storage
Dùng cho:
- report;
- PDF/DOCX;
- slide;
- image;
- attachment;
- lesson video;
- recording.

---

# 8. Mass Join phải được thiết kế ngay từ đầu

Critical join path:

```text
Authenticate
    ↓
Check Enrollment / Permission
    ↓
Check LiveSession rule
    ↓
Register minimal Presence
    ↓
Get shared LiveSessionSnapshot
    ↓
Return bootstrap
    ↓
Connect Realtime / Media
```

Dữ liệu dùng chung phải cache/reuse:
- class;
- lesson;
- teacher;
- current activity;
- resource links;
- shared session state.

Dữ liệu riêng vẫn xử lý riêng:
- identity;
- permission;
- enrollment;
- attendance identity;
- connection;
- student attempt.

Khi cache miss đồng thời, phải tránh cache stampede bằng single-flight/request coalescing hoặc cơ chế tương đương.

---

# 9. Failure Isolation bắt buộc

Các workload phải được cô lập ở mức hợp lý:

- Control Plane;
- Media Plane;
- Class State Plane;
- Interaction Plane;
- Student Detail Plane.

Acceptance:
- chat lỗi -> vẫn học/làm bài;
- notification lỗi -> vẫn submit;
- analytics lỗi -> vẫn save answer;
- media lỗi -> không làm mất submission;
- Student Detail realtime lỗi -> Class Overview vẫn có thể hoạt động.

---

# 10. Data & Event Rules

## 10.1. Event có ý nghĩa nghiệp vụ
Ví dụ:
- LiveSessionStarted;
- StudentJoinedLiveSession;
- ActivityPublished;
- StudentActivityStarted;
- QuestionOpened;
- AnswerSubmitted;
- AnswerChanged;
- HelpRequested;
- ActivitySubmitted;
- GradeRecorded.

## 10.2. Không lưu transient noise vào Event Store
Không tạo domain event cho:
- heartbeat;
- mouse movement;
- websocket ping;
- typing indicator;
- từng keystroke.

## 10.3. UI đọc từ Read Model
Không replay event stream để render UI thông thường.

Các read model quan trọng:
- ClassOverviewView;
- StudentAttemptDetailView;
- GradebookView;
- LearningProgressView.

---

# 11. Thứ tự triển khai

Thứ tự dưới đây ưu tiên dependency và Golden Path, không có nghĩa mỗi dòng phải thành microservice.

## Phase 0 — Foundation
- solution/module structure;
- configuration;
- authentication/authorization foundation;
- PostgreSQL + Marten;
- Wolverine;
- Redis;
- Object Storage abstraction/integration tối thiểu;
- realtime foundation;
- media integration boundary;
- logging/error handling/health checks tối thiểu;
- test foundation.

## Phase 1 — Learning Foundation
- Tài khoản & Tổ chức;
- Khóa học;
- Lớp học;
- Enrollment;
- Scheduling;
- Learning Resources.

## Phase 2 — Live Classroom Foundation
- LiveSession;
- join/leave;
- presence;
- media room/access integration;
- chat/raise hand cơ bản;
- attendance.

## Phase 3 — Learning Activity
- Assignment/Exercise/Quiz;
- Question Bank;
- publish activity;
- StudentActivity;
- answer save;
- submission.

## Phase 4 — Realtime Core Differentiator
- Class Overview;
- progress/current question/status;
- idle/resume;
- help request;
- Student Detail on-demand;
- snapshot + delta;
- reconnect;
- switch subscription A -> B đúng rule.

## Phase 5 — Assessment
- auto grading cho objective question;
- manual grading;
- rubric;
- feedback;
- Gradebook;
- result publication;
- Learning Progress.

## Phase 6 — Exam
- exam rules;
- attempts;
- time/limit/shuffle/pass/retake;
- result release.

## Phase 7 — Essay
- groups;
- topic registration/approval;
- locked canonical topic;
- pre-submission validation;
- empty/near-empty report validation;
- group submission;
- resubmission/version history;
- manual grading.

## Phase 8 — Reporting & Hardening
- reports/analytics cơ bản;
- async projections cần thiết;
- mass join load test;
- overview load test;
- detail switching load test;
- answer burst;
- reconnect;
- cache stampede;
- projection lag;
- failure isolation verification.

---

# 12. Definition of Done cho một module

Một module chỉ được xem là hoàn thành khi:

- business flow đúng `GOAL.md`;
- boundary đúng `ARCHITECTURE_RULES.md`;
- command/query/event naming phản ánh đúng nghiệp vụ;
- invariant quan trọng có test;
- endpoint/handler chạy được;
- persistence đúng ownership;
- projection/query chạy đúng nếu có;
- permission được kiểm tra;
- validation đầy đủ;
- error cases chính được xử lý;
- build pass;
- automated tests liên quan pass;
- smoke test flow chính pass;
- không làm hỏng module khác;
- không thêm dependency/abstraction không cần thiết;
- tài liệu thay đổi nếu contract/architecture thay đổi.

Build thành công **không đồng nghĩa Done**.

---

# 13. Definition of Done cho hệ thống hiện tại

GOAL hiện tại chỉ hoàn thành khi có thể chạy end-to-end tối thiểu các flow sau.

## Flow A — Khóa học và lớp
```text
Admin/Teacher
→ tạo Course
→ tạo Class
→ enroll Student
→ tạo Schedule
→ Student xem được lớp/lịch
```

## Flow B — Lớp trực tuyến
```text
Teacher Start Session
→ Students Join
→ Presence/Attendance ghi nhận
→ Media/Chat hoạt động theo permission
→ Session End
```

## Flow C — Realtime Activity
```text
Teacher Publish Activity
→ Students Start
→ Answer/Question/Progress cập nhật
→ Teacher Overview thấy cả lớp
→ Teacher mở Student A
→ xem realtime A
→ chuyển Student B
→ A unsubscribed, B subscribed
→ Student Submit
```

## Flow D — Chấm điểm
```text
Submission
→ objective auto-grade khi phù hợp
hoặc teacher manual-grade
→ feedback/rubric
→ Gradebook
→ result published
→ Student xem kết quả
```

## Flow E — Tiểu luận
```text
Create Essay Assignment
→ Create Group
→ Register Topic
→ Teacher Approve
→ Topic Locked
→ Upload Report
→ Validate File/Content/Topic
→ Submit
→ Teacher Manual Grade
→ Gradebook
```

## Flow F — Failure Isolation
Phải chứng minh tối thiểu:
- chat hỏng không chặn answer/submit;
- notification hỏng không chặn nghiệp vụ lõi;
- analytics hỏng không chặn answer;
- detail realtime lỗi không buộc overview chết theo.

---

# 14. Quy tắc không overengineering

Không được tự động thêm:
- API Gateway cho một app chưa cần;
- Kubernetes;
- service mesh;
- nhiều database chỉ để giống microservices;
- distributed saga nếu business flow chưa cần;
- RabbitMQ chỉ vì có event;
- generic repository bọc Marten không có giá trị;
- abstraction nhiều tầng không mang business meaning;
- Event Sourcing cho CRUD đơn giản không cần lịch sử event;
- AI infrastructure trong scope hiện tại.

Nếu một component mới được đề xuất, phải trả lời được:
1. vấn đề hiện tại là gì;
2. component giải quyết vấn đề nào;
3. tại sao giải pháp đơn giản hơn không đủ.

---

# 15. Tiêu chí double-check scope

GOAL hiện tại đã bao phủ đầy đủ các nhóm chức năng trong `SYSTEM_MASTER.md` thuộc scope triển khai:

- [x] Tài khoản & Tổ chức
- [x] Quản lý Khóa học
- [x] Quản lý Lớp học
- [x] Lịch học & Lịch đào tạo
- [x] Lớp học Trực tuyến
- [x] Điểm danh & Chuyên cần
- [x] Hoạt động Học tập
- [x] Quá trình Học tập Realtime
- [x] Yêu cầu Hỗ trợ
- [x] Nộp bài & Chấm điểm
- [x] Sổ điểm
- [x] Tiến độ Học tập
- [x] Giao tiếp & Thông báo
- [x] Học liệu & Đa phương tiện
- [x] Ngân hàng Câu hỏi
- [x] Thi & Kiểm tra
- [x] Đề tài & Tiểu luận
- [x] Báo cáo & Phân tích
- [x] Mass Join
- [x] Realtime On-demand Detail
- [x] Failure Isolation
- [x] Storage split
- [x] Media isolation
- [x] Load-test workloads

Được mô tả nhưng cố ý không triển khai:
- [ ] AI Essay Review / RAG — Coming Soon
- [ ] Payment
- [ ] Commercial Learning Packages
- [ ] Certificate
- [ ] Full Microservices Migration
