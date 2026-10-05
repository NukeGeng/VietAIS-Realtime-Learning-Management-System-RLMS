# GIT_RULES.md

# Git Rulebase — RLMS

## 1. Mục tiêu

Tài liệu này quy định cách quản lý branch, commit, pull request và merge cho dự án RLMS.

Mục tiêu:
- branch dễ hiểu;
- không commit quá vụn;
- không sửa trực tiếp `main` / `develop`;
- mỗi module/feature có phạm vi rõ;
- mọi thay đổi đi qua review;
- branch cũ phải được dọn;
- không biến Git history thành nơi chứa thử nghiệm hỗn loạn.

---

# 2. Branch chính

Dự án sử dụng hai branch chính:

```text
main
  ↓
develop
  ↓
module/(fe-be-ai)-noi-dung
```

## `main`

`main` là branch ổn định nhất.

Chỉ chứa code:
- đã được tích hợp qua `develop`;
- đã verify;
- đủ điều kiện release/deploy.

Không code trực tiếp trên `main`.

Không push trực tiếp vào `main`.

---

## `develop`

`develop` là branch tích hợp chính của quá trình phát triển.

Tất cả module/feature branch phải:
- tạo từ `develop`;
- merge trở lại `develop`.

Không dùng `main` làm base cho feature/module branch thông thường.

---

# 3. Quy ước branch module

Format bắt buộc:

```text
module/(fe-be-ai)-noi-dung
```

Ví dụ:

```text
module/fe-student-dashboard
module/fe-teacher-live-workspace

module/be-course-management
module/be-student-activity
module/be-essay-validation

module/ai-essay-review
```

Lưu ý:

`module/ai-*` hiện tại chỉ được phép tồn tại khi GOAL tương lai mở scope AI.

Trong GOAL hiện tại:
- không tạo branch AI Essay Review;
- không implement RAG/AI grading.

---

# 4. Ý nghĩa prefix

## `fe`

Frontend.

Ví dụ:

```text
module/fe-classroom-overview
module/fe-gradebook
module/fe-essay-topic
```

## `be`

Backend.

Ví dụ:

```text
module/be-live-session
module/be-student-activity
module/be-gradebook
```

## `ai`

AI feature.

Ví dụ tương lai:

```text
module/ai-essay-review
```

Chỉ dùng khi feature AI được GOAL chính thức bật.

Không dùng `ai` để đặt tên cho code không phải AI.

---

# 5. Naming rule

Tên branch:
- lowercase;
- dùng dấu `-`;
- ngắn nhưng đủ rõ nghĩa;
- mô tả business/module đang làm.

Đúng:

```text
module/be-student-activity
module/fe-class-overview
module/be-essay-topic-validation
```

Không:

```text
module/backend-abc
module/fix123
module/test
module/hao-work
module/new-feature
module/be-do-something
```

---

# 6. Flow chuẩn

```text
main
 │
 ▼
develop
 │
 ├── module/be-course-management
 │
 ├── module/be-student-activity
 │
 ├── module/fe-classroom
 │
 └── module/fe-gradebook
```

Flow:

```text
develop
  ↓
create module branch
  ↓
implement
  ↓
build/test
  ↓
Draft PR → develop
  ↓
review diff
  ↓
fix/review
  ↓
submit ready PR
  ↓
merge → develop
  ↓
delete module branch
```

---

# 7. Tạo branch

Trước khi tạo branch:

```bash
git checkout develop
git pull
```

Sau đó:

```bash
git checkout -b module/be-student-activity
```

Branch phải bắt đầu từ trạng thái `develop` mới nhất hợp lý.

Không tạo branch mới từ một feature branch khác trừ khi dependency thật sự yêu cầu và đã được xác định rõ.

---

# 8. Scope của một branch

Một branch phải đại diện cho một module/feature hoặc một thay đổi logic rõ.

Ví dụ tốt:

```text
module/be-essay-validation
```

bao gồm:
- file validator;
- text extraction;
- topic validation;
- tests liên quan.

Không nên một branch chứa:

```text
Essay validation
+
Gradebook UI
+
Redis refactor
+
Course API
```

trừ khi đây thật sự là một thay đổi thống nhất không thể tách.

---

# 9. Commit Rule

Không commit quá vụn.

Không:

```text
fix
fix again
oops
update
update 2
test
test2
```

Không cần commit sau mỗi file.

Commit khi hoàn thành một thay đổi logic nhỏ nhưng có nghĩa.

Ví dụ:

```text
feat(classroom): add student activity overview projection
feat(essay): validate canonical topic before submission
fix(realtime): unsubscribe previous student detail channel
test(assessment): cover objective grading rules
```

---

# 10. Commit format

Khuyến nghị:

```text
type(scope): description
```

Type:

```text
feat
fix
refactor
test
docs
chore
perf
```

Ví dụ:

```text
feat(course): add course publishing flow
feat(classroom): add live session join flow
fix(realtime): prevent stale student detail update
perf(classroom): reuse live session bootstrap snapshot
test(essay): add empty report validation cases
docs(domain): update student activity ownership
```

Không bắt buộc Conventional Commits đầy đủ nếu repo sau này dùng convention khác, nhưng commit phải rõ nghĩa.

---

# 11. Không commit build artifact / secret

Không commit:
- `.env` có secret;
- password;
- API key;
- local database credential;
- build output không cần;
- `bin/`;
- `obj/`;
- `node_modules/`;
- local cache;
- generated temporary files.

Phải có `.gitignore` đúng.

---

# 12. Draft PR bắt buộc

Khi module đã có đủ phần chính:

```text
module/... → develop
```

phải mở **Draft Pull Request**.

Không đợi tới lúc code hoàn toàn xong mới mở PR nếu module đủ lớn để cần review sớm.

Draft PR dùng để:
- xem diff;
- kiểm tra scope;
- phát hiện thay đổi ngoài module;
- kiểm tra architecture;
- theo dõi test/build;
- review trước khi merge.

---

# 13. PR title

Ví dụ:

```text
[BE] Student Activity realtime flow
[FE] Teacher Live Workspace
[BE] Essay report validation
[FE] Gradebook interface
```

Hoặc theo conventional style của repository nếu sau này có.

---

# 14. PR description

PR phải trả lời tối thiểu:

```text
What
- Làm chức năng gì?

Why
- Business use case nào?

Scope
- Module nào bị thay đổi?

Verification
- Build?
- Tests?
- Smoke test?

Architecture
- Có thay domain/event/contract không?
- Có ảnh hưởng realtime/performance không?

Docs
- Có cần cập nhật docs không?
```

---

# 15. Trước khi chuyển Draft → Ready

Phải kiểm tra:

- code đúng GOAL;
- không làm Coming Soon;
- build pass;
- test liên quan pass;
- smoke test pass;
- review diff;
- không có file ngoài scope vô tình bị thay;
- không có secret;
- không disable test;
- không xóa validation để pass;
- docs cập nhật nếu architecture/domain/UX thay đổi.

---

# 16. Review diff bắt buộc

Trước khi submit PR:

```bash
git diff develop...HEAD
```

hoặc review trực tiếp trên PR.

Phải kiểm tra:
- file nào thay đổi;
- có file không liên quan không;
- có generated file không;
- có debug code không;
- có commented-out code lớn không;
- có temporary workaround không.

---

# 17. Merge về `develop`

Chỉ merge khi:
- PR Ready;
- review hoàn thành;
- CI/build/test đạt;
- scope đúng;
- không có conflict chưa xử lý.

Không merge branch lỗi chỉ vì “để develop rồi sửa sau”.

---

# 18. Merge strategy

Ưu tiên lịch sử dễ đọc.

Có thể dùng:
- Squash Merge cho module nhỏ/vừa;
- Merge commit nếu module có nhiều commit logic cần giữ.

Không bắt buộc giữ hàng chục commit vụn.

Nếu branch có history xấu:
- clean/rebase/squash trước khi merge nếu cần.

Không rewrite shared branch history tùy tiện.

---

# 19. `develop` → `main`

Không merge từng module trực tiếp vào `main`.

Flow release:

```text
module/*
   ↓
develop
   ↓
verify integrated system
   ↓
main
```

Chỉ đưa `develop` vào `main` khi:
- integration ổn;
- test phù hợp pass;
- release scope rõ;
- không còn lỗi blocker đã biết.

---

# 20. Hotfix

Nếu production/main có lỗi nghiêm trọng, có thể dùng:

```text
hotfix/noi-dung
```

Ví dụ:

```text
hotfix/student-submit-loss
```

Flow:

```text
main
 ↓
hotfix/*
 ↓
main
```

Sau đó phải đồng bộ fix trở lại:

```text
develop
```

để không mất hotfix ở nhánh phát triển.

Không dùng hotfix cho feature thông thường.

---

# 21. Branch cleanup

Sau khi PR merge:

```text
delete branch
```

Cả remote và local nếu không còn dùng.

Không giữ:
- branch đã merge;
- branch thử nghiệm cũ;
- branch lỗi thời;
- branch không còn owner;
- branch lệch develop quá lâu.

---

# 22. Branch quá lỗi thời

Nếu branch:
- tồn tại lâu;
- conflict quá nhiều;
- architecture đã thay đổi;
- scope đã đổi lớn;

không cố cứu bằng hàng loạt merge rối.

Đánh giá:

```text
branch hiện tại
   ↓
còn hợp scope?
   ↓
Yes -> rebase/merge develop phù hợp
No  -> bỏ branch, tạo branch mới sạch
```

Không ngại bỏ branch lỗi thời nếu giữ nó gây rủi ro lớn hơn.

---

# 23. Không làm nhiều module trên một branch lâu dài

Không dùng:

```text
module/be-all-backend
module/fe-everything
```

cho toàn bộ dự án.

Mục tiêu branch là review được.

Module lớn có thể chia theo chức năng nếu cần:

```text
module/be-student-activity
module/be-realtime-overview
module/be-student-detail
```

Nhưng không chia vụn vô nghĩa.

---

# 24. FE / BE dependency

Nếu một feature cần cả frontend và backend:

Có thể dùng hai branch:

```text
module/be-essay-topic
module/fe-essay-topic
```

Cả hai merge độc lập về `develop`.

Không bắt buộc một PR chứa cả FE và BE nếu khiến diff quá lớn.

Nếu contract liên quan:
- backend contract phải rõ;
- frontend bám contract;
- cập nhật contract/docs/test phù hợp.

---

# 25. Cross-module change

Nếu task bắt buộc thay nhiều module:
- giữ scope trong một PR nếu thay đổi là atomic/business-consistent;
- hoặc tách nhiều PR có thứ tự dependency rõ.

Không tách chỉ để đạt “mỗi PR một folder”.

Không gom chỉ để “đỡ tạo PR”.

Ưu tiên reviewability + correctness.

---

# 26. Git và WORKFLOW

Git workflow không thay thế `WORKFLOW.md`.

Trước khi PR:
- feature phải đạt Definition of Done;
- Event Storming/business flow đúng;
- test đúng;
- architecture boundary đúng.

Git chỉ quản lý cách thay đổi được đưa vào codebase.

---

# 27. Git và AGENTS

Coding Agent phải:
- xác định branch đúng trước khi code nếu workflow có Git access;
- không commit ngoài scope;
- không push trực tiếp `main`;
- không push trực tiếp `develop` cho feature;
- review diff trước PR;
- không tự merge nếu workflow/permission không yêu cầu.

---

# 28. Sơ đồ cuối

```text
                           main
                            ▲
                            │
                     release/integration
                            │
                         develop
                            ▲
            ┌───────────────┼────────────────┐
            │               │                │
module/be-student     module/fe-class   module/be-essay
-activity              -workspace        -validation
```

AI tương lai:

```text
develop
   ▲
   │
module/ai-essay-review
```

chỉ khi GOAL mới cho phép.

---

# 29. Quy tắc cuối

> Branch phải phản ánh một thay đổi có thể hiểu, review, test và merge độc lập ở mức hợp lý.

Không tối ưu Git workflow để có nhiều branch/commit/PR nhất.

Mục tiêu là:
- lịch sử sạch;
- review dễ;
- scope rõ;
- code ổn định;
- develop luôn ở trạng thái tích hợp được.
