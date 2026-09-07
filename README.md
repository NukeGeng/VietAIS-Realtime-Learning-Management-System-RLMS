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

Repository hiện là backend/API và không chứa frontend web hoàn chỉnh. Media payload
đi qua SFU bên ngoài; AI essay review/RAG, payment, certificate và full
microservices migration vẫn nằm ngoài scope GOAL.

## Chạy local

Yêu cầu .NET SDK theo `global.json`, Python 3 và Docker Compose.

```bash
python3 scripts/setup-local.py
bash scripts/start-services.sh
bash scripts/build.sh
bash scripts/test.sh
dotnet run --project backend/src/Api --launch-profile https
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
MinIO console: localhost:59001. Media endpoint là boundary tới SFU bên ngoài;
local compose chưa chạy SFU thật.

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
