# Giai đoạn 1 — bản lái Android

Bản triển khai và APK đã cập nhật ngày 07/11/2026, gồm cả người chèo và chuyển động chèo mới. Build Android thành công, 0 lỗi. Chưa xác nhận chạy trên điện thoại thật vì chưa có thiết bị kết nối.

## Phần đã triển khai

- Map sông spline có dòng chảy, nhánh rẽ, đá, thác và hang động; giữ nguyên cảnh quan đã duyệt.
- 10 loại thuyền, vật lý nổi 4 điểm, va chạm và tự đưa về dòng khi lật/kẹt/lên bờ.
- Camera bám sau và góc nhìn thứ nhất, người chèo và mái chèo chuyển động.
- Chọn thuyền, tốc độ, tiến độ, đồng hồ, màn hoàn thành và chơi lại.
- PC: W/S chèo, A/D lái, R đưa về giữa dòng, V đổi camera, Esc chọn lại.
- Android: giữ CHÈO/LÙI và TRÁI/PHẢI; hoặc chuyển LÁI: NGHIÊNG, giữ máy ở tư thế thoải mái rồi chạm CÂN BẰNG MÁY. Nghiêng nhẹ để rẽ, có vùng chết và làm mượt. Nút lái được ưu tiên khi đang giữ.
- Nút cảm ứng xử lý nhiều ngón tay và nhả khi mất focus/tạm nghỉ. Giao diện nằm trong vùng an toàn của màn hình.
- Android nằm ngang hai chiều, ARM64, Android 8/API 26 trở lên, IL2CPP. Dùng profile Mobile và giới hạn cảnh vật xa theo kích thước ở runtime.

## Kiểm tra ngày 07/11/2026

### Các sửa trong project sau lần xuất APK bên dưới

- Bậc cửa và móng đỡ của 13 nhà đã được nối xuống địa hình, sửa lối đá hở chân.
- Thuyền qua thác: bốn điểm nổi lấy mặt nước riêng, thân thuyền nghiêng theo dốc và giảm chúi khi tiếp nước. Nước sông/lớp bọt không vẽ xuyên thể tích lòng thuyền; bọt sát thân lấy độ cao riêng ở mũi và đuôi.
- Camera sau giữ tối thiểu 0,9m trên mặt nước, kể cả khi camera còn nằm ở phần sông cao phía sau thuyền.
- Kiểm tra vật lý tách biệt: 30/30 lượt (10 thuyền × 3 thác), vận tốc vào 8m/s, không hồi vị trí; báo cáo `Builds/Validation/fall-buoyancy.txt`. Chạy trực tiếp đoạn thác với phao và xuồng gỗ, không có lỗi runtime trong lượt thử; camera được đo ở trên mặt nước ít nhất 0,9m.

**Các sửa nhà và vượt thác này chưa nằm trong APK đã xuất trước đó; cần build lại nếu muốn thử chúng trên điện thoại.**

Động tác chèo đã được chỉnh tiếp: mái chèo giữ chiều dài cố định, kayak đổi bên liên tục, nhịp cắm–kéo–rút–đưa về và đổi chiều được làm mượt. Ghế/người được đặt lại để tay tránh mạn, bàn tay bám cán bằng IK có giới hạn tầm với; đội thuyền rồng chèo đồng bộ. Người chèo có áo phao gọn hơn, áo thể thao, giày nhẹ, tỷ lệ tay và nét mặt được chỉnh lại.

Thuyền chỉ có một người chèo nay luân phiên một nhịp trái, một nhịp phải (xuồng ba lá, thúng, bè/sào và tam bản). Mái được nhấc khỏi nước rồi đưa qua phía trước; hai tay đổi vị trí cầm khi chuyển bên. Kayak tiếp tục dùng mái hai đầu; thuyền nhiều người giữ vị trí chèo theo đội. Thúng có thêm khoảng hở với vành khi chèo bên đối diện ghế lệch tâm.

Kiểm tra riêng động tác: 9.360 tư thế ở 3 mức sức chèo trên toàn bộ người có mái chèo, gồm đủ hai nhịp đổi bên; không phát hiện cán chèo hoặc đoạn tay cắt các mesh thân/phụ kiện thuyền được kiểm tra. Sai lệch nắm cán tối đa khoảng 0,1mm; chênh đầu cán qua điểm nối chu kỳ khoảng 0,09mm. Mái chèo đơn và sào có khoảng hở mặt nước ở giai đoạn đưa về. Báo cáo có thể chạy lại bằng `RowingMotionAudit.Run()` và nằm tại `Builds/Validation/rowing-motion.txt`. Đây là kiểm tra lấy mẫu đường cán/tay, không phải chứng nhận va chạm mọi điểm trên toàn bộ lưới người trong mọi trạng thái vật lý. Thay đổi luân phiên hai bên chưa có trong APK cũ.

| Kiểm tra | Kết quả |
|---|---|
| Biên dịch C# trong Editor | Không có lỗi tại thời điểm kiểm tra |
| Mặt nước toàn tuyến và 3 nhánh, bước 2m | 971 điểm, 0 điểm khô; chênh cao tra cứu lớn nhất 0,000021m |
| Độ nổi cả 10 thuyền trong Play Mode | Cả 10 ở trên nước; trục đứng y=0,979–1,000; thân cao hơn mặt nước 0,060–0,401m tùy thuyền |
| Nút giữ với 2 ngón trên cùng nút | Nhả một ngón vẫn giữ, nhả ngón cuối trả về 0 |
| Vùng chết/cân bằng nghiêng | Cùng tư thế = 0; nghiêng lớn được chặn ở 1 |
| Lật thuyền thử | Đã trở lại tư thế thẳng sau cơ chế hồi phục |
| Giới hạn cảnh vật xa ở runtime | Đã kiểm tra: 6.224/8.573 renderer ngoài ngân sách khoảng cách được tạm ẩn ở vị trí thử; không sửa hoặc xóa cảnh quan |
| Qua đích | Đã kiểm tra chuyển sang Finished, khóa điều khiển và dừng thuyền |
| Chạy thử người chèo mới | Kayak chèo trong Play Mode, cả camera sau và góc nhìn thứ nhất; không có lỗi runtime trong lượt thử |
| Điện thoại USB/ADB | Chưa có thiết bị kết nối |

Đây là kiểm tra hệ thống trong Editor; chưa xác nhận chạy trọn đường đua bằng tay trên máy thật, FPS/nhiệt máy hay cảm giác gia tốc kế thực tế. Số FPS trong Editor không được dùng làm FPS Android.

## APK và cách build lại

### Sửa kẹt cua và chìm/bật thuyền (07/11/2026, trong project Unity)

- Giữ lực lái/lực đẩy khi thân còn trên nước dù mũi chạm bờ; giữ lực nổi liên tục ở mép nước.
- Va quệt giữ vận tốc dọc mặt va chạm. Khi bị ghì chậm vào đá/bờ, lực gỡ kẹt đi theo pháp tuyến tiếp xúc và trượt quanh vật cản, kể cả đá nằm phía trong lòng sông.
- Lực nổi dùng giảm chấn theo vận tốc, bỏ xung lực bình phương tốc độ từng làm thuyền bật vọt sau khi chìm. Tăng giảm chấn nghiêng cho kayak và các thân hẹp.
- Xoay đầu tại chỗ không bị tính nhầm là đứng im; lưu `LastRecoveryReason` để chẩn đoán các lần hồi vị.
- Kiểm tra với collider thực của scene: 60/60 tình huống cạ hai bờ ở ba cua, 30/30 lượt qua ba thác, 10/10 lượt phục hồi sau ép chìm và 30/30 lượt đi qua ba nhánh hang, trên toàn bộ 10 thuyền. Không có lần hồi vị trong các lượt cuối. Console không có lỗi trong lượt thử. Báo cáo tại `Builds/Validation/gameplay-*.txt`; ba nhóm đầu chạy lại bằng `BoatGameplayValidation.Run("shore"/"falls"/"immersion")` trong Play Mode.
- Đây là các tình huống kiểm tra có điều khiển trong Editor, chưa thay thế chơi thử toàn tuyến bằng tay và trên điện thoại. APK bên dưới chưa chứa các sửa này.

- APK đã cập nhật người/chuyển động chèo mới: `Builds/Android/Phase1/VuotThac-Phase1.apk` — 240.927.256 byte (229,77 MiB).
- Đã xác minh chữ ký APK v2 bằng `apksigner` và căn chỉnh 16KB bằng `zipalign` (mã thoát 0).
- Đã đọc manifest APK: `com.kaito1412hung.vuotthac`, phiên bản `0.1.0`, ARM64, min SDK 26, target SDK 36, activity khởi chạy `UnityPlayerGameActivity`.
- SHA-256: `4FE0DDE0CC4D7CAB4A689CF4B61144304DE23D1B5EA84C5F28F756657B3BC6CE`.
- Unity menu: **Vuot Thac → Phase 1 → Build Android APK**.
- Trạng thái và lỗi build: `Builds/Android/Phase1/build-status.txt`, `build-messages.txt`.
- Cache Gradle nằm tại `D:/Unity/Project_Cki_CNM/.g` để tránh giới hạn đường dẫn 260 ký tự của Ninja trên Windows. Android user settings và thư mục tạm nằm trong `Builds/Android/ToolCache` trên ổ D. Unity/SDK/JDK đã cài trước đó vẫn dùng tại vị trí có sẵn.
- APK là bản thử nghiệm dùng chữ ký debug, không phải bản phát hành cửa hàng.

## Cần xác nhận trên điện thoại

1. Cài APK lên máy ARM64, thử hai chiều ngang và màn hình có tai thỏ.
2. Chọn vài loại thuyền, chèo và rẽ bằng hai ngón; chuyển ứng dụng rồi quay lại để kiểm tra nút không bị giữ kẹt.
3. Bật nghiêng, cân bằng tư thế cầm và thử rẽ hai phía; cân bằng lại sau khi đổi chiều cầm.
4. Chạy từ xuất phát qua nhánh hang đến đích, đổi camera, thử hồi phục và chơi lại.
5. Chơi 10–15 phút để đo FPS, bộ nhớ, nhiệt độ và tinh chỉnh độ nhạy.

AI đối thủ, checkpoint/xếp hạng và rubber-banding thuộc giai đoạn 2. Trạng thái triển khai và kiểm tra hiện tại của các phần này xem [PHASE2.md](PHASE2.md); chúng không nằm trong APK giai đoạn 1 nêu trên.
