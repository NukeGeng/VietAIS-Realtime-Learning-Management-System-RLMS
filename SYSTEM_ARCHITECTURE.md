# SYSTEM_ARCHITECTURE.md

# Thiết kế kiến trúc hệ thống học trực tuyến

## 1. Mục tiêu kiến trúc

Hệ thống là một nền tảng học trực tuyến kết hợp:
- LMS;
- lớp học trực tuyến realtime;
- hoạt động học tập ngay trong buổi học;
- theo dõi tiến độ toàn lớp;
- quan sát chi tiết bài làm của từng sinh viên theo yêu cầu;
- assignment/quiz/exam;
- tiểu luận;
- điểm số, sổ điểm và learning progress.

Điểm khác biệt chính:
> Giảng viên không chỉ biết sinh viên "đang online" mà biết cả lớp đang học/làm tới đâu và có thể mở đúng bài làm của một sinh viên để quan sát realtime.

---

## 2. Kiến trúc tổng thể

```text
                              CLIENTS
                    Student Web / Teacher Web
                                │
                ┌───────────────┴────────────────┐
                │                                │
                ▼                                ▼
        BUSINESS / REALTIME                  MEDIA LAYER
            ASP.NET Core                       WebRTC
                │                                │
        Modular Monolith                    SFU / TURN / STUN
                │                                │
     ┌──────────┼──────────┐                 Video / Audio
     │          │          │                 Screen Share
  Commands   Queries    SignalR
     │          │          │
     └──────────┼──────────┘
                │
           Wolverine
                │
              Marten
                │
           PostgreSQL
                │
       Event Store / Documents
       Inline / Async Projection

      ┌──────────────┬────────────────┐
      │              │                │
    Redis       Object Storage     CDN
 Presence       Files/Recording    Static Delivery
 Cache
```

---

## 3. Business Core: Modular Monolith

Business Core được chia theo module/bounded context, nhưng deploy ban đầu trong một application.

### Các context chính

| Context | Trách nhiệm |
|---|---|
| Tài khoản & Tổ chức | User, Teacher, Student, Organization, Role, Permission |
| Quản lý Khóa học | Course, Module, Lesson, cấu trúc nội dung |
| Quản lý Lớp học | Class, Enrollment, Roster, Teacher Assignment |
| Lịch học & Lịch đào tạo | Schedule, Class Session, Calendar, Reschedule, Cancel |
| Lớp học Trực tuyến | LiveSession, room, participant, join/leave |
| Điểm danh & Chuyên cần | Attendance, late, absent, duration |
| Hoạt động Học tập | Assignment, Quiz, Exercise, Question |
| Quá trình Học tập Realtime | StudentActivity, CurrentQuestion, Progress, Idle, Resume |
| Yêu cầu Hỗ trợ | HelpRequest, acknowledge, resolve |
| Nộp bài & Chấm điểm | Submission, Answer, Rubric, Manual/Auto grading |
| Sổ điểm | Grade, weight, average, final grade |
| Tiến độ Học tập | Course/Lesson progress, completion |
| Giao tiếp & Thông báo | Announcement, chat, notification |
| Học liệu & Đa phương tiện | Resource metadata, attachment, recording metadata |
| Ngân hàng Câu hỏi | Question bank, classification, reuse, version |
| Thi & Kiểm tra | Exam policy, attempt, time, pass rule |
| Đề tài & Tiểu luận | Topic registration, group topic, report validation |
| Báo cáo & Phân tích | Historical analytics và reporting |
| AI hỗ trợ bài tự luận | Coming Soon, ngoài scope triển khai hiện tại |

---

## 4. Core Domain

Core Domain tạo khác biệt tập trung ở:

```text
Live Classroom
      +
Learning Activity
      +
Student Activity
      +
Realtime Teacher Workspace
```

Golden Path:

```text
Giảng viên bắt đầu buổi học
        ↓
Sinh viên tham gia
        ↓
Giảng viên giao activity
        ↓
Sinh viên bắt đầu làm
        ↓
Hệ thống ghi nhận Question/Answer/Progress
        ↓
Teacher Overview cập nhật realtime
        ↓
Giảng viên mở Student A
        ↓
Load Student A Snapshot
        ↓
Subscribe Student A Detail
        ↓
Quan sát bài làm realtime
        ↓
Đổi sang Student B
        ↓
Unsubscribe A / Subscribe B
```

---

## 5. Realtime Architecture

### 5.1. Presence Channel
Dữ liệu:
- Joined;
- Left;
- Online;
- Offline;
- LastSeen.

Có thể lưu ephemeral state tại Redis.

### 5.2. Class Overview Channel
Teacher subscribe theo:
`class:{classId}:activity:{activityId}:overview`

Payload tối thiểu:

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

Không gửi answer detail.

### 5.3. Student Detail Channel
Teacher chỉ subscribe khi mở attempt:

`attempt:{attemptId}:detail`

Khi chuyển sinh viên:
- leave group cũ;
- load snapshot mới;
- join group mới.

### 5.4. Snapshot + Delta

```text
GET Current StudentAttempt Snapshot
        ↓
SnapshotVersion = 37
        ↓
Subscribe detail channel
        ↓
AnswerChanged Version 38
QuestionOpened Version 39
...
```

Nếu mất connection, client có thể reload snapshot hiện tại rồi subscribe lại.

---

## 6. Mass Join Architecture

Khi nhiều sinh viên vào cùng lúc:

```text
                 N STUDENTS
                      │
                 Load Balancer
                      │
                   Join API
                      │
       ┌──────────────┼─────────────┐
       │              │             │
 Authentication   Enrollment     Session Rule
       │              │             │
       └──────────────┼─────────────┘
                      │
                Register Presence
                      │
                      ▼
             LiveSessionSnapshot Cache
                      │
                      ▼
                Return Bootstrap
                      │
            ┌─────────┴─────────┐
            ▼                   ▼
         SignalR               WebRTC
```

### Shared data
Cache/reuse:
- class metadata;
- lesson metadata;
- teacher metadata;
- current activity metadata;
- resource links;
- common session state.

### User-specific data
Không cache dùng chung:
- identity;
- enrollment result;
- permission;
- attendance identity;
- connection state;
- StudentActivity attempt.

### Cache Stampede
Sử dụng single-flight/request coalescing khi một snapshot hết hạn đúng lúc có nhiều request.

---

## 7. Media Architecture

Media không đi qua Business API.

```text
Teacher / Student
       │
       │ WebRTC Upstream
       ▼
      SFU
       │
       ├── Viewer A
       ├── Viewer B
       ├── Viewer C
       └── ...
```

Business API chỉ quản:
- room/session metadata;
- permission;
- token/access;
- recording metadata.

---

## 8. Event-driven Business Flow

Luồng command:

```text
HTTP / Message
      ↓
Command
      ↓
Wolverine Handler
      ↓
Aggregate / Domain
      ↓
Business Rules / Invariants
      ↓
Domain Event
      ↓
Marten Event Stream
      ↓
Marten Event Store
```

Projection:

```text
Event Store
   │
   ├──── Inline Projection
   │          ↓
   │     Current Read Model
   │
   └──── Async Projection
              ↓
         Async Daemon
              ↓
       Report / Analytics
```

Không bắt buộc mọi context phải dùng Event Sourcing. Chỉ dùng nơi event history và state transition có giá trị nghiệp vụ thật.

---

## 9. Storage Design

### PostgreSQL + Marten
Source of truth cho:
- Course/Class/Schedule;
- Event Streams;
- StudentActivity;
- Submission;
- Grade;
- Learning Progress;
- Read Models.

### Redis
Ephemeral / cache:
- LiveSessionSnapshot cache;
- Presence;
- LastSeen;
- realtime connection mapping;
- short-lived counters/state.

### Object Storage
- PDF/DOCX;
- report;
- attachment;
- slide;
- image;
- video lesson;
- recording.

### CDN
Dùng phân phối static/media file phù hợp, tránh đẩy binary qua API.

---

## 10. Failure Isolation

```text
CONTROL PLANE
Session / Permission / Enrollment / Schedule

MEDIA PLANE
Video / Audio / Screen share

CLASS STATE PLANE
Presence / Attendance / Progress Overview

INTERACTION PLANE
Chat / Raise Hand / Help Request

STUDENT DETAIL PLANE
Answers / Draft / Attempt Detail
```

Mục tiêu:
- chat down -> lớp vẫn học;
- notification down -> vẫn nộp bài;
- analytics down -> vẫn lưu answer;
- media có sự cố -> submission không mất;
- detail channel có sự cố -> overview vẫn hoạt động.

---

## 11. Tiểu luận

Flow:

```text
Tạo bài tiểu luận
     ↓
Tạo nhóm
     ↓
Đăng ký đề tài
     ↓
Giảng viên duyệt
     ↓
Khóa tên đề tài
     ↓
Nhóm làm báo cáo
     ↓
Upload file
     ↓
Pre-Submission Validation
     ├── định dạng hợp lệ?
     ├── đọc được?
     ├── không rỗng?
     ├── đủ nội dung tối thiểu?
     ├── đúng tên đề tài?
     └── đủ section bắt buộc?
     ↓
PASS
     ↓
Nộp chính thức
```

Không chỉ kiểm tra file size. Phải kiểm tra text/content density và các rule cấu hình cho bài.

MVP không cần OCR bắt buộc. Có thể yêu cầu PDF/DOCX có text extract được.

---

## 12. AI hỗ trợ bài tự luận — Coming Soon

Không triển khai trong GOAL hiện tại.

Sau này:

```text
Teacher Reference
  ├── Rubric
  ├── Đáp án tham khảo
  ├── Slide
  └── Giáo trình
        │
        ▼
        RAG
        ▲
        │
Student Submission
        │
        ▼
AI Review Findings
  ├── Có căn cứ
  ├── Chưa đầy đủ
  ├── Có thể mâu thuẫn
  ├── Cần teacher review
  └── References
        │
        ▼
      TEACHER
        │
        ▼
Teacher tự chấm và nhập điểm
```

AI không được tạo hoặc đề xuất điểm.

---

## 13. Lộ trình scale

### Giai đoạn đầu
- Modular Monolith;
- PostgreSQL + Marten;
- Wolverine;
- SignalR;
- Redis;
- Object Storage;
- Media Layer riêng.

### Khi có số liệu thật
Có thể tách độc lập:
- Notification Service;
- Realtime Gateway/StudentActivity workload;
- Media infrastructure đã độc lập từ đầu;
- analytics workers.

### Chỉ chuyển business module thành microservice khi có lý do:
- cần scale riêng;
- cần deploy riêng;
- ownership/team riêng;
- failure domain riêng;
- số liệu chứng minh monolith boundary hiện tại không còn phù hợp.

---

## 14. Nguyên tắc kiểm thử tải

Phải test theo workload thực tế, ít nhất gồm:
- Mass Join cùng thời điểm;
- Teacher Overview nhiều sinh viên;
- Teacher chuyển nhanh giữa Student Detail;
- nhiều lớp hoạt động đồng thời;
- chat spike;
- answer burst;
- media concurrency;
- reconnect;
- cache miss đồng thời;
- projection lag.

Không dùng một benchmark tổng quát rồi kết luận toàn hệ thống đủ tải.
