# ARCHITECTURE_RULES.md

# Nguyên tắc kiến trúc hệ thống học trực tuyến

Tài liệu này là rulebase bắt buộc khi thiết kế và triển khai hệ thống.

Mục tiêu là xây một nền tảng học trực tuyến có LMS + lớp học realtime + theo dõi tiến độ học tập trực tiếp, nhưng không overengineering và không đánh đổi trải nghiệm người dùng để lấy khả năng chịu tải.

---

## 1. Kiến trúc nền tảng

### 1.1. Modular Monolith trước, Microservice sau
- Business core phải bắt đầu bằng **Modular Monolith**.
- Không tách mỗi Bounded Context thành microservice ngay từ đầu.
- Mỗi module phải có boundary rõ, ownership dữ liệu rõ và không truy cập trực tiếp dữ liệu nội bộ của module khác.
- Module được thiết kế để có thể extract thành service sau này nếu có lý do thực tế về tải, deployment hoặc ownership.
- Không extract service chỉ vì "microservice hiện đại hơn".

### 1.2. Media được tách khỏi Business Core
- Video, audio, screen sharing và recording không được chạy chung workload với API nghiệp vụ.
- Business Core chịu trách nhiệm session, permission, activity, progress, assessment...
- Media Layer sử dụng WebRTC + SFU + TURN/STUN hoặc hạ tầng tương đương.
- Media quá tải không được làm API nộp bài, lưu câu trả lời hoặc Gradebook ngừng hoạt động.

### 1.3. Không overengineering
- Chỉ thêm dependency, broker, service hoặc distributed component khi có use case rõ.
- Không tạo abstraction chỉ để "phòng tương lai" nếu chưa có nhu cầu.
- Không biến mọi event thành distributed event.
- Không biến mọi module thành Event Sourcing nếu state-based model đơn giản hơn và đủ đúng.

---

## 2. Nguyên tắc tối ưu hiệu năng

### 2.1. Performance Optimization != Degrading UX
Tối ưu phải nhằm:
- giảm xử lý dư thừa;
- giảm truy vấn dư thừa;
- giảm payload dư thừa;
- giảm fan-out không cần thiết;
- tăng cache hit;
- cô lập workload;
- scale đúng thành phần.

Không được coi các cách sau là giải pháp chịu tải mặc định:
- cố tình làm refresh chậm hơn;
- bắt người học chờ theo đợt;
- giảm chức năng realtime chỉ để server nhẹ;
- giảm chất lượng trải nghiệm chỉ vì kiến trúc chưa đủ tải.

Nếu có trade-off trải nghiệm thật sự, phải được quyết định riêng và ghi rõ.

### 2.2. Shared Once, Fan-out Many
Dữ liệu giống nhau cho nhiều người dùng chỉ được tính toán/query khi cần và phải tái sử dụng.

Ví dụ dữ liệu dùng chung trong một buổi học:
- thông tin lớp;
- thông tin giảng viên;
- bài học hiện tại;
- activity metadata;
- tài nguyên;
- session state dùng chung.

Các dữ liệu này nên được tạo thành `LiveSessionSnapshot`/bootstrap snapshot và cache.

Không để 1.000 người join gây ra 1.000 lần query lại cùng Course/Lesson/Class nếu dữ liệu không thay đổi.

### 2.3. Request Coalescing / Single-flight
Khi nhiều request cùng cần rebuild một cache key:
- chỉ một request được phép rebuild;
- các request còn lại dùng chung kết quả;
- tránh cache stampede vào PostgreSQL.

### 2.4. User-specific vẫn phải xử lý riêng
Không tái sử dụng dữ liệu riêng của người học như:
- authentication;
- enrollment;
- permission;
- attendance;
- presence;
- connection/session id;
- student activity attempt.

Shared data được tái sử dụng, identity/business state riêng vẫn phải kiểm tra độc lập.

---

## 3. Realtime Rules

### 3.1. Realtime phải chia 3 tầng

#### Tầng 1 — Presence
Chỉ chứa dữ liệu nhẹ:
- online/offline;
- joined/left;
- last seen;
- connection state.

#### Tầng 2 — Class Overview
Giảng viên có thể xem toàn lớp với metadata tối thiểu:
- StudentId;
- CurrentQuestion;
- AnsweredCount;
- TotalQuestions;
- Progress;
- Status;
- LastActivityAt;
- HelpRequested.

Không chứa toàn bộ nội dung câu trả lời của tất cả sinh viên.

#### Tầng 3 — Student Detail
Chỉ khi giảng viên mở một bài làm cụ thể mới load:
- answers;
- current answer;
- current question;
- draft;
- answer changes;
- detailed attempt state.

### 3.2. On-demand Detail
- Không broadcast chi tiết bài làm của toàn bộ học viên tới giảng viên.
- Giảng viên mở sinh viên A -> load snapshot A -> subscribe realtime A.
- Giảng viên chuyển sang B -> unsubscribe A -> load snapshot B -> subscribe B.
- Nếu UI thực sự hiển thị nhiều sinh viên song song thì chỉ subscribe đúng số sinh viên đang được hiển thị.

### 3.3. Snapshot + Delta
Khi mở bài làm:
1. load current snapshot;
2. subscribe delta sau snapshot;
3. mỗi thay đổi chỉ gửi phần thay đổi.

Không gửi lại toàn bộ submission sau mỗi thao tác.

Ví dụ hợp lệ:
`AnswerChanged(QuestionId, AnswerId, Version)`

Không hợp lệ:
`FullSubmissionChanged(AllQuestionsAndAnswers...)`

### 3.4. Overview có thể batching
Các thay đổi tiến độ trong cửa sổ rất ngắn có thể được gom thành một `ClassOverviewDelta` để giảm message/frame overhead nhưng vẫn đảm bảo cảm giác realtime.

### 3.5. Text typing không được biến thành domain event theo từng ký tự
Với câu tự luận:
- không lưu mỗi keystroke vào Event Store;
- không broadcast toàn bộ đoạn văn sau mỗi phím gõ;
- có thể dùng patch/delta và gom burst ngắn trước khi gửi;
- autosave là state riêng với chiến lược phù hợp.

### 3.6. Business Processing != UI Broadcasting
Sinh viên vẫn phải lưu answer/progress bình thường kể cả khi không có giảng viên đang xem chi tiết.

Việc có subscriber hay không chỉ quyết định realtime delivery, không quyết định business persistence.

---

## 4. Failure Isolation

Hệ thống được chia thành các plane/workload độc lập tương đối:

### 4.1. Control Plane
- authentication;
- authorization;
- schedule;
- enrollment;
- session permission;
- class/session metadata.

### 4.2. Media Plane
- video;
- audio;
- screen sharing;
- recording.

### 4.3. Class State Plane
- presence;
- attendance;
- current activity;
- progress overview.

### 4.4. Interaction Plane
- chat;
- raise hand;
- help request;
- lightweight classroom interactions.

### 4.5. Student Detail Plane
- answer detail;
- current question detail;
- draft;
- attempt detail realtime.

Rule:
- Chat lỗi không được làm video/class/assignment ngừng hoạt động.
- Notification lỗi không được chặn nộp bài.
- Analytics lỗi không được chặn student answer.
- Media lỗi không được làm mất submission đã được lưu.
- Một plane bị lỗi phải được cô lập tối đa khỏi các plane còn lại.

---

## 5. Mass Join Rules

Khi nhiều sinh viên vào cùng lúc:

### 5.1. Join path phải ngắn
Đường critical path chỉ nên gồm:
1. authenticate;
2. kiểm tra enrollment/permission;
3. xác nhận session có thể join;
4. register presence tối thiểu;
5. trả bootstrap/session snapshot;
6. client kết nối realtime/media.

Không để join phải chờ:
- analytics;
- report rebuild;
- attendance aggregate nặng;
- notification;
- background statistics.

### 5.2. Attendance sau join có thể xử lý event-driven
Sau khi join thành công:
`StudentJoinedSession`
-> cập nhật attendance/projection/background processing.

### 5.3. Static asset không đi qua Business API
PDF, slide, image, video, recording, attachment:
- lưu Object Storage;
- phân phối qua CDN hoặc đường tải phù hợp;
- Business API chỉ quản metadata, permission và signed access.

---

## 6. CQRS, Event Sourcing và Projection

### 6.1. Command side xử lý business
Luồng chuẩn:
`HTTP/Message -> Command -> Wolverine Handler -> Aggregate/Domain -> Business Rules -> Domain Event -> Marten`

### 6.2. UI không query Event Stream trực tiếp
Không replay hàng trăm event mỗi lần mở màn hình.

UI phải query Read Model/Projection phù hợp:
- `ClassOverviewView`;
- `StudentAttemptDetailView`;
- `GradebookView`;
- `LearningProgressView`.

### 6.3. Inline Projection
Dùng cho dữ liệu cần phản ánh ngay:
- current student activity state;
- progress;
- current question;
- submission state quan trọng.

### 6.4. Async Projection
Dùng cho:
- historical analytics;
- class/course statistics;
- question difficulty;
- long-term reports;
- dashboard không nằm trên critical path.

### 6.5. Event Store chỉ lưu event có ý nghĩa nghiệp vụ
Được phép:
- StudentActivityStarted;
- QuestionOpened;
- AnswerSubmitted;
- AnswerChanged;
- ActivitySubmitted;
- HelpRequested.

Không đưa vào Event Store:
- heartbeat mỗi vài giây;
- mouse movement;
- typing indicator;
- connection ping;
- transient websocket state.

---

## 7. Storage Rules

### PostgreSQL + Marten
Dùng cho:
- domain state;
- event store;
- document model;
- projection/read model;
- submission;
- assessment;
- grade;
- learning progress.

### Redis
Dùng cho dữ liệu ngắn hạn / truy cập nhanh:
- cache;
- presence;
- last seen;
- realtime connection state;
- short-lived session state;
- rate/counter cần tốc độ cao.

Redis không phải source of truth cho điểm, submission hoặc domain state quan trọng.

### Object Storage
Dùng cho:
- report files;
- PDF/DOCX;
- slides;
- images;
- lesson videos;
- recordings;
- attachments.

Không lưu binary lớn trực tiếp trong Event Store.

---

## 8. Module Boundary Rules

- Module sở hữu data của module đó.
- Module khác không được query trực tiếp internal table/document nếu chưa có public contract.
- Giao tiếp nội bộ ưu tiên command/event/public query contract.
- Không hard-code role name vào business logic nếu permission mới là rule thật.
- Không để `Course` aggregate sở hữu toàn bộ Class, Student, Activity, Submission, Grade...
- Không tạo monster aggregate.

Các aggregate quan trọng có thể gồm:
- Course;
- Class;
- LiveSession;
- Activity;
- StudentActivity;
- Submission;
- EssayTopic/EssaySubmission khi phù hợp;
- HelpRequest.

---

## 9. AI Essay Review Rules — COMING SOON

Tính năng AI hỗ trợ bài tự luận **không thuộc GOAL triển khai hiện tại**.

Khi triển khai sau này:
- dùng RAG với nguồn do giảng viên cung cấp;
- AI chỉ đưa ra nhận xét hỗ trợ và evidence;
- AI không được chấm điểm;
- AI không được đề xuất điểm;
- AI không được xếp loại;
- AI không được tạo "percentage correct" có thể quy đổi thành điểm;
- AI output không được tự động ghi vào Gradebook;
- giảng viên là người duy nhất xác nhận điểm;
- student submission được xem là untrusted input, chống prompt injection.

Contract AI không được chứa:
- `Score`;
- `SuggestedScore`;
- `Grade`;
- `RecommendedGrade`;
- `PercentageCorrect`.

---

## 10. Essay Submission Rules

Với môn có tiểu luận:
- đề tài được đăng ký theo nhóm;
- đề tài sau khi duyệt là tên chính thức;
- báo cáo phải chứa đúng tên đề tài sau normalization hợp lý;
- sai đề tài -> không cho nộp chính thức;
- file rỗng/gần rỗng -> không cho nộp;
- kiểm tra số trang, số từ/ký tự, non-empty content và required sections;
- ngưỡng phải cấu hình theo bài, không hard-code toàn hệ thống;
- lưu lịch sử phiên bản nếu cho phép nộp lại.

---

## 11. Scalability Rules

- Scale theo workload, không scale toàn hệ thống một cách mù quáng.
- Media scale độc lập Business Core.
- Realtime Gateway có thể scale độc lập khi cần.
- Notification là ứng viên tốt để extract service sớm nếu muốn học distributed system.
- Chỉ extract `StudentActivity` khi số liệu thực tế cho thấy cần scale/deploy độc lập.
- Bắt buộc đo load, latency, throughput, queue depth và error rate trước khi tối ưu sâu.

---

## 12. Quy tắc cuối

> Tính đúng đắn nghiệp vụ, tính toàn vẹn dữ liệu và trải nghiệm học tập không được hy sinh để che giấu một kiến trúc chịu tải kém.

Mọi tối ưu phải ưu tiên:
1. loại bỏ xử lý dư thừa;
2. tái sử dụng dữ liệu chung;
3. truyền đúng dữ liệu cần thiết;
4. dùng đúng storage;
5. cô lập lỗi;
6. scale đúng workload;
7. giữ nguyên business semantics.
