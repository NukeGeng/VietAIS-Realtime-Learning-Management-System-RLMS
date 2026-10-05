# RLMS — Full End-to-End Regression Audit V2

**Audit date:** 2026-09-14  
**Fix verification:** 2026-09-16  
**Environment:** local/dev  
**Target tenant:** RLMS-DEMO  
**Required perspective:** Admin → Teacher → Student  
**Audit rule:** không sửa code, không tạo/xóa/sửa dữ liệu nghiệp vụ trong khi audit. Chỉ đăng nhập, điều hướng và kiểm tra dữ liệu đã có.

## Summary

| Metric | Count |
| --- | ---: |
| Total Features Tested | 60 |
| PASS | 21 |
| PARTIAL | 16 |
| FAIL | 1 |
| BLOCKED | 16 |
| SKIPPED | 6 |
| P0 | 0 |
| P1 | 2 |
| P2 | 5 |
| P3 | 0 |

The counts above are the original audit snapshot. The follow-up below records
which findings were fixed in code and which observations remain blocked by
missing lifecycle fixtures.

## Fix follow-up — 2026-09-16

- Student attempts now expose an explicit interaction contract. Expired,
  closed, unpublished, and not-yet-open attempts render read-only with a
  Vietnamese reason; question navigation waits for server confirmation and
  cannot mutate an expired attempt.
- Teacher observation reuses the canonical Student Attempt Workspace. Its
  route maps back to the activity workspace tab, while A → B changes only the
  observed attempt context.
- Class-first selectors now consume course and enrollment metadata when it is
  available. Teacher progress keeps the decimal class average instead of
  rounding it to a different integer percentage; the UI labels this as an
  average per student.
- Attendance keeps the selected scheduled date/session context and labels any
  linked runtime start/end separately, so actual join history is not presented
  as the scheduled time.
- Schedule notifications are tenant-scoped, localized, and deduplicated in
  the read model while preserving unread state.
- Gradebook and observer labels were localized/clarified.

The active live/media, schedule lifecycle, tenant-isolation, failure-injection,
and performance checks remain blocked until their required fixtures exist.

Stack local đã khởi động qua `run-dev.sh`: frontend `http://127.0.0.1:5173`, API `https://localhost:7041`, PostgreSQL/Redis/object storage và Wolverine đều báo healthy.

Audit đã sử dụng hai phiên trình duyệt độc lập:

- Admin và Teacher trên Chrome.
- Student25 trên Codex In-app Browser.
- Đăng nhập bằng tài khoản demo trong `docs/reference/DEMO_ACCOUNTS.md`.
- Đã refresh các workspace chính, kiểm tra deep-link, đổi vai trò bằng logout/login và mở luồng observer Student Attempt.

## Verification evidence

Các kiểm tra tự động chạy trong lần audit này:

- `npm run typecheck`: **PASS**.
- `npm run build`: **PASS**, 518 module; chỉ có cảnh báo PURE-comment hiện có của SignalR.
- `node --test frontend/tests/*.test.mjs`: **18/18 PASS**. Bao phủ attendance schedule, linked report date, schedule-only flow, live layout/pagination/presenter pinning, attempt availability và observer workspace behavior.
- `dotnet build backend/LearningPlatform.sln --no-restore --disable-build-servers -m:1`: **PASS**, 0 warning, 0 error.
- `dotnet test backend/LearningPlatform.sln --no-restore --disable-build-servers -m:1`: **101/101 PASS** (Unit 40/40, Integration 61/61).
- `git diff --check`: **PASS**.

Integration suite đã chạy lại sau khi hoàn tất bản sửa; dữ liệu UI audit vẫn được giữ nguyên trong quá trình kiểm tra.

## Real-user flows executed

### Admin

- Đăng nhập tenant `RLMS-DEMO`, mở dashboard và xác nhận workspace Admin.
- Mở Organization: thấy mã `RLMS-DEMO`, tên `RLMS Demo University`.
- Mở Users: thấy 32 tài khoản, trạng thái hiển thị rõ `Đang Hoạt Động`/`Đã Vô Hiệu Hóa`, action `Vô hiệu hóa`/`Kích hoạt`; tìm `student25@rlms.local` trả đúng một kết quả.
- Mở Roles: thấy 3 role và permission catalog.
- Logout: app tabs được reset về màn hình login, không giữ route quản trị cũ.

### Teacher

- Dashboard hiển thị 1 lớp, 2 khóa học và workspace Teacher.
- Class selector hiển thị `RLMS-WED-DEMO` và mở được class workspace.
- Class workspace giữ các tab Overview, Content, Resources, Essays trong cùng ngữ cảnh lớp.
- Course list/detail hiển thị 2 course; course chính có 5 module và 32 lessons.
- Activities hiển thị hoạt động draft và 3 hoạt động đã phát hành; draft không cho publish khi chưa có câu hỏi.
- Question Bank hiển thị 32 câu hỏi; version panel hiển thị version hiện tại và version cũ bất biến.
- Grading, Gradebook, Progress, Reports, Attendance đều dùng class-first search/card selector.
- Progress teacher hiển thị 15/28 và 55%.
- Reports hiển thị 25 enrollment, grade/attendance summaries.
- Attendance mở được ngày `Thứ 4 · 09/09/2026`, ca `Buổi sáng · 07:00–11:30 · Đã kết thúc`, bảng lịch sử vào/rời và nút `Xuất Excel (.xls)`.
- Teacher mở được canonical Student Workspace observer; toolbar có `Realtime · Following`, chuyển được sang Inspect và `Theo dõi lại`.

### Student25

- Dashboard, class list và LearningClass mở thành công.
- Class tabs Overview, Nội dung khóa học, Tài nguyên, Tiểu luận nằm trong `/classes/:classId`.
- Nội dung khóa học hiển thị curriculum card `Nền tảng C# Backend`, 5 module, 28 bài, 15/28 hoàn thành; click card mở `/classes/:classId/curriculums/:curriculumId` ở tab ứng dụng mới.
- Curriculum workspace hiển thị module accordion, lesson status, progress và lesson detail; nút `Về nội dung lớp` có route đúng.
- Resources hiển thị 7 tài nguyên; lesson detail hiển thị tài nguyên tương ứng.
- Student essay route hiển thị workspace nộp bài, nhóm, topic approval và trạng thái khóa bài.
- Student gradebook tải được và chỉ hiển thị lớp đang enroll.
- Student direct-link tới `/admin/users` và `/courses` bị đưa về dashboard, không lộ teacher/admin workspace.
- Invalid class id hiển thị error state `Không tải được chi tiết lớp học`.
- Ended live route hiển thị `Đã kết thúc`, `Phiên đã kết thúc`, không hiển thị control join/media.

## Previous bug regression table

| Bug | V1 | V2 | Result |
| --- | --- | --- | --- |
| BUG-RLMS-001 | Open P2 | **FIXED** | Logout rồi login lại reset app tabs về dashboard/login, không giữ tab business object của tài khoản trước. |
| BUG-RLMS-002 | Open P2 | **FIXED** | Admin Users hiển thị status/action labels dễ đọc; search account hoạt động đúng. Không toggle account để tránh sửa dataset. |
| BUG-RLMS-003 | Open P1 | **FIXED** | Teacher progress được trả dưới dạng decimal average và UI ghi rõ `Số bài hoàn thành trung bình / sinh viên`; cùng một 15/28 không còn bị hiểu là hai tiến độ khác nhau. |
| BUG-RLMS-004 | Open P2 | **FIXED (context)** | Attendance giữ ngày/ca theo lịch đã chọn và hiển thị riêng mốc bắt đầu/kết thúc thực tế của live session; lịch sử join/leave không còn bị trình bày như giờ lịch học. |
| BUG-RLMS-005 | Open P1 | **FIXED** | Student mở class Essays và essay workspace đúng route student; teacher management route bị chặn/redirect. |
| BUG-RLMS-006 | Open P2 | **BLOCKED** | Class Overview và Calendar hiện đều `0 lịch`; không có lifecycle schedule đủ dữ liệu để xác nhận dedupe/reschedule/cancel qua UI. |
| BUG-RLMS-007 | Open P2 | **FIXED** | Notification query tenant-scoped, schedule title/body được bản địa hóa và các delivery trùng nội dung được gom theo trạng thái unread/mới nhất. |
| BUG-RLMS-008 | Open P2 | **FIXED** | Hai live route cũ đều hiển thị ended state rõ ràng và không cho join/media control sau khi kết thúc. |
| BUG-RLMS-009 | Open P1 | **BLOCKED** | Không có live session đang chờ/bắt đầu trong UI; các session có sẵn đều ended, nên không thể chạy preloaded Student → Teacher start realtime. |
| BUG-RLMS-010 | Open P1 | **BLOCKED** | Không có active media session để kiểm camera, mic, screen share, permission denied và recovery. |
| BUG-RLMS-011 | Open P2 | **FIXED** | Essays, Activities, Grading, Gradebook, Progress, Reports và Attendance đều hiển thị search/card class selector thay cho dropdown chính. |
| BUG-RLMS-012 | New P1 | **FIXED** | Attempt hết hạn được đánh dấu read-only từ snapshot; điều hướng câu hỏi chờ server xác nhận và UI hiển thị lý do khóa thao tác. |
| BUG-RLMS-013 | New P2 | **FIXED** | Observer route chuẩn hóa về activity workspace tab; không còn tạo application tab riêng cho từng sinh viên. |

## Feature Stability Matrix

`PASS` là luồng đã quan sát được qua UI thật trong phạm vi dữ liệu hiện có. `PARTIAL` là đã mở được nhưng còn thiếu hành động cuối hoặc có sai lệch. `BLOCKED` là không có fixture/trạng thái cần thiết hoặc hành động tiếp theo sẽ sửa dataset. `SKIPPED` là ngoài GOAL.

| ID | Area | Feature | Role | Status | Evidence / limitation |
| --- | --- | --- | --- | --- | --- |
| F01 | Identity | Login, logout, session reset | Admin | PASS | Admin login/logout và app-tab reset quan sát được. |
| F02 | Admin | Dashboard và organization context | Admin | PASS | Dashboard/tenant RLMS-DEMO hiển thị đúng. |
| F03 | Admin | Organization view/update | Admin | PARTIAL | View đã kiểm; không save thay đổi. |
| F04 | Admin | User search/status/actions | Admin | PARTIAL | Search và labels PASS; toggle không thực hiện để giữ dataset. |
| F05 | Admin | Role và permission catalog | Admin | PASS | 3 role và permission lists hiển thị. |
| F06 | Security | Protected deep-link redirect | Student | PASS | `/admin/users` và `/courses` redirect dashboard. |
| F07 | Security | Tenant A/B isolation | All | BLOCKED | Dataset chỉ có RLMS-DEMO, không có tenant thứ hai. |
| F08 | Security | Tenant-scoped realtime/file access | All | BLOCKED | Không có cross-tenant fixture/active subscription. |
| F09 | Teacher | Dashboard/sidebar/active state | Teacher | PASS | Workspace Teacher và nested sidebar hiển thị đúng. |
| F10 | Academics | Course list/detail lifecycle | Teacher | PARTIAL | List/detail và module data PASS; create/edit/archive không chạy. |
| F11 | Learning | Module/lesson management | Teacher | PARTIAL | 5 modules/32 lessons quan sát được; không mutate ordering/content. |
| F12 | Learning | Published content visibility/resources | Teacher/Student | PASS | Student thấy curriculum/resources đã publish; teacher course detail khớp. |
| F13 | Classroom | LearningClass overview và tabs | Teacher/Student | PASS | 4 tabs giữ trong `/classes/:classId`. |
| F14 | Classroom | Enrollment/roster view | Teacher/Student | PASS | Teacher 25 enrollment; Student chỉ thấy enrollment của mình. |
| F15 | Scheduling | Create/recurrence/reschedule/cancel/Calendar | Teacher/Student | BLOCKED | Calendar/Class Overview không có event; không tạo lịch mới. |
| F16 | Assessment | Question Bank/version immutability | Teacher | PASS | 32 questions, version cũ/current hiển thị. |
| F17 | Assessment | Activity configure/publish | Teacher | PARTIAL | Existing activities mở được; draft 0 questions bị disable publish, không tạo/publish mới. |
| F18 | Live | Start/join/leave/end/disconnect | Teacher/Student | BLOCKED | Không có active/pre-start session. |
| F19 | Realtime | Teacher overview lightweight metadata | Teacher | PASS | 25-row overview có progress/status/help metadata. |
| F20 | Realtime | Canonical observer workspace | Teacher | PASS | Student Workspace read-only, snapshot và status toolbar hiển thị. |
| F21 | Grading | Rubric/help queue/grade publication | Teacher | PARTIAL | Rubric/help queue quan sát được; không ghi điểm/publish. |
| F22 | Gradebook | Teacher gradebook items/status | Teacher | PASS | 4 grade items, 25 learners, publish states hiển thị. |
| F23 | Essays | Teacher class-first essay workspace | Teacher | PASS | Search/card selector và assignment workspace hoạt động. |
| F24 | Attendance | Session/date/history/export | Teacher | PARTIAL | Selector/table/export control PASS; schedule context and actual runtime timestamps are now labeled separately, file export chưa chạy. |
| F25 | Reporting | Class/activity/attendance report | Teacher | PASS | Report workspace có enrollment, grades, attendance, assessment health. |
| F26 | Student | Login/dashboard | Student | PASS | Student25 dashboard và tenant context hiển thị. |
| F27 | Student LearningClass | Overview/content/resources/essays tabs | Student | PASS | Các tab vẫn nằm trong class workspace. |
| F28 | Student Learning | Curriculum card → curriculum workspace | Student | PASS | Card selection và route curriculum hoạt động. |
| F29 | Student Learning | Lesson open/complete/progress/refresh | Student | PARTIAL | Lesson detail và progress hiển thị; chưa đánh dấu complete để tránh đổi dữ liệu. |
| F30 | Student Resources | Class/lesson resource access | Student | PASS | 7 class resources và lesson resource hiển thị. |
| F31 | Assessment | Objective attempt navigation/autosave/submit | Student | PARTIAL | Expired attempt is now read-only with a clear reason and regression coverage; a fresh unexpired attempt was not available for a full save/submit UI flow. |
| F32 | Assessment | Subjective autosave/reconnect | Student | BLOCKED | Không có subjective attempt phù hợp để nhập/reconnect an toàn. |
| F33 | Essays | Student essay workspace/role boundary | Student | PASS | Student thấy nhóm/topic/submission state; không lộ teacher editor. |
| F34 | Gradebook | Student own published grades | Student | PARTIAL | Trang tải đúng lớp; header `Kết quả đã công bố` nhưng mọi cell hiện `Chưa publish`. |
| F35 | Live | Student join/wait/media/end | Student | BLOCKED | Chỉ có ended routes, không có active media fixture. |
| F36 | Communication | Announcements/notifications lifecycle | Student/Teacher | PARTIAL | Student inbox remains available; schedule notifications are localized and deduplicated in the read model, while a full create/reschedule/cancel lifecycle is fixture-blocked. |
| F37 | Realtime | Follow/Inspect/Follow Again | Teacher | PASS | Following → Inspecting → Theo dõi lại hoạt động, question không bị mutate student. |
| F38 | Realtime | Student A → Student B observer switch | Teacher | PARTIAL | Snapshot đổi được qua route A → B and both routes resolve to the activity workspace tab; không có active pair để xác nhận unsubscribe/delta. |
| F39 | Realtime | Reconnect/snapshot/version/dedup | All | BLOCKED | Không có active live pair; attempt interaction error ngăn delta test. |
| F40 | Realtime | Help Requested → Acknowledged → Resolved | Student/Teacher | BLOCKED | Không có active live/help request fixture. |
| F41 | Realtime | Preloaded student receives teacher start | Student/Teacher | BLOCKED | Không có pre-start session trong UI. |
| F42 | Failure isolation | Chat failure vs activity | Student/Teacher | BLOCKED | Không có fault-injection surface an toàn. |
| F43 | Failure isolation | Notification failure vs submission | Student/Teacher | BLOCKED | Không có fault-injection surface an toàn. |
| F44 | Failure isolation | Analytics failure vs answer save | Student/Teacher | BLOCKED | Không có fault-injection surface an toàn. |
| F45 | Failure isolation | Media failure vs session/attendance | Student/Teacher | BLOCKED | Không có active media session. |
| F46 | Exams | Time window/limit/attempts/shuffle/release | Teacher/Student | BLOCKED | Existing activity metadata visible; timed lifecycle not executed. |
| F47 | Essays | Report validation/manual grade/published result | Teacher/Student | BLOCKED | Essay locked/no-submit fixture; no report mutation. |
| F48 | Security | Student cannot read another student's detail | Student | PARTIAL | Teacher/admin routes blocked; another student's grade/detail direct-link not exercised. |
| F49 | Attendance | Presence vs durable attendance history | Teacher | PARTIAL | Durable table/history visible; live join/leave source not available. |
| F50 | Navigation | Deep links and refresh | All | PARTIAL | Activity, curriculum, essay, resources, live ended and protected routes checked; not every route/state. |
| F51 | Application shell | Tabs, duplicates, close, reset | All | PARTIAL | Class/curriculum tab behavior, observer tab reuse and logout reset PASS; broader duplicate/close coverage remains partial. |
| F52 | UX/errors | Empty/403/404/ended errors | All | PASS | Invalid class, protected route redirect and ended-live empty states observed. |
| F53 | UX | Layout/live tile/presentation responsive behavior | All | PARTIAL | Automated layout tests pass; active production media layout not available for visual verification. |
| F54 | Performance | Requests/preloads/subscriptions/large list | All | BLOCKED | No network trace/performance profile collected in this audit. |
| F55 | Out of scope | AI Essay Review | All | SKIPPED | Outside GOAL. |
| F56 | Out of scope | RAG/embeddings/vector search | All | SKIPPED | Outside GOAL. |
| F57 | Out of scope | AI grading/suggested score | All | SKIPPED | Outside GOAL. |
| F58 | Out of scope | Payment/commercial packages | All | SKIPPED | Outside GOAL. |
| F59 | Out of scope | Certificate issuance | All | SKIPPED | Outside GOAL. |
| F60 | Out of scope | Full microservices migration | All | SKIPPED | Outside GOAL. |

## Stable Features

Các phần sau đạt PASS trong luồng người dùng thật với dữ liệu hiện có:

- Admin login, organization context, user search/status labels, role catalog và logout reset.
- Teacher dashboard, nested sidebar, LearningClass tabs, class-first selector, course/class/resource views.
- Question Bank version view, teacher activity overview, observer canonical workspace.
- Teacher gradebook/report, teacher essay workspace, ended-live state.
- Student dashboard, LearningClass tabs, curriculum card selection, curriculum workspace, lesson detail, resources và essay workspace.
- Student protected-route redirect, invalid-class error state.
- Observer Follow → Inspect → Follow Again mà không cho teacher sửa attempt.

## Features Requiring Work

### P0

- Không có P0 exposure được xác nhận. Cross-tenant matrix bị thiếu fixture nên chưa thể chứng minh P0 an toàn.

### P1

- Không còn lỗi P1 mở đã xác nhận sau lần sửa này. BUG-RLMS-012 đã được xử lý bằng interaction guard và regression test; các luồng live/media P1 vẫn chỉ là BLOCKED do thiếu fixture.

### P2

- **BUG-RLMS-006:** bổ sung fixture/lifecycle để kiểm schedule occurrence dedupe và canceled history qua Class Overview/Calendar.
- Cross-tenant, live/media, failure isolation và performance tiếp tục ở trạng thái BLOCKED, không được nâng thành lỗi đã xác nhận.

Các mục BUG-RLMS-003, BUG-RLMS-004, BUG-RLMS-007 và BUG-RLMS-013 cùng các UX finding về class metadata/gradebook đã được xử lý; xem bảng regression và phần follow-up ở trên.

### P3

- Không có P3 bắt buộc sau lần retest này.

## New Bugs

### BUG-RLMS-012 — Student attempt rejects interaction while In Progress (**FIXED**)

**Severity:** P1  
**Route:** `/activities/ddf4d7a4-349d-4df9-8440-ebe344c3a985`  
**Actor:** Student25

**Reproduction:**

1. Mở activity `Kiểm tra ASP.NET Core`; UI hiển thị `QUESTION 4 · In Progress`, `2/10 · 20%`, các control làm bài.
2. Bấm nút câu hỏi `5`.
3. UI chuyển phần nội dung sang câu 5 nhưng đồng thời hiển thị `This activity attempt is not accepting interaction.`.
4. Reload rồi lặp lại khi Teacher không observe, lỗi vẫn tái hiện.

**Impact at audit time:** Student không thể điều hướng/tiếp tục attempt đang được hiển thị là In Progress. Realtime current-question delta cũng không được Teacher observer cập nhật trong lần thử này.

**Fix verification:** Snapshot now computes `CanInteract`/`InteractionMessage` from the attempt deadline and activity state. The expired fixture renders disabled controls and `Đã hết thời gian làm bài…`; interactive question navigation waits for the server response. Covered by backend availability tests and frontend regression tests.

### BUG-RLMS-013 — Observer Student Detail tạo application tab riêng (**FIXED**)

**Severity:** P2  
**Route:** `/activities/:activityId/attempts/:attemptId`  
**Actor:** Teacher

Khi Teacher mở Student Attempt, shell từng tạo tab `SV f5d314f3 · Kiểm tra ASP.NET Core` bên cạnh tab Activity. Prompt/architecture yêu cầu observer dùng cùng workspace/live tab, không tạo header tab cho từng student. `applicationTabRoute` hiện chuẩn hóa attempt route về activity workspace; snapshot, Follow và Inspect vẫn hoạt động trong cùng tab.

## Blocked Tests

- **Tenant A/B:** chỉ có tenant RLMS-DEMO; cần tenant thứ hai và tài khoản tương ứng.
- **Schedule lifecycle:** Class Overview và Calendar đều không có event; cần fixture lịch đã tạo, reschedule và cancel.
- **Live start/realtime/media:** các session ID có sẵn đều ended; cần pre-start/started fixture để chạy Teacher/Student đồng thời, camera/mic/screen share, presentation handoff, reconnect và auto-disconnect.
- **Help/failure isolation:** không có active session hoặc fault-injection toggle an toàn.
- **Publish/create flows:** draft course/activity tồn tại nhưng audit không publish/create để tránh đổi dataset.
- **Essay report validation:** assignment đang khóa, không có valid/invalid upload flow an toàn.
- **Performance/network:** không thu network trace trong CUA audit.
- **Integration suite:** đã chạy lại sau bản sửa; Unit 40/40 và Integration 61/61 đều pass. Các fixture-blocked mục trên vẫn chưa có trạng thái cần thiết cho UI retest.

Mỗi blocker cần retest sau khi cung cấp fixture tương ứng; không nên suy ra PASS từ status code hoặc static code.

## UX Findings

- Class selector cards now render course and roster counts from class metadata when available; older records without those fields still use the compact omission fallback.
- Observer toolbar labels are localized (`Trực tiếp`, `Đang theo dõi`, `Đang xem riêng`, `Tổng quan`).
- Attendance distinguishes the scheduled date/session from the linked live session's actual start/end; join history remains tied to the selected live-session id.
- Schedule notifications are localized and duplicate deliveries are collapsed in the inbox read model; a complete schedule lifecycle remains fixture-blocked.
- Student gradebook uses `Điểm của bạn`, while unpublished cells use `Chưa có điểm công bố`.
- Teacher progress explicitly says it is the class average per student, so it is not compared as a personal student percentage.

## Role Verdicts

### Admin — USABLE WITH ISSUES

Dashboard, organization, users, roles, search, protected routing và logout reset hoạt động qua UI. Account toggle, cross-tenant isolation và destructive admin changes chưa được retest để giữ dataset.

### Teacher — USABLE WITH ISSUES

Class-first workflows, course/class views, activities, question versions, grading, gradebook, reports, attendance và observer workspace hoạt động. Schedule/live fixtures, tenant isolation, failure-injection và performance vẫn cần retest; confirmed progress, attendance-context, localization và observer-tab findings đã được xử lý.

### Student — USABLE WITH ISSUES

Student class/content/curriculum/resources/essay/protected routes hoạt động. Expired attempts hiện chuyển read-only đúng quy tắc; full unexpired save/submit và live/media/reconnect vẫn chưa có fixture để xác nhận.

## Pilot Verdict

**NOT READY FOR PILOT**

Các lỗi P1 đã xác nhận trong attempt/progress đã được sửa và có regression coverage. Tuy vậy thiếu active live/media, schedule lifecycle, cross-tenant và failure-injection fixtures nên chưa đủ bằng chứng cho pilot end-to-end.
