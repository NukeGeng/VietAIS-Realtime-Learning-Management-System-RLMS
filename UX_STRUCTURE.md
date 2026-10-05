# UX_STRUCTURE.md

# Thiết kế UX/UI tổng thể — RLMS

## 1. Mục tiêu

RLMS = **Realtime Learning Management System**.

Subdomain dự kiến:

```text
rlms.vietais.vn
```

UX của RLMS không được thiết kế như một dashboard dùng chung rồi ẩn/hiện menu theo role.

Ba nhóm người dùng có mục tiêu khác nhau:

```text
RLMS
├── Student Workspace
├── Teacher Workspace
└── Admin Workspace
```

Nguyên tắc:

- Student ưu tiên: học gì, học lúc nào, còn bài gì, kết quả ra sao.
- Teacher ưu tiên: dạy lớp nào, lớp đang làm gì, ai cần hỗ trợ, bài nào cần chấm.
- Admin ưu tiên: người dùng, tổ chức, phân quyền, cấu hình và tình trạng vận hành.
- Live Classroom có layout riêng, không ép vào layout dashboard thông thường.
- Realtime Overview và Student Observation là hai trải nghiệm khác nhau.
- UI không được tải dữ liệu chỉ vì backend có sẵn nếu người dùng chưa sử dụng dữ liệu đó.

---

# 2. Điều hướng cấp cao

## Student

```text
Trang chủ
Khóa học / Lớp học
Lịch học
Bài tập
Bài thi
Tiểu luận
Điểm số
Tiến độ
Thông báo
```

## Teacher

```text
Tổng quan
Lớp giảng dạy
Khóa học
Lịch giảng
Bài tập
Bài thi
Tiểu luận
Chấm bài
Sổ điểm
Báo cáo
Thông báo
```

## Admin

```text
Tổng quan
Tổ chức
Người dùng
Vai trò & Quyền
Khóa học
Lớp học
Lịch / Hoạt động hệ thống
Báo cáo
Cấu hình
```

Admin không mặc định được đưa vào Teacher Live Workspace chỉ vì có role Admin.
Quyền xem dữ liệu học tập chi tiết phải dựa trên permission/use case rõ ràng.

---

# 3. Student Workspace

## 3.1. Student Dashboard

Mục tiêu:
> Mở RLMS lên là sinh viên biết ngay hôm nay cần làm gì.

Bố cục:

```text
┌─────────────────────────────────────────────────────┐
│ RLMS                                   🔔  Avatar  │
├─────────────┬───────────────────────────────────────┤
│ Trang chủ   │ Xin chào                              │
│ Khóa học    │                                       │
│ Lịch học    │ BUỔI HỌC TIẾP THEO                    │
│ Bài tập     │ C# Backend · 09:00       [Vào lớp]   │
│ Bài thi     │                                       │
│ Tiểu luận   │ VIỆC CẦN LÀM                          │
│ Điểm số     │ Assignment 05         còn 2 ngày     │
│ Tiến độ     │ Essay Report          còn 5 ngày     │
│             │ Quiz 03               hôm nay        │
│             │                                       │
│             │ TIẾN ĐỘ                               │
│             │ C# Backend             72%            │
│             │ Database               55%            │
│             │                                       │
│             │ KẾT QUẢ GẦN ĐÂY                       │
│             │ Quiz 02                8.5/10         │
└─────────────┴───────────────────────────────────────┘
```

Ưu tiên hiển thị:
1. buổi học sắp tới;
2. deadline gần nhất;
3. activity chưa hoàn thành;
4. kết quả mới;
5. tiến độ khóa học.

Không đưa analytics phức tạp lên dashboard sinh viên nếu không giúp họ ra quyết định.

---

## 3.2. My Courses / Classes

Mỗi card nên thể hiện:

```text
Tên môn / khóa học
Lớp
Giảng viên
Tiến độ
Buổi học tiếp theo
Activity đang chờ
```

Click vào lớp:

```text
LearningClass Detail
├── Tổng quan
├── Nội dung khóa học
├── Tài nguyên
├── Tiểu luận
└── Live Classroom
```

Student Course Content dùng route `/classes/:classId/content` trong context lớp đã
chọn. Màn hình hiển thị Course title, tiến độ tổng, rồi các CourseModule đã publish
dưới dạng accordion. Mỗi module chỉ tải lesson summary (title, order, status);
lesson detail và tài nguyên lesson được tải khi sinh viên mở bài. Trạng thái tối
thiểu là Chưa bắt đầu, Đang học và Đã hoàn thành. Nút `Về lớp học` quay lại
`/classes/:classId`; route `/courses/:courseId` vẫn là teacher management route.

---

## 3.3. Student Calendar

Hiển thị:
- lịch học;
- lịch thi;
- deadline;
- lịch hoạt động;
- thay đổi/hủy lịch.

Cho phép:
- ngày;
- tuần;
- tháng nếu thật sự cần.

Mặc định ưu tiên agenda/tuần vì phù hợp hành động học tập hơn calendar trang trí.

---

## 3.4. Assignment Workspace

Sinh viên làm activity trong một workspace thống nhất.

```text
┌───────────────────────────────────────────────────────┐
│ Assignment 05                        07 / 10 câu      │
├────────────┬──────────────────────────────────────────┤
│ Q1 ✓       │ Câu 7                                   │
│ Q2 ✓       │                                         │
│ Q3 ✓       │ Nội dung câu hỏi                        │
│ Q4 ✓       │                                         │
│ Q5 ✓       │ ○ A                                     │
│ Q6 ✓       │ ● B                                     │
│ Q7 ●       │ ○ C                                     │
│ Q8         │ ○ D                                     │
│ Q9         │                                         │
│ Q10        │                       [Tiếp tục]        │
└────────────┴──────────────────────────────────────────┘
```

Phải có:
- autosave hợp lý;
- trạng thái saved/saving/error rõ;
- current question;
- progress;
- deadline/time limit nếu có;
- submit confirmation.

Không spam network theo từng keystroke ở câu tự luận.

---

# 4. Student Live Classroom

Live Classroom có layout riêng.

## 4.1. Khi chưa có activity

```text
┌────────────────────────────────────────────────────────┐
│ C# Backend · Buổi 08                        19:32      │
├────────────────────────────────────────────────────────┤
│                                                        │
│                    TEACHER / MEDIA                     │
│                                                        │
├─────────────────────────────┬──────────────────────────┤
│ Participants                │ Chat                     │
└─────────────────────────────┴──────────────────────────┘
```

## 4.2. Khi teacher publish activity

Activity phải xuất hiện ngay trong cùng workspace.

```text
┌──────────────────────────────────────────────────────────┐
│ C# Backend · Buổi 08                          19:45     │
├──────────────────────────────┬───────────────────────────┤
│                              │ Assignment 03             │
│        TEACHER VIDEO         │                           │
│                              │ Câu 7 / 10                │
│                              │ ○ A                       │
│                              │ ● B                       │
│                              │ ○ C                       │
│                              │ ○ D                       │
├──────────────────────────────┼───────────────────────────┤
│ Participants / Chat          │ [Cần hỗ trợ]             │
└──────────────────────────────┴───────────────────────────┘
```

Student không phải:
- mở Meet tab khác;
- mở LMS tab khác;
- tự tìm lại bài vừa được giao.

---

# 5. Teacher Workspace

## 5.1. Teacher Dashboard

Mục tiêu:
> Teacher biết hôm nay dạy gì, bài nào chưa chấm và lớp nào cần chú ý.

```text
┌─────────────────────────────────────────────────────┐
│ RLMS Teacher                              🔔 Avatar │
├──────────────┬──────────────────────────────────────┤
│ Tổng quan    │ LỊCH HÔM NAY                         │
│ Lớp học      │ 09:00 SE01 C#          [Mở lớp]     │
│ Khóa học     │ 13:00 SE02 Database                  │
│ Lịch giảng   │                                      │
│ Bài tập      │ CẦN CHẤM                             │
│ Bài thi      │ Assignment 04      18 submissions   │
│ Tiểu luận    │ Essay Report        6 submissions   │
│ Chấm bài     │                                      │
│ Sổ điểm      │ HOẠT ĐỘNG GẦN ĐÂY                    │
│ Báo cáo      │ ...                                  │
└──────────────┴──────────────────────────────────────┘
```

---

# 6. Teacher Live Workspace — Core UX

Đây là màn hình quan trọng nhất của RLMS.

## 6.1. Overview Mode

```text
┌───────────────────────────────────────────────────────────────┐
│ SE01 · Assignment 03                              38 Online  │
├──────────────────────┬────────────────────────────────────────┤
│ LIVE CLASSROOM       │ CLASS PROGRESS                         │
│                      │                                        │
│ Teacher / Media      │ Hào    Q7/10  70%  Working            │
│                      │ Minh   Q4/10  40%  Working            │
│                      │ Lan    Q6/10  60%  Idle 2m            │
│                      │ Nam   Q10/10 100%  Completed          │
│                      │                                        │
├──────────────────────┼────────────────────────────────────────┤
│ Chat / Participants  │ Help Queue: 3                          │
└──────────────────────┴────────────────────────────────────────┘
```

Overview chỉ hiển thị metadata:
- student;
- current question;
- progress;
- status;
- last activity;
- help.

**Không chứa full answer.**

## 6.2. Participants, attendance và moderation

Teacher mở bảng **Người tham gia** để xem danh sách người đang online theo tên.
Bảng **Điểm danh** hiển thị ngưỡng đi trễ của phiên, tổng thời gian học, số lần
vào lại và lịch sử từng đoạn vào/rời. Với một lần reconnect, mỗi đoạn giữ giờ
vào, giờ rời, thời lượng học và khoảng gián đoạn trước lần vào tiếp theo.

Trong đúng phiên live hiện tại, teacher có thể:

- tắt/mở mic của một student;
- đưa student ra khỏi phiên (kick);
- chặn student vào lại phiên (block/unblock).

Các điều khiển này là state theo `LiveSession`, không thay đổi quyền tham gia
hoặc mic của student ở những phiên khác.

Teacher có thêm trang **Điểm danh** trong nhóm Theo dõi lớp học. Bộ lọc đi theo thứ tự
lớp học → ngày học → buổi học cụ thể trong ngày, để một lớp có nhiều buổi
trong học kỳ vẫn được đối chiếu riêng. Bảng hiển thị mã học viên, trạng thái, thời điểm vào lần
đầu, số phút đi trễ sau ngưỡng, tổng thời gian học, số lần reconnect, thời điểm
rời cuối và từng đoạn vào/rời. Bảng có thể xuất thành file Excel dạng hàng/cột
để lưu trữ hoặc xử lý tiếp.
Tên ngày và buổi dùng nhãn dễ đọc theo ngữ cảnh, chẳng hạn
`Lớp mẫu — Thứ 4 · 09/09/2026 · Buổi sáng`; định danh UUID chỉ dùng trong
liên kết và lời gọi API, không xuất hiện trong nhãn giao diện.
Ngày và khung giờ của buổi được lấy từ `ClassSchedule.startsAt/endsAt` (lịch
giảng/lịch học). Thời điểm live thực tế chỉ dùng cho trạng thái phiên và lịch
sử điểm danh, nên việc vào sớm, vào trễ hoặc kết thúc phiên không làm đổi ngày
của buổi học trên lịch.
Danh sách buổi gồm cả lịch chưa tạo phiên live; khi chọn, hiển thị trạng thái
chưa có phiên và không tải/xuất điểm danh. Phiên không liên kết lịch được giữ
riêng, không tự ghép vào một buổi sáng/chiều chỉ vì cùng lớp.

## 6.2.1. Media grid và quyền trình bày

Vùng media vừa viewport, tự chọn số hàng/cột và kích thước ô theo số người và
kích thước cửa sổ. Desktop hiển thị tối đa 12 người mỗi trang, mobile tối đa 6.
Hàng cuối được căn giữa. Các ô chuyển vị trí/kích thước mềm khi người vào/rời;
tôn trọng prefers-reduced-motion. Cụm điều khiển chỉ xuất hiện sau khi phiên đã
bắt đầu và người dùng join thành công; khi phiên còn scheduled/ended thì không
render cụm này. Khi hiển thị, cụm điều khiển cố định phía dưới, có vùng chừa
riêng, trạng thái bật nền trắng/chữ đen. Chat/participants nổi trên controls.

Khi trình bày, màn hình dùng khoảng 73% chiều rộng; camera/avatar người trình bày
được ghim bên phải ở mọi trang. Khung trình bày bám tỷ lệ video; nếu còn đủ chiều cao bên dưới thì xếp các ô người dùng vào vùng đó để giảm khoảng đen. Dải chứa tối đa 6 người (mobile 3); màn
hình dọc chuyển dải xuống dưới. Video trình bày giữ đủ nội dung (contain), nút
fullscreen chỉ phóng video. Âm thanh không phụ thuộc trang ô đang xem.
Một phiên chỉ có một người trình bày tại một thời điểm. Student phải chờ khi đang có presenter. Teacher có thể tiếp quản; trước khi
thay presenter là student, hệ thống yêu cầu xác nhận và gửi sự kiện dừng trình bày
cho presenter cũ.
Nút `Về lớp học` chỉ rời live view trên thiết bị hiện tại và đưa người dùng về
trang lớp; nó không đổi trạng thái phiên. Teacher dùng nút `Kết thúc phiên` khi
muốn đóng phiên và đưa mọi client đang kết nối về trang lớp.

---

## 6.3. Question Heatmap

Mục tiêu:
> Teacher nhìn một phát biết cả lớp đang mắc ở đâu.

```text
          Q1 Q2 Q3 Q4 Q5 Q6 Q7 Q8 Q9 Q10
Hào       ✓  ✓  ✓  ✓  ✓  ✓  ●
Minh      ✓  ✓  ✓  ●
Lan       ✓  ✓  ✓  ✓  ✓  ●
Nam       ✓  ✓  ✓  ✓  ✓  ✓  ✓  ✓  ✓  ✓
```

Ký hiệu có thể dùng:
- completed;
- current;
- not reached;
- idle/stuck.

Heatmap không cần tải nội dung answer.

---

# 7. Teacher Student Observation

Khi teacher click **Mở workspace** ở một sinh viên, app mở route riêng
`/activities/{activityId}/attempts/{attemptId}`. Trang riêng này có đủ không gian
cho bản xem workspace chỉ đọc; trang activity hiện tại chỉ giữ overview và các thao tác
quản lý activity.

Trong trang workspace riêng:

```text
┌──────────────────────────────────────────────────────┐
│ Nguyễn Văn Hào                     Progress 70%     │
├─────────────┬────────────────────────────────────────┤
│ Q1 ✓        │ Câu 7                                 │
│ Q2 ✓        │                                       │
│ Q3 ✓        │ ○ A                                   │
│ Q4 ✓        │ ● B                                   │
│ Q5 ✓        │ ○ C                                   │
│ Q6 ✓        │ ○ D                                   │
│ Q7 ●        │                                       │
│ Q8          │ Last change: 2s ago                   │
│ Q9          │ Status: Working                       │
│ Q10         │                                       │
└─────────────┴────────────────────────────────────────┘
```

Mặc định teacher bật **Following**. Khi student chuyển câu hoặc cập nhật đáp án,
workspace quan sát chuyển theo detail delta. Teacher có thể chọn một câu khác để
vào **Inspecting** mà không làm thay đổi câu hiện tại của student; nút **Theo dõi lại**
đưa workspace về câu student đang mở.

## Realtime rule

Trang quan sát dùng cùng component **StudentAttemptWorkspace** với trang làm bài:
student dùng mode `interactive`, teacher dùng mode `observer`. Observer là chỉ đọc,
giữ nguyên danh sách câu hỏi, câu hiện tại, lựa chọn và tiến độ, đồng thời cập nhật
từ detail delta để teacher quan sát trực tiếp student đang thao tác mà không thể sửa
câu trả lời.

```text
Open Student A
    ↓
Load StudentAttemptDetail snapshot A
    ↓
Subscribe A detail channel
```

Switch:

```text
A -> B
    ↓
Unsubscribe A
    ↓
Load B snapshot
    ↓
Subscribe B
```

Nếu UI đóng detail:
- unsubscribe detail channel;
- tiếp tục overview channel.

Không giữ subscription cũ chỉ để “phòng khi teacher quay lại”.

---

# 8. Teacher Grading Workspace

## Objective

Hiển thị:
- answer;
- đáp án;
- auto-grading result;
- rule rõ ràng.

Teacher có thể review nếu business rule cho phép.

## Subjective / Essay

```text
┌─────────────────────────────────────────────────────────┐
│ Submission · Nguyễn Văn A                              │
├────────────────────────────────┬────────────────────────┤
│ Student Answer / Report        │ RUBRIC                 │
│                                │                        │
│ ...                            │ Tiêu chí 1   [   /3 ] │
│                                │ Tiêu chí 2   [   /3 ] │
│                                │ Tiêu chí 3   [   /2 ] │
│                                │ Trình bày    [   /2 ] │
├────────────────────────────────┴────────────────────────┤
│ Feedback                                               │
│                                                        │
│ Final Score: [ Teacher nhập/xác nhận ]                 │
└─────────────────────────────────────────────────────────┘
```

Trong GOAL hiện tại:
- không có AI panel;
- không có Suggested Score;
- không có AI score.

---

# 9. Teacher Essay Workspace

## Class-first entry

Các tính năng teacher cần một `LearningClass` (gồm Tiểu luận, Chấm bài,
Sổ điểm, Bài tập/Bài thi và Báo cáo) bắt đầu bằng một bộ chọn lớp dùng chung:

```text
Tìm lớp học [Search classes by name or code]

┌──────────────┐  ┌──────────────┐
│ SE01         │  │ SE02         │
│ C# Backend   │  │ Database     │
│ 42 sinh viên │  │ 35 sinh viên │
└──────────────┘  └──────────────┘
```

Tìm kiếm lọc trực tiếp các thẻ lớp. Teacher nhấn vào một thẻ để mở workspace
theo lớp; không dùng dropdown làm điểm vào chính và không hiển thị hành động
tạo trước khi lớp được chọn. Workspace phải hiển thị rõ lớp hiện tại cùng thao
tác `Đổi lớp`/`Quay lại danh sách lớp`. Tên lớp không lặp lại trong từng thẻ
bài khi đã có trong header workspace.

## Topic management

Danh sách nhóm được chia trang, tối đa 5 nhóm mỗi trang. Toàn bộ hàng nhóm
có thể được nhấn để mở chi tiết nhóm; khi rê chuột hoặc đưa focus bàn phím vào
hàng, hiển thị tóm tắt thành viên và trạng thái đề tài. Trạng thái `Mở nhóm` /
`Đang mở` luôn nằm ở mép phải cùng một vị trí. Mở một nhóm không làm thay đổi
kích thước cột danh sách; phần chi tiết chỉ tải cho nhóm đang được chọn.

Teacher nhìn:

```text
Group 01
Members: ...
Proposed Topic: ...
Status: Pending

[Approve] [Request Change] [Reject]
```

Sau approve:

```text
Canonical Topic
Status: Locked
```

## Report validation

Khi student upload:
- file valid?
- text extractable?
- content threshold?
- required section?
- topic match?

Teacher/student phải thấy lỗi cụ thể:

```text
Không thể nộp:
- Tên đề tài trong báo cáo không khớp đề tài đã duyệt.
- Báo cáo chỉ có 214 từ, yêu cầu tối thiểu 3.000 từ.
```

Không trả lỗi chung chung kiểu `Invalid report`.

---

# 10. Gradebook UX

Teacher:

```text
             A1   Quiz1   Midterm   Essay   Final
Hào          8.0   9.0      7.5      8.5     ...
Minh         7.0   8.5      8.0      7.5     ...
```

Hỗ trợ:
- filter;
- grade item;
- weight;
- publish/unpublished state;
- audit/adjustment history khi cần.

Student chỉ thấy:
- điểm của chính mình;
- điểm đã được công bố;
- feedback được phép xem.

---

# 11. Admin Workspace

Admin UX tập trung hệ thống, không tập trung chấm bài.

Dashboard:

```text
Organizations         8
Users               4,500
Teachers              180
Students            4,200
Active Classes         23
Active Sessions        11
```

Các màn hình chính:

```text
Organizations
Users
Roles & Permissions
Courses
Classes
System Reports
Configuration
```

Admin không mặc định xem:
- answer realtime của từng student;
- bài tự luận chi tiết;
- teacher grading screen;

trừ khi permission/business use case được định nghĩa rõ.

---

# 12. Permission UX

UI có thể ẩn/disable action theo permission nhưng backend vẫn phải enforce.

Không dùng:

```text
if role == "Teacher"
```

để quyết định business action nếu rule thật là permission.

Ví dụ:
- `class.session.start`
- `activity.publish`
- `submission.grade`
- `gradebook.manage`
- `essay.topic.approve`

Tên permission cuối cùng theo convention codebase, nhưng semantics phải rõ.

---

# 13. Loading & Error UX

## Realtime
Phải phân biệt:
- connecting;
- connected;
- reconnecting;
- temporarily unavailable.

Không giả vờ dữ liệu vẫn realtime khi connection đã mất.

## Autosave
Hiển thị:
- Saving...
- Saved
- Save failed / Retry

## Student Observation
Khi switch nhanh A -> B:
- cancel/ignore stale response A;
- không để payload A ghi đè màn hình B.

## Failure Isolation UX
Ví dụ chat down:

```text
Chat tạm thời không khả dụng.
```

Nhưng:
- media vẫn chạy;
- activity vẫn làm được;
- answer vẫn save.

---

# 14. Responsive Priority

## Student
Phải sử dụng tốt trên:
- desktop;
- laptop;
- tablet;
- mobile ở các flow xem lịch, xem bài, notification.

Làm bài phức tạp/live classroom có thể ưu tiên tablet/desktop nhưng không được cố ý phá mobile nếu không có reason.

## Teacher
Teacher Live Workspace ưu tiên desktop/laptop vì cần:
- media;
- overview;
- student detail;
- controls.

Không ép toàn bộ workspace phức tạp vào mobile layout chỉ để “responsive đủ checklist”.

## Admin
Desktop/laptop-first.

---

# 15. Design Principles

1. Role workspace khác nhau thật sự.
2. Core action phải nổi bật hơn analytics.
3. Không load dữ liệu chưa được hiển thị.
4. Không dùng modal cho mọi workflow phức tạp.
5. Realtime state phải hiển thị rõ trạng thái kết nối.
6. Teacher phải chuyển Overview <-> Detail nhanh.
7. Student luôn biết answer đã được lưu hay chưa.
8. Deadline/time limit phải rõ.
9. Grade publication phải rõ unpublished/published.
10. Essay validation phải giải thích chính xác lý do fail.
11. Không thêm AI UI trong GOAL hiện tại.
12. Không thiết kế UI dựa trực tiếp trên database schema.

---

# 16. Frontend Mapping

Theo `PROJECT_STRUCTURE.md`:

```text
features/
├── auth/
├── dashboard/
├── courses/
├── classrooms/
├── assignments/
├── exams/
├── essays/
├── gradebook/
├── calendar/
└── reports/
```

Không mirror backend modules 1:1.

Ví dụ:
- `StudentActivity` backend thuộc Classroom;
- frontend vẫn hiển thị nó trong `classrooms/` hoặc `assignments/` tùy screen.

UX ownership quyết định frontend folder.

---

# 17. UI Read Models

UI không dựng màn hình bằng cách ghép hàng chục aggregate request nếu một read model hợp lý hơn.

Các read model quan trọng:

```text
StudentDashboardView
TeacherDashboardView
AdminDashboardView
StudentClassView
TeacherClassView
ClassOverviewView
StudentAttemptDetailView
StudentCurriculumContentView
GradebookView
StudentLearningProgressView
EssayGroupStatusView
EssaySubmissionValidationView
```

Không dùng Event Stream trực tiếp để render UI.

---

# 17.1. Student LearningClass Content

Nội dung khóa học là một tab bên trong workspace `LearningClass`, không tự mở
module/lesson workspace. Tab này hiển thị các curriculum được gán dưới dạng
thẻ chọn:

```text
/classes/:classId/content
  → Curriculum cards
  → /classes/:classId/curriculums/:curriculumId
  → Module → Lesson
```

Chỉ route curriculum mới được mở application tab riêng. Tiến độ được tính
theo Student + LearningClass + Curriculum.

---

# 18. UX Golden Paths

## Student

```text
Login
→ Dashboard
→ Next Class
→ Join LiveSession
→ Teacher publishes Activity
→ Student làm bài
→ Autosave
→ Submit
→ Result published
→ Student xem Grade/Feedback
```

## Teacher

```text
Login
→ Teacher Dashboard
→ Open Today's Class
→ Start LiveSession
→ Publish Activity
→ Watch Class Overview
→ Mở workspace quan sát sinh viên khi cần
→ Help/Intervene
→ End Session
→ Grade Submissions
→ Publish Results
```

## Admin

```text
Login
→ Admin Dashboard
→ Manage Organization/User/Permission
→ Manage system-level academic data/config
→ Review reports
```

---

# 19. Quy tắc cuối

> UX của RLMS phải thể hiện đúng điểm khác biệt của sản phẩm: giáo viên hiểu trạng thái học tập của cả lớp theo thời gian thực nhưng chỉ tải chi tiết khi thực sự cần.

Không được biến Teacher Workspace thành:
- một bảng CRUD;
- một trang analytics chỉ refresh;
- hoặc một màn hình tải toàn bộ answer của cả lớp.
