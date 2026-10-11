#!/usr/bin/env python3
"""Build the RLMS-DEMO tenant through the public local HTTP workflows.

The script intentionally uses the same HTTP commands as the teacher and
students.  It is local-only, deterministic, and safe to rerun: demo objects
are found by their stable code/title/email before a create call is made.

Run only against a development server, for example:

    RLMS_DEMO_DATA=1 python3 scripts/setup-demo-data.py

The backend must be running on https://localhost:7041 and the local object
storage service must be available when essay reports are uploaded.
"""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import http.cookiejar
import json
import os
from pathlib import Path
import re
import ssl
import struct
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_BASE_URL = "https://localhost:7041"
ORG_CODE = "RLMS-DEMO"
ORG_NAME = "RLMS Demo University"
ADMIN_EMAIL = "admin.demo@rlms.local"
ADMIN_PASSWORD = os.environ.get("RLMS_DEMO_ADMIN_PASSWORD", "DemoAdmin2026!")
TEACHER_EMAIL = "teacher.demo@rlms.local"
TEACHER_PASSWORD = "TeacherDemo2026!"

STUDENT_NAMES = [
    "Nguyễn Văn Minh", "Trần Gia Huy", "Lê Hoàng Nam", "Phạm Ngọc Anh",
    "Võ Minh Khang", "Đặng Thảo My", "Nguyễn Quốc Bảo", "Trần Khánh Linh",
    "Lê Đức Anh", "Hoàng Nhật Minh", "Phan Gia Hân", "Nguyễn Thành Đạt",
    "Võ Thanh Tùng", "Đỗ Minh Thư", "Bùi Gia Bảo", "Trần Anh Khoa",
    "Nguyễn Ngọc Mai", "Lê Quang Huy", "Phạm Minh Đức", "Võ Hải Yến",
    "Nguyễn Tuấn Kiệt", "Trần Ngọc Hân", "Lê Minh Quân", "Nguyễn Phương Anh",
    "Phạm Quốc Khánh",
]

MODULES = [
    ("C# Fundamentals", [
        ("Giới thiệu .NET và C#", "Nắm được CLR, SDK, project và vòng đời chạy của một ứng dụng C#."),
        ("Biến và kiểu dữ liệu", "Phân biệt kiểu giá trị, kiểu tham chiếu, nullable và chuyển đổi an toàn."),
        ("Điều kiện và vòng lặp", "Viết luồng điều khiển dễ đọc bằng if, switch, for, foreach và while."),
        ("Collection và Generic", "Chọn collection phù hợp và dùng generic để giữ an toàn kiểu dữ liệu."),
        ("Method và Parameter", "Thiết kế method có trách nhiệm rõ ràng, tham số dễ hiểu và giá trị trả về ổn định."),
    ]),
    ("Object-Oriented Programming", [
        ("Class và Object", "Mô hình hóa thực thể backend bằng class, object và trạng thái có ý nghĩa."),
        ("Property và Constructor", "Khởi tạo object hợp lệ và kiểm soát dữ liệu qua property."),
        ("Encapsulation", "Đóng gói invariant trong domain object thay vì để mã gọi tự sửa trạng thái."),
        ("Inheritance", "Nhận biết khi kế thừa giúp tái sử dụng và khi composition phù hợp hơn."),
        ("Polymorphism", "Dùng abstraction và polymorphism để thay đổi hành vi mà không sửa client."),
        ("Interface và Abstraction", "Tách hợp đồng khỏi triển khai để test và thay thế dependency dễ dàng."),
    ]),
    ("ASP.NET Core", [
        ("Tổng quan ASP.NET Core", "Hiểu host, middleware pipeline, configuration và môi trường chạy."),
        ("Minimal API", "Tạo endpoint nhỏ gọn với request, response, validation và dependency injection."),
        ("Routing và Endpoint", "Thiết kế route rõ nghĩa, ràng buộc tham số và mã trạng thái HTTP phù hợp."),
        ("Dependency Injection", "Đăng ký lifetime và inject dependency đúng boundary của module."),
        ("Middleware", "Đặt logging, exception handling, authentication và correlation theo đúng thứ tự."),
        ("Validation", "Trả lỗi cụ thể cho input sai và giữ invariant ở cả API lẫn domain."),
        ("Authentication và Authorization", "Phân biệt xác thực với quyền, tenant scope và permission policy."),
    ]),
    ("PostgreSQL & Marten", [
        ("PostgreSQL cơ bản", "Làm quen schema, khóa, transaction và truy vấn có chỉ mục."),
        ("Document Database với Marten", "Lưu document theo aggregate boundary và tenant một cách an toàn."),
        ("IDocumentSession và IQuerySession", "Tách thao tác ghi khỏi query read model trong request."),
        ("Query và Specification", "Viết query đúng màn hình, tránh tải cả object graph không cần thiết."),
        ("Indexing", "Đo query thật và thêm index cho predicate, sort hoặc uniqueness cần thiết."),
        ("Event Sourcing cơ bản", "Chọn event sourcing khi lịch sử chuyển trạng thái có giá trị nghiệp vụ."),
        ("Projection trong Marten", "Dựng read model bất đồng bộ để dashboard không phải replay event history."),
    ]),
    ("Backend Architecture", [
        ("Clean Code cho Backend", "Giữ tên, boundary và flow xử lý rõ ràng để thay đổi có kiểm soát."),
        ("Domain-Driven Design cơ bản", "Xác định bounded context, aggregate và ubiquitous language."),
        ("CQRS", "Tách command thay đổi trạng thái khỏi query phục vụ giao diện."),
        ("Domain Event", "Phát sự kiện sau khi nghiệp vụ hoàn tất và giữ payload đúng nhu cầu consumer."),
        ("Wolverine", "Điều phối command, message và retry theo convention của modular monolith."),
        ("Modular Monolith", "Giữ module độc lập trong một tiến trình trước khi có bằng chứng cần tách service."),
        ("Tổng kết xây dựng Backend API", "Kết hợp validation, persistence, permission, realtime và quan sát vận hành."),
    ]),
]


def q(topic: str, prompt: str, options: list[tuple[str, str]], correct: str,
      difficulty: str = "Trung bình", question_type: int = 0) -> dict:
    return {
        "topic": f"RLMS-DEMO · {topic}", "prompt": prompt, "options": options,
        "correct": [] if question_type == 4 else [correct],
        "difficulty": difficulty, "type": question_type,
    }


QUESTIONS = [
    q("C# Fundamentals", "CLR có vai trò chính nào khi chạy ứng dụng .NET?", [("A", "Biên dịch CSS"), ("B", "Quản lý thực thi mã .NET"), ("C", "Lưu file tĩnh"), ("D", "Tạo bảng SQL")], "B", "Dễ"),
    q("C# Fundamentals", "Từ khóa nào khai báo biến không thể gán lại sau khi khởi tạo?", [("A", "var"), ("B", "static"), ("C", "readonly"), ("D", "dynamic")], "C", "Dễ"),
    q("C# Fundamentals", "Collection nào phù hợp khi cần tra cứu theo khóa duy nhất?", [("A", "List<T>"), ("B", "Queue<T>"), ("C", "Dictionary<TKey,TValue>"), ("D", "Stack<T>")], "C", "Dễ"),
    q("C# Fundamentals", "`async`/`await` trong C# chủ yếu giúp điều gì?", [("A", "Chạy SQL đồng bộ hơn"), ("B", "Viết luồng bất đồng bộ dễ đọc"), ("C", "Bỏ qua exception"), ("D", "Tắt garbage collector")], "B"),
    q("C# Fundamentals", "Generic giúp method hoặc class đạt lợi ích nào?", [("A", "Không cần kiểu dữ liệu"), ("B", "An toàn kiểu và tái sử dụng"), ("C", "Luôn chạy nhanh gấp đôi"), ("D", "Tự tạo database")], "B"),
    q("Object-Oriented Programming", "Encapsulation nên đặt invariant ở đâu?", [("A", "Trong mọi controller"), ("B", "Trong object sở hữu trạng thái"), ("C", "Trong file cấu hình"), ("D", "Trong CSS")], "B"),
    q("Object-Oriented Programming", "Constructor nên đảm bảo điều gì?", [("A", "Object được tạo ở trạng thái hợp lệ"), ("B", "Mọi field đều public"), ("C", "Luôn gọi database"), ("D", "Không nhận tham số")], "A"),
    q("Object-Oriented Programming", "Khi hai lớp chỉ cần dùng chung hành vi, lựa chọn thường phù hợp hơn kế thừa là gì?", [("A", "Composition"), ("B", "Global variable"), ("C", "Reflection bắt buộc"), ("D", "Copy-paste")], "A"),
    q("Object-Oriented Programming", "Interface mô tả tốt nhất điều gì?", [("A", "Hợp đồng hành vi"), ("B", "Một bảng database"), ("C", "Một thread"), ("D", "Một HTTP status")], "A"),
    q("Object-Oriented Programming", "Polymorphism cho phép client làm gì?", [("A", "Gọi abstraction mà không biết triển khai cụ thể"), ("B", "Sửa private field tùy ý"), ("C", "Bỏ qua compile"), ("D", "Xóa mọi dependency")], "A"),
    q("Object-Oriented Programming", "Access modifier nào giới hạn thành viên trong chính type?", [("A", "public"), ("B", "protected"), ("C", "private"), ("D", "internal")], "C"),
    q("Object-Oriented Programming", "Câu hỏi tự luận: hãy mô tả một cách áp dụng abstraction trong service backend.", [], "", "Trung bình", 4),
    q("ASP.NET Core", "Middleware trong ASP.NET Core được thực thi theo mô hình nào?", [("A", "Pipeline request/response"), ("B", "Chỉ chạy lúc build"), ("C", "Chỉ chạy ở database"), ("D", "Chỉ chạy trên browser")], "A"),
    q("ASP.NET Core", "Minimal API thường khai báo endpoint bằng cách nào?", [("A", "MapGet/MapPost trên app"), ("B", "Tạo file CSS"), ("C", "Sửa csproj bằng tay mỗi request"), ("D", "Chỉ dùng stored procedure")], "A", "Dễ"),
    q("ASP.NET Core", "HTTP status nào thường dùng khi tạo resource thành công?", [("A", "200 OK"), ("B", "201 Created"), ("C", "204 No Content"), ("D", "500")], "B", "Dễ"),
    q("ASP.NET Core", "DI lifetime Scoped phù hợp với phạm vi nào?", [("A", "Một HTTP request scope"), ("B", "Toàn bộ máy chủ mãi mãi"), ("C", "Một CSS bundle"), ("D", "Một database column")], "A"),
    q("ASP.NET Core", "Authorization khác authentication ở điểm nào?", [("A", "Authorization quyết định quyền được làm gì"), ("B", "Authentication luôn cấp admin"), ("C", "Hai khái niệm giống hệt"), ("D", "Authorization chỉ dùng cho CSS")], "A"),
    q("ASP.NET Core", "Validation lỗi input nên trả điều gì cho client?", [("A", "Lỗi cụ thể và mã 4xx"), ("B", "Stack trace database"), ("C", "Luôn 200"), ("D", "Không trả response")], "A", "Dễ"),
    q("ASP.NET Core", "Trong API nhiều tenant, cache key cần thêm thành phần nào?", [("A", "Tenant/organization scope"), ("B", "Màu nút"), ("C", "Tên browser"), ("D", "CSS class")], "A"),
    q("PostgreSQL & Marten", "Transaction giúp bảo vệ tính chất nào?", [("A", "Atomicity của nhóm thay đổi"), ("B", "Độ sáng màn hình"), ("C", "Kích thước font"), ("D", "Tên route")], "A"),
    q("PostgreSQL & Marten", "IDocumentSession thường dùng cho thao tác nào?", [("A", "Theo dõi và lưu document"), ("B", "Render video"), ("C", "Xác thực browser"), ("D", "Tạo CSS")], "A"),
    q("PostgreSQL & Marten", "IQuerySession nên được dùng khi nào?", [("A", "Đọc dữ liệu không thay đổi state"), ("B", "Append event bắt buộc"), ("C", "Upload file binary"), ("D", "Đăng nhập browser")], "A"),
    q("PostgreSQL & Marten", "Index có tác dụng trực tiếp nào?", [("A", "Giảm chi phí truy vấn theo pattern phù hợp"), ("B", "Đổi kiểu dữ liệu tùy ý"), ("C", "Tự sửa business rule"), ("D", "Thay transaction")], "A"),
    q("PostgreSQL & Marten", "Projection thường phục vụ mục đích nào?", [("A", "Tạo read model cho query/UI"), ("B", "Mã hóa mật khẩu"), ("C", "Thay enrollment"), ("D", "Chặn mọi event")], "A"),
    q("PostgreSQL & Marten", "Event sourcing phù hợp nhất khi nào?", [("A", "Lịch sử chuyển trạng thái có giá trị nghiệp vụ"), ("B", "Mọi heartbeat UI"), ("C", "Mỗi mouse move"), ("D", "Mọi file binary")], "A"),
    q("CQRS & Architecture", "CQRS tách hai trách nhiệm nào?", [("A", "Command ghi và query đọc"), ("B", "HTML và CSS"), ("C", "Browser và monitor"), ("D", "Email và password")], "A", "Dễ"),
    q("CQRS & Architecture", "Domain event nên biểu đạt điều gì?", [("A", "Một việc nghiệp vụ đã xảy ra"), ("B", "Một ý định chưa xử lý"), ("C", "Một heartbeat"), ("D", "Một câu SQL")], "A"),
    q("CQRS & Architecture", "Aggregate boundary giúp tránh vấn đề nào?", [("A", "Monster aggregate và transaction quá rộng"), ("B", "Mọi validation"), ("C", "Mọi permission"), ("D", "Mọi query")], "A"),
    q("CQRS & Architecture", "Modular monolith nên ưu tiên điều gì?", [("A", "Boundary module rõ trong một tiến trình"), ("B", "Tách service ngay lập tức"), ("C", "Dùng Redis làm source of truth"), ("D", "Bỏ test")], "A"),
    q("CQRS & Architecture", "Wolverine phù hợp với nhiệm vụ nào trong RLMS?", [("A", "Điều phối command/message theo convention"), ("B", "Lưu file PDF"), ("C", "Thay password hasher"), ("D", "Render Vue")], "A"),
    q("CQRS & Architecture", "Câu hỏi tự luận: nêu cách tách control plane và media plane trong live classroom.", [], "", "Khó", 4),
]


class ApiError(RuntimeError):
    def __init__(self, status: int, body: object):
        self.status = status
        self.body = body
        super().__init__(f"HTTP {status}: {body}")


class ApiClient:
    def __init__(self, base_url: str):
        self.base_url = base_url.rstrip("/")
        self.cookies = http.cookiejar.CookieJar()
        context = ssl._create_unverified_context()
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.cookies),
            urllib.request.HTTPSHandler(context=context),
        )
        self.csrf = ""

    def _request(self, method: str, path: str, body: object | None = None,
                 headers: dict[str, str] | None = None,
                 data: bytes | None = None, allow: set[int] | None = None):
        request_headers = {"Accept": "application/json", **(headers or {})}
        if data is None and body is not None:
            data = json.dumps(body, ensure_ascii=False).encode("utf-8")
            request_headers.setdefault("Content-Type", "application/json")
        if method.upper() not in {"GET", "HEAD", "OPTIONS"} and self.csrf:
            request_headers.setdefault("X-CSRF-TOKEN", self.csrf)
        request = urllib.request.Request(
            self.base_url + path, data=data, headers=request_headers, method=method.upper())
        try:
            response = self.opener.open(request, timeout=45)
            status = response.status
            raw = response.read()
        except urllib.error.HTTPError as error:
            status = error.code
            raw = error.read()
        except urllib.error.URLError as error:
            raise RuntimeError(f"Cannot reach {self.base_url}: {error}") from error
        try:
            value = json.loads(raw.decode("utf-8")) if raw else None
        except json.JSONDecodeError:
            value = raw.decode("utf-8", errors="replace")
        if allow and status in allow:
            # Optional GETs use 404 as the absence signal. Keeping that
            # distinction as None prevents a problem-details document from
            # being mistaken for an existing domain object.
            return None if status == 404 else value
        if status < 200 or status >= 300:
            raise ApiError(status, value)
        return value

    def csrf_token(self):
        value = self._request("GET", "/api/identity/csrf")
        self.csrf = value["token"]
        return self.csrf

    def login(self, email: str, password: str, organization: str = ORG_CODE):
        self.csrf_token()
        result = self._request("POST", "/api/identity/login", {
            "organizationCode": organization, "email": email, "password": password,
        })
        # Login may rotate the antiforgery token alongside the auth cookie.
        # Fetch a fresh request token before the first authenticated mutation.
        self.csrf_token()
        return result

    def get(self, path: str, allow: set[int] | None = None):
        return self._request("GET", path, allow=allow)

    def post(self, path: str, body: object | None = None, allow: set[int] | None = None,
             headers: dict[str, str] | None = None):
        return self._request("POST", path, body, headers=headers, allow=allow)

    def put(self, path: str, body: object, allow: set[int] | None = None):
        return self._request("PUT", path, body, allow=allow)

    def multipart(self, path: str, fields: dict[str, str], filename: str,
                  content_type: str, content: bytes, allow: set[int] | None = None):
        boundary = f"----rlms-demo-{uuid.uuid4().hex}"
        chunks: list[bytes] = []
        for key, value in fields.items():
            chunks += [f"--{boundary}\r\n".encode(),
                       f'Content-Disposition: form-data; name="{key}"\r\n\r\n'.encode(),
                       str(value).encode(), b"\r\n"]
        chunks += [f"--{boundary}\r\n".encode(),
                   f'Content-Disposition: form-data; name="file"; filename="{filename}"\r\n'.encode(),
                   f"Content-Type: {content_type}\r\n\r\n".encode(), content, b"\r\n",
                   f"--{boundary}--\r\n".encode()]
        return self._request("POST", path, data=b"".join(chunks), headers={
            "Content-Type": f"multipart/form-data; boundary={boundary}",
        }, allow=allow)


def stable_guid(value: str) -> str:
    digest = bytearray(hashlib.sha256(value.encode()).digest()[:16])
    digest[6] = (digest[6] & 0x0F) | 0x50
    digest[8] = (digest[8] & 0x3F) | 0x80
    # .NET's Guid(byte[]) constructor uses little-endian fields for the first
    # three components when formatting as D, so bytes_le matches the server's
    # AssessmentConstants.StableId implementation.
    return str(uuid.UUID(bytes_le=bytes(digest)))


def enum_is(value: object, number: int, name: str) -> bool:
    return value == number or str(value).lower() == name.lower()


def iso(value: dt.datetime) -> str:
    return value.replace(microsecond=0).isoformat()


def read_local_setting(name: str) -> str:
    env_path = ROOT / ".env"
    if env_path.exists():
        for line in env_path.read_text().splitlines():
            if line.startswith(name + "="):
                return line.split("=", 1)[1].strip()
    local = ROOT / "backend/src/Host/LearningPlatform.Api/appsettings.Local.json"
    if name == "RLMS_BOOTSTRAP_TOKEN" and local.exists():
        try:
            return json.loads(local.read_text())["Identity"]["BootstrapToken"]
        except (KeyError, json.JSONDecodeError):
            pass
    return ""


def ensure_user(admin: ApiClient, email: str, display: str, password: str,
                role: str) -> dict:
    users = admin.get("/api/identity/users?page=1&pageSize=100") or []
    found = next((u for u in users if u.get("email", "").lower() == email.lower()), None)
    body = {"displayName": display, "password": password, "roles": [role]}
    if found:
        admin.put(f"/api/identity/users/{found['id']}", body)
        return {**found, "displayName": display, "email": email.upper()}
    try:
        return admin.post("/api/identity/users", {
            "email": email, "displayName": display, "password": password, "roles": [role],
        })
    except ApiError as error:
        if error.status != 409:
            raise
        users = admin.get("/api/identity/users?page=1&pageSize=100") or []
        found = next((u for u in users if u.get("email", "").lower() == email.lower()), None)
        if not found:
            raise
        return found


def bootstrap_admin(base_url: str, token: str) -> ApiClient:
    client = ApiClient(base_url)
    try:
        client.login(ADMIN_EMAIL, ADMIN_PASSWORD)
        return client
    except ApiError as login_error:
        if login_error.status not in {400, 401, 404}:
            raise
    client.csrf_token()
    try:
        client.post("/api/identity/bootstrap", {
            "organizationCode": ORG_CODE, "organizationName": ORG_NAME,
            "adminEmail": ADMIN_EMAIL, "adminDisplayName": "RLMS Demo Administrator",
            "adminPassword": ADMIN_PASSWORD,
        }, headers={
            "X-RLMS-Bootstrap-Token": token,
            "X-RLMS-Allow-Additional-Development-Organization": "true",
        })
    except ApiError as error:
        if error.status != 409:
            raise RuntimeError("Unable to bootstrap RLMS-DEMO. "
                               "Use RLMS_DEMO_ADMIN_PASSWORD when the tenant already exists.") from error
    admin = ApiClient(base_url)
    try:
        admin.login(ADMIN_EMAIL, ADMIN_PASSWORD)
    except ApiError as error:
        raise RuntimeError("RLMS-DEMO exists but the admin password is not the configured "
                           "RLMS_DEMO_ADMIN_PASSWORD.") from error
    return admin


def ensure_course(teacher: ApiClient) -> dict:
    courses = teacher.get("/api/academics/courses?page=1&pageSize=100") or []
    course = next((v for v in courses if v.get("code") == "CSHARP-BE"), None)
    payload = {
        "code": "CSHARP-BE", "title": "C# Backend Development",
        "description": "Khóa học xây dựng nền tảng C# và ASP.NET Core từ kiến thức ngôn ngữ, OOP, Web API đến làm việc với PostgreSQL, Marten và kiến trúc backend hiện đại.",
    }
    if course is None:
        course = teacher.post("/api/academics/courses", payload)
    else:
        teacher.put(f"/api/academics/courses/{course['id']}", payload)
    if not enum_is(course.get("status"), 1, "published"):
        teacher.post(f"/api/academics/courses/{course['id']}/publish", allow={409})
    detail = teacher.get(f"/api/academics/courses/{course['id']}")
    # Course detail is a wrapped response (`course`, `modules`, `lessons`),
    # while the list/create endpoints return the course itself.
    return detail.get("course", detail)


def ensure_curriculum(teacher: ApiClient, course_id: str, class_id: str | None = None) -> dict:
    curriculums = teacher.get(f"/api/academics/courses/{course_id}/curriculums") or []
    curriculum = next((v for v in curriculums if v.get("title") == "Nền tảng C# Backend"), None)
    if curriculum is None:
        curriculum = teacher.post(f"/api/academics/courses/{course_id}/curriculums", {
            "title": "Nền tảng C# Backend",
            "description": "Giáo trình nền tảng dành cho sinh viên bắt đầu học Backend với C# và ASP.NET Core.",
        })
    if not enum_is(curriculum.get("status"), 1, "published"):
        curriculum = teacher.post(f"/api/academics/curriculums/{curriculum['id']}/publish")
    if class_id:
        assigned = teacher.get(f"/api/academics/classes/{class_id}/curriculums") or []
        if not any(v.get("curriculumId") == curriculum["id"] for v in assigned):
            teacher.post(f"/api/academics/classes/{class_id}/curriculums", {
                "curriculumId": curriculum["id"], "order": 1,
            })
    return curriculum


def ensure_content(teacher: ApiClient, course: dict) -> tuple[dict, list[dict]]:
    detail = teacher.get(f"/api/academics/courses/{course['id']}")
    modules_by_order = {m["order"]: m for m in detail.get("modules", [])}
    all_lessons = detail.get("lessons", [])
    lessons_by_module: dict[str, dict[int, dict]] = {}
    for lesson in all_lessons:
        lessons_by_module.setdefault(lesson["courseModuleId"], {})[lesson["order"]] = lesson

    for module_order, (module_title, lesson_specs) in enumerate(MODULES, start=1):
        module = modules_by_order.get(module_order)
        if module is None:
            module = teacher.post(f"/api/academics/courses/{course['id']}/modules", {
                "title": module_title, "order": module_order,
            })
        elif module.get("title") != module_title:
            module = teacher.put(
                f"/api/academics/courses/{course['id']}/modules/{module['id']}",
                {"title": module_title, "order": module_order})
        existing_lessons = lessons_by_module.setdefault(module["id"], {})
        for lesson_order, (title, focus) in enumerate(lesson_specs, start=1):
            summary = (
                f"Mục tiêu học tập: {focus}\n\n"
                f"Nội dung cốt lõi: Sinh viên đọc ví dụ, phân tích trade-off và liên hệ với một API backend có tenant, validation và persistence. "
                f"Bài học tập trung vào cách đặt tên, boundary và xử lý lỗi để mã dễ kiểm thử.\n\n"
                f"Thực hành: viết một ví dụ nhỏ, kiểm tra happy path và một trường hợp lỗi. Thời lượng dự kiến: {35 + (lesson_order % 3) * 10} phút."
            )
            lesson = existing_lessons.get(lesson_order)
            if lesson is None:
                lesson = teacher.post(
                    f"/api/academics/courses/{course['id']}/modules/{module['id']}/lessons",
                    {"title": title, "order": lesson_order, "contentSummary": summary})
            elif lesson.get("title") != title or lesson.get("contentSummary") != summary:
                lesson = teacher.put(
                    f"/api/academics/courses/{course['id']}/modules/{module['id']}/lessons/{lesson['id']}",
                    {"title": title, "order": lesson_order, "contentSummary": summary})
            existing_lessons[lesson_order] = lesson
        modules_by_order[module_order] = module

    # Publish modules 1-4 and all their lessons. Module 5 is deliberately
    # partial: the parent module is published, only its first three lessons are.
    for module_order, (_, lesson_specs) in enumerate(MODULES, start=1):
        module = modules_by_order[module_order]
        if not enum_is(module.get("status"), 1, "published"):
            module = teacher.post(
                f"/api/academics/courses/{course['id']}/modules/{module['id']}/publish",
                allow={409}) or module
        for lesson_order in range(1, len(lesson_specs) + 1):
            if module_order == 5 and lesson_order > 3:
                continue
            lesson = lessons_by_module[module["id"]][lesson_order]
            if not enum_is(lesson.get("status"), 1, "published"):
                teacher.post(
                    f"/api/academics/courses/{course['id']}/modules/{module['id']}/lessons/{lesson['id']}/publish",
                    allow={409})
    detail = teacher.get(f"/api/academics/courses/{course['id']}")
    return detail, [lesson for lesson in detail.get("lessons", [])
                    if enum_is(lesson.get("status"), 1, "published")]


def ensure_class(teacher: ApiClient, course_id: str, teacher_id: str) -> dict:
    classes = teacher.get("/api/academics/classes") or []
    learning_class = next((v for v in classes if v.get("code") == "RLMS-WED-DEMO"), None)
    payload = {
        "courseId": course_id, "code": "RLMS-WED-DEMO",
        "name": "Lớp C# Backend — Thứ 4", "startDate": "2026-09-01", "endDate": "2026-12-31",
    }
    if learning_class is None:
        learning_class = teacher.post("/api/academics/classes", payload)
    class_id = learning_class["id"]
    detail = teacher.get(f"/api/academics/classes/{class_id}")
    if teacher_id not in detail.get("teacherIds", []):
        teacher.post(f"/api/academics/classes/{class_id}/teachers", {"teacherId": teacher_id}, allow={409})
    status = learning_class.get("status")
    if enum_is(status, 0, "preparing"):
        teacher.post(f"/api/academics/classes/{class_id}/status", {"status": 1})
    elif enum_is(status, 2, "paused"):
        teacher.post(f"/api/academics/classes/{class_id}/status", {"status": 1})
    return teacher.get(f"/api/academics/classes/{class_id}")


def ensure_enrollments(teacher: ApiClient, class_id: str, students: list[dict]):
    current = {v["studentId"]: v for v in
               (teacher.get(f"/api/academics/classes/{class_id}/enrollments") or [])}
    for student in students:
        entry = current.get(student["id"])
        if entry is None or not enum_is(entry.get("status"), 0, "active"):
            teacher.post(f"/api/academics/classes/{class_id}/enrollments",
                         {"studentId": student["id"]}, allow={409})


def ensure_schedules(teacher: ApiClient, class_id: str, teacher_id: str) -> list[dict]:
    schedules = teacher.get(f"/api/academics/classes/{class_id}/schedules?includeHistory=true") or []
    desired = [
        ("2026-09-09", "07:00:00", "11:30:00"),
        ("2026-09-17", "07:00:00", "11:30:00"),
    ]

    # Older runs briefly used 16/09 and rescheduled it to 17/09. Finish that
    # workflow once if such a legacy schedule is present, then use the final
    # date as the stable identity for all subsequent reruns.
    legacy = next((v for v in schedules
                   if str(v.get("startsAt", "")).startswith("2026-09-16T07:00:00") and
                   not enum_is(v.get("status"), 2, "cancelled") and
                   not enum_is(v.get("status"), 3, "completed")), None)
    if legacy:
        teacher.post(f"/api/academics/classes/{class_id}/schedules/{legacy['id']}/reschedule", {
            "startsAt": "2026-09-17T07:00:00+07:00", "endsAt": "2026-09-17T11:30:00+07:00",
            "recurrence": "FREQ=WEEKLY;BYDAY=TH",
        }, allow={409})
        schedules = teacher.get(f"/api/academics/classes/{class_id}/schedules?includeHistory=true") or []

    for date, start, end in desired:
        recurrence = "FREQ=WEEKLY;BYDAY=TH" if date == "2026-09-17" else "FREQ=WEEKLY;BYDAY=WE"
        if not any(str(v.get("startsAt", "")).startswith(f"{date}T{start}")
                   for v in schedules):
            value = teacher.post(f"/api/academics/classes/{class_id}/schedules", {
                "teacherId": teacher_id, "startsAt": f"{date}T{start}+07:00",
                "endsAt": f"{date}T{end}+07:00", "recurrence": recurrence,
            })
            schedules.append(value)

    # Keep one record for each target date. Prefer a completed record for the
    # past session; otherwise keep the first non-cancelled record. This repairs
    # duplicates produced by interrupted development runs without touching the
    # attendance session itself.
    for date, start, _ in desired:
        matches = [v for v in schedules
                   if str(v.get("startsAt", "")).startswith(f"{date}T{start}")]
        completed = [v for v in matches if enum_is(v.get("status"), 3, "completed")]
        keep = completed[0] if completed else next(
            (v for v in matches if not enum_is(v.get("status"), 2, "cancelled")), None)
        if keep is None:
            continue
        for duplicate in matches:
            if duplicate["id"] == keep["id"] or enum_is(duplicate.get("status"), 2, "cancelled"):
                continue
            teacher.post(f"/api/academics/classes/{class_id}/schedules/{duplicate['id']}/cancel",
                         allow={409})
    return teacher.get(f"/api/academics/classes/{class_id}/schedules?includeHistory=true") or []


def ensure_resources(teacher: ApiClient, class_id: str, lessons: list[dict]):
    resources = teacher.get(f"/api/learning/classes/{class_id}/resources") or []
    lesson_by_title = {lesson["title"]: lesson for lesson in lessons}
    specs = [
        ("gioi-thieu-dotnet.pdf", "Giới thiệu .NET và C#", 0, "https://learn.microsoft.com/dotnet/core/introduction"),
        ("csharp-collections-cheatsheet.pdf", "Collection và Generic", 0, "https://learn.microsoft.com/dotnet/csharp/programming-guide/concepts/collections"),
        ("aspnet-core-routing.pdf", "Routing và Endpoint", 0, "https://learn.microsoft.com/aspnet/core/fundamentals/routing"),
        ("aspnet-core-di-slides", "Dependency Injection", 1, "https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection"),
        ("marten-introduction.pdf", "Document Database với Marten", 0, "https://martendb.io/documents/"),
        ("cqrs-overview.pdf", "CQRS", 0, "https://learn.microsoft.com/azure/architecture/patterns/cqrs"),
        # Keep the external/video reference on a published lesson so students
        # can exercise the resource link from their curriculum workspace.
        ("backend-architecture-video", "CQRS", 2, "https://martinfowler.com/articles/modular-monolith.html"),
    ]
    for title, lesson_title, kind, url in specs:
        lesson = lesson_by_title.get(lesson_title)
        if not lesson:
            continue
        existing = next((v for v in resources if v.get("title") == title), None)
        if existing:
            if not enum_is(existing.get("status"), 1, "published"):
                teacher.post(f"/api/learning/resources/{existing['id']}/publish", allow={409})
            continue
        resource = teacher.post(f"/api/learning/classes/{class_id}/resources", {
            "lessonId": lesson["id"], "title": title,
            "description": f"Tài nguyên tham khảo cho bài {lesson_title}.",
            "type": kind, "externalUrl": url,
        })
        teacher.post(f"/api/learning/resources/{resource['id']}/publish", allow={409})
        resources.append(resource)


def ensure_learning_progress(students: list[dict], clients: dict[str, ApiClient],
                             class_id: str, curriculum_id: str, lessons: list[dict]):
    """Write varied lesson progress through the student progress endpoint.

    The distribution is deterministic by student index: each learner has a
    different completed prefix and, when there are remaining lessons, one
    current in-progress lesson. Calling the endpoint again is safe because the
    server keeps completed lessons monotonic.
    """
    ordered_lessons = sorted(lessons, key=lambda value: (value.get("courseModuleId", ""),
                                                         value.get("order", 0), value["id"]))
    for index, account in enumerate(students):
        if not ordered_lessons:
            continue
        client = clients[account["email"].lower()]
        # Follow the same read-before-write flow as the student workspace: the
        # learner opens the assigned curriculum and the lesson detail before
        # marking it in progress/completed. These GETs also prove that tenant,
        # enrollment, curriculum assignment and publication rules are applied
        # to the demo users rather than bypassed by the runner.
        client.get(f"/api/learning/classes/{class_id}/curriculums/{curriculum_id}")
        completed_count = 3 + (index * 7) % max(1, len(ordered_lessons) - 2)
        completed_count = min(completed_count, len(ordered_lessons))
        for lesson in ordered_lessons[:completed_count]:
            client.get(
                f"/api/learning/classes/{class_id}/curriculums/{curriculum_id}/lessons/{lesson['id']}")
            client.post(
                f"/api/learning/classes/{class_id}/curriculums/{curriculum_id}/lessons/{lesson['id']}/progress",
                {"status": 1})
        if completed_count < len(ordered_lessons):
            client.get(
                f"/api/learning/classes/{class_id}/curriculums/{curriculum_id}/lessons/"
                f"{ordered_lessons[completed_count]['id']}")
            client.post(
                f"/api/learning/classes/{class_id}/curriculums/{curriculum_id}/lessons/"
                f"{ordered_lessons[completed_count]['id']}/progress",
                {"status": 0})


def ensure_questions(teacher: ApiClient, course_id: str) -> dict[str, dict]:
    items = teacher.get("/api/assessment/question-bank?page=1&pageSize=100") or []
    by_prompt: dict[str, dict] = {}
    for item in items:
        if not str(item.get("topic", "")).startswith("RLMS-DEMO"):
            continue
        detail = teacher.get(f"/api/assessment/question-bank/{item['id']}")
        for version in detail.get("versions", []):
            by_prompt[version.get("prompt", "")] = {
                "item": item, "version": version,
            }
    result: dict[str, dict] = {}
    for spec in QUESTIONS:
        existing = by_prompt.get(spec["prompt"])
        if existing:
            result[spec["prompt"]] = {**spec, **existing}
            continue
        body = {
            "courseId": course_id, "topic": spec["topic"], "difficulty": spec["difficulty"],
            "prompt": spec["prompt"], "type": spec["type"],
            "options": [{"key": key, "text": text} for key, text in spec["options"]],
            "correctAnswers": spec["correct"],
        }
        detail = teacher.post("/api/assessment/question-bank", body)
        version = detail["versions"][-1]
        result[spec["prompt"]] = {**spec, "item": detail["item"], "version": version}
    return result


def ensure_activity(teacher: ApiClient, class_id: str, title: str, description: str,
                    kind: int, desired_prompts: list[str], question_map: dict[str, dict],
                    opens_at: str, due_at: str, exam_policy: dict | None = None) -> dict:
    activities = teacher.get(f"/api/assessment/classes/{class_id}/activities") or []
    activity = next((v for v in activities if v.get("title") == title), None)
    if activity is None:
        activity = teacher.post(f"/api/assessment/classes/{class_id}/activities", {
            "title": title, "description": description, "kind": kind,
            "opensAt": opens_at, "dueAt": due_at, "timeLimit": "01:30:00",
            "examPolicy": exam_policy,
        })
    activity = teacher.get(f"/api/assessment/activities/{activity['id']}")
    existing_versions = {v.get("questionVersionId") for v in activity.get("questions", [])}
    if enum_is(activity.get("status"), 0, "draft"):
        for order, prompt in enumerate(desired_prompts, start=1):
            version_id = question_map[prompt]["version"]["id"]
            if version_id in existing_versions:
                continue
            teacher.post(f"/api/assessment/activities/{activity['id']}/questions", {
                "questionVersionId": version_id, "order": order, "points": 10,
            }, allow={409})
        activity = teacher.get(f"/api/assessment/activities/{activity['id']}")
        if activity.get("questions") and not enum_is(activity.get("status"), 1, "published"):
            teacher.post(f"/api/assessment/activities/{activity['id']}/publish", allow={409})
    detail = teacher.get(f"/api/assessment/activities/{activity['id']}")
    # Published activity payloads intentionally omit correct answers. Enrich
    # the local fixture from the deterministic question specs before simulating
    # student attempts; the answer key is never sent to student endpoints.
    for question in detail.get("questions", []):
        spec = question_map.get(question.get("prompt"))
        if spec is not None:
            question["correct"] = spec["correct"]
    return detail


def answer_for(question: dict, student_index: int, order: int) -> str:
    if question["type"] == 4:
        return ("Phân tách hợp đồng khỏi triển khai bằng interface, giữ command và media "
                "plane ở boundary riêng, kiểm tra quyền trước khi phát dữ liệu realtime.")
    keys = [option["key"] for option in question["options"]]
    if not keys:
        return "A"
    if (student_index + order) % 4 == 0:
        return keys[(keys.index(question["correct"][0]) + 1) % len(keys)]
    return question["correct"][0]


def ensure_attempt(student: ApiClient, activity: dict, student_index: int,
                   submit: bool, answer_limit: int) -> dict | None:
    activity_id = activity["id"]
    attempt = student.get(f"/api/assessment/activities/{activity_id}/attempt", allow={404})
    if attempt is None:
        try:
            attempt = student.post(f"/api/assessment/activities/{activity_id}/attempt/start")
        except ApiError as error:
            if error.status == 409:
                return None
            raise
    status = attempt.get("status")
    if enum_is(status, 3, "submitted"):
        return attempt
    if enum_is(status, 1, "idle") or enum_is(status, 2, "paused"):
        student.post(f"/api/assessment/activities/{activity_id}/attempt/resume", allow={409})
    questions = sorted(activity.get("questions", []), key=lambda value: value.get("order", 0))
    count = len(questions) if submit else min(answer_limit, len(questions))
    for index, question in enumerate(questions[:count], start=1):
        student.post(f"/api/assessment/activities/{activity_id}/attempt/questions/{question['id']}/open",
                     allow={409})
        answer = answer_for(question, student_index, index)
        if index == 1 and student_index % 5 == 0 and question.get("options"):
            keys = [option["key"] for option in question["options"]]
            student.put(f"/api/assessment/activities/{activity_id}/attempt/answers/{question['id']}",
                        {"value": keys[-1]})
        student.put(f"/api/assessment/activities/{activity_id}/attempt/answers/{question['id']}",
                    {"value": answer})
    if not submit and student_index % 3 == 0:
        student.post(f"/api/assessment/activities/{activity_id}/attempt/idle", allow={409})
    if submit:
        student.post(f"/api/assessment/activities/{activity_id}/attempt/submit", allow={409})
    return student.get(f"/api/assessment/activities/{activity_id}/attempt", allow={404})


def simulate_activities(students: list[dict], clients: dict[str, ApiClient],
                        activity1: dict, activity2: dict, activity3: dict):
    for index, account in enumerate(students):
        client = clients[account["email"].lower()]
        ensure_attempt(client, activity1, index, index < 20, 4 + index % 5)
        ensure_attempt(client, activity2, index, index < 12, 2 + index % 4)
        # Leave a small, useful live cohort on the open exam.
        if index < 3:
            ensure_attempt(client, activity3, index, False, 1)


def ensure_rubric_and_grade(teacher: ApiClient, activity: dict, students: list[dict]):
    if not any(question.get("type") == 4 for question in activity.get("questions", [])):
        return
    rubric = teacher.get(f"/api/assessment/activities/{activity['id']}/rubric", allow={404})
    if rubric is None:
        rubric = teacher.post(f"/api/assessment/activities/{activity['id']}/rubric", {
            "criteria": [{"id": str(uuid.uuid4()), "name": "Phân tích thiết kế",
                           "description": "Lập luận và ranh giới abstraction.", "maxPoints": 10, "order": 0}]
        })
    student = students[0]
    submission_id = stable_guid(f"submission:{activity['id']}:{student['id']}:1")
    entry = teacher.get(f"/api/assessment/submissions/{submission_id}/grade", allow={404})
    if entry is None:
        subjective = next(q for q in activity["questions"] if q.get("type") == 4)
        criterion_id = rubric.get("criteria", [{}])[0].get("id")
        entry = teacher.post(f"/api/assessment/submissions/{submission_id}/grade", {
            "questions": [{"questionId": subjective["id"], "score": 8,
                           "feedback": "Phân tích đúng boundary; bổ sung ví dụ kiểm thử.",
                           "rubricCriterionId": criterion_id}],
            "feedback": "Bài làm có hướng tiếp cận tốt.", "rubricId": rubric["id"],
        })
    if entry and not enum_is(entry.get("status"), 1, "published"):
        teacher.post(f"/api/assessment/grade-entries/{entry['id']}/publish", allow={409})


def pdf_bytes(title: str, body: str, pages: int = 2) -> bytes:
    lines = [title] + re.findall(r".{1,95}(?:\s|$)", body)
    lines = [line.strip() for line in lines if line.strip()]
    streams: list[bytes] = []
    for page in range(pages):
        page_lines = lines[page * max(1, len(lines) // pages):(page + 1) * max(1, len(lines) // pages)]
        commands = [b"BT /F1 11 Tf 45 770 Td"]
        for line in page_lines[:45]:
            encoded: list[str] = []
            for character in line:
                codepoint = ord(character)
                if character in "\\()":
                    encoded.append("\\" + character)
                elif 32 <= codepoint <= 126:
                    encoded.append(character)
                else:
                    # EssayRules extracts PDF strings as Latin-1 and then
                    # applies Regex.Unescape. A \u escape preserves Vietnamese
                    # text without requiring a custom embedded PDF font.
                    encoded.append(f"\\u{codepoint:04x}")
            commands.append(f"({''.join(encoded)}) Tj 0 -16 Td".encode("ascii"))
        commands.append(b"ET")
        streams.append(b" ".join(commands))
    objects: list[bytes] = [
        b"<< /Type /Catalog /Pages 2 0 R >>",
        b"<< /Type /Pages /Kids [",
        b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
    ]
    page_ids = []
    next_id = 4
    for stream in streams:
        page_id, stream_id = next_id, next_id + 1
        next_id += 2
        page_ids.append(page_id)
        objects[1] += f" {page_id} 0 R".encode()
        objects += [f"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {stream_id} 0 R >>".encode(),
                    f"<< /Length {len(stream)} >>\nstream\n".encode() + stream + b"\nendstream"]
    objects[1] += b" ] /Count " + str(len(streams)).encode() + b" >>"
    output = bytearray(b"%PDF-1.4\n")
    offsets = [0]
    for number, obj in enumerate(objects, start=1):
        offsets.append(len(output)); output += f"{number} 0 obj\n".encode() + obj + b"\nendobj\n"
    xref = len(output); output += f"xref\n0 {len(objects)+1}\n0000000000 65535 f \n".encode()
    output += b"".join(f"{offset:010d} 00000 n \n".encode() for offset in offsets[1:])
    output += f"trailer\n<< /Size {len(objects)+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n".encode()
    return bytes(output)


def ensure_essay(teacher: ApiClient, class_id: str, students: list[dict], clients: dict[str, ApiClient]):
    assignments = teacher.get(f"/api/assessment/classes/{class_id}/essay-assignments") or []
    title = "Thiết kế hệ thống Backend cho nền tảng học trực tuyến"
    assignment = next((v for v in assignments if v.get("title") == title), None)
    policy = {
        "allowedFileTypes": ["application/pdf"], "maxFileBytes": 5 * 1024 * 1024,
        "minimumPages": 2, "minimumWords": 70, "minimumCharacters": 350,
        "requiredSections": ["Mục tiêu", "Kiến trúc", "Kết luận"], "requireTopicMatch": True,
    }
    if assignment is None:
        assignment = teacher.post(f"/api/assessment/classes/{class_id}/essay-assignments", {
            "title": title,
            "description": "Sinh viên phân tích và thiết kế một backend có quản lý khóa học, lớp học, bài tập và tiến độ học tập.",
            "deadline": "2026-12-01T23:59:00+07:00", "maxScore": 100, "maxGroupSize": 5,
            "allowResubmission": True, "validationPolicy": policy,
        })
    if not enum_is(assignment.get("status"), 1, "published"):
        teacher.post(f"/api/assessment/essay-assignments/{assignment['id']}/publish", allow={409})
    assignment = teacher.get(f"/api/assessment/essay-assignments/{assignment['id']}")
    groups = teacher.get(f"/api/assessment/essay-assignments/{assignment['id']}/groups") or []
    topics = [
        "Xây dựng hệ thống quản lý học trực tuyến", "Thiết kế backend quản lý khóa học bằng ASP.NET Core",
        "Xây dựng hệ thống quản lý bài tập và chấm điểm", "Thiết kế hệ thống lớp học realtime",
        "Xây dựng hệ thống quản lý tiến độ học tập", "Thiết kế nền tảng quản lý đào tạo theo Modular Monolith",
    ]
    for index in range(6):
        # Keep all 25 learners represented: five groups of four and one group
        # of five, within the assignment's maxGroupSize.
        members = students[index * 4:] if index == 5 else students[index * 4:(index + 1) * 4]
        group = next((v for v in groups if v.get("name") == f"Nhóm Backend {index + 1:02d}"), None)
        if group is None:
            creator = clients[members[0]["email"].lower()]
            group = creator.post(f"/api/assessment/essay-assignments/{assignment['id']}/groups", {
                "name": f"Nhóm Backend {index + 1:02d}"
            })
            groups.append(group)
        existing_ids = {m["studentId"] for m in group.get("members", [])}
        for member in members[1:]:
            if member["id"] not in existing_ids:
                teacher.post(f"/api/assessment/essay-groups/{group['id']}/members",
                             {"studentId": member["id"]}, allow={409})
        member_client = clients[members[0]["email"].lower()]
        topic = member_client.get(f"/api/assessment/essay-groups/{group['id']}/topic", allow={404})
        if topic is None:
            topic = member_client.post(f"/api/assessment/essay-groups/{group['id']}/topic",
                                       {"proposedTitle": topics[index]})
        status = topic.get("status")
        if index in {0, 3, 4} and enum_is(status, 1, "pendingreview"):
            topic = teacher.post(f"/api/assessment/essay-topics/{topic['id']}/approve")
        if index == 2 and enum_is(status, 1, "pendingreview"):
            teacher.post(f"/api/assessment/essay-topics/{topic['id']}/request-changes")
        if index == 0:
            topic = teacher.get(f"/api/assessment/essay-groups/{group['id']}/topic")
            if enum_is(topic.get("status"), 3, "approved"):
                teacher.post(f"/api/assessment/essay-topics/{topic['id']}/lock", allow={409})
        if index == 3:
            topic = teacher.get(f"/api/assessment/essay-groups/{group['id']}/topic")
            if enum_is(topic.get("status"), 3, "approved"):
                teacher.post(f"/api/assessment/essay-topics/{topic['id']}/lock", allow={409})
        if index == 4:
            topic = teacher.get(f"/api/assessment/essay-groups/{group['id']}/topic")
            if enum_is(topic.get("status"), 3, "approved"):
                teacher.post(f"/api/assessment/essay-topics/{topic['id']}/lock", allow={409})

        if index in {0, 1, 2, 3, 4}:
            topic = teacher.get(f"/api/assessment/essay-groups/{group['id']}/topic", allow={404})
            submissions = teacher.get(f"/api/assessment/essay-groups/{group['id']}/submissions") or []
            accepted = any(enum_is(value.get("status"), 1, "submitted") or
                           enum_is(value.get("status"), 3, "graded") for value in submissions)
            invalid_title_recorded = any(
                index == 4 and any("canonical topic" in error.lower()
                                   for error in value.get("errors", []))
                for value in submissions)
            needs_upload = (not accepted and not invalid_title_recorded and
                            topic and enum_is(topic.get("status"), 5, "locked"))
            if needs_upload:
                body = (
                    "Mục tiêu: xây dựng một thiết kế có thể mở rộng cho lớp học trực tuyến. "
                    "Kiến trúc: tách Identity, Academics, Assessment, Learning, Communication và Reporting; "
                    "dùng command cho thay đổi và query cho read model. "
                    "Kết luận: thiết kế giữ tenant isolation, validation, audit và khả năng realtime. "
                    "Nhóm phân tích các trade-off của PostgreSQL, Marten, projection, permission và failure isolation. "
                    "Phần triển khai minh họa request flow, kiểm thử permission, cache key theo tenant và cách xử lý reconnect."
                )
                if index == 4:
                    file_title = "Báo cáo chưa đúng chủ đề"
                    # Keep all required sections valid so this fixture isolates
                    # the canonical-topic mismatch error.
                    file_body = body
                elif index == 3:
                    file_title, file_body = topics[index], body
                else:
                    file_title, file_body = topics[index], body
                uploaded = member_client.multipart(
                    f"/api/assessment/essay-assignments/{assignment['id']}/groups/{group['id']}/submissions",
                    {}, f"bao-cao-nhom-{index + 1:02d}.pdf", "application/pdf",
                    pdf_bytes(file_title, file_body), allow={400, 409, 503})
                if uploaded and isinstance(uploaded, dict) and uploaded.get("id"):
                    pass
    return assignment


def ensure_announcements(teacher: ApiClient, class_id: str):
    existing = teacher.get(f"/api/communication/classes/{class_id}/announcements") or []
    specs = [
        ("Tuần này học trực tuyến", "Tuần này lớp học chuyển sang phòng trực tuyến lúc 07:00."),
        ("Quiz C# Fundamentals đã được mở", "Các bạn có thể bắt đầu quiz C# Fundamentals trong mục Bài tập."),
        ("Hạn đăng ký đề tài tiểu luận", "Hạn đăng ký đề tài tiểu luận là ngày 20/09/2026."),
    ]
    for title, content in specs:
        announcement = next((v for v in existing if v.get("title") == title), None)
        if announcement:
            if not enum_is(announcement.get("status"), 1, "published"):
                teacher.post(f"/api/communication/announcements/{announcement['id']}/publish",
                             allow={409})
            continue
        announcement = teacher.post(f"/api/communication/classes/{class_id}/announcements", {
            "title": title, "content": content,
        })
        teacher.post(f"/api/communication/announcements/{announcement['id']}/publish", allow={409})


def ensure_attendance(teacher: ApiClient, class_id: str, students: list[dict],
                      clients: dict[str, ApiClient]):
    def ensure_demo_presence(session_id: str):
        # Event handlers finalize records asynchronously after the session is
        # ended. Retry the read through the teacher API so a fresh setup has
        # the same deterministic result as a rerun.
        for attempt in range(5):
            summary = teacher.get(f"/api/classroom/sessions/{session_id}/attendance") or {}
            records = {value.get("studentId"): value for value in summary.get("records", [])}
            missing_record = False
            for student in students[:2]:
                record = records.get(student["id"])
                if record is None:
                    missing_record = True
                    continue
                if not enum_is(record.get("status"), 0, "present"):
                    teacher.post(f"/api/classroom/sessions/{session_id}/attendance/{student['id']}", {
                        "status": "Present",
                        "reason": "Đã xác nhận log tham gia trong phiên demo.",
                    })
            if not missing_record:
                return
            if attempt < 4:
                time.sleep(0.5)

    schedules = teacher.get(f"/api/academics/classes/{class_id}/schedules?includeHistory=true") or []
    schedule = next((v for v in schedules if str(v.get("startsAt", "")).startswith("2026-09-09T07:00:00")), None)
    if not schedule:
        return
    sessions = teacher.get(f"/api/classroom/classes/{class_id}/sessions?includeEnded=true") or []
    live = next((v for v in sessions if v.get("classScheduleId") == schedule["id"]), None)
    if live and enum_is(live.get("status"), 2, "ended"):
        # Keep the join/leave history created above, then use the same teacher
        # attendance correction endpoint used by the UI to cover the normal
        # "Present" state as well as the naturally late joins. The correction
        # is audited on the record and is idempotent on reruns.
        ensure_demo_presence(live["id"])
        return
    if live is None:
        live = teacher.post(f"/api/classroom/classes/{class_id}/sessions", {
            "classScheduleId": schedule["id"], "teacherId": teacher.get("/api/identity/me")["id"],
            "lateAfterMinutes": 15,
        })
    if enum_is(live.get("status"), 0, "scheduled"):
        live = teacher.post(f"/api/classroom/sessions/{live['id']}/start")
    for index, student in enumerate(students[:18]):
        client = clients[student["email"].lower()]
        client.post(f"/api/classroom/sessions/{live['id']}/join", allow={409})
        if index in {2, 7}:
            client.post(f"/api/classroom/sessions/{live['id']}/leave", allow={409})
            client.post(f"/api/classroom/sessions/{live['id']}/reconnect", allow={409})
        if index != 0:
            client.post(f"/api/classroom/sessions/{live['id']}/leave", allow={409})
    teacher.post(f"/api/classroom/sessions/{live['id']}/end", allow={409})
    time.sleep(1.0)
    ensure_demo_presence(live["id"])


def verify(teacher: ApiClient, student: ApiClient, class_id: str, curriculum_id: str,
           activity_ids: list[str]):
    class_detail = teacher.get(f"/api/academics/classes/{class_id}")
    class_value = class_detail.get("class", class_detail)
    course_content = teacher.get(f"/api/academics/courses/{class_value['courseId']}")
    enrollments = teacher.get(f"/api/academics/classes/{class_id}/enrollments") or []
    curriculum = student.get(f"/api/learning/classes/{class_id}/curriculums/{curriculum_id}")
    published_lessons = [lesson for module in curriculum.get("modules", []) for lesson in module.get("lessons", [])]
    draft_visible = any("Tổng kết xây dựng Backend API" in lesson.get("title", "") for lesson in published_lessons)
    activities = teacher.get(f"/api/assessment/classes/{class_id}/activities") or []
    gradebook = teacher.get(f"/api/assessment/classes/{class_id}/gradebook")
    course_progress = teacher.get(f"/api/learning/classes/{class_id}/course-progress") or {}
    essays = teacher.get(f"/api/assessment/classes/{class_id}/essay-assignments") or []
    resources = teacher.get(f"/api/learning/classes/{class_id}/resources") or []
    progress_entries = course_progress.get("entries", [])
    if (len(enrollments) != 25 or len(published_lessons) != 28 or draft_visible or
            len(progress_entries) < 25):
        raise RuntimeError(
            f"Verification failed: enrollments={len(enrollments)}, lessons={len(published_lessons)}, "
            f"draft_visible={draft_visible}, progress_entries={len(progress_entries)}")
    return {
        "courseModules": len(course_content.get("modules", [])),
        "publishedLessonsForStudent": len(published_lessons),
        "enrollments": len(enrollments),
        "activities": [{"title": v.get("title"), "status": v.get("status"), "questions": len(v.get("questions", []))} for v in activities],
        "essayAssignments": len(essays), "resources": len(resources),
        "gradebookStudents": len((gradebook or {}).get("students", [])),
        "progressEntries": len(progress_entries),
        "progressCompletionPercent": course_progress.get("completionPercent"),
        "activityIds": activity_ids,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", default=os.environ.get("RLMS_BASE_URL", DEFAULT_BASE_URL))
    parser.add_argument("--organization-code", default=ORG_CODE)
    args = parser.parse_args()
    if os.environ.get("RLMS_DEMO_DATA") != "1":
        raise SystemExit("Refusing to run: set RLMS_DEMO_DATA=1 for the local development setup.")
    parsed = urllib.parse.urlparse(args.base_url)
    if parsed.hostname not in {"localhost", "127.0.0.1", "::1"}:
        raise SystemExit("Refusing to run against a non-local host.")
    if args.organization_code != ORG_CODE:
        raise SystemExit("This deterministic runner only manages RLMS-DEMO.")
    token = os.environ.get("RLMS_BOOTSTRAP_TOKEN") or read_local_setting("RLMS_BOOTSTRAP_TOKEN")
    if not token:
        raise SystemExit("RLMS_BOOTSTRAP_TOKEN is required; run scripts/setup-local.py first.")

    print("Bootstrapping or reusing RLMS-DEMO…")
    admin = bootstrap_admin(args.base_url, token)
    teacher_account = ensure_user(admin, TEACHER_EMAIL, "Nguyễn Minh Anh", TEACHER_PASSWORD, "TEACHER")
    student_accounts = [ensure_user(admin, f"student{i:02d}@rlms.local", name,
                                    f"StudentDemo{i:02d}2026!", "STUDENT")
                        for i, name in enumerate(STUDENT_NAMES, start=1)]
    teacher = ApiClient(args.base_url); teacher.login(TEACHER_EMAIL, TEACHER_PASSWORD)
    students = [{**account, "email": f"student{i:02d}@rlms.local",
                 "password": f"StudentDemo{i:02d}2026!"}
                for i, account in enumerate(student_accounts, start=1)]
    clients: dict[str, ApiClient] = {}
    for student in students:
        client = ApiClient(args.base_url); client.login(student["email"], student["password"])
        clients[student["email"].lower()] = client

    course = ensure_course(teacher)
    learning_class = ensure_class(teacher, course["id"], teacher_account["id"])
    class_id = learning_class["class"]["id"] if "class" in learning_class else learning_class["id"]
    ensure_enrollments(teacher, class_id, students)
    curriculum = ensure_curriculum(teacher, course["id"], class_id)
    course_detail, published_lessons = ensure_content(teacher, course)
    ensure_resources(teacher, class_id, published_lessons)
    ensure_learning_progress(students, clients, class_id, curriculum["id"], published_lessons)
    schedules = ensure_schedules(teacher, class_id, teacher_account["id"])

    question_map = ensure_questions(teacher, course["id"])
    prompts = [spec["prompt"] for spec in QUESTIONS]
    activity1 = ensure_activity(teacher, class_id, "Quiz C# Fundamentals",
        "Kiểm tra nhanh nền tảng ngôn ngữ C#.", 2, prompts[0:10], question_map,
        "2026-09-01T00:00:00+07:00", "2026-10-15T23:59:00+07:00")
    activity2 = ensure_activity(teacher, class_id, "Bài tập OOP",
        "Bài tập kết hợp trắc nghiệm và phân tích abstraction.", 1, prompts[5:12], question_map,
        "2026-09-01T00:00:00+07:00", "2026-11-01T23:59:00+07:00")
    activity3 = ensure_activity(teacher, class_id, "Kiểm tra ASP.NET Core",
        "Bài kiểm tra mở để giáo viên quan sát realtime.", 3, prompts[12:22], question_map,
        "2026-09-12T00:00:00+07:00", "2026-10-31T23:59:00+07:00",
        {"attemptLimit": 1, "allowRetake": False, "shuffleQuestions": False, "passingScore": 60})

    simulate_activities(students, clients, activity1, activity2, activity3)
    ensure_rubric_and_grade(teacher, activity2, students)
    ensure_essay(teacher, class_id, students, clients)
    ensure_announcements(teacher, class_id)
    ensure_attendance(teacher, class_id, students, clients)
    time.sleep(1.0)
    result = verify(teacher, clients[students[0]["email"].lower()], class_id,
                    curriculum["id"], [activity1["id"], activity2["id"], activity3["id"]])
    result.update({"organization": ORG_CODE, "classId": class_id,
                   "courseId": course["id"], "curriculumId": curriculum["id"],
                   "scheduleCount": len(schedules), "teacher": TEACHER_EMAIL,
                   "students": [student["email"] for student in students]})
    print(json.dumps(result, ensure_ascii=False, indent=2))
    print("Demo data is ready. Accounts are documented in docs/reference/DEMO_ACCOUNTS.md.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ApiError as error:
        print(f"Demo setup stopped at {error}", file=sys.stderr)
        raise
