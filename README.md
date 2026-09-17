# VietAIS — Realtime Learning Management System (RLMS)

RLMS kết hợp LMS và lớp học realtime, theo scope trong `GOAL.md`.

## Trạng thái triển khai

Backend hiện bao phủ các business slice trong GOAL: Foundation, Identity, Academics,
Classroom, Assessment, Learning, Communication và Reporting. Hệ thống dùng
ASP.NET Core, Marten/PostgreSQL, Wolverine, Redis, S3-compatible storage, cookie
authentication, CSRF, role/permission checks và tenant-scoped sessions. Các flow đã
triển khai gồm course/class/enrollment/scheduling, LiveSession, attendance, chat,
raise-hand, announcements/notifications, versioned question bank, immutable
published activities, event-sourced student attempts, answer save/change/submit,
realtime Class Overview + on-demand Student Detail, grading/gradebook, learning
progress/resources, essay topic/group/report validation, signed file access và các
read model báo cáo cơ bản.

Frontend web đã có trong `frontend/`, dùng Vue 3, Vite, Pinia, Vue Router và SignalR.
Các workspace Student/Teacher/Admin, opened tabs, live classroom, activity
autosave, overview/detail on-demand, essay validation flow và gradebook đều gọi
API tenant-scoped hiện tại. Trong live classroom, SignalR chỉ relay signaling còn
audio/video đi trực tiếp qua WebRTC; không có media payload nào đi qua Business API.
Media:ServerUrl và access token vẫn giữ boundary để bổ sung SFU adapter ở môi
trường triển khai cần scale nhiều người tham gia. AI essay review/RAG, payment, certificate và full
microservices migration vẫn nằm ngoài scope GOAL.

## Chạy local

Yêu cầu .NET SDK theo `global.json`, Python 3 và Docker Compose.

Từ thư mục gốc của repository, chạy toàn bộ hạ tầng, backend và frontend bằng một
lệnh:

```bash
./run-dev.sh
```

Script tự tạo cấu hình local nếu cần, khởi động PostgreSQL/Redis/MinIO, chờ API
healthy rồi chạy Vite frontend. Nhấn `Ctrl+C` để dừng backend và frontend. Nếu
chưa trust development certificate, chạy một lần:

```bash
dotnet dev-certs https --trust
```

Các lệnh riêng lẻ vẫn có thể dùng khi cần debug từng thành phần:

```bash
python3 scripts/setup-local.py
bash scripts/start-services.sh
bash scripts/build.sh
bash scripts/test.sh
dotnet run --project backend/src/Api --launch-profile https
```

Chạy frontend ở terminal khác khi API đang lắng nghe tại `https://localhost:7041`:

```bash
cd frontend
npm install
npm run typecheck
npm run dev
```

Vite proxy các request `/api` và `/hubs` tới API local. Có thể đặt
`VITE_DEV_API_TARGET` hoặc `VITE_API_BASE_URL` trong file `.env.local` khi chạy
frontend ở môi trường khác.

Kiểm thử workload realtime riêng chạy trên test host và mặc định dùng 16 sinh viên:

```bash
bash scripts/load-test.sh
RLMS_LOAD_STUDENTS=32 bash scripts/load-test.sh
```

API HTTPS: `https://localhost:7041`; health HTTP: `http://localhost:5164/health/live`.
Cookie Secure yêu cầu HTTPS cho các flow đăng nhập. Cần trust development
certificate trên máy dùng browser (`dotnet dev-certs https --trust`).

`setup-local.py` tạo secret ngẫu nhiên trong `.env` và `appsettings.Local.json`
đã được gitignore; chạy lại giữ nguyên cấu hình hiện có. Không dùng credential local
cho production. Mỗi test fixture tạo database PostgreSQL riêng, tách khỏi `rlms`.

Với API đang chạy:

```bash
python3 scripts/smoke-foundation.py
python3 scripts/smoke-foundation.py --isolate-dependencies
```

Lệnh thứ hai tạm dừng Redis/MinIO của riêng RLMS, kiểm tra core health rồi khởi động
lại chúng. Chỉ chạy trong môi trường local không có người dùng khác đang học.

PostgreSQL: localhost:55432; Redis: localhost:56379; S3: localhost:59000;
MinIO console: localhost:59001. Local compose chưa chạy SFU thật; live classroom
dùng WebRTC peer connections cho phiên thử nghiệm cục bộ.

Để tạo bộ dữ liệu demo thực tế cho tenant riêng `RLMS-DEMO`, chạy sau khi API đã
healthy:

```bash
RLMS_DEMO_DATA=1 python3 scripts/setup-demo-data.py
```

Runner chỉ gọi các HTTP API nghiệp vụ trên host local, tự đăng nhập bằng tài
khoản admin/teacher/student, kiểm tra permission và publication rules, và có thể
chạy lại an toàn. Tài khoản, route và số liệu đã tạo được ghi trong
[`docs/reference/DEMO_ACCOUNTS.md`](docs/reference/DEMO_ACCOUNTS.md). Dữ liệu
`RLMS-LOCAL` hiện có không bị thay đổi.

## Tài liệu

- **SYSTEM_MASTER.md**: tổng thể hệ thống + toàn bộ chức năng; đọc sau GOAL.
- **SYSTEM_ARCHITECTURE.md**: kiến trúc business/realtime/media/storage/scaling.
- **ARCHITECTURE_RULES.md**: rule bắt buộc khi code và review.
- **FUNCTIONS.md**: danh sách chức năng chi tiết.

Nguyên tắc quan trọng nhất:
1. Modular Monolith trước.
2. Shared Once, Fan-out Many.
3. Realtime Overview nhẹ + Student Detail on-demand.
4. Failure Isolation.
5. Không hạ trải nghiệm để đổi lấy chịu tải.
6. AI essay review là Coming Soon và không được chấm điểm.

## Tài liệu điều hành triển khai

- **GOAL.md**: scope triển khai hiện tại; đọc đầu tiên trước khi code.
- **WORKFLOW.md**: quy trình phân tích, Event Storming, implement, test và review.

- **PROJECT_STRUCTURE.md**: cấu trúc thư mục chuẩn frontend/backend từ root.
- **DOMAIN_MODEL.md**, **UX_STRUCTURE.md**: domain ownership và user flows.
- **GIT_RULES.md**: feature branch và Draft PR về `develop`.
- **docs/architecture/foundation.md**: quyết định và verification cho Foundation và Identity.
- **docs/architecture/multi-tenancy.md**: quyết định database, tenant isolation và placement strategy.
- **docs/reference/**: tài khoản demo và quy tắc tham chiếu UI.
- **docs/audits/**: các báo cáo audit môi trường và kiến trúc.
- **docs/README.md**: mục lục tài liệu theo nhóm.
