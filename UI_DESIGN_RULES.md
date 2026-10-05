# UI_DESIGN_RULES.md

# UI Design Reference Rules — RLMS

## 1. Nguồn thiết kế tham chiếu bắt buộc

Frontend RLMS phải học design language và interaction pattern từ source local:

```text
/Users/dangvyhao/Documents/NLT_Tranning/front-end/nlt-smart-gas
```

Source này là **UI/UX reference source**, không phải business source của RLMS.

Coding Agent chạy trên máy local phải inspect source này trước khi tạo hoặc refactor các thành phần UI chính.

Nếu source local không tồn tại hoặc không đọc được:
- không tự đoán nội dung source;
- vẫn tuân thủ `UX_STRUCTURE.md`;
- giữ design implementation tối thiểu và ghi rõ reference source chưa được inspect.

---

# 2. Thứ tự ưu tiên khi thiết kế UI

```text
GOAL.md
   ↓
UX_STRUCTURE.md
   ↓
UI_DESIGN_RULES.md
   ↓
Inspect nlt-smart-gas
   ↓
Áp dụng design language vào RLMS
```

Nếu reference source mâu thuẫn với nghiệp vụ RLMS:

```text
UX_STRUCTURE.md thắng.
```

Source local quyết định:
- style;
- interaction convention;
- component pattern;
- layout language.

Source local **không quyết định business flow**.

---

# 3. Những gì phải học từ source local

Trước khi tạo UI, phải inspect các phần tương ứng trong `nlt-smart-gas`:

- application shell;
- sidebar;
- nested sidebar/dropdown menu;
- header;
- opened-page tab switcher;
- page container;
- page header;
- breadcrumb nếu source dùng;
- cards;
- tables;
- forms;
- modal/dialog;
- drawer;
- buttons;
- badges/status;
- dropdown;
- search;
- filter;
- pagination;
- loading;
- skeleton;
- empty state;
- error state;
- toast/notification;
- icon convention;
- typography;
- spacing;
- border/radius;
- responsive behavior;
- dark/light/theme convention nếu source có.

Không tạo một design system hoàn toàn khác nếu source đã có pattern tương ứng.

---

# 4. Không copy business logic từ source

Không copy hoặc mang sang RLMS:

- API endpoint của Smart Gas;
- domain model Smart Gas;
- role/permission Smart Gas;
- route nghiệp vụ Smart Gas;
- store nghiệp vụ Smart Gas;
- business service Smart Gas;
- entity/model Smart Gas;
- text/data nghiệp vụ Smart Gas.

Chỉ học:

```text
Visual Pattern
+
Layout Pattern
+
Interaction Pattern
+
Component Convention
```

rồi map sang domain RLMS.

---

# 5. Sidebar — nguyên tắc bắt buộc

Sidebar RLMS phải học pattern sidebar của source local, đặc biệt:
- group menu;
- nested/dropdown menu;
- active state;
- icon;
- collapse/expand;
- spacing;
- section hierarchy.

Nhưng nội dung menu phải theo đúng role/workspace của RLMS.

Không dùng một sidebar chứa toàn bộ chức năng rồi chỉ ẩn ngẫu nhiên từng item.

Sidebar phải phản ánh **nghiệp vụ của người đang sử dụng**.

---

# 6. Sidebar cho Student

Ví dụ cấu trúc nghiệp vụ:

```text
Student Workspace

Trang chủ

Học tập
├── Khóa học / Lớp học
├── Lịch học
└── Tiến độ

Đánh giá
├── Bài tập
├── Bài thi
├── Tiểu luận
└── Điểm số

Cá nhân
└── Thông báo
```

Nếu source local dùng dropdown/collapsible group:
- áp dụng đúng interaction đó;
- giữ active child;
- parent group phải thể hiện active khi child đang active.

Không thêm menu teacher/admin cho student.

---

# 7. Sidebar cho Teacher

Đây là yêu cầu đặc biệt phải bám source local.

Teacher sidebar phải nhóm các chức năng theo nghiệp vụ và hỗ trợ dropdown giống pattern trong `nlt-smart-gas`.

Ví dụ:

```text
Teacher Workspace

Tổng quan

Giảng dạy
├── Lớp giảng dạy
├── Khóa học
└── Lịch giảng

Đánh giá
├── Bài tập
├── Bài thi
├── Tiểu luận
├── Chấm bài
└── Sổ điểm

Theo dõi
├── Tiến độ lớp
└── Báo cáo

Giao tiếp
└── Thông báo
```

Khi teacher mở:

```text
Giảng dạy
```

sidebar dropdown ra các chức năng con theo đúng interaction pattern của source.

Ví dụ:

```text
▾ Giảng dạy
   ├─ Lớp giảng dạy
   ├─ Khóa học
   └─ Lịch giảng
```

Không flatten tất cả thành 10–15 menu ngang hàng nếu source đã có hierarchy rõ.

---

# 8. Sidebar cho Admin

Ví dụ:

```text
Admin Workspace

Tổng quan

Hệ thống
├── Tổ chức
├── Người dùng
└── Vai trò & Quyền

Học vụ
├── Khóa học
├── Lớp học
└── Lịch

Vận hành
├── Báo cáo
└── Cấu hình
```

Admin sidebar không mặc định chứa:
- Student Detail realtime;
- Chấm bài;
- Teacher Live Workspace;

trừ khi UX/business permission sau này định nghĩa rõ.

---

# 9. Sidebar behavior

Phải học behavior thực tế của source trước khi implement.

Các behavior cần giữ nếu source có:

- collapse toàn sidebar;
- expand/collapse group;
- active route highlight;
- parent active state;
- tooltip khi sidebar collapsed;
- icon alignment;
- scroll độc lập;
- remembered expansion state nếu implementation source làm như vậy và thực sự hữu ích.

Không tự thêm nhiều state persistence phức tạp nếu source không có hoặc UX không cần.

---

# 10. Header — Browser-like Opened Tabs

RLMS phải học header/tab-switch pattern của source local.

Mục tiêu:

> Người dùng mở nhiều trang chức năng trong RLMS và có thể chuyển nhanh giữa các trang đang mở giống các tab trong trình duyệt.

Ví dụ:

```text
┌──────────────────────────────────────────────────────┐
│ Sidebar │ Dashboard | SE01 | Assignment 05 | Gradebook ×
└──────────────────────────────────────────────────────┘
```

Tab hiện tại:

```text
Dashboard
SE01
Assignment 05   ← active
Gradebook
```

Không dùng browser tab thật.
Đây là **application-level opened page tabs**.

---

# 11. Opened Tab model

Một opened tab tối thiểu cần:

```text
key
title
route
closable
```

Có thể có:
- icon;
- route params;

nếu source pattern thật sự cần.

Không lưu cả component instance hoặc full page state vào tab model.

---

# 12. Khi mở trang mới

Ví dụ teacher click:

```text
Lớp giảng dạy
→ SE01
```

Header:

```text
Dashboard | Lớp giảng dạy | SE01
```

Nếu mở `SE01` lần nữa:
- activate tab hiện có;
- không tạo duplicate tab cùng identity.

Tab identity phải dựa trên route + business key phù hợp.

Ví dụ:

```text
/classes/{classId}
```

hai class khác nhau có thể là hai tab khác nhau.

---

# 13. Khi chuyển tab

Switch tab phải:
- đổi route;
- hiển thị đúng page;
- giữ active state rõ;
- không reload toàn application shell.

Nếu page data cần refresh thì theo data rule của feature, không phải cứ switch tab là reload toàn bộ.

---

# 14. Đóng tab

Tab chức năng thông thường nên có close action nếu source dùng pattern này.

Behavior:

```text
Close active tab
    ↓
activate nearest valid remaining tab
```

Không được để application rơi vào route không hợp lệ.

Các tab root như Dashboard có thể:
- non-closable;

nếu source local cũng dùng pattern tương tự.

---

# 15. Optional Tab actions

Chỉ triển khai nếu source local đã có hoặc UX thật sự cần:

```text
Close
Close Others
Close Tabs to Right
Close All
```

Không tự tạo context menu phức tạp chỉ vì browser có.

---

# 16. Tab persistence

Nếu source local persist opened tabs sau refresh và implementation không quá phức tạp:
- có thể tái sử dụng pattern đó.

Nếu không:
- session-only tabs là đủ cho MVP.

Không biến opened-tab persistence thành business state trong backend.

Đây là frontend UI state.

---

# 17. Route change và Tab sync

Phải đồng bộ:

```text
Sidebar
   ↕
Router
   ↕
Opened Tabs
```

Khi route đổi:
- sidebar active đúng;
- tab active đúng.

Khi click tab:
- route đổi đúng;
- sidebar phản ánh route.

Không tạo ba nguồn state độc lập dễ lệch nhau.

Router là nguồn sự thật hợp lý cho current page.

Opened tabs là navigation history/UI state.

---

# 18. Dynamic Tab Titles

Tab title phải dễ hiểu.

Đúng:

```text
SE01
Assignment 05
Nguyễn Văn Hào
Gradebook
```

Không:

```text
Class Detail
Activity Detail
Student Detail
```

nếu người dùng đang mở nhiều object cùng loại.

Ví dụ teacher mở Student Detail theo UI overlay/panel trong Teacher Live Workspace thì **không nhất thiết tạo tab cho từng student**.

Phải theo UX:
- Student Detail realtime nằm trong Teacher Live Workspace;
- không biến mỗi student click thành một browser-like header tab nếu làm trải nghiệm rối.

---

# 19. Live Classroom và Opened Tabs

Live Classroom là workspace đặc biệt.

Khi mở:

```text
SE01 Live
```

có thể xuất hiện như một opened application tab.

Nhưng bên trong:

```text
Class Overview
Student Detail
Chat
Participants
```

là internal workspace state/panel.

Không tạo header tab mới cho:
- mỗi question;
- mỗi student đang inspect;
- chat;
- participant panel.

Nếu teacher switch Student A → B:
- vẫn ở cùng `SE01 Live` tab;
- chỉ đổi Student Detail subscription.

---

# 20. Layout Shell

Application shell cần học source local:

```text
┌─────────────┬──────────────────────────────────────┐
│             │ Header / Opened Tabs                 │
│   Sidebar   ├──────────────────────────────────────┤
│             │                                      │
│             │ Page Content                         │
│             │                                      │
└─────────────┴──────────────────────────────────────┘
```

Không tự thiết kế shell mới khác hẳn source nếu source đã phù hợp.

Live Classroom có thể dùng variant:

```text
LiveClassroomLayout
```

để ưu tiên viewport.

---

# 21. Role Workspace Switching

Nếu một user có nhiều capability/role (ví dụ vừa Admin vừa Teacher):
- không trộn tất cả menu vào một sidebar khổng lồ.

Nếu product cần switch workspace:
- dùng workspace switcher/profile menu theo design source nếu pattern phù hợp;
- khi đổi workspace, menu thay theo workspace.

Không tự implement workspace switch nếu GOAL chưa yêu cầu user multi-role UX cụ thể.
Có thể giữ permission-aware menu trước.

---

# 22. Page header

Page header phải học source local:
- title;
- subtitle;
- primary action;
- secondary action;
- filter/search placement.

Ví dụ teacher page:

```text
Lớp giảng dạy                         [+ Tạo lớp]
Quản lý các lớp bạn đang phụ trách
```

Không để mỗi page tự phát minh header style.

---

# 23. Table / Data List

Các trang:
- users;
- classes;
- assignments;
- submissions;
- gradebook;
- essay groups;

phải tái sử dụng table/list pattern từ source khi phù hợp.

Giữ:
- density;
- action placement;
- filter;
- pagination;
- status badges.

Không copy column/business field của Smart Gas.

---

# 24. Status language

Status của RLMS phải dùng business vocabulary của RLMS.

Ví dụ:

```text
Draft
Published
Scheduled
In Progress
Completed
Pending Review
Approved
Changes Requested
Submitted
Graded
```

Design badge/color có thể học source.

Business status không được lấy từ Smart Gas.

---

# 25. Form Pattern

Form RLMS phải học:
- label;
- validation;
- input grouping;
- action footer;
- modal/drawer/page form convention;

từ source.

Nhưng workflow dài như:
- tạo Exam;
- tạo Essay Assignment;
- Gradebook setup;

không nên ép vào modal nhỏ nếu source modal pattern không phù hợp.

UX requirement thắng visual imitation.

---

# 26. Realtime visual states

Source local có thể không có realtime classroom state.

Khi phải tạo mới, vẫn dùng design tokens/component language từ source.

Các state cần có:
- Online;
- Offline;
- Working;
- Idle;
- Completed;
- Needs Help;
- Reconnecting.

Không dùng animation nặng chỉ để trông “realtime”.

---

# 27. Loading / Skeleton

Phải inspect source:
- spinner;
- skeleton;
- table loading;
- page loading.

Áp dụng thống nhất.

Student Detail switch A → B:
- có loading state cục bộ;
- không block cả Teacher Live Workspace.

---

# 28. Error pattern

Error phải theo source visual pattern nhưng message theo RLMS business.

Ví dụ:

```text
Không thể tải chi tiết bài làm.
Tổng quan lớp vẫn đang hoạt động.
[Thử lại]
```

Failure isolation phải thể hiện cả trong UI.

---

# 29. Responsive behavior

Phải học breakpoint/layout source trước.

Tuy nhiên:
- Student workspace cần responsive tốt;
- Teacher Live Workspace desktop/laptop-first;
- Admin desktop-first.

Không ép Teacher Live Workspace thành mobile-first nếu làm hỏng usability.

---

# 30. Component Reuse Strategy

Trước khi tạo component mới:

1. inspect source local;
2. tìm component/pattern tương tự;
3. xác định component có phù hợp RLMS không;
4. adapt theo RLMS;
5. chỉ tạo mới khi không có pattern phù hợp.

Không copy nguyên directory nếu mang theo dependency/business không cần.

---

# 31. Design Tokens

Nếu source có centralized:
- color tokens;
- spacing;
- typography;
- radius;
- shadow;
- theme;

phải ưu tiên reuse/adapt.

Không hard-code một palette mới khắp RLMS.

Nếu source chưa token hóa tốt:
- không bắt buộc refactor source reference;
- chỉ tạo token tối thiểu cần cho RLMS.

---

# 32. Icons

Theo icon library/convention của source local nếu phù hợp.

Không mix nhiều icon library chỉ vì thiếu 1 icon.

Nếu cần icon mới:
- ưu tiên cùng library.

---

# 33. Sidebar + Header là Application Shell Contract

Hai phần sau phải được xem là core design contract:

```text
Role-aware Nested Sidebar
        +
Opened-page Header Tabs
```

Không được một feature tự thay sidebar/header behavior.

Chỉ `app/layouts` / application shell sở hữu behavior này.

Feature chỉ cung cấp:
- route;
- title;
- navigation metadata;
- permission metadata nếu cần.

---

# 34. Frontend structure mapping

Theo `PROJECT_STRUCTURE.md`:

```text
frontend/src/
├── app/
│   ├── router/
│   ├── layouts/
│   └── providers/
│
├── features/
└── shared/
```

Các component shell tham chiếu source nên nằm hợp lý tại:

```text
app/layouts/
shared/ui/
```

Không copy Smart Gas structure 1:1 nếu không phù hợp structure RLMS đã khóa.

---

# 35. Agent workflow khi làm frontend

Trước mỗi task UI:

```text
Read UX_STRUCTURE.md
    ↓
Read UI_DESIGN_RULES.md
    ↓
Inspect relevant source in nlt-smart-gas
    ↓
Find existing pattern
    ↓
Map pattern → RLMS business use case
    ↓
Implement
    ↓
Compare visually/behaviorally with reference
    ↓
Verify responsive + permission + routing
```

---

# 36. Không được làm

Không:
- tự thay toàn bộ UI library;
- tự đổi sidebar style;
- bỏ nested sidebar vì “đơn giản hơn”;
- bỏ opened tabs;
- tạo design khác nhau cho từng feature;
- copy Smart Gas business naming;
- copy source API/business logic;
- tạo mỗi student detail thành header tab;
- fetch dữ liệu detail chỉ để giữ tab state;
- thêm animation/layout nặng làm ảnh hưởng realtime.

---

# 37. Quy tắc cuối

> RLMS phải có cảm giác cùng một design family với `nlt-smart-gas`, nhưng nghiệp vụ, information architecture và realtime learning flow phải hoàn toàn đúng với RLMS.

Hai pattern đặc biệt bắt buộc học kỹ từ source:

```text
1. Sidebar phân nhóm nghiệp vụ theo role với nested/dropdown menu.
2. Header quản lý các trang đang mở bằng application tabs giống trải nghiệm browser tabs.
```
