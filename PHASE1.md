# Giai đoạn 1 — bản lái Android

Bản triển khai và APK đã hoàn tất ngày 07/11/2026. Build Android thành công, 0 lỗi. Chưa xác nhận chạy trên điện thoại thật vì chưa có thiết bị kết nối.

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
| Điện thoại USB/ADB | Chưa có thiết bị kết nối |

Đây là kiểm tra hệ thống trong Editor; chưa xác nhận chạy trọn đường đua bằng tay trên máy thật, FPS/nhiệt máy hay cảm giác gia tốc kế thực tế. Số FPS trong Editor không được dùng làm FPS Android.

## APK và cách build lại

- APK đã xuất: `Builds/Android/Phase1/VuotThac-Phase1.apk` — 276.487.807 byte (263,68 MiB).
- Đã xác minh chữ ký APK v2 bằng `apksigner` và căn chỉnh 16KB bằng `zipalign` (mã thoát 0).
- Đã đọc manifest APK: `com.kaito1412hung.vuotthac`, phiên bản `0.1.0`, ARM64, min SDK 26, target SDK 36, activity khởi chạy `UnityPlayerGameActivity`.
- SHA-256: `F2AA5F216C4684BE77B96A68AFF1F5E5E314A8B06F176D07008315BDA858D562`.
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

AI đối thủ, checkpoint/xếp hạng và rubber-banding thuộc giai đoạn 2, chưa tính là đã hoàn thành ở đây.
