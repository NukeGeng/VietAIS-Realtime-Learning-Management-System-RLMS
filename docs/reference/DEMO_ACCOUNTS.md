# Tài khoản mẫu RLMS

Các tài khoản dưới đây chỉ dùng cho môi trường local/demo. Không dùng lại những thông tin này ở môi trường thật.

## Bộ dữ liệu RLMS-DEMO

Bộ dữ liệu này được tạo và kiểm tra qua các HTTP API nghiệp vụ của ứng dụng
(đăng nhập, ghi danh, tạo nội dung, phát hành, làm bài, chấm điểm, nộp tiểu
luận và điểm danh). Runner có thể chạy lại mà không tạo thêm bản ghi demo:

```bash
RLMS_DEMO_DATA=1 python3 scripts/setup-demo-data.py
```

Runner chỉ chấp nhận host local và chỉ quản lý tổ chức `RLMS-DEMO`.

### Tenant và workspace

- Mã tổ chức: `RLMS-DEMO`
- Tên tổ chức: `RLMS Demo University`
- URL ứng dụng: <http://127.0.0.1:5173>
- Trang đăng nhập: <http://127.0.0.1:5173/login>
- Lớp: `RLMS-WED-DEMO` — `Lớp C# Backend — Thứ 4`
- Class ID: `05c8e88b-ecf4-42a0-b21d-4d23169385c0`
- Course: `CSHARP-BE` — `C# Backend Development`
- Course ID: `866b93e7-8dbb-4cec-acde-67f05405e60e`
- Curriculum: `Nền tảng C# Backend`
- Curriculum ID: `608ed1ce-6399-4793-84ab-8f6102f38f02`

Đường dẫn kiểm tra nhanh:

- Teacher class overview: <http://127.0.0.1:5173/classes/05c8e88b-ecf4-42a0-b21d-4d23169385c0/overview>
- Student class content: <http://127.0.0.1:5173/classes/05c8e88b-ecf4-42a0-b21d-4d23169385c0/content>
- Student curriculum workspace: <http://127.0.0.1:5173/classes/05c8e88b-ecf4-42a0-b21d-4d23169385c0/curriculums/608ed1ce-6399-4793-84ab-8f6102f38f02>
- Teacher course management: <http://127.0.0.1:5173/courses/866b93e7-8dbb-4cec-acde-67f05405e60e>
- Teacher essay workspace: <http://127.0.0.1:5173/essays?classId=05c8e88b-ecf4-42a0-b21d-4d23169385c0>
- Activities workspace: mở `/activities/{activityId}` với một trong các ID bên dưới.
- Attendance: <http://127.0.0.1:5173/attendance>

Curriculum có 5 module và 32 lesson; 28 lesson đã phát hành cho sinh viên.
Module 5 chỉ phát hành 3 lesson đầu, các lesson còn lại vẫn là bản nháp trong
giao diện quản lý của teacher. Bảy tài nguyên đã phát hành được gắn vào các
lesson tiêu biểu, gồm tài liệu PDF, slide và một liên kết external/video.

### Tài khoản chính

| Vai trò | Email | Mật khẩu | Tên hiển thị |
| --- | --- | --- | --- |
| Admin | `admin.demo@rlms.local` | `DemoAdmin2026!` | RLMS Demo Administrator |
| Teacher | `teacher.demo@rlms.local` | `TeacherDemo2026!` | Nguyễn Minh Anh |

Tài khoản teacher có quyền quản lý course/class, phát hành nội dung, hoạt động,
essay, gradebook, attendance và live classroom. Tài khoản admin chỉ dùng để
bootstrap tenant và người dùng.

### 25 tài khoản sinh viên

Mật khẩu theo mẫu `StudentDemoNN2026!`, trong đó `NN` là số thứ tự hai chữ số.
Mỗi tài khoản đã được ghi danh vào `RLMS-WED-DEMO`.

| # | Email | Mật khẩu | Tên hiển thị |
| ---: | --- | --- | --- |
| 01 | `student01@rlms.local` | `StudentDemo012026!` | Nguyễn Văn Minh |
| 02 | `student02@rlms.local` | `StudentDemo022026!` | Trần Gia Huy |
| 03 | `student03@rlms.local` | `StudentDemo032026!` | Lê Hoàng Nam |
| 04 | `student04@rlms.local` | `StudentDemo042026!` | Phạm Ngọc Anh |
| 05 | `student05@rlms.local` | `StudentDemo052026!` | Võ Minh Khang |
| 06 | `student06@rlms.local` | `StudentDemo062026!` | Đặng Thảo My |
| 07 | `student07@rlms.local` | `StudentDemo072026!` | Nguyễn Quốc Bảo |
| 08 | `student08@rlms.local` | `StudentDemo082026!` | Trần Khánh Linh |
| 09 | `student09@rlms.local` | `StudentDemo092026!` | Lê Đức Anh |
| 10 | `student10@rlms.local` | `StudentDemo102026!` | Hoàng Nhật Minh |
| 11 | `student11@rlms.local` | `StudentDemo112026!` | Phan Gia Hân |
| 12 | `student12@rlms.local` | `StudentDemo122026!` | Nguyễn Thành Đạt |
| 13 | `student13@rlms.local` | `StudentDemo132026!` | Võ Thanh Tùng |
| 14 | `student14@rlms.local` | `StudentDemo142026!` | Đỗ Minh Thư |
| 15 | `student15@rlms.local` | `StudentDemo152026!` | Bùi Gia Bảo |
| 16 | `student16@rlms.local` | `StudentDemo162026!` | Trần Anh Khoa |
| 17 | `student17@rlms.local` | `StudentDemo172026!` | Nguyễn Ngọc Mai |
| 18 | `student18@rlms.local` | `StudentDemo182026!` | Lê Quang Huy |
| 19 | `student19@rlms.local` | `StudentDemo192026!` | Phạm Minh Đức |
| 20 | `student20@rlms.local` | `StudentDemo202026!` | Võ Hải Yến |
| 21 | `student21@rlms.local` | `StudentDemo212026!` | Nguyễn Tuấn Kiệt |
| 22 | `student22@rlms.local` | `StudentDemo222026!` | Trần Ngọc Hân |
| 23 | `student23@rlms.local` | `StudentDemo232026!` | Lê Minh Quân |
| 24 | `student24@rlms.local` | `StudentDemo242026!` | Nguyễn Phương Anh |
| 25 | `student25@rlms.local` | `StudentDemo252026!` | Phạm Quốc Khánh |

### Dữ liệu kiểm thử đã có

- 25 enrollment và 700 bản ghi lesson progress, với mức hoàn thành khác nhau
  giữa các sinh viên.
- `Quiz C# Fundamentals` (10 câu), `Bài tập OOP` (7 câu) và `Kiểm tra ASP.NET
  Core` (10 câu) đều đã phát hành.
- Activity IDs: Quiz `d778b575-c228-4c4f-8975-063bcb2f6585`; Bài tập
  `8bec25a7-dce4-453e-a157-c4487976c001`; Kiểm tra `ddf4d7a4-349d-4df9-8440-ebe344c3a985`.
- Quiz có 20 bài đã nộp, 3 bài đang làm và 2 chưa bắt đầu. Bài tập OOP có 12
  bài đã nộp, 8 đang làm và 5 chưa bắt đầu. Đề ASP.NET Core giữ 3 sinh viên
  đang làm để kiểm tra Teacher Realtime Workspace.
- Một essay assignment có 6 nhóm, topic ở nhiều trạng thái và submission hợp
  lệ/lỗi validation để kiểm thử teacher workflow.
- Phiên điểm danh ngày 09/09/2026 đã kết thúc, có log join/leave/reconnect;
  có cả trạng thái có mặt, đi trễ, vắng và hai bản ghi reconnect. Hai bản ghi
  đầu được giáo viên xác nhận qua endpoint điều chỉnh điểm danh có audit reason;
  một lịch tương lai ngày 17/09/2026 đang hoạt động. Lịch đã hủy do lần chạy
  thử trước được giữ lại như lịch sử audit, không có lịch active trùng.

## Thông tin dùng chung

- Mã tổ chức: `RLMS-LOCAL`
- Tên tổ chức: `RLMS Local Demo`
- URL ứng dụng: <http://127.0.0.1:5173>
- Trang đăng nhập: <http://127.0.0.1:5173/login>
- Lớp mẫu: `RLMS-WED-DEMO` — Lớp mẫu — Thứ 4
- Class ID: `977bb80f-0525-4c75-ac64-ebf145ebc30b`
- Phiên live mẫu hiện tại: `642306a4-416f-4649-b86f-4fc0e96bf872`
- Đề mẫu Marten: [Đề mẫu Marten · 10 câu trắc nghiệm](http://127.0.0.1:5173/activities/c1d14384-5bd6-4e43-bb85-cf77f075237e)

## Kiểm tra hai giao diện đề mẫu

- Giảng viên theo dõi: <http://127.0.0.1:5173/activities/c1d14384-5bd6-4e43-bb85-cf77f075237e>
- Giảng viên quan sát workspace riêng: <http://127.0.0.1:5173/activities/c1d14384-5bd6-4e43-bb85-cf77f075237e/attempts/15dd3a9e-4912-2f5c-9256-4e3513aa7b2c>
- Sinh viên làm bài: <http://localhost:5173/activities/c1d14384-5bd6-4e43-bb85-cf77f075237e>
- Đề đã phát hành với 10 câu một lựa chọn; attempt của Student Demo đang ở 3/10 để giao diện giảng viên có dữ liệu theo dõi.
- Dùng `localhost` cho phiên sinh viên và `127.0.0.1` cho phiên giảng viên nếu mở cùng lúc trong hai tab; hai host giữ cookie đăng nhập độc lập.

## Tài khoản

| Vai trò | Email đăng nhập | Mật khẩu hiện đã xác nhận | Tên hiển thị | Ghi chú |
| --- | --- | --- | --- | --- |
| Student | `student@rlms.local` | `student123456` | Student Demo | Đã ghi danh lớp mẫu và đã join phiên live mẫu. |
| Teacher | `teacher@rlms.local` | `TeacherDemo2026!` | Teacher Demo | Có quyền quản lý lớp, phiên live và theo dõi bài làm. |
| Admin | `admin@rlms.local` | Chưa xác nhận | Local Administrator | Tài khoản quản trị trong tenant local. |

### Lưu ý về mật khẩu

- Mật khẩu student ở trên đã được đặt lại trong database local hiện tại.
- Mật khẩu `123123123` từng được yêu cầu cho teacher, nhưng chính sách backend hiện yêu cầu mật khẩu dài tối thiểu 12 ký tự nên giá trị đó không phải thông tin đăng nhập hợp lệ qua API.
- Mật khẩu teacher được đặt lại riêng cho môi trường local hiện tại để kiểm thử đề mẫu và giao diện theo dõi.
- Mật khẩu admin hiện chưa được xác nhận lại từ môi trường đang chạy.

## Cách đăng nhập

1. Mở <http://127.0.0.1:5173/login>.
2. Nhập mã tổ chức `RLMS-LOCAL`.
3. Nhập email và mật khẩu theo bảng trên.

Sau khi đăng nhập student, mở lớp `RLMS-WED-DEMO` để kiểm tra lịch học, roster và phiên live. Tài khoản teacher dùng để kiểm tra workspace giảng dạy; tài khoản admin dùng để kiểm tra quản trị người dùng, role và tổ chức.
