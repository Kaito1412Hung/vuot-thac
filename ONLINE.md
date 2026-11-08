# Chơi với máy và chơi cùng bạn bè

## Năm độ khó đấu máy

Ở màn chọn thuyền, bấm **ĐỘ KHÓ** để đổi lần lượt Rất dễ → Dễ → Thường → Khó → Rất khó. Lựa chọn được lưu cho lần chơi sau. Bấm **BẮT ĐẦU** để đua với 5 máy.

| Mức | Lực chèo cơ sở của AI | Chu kỳ quyết định |
|---|---|---|
| Rất dễ | 64% | 0,14 giây |
| Dễ | 73% | 0,12 giây |
| Thường | 82% | 0,10 giây |
| Khó | 91% | 0,075 giây |
| Rất khó | 100% | 0,05 giây |

Đây là mức đầu vào chèo, không phải phần trăm tốc độ tuyệt đối: dòng chảy và loại thuyền vẫn ảnh hưởng tốc độ. AI dùng chung vật lý, va chạm và checkpoint; không tăng chỉ số thân thuyền, không nhảy checkpoint để bắt kịp. Mức thấp vẫn giữ đủ khả năng lái qua cửa nhánh.

Kiểm tra với 6 thuyền tự lái trên collider thật của map, ngày 08/11/2026:

| Mức | Về đích | Thời gian thuyền cuối trong lượt thử |
|---|---|---|
| Rất dễ | 6/6 | 157,1 giây |
| Dễ | 6/6 | 146,7 giây |
| Thường | 6/6 | 142,4 giây |
| Khó | 6/6 | 129,7 giây |
| Rất khó | 6/6 | 120,1 giây |

Thời gian là kết quả của các lượt kiểm tra này, không phải thời gian cố định cho mọi cuộc đua. Báo cáo nằm ở `Builds/Validation/phase2-difficulty-0.txt` đến `phase2-difficulty-4.txt`. Chạy lại trong Play Mode bằng `PhaseTwoValidation.RunRace(0)` đến `RunRace(4)`.

## Phòng bạn bè qua Internet

1. Mỗi người chọn thuyền của mình, bấm **CHƠI VỚI BẠN BÈ** và điền tên.
2. Một người bấm **TẠO PHÒNG**, chia sẻ mã phòng cho bạn bè.
3. Những người còn lại nhập mã rồi bấm **VÀO PHÒNG**.
4. Tất cả, kể cả chủ phòng, bấm **SẴN SÀNG**.
5. Khi có ít nhất 2 người và tất cả đã sẵn sàng, chủ phòng bấm **BẮT ĐẦU ĐUA**.

Cuộc đua chỉ tạo thuyền cho những người trong phòng: 2 người thì 2 thuyền, không thêm AI. Độ khó đấu máy không thay đổi luật hoặc chỉ số người chơi online. Phòng khóa sau khi bắt đầu; ai muốn tham gia phải vào trước lúc xuất phát.

Số người không bị cố định ở 2, 4 hoặc 6. Dịch vụ Unity Lobby/Relay có giới hạn kỹ thuật **150 người/phòng**, bao gồm chủ phòng: [Unity — Players](https://docs.unity.com/en-us/mps-sdk/players). Đây không phải cam kết rằng điện thoại hoặc map chạy ổn định với 150 thuyền; số người thực tế cần được kiểm tra tải. Không có chế độ “vô hạn người chơi”. Với nhiều thuyền, lưới xuất phát chia hàng theo chiều rộng dòng sông; các phòng đông chưa được nghiệm thu.

## Đồng bộ và xử lý kết nối

- Unity Multiplayer Services 2.4.1 cung cấp phiên riêng, mã phòng và Relay; NGO 2.13.3 truyền gói dữ liệu qua Unity Transport. Project đã liên kết Unity Cloud; không cần nhập địa chỉ IP hoặc mở cổng trên router khi dùng Relay.
- Máy chủ phòng tính vật lý, checkpoint, thứ hạng và thời gian về đích. Máy khách gửi thao tác lái khoảng 20 lần/giây; máy chủ gửi trạng thái khoảng 10 lần/giây. Máy khách nội suy vị trí nhận được.
- Đầu vào chỉ được áp dụng cho thuyền thuộc kết nối gửi nó; các máy khách không tự gửi vị trí để tăng tiến trình. Có kiểm tra phiên bản giao thức, dữ liệu số và thứ tự bản cập nhật.
- Quá 0,75 giây không có thao tác mới thì máy chủ nhả đầu vào của thuyền đó.
- Người rời giữa trận được ghi mất kết nối; thuyền dừng và không cản đường. Chủ phòng rời hoặc mất kết nối thì cuộc đua kết thúc trên các máy khách; chưa chuyển quyền máy chủ cuộc đua sang người khác.
- Về màn chọn thuyền sẽ rời phòng và dọn cuộc đua. Chọn lại thuyền trước khi tạo/vào phòng mới.

## Trạng thái kiểm tra online

Đã kiểm tra qua Relay thật với hai tiến trình game độc lập trên cùng máy: Unity Editor làm chủ phòng và bản Windows Development nhập mã bằng tài khoản ẩn danh riêng. Cả hai sẵn sàng, bắt đầu cuộc đua có đúng **2 racer và 0 AI**. Máy khách gửi thao tác chèo, nhận vị trí/thời gian/checkpoint từ chủ phòng; trong lượt thử quan sát thuyền máy khách đi khoảng **62,8m**, nhận checkpoint đầu và thời gian đua khoảng **7,15 giây**. Bản Windows thoát với mã 0. Khi máy khách rời, chủ phòng đánh dấu thuyền đó mất kết nối và dừng vật lý; rời phòng dọn hết racer.

Báo cáo tại `Builds/Validation/online-peer.json`, `online-host-start.txt` và `online-host-final.txt`. Đây là hai tiến trình dùng Internet Relay trên cùng một máy, chưa phải kiểm tra hai điện thoại hoặc hai mạng vật lý khác nhau. Chưa đo tải phòng đông, mất gói kéo dài hoặc độ trễ mạng di động. Lượt thử có cảnh báo telemetry của Unity và cảnh báo dừng phiên lặp trong SDK lúc thoát; không có ngoại lệ C# trong kiểm tra gameplay.

Android đã bật quyền Internet trong cấu hình project. APK giai đoạn 1 cũ chưa chứa tính năng này; cần build lại để thử trên điện thoại. Bản Windows trong `Builds/Windows/OnlineTest` phục vụ kiểm tra, không phải bản phát hành.
