# Giai đoạn 6 — Backend

Trạng thái 08/11/2026: đã chạy trên máy bằng Docker Compose, game trong Unity Editor gửi và nhận được dữ liệu thật. Chưa đưa lên server, chưa kiểm tra trên điện thoại. Chi tiết API xem [Backend/README.md](Backend/README.md).

## Chơi thử

1. Mở Docker Desktop, chạy Backend theo [Backend/README.md](Backend/README.md).
2. Mở scene `Track01_SuoiRung` trong Unity, bấm Play.
3. Chơi xong một lượt: màn kết quả hiện thứ hạng trên bảng xếp hạng.
4. Ở màn chọn thuyền:
   - **BẢNG XẾP HẠNG:** xem theo Đua 1 lượt, Cúp, Hôm nay, lọc theo thuyền, đổi tên. Bấm **ĐUA BÓNG MA** để chọn đối thủ, rồi **BẮT ĐẦU**.
   - **CHẾ ĐỘ:** bấm tới **THỬ THÁCH NGÀY** để chơi bố cục và độ khó server chọn cho ngày đó.

## Đã làm trong game

- **Tài khoản ẩn danh:** tự tạo khi cần lần đầu, token lưu trên máy. Token bị từ chối thì tự tạo tài khoản mới.
- **Gửi kết quả:** ghi thời gian từng checkpoint, về đích là gửi. Chỉ gửi đấu máy và Cúp, không gửi đua cùng bạn bè. Cúp lưu thời gian từng lượt, chưa xếp hạng theo điểm cả Cúp.
- **Mất mạng:** kết quả lưu thành file trong `persistentDataPath/backend_runs` và tự gửi lại khi mở game hoặc mở bảng xếp hạng. Giữ tối đa 50 lượt. Máy chủ từ chối ghost thì gửi lại không kèm ghost để giữ thành tích.
- **Bóng ma:** ghi 10 mẫu mỗi giây. Bóng ma trong suốt, không va chạm, không ảnh hưởng thứ hạng và AI. Hết đua hiện thắng hay thua bóng ma bao nhiêu giây.
- **Thử thách ngày:** cùng seed nên cùng bố cục, vật phẩm và đội máy trong cùng phiên bản game.
- **Tiến độ:** đồng bộ thời gian tốt nhất và hạng Cúp tốt nhất, lấy giá trị tốt hơn giữa máy và server.

## Địa chỉ server

Sửa `ProductionUrl` trong [BackendClient.cs](Assets/_Game/Scripts/Runtime/Backend/BackendClient.cs). Editor và bản Development dùng `http://localhost:8088`. Bản phát hành đang để trống nên **tự ẩn mọi tính năng ở trên và không gọi mạng**, phòng bạn bè qua Unity Relay vẫn hoạt động. Giai đoạn 7 điền địa chỉ HTTPS rồi build lại APK.

## Kiểm tra ngày 08/11/2026

Chạy trong Play Mode, Backend trong Docker, AI lái thuyền người chơi trên đường đua thật với tốc độ thời gian gấp 3 lần.

| Kiểm tra | Kết quả |
|---|---|
| Test tự động Backend trên PostgreSQL thật | 46/46 đạt |
| Đua thật, gửi kết quả | Đạt: 12 checkpoint, về đích 88,4 giây, server xếp hạng và lưu ghost |
| Ngưỡng chống gian lận với lượt thật | Không từ chối nhầm ở 5 lượt. Đoạn ngắn nhất giữa hai cổng đo được khoảng 4,7 giây, ngưỡng là 2,67 giây |
| Bảng xếp hạng và màn kết quả | Đạt, có hiện hạng của mình |
| Đua với bóng ma | Đạt: tải được, chạy đúng đường, không có va chạm, báo thua 4,8 giây |
| Thử thách ngày | Đạt: seed và độ khó khớp server, bảng riêng theo ngày |
| Cúp | Đạt với 1 lượt, chưa chạy trọn 3 lượt có gửi kết quả |
| Tắt server rồi về đích | Đạt: báo sẽ gửi lại, lưu file 18,7 KB; mở lại game và bật server thì tự gửi, kèm ghost |
| Xoá thành tích trên máy rồi đồng bộ | Đạt: lấy lại từ server |
| Token sai | Đạt: tự tạo tài khoản mới |
| Bài kiểm tra cũ | Luật đua 7/7, vật phẩm và Cúp 31/31 |
| Console | Không có lỗi |

## Chưa kiểm tra và giới hạn

- **Chưa build lại APK và chưa thử trên điện thoại.** Chưa xác nhận đọc/ghi file hàng chờ, nén ghost bằng IL2CPP và độ trễ mạng di động.
- **Chưa có người chơi thật thử bằng tay:** các lượt trên do AI lái. Giao diện được kiểm tra bằng ảnh chụp ở 1920×1080 và gọi hàm, chưa bấm thử trên màn cảm ứng.
- **Mất token là mất tài khoản:** gỡ game thì không lấy lại được, vì chưa có đăng nhập khôi phục.
- **Chống gian lận chỉ ở mức cơ bản:** xem [Backend/README.md](Backend/README.md).
- **Thử thách ngày phụ thuộc phiên bản game:** đổi luật sinh bố cục thì cùng seed có thể ra bố cục khác.
- **Bóng ma đi xuyên chướng ngại:** bố cục đá và gỗ mỗi lượt mỗi khác, trừ thử thách ngày.
- **Tài khoản nằm trong PlayerPrefs:** Editor và bản Windows trên cùng máy là các tài khoản khác nhau.
