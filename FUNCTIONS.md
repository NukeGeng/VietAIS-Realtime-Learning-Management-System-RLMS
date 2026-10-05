# DANH SÁCH CHỨC NĂNG HỆ THỐNG HỌC TRỰC TUYẾN

## 1. Tài khoản & Tổ chức

### Quản lý tài khoản
// Quản lý thông tin người dùng trong hệ thống bao gồm tạo tài khoản, cập nhật thông tin, khóa/mở khóa tài khoản và quản lý trạng thái hoạt động.

### Quản lý giảng viên
// Quản lý hồ sơ giảng viên, thông tin chuyên môn, các lớp và khóa học đang phụ trách.

### Quản lý học viên / sinh viên
// Quản lý hồ sơ học viên, thông tin học tập, lớp đang tham gia và trạng thái học tập.

### Quản lý tổ chức
// Quản lý trường học, trung tâm đào tạo, doanh nghiệp hoặc đơn vị sử dụng hệ thống.

### Quản lý vai trò và quyền
// Quản lý các vai trò trong hệ thống và quyền được phép thực hiện đối với từng nhóm người dùng.

---

## 2. Quản lý Khóa học

### Quản lý khóa học
// Tạo, cập nhật, lưu trữ và quản lý thông tin tổng thể của một khóa học.

### Quản lý chương / module
// Chia khóa học thành các chương hoặc module để tổ chức nội dung học tập theo cấu trúc rõ ràng.

### Quản lý bài học
// Quản lý nội dung từng bài học, thứ tự học, tài liệu liên quan và trạng thái xuất bản.

### Quản lý nội dung khóa học
// Sắp xếp bài học, bài tập, tài liệu và các hoạt động học tập theo đúng lộ trình của khóa học.

### Xuất bản khóa học
// Cho phép giảng viên hoặc người quản lý đưa khóa học từ trạng thái nháp sang trạng thái có thể sử dụng.

### Sinh viên xem nội dung khóa học theo lớp
// Sinh viên mở nội dung từ `/classes/:classId/content` sau khi vào lớp. Hệ thống
// chỉ trả về Course, module và lesson đã xuất bản mà sinh viên được phép xem;
// module/lesson được sắp xếp theo Order và trạng thái được lấy từ tiến độ bài học
// của chính sinh viên.

### Xem chi tiết bài học theo yêu cầu
// Khi sinh viên mở một lesson, hệ thống tải riêng nội dung, video và metadata tài
// nguyên đã xuất bản của lesson đó, không tải toàn bộ nội dung chi tiết trong
// danh sách module ban đầu.

### Ghi nhận tiến độ bài học
// Mở bài tạo trạng thái Đang học khi chưa có tiến độ; sinh viên có thể đánh dấu
// hoàn thành. Tiến độ khóa học được tính từ số lesson đã hoàn thành trên tổng số
// lesson đã xuất bản và không cho phép lùi từ Đã hoàn thành về Đang học.

---

## 3. Quản lý Lớp học

### Quản lý lớp học
// Tạo và quản lý một lớp học cụ thể gắn với khóa học hoặc chương trình đào tạo.

### Quản lý danh sách học viên
// Quản lý danh sách học viên đang tham gia lớp, thêm hoặc loại học viên khỏi lớp.

### Ghi danh học viên
// Thực hiện việc đăng ký hoặc phân học viên vào một lớp học.

### Phân công giảng viên
// Gán một hoặc nhiều giảng viên phụ trách lớp học.

### Quản lý trạng thái lớp học
// Quản lý các trạng thái như chuẩn bị, đang hoạt động, tạm dừng hoặc kết thúc lớp.

---

## 4. Lịch học & Lịch đào tạo

### Tạo lịch học
// Tạo lịch cho một buổi học cụ thể bao gồm ngày, giờ bắt đầu, giờ kết thúc, lớp và giảng viên phụ trách.

### Quản lý lịch học lặp lại
// Hỗ trợ tạo lịch học lặp theo ngày, tuần hoặc chu kỳ xác định.

### Thay đổi lịch học
// Cho phép cập nhật ngày hoặc thời gian của một buổi học đã được lên lịch.

### Hủy lịch học
// Hủy một buổi học đã được lên lịch và cập nhật trạng thái liên quan.

### Quản lý lịch thi / kiểm tra
// Quản lý thời gian tổ chức các bài kiểm tra hoặc kỳ thi trong lớp học.

### Xem lịch cá nhân
// Cho phép giảng viên và học viên xem lịch học, lịch kiểm tra và các hoạt động sắp tới của mình.

---

## 5. Lớp học Trực tuyến

### Tạo phòng học trực tuyến
// Tạo không gian học trực tuyến cho một buổi học đã được lên lịch hoặc được mở trực tiếp bởi giảng viên.

### Mở lại phòng học trực tuyến đang hoạt động
// Liệt kê các phiên chưa kết thúc để giảng viên hoặc học viên có thể quay lại đúng phòng học.

### Bắt đầu buổi học
// Cho phép giảng viên bắt đầu một phiên học trực tuyến.

### Kết thúc buổi học
// Kết thúc phiên học trực tuyến và ghi nhận thời gian kết thúc của buổi học.

### Tham gia buổi học
// Cho phép học viên và giảng viên tham gia phòng học trực tuyến.

### Rời buổi học
// Ghi nhận khi người tham gia rời khỏi phòng học.

### Quản lý người tham gia
// Hiển thị và quản lý danh sách người đang có mặt trong buổi học.

### Video và âm thanh
// Hỗ trợ truyền hình ảnh và âm thanh trực tiếp giữa giảng viên và học viên.

### Chia sẻ màn hình
// Cho phép giảng viên hoặc người được cấp quyền chia sẻ màn hình trong buổi học.
// Mỗi phiên chỉ có một presenter; học viên phải chờ khi đang có người trình bày và giảng viên có thể tiếp quản sau khi xác nhận.

### Trò chuyện trong lớp
// Cho phép người tham gia gửi tin nhắn trong thời gian buổi học đang diễn ra.

### Giơ tay phát biểu
// Cho phép học viên gửi tín hiệu yêu cầu phát biểu hoặc tương tác với giảng viên.

### Quản lý trạng thái hiện diện
// Theo dõi người dùng đang online, đã rời lớp hoặc mất kết nối trong buổi học.

### Điều khiển người tham gia theo phiên
// Giáo viên có thể kick, chặn hoặc tắt mic của một học viên trong đúng phiên live hiện tại.

---

## 6. Điểm danh & Chuyên cần

### Điểm danh tự động
// Ghi nhận sự có mặt của học viên dựa trên thời điểm tham gia buổi học trực tuyến.

### Ghi nhận thời gian tham gia
// Theo dõi thời gian học viên tham gia và rời khỏi từng buổi học.

### Ghi nhận đi muộn
// Xác định học viên tham gia sau ngưỡng đi muộn được cấu hình cho phiên học.

### Lịch sử reconnect
// Lưu từng lần vào/rời, thời lượng đã học, khoảng gián đoạn và số lần vào lại trong phiên.

### Ghi nhận vắng mặt
// Đánh dấu học viên không tham gia buổi học theo lịch.

### Điều chỉnh điểm danh
// Cho phép giảng viên chỉnh sửa trạng thái điểm danh trong các trường hợp đặc biệt.

### Theo dõi tỷ lệ chuyên cần
// Tổng hợp số buổi tham gia, vắng, đi muộn và tỷ lệ chuyên cần của học viên.

---

## 7. Hoạt động Học tập

### Tạo bài tập
// Cho phép giảng viên tạo bài tập để học viên thực hiện trong hoặc ngoài buổi học.

### Tạo bài luyện tập
// Tạo các hoạt động luyện tập không nhất thiết ảnh hưởng trực tiếp đến điểm tổng kết.

### Tạo bài quiz
// Tạo bài kiểm tra ngắn gồm một hoặc nhiều câu hỏi.

### Quản lý câu hỏi
// Tạo và quản lý các câu hỏi thuộc bài tập, quiz hoặc bài kiểm tra.

### Giao bài cho lớp
// Giao một hoạt động học tập cho toàn bộ hoặc một nhóm học viên trong lớp.

### Giao bài trực tiếp trong buổi học
// Cho phép giảng viên phát hành một bài tập ngay khi lớp học trực tuyến đang diễn ra.

### Thiết lập thời gian làm bài
// Xác định thời gian bắt đầu, thời hạn hoặc giới hạn thời gian thực hiện một hoạt động.

### Tạm dừng hoạt động
// Cho phép giảng viên tạm dừng bài tập đang được thực hiện trong lớp để giải thích hoặc hướng dẫn thêm.

### Tiếp tục hoạt động
// Cho phép học viên tiếp tục thực hiện bài tập sau khi giảng viên mở lại hoạt động.

### Đóng hoạt động
// Kết thúc quyền làm hoặc nộp bài của học viên đối với một hoạt động.

---

## 8. Quá trình Học tập Realtime

### Bắt đầu làm bài
// Ghi nhận thời điểm học viên bắt đầu thực hiện một bài tập, quiz hoặc hoạt động học tập.

### Theo dõi câu hỏi hiện tại
// Ghi nhận học viên hiện đang xem hoặc thực hiện câu hỏi nào.

### Theo dõi tiến độ làm bài
// Tính toán và cập nhật số câu đã hoàn thành, số câu còn lại và phần trăm tiến độ của học viên.

### Theo dõi trạng thái làm bài
// Ghi nhận học viên đang làm bài, tạm ngừng, đã hoàn thành hoặc chưa bắt đầu.

### Ghi nhận hoạt động gần nhất
// Lưu thời điểm tương tác gần nhất để hệ thống có thể xác định trạng thái hiện tại.

### Phát hiện trạng thái không hoạt động
// Xác định học viên không có tương tác trong một khoảng thời gian nhất định.

### Tiếp tục sau khi không hoạt động
// Ghi nhận khi học viên quay lại tiếp tục làm bài sau thời gian không tương tác.

### Theo dõi tiến độ toàn lớp
// Tổng hợp trạng thái và tiến độ của tất cả học viên đang thực hiện cùng một hoạt động.

### Bảng theo dõi realtime cho giảng viên
// Hiển thị cho giảng viên học viên nào đang làm bài, đang ở câu nào, tiến độ bao nhiêu và ai đang gặp khó khăn.

### Bản đồ tiến độ câu hỏi
// Hiển thị dạng tổng hợp giúp giảng viên nhận biết nhiều học viên đang tập trung hoặc gặp khó khăn tại cùng một câu hỏi.

### Phát hiện học viên gặp khó khăn
// Xác định học viên dành thời gian bất thường tại một câu hoặc không có tiến triển trong thời gian dài.

---

## 9. Yêu cầu Hỗ trợ trong Lớp

### Yêu cầu hỗ trợ
// Cho phép học viên gửi yêu cầu hỗ trợ khi gặp khó khăn trong quá trình học hoặc làm bài.

### Hiển thị danh sách cần hỗ trợ
// Cho phép giảng viên xem những học viên đang chờ được hỗ trợ.

### Xác nhận yêu cầu hỗ trợ
// Giảng viên xác nhận đã nhận được yêu cầu của học viên.

### Hoàn tất hỗ trợ
// Đánh dấu yêu cầu hỗ trợ đã được xử lý.

### Theo dõi thời gian chờ hỗ trợ
// Ghi nhận khoảng thời gian từ lúc học viên yêu cầu đến khi được giảng viên hỗ trợ.

---

## 10. Nộp bài & Chấm điểm

### Nộp bài
// Cho phép học viên gửi kết quả cuối cùng của bài tập hoặc bài kiểm tra.

### Lưu câu trả lời
// Lưu các câu trả lời mà học viên đã thực hiện trong quá trình làm bài.

### Chỉnh sửa câu trả lời
// Cho phép thay đổi câu trả lời khi hoạt động vẫn còn cho phép chỉnh sửa.

### Chấm điểm tự động
// Tự động chấm các dạng câu hỏi có đáp án xác định như trắc nghiệm.

### Chấm điểm thủ công
// Cho phép giảng viên chấm các bài tự luận hoặc bài cần đánh giá bằng con người.

### Nhận xét bài làm
// Cho phép giảng viên cung cấp feedback cho toàn bài hoặc từng câu hỏi.

### Chấm điểm theo tiêu chí
// Hỗ trợ rubric hoặc các tiêu chí đánh giá được xác định trước.

### Công bố kết quả
// Cho phép giảng viên quyết định thời điểm học viên được xem điểm và nhận xét.

---

## 11. Sổ điểm

### Ghi nhận điểm
// Lưu điểm của học viên sau khi một bài làm đã được chấm.

### Quản lý điểm thành phần
// Quản lý điểm bài tập, quiz, kiểm tra giữa kỳ, cuối kỳ hoặc các thành phần khác.

### Quản lý trọng số điểm
// Thiết lập tỷ lệ đóng góp của từng loại điểm vào kết quả cuối cùng.

### Tính điểm trung bình
// Tự động tính điểm trung bình dựa trên các điểm thành phần và trọng số.

### Tính điểm tổng kết
// Tính kết quả cuối cùng của học viên trong khóa học hoặc lớp học.

### Điều chỉnh điểm
// Cho phép giảng viên thay đổi điểm khi có lý do phù hợp và lưu lại lịch sử thay đổi.

### Xem bảng điểm
// Cho phép học viên xem điểm của mình và giảng viên xem điểm của cả lớp.

---

## 12. Tiến độ Học tập

### Theo dõi tiến độ khóa học
// Theo dõi phần trăm hoàn thành của học viên đối với toàn bộ khóa học.

### Theo dõi tiến độ bài học
// Ghi nhận những bài học học viên đã bắt đầu, đang học hoặc đã hoàn thành.

### Ghi nhận hoàn thành nội dung
// Đánh dấu nội dung học tập đã được hoàn thành theo các điều kiện được quy định.

### Lịch sử học tập
// Lưu lại quá trình học, các hoạt động đã thực hiện và kết quả theo thời gian.

### Theo dõi tiến độ theo lớp
// Cho phép giảng viên xem mức độ hoàn thành khóa học của từng học viên trong lớp.

---

## 13. Giao tiếp & Thông báo

### Thông báo lớp học
// Cho phép giảng viên đăng thông báo cho toàn bộ lớp hoặc một nhóm học viên.

### Tin nhắn
// Hỗ trợ trao đổi giữa giảng viên và học viên trong phạm vi được hệ thống cho phép.

### Thông báo giao bài
// Gửi thông báo khi có bài tập hoặc hoạt động mới được giao.

### Nhắc hạn nộp bài
// Nhắc học viên khi bài tập sắp đến hạn.

### Thông báo thay đổi lịch học
// Gửi thông báo khi một buổi học được đổi lịch hoặc hủy.

### Thông báo kết quả
// Thông báo khi bài làm đã được chấm hoặc điểm đã được công bố.

### Thông báo trong ứng dụng
// Hiển thị các notification trực tiếp trong hệ thống.

### Thông báo qua email
// Gửi các thông báo cần thiết qua email khi được cấu hình.

---

## 14. Học liệu & Đa phương tiện

### Quản lý tài liệu
// Upload, lưu trữ và quản lý các tài liệu phục vụ khóa học và bài học.

### Quản lý video bài học
// Lưu trữ hoặc liên kết video phục vụ nội dung học tập.

### Quản lý slide
// Quản lý các slide và tài liệu trình bày của bài học.

### Quản lý tệp đính kèm
// Cho phép gắn file vào bài học, bài tập, thông báo hoặc các nội dung liên quan.

### Ghi hình buổi học
// Lưu recording của buổi học trực tuyến khi chức năng ghi hình được bật.

### Xem lại buổi học
// Cho phép người có quyền truy cập xem lại recording của buổi học đã kết thúc.

---

## 15. Ngân hàng Câu hỏi

### Quản lý ngân hàng câu hỏi
// Lưu trữ câu hỏi để có thể sử dụng lại cho nhiều bài tập, quiz hoặc bài kiểm tra.

### Phân loại câu hỏi
// Phân nhóm câu hỏi theo môn học, chủ đề, bài học, độ khó hoặc thẻ.

### Tái sử dụng câu hỏi
// Cho phép giảng viên chọn lại câu hỏi có sẵn khi tạo bài kiểm tra mới.

### Random câu hỏi
// Tạo đề bằng cách chọn ngẫu nhiên câu hỏi từ ngân hàng theo điều kiện xác định.

### Quản lý phiên bản câu hỏi
// Theo dõi thay đổi nội dung câu hỏi mà không làm ảnh hưởng đến các bài kiểm tra đã sử dụng phiên bản cũ.

---

## 16. Thi & Kiểm tra

### Tạo kỳ thi / bài kiểm tra
// Tạo một hoạt động đánh giá chính thức có thời gian, quy tắc và cách tính điểm riêng.

### Thiết lập thời gian thi
// Quy định thời điểm mở bài, đóng bài và thời lượng làm bài.

### Giới hạn số lần làm
// Quy định số lần học viên được phép thực hiện bài kiểm tra.

### Trộn câu hỏi
// Thay đổi thứ tự câu hỏi hoặc lựa chọn để tạo các phiên bản bài thi khác nhau.

### Thiết lập điểm đạt
// Xác định mức điểm tối thiểu để học viên được xem là đạt.

### Quản lý thi lại
// Cho phép tổ chức lại bài kiểm tra theo các điều kiện được thiết lập.

### Công bố kết quả thi
// Quản lý thời điểm và phạm vi thông tin kết quả được hiển thị cho học viên.

---

## 17. Báo cáo & Phân tích

### Báo cáo lớp học
// Tổng hợp số lượng học viên, mức độ tham gia, tiến độ và kết quả của từng lớp.

### Báo cáo khóa học
// Tổng hợp mức độ hoàn thành và kết quả học tập của học viên trong khóa học.

### Báo cáo học viên
// Hiển thị lịch sử học tập, tiến độ, điểm số và chuyên cần của từng học viên.

### Báo cáo bài tập
// Tổng hợp tỷ lệ bắt đầu, hoàn thành, nộp bài và điểm của một hoạt động.

### Phân tích câu hỏi
// Thống kê tỷ lệ đúng/sai, thời gian làm và mức độ khó thực tế của từng câu hỏi.

### Phân tích realtime lớp học
// Tổng hợp trạng thái hiện tại của lớp để giảng viên nhanh chóng nhận biết tiến độ chung và các điểm đang gây khó khăn.

### Báo cáo chuyên cần
// Tổng hợp tình trạng có mặt, vắng mặt, đi trễ và thời lượng tham gia của học viên.
// Teacher có thể xem bảng theo từng lớp/phiên, gồm lịch sử reconnect và xuất dữ liệu hàng/cột ra Excel.

---


## 18. Đề tài & Tiểu luận

### Đăng ký đề tài
// Cho phép học viên hoặc đại diện nhóm đăng ký tên đề tài tiểu luận cho một môn học hoặc học phần có yêu cầu báo cáo.

### Đăng ký đề tài theo nhóm
// Tên đề tài được gắn với toàn bộ nhóm thực hiện. Mọi thành viên trong nhóm cùng sử dụng một đề tài đã được đăng ký và phê duyệt.

### Duyệt đề tài
// Cho phép giảng viên kiểm tra, chấp thuận, yêu cầu chỉnh sửa hoặc từ chối đề tài mà nhóm đã đăng ký.

### Khóa tên đề tài sau khi duyệt
// Sau khi đề tài được giảng viên chấp thuận, tên đề tài chính thức được khóa để làm căn cứ kiểm tra các báo cáo nộp về sau.

### Thay đổi đề tài
// Cho phép nhóm gửi yêu cầu đổi tên hoặc thay đổi đề tài. Việc thay đổi chỉ có hiệu lực sau khi được giảng viên chấp thuận.

### Kiểm tra tên đề tài trong báo cáo
// Khi sinh viên nộp báo cáo, hệ thống trích xuất nội dung từ file và kiểm tra tên đề tài xuất hiện trong phần đầu hoặc trang bìa có khớp với tên đề tài chính thức đã đăng ký hay không.

### Chuẩn hóa tên đề tài trước khi so sánh
// Hệ thống chuẩn hóa chữ hoa/thường, khoảng trắng, xuống dòng và một số ký tự trình bày trước khi so sánh để tránh từ chối sai chỉ vì khác định dạng nhưng vẫn giữ nguyên nội dung đề tài.

### Từ chối báo cáo sai tên đề tài
// Nếu tên đề tài trong báo cáo không khớp với tên đề tài đã được đăng ký và phê duyệt, hệ thống không cho nộp chính thức và yêu cầu sinh viên chỉnh sửa file rồi nộp lại.

### Kiểm tra file báo cáo rỗng
// Hệ thống kiểm tra file không chỉ dựa vào dung lượng mà còn dựa trên số trang, lượng văn bản trích xuất được và mật độ nội dung để phát hiện các file gần như không có nội dung thực.

### Kiểm tra dung lượng và cấu trúc file
// Xác thực file có tồn tại, đúng định dạng cho phép, có số trang hợp lệ và không phải file lỗi hoặc file không đọc được.

### Kiểm tra lượng nội dung tối thiểu
// Hệ thống yêu cầu báo cáo đạt số lượng từ hoặc ký tự tối thiểu do giảng viên cấu hình cho từng bài tiểu luận.

### Kiểm tra các phần bắt buộc
// Giảng viên có thể cấu hình một số mục bắt buộc như tên đề tài, thông tin nhóm, mục lục, nội dung chính, kết luận hoặc tài liệu tham khảo. Hệ thống kiểm tra sự hiện diện của các phần này trước khi cho phép nộp.

### Kiểm tra báo cáo chỉ có trang trắng
// Với PDF hoặc tài liệu có nhiều trang, hệ thống phát hiện trường hợp file có số trang nhưng phần lớn trang không chứa văn bản hoặc nội dung đáng kể.

### Kiểm tra báo cáo chỉ có hình ảnh
// Nếu tài liệu không có văn bản có thể trích xuất nhưng chứa hình ảnh, hệ thống đánh dấu để kiểm tra thêm hoặc áp dụng OCR nếu bài nộp cho phép tài liệu dạng scan.

### Cảnh báo báo cáo có nội dung quá ít
// Nếu file có nội dung nhưng thấp hơn ngưỡng tối thiểu, hệ thống cảnh báo và không cho nộp chính thức cho đến khi sinh viên cập nhật lại báo cáo.

### Xác thực báo cáo trước khi nộp
// Trước khi tạo submission chính thức, hệ thống chạy toàn bộ các kiểm tra về định dạng file, đề tài, lượng nội dung và các phần bắt buộc rồi trả về danh sách lỗi cụ thể cho sinh viên.

### Nộp lại báo cáo
// Cho phép sinh viên thay thế file báo cáo khi lần nộp trước không đạt yêu cầu hoặc khi giảng viên yêu cầu chỉnh sửa, trong phạm vi thời gian cho phép.

### Lưu lịch sử phiên bản báo cáo
// Lưu lại các lần nộp, thời điểm nộp, file đã gửi và lý do bị từ chối để giảng viên và sinh viên có thể theo dõi quá trình chỉnh sửa.

### Theo dõi trạng thái tiểu luận
// Quản lý trạng thái như chưa đăng ký đề tài, chờ duyệt đề tài, đề tài đã duyệt, đang thực hiện, báo cáo không hợp lệ, đã nộp và đã chấm.

### Quản lý thành viên nhóm
// Quản lý danh sách thành viên thực hiện cùng một đề tài và đảm bảo báo cáo được liên kết đúng với nhóm đã đăng ký.

### Nộp báo cáo theo nhóm
// Cho phép một thành viên đại diện nộp báo cáo cho toàn nhóm. Submission được tính cho tất cả thành viên thuộc nhóm tại thời điểm nộp.

---


## 19. Hỗ trợ Chấm bài Tự luận bằng AI (Coming Soon)

> **Trạng thái: COMING SOON — Không triển khai trong GOAL hiện tại.**

### Quản lý tài liệu đối chiếu cho bài tự luận
// Cho phép giảng viên cung cấp đáp án tham khảo, rubric, giáo trình, slide hoặc tài liệu chuẩn dùng làm nguồn đối chiếu khi AI phân tích bài làm.

### Phân tích bài tự luận bằng RAG
// AI sử dụng RAG để đối chiếu nội dung bài làm của sinh viên với tài liệu do giảng viên cung cấp và tạo các nhận xét hỗ trợ cho giảng viên.

### Xác định các ý có căn cứ
// AI chỉ ra những luận điểm hoặc nội dung trong bài làm có thể được đối chiếu và tìm thấy căn cứ trong tài liệu chuẩn.

### Xác định các ý chưa đầy đủ
// AI đánh dấu các nội dung có liên quan nhưng còn thiếu ý, thiếu lập luận hoặc chưa thể hiện đầy đủ yêu cầu theo tài liệu đối chiếu.

### Phát hiện nội dung có khả năng mâu thuẫn
// AI chỉ ra các nội dung có khả năng mâu thuẫn với tài liệu chuẩn để giảng viên chủ động kiểm tra lại.

### Hiển thị nguồn đối chiếu
// Mỗi nhận xét của AI cần đi kèm nguồn tham khảo tương ứng như tài liệu, mục, chương hoặc đoạn liên quan để giảng viên có thể tự xác minh.

### Đánh dấu nội dung cần giảng viên xem xét
// AI có thể đánh dấu các phần không đủ căn cứ để kết luận và chuyển quyền đánh giá hoàn toàn cho giảng viên.

### Giảng viên xác nhận đánh giá
// Giảng viên là người trực tiếp đọc bài, sử dụng hoặc bỏ qua nhận xét của AI, chấm theo rubric và nhập điểm cuối cùng.

### Không cho phép AI chấm điểm
// AI tuyệt đối không được tự tạo điểm số, đề xuất điểm, xếp loại, phần trăm đúng hoặc quyết định kết quả học tập của sinh viên dưới bất kỳ hình thức trực tiếp hay gián tiếp nào.

### Không tự động chuyển kết quả AI thành điểm
// Kết quả phân tích của AI không được liên kết tự động với trường điểm, Gradebook hoặc bất kỳ command nào có thể ghi nhận điểm cho sinh viên.

### Bảo vệ khỏi prompt injection trong bài làm
// Nội dung bài nộp của sinh viên luôn được xem là dữ liệu không tin cậy. Các câu lệnh hoặc hướng dẫn được chèn trong bài làm không được phép thay đổi quy tắc phân tích của hệ thống AI.

---

## 20. Các chức năng mở rộng về sau

### Chứng chỉ hoàn thành
// Cấp chứng chỉ khi học viên đáp ứng điều kiện hoàn thành khóa học hoặc chương trình đào tạo.

### Đăng ký khóa học
// Cho phép học viên đăng ký hoặc gửi yêu cầu tham gia khóa học.

### Quản lý gói học
// Quản lý các gói đào tạo hoặc quyền truy cập theo chương trình.

### Thanh toán
// Xử lý thanh toán khi hệ thống triển khai mô hình bán khóa học hoặc dịch vụ đào tạo.

---

# Chức năng lõi tạo khác biệt

Phần tạo khác biệt chính của hệ thống không nằm ở các chức năng LMS truyền thống như tạo khóa học, giao bài hay chấm điểm, mà nằm ở chuỗi chức năng realtime:

```text
Buổi học trực tuyến
        ↓
Giảng viên giao hoạt động ngay trong lớp
        ↓
Học viên làm trực tiếp trên hệ thống
        ↓
Hệ thống ghi nhận quá trình làm bài
        ↓
Giảng viên nhìn thấy trạng thái cả lớp theo thời gian thực
        ↓
Phát hiện học viên đang chậm / không hoạt động / cần hỗ trợ
        ↓
Giảng viên can thiệp trực tiếp
        ↓
Học viên tiếp tục và hoàn thành hoạt động
```

Các chức năng này cần được ưu tiên khi thiết kế Event Storming, Domain Model và kiến trúc hệ thống.
