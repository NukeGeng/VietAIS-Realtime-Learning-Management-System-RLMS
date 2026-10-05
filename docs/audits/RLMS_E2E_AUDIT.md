# RLMS End-to-End User Acceptance Audit

Audit ngày 2026-09-14 trên môi trường local/dev, tenant RLMS-DEMO. Đây là lượt audit đầu tiên theo luồng người dùng thực tế Admin → Teacher → Student. Trong lượt này không sửa mã, không ghi trực tiếp vào database và không dùng thao tác tắt lỗi để làm test pass. Các tài khoản, khóa học, activity và phiên live audit được tạo qua giao diện bình thường vẫn được giữ lại để phục vụ retest.

Total Features Tested: 70
PASS: 46
PARTIAL: 16
FAIL: 2
BLOCKED: 2
SKIPPED: 4

P0: 0
P1: 4
P2: 7
P3: 0

## Environment and test data

- Frontend: http://127.0.0.1:5173 (student) và http://localhost:5173 (teacher/admin), Vite local.
- Backend: https://localhost:7041, API local đang hoạt động.
- Tenant: RLMS-DEMO, organization id dad90798-30ed-815c-8081-614235cf7a3e.
- LearningClass: RLMS-WED-DEMO / Lớp C# Backend — Thứ 4, id 05c8e88b-ecf4-42a0-b21d-4d23169385c0.
- Course: CSHARP-BE / C# Backend Development.
- Curriculum: Nền tảng C# Backend, 5 module, 28 lesson được publish cho student.
- Teacher: teacher.demo@rlms.local.
- Admin: admin.demo@rlms.local.
- Students used: student01@rlms.local và student25@rlms.local.
- Existing demo assessment data: 3 published activities, 25 enrolled students, submitted/in-progress/idle attempts, gradebook, essay groups/submissions, attendance history and announcements.
- Audit-created data: one teacher, one student, one draft course with module/lesson, one question with version 2, one draft activity. These were created through UI and are intentionally not removed in an audit pass.

## Feature Stability Matrix

| ID | Area | Feature | Role | Status | Notes |
| --- | --- | --- | --- | --- | --- |
| F01 | Identity | Login, logout and session persistence | Admin | PASS | RLMS-DEMO admin logged in and reached the correct workspace. |
| F02 | Identity | Invalid credentials and unauthenticated route handling | Admin | PASS | Invalid/unauthenticated navigation returned to login or dashboard without exposing protected content. |
| F03 | Organization | View and update organization metadata | Admin | PASS | Organization page loaded RLMS-DEMO metadata and save workflow was available. |
| F04 | Organization | Create organization | Admin | SKIPPED | Current UI does not expose organization creation in this environment. |
| F05 | Users | Create teacher account | Admin | PASS | Created e2e.audit.teacher@rlms.local through the user form. |
| F06 | Users | Create student account | Admin | PASS | Created e2e.audit.student@rlms.local through the user form. |
| F07 | Users | Search/filter users | Admin | PASS | Search reduced the list to the matching audit account. |
| F08 | Users | Disable/enable user state and action label | Admin | PARTIAL | Toggle works for the created account, but existing records render numeric status/action combinations inconsistently. BUG-RLMS-002. |
| F09 | Users | Edit user profile | Admin | SKIPPED | No edit workflow was exposed in the current user page. |
| F10 | Roles | Role catalog and permission display | Admin | PASS | Administrator, Teacher and Student roles/permission catalog rendered. |
| F11 | Roles | Permission mutation and effective authorization | Admin | PARTIAL | Backend denial for protected routes was verified, but a complete assign/remove-permission UI workflow was not exposed for this audit. |
| F12 | Multi-tenant | Tenant A to Tenant B isolation | Admin | BLOCKED | No usable second tenant was present. Invalid old-class probe returned 404; full cross-tenant matrix requires a second tenant fixture. |
| F13 | Multi-tenant | Invalid/foreign class read probe | Admin/Teacher/Student | PASS | Old RLMS-LOCAL class id returned 404 for all three roles. |
| F14 | Teacher workspace | Dashboard and teacher sidebar | Teacher | PASS | Teacher dashboard, nested groups and class/activity summaries loaded. |
| F15 | Teacher workspace | Nested menu active state | Teacher | PASS | Giảng dạy and Đánh giá groups expanded/collapsed and route links were usable. |
| F16 | Navigation | Opened tabs, duplicate suppression and close | Teacher | PARTIAL | Existing tabs stay visible and route tabs open, but stale tabs can survive account/tenant transitions. BUG-RLMS-001. |
| F17 | Academics | Create Course as draft | Teacher | PASS | E2E-AUDIT course was created through /courses and appeared in teacher views. |
| F18 | Academics | Edit Course | Teacher | SKIPPED | Course edit action was not exercised in this pass. |
| F19 | Academics | Publish/archive Course | Teacher | PARTIAL | Draft state is represented; a complete publish/archive transition was not completed for the audit-created course. |
| F20 | Learning | Create curriculum modules and lessons with ordering | Teacher | PASS | Audit module and lesson were created after selecting the module; ordering rendered consistently. |
| F21 | Learning | Lesson draft/published visibility | Teacher/Student | PARTIAL | Published demo lessons are visible and unpublished content is hidden, but the full teacher publish transition for the audit lesson was not exercised. |
| F22 | Classroom | Class detail, status and class tabs | Teacher | PASS | Overview, content, resources and essays tabs remained in the class context. |
| F23 | Classroom | Teacher assignment and roster view | Teacher | PASS | Assigned teacher and 25-person roster rendered; teacher controls were visible. |
| F24 | Enrollment | Duplicate enrollment and removal | Teacher | PARTIAL | Existing roster and normal enrollment view work; complete duplicate/remove matrix was not run against the demo roster. |
| F25 | Scheduling | Create, recurring, reschedule and cancel schedule | Teacher | PARTIAL | Existing dates/times/statuses are shown, but the full create/reschedule path was not run; duplicate/canceled fixtures are visible. BUG-RLMS-006. |
| F26 | Student workspace | Student dashboard and class access | Student | PASS | Student reached RLMS-DEMO dashboard and enrolled class. |
| F27 | Student workspace | Class tabs remain inside LearningClass | Student | PASS | Overview/content/resources/essays links kept the class route and shell. |
| F28 | Learning | Student content curriculum cards | Student | PASS | Class content showed the assigned curriculum card and progress. |
| F29 | Learning | Curriculum workspace and module accordion | Student | PASS | Dedicated curriculum route rendered modules and expandable lesson lists. |
| F30 | Learning | Lesson detail, status and progress | Student | PASS | Lesson opened on demand; marking complete updated the visible progress state. |
| F31 | Learning | Unpublished lessons hidden | Student | PASS | Student saw 28 published lessons while unpublished demo content remained unavailable. |
| F32 | Resources | Class and lesson resources | Student | PASS | Seven class resources rendered with student-safe view/download actions. |
| F33 | Assessment | Create/edit question bank item | Teacher | PASS | Audit multiple-choice question was created and opened in version management. |
| F34 | Assessment | Published question version immutability | Teacher | PASS | Version 1 remained immutable while version 2 became current. |
| F35 | Assessment | Create assignment with explicit class context | Teacher | PASS | Activity form required a class and created the audit draft. |
| F36 | Assessment | Activity validation and publish readiness | Teacher | PARTIAL | Draft with zero questions correctly disabled publishing and explained the requirement; full publish of the audit activity was not completed. |
| F37 | Assessment | Student objective start, autosave and navigation | Student | PASS | Student25 started the ASP.NET activity, answered Q1, navigated to Q2/Q4 and saw saved progress. |
| F38 | Assessment | Submit confirmation and cancellation | Student | PASS | Submit confirmation appeared; cancel returned to the workspace and unanswered validation was shown. |
| F39 | Assessment | Objective auto-grading and result visibility | Student/Teacher | PASS | Existing submitted quiz remained read-only; published gradebook result was visible only to the owning student. |
| F40 | Assessment | Student subjective/essay attempt route | Student | FAIL | Class essay assignment link opened teacher essay workspace instead of a student submission workspace. BUG-RLMS-005. |
| F41 | Realtime | Teacher class overview metadata | Teacher | PASS | Overview showed 25 learners, current question, progress, status, idle and help columns. |
| F42 | Realtime | Same student workspace in read-only observer mode | Teacher | PASS | Observer toolbar and canonical question workspace rendered without answer mutation controls. |
| F43 | Realtime | Follow, Inspect and Follow Again | Teacher | PASS | Student question movement was followed; inspect stopped follow and Follow Again returned to the current question. |
| F44 | Realtime | Switch Student A to Student B | Teacher | PASS | A observer route was opened, returned to overview and switched to B; toolbar and snapshot changed to B. |
| F45 | Realtime | Reconnect and snapshot reload | Teacher | PASS | Fresh observer reload restored the attempt snapshot and current question/progress. |
| F46 | Realtime | Help request lifecycle | Student/Teacher | PASS | Requested → Acknowledged → Resolved was exercised without stopping the activity. |
| F47 | Assessment | Final submission and duplicate/idempotence behavior | Student | PARTIAL | Existing final submission is read-only and normal submit works; full duplicate/resubmit policy matrix was not completed. |
| F48 | Grading | Rubric and subjective teacher grading | Teacher | PASS | Rubric criterion was saved; essay submission received teacher score, feedback and publication. |
| F49 | Gradebook | Teacher gradebook | Teacher | PASS | Four grade items and 25 learners rendered with publication status. |
| F50 | Gradebook | Student published/unpublished scope | Student | PASS | Student gradebook showed only the own row and hid unpublished scores; published essay score appeared for Student01. |
| F51 | Exams | Exam policy and complete timed attempt | Teacher/Student | PARTIAL | Exam fields exist (time limit, attempt limit, shuffle, passing and release), but a complete timed exam attempt was not run. |
| F52 | Essays | Teacher essay assignment and group policy | Teacher | PASS | Class-first essay workspace, assignment, six groups and member counts rendered. |
| F53 | Essays | Topic/report validation and teacher review | Teacher | PASS | Approved topic/canonical title, valid and rejected report versions, and concrete validation errors were visible. |
| F54 | Essays | Student group/topic/report submission | Student | FAIL | Student assignment link leaves the student workspace and opens teacher management UI. BUG-RLMS-005. |
| F55 | Communication | Notifications read/unread and target | Student/Teacher | PARTIAL | Read/unread interaction works, but duplicate schedule notifications and mixed Vietnamese/English content were observed. BUG-RLMS-007. |
| F56 | Communication | Class announcements and enrollment visibility | Teacher/Student | PASS | Class announcements rendered in the class context; student did not receive teacher-only controls. |
| F57 | Attendance | Attendance status, history, duration and Excel action | Teacher | PARTIAL | Table and export action rendered, but selected-session timestamps/late minutes are inconsistent and download could not be independently verified. BUG-RLMS-004. |
| F58 | Live Classroom | Start, join, leave and end session | Teacher/Student | PASS | Teacher started; Student25 joined; teacher ended; both browser tabs returned to class and API rejected later join. |
| F59 | Live Classroom | Start status propagation to preloaded student tab | Student | PARTIAL | A tab loaded before start remained pre-start until reload. BUG-RLMS-009. |
| F60 | Live Classroom | Camera, microphone and screen-share media | Teacher/Student | BLOCKED | Local browser had no usable media device/SFU path; session controls were reachable but media output could not be accepted as a product result. BUG-RLMS-010. |
| F61 | Reporting | Class, course, activity and essay summaries | Teacher/Admin | PASS | Reporting summary endpoints and teacher pages returned scoped demo metrics. |
| F62 | Reporting | Student own vs other-student permission | Student | PASS | Student own report returned; class summary, activity summary and another student's report returned 403. |
| F63 | Permission | Role-separated direct routes/deep links | Student/Teacher | PASS | Student attempts to teacher/admin routes redirected/denied; teacher admin route was denied. |
| F64 | Navigation | Browser refresh and deep links | Student | PASS | Class content, curriculum, activity, gradebook and essay routes retained context on refresh. |
| F65 | Empty states | Empty curriculum/lesson/assignment/notification states | Student/Teacher | PASS | Empty states were meaningful; no 0/0 progress hero was shown in tested student content. |
| F66 | Error states | Invalid ID, not-found and permission-denied feedback | All | PASS | 404/403 paths did not expose stack traces; user-facing pages showed understandable fallback. |
| F67 | UX | Class selection on teacher feature pages | Teacher | PARTIAL | Activities, grading, reports, progress, attendance and gradebook still rely on dropdown selectors instead of the visual class-first search/card pattern. BUG-RLMS-011. |
| F68 | UX | Desktop responsive/layout smoke review | All | PARTIAL | Desktop flows are usable, but dense tables, long lists and live/media states need a wider responsive review. |
| F69 | Performance | Obvious duplicate-load/list-size smoke review | All | PARTIAL | No load test was run; progress/report screens expose large lists and this pass did not measure request count or latency. |
| F70 | Scope | AI/RAG/AI grading/payment/certificate/full microservice | All | SKIPPED | Explicitly outside the current GOAL. |

## Bugs

## BUG-RLMS-001 — Stale opened tabs survive role/tenant transitions

**Severity:** P2
**Status:** Open
**Role:** Admin/Teacher
**Feature:** Application opened tabs and route synchronization
**Route:** Application shell; old class route /classes/977bb80f-0525-4c75-ac64-ebf145ebc30b

### Steps to reproduce

1. Leave an older RLMS-LOCAL session with its class tab open.
2. Log in to RLMS-DEMO as Admin or Teacher.
3. Select the old opened class tab.

### Expected

The tab is closed, revalidated for the new account/tenant, or routes to a valid page.

### Actual

The stale tab remains selectable and the class page shows “Không tải được chi tiết lớp học.” No data exposure was observed.

### Reproducibility

Always with the pre-existing stale tab set.

### Evidence

The shell retained an old 977bb... tab after the current RLMS-DEMO session was active.

### Suspected area

Frontend navigation/session reset and opened-tab lifecycle.

## BUG-RLMS-002 — User status values and enable/disable labels are inconsistent

**Severity:** P2
**Status:** Open
**Role:** Admin
**Feature:** User management
**Route:** /admin/users

### Steps to reproduce

1. Log in as RLMS-DEMO Admin.
2. Open Users.
3. Compare existing demo/debug accounts and the newly created audit accounts.
4. Toggle an audit account.

### Expected

Every account uses a readable status (Active/Disabled) and the action always describes the next valid operation.

### Actual

Some existing rows render numeric values 0/1 and an action that conflicts with the apparent state. The created account becomes Active/Disable after toggling, so the same list mixes two representations.

### Reproducibility

Always on the current mixed demo list.

### Evidence

The accessibility tree showed numeric status values and Enable/Disable actions before the audit account was toggled.

### Suspected area

Frontend status mapping and account response normalization.

## BUG-RLMS-003 — Progress summary contradicts lesson records

**Severity:** P1
**Status:** Open
**Role:** Teacher/Student
**Feature:** Learning progress
**Route:** /progress

### Steps to reproduce

1. Log in as Teacher and open Progress for RLMS-WED-DEMO.
2. Observe the top summary and lesson rows.
3. Repeat as Student01 or Student25.

### Expected

The headline count, percentage and lesson rows are calculated from one consistent student/class scope.

### Actual

The page showed “54% hoàn thành” and “Tiến độ khóa học 54%” while also showing “Bài học hoàn thành 0/28”; many rows below were marked completed. Student and teacher views reproduced the contradiction.

### Reproducibility

Always on the RLMS-DEMO progress page.

### Evidence

Visible page values: 54%, 0/28 and completed row values in the same response.

### Suspected area

Frontend summary calculation or inconsistent progress read models.

## BUG-RLMS-004 — Attendance time and late-minute values do not match the selected session

**Severity:** P2
**Status:** Open
**Role:** Teacher
**Feature:** Attendance
**Route:** /attendance

### Steps to reproduce

1. Select RLMS-WED-DEMO.
2. Select the 09/09/2026 morning session.
3. Inspect the first attendance rows and history columns.

### Expected

Join/leave timestamps, duration and late minutes are relative to the selected 09/09 session.

### Actual

Rows showed 14/09/2026 timestamps and “7665 phút” late against the 09/09 session, with tiny fractional duration values. The table structure and history columns loaded, but the reported values are not credible for the selected session.

### Reproducibility

Always on the current seeded attendance records.

### Evidence

The attendance UI displayed 15:00 14/9/26 and 7665 minutes for a 09/09 session.

### Suspected area

Attendance fixture/state mapping and session-time calculation.

## BUG-RLMS-005 — Student essay link opens teacher management workspace

**Severity:** P1
**Status:** Open
**Role:** Student
**Feature:** Student essay workflow and role separation
**Route:** /classes/05c8e88b-ecf4-42a0-b21d-4d23169385c0/essays → /essays

### Steps to reproduce

1. Log in as Student25.
2. Open the class Essays tab.
3. Click the published essay assignment.
4. Follow the resulting route and open the class card if shown.

### Expected

Student stays in a student essay workspace with only own group/topic/report submission actions.

### Actual

The link routes to /essays and renders “TEACHER WORKSPACE”, class management UI and teacher actions such as Tạo nhóm. Student API group scope remained limited to the student's own group in direct read checks, so a backend cross-group leak was not proven; the normal UI flow is nevertheless unusable and violates role separation.

### Reproducibility

Always with the current student assignment link.

### Evidence

Student25 browser showed TEACHER WORKSPACE and the teacher class selector after following the class assignment link.

### Suspected area

Frontend route selection and student/teacher essay page boundary.

## BUG-RLMS-006 — Duplicate and canceled schedules clutter class/calendar views

**Severity:** P2
**Status:** Open
**Role:** Teacher/Student
**Feature:** Scheduling and calendar
**Route:** /classes/05c8e88b-ecf4-42a0-b21d-4d23169385c0/overview and /calendar

### Steps to reproduce

1. Open the demo class overview.
2. Inspect Lịch học.
3. Open Calendar as a student.

### Expected

One canonical schedule occurrence is shown with a clear state and canceled history is separated or collapsed.

### Actual

The class showed six entries including duplicate 09/09 and 17/09 occurrences with conflicting completed/canceled states. Calendar showed four events on the same Thursday, mostly canceled.

### Reproducibility

Always with current demo schedule data.

### Evidence

Overview and calendar accessibility trees listed repeated same-day/time events.

### Suspected area

Schedule projection/deduplication and calendar presentation.

## BUG-RLMS-007 — Duplicate notifications and mixed-language content

**Severity:** P2
**Status:** Open
**Role:** Student
**Feature:** Notifications
**Route:** /notifications

### Steps to reproduce

1. Log in as Student25.
2. Open Notifications.
3. Review unread schedule notifications and mark one read.

### Expected

Each business event produces one localized notification with a clear target.

### Actual

The page contained repeated “Class session cancelled”, “New class session” and “Class session changed” items, with English text mixed into the Vietnamese UI. Read/unread interaction itself worked.

### Reproducibility

Always with the current seeded notification set.

### Evidence

20 unread items were initially displayed, including repeated schedule events; after marking one read the count became 19.

### Suspected area

Notification event deduplication and localization.

## BUG-RLMS-008 — Ended live session is rendered as “not started”

**Severity:** P2
**Status:** Open
**Role:** Teacher/Student
**Feature:** Live session state and error messaging
**Route:** /classes/05c8e88b-ecf4-42a0-b21d-4d23169385c0/live/0d993f85-bc7d-4a2f-80a9-d97ba72754c8

### Steps to reproduce

1. Open an ended demo session URL.
2. Compare header, center state, footer and API session status.
3. Attempt to join after it has ended.

### Expected

The UI says the session ended and explains that joining is unavailable.

### Actual

The header/center state said “Chưa bắt đầu” and “Phiên chưa bắt đầu” with zero participants while the footer said “Phiên đã kết thúc”. API join returned 409 with “The live session has not started”, which is also misleading for an ended session.

### Reproducibility

Always for the existing ended session.

### Evidence

API status was 2 with both startedAt and endedAt; browser rendered contradictory not-started/end labels.

### Suspected area

LiveSession status mapping and conflict error message.

## BUG-RLMS-009 — Preloaded student live tab does not receive start transition

**Severity:** P1
**Status:** Open
**Role:** Student
**Feature:** Live Classroom realtime session state
**Route:** /classes/05c8e88b-ecf4-42a0-b21d-4d23169385c0/live/38051efb-62d0-46c6-abb8-dcd064778515

### Steps to reproduce

1. Open the session as Student25 while it is scheduled but not started.
2. Leave the tab open.
3. Start the session as Teacher.
4. Observe the original student tab without reloading.

### Expected

The student receives the state transition and can join or is prompted to join.

### Actual

The original tab remained “Phiên chưa bắt đầu” with zero participants. A fresh reload then joined the active session successfully.

### Reproducibility

Reproduced in the audit session; refresh is a workaround.

### Evidence

Student pre-start tab stayed unchanged after Teacher started; new student tab showed “Đã kết nối” and two participants after reload.

### Suspected area

SignalR live-session status propagation or pre-start subscription lifecycle.

## BUG-RLMS-010 — Media-unavailable state has no clear recovery message

**Severity:** P1
**Status:** Open
**Role:** Teacher/Student
**Feature:** Camera, microphone and screen sharing
**Route:** Live Classroom media controls

### Steps to reproduce

1. Start and join a live session in the local browser.
2. Toggle camera/microphone or screen sharing.
3. Observe the stage and controls when no usable local media device/SFU is available.

### Expected

The control remains understandable and the UI explains permission/device/media limitations with a recovery action; session/chat/attendance remain usable.

### Actual

Camera interaction produced a black stage/disabled media controls and a minimal “Camera đang bật” state; screen-share/video output could not be verified. No actionable device/SFU explanation was shown.

### Reproducibility

Reproduced in this browser environment; product behavior cannot be accepted as a media PASS without a usable device/media path.

### Evidence

Session start/join/end worked and both roles remained connected, but camera/screen media output was unavailable.

### Suspected area

Media boundary integration and frontend error-state handling. The environment limitation is why this test is BLOCKED rather than treating the whole Live Classroom as FAIL.

## BUG-RLMS-011 — Teacher feature pages still use dropdown class selectors

**Severity:** P2
**Status:** Open
**Role:** Teacher
**Feature:** Class-selection UX consistency
**Route:** /activities, /grading, /reports, /progress, /attendance, /gradebook

### Steps to reproduce

1. Open each teacher feature page that requires a LearningClass.
2. Inspect the first class-selection control.

### Expected

Search/filter plus visible LearningClass cards using the reusable class-first pattern.

### Actual

Pages use dropdowns such as “Tất cả lớp học” or “Chọn lớp học”. With one class the flow works, but it does not satisfy the required See → Recognize → Click pattern and scales poorly to many classes.

### Reproducibility

Always on the listed pages.

### Evidence

Accessibility trees showed select controls rather than class-card search on all listed routes.

### Suspected area

Frontend shared class-selector component adoption.

## Stable Features

- Admin authentication, organization view/update, user creation and user search.
- Teacher dashboard, nested sidebar groups, class overview, roster and teacher assignment.
- Course draft creation and curriculum module/lesson creation with ordering.
- Student class workspace, class tabs, curriculum card, curriculum workspace and lesson progress.
- Published lesson visibility and class resources.
- Question bank creation, version management and immutability of a published version.
- Assignment creation validation requiring a class and questions before publishing.
- Student objective activity start, autosave, navigation, confirmation and unanswered-question validation.
- Teacher realtime class overview with lightweight progress/status metadata.
- Canonical read-only student observer workspace, Follow/Inspect/Follow Again, A → B switching and snapshot reload.
- Help request lifecycle Requested → Acknowledged → Resolved.
- Teacher rubric, subjective grading and published essay result.
- Teacher/student gradebook publication scope.
- Reporting summary for authorized roles and student own-vs-other permission enforcement.
- Class announcements, notification read state, empty states, not-found/permission feedback and role-separated direct routes.
- Live session start, join, participant presence, chat persistence/display, teacher end and automatic return/disconnect behavior. After end, API status was ended and both roles were rejected from joining again.

## Features Requiring Work

### P0

- None observed.

### P1

- BUG-RLMS-003: reconcile progress summary with lesson records before relying on progress/reporting.
- BUG-RLMS-005: restore a student essay workspace route and keep teacher actions out of the student flow.
- BUG-RLMS-009: push scheduled-to-start live state to already-open student tabs.
- BUG-RLMS-010: provide a clear media/device/SFU unavailable state and retest with real media infrastructure.

### P2

- BUG-RLMS-001: invalidate or revalidate opened tabs on account/tenant change.
- BUG-RLMS-002: normalize account status and action labels.
- BUG-RLMS-004: correct attendance session-time and late-minute calculations/data.
- BUG-RLMS-006: deduplicate/categorize schedule occurrences and canceled history.
- BUG-RLMS-007: deduplicate and localize notifications.
- BUG-RLMS-008: distinguish ended from not-started live sessions in UI/API errors.
- BUG-RLMS-011: adopt reusable visual class-card search on teacher feature pages.

### P3

- No standalone P3 defect was promoted to the bug list. Minor copy/density concerns are recorded under UX Findings.

## Blocked Tests

1. Tenant A → Tenant B isolation matrix: no second usable tenant was available in local data. The invalid old-class probe was still run and returned 404. Retest with a real second tenant, user, class, file and realtime subscription.
2. Camera/microphone/screen-share output: the local browser did not provide a usable media device/SFU path. Business session, presence, chat and end behavior were tested; media must be retested with the supported browser/device/media setup.
3. Fault-injection failure isolation (chat/notification/analytics outages): no safe runtime fault switch was exposed, so intentionally taking those dependencies down was not performed. Do not infer PASS from the absence of a fault test.

## UX Findings

- Class selection is technically functional but inconsistent with the required class-first pattern. A shared card/search selector is needed on activities, grading, reports, progress, attendance and gradebook.
- Opened application tabs are useful, but stale tabs can remain after identity/tenant changes and create confusing dead pages.
- Progress uses multiple headline values that can disagree, which is more confusing than a missing progress value.
- Schedule and notification views expose canceled/duplicate records as if they were parallel current events.
- Admin user status values are not consistently human-readable.
- Live ended/not-started states and media-unavailable states need precise, actionable wording.
- Dense progress/attendance/gradebook tables and large student lists need an additional narrow-screen pass. No mobile acceptance was claimed in this desktop audit.
- Student and teacher workspace headers otherwise kept class context and opened-tab navigation visible without exposing raw exceptions.

## Recommended Retest Order

1. Re-run any P0/security checks if a second tenant is provisioned.
2. Fix and retest P1 progress consistency, student essay routing, live start propagation and media error handling.
3. Re-run the full Student journey: class → content → curriculum → lesson → activity → essay → gradebook.
4. Re-run the full Teacher journey: class/course/curriculum → assignment → realtime overview/observer → grading → attendance/reporting.
5. Repeat realtime A → B switch, stale-event protection, reconnect and live end/disconnect.
6. Repeat assessment, essay validation/topic/report, notification/announcement and attendance checks.
7. Apply the reusable class-card selector and retest every teacher feature page.
8. Run responsive/layout and performance smoke checks at supported viewport sizes.
9. Re-run invalid/deep-link/empty/error and role-permission checks.
10. Run a second complete E2E audit and compare this report against the new result before pilot approval.

## Final User Journey Verdict

### Admin

USABLE WITH ISSUES

Admin can log in, view organization data, create/search users and inspect roles. Inconsistent status labels and the lack of a second tenant prevent a clean production-style acceptance.

### Teacher

USABLE WITH ISSUES

Core teaching, class, assessment, grading, reporting, observation and live-session business flows work. Progress inconsistency, attendance data, stale tabs, class-selector UX and live/media edge states require work.

### Student

NOT READY

Class content, curriculum, lessons, objective activities, progress updates, gradebook scope and notifications are usable. The student essay link opens teacher management UI, progress headlines are contradictory, and a preloaded live tab needs refresh after start.

## Release Verdict

NOT READY FOR PILOT
