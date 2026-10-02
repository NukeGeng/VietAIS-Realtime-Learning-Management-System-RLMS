# DOMAIN_MODEL.md

# Domain Model & Data Model — RLMS

## 1. Mục tiêu

Tài liệu này khóa:
- domain class chính;
- aggregate ownership;
- quan hệ;
- storage strategy;
- event-sourced state;
- read model;
- ephemeral state;
- binary storage.

Nguyên tắc quan trọng:

```text
Domain Class != Database Table bắt buộc
Bounded Context != Physical top-level module
Write Model != Read Model
Persistent Business State != Ephemeral Realtime State
```

RLMS dùng:
- PostgreSQL + Marten;
- Event Store khi domain thực sự cần history;
- Redis cho cache/presence/ephemeral state;
- Object Storage cho binary.

Không tạo database riêng cho từng module trong GOAL hiện tại.

---

# 2. Domain Map tổng thể

```text
Organization
│
├── Users
│
├── Courses
│    └── Curriculum
│         ├── CourseModule
│         └── Lesson
│
└── LearningClass
     └── ClassCurriculumAssignment
     ├── Enrollment
     ├── ClassSchedule
     ├── LiveSession
     └── AssessmentActivity
          ├── ActivityQuestion
          ├── StudentActivity
          └── Submission
               └── GradeEntry
```

Essay:

```text
LearningClass
  └── EssayAssignment
       └── EssayGroup
            ├── EssayGroupMember
            ├── EssayTopic
            └── EssaySubmission
                 └── ReportValidationResult
```

Learning:

```text
Student
 ├── LearningProgress
 └── LearningHistory
```

---

# 3. Module Ownership

| Module | Sở hữu chính |
|---|---|
| Identity | Organization, User, Role, Permission |
| Academics | Course, Curriculum, CourseModule, Lesson, LearningClass, ClassCurriculumAssignment, Enrollment, ClassSchedule |
| Classroom | LiveSession, StudentActivity, AttendanceRecord, HelpRequest |
| Assessment | AssessmentActivity, QuestionBankItem, Submission, Gradebook, Essay* |
| Learning | LearningProgress, LearningResource, learning history |
| Communication | Announcement, ChatMessage, Notification |
| Reporting | Read models / projections phục vụ report |

Không module nào được sửa trực tiếp state nội bộ của module khác.

---

# 4. Identity

## 4.1. Organization

```csharp
Organization
{
    Id
    Code
    Name
    Status
    CreatedAt
}
```

Vai trò:
- tenant/business boundary cấp tổ chức;
- trường/trung tâm/doanh nghiệp sử dụng RLMS.

Các dữ liệu thuộc tổ chức phải có scope phù hợp theo `OrganizationId` khi business yêu cầu.

GOAL hiện tại:
- dùng một PostgreSQL;
- không DB-per-organization.

---

## 4.2. User

```csharp
User
{
    Id
    OrganizationId
    Email
    DisplayName
    Status
    CreatedAt
}
```

Không tạo:
- `TeacherUser`;
- `StudentUser`;
- `AdminUser`;

chỉ để phân biệt role.

Teacher/Student là capability/profile/assignment theo business.

Nếu cần profile riêng:

```text
TeacherProfile
StudentProfile
```

chỉ tạo khi có field/business behavior riêng thật sự.

---

## 4.3. Role / Permission

```text
Role
Permission
RolePermission
UserRole
```

Cách lưu cụ thể có thể là document/collection phù hợp.

Business rule phải kiểm tra permission/capability, không hard-code:

```csharp
if (role == "Teacher")
```

khi điều cần biết thật sự là `CanGradeSubmission`.

---

# 5. Academics

## 5.1. Course

```csharp
Course
{
    Id
    OrganizationId
    Code
    Title
    Description
    Status
    CreatedBy
    CreatedAt
}
```

Course là aggregate/document cấp cao cho lifecycle:
- Draft;
- Published;
- Archived nếu cần.

`Curriculum` là cấu trúc nội dung cụ thể của một Course. Một `LearningClass`
có thể được gán một hoặc nhiều curriculum thông qua `ClassCurriculumAssignment`
(LearningClassId, CurriculumId, Order, Status). Assignment là quan hệ tham chiếu;
không sao chép cây nội dung vào lớp.

Không để Course aggregate chứa toàn bộ:
- classes;
- students;
- submissions;
- grades.

---

## 5.2. Curriculum

```csharp
Curriculum
{
    Id
    CourseId
    Title
    Description
    Status
}
```

Curriculum có lifecycle publish riêng và là context của tiến độ học theo học
sinh + lớp + curriculum.

## 5.3. CourseModule

```csharp
CourseModule
{
    Id
    CourseId
    Title
    Order
    Status
}
```

Có thể là document riêng hoặc entity con tùy kích thước/lifecycle thực tế.

Không bắt buộc nhét toàn bộ course tree vào một JSON document khổng lồ.

---

## 5.4. Lesson

```csharp
Lesson
{
    Id
    CourseId
    CourseModuleId
    Title
    Order
    ContentSummary
    Status
}
```

Lesson chỉ lưu metadata/nội dung business cần thiết.

File/video:
- lưu metadata/reference;
- binary ở Object Storage.

---

# 6. LearningClass

Không dùng domain class tên `Class`.

Dùng:

```csharp
LearningClass
```

để code dễ đọc và tránh xung đột nghĩa với `class` trong C#.

```csharp
LearningClass
{
    Id
    OrganizationId
    CourseId
    Code
    Name
    Status
    StartDate
    EndDate
    CreatedAt
}
```

Teacher assignment không cần nhét toàn bộ User object vào document.

Dùng ID/reference hợp lý.

---

# 7. Enrollment

```csharp
Enrollment
{
    Id
    LearningClassId
    StudentId
    Status
    EnrolledAt
}
```

Invariant chính:
- một student không được có nhiều enrollment active trùng nhau cho cùng class;
- student chỉ join class/session nếu enrollment/permission hợp lệ.

Enrollment thuộc Academics.

---

# 8. ClassSchedule

```csharp
ClassSchedule
{
    Id
    LearningClassId
    TeacherId
    StartsAt
    EndsAt
    Recurrence
    Status
}
```

Có thể có:
- scheduled;
- rescheduled;
- cancelled;
- completed.

Schedule không đồng nghĩa LiveSession.

```text
ClassSchedule
      ↓
đến thời điểm học
      ↓
LiveSession
```

---

# 9. LiveSession

```csharp
LiveSession
{
    Id
    LearningClassId
    ClassScheduleId?
    TeacherId
    Status
    StartedAt
    EndedAt
    LateAfterMinutes
    MediaRoomId
}
```

LiveSession sở hữu business lifecycle:
- Scheduled/Open nếu có;
- Started;
- Ended.

Không nhét danh sách connection/presence realtime lớn vào LiveSession document.

Presence/connection nằm Redis.

---

# 10. AttendanceRecord

```csharp
AttendanceRecord
{
    Id
    LiveSessionId
    StudentId
    FirstJoinedAt?
    LateCutoffAt?
    LastLeftAt?
    TotalDuration
    Status
    AdjustedBy?
    AdjustmentReason?
    CurrentJoinedAt?
    Segments
    ReconnectCount
}
```

Attendance là durable business data.

Presence là ephemeral.

Không dùng Redis làm source of truth cho attendance cuối cùng.

`AttendanceRecord` là một document tenant-scoped cho mỗi cặp
`LiveSessionId + StudentId`. `CurrentJoinedAt` chỉ giữ segment đang mở để handler
đóng segment khi nhận leave hoặc khi phiên kết thúc; `ActiveConnectionIds` giữ
những HTTP/SignalR connection đang tham gia segment. Một disconnect chỉ đóng
segment khi connection cuối cùng rời đi; nó không thay thế presence. Join/leave
được xử lý bất đồng bộ và phải idempotent.

`Segments` lưu lịch sử các lần vào/rời trong đúng phiên đó (thời điểm vào, thời điểm
rời và thời lượng). `ReconnectCount` là số lần mở lại sau segment đầu tiên. Ngưỡng
đi muộn được chốt trên `LiveSession.LateAfterMinutes` để một lần reconnect không làm
thay đổi luật của phiên khác.

`LateCutoffAt` lưu mốc tính trễ đã dùng khi ghi nhận lần vào đầu tiên. Mốc lấy theo
lịch học (hoặc thời gian bắt đầu thực tế nếu phiên mở sau khi lịch đã kết thúc),
cộng ngưỡng trễ của phiên. Reconnect giữ nguyên mốc; sửa lịch sau này không tính
lại lịch sử. Bản ghi cũ không có mốc vẫn giữ trạng thái durable, không tự backfill
hoặc đoán thời lượng trễ khi không có đủ bằng chứng.

Các API đọc roster, tổng quan hoạt động, chi tiết đang quan sát, sổ điểm và điểm
danh có thêm `StudentDisplayName`. Tên được resolve qua public Identity query
contract theo batch sau kiểm tra quyền và tenant; không lưu bản sao User trong
aggregate/projection, không thêm answer detail vào overview.

# 10.1. RaiseHand

```csharp
RaiseHand
{
    Id
    LiveSessionId
    StudentId
    Status
    RaisedAt
    LoweredAt?
    ClearedBy?
}
```

Đây là document trạng thái nhỏ, có identity xác định theo
`LiveSessionId + StudentId`. Broadcast SignalR chỉ là delta; document là nguồn
durable để teacher tải lại hàng đợi giơ tay.

---

# 10.2. LiveSessionParticipantModeration

```csharp
LiveSessionParticipantModeration
{
    LiveSessionId
    StudentId
    IsBlocked
    IsMuted
    ChangedBy
    UpdatedAt
}
```

Đây là document tenant-scoped theo cặp `LiveSessionId + StudentId`. Kick, block và
mute chỉ tác động tới phiên hiện tại; không ghi trạng thái vào enrollment hoặc tài
khoản để tránh lan sang phiên khác.

---

# 11. StudentActivity — Core Aggregate

Đây là aggregate quan trọng nhất của RLMS.

Ý nghĩa:
> Một phiên một sinh viên thực hiện một activity.

Không chỉ giới hạn trong live classroom; cùng model có thể dùng cho activity được giao ngoài buổi học nếu phù hợp.

State:

```csharp
StudentActivity
{
    Id
    StudentId
    ActivityId
    LearningClassId
    LiveSessionId?
    CurrentQuestionId?
    Status
    AnsweredCount
    StartedAt
    LastActivityAt
    SubmittedAt?
    Version
}
```

## Event Sourcing

`StudentActivity` là candidate chính cho Event Sourcing.

Implementation uses a conjoined Marten event stream with an inline snapshot. The
snapshot is the attempt read model; event history is reserved for durable state
transitions and audit. Current transitions are `StudentActivityStarted`,
`QuestionOpened`, `AnswerSubmitted`, `AnswerChanged`, and `ActivitySubmitted`.

Assessment also owns two tenant-scoped read models for the teacher workspace:
`ClassOverviewView` stores one metadata summary per enrolled student and never
stores answer content in its realtime response; `StudentAttemptDetailView` stores
the latest full answer snapshot for one attempt. Both include a monotonic version.
The async projection updates them from the durable `StudentActivityChanged`
contract, and the API falls back to the inline `StudentActivity` snapshot when a
detail projection is still catching up.

Stream:

```text
student-activity-{attemptId}

StudentActivityStarted
QuestionOpened
AnswerSubmitted
AnswerChanged
StudentBecameIdle
StudentActivityResumed
HelpRequested
ActivitySubmitted
```

Không lưu:
- heartbeat;
- websocket ping;
- mouse move;
- từng keystroke;
- typing indicator.

---

# 12. Answer State

Không nhất thiết tạo `Answer` thành aggregate riêng.

Có thể biểu diễn answer trong:
- StudentActivity events;
- StudentAttemptDetail projection;
- final Submission snapshot.

Ví dụ event:

```text
AnswerSubmitted
{
    AttemptId
    QuestionId
    Value
    OccurredAt
}
```

Với tự luận dài:
- autosave/draft có thể dùng state/delta riêng;
- không event source từng ký tự.

---

# 13. AssessmentActivity

Không tạo inheritance tree lớn:

```text
Assignment : Activity
Quiz : Activity
Exercise : Activity
Exam : Activity
```

mặc định.

Ưu tiên composition.

```csharp
AssessmentActivity
{
    Id
    LearningClassId
    Title
    Kind
    Status
    OpensAt?
    DueAt?
    TimeLimit?
    SubmissionPolicy
    PublishedAt?
}
```

`Kind` có thể gồm:
- Assignment;
- Exercise;
- Quiz.

Exam có policy phức tạp hơn và có thể dùng `ExamPolicy` composition.

Essay có workflow riêng, không ép vào inheritance.

---

# 14. ActivityQuestion

Một activity đã publish không được bị thay đổi ngầm khi Question Bank thay đổi.

Do đó activity phải tham chiếu version/snapshot của question.

```csharp
ActivityQuestion
{
    Id
    ActivityId
    QuestionVersionId
    Order
    Points
}
```

Có thể lưu question snapshot khi publish nếu implementation thấy phù hợp.

The current implementation stores selected `ActivityQuestion` entries as immutable
snapshots inside the activity document. Each entry retains its source
`QuestionVersionId`, prompt, type, options, points, and objective answer for later
grading. Publishing is the boundary after which the question list and snapshots
cannot be changed.

Invariant:
> Sửa Question Bank sau này không làm thay đổi nội dung một bài đã phát hành/làm trước đó.

---

# 15. QuestionBankItem

```csharp
QuestionBankItem
{
    Id
    OrganizationId
    CourseId?
    Topic
    Difficulty?
    CurrentVersion
    Status
}
```

Question version chứa:
- prompt;
- type;
- options;
- correct answer khi objective;
- metadata cần thiết.

Không overwrite historical version đã được activity sử dụng.

---

# 16. ExamPolicy

Thay vì một aggregate hoàn toàn khác ngay từ đầu, có thể reuse Assessment concepts bằng policy:

```csharp
ExamPolicy
{
    OpensAt
    ClosesAt
    TimeLimit
    AttemptLimit
    ShuffleQuestions
    ShuffleOptions
    PassingScore?
    RetakeRule
    ResultReleaseRule
}
```

Nếu sau này Exam phát triển thành domain quá khác biệt, mới xem xét boundary riêng.

Không overengineering trước.

---

# 17. Submission

Final submission là durable snapshot phục vụ:
- grading;
- audit;
- result publication.

```csharp
Submission
{
    Id
    ActivityId
    StudentId
    StudentActivityId
    SubmittedAt
    Status
    Version
}
```

Final answers có thể nằm:
- trong Submission document;
- hoặc read model/reference phù hợp từ attempt;

nhưng grader không được phải replay toàn event stream mỗi lần mở bài.

---

# 18. Grading / Rubric

## Rubric

```csharp
Rubric
{
    Id
    ActivityId
    Criteria[]
}
```

```csharp
RubricCriterion
{
    Id
    Name
    Description
    MaxPoints
    Order
}
```

## GradeEntry

```csharp
GradeEntry
{
    Id
    LearningClassId
    StudentId
    GradeItemId
    SourceSubmissionId?
    Score
    MaxScore
    Feedback?
    Status
    GradedBy
    GradedAt
}
```

Điểm do teacher xác nhận đối với subjective work.

Auto grading chỉ với deterministic objective question.

`GradeAdjustment` là audit document cho mỗi thay đổi điểm đã ghi nhận. Adjustment
giữ `GradeEntryId`, điểm cũ/mới, người điều chỉnh, lý do, thời điểm và phiên bản
điểm. Entry được đưa về Draft sau khi điều chỉnh và phải publish lại trước khi
student có thể thấy kết quả mới.

---

# 19. Gradebook

Không nhất thiết có một aggregate chứa toàn bộ sinh viên × điểm của lớp.

Có thể dùng:

```text
GradeItem
GradeEntry
GradebookView (Projection)
```

## GradeItem

```csharp
GradeItem
{
    Id
    LearningClassId
    SourceType
    SourceId
    Name
    MaxScore
    Weight
    Status
}
```

`GradebookView` phục vụ UI dạng bảng.

Không load một aggregate khổng lồ để hiển thị gradebook.

---

# 20. HelpRequest

HelpRequest là business state, không chỉ notification.

```csharp
HelpRequest
{
    Id
    OrganizationId
    LiveSessionId
    StudentActivityId
    ActivityId
    LearningClassId
    StudentId
    QuestionId?
    Status
    RequestedAt
    AcknowledgedAt?
    ResolvedAt?
    HandledBy?
}
```

`HelpRequest` là document tenant-scoped. Hàng chờ đọc các request chưa Resolved;
`WaitDurationSeconds` được tính từ `RequestedAt` đến `AcknowledgedAt`, hoặc đến
`ResolvedAt`/thời điểm đọc nếu request còn chờ.

Lifecycle:

```text
Requested
→ Acknowledged
→ Resolved
```

Có thể phát event để update Teacher Help Queue.

---

# 21. EssayAssignment

Essay workflow đủ khác để có model riêng.

```csharp
EssayAssignment
{
    Id
    LearningClassId
    Title
    Deadline
    GroupPolicy
    SubmissionPolicy
    ValidationPolicy
    Status
}
```

Không dùng inheritance từ `AssessmentActivity` chỉ để tái sử dụng field.

Có thể dùng composition/shared primitives nếu hợp lý.

---

# 22. EssayGroup

```csharp
EssayGroup
{
    Id
    EssayAssignmentId
    LearningClassId
    Name
    Status
    Members[]
}
```

Member:

```csharp
EssayGroupMember
{
    StudentId
    JoinedAt
}
```

Invariant:
- member phải thuộc class;
- group membership phải phù hợp group policy;
- một student không được nằm trong nhiều group cho cùng essay nếu policy không cho phép.

---

# 23. EssayTopic

```csharp
EssayTopic
{
    Id
    EssayAssignmentId
    EssayGroupId
    ProposedTitle
    CanonicalTitle?
    Status
    SubmittedAt
    ReviewedBy?
    ReviewedAt?
}
```

Lifecycle:

```text
Draft
→ PendingReview
→ Approved
→ Locked
```

hoặc:
- ChangesRequested;
- Rejected.

Invariant quan trọng:
> Sau khi approved/locked, `CanonicalTitle` là tên chuẩn dùng để validate report.

Không sửa âm thầm canonical title.

Đổi đề tài phải qua business flow.

---

# 24. EssaySubmission

```csharp
EssaySubmission
{
    Id
    EssayAssignmentId
    EssayGroupId
    FileAssetId
    Version
    Status
    UploadedAt
    SubmittedAt?
    ValidationResultId?
}
```

Một member có thể upload thay group nếu permission/policy cho phép.

Submission được tính cho group.

---

# 25. ReportValidationPolicy

```csharp
ReportValidationPolicy
{
    AllowedFileTypes
    MinimumPages?
    MinimumWords?
    MinimumCharacters?
    RequiredSections[]
    RequireTopicMatch
}
```

Rule là configuration theo assignment.

Không hard-code:
- 15 pages;
- 3000 words;

cho toàn hệ thống.

---

# 26. ReportValidationResult

```csharp
ReportValidationResult
{
    Id
    EssaySubmissionId
    IsValid
    ExtractedTitle?
    NormalizedTitle?
    PageCount?
    WordCount?
    CharacterCount?
    MissingSections[]
    Errors[]
    ValidatedAt
}
```

Validation:
- file readable;
- content not empty/near-empty;
- title match;
- required section;
- threshold.

MVP:
- không bắt buộc OCR;
- có thể yêu cầu PDF/DOCX có extractable text.

---

# 27. FileAsset

Business DB chỉ lưu metadata:

```csharp
FileAsset
{
    Id
    OrganizationId
    StorageKey
    FileName
    ContentType
    Size
    CreatedBy
    CreatedAt
}
```

Binary nằm ở Object Storage.

Không lưu MP4/PDF/DOCX binary trong Event Store.

---

# 28. LearningProgress

Không nhất thiết là aggregate event-sourced.

```csharp
LearningProgress
{
    StudentId
    CourseId
    LearningClassId
    CompletedLessons
    TotalLessons
    CompletionPercentage
    UpdatedAt
}
```

Có thể là projection từ:
- lesson completion;
- activity completion;
- rules liên quan.

---

# 29. Communication

## Announcement

```csharp
Announcement
{
    Id
    LearningClassId
    AuthorId
    Title
    Content
    PublishedAt
}
```

## ChatMessage

```csharp
ChatMessage
{
    Id
    LiveSessionId
    SenderId
    Body
    SentAt
}
```

Chat có workload riêng.

Durability/retention cụ thể theo product rule.

Chat failure không được chặn:
- media;
- answer;
- submit.

## Notification

```csharp
Notification
{
    Id
    UserId
    Type
    Title
    Body
    ReadAt?
    CreatedAt
}
```

Notification handler không nằm trong critical business transaction nếu không cần.

---

# 30. Read Models / Projections

## ClassOverviewView

```csharp
ClassOverviewView
{
    LiveSessionId
    ActivityId
    TotalStudents
    WorkingCount
    IdleCount
    CompletedCount
    Students[]
}
```

Row:

```csharp
ClassOverviewStudentRow
{
    StudentId
    DisplayName
    CurrentQuestion
    AnsweredCount
    TotalQuestions
    Progress
    Status
    LastActivityAt
    HelpRequested
}
```

**Không chứa answer content.**

---

## StudentAttemptDetailView

```csharp
StudentAttemptDetailView
{
    StudentActivityId
    StudentId
    ActivityId
    CurrentQuestionId
    Status
    Answers
    Version
    LastActivityAt
}
```

Chỉ load khi teacher/student được phép mở detail.

Realtime:
- snapshot;
- delta.

---

## GradebookView

Read model tối ưu cho:

```text
Student × GradeItem
```

Không query/rebuild từng submission khi mở gradebook.

---

## StudentDashboardView

Có thể tổng hợp:
- next session;
- upcoming deadlines;
- latest results;
- course progress.

## TeacherDashboardView

Có thể tổng hợp:
- today's sessions;
- submissions waiting for grading;
- current/next classes.

## AdminDashboardView

Có thể tổng hợp:
- organizations;
- users;
- active classes/sessions;
- high-level operational stats.

---

# 31. Redis / Ephemeral Model

Redis có thể chứa:

```text
LiveSessionSnapshot
Presence
LastSeen
SignalRConnectionMapping
DetailSubscriptionMapping
ShortLivedCounters
TypingIndicator
```

Ví dụ:

```text
presence:{liveSessionId}:{studentId}
session-snapshot:{liveSessionId}
connection:{userId}
```

Đây chỉ là naming illustration, không phải bắt buộc key convention.

Redis không là source of truth cho:
- GradeEntry;
- Submission;
- EssayTopic;
- Enrollment;
- Attendance final state.

---

# 32. LiveSessionSnapshot

Dùng cho Mass Join.

```csharp
LiveSessionSnapshot
{
    LiveSessionId
    LearningClass
    TeacherSummary
    LessonSummary
    CurrentActivitySummary?
    ResourceLinks
    SessionStatus
    Version
}
```

Dữ liệu shared.

Không nhét:
- Student-specific permission;
- StudentActivity;
- private grade;
- per-user answer.

---

# 32.1. StudentCurriculumContentView

`StudentCurriculumContentView` là read model theo `LearningClass`, curriculum và
người học, được dựng on-demand từ nội dung đã publish và
`StudentLessonProgressRecord` của chính sinh viên. Nó chỉ chứa tiêu đề, thứ tự,
trạng thái và số bài hoàn thành; nội dung chi tiết và tài nguyên lesson được tải
qua query riêng sau khi sinh viên mở bài.

```text
StudentCurriculumContentView
    LearningClassId
    CurriculumId
    CourseId
    CurriculumTitle
    CourseTitle
    CompletedLessons
    TotalLessons
    Progress
    Modules[]

StudentCourseModuleView
    ModuleId
    Title
    Order
    CompletedLessons
    TotalLessons
    Lessons[]

StudentLessonSummaryView
    LessonId
    Title
    Order
    Status (InProgress | Completed | null)
```

`StudentLessonDetailView` là query on-demand có thêm `ContentSummary`, module
context, trạng thái tiến độ và metadata tài nguyên đã publish. Đây là các view
phục vụ UI, không phải aggregate mới và không sao chép Course/Module/Lesson.

---

# 33. Persistence Matrix

| Model | Storage chính | Kiểu |
|---|---|---|
| Organization | PostgreSQL/Marten | Document |
| User | PostgreSQL/Marten | Document |
| Course | PostgreSQL/Marten | Document |
| Curriculum/CourseModule/Lesson | PostgreSQL/Marten | Document/entity tùy lifecycle |
| LearningClass | PostgreSQL/Marten | Document |
| Enrollment | PostgreSQL/Marten | Document |
| ClassSchedule | PostgreSQL/Marten | Document |
| LiveSession | PostgreSQL/Marten | Document / event-backed nếu cần |
| AttendanceRecord | PostgreSQL/Marten | Durable document/projection |
| RaiseHand | PostgreSQL/Marten | Durable state document |
| HelpRequest | PostgreSQL/Marten | Durable lifecycle document |
| StudentActivity | Marten Event Store | Event-sourced Aggregate |
| AssessmentActivity | PostgreSQL/Marten | Document |
| QuestionBankItem/Version | PostgreSQL/Marten | Documents |
| Submission | PostgreSQL/Marten | Durable document |
| GradeItem/GradeEntry | PostgreSQL/Marten | Documents |
| GradeAdjustment | PostgreSQL/Marten | Audit document |
| Essay* | PostgreSQL/Marten | Documents + domain events khi cần |
| StudentProgressRecord/StudentLessonProgressRecord | PostgreSQL/Marten | Projection/documents |
| StudentCourseContentView (query response) | PostgreSQL/Marten query over published CourseModule/Lesson + StudentLessonProgressRecord | On-demand student read model |
| LearningResource | PostgreSQL/Marten | Metadata document |
| ClassReportSnapshot/StudentReportSnapshot | PostgreSQL/Marten | Reporting projections |
| ClassOverviewView | PostgreSQL/Marten | Projection |
| StudentAttemptDetailView | PostgreSQL/Marten | Projection |
| Presence | Redis | Ephemeral |
| LiveSessionSnapshot | Redis | Cache |
| Connection Mapping | Redis | Ephemeral |
| Files/Recordings | Object Storage | Binary |
| FileAsset | PostgreSQL/Marten | Metadata |

Không coi matrix này là yêu cầu tạo table thủ công tương ứng 1:1.

---

# 34. Relationship Rules

1. Reference giữa aggregate ưu tiên ID, không nhét object graph lớn.
2. Không eager-load toàn domain graph chỉ để phục vụ một UI.
3. Read model có thể denormalize dữ liệu hiển thị.
4. Write model ưu tiên invariant và ownership.
5. Query model ưu tiên UX/performance.
6. Không share mutable entity instance giữa module.
7. Module khác giao tiếp qua contract/event/query public phù hợp.
8. Một transaction không được kéo nhiều aggregate chỉ vì tiện code nếu business không cần atomicity đó.

---

# 35. Event Naming Rules

Command:
- StartLiveSession
- PublishActivity
- SubmitAnswer
- RequestHelp
- ApproveEssayTopic

Event:
- LiveSessionStarted
- ActivityPublished
- AnswerSubmitted
- HelpRequested
- EssayTopicApproved

Event luôn diễn tả việc **đã xảy ra**.

Không:
- `StartSessionEvent`
- `SubmitAnswerEvent`

---

# 36. Aggregate Candidates

## Chắc chắn / mạnh
- Course
- LearningClass
- StudentActivity
- AssessmentActivity
- Submission
- EssayGroup / EssayTopic tùy implementation

## Có thể là document, không cần aggregate phức tạp
- Enrollment
- ClassSchedule
- Announcement
- Notification
- FileAsset
- LearningProgress

## Không được biến thành aggregate khổng lồ
- toàn bộ Gradebook của một class;
- toàn bộ Course + classes + students + submissions;
- toàn bộ LiveSession + connections + answers.

---

# 37. Database Indexing — Nguyên tắc

Chỉ tạo index theo query thật.

Các lookup có khả năng quan trọng:
- OrganizationId;
- LearningClassId;
- CourseId;
- StudentId;
- ActivityId;
- LiveSessionId;
- status + deadline;
- unique enrollment `(LearningClassId, StudentId)` ở semantics phù hợp.

Không tạo index hàng loạt cho mọi field.

Load test/query profile mới quyết định index sâu hơn.

---

# 38. Multi-Organization Scoping

Nếu RLMS phục vụ nhiều trường/trung tâm:

- organization-owned data phải scope theo OrganizationId;
- user không được query data organization khác;
- permission check phải áp dụng cả HTTP và realtime subscription;
- cache key shared phải chứa scope/session đủ để không leak tenant data.

Không cần DB-per-tenant trong GOAL hiện tại.

---

# 39. Data Safety Rules

- Grade/Submission là durable.
- Student answer đã save không được mất chỉ vì realtime disconnect.
- Realtime delivery không phải source of truth.
- Redis mất cache không được làm mất business state.
- projection có thể rebuild từ source phù hợp.
- file metadata và object storage reference phải nhất quán.
- report validation failure không tạo final submission.

---

# 40. Domain Golden Path

```text
LearningClass
     ↓
ClassSchedule
     ↓
LiveSession
     ↓
AssessmentActivity Published
     ↓
StudentActivity Started
     ↓
QuestionOpened
     ↓
AnswerSubmitted / Changed
     ↓
ClassOverviewView Updated
     ↓
StudentAttemptDetailView Updated
     ↓
ActivitySubmitted
     ↓
Submission
     ↓
Grading
     ↓
GradeEntry
     ↓
GradebookView
     ↓
LearningProgress
```

---

# 41. Quy tắc cuối

> Không thiết kế database trước rồi ép business vào database.

Thứ tự đúng:

```text
Business Flow
    ↓
Invariant / Ownership
    ↓
Aggregate / Document / Event
    ↓
Read Model
    ↓
Storage Mapping
```

Marten/PostgreSQL là công cụ lưu domain, không phải domain.
