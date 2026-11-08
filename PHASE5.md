# Giai đoạn 5 — Mobile

Trạng thái 08/11/2026: đã tối ưu một số chi phí CPU/mesh và sửa điều khiển mobile; đã build APK Android thành công, 0 lỗi. Bản cài đặt bàn giao để thử điện thoại sau; chưa xác nhận FPS, nhiệt độ, bộ nhớ hay cảm giác lái trên thiết bị thật.

## Những thay đổi trong giai đoạn này

- Loại các mảng cấp phát lặp lại trong hoạt ảnh chân người chèo, đầu mái chèo, hiệu ứng sóng hai bên và quan sát nhìn trước của AI. Giữ nguyên thứ tự/giá trị 42 quan sát và lực vật lý.
- Mesh icon vật phẩm trên mobile dùng đường tròn ít đoạn hơn: tăng tốc 1.578 → 642 đỉnh, khiên 1.590 → 654, gỗ 4.410 → 2.154. HUD vẫn dùng mesh đầy đủ ở độ phân giải giao diện.
- Trên mobile, biểu tượng vật phẩm xa camera hơn 160 m được ẩn renderer; kiểm tra khoảng cách mỗi 0,2 giây và bỏ cập nhật billboard khi đang ẩn. Logic nhặt/hồi vật phẩm vẫn chạy.
- Safe area chỉ cập nhật anchors khi vùng an toàn hoặc kích thước màn hình thay đổi, tránh yêu cầu dựng lại Canvas mỗi frame.
- Giữ đồng thời trái/phải sẽ triệt tiêu lái cảm ứng và không vô tình quay sang lái bằng nghiêng máy. Nút cảm ứng vẫn ưu tiên nghiêng máy.
- Mất focus/tạm dừng sẽ xóa lệnh chạm và giá trị nghiêng đang giữ; khi quay lại hoặc đổi hướng màn hình sẽ cân bằng lại tư thế cầm.
- Giữ cấu hình mobile sẵn có: mục tiêu 60 FPS, URP render scale 0,8, landscape hai chiều, CullingGroup cho cảnh vật. Đây là cấu hình mục tiêu, chưa phải số đo đạt được.

Không dựng lại map hoặc thay đổi vị trí công trình, cây, hang, thác. Các tối ưu hiển thị chỉ tác động runtime.

## Kiểm tra đã chạy

`Builds/Validation/phase5-editor-checks.txt`: 8/8 kiểm tra đạt, gồm triệt tiêu trái/phải, ưu tiên nút chạm, giải phóng điều khiển, trung tính/giới hạn nghiêng và mesh gọn cho ba loại icon. Unity biên dịch C# không lỗi sau thay đổi. Đây là kiểm tra logic trong Editor, không thay thế thử cảm biến và đa chạm trên thiết bị.

## Build Android

Menu **Vuot Thac / Phase 5 / Build Android APK**. Đích `Builds/Android/Phase5/VuotThac-Phase5.apk`, phiên bản 0.5.0 (code 5), ARM64 / IL2CPP, Android 8.0 trở lên (API 26), target API 36. Một scene Suối Rừng; gồm tính năng các giai đoạn 1–4 và model PPO hiện tại.

Gradle cache dùng `D:/Unity/Project_Cki_CNM/.g`; thư mục tạm và Android user home ở `Builds/Android/ToolCache` trong project D. SDK/NDK/JDK có sẵn của Unity được sử dụng tại nơi đã cài. Build xong trả Editor về Windows để không dùng nhầm chất lượng Mobile khi xem trên máy tính.

### Kết quả build và kiểm tra gói cài đặt

- Build: **Succeeded, 0 lỗi**, thời gian 25 phút 22 giây cho lần build đầy đủ đầu tiên với các gói mới.
- APK thực tế: **264.843.969 byte**, khoảng **252,6 MiB**. Không nhầm với tổng dung lượng dữ liệu/intermediate/symbol trong BuildReport.
- Package `com.kaito1412hung.vuotthac`, version `0.5.0`, code `5`, min SDK `26`, target SDK `36`, chỉ `arm64-v8a`.
- `apksigner verify --verbose`: chữ ký APK v2 hợp lệ. `zipalign -c -P 16 4`: exit 0.
- Cả 6 thư viện native trong APK có PT_LOAD alignment tối thiểu 16.384 byte; kiểm tra đóng gói/ELF 16 KB đạt. Chưa thay thế thử khởi chạy trên máy 16 KB thật.
- SHA-256: `eef7be5c935a07b09d3789c415975f7cb4613f1038968b811d9399c12ffdd3ee`.
- Báo cáo: `Builds/Android/Phase5/apk-verification.json`, `build-status.txt`, `build-messages.txt`. Build còn cảnh báo API Unity cũ, shader của package và SDK chỉ đọc; không có lỗi build. Cảnh báo Diagnostics thiếu native symbols đầy đủ giới hạn việc giải mã stack crash native.

## Cần thử trên điện thoại trước khi chốt giai đoạn 5

1. Cài APK, chạy khởi động nguội và chọn lần lượt 10 loại thuyền.
2. Kiểm tra nút chạm giữ nhiều ngón, nhả tay, item, reset, góc nhìn thứ nhất; không che UI ở màn hình có tai thỏ.
3. Bật nghiêng, cân bằng ở tư thế cầm; đổi landscape trái/phải, đưa app ra nền rồi mở lại; không còn lệnh lái bị giữ.
4. Chạy đủ Cúp 3 lượt với PPO, vật phẩm dày và vật cản ngẫu nhiên; kiểm tra nhánh sông, cửa hang, thác, va bờ và về đích.
5. Đo frame time/FPS và bộ nhớ bằng Unity Profiler hoặc công cụ thiết bị, chơi liên tục tối thiểu 15 phút để phát hiện nóng máy/giảm xung; ghi rõ model máy, Android, độ phân giải và mức AI. Không kết luận 60 FPS chỉ từ mục tiêu cấu hình hoặc số đo Editor.
6. Thử phòng mã Internet giữa hai thiết bị và gián đoạn mạng. Vật phẩm/Cúp vẫn chỉ có trong đấu máy theo phạm vi giai đoạn 4.

Chưa đánh dấu hoàn tất phần thử máy thật khi chưa có kết quả thực tế.
