# RLMS_FIX_PROMPTS.md

# RLMS — Prompt Pack sửa lỗi sau E2E Audit

Dùng từng prompt độc lập, theo thứ tự ưu tiên. Mỗi prompt phải audit đúng bug trước, sửa tối thiểu, thêm regression test và retest theo bước tái hiện cũ.

---

## 1. BUG-RLMS-005 — Student Essay Workspace

```md
# Fix BUG-RLMS-005 — Student Essay Workspace

Current confirmed bug:
Student vào `/classes/:classId/essays`, bấm assignment tiểu luận nhưng bị route sang `/essays` và thấy Teacher Workspace.

Đây là P1.

## Mục tiêu
Tạo Student Essay Workspace riêng, không tái sử dụng nguyên Teacher Workspace.

Student flow:
LearningClass
→ Tiểu luận
→ Essay Assignment
→ Student Essay Workspace

Student chỉ được thấy:
- assignment info
- group của chính mình
- thành viên nhóm
- topic status
- canonical approved topic
- upload report
- validation result
- submission versions
- deadline
- feedback/grade đã publish

Student không được thấy:
- Teacher Workspace
- class selector dành cho teacher
- tạo/quản lý group tùy ý
- approve/reject topic
- grading của nhóm khác

## Route
Giữ teacher route riêng.
Student dùng route tương đương:
`/classes/:classId/essays/:essayAssignmentId`

## Security
Backend phải enforce:
Tenant
→ enrolled LearningClass
→ assigned Essay
→ own EssayGroup

Không dựa vào frontend hide.

## Tests
1. student click essay mở Student Essay Workspace
2. không render Teacher Workspace
3. student không gọi được teacher management API
4. chỉ thấy own group
5. đổi groupId không xem được group khác
6. topic status đúng
7. CanonicalTitle hiển thị đúng khi approved
8. report hợp lệ submit được
9. report lỗi không final-submit
10. teacher flow không bị phá

## Verification
Reproduce đúng flow của audit với Student25.
Không mark Done nếu còn route sang Teacher Workspace.
```

---

## 2. BUG-RLMS-003 — Learning Progress Consistency

```md
# Fix BUG-RLMS-003 — Learning Progress Consistency

Current confirmed bug:
Cùng một màn có thể hiển thị:
- 54%
- 0/28 bài hoàn thành
- nhiều lesson row lại Completed

Đây là P1.

Không được chỉ ẩn một trong các số.

## Audit
Trace:
LessonStarted / LessonCompleted
→ LearningProgress
→ Projection / Read Model
→ Teacher Progress
→ Student Progress

Kiểm tra:
- published lesson scope
- completed count
- total count
- percentage
- curriculum/class scope
- frontend có tự tính lại hay không
- projection có stale hay không

## Rule
Cùng Student + LearningClass + Curriculum phải có một canonical source:

CompletedPublishedLessons / TotalPublishedLessons = ProgressPercentage

Draft lesson không được ảnh hưởng student-visible progress.

## Fix
Ưu tiên:
- canonical LearningProgress read model/projection
- backend trả total/completed/percentage
- frontend chỉ render
- không duplicate logic nhiều nơi

## Tests
- 0/28 = 0%
- 14/28 = 50%
- 28/28 = 100%
- draft lesson không tăng denominator
- teacher/student cùng giá trị
- complete lesson cập nhật count + percent
- refresh không revert
- cross-tenant denied

## Verification
Student complete lesson → Progress
Teacher mở cùng student → số liệu phải khớp.
```

---

## 3. BUG-RLMS-009 — LiveSession Start Realtime Propagation

```md
# Fix BUG-RLMS-009 — LiveSession Start Realtime Propagation

Current bug:
Student mở session trước khi start.
Teacher Start.
Student vẫn thấy "Phiên chưa bắt đầu" cho tới khi reload.

Đây là P1.

## Required behavior
Teacher Start
→ backend state changes
→ realtime event
→ preloaded Student tab nhận event
→ UI chuyển sang Started
→ không reload

## Trace
StartLiveSession
→ Domain/Event
→ Persistence
→ Realtime Publisher
→ SignalR group
→ pre-start Student subscription
→ frontend store
→ UI

Kiểm tra:
- pre-start page có subscribe chưa
- group naming/scope
- tenant scope
- frontend event handler
- stale cache/state
- reconnect behavior

## Không được fix bằng
- polling liên tục
- force reload
- fake optimistic state

## Tests
1. Student mở trước start
2. Teacher start
3. Student nhận ngay
4. 2 students cùng nhận
5. class khác không nhận
6. cross-tenant không nhận
7. reconnect restore đúng
8. Ended transition cũng nhận
9. duplicate event không corrupt state
```

---

## 4. BUG-RLMS-008 + BUG-RLMS-010 — LiveSession State + Media UX

```md
# Fix LiveSession State Mapping and Media-Unavailable UX

Address:
- BUG-RLMS-008
- BUG-RLMS-010

## Part A — Ended state
Current bug:
session đã ended nhưng UI/API lại báo "chưa bắt đầu".

Tạo một authoritative mapping:
Scheduled
Started
Ended

Ended phải hiển thị:
`Phiên đã kết thúc`

Join ended session phải trả business error tương đương:
`Live session has ended`

Teacher/Student/API phải dùng cùng semantics.

## Part B — Media unavailable
Phân biệt:
- permission denied
- no camera
- no microphone
- media service unavailable
- screen-share canceled
- unsupported environment

Hiển thị lỗi có hành động rõ ràng, ví dụ Retry / kiểm tra quyền trình duyệt.

Media failure không được làm hỏng:
- LiveSession state
- participant presence
- chat
- assignment/activity
- attendance phù hợp

Không fake media chỉ để test pass.
Nếu môi trường media chưa sẵn sàng thì media vẫn BLOCKED, không được báo PASS giả.
```

---

## 5. BUG-RLMS-004 + 006 + 007 — Scheduling / Attendance / Notifications

```md
# Fix Scheduling, Attendance and Notification Consistency

Address:
- BUG-RLMS-004
- BUG-RLMS-006
- BUG-RLMS-007

Audit shared schedule lifecycle trước khi sửa UI.

## Attendance
Current bug:
chọn session 09/09 nhưng row có timestamp 14/09 và late 7665 phút.

Attendance phải tính theo đúng schedule/live-session occurrence:
- scheduled start
- first join
- last leave
- duration
- late minutes
- status

Không clamp số vô lý; sửa mapping/calculation gốc.

## Schedule
Current bug:
duplicate same-day occurrences với Completed/Cancelled song song.

Một occurrence phải có canonical identity/lifecycle.
Reschedule/cancel không được xuất hiện như nhiều event hiện hành.
Tách:
Current occurrence
vs
History

Không xóa history nếu cần audit.

## Notifications
Current bug:
notification trùng và lẫn English/Vietnamese.

Một business transition chỉ tạo đúng notification mong muốn một lần.

Audit:
- duplicate handlers
- retries/outbox
- replay
- seed
- multiple consumers
- idempotency

Không chỉ dedupe ở frontend.

## Tests
- create/reschedule/cancel
- attendance on-time/late/rejoin
- notification exactly-once theo business intent
- localization
- tenant isolation
```

---

## 6. BUG-RLMS-001 + 002 + 011 — Shell / User Status / Teacher Class Selector

```md
# Fix RLMS Shell and Teacher Navigation UX

Address:
- BUG-RLMS-001
- BUG-RLMS-002
- BUG-RLMS-011

## A. Opened tabs
Khi đổi account/tenant:
- revalidate tabs
- xóa tenant-specific tabs không còn hợp lệ
- không giữ dead class/curriculum/gradebook tabs
- route active phải hợp lệ
- không leak tenant UI state qua local/session storage

Test:
Teacher A → logout → Teacher B
Tenant A → Tenant B
Admin → Teacher

## B. User status
Không render raw `0/1`.

Canonical UI:
Active
Disabled

Action phải mô tả hành động tiếp theo:
Active → Vô hiệu hóa
Disabled → Kích hoạt

Normalize response nếu cần.

## C. Teacher Class Selection
Các trang:
- /activities
- /grading
- /reports
- /progress
- /attendance
- /gradebook

không dùng dropdown làm primary selector nữa.

Dùng pattern:
Search
→ LearningClass cards
→ click class
→ Feature Workspace

Tái sử dụng ONE `TeacherClassSelector` pattern/component.

Card có thể gồm:
- class code
- class name
- course
- student count
- feature-specific metric nếu hữu ích

Sau khi chọn:
- show class context
- có Đổi lớp / Back
- rồi mới hiện workspace
```

---

## 7. Complete Multi-Tenant Isolation Retest

```md
# Complete Multi-Tenant Isolation Retest

Audit V1 bị BLOCKED vì chưa có tenant B.

Tạo tenant test tối thiểu:
Tenant A = RLMS-DEMO
Tenant B = RLMS-DEMO-B

Tenant B chỉ cần:
- Organization
- Teacher
- Student
- Course
- LearningClass
- 1 FileAsset
- 1 Activity
- 1 StudentActivity

Dùng normal business flow, không direct SQL.

## Matrix
Tenant A không được truy cập Tenant B:
- Course
- LearningClass
- Student
- Curriculum
- Lesson
- Activity
- StudentActivity
- Submission
- Gradebook
- Essay
- FileAsset
- Reports
- Realtime overview
- Student detail realtime
- Presence/cache

Test UI/deep link + API khi phù hợp.

Realtime subscription phải reject cross-tenant.

FileAsset không được tải chỉ vì biết ID.

Bất kỳ cross-tenant leak thật sự = P0.
Dừng và report ngay nếu phát hiện.
```

---

## 8. Final Regression Audit V2

```md
# RLMS Regression Audit V2

Sau khi sửa các bug, chạy lại full E2E audit.

Không assume fixed chỉ vì test pass.

Reproduce:
BUG-RLMS-001
BUG-RLMS-002
BUG-RLMS-003
BUG-RLMS-004
BUG-RLMS-005
BUG-RLMS-006
BUG-RLMS-007
BUG-RLMS-008
BUG-RLMS-009
BUG-RLMS-010
BUG-RLMS-011

Mỗi bug:
FIXED
PARTIALLY FIXED
STILL BROKEN
BLOCKED

## Student journey
Login
→ Dashboard
→ LearningClass
→ Nội dung khóa học
→ Curriculum
→ Lesson
→ Progress
→ Assignment
→ Submit
→ Essay
→ Topic
→ Report
→ Gradebook
→ Live Classroom

## Teacher journey
Login
→ Course
→ Curriculum
→ Module/Lesson
→ LearningClass
→ Schedule
→ Assignment
→ Realtime Overview
→ Student Observer
→ Grading
→ Gradebook
→ Essay Review
→ Attendance
→ Reports
→ Live Classroom

## Admin journey
Login
→ Organization
→ Users
→ Roles/Permissions
→ tenant boundary checks

## Output
Tạo:
`RLMS_E2E_AUDIT_V2.md`

Bao gồm:
- PASS / PARTIAL / FAIL / BLOCKED
- P0/P1/P2/P3
- trạng thái 11 bug cũ
- bug mới nếu có
- stable features
- remaining blockers
- so sánh Audit V1 → V2

Kết thúc bằng đúng một verdict:
READY FOR INTERNAL DEMO
READY FOR PILOT WITH KNOWN ISSUES
NOT READY FOR PILOT

Audit trước.
Không sửa thêm trong lúc audit.
Report rồi dừng.
```

---

# Thứ tự chạy khuyến nghị

```text
1. BUG-RLMS-005 — Student Essay
2. BUG-RLMS-003 — Progress
3. BUG-RLMS-009 — Live realtime start
4. BUG-RLMS-008 + 010 — Live state / Media UX
5. BUG-RLMS-004 + 006 + 007 — Schedule / Attendance / Notification
6. BUG-RLMS-001 + 002 + 011 — Shell / Admin / Class selector UX
7. Multi-Tenant Isolation Retest
8. Full E2E Audit V2
```

Ba lỗi ưu tiên cao nhất:
- BUG-RLMS-005
- BUG-RLMS-003
- BUG-RLMS-009

vì chúng trực tiếp làm Student journey chưa sẵn sàng cho pilot.
