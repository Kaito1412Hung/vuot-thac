# Giai đoạn 4 — Vật phẩm và Cúp Suối Rừng

Trạng thái 08/11/2026: đã triển khai và kiểm tra prototype trong Unity Editor. Game tiếp tục dùng một map Suối Rừng. Đã có APK Android 0.5.0 ở giai đoạn 5, chưa kiểm tra chơi thực tế trên điện thoại; xem [PHASE5.md](PHASE5.md).

## Chơi thử

Mở `Assets/_Game/Scenes/Track01_SuoiRung.unity`, vào Play. Trong màn chọn thuyền:

- Nút **CHẾ ĐỘ** đổi giữa đua một lượt và **Cúp 3 lượt**.
- Chọn mức độ khó, chọn PPO hoặc FSM rồi bắt đầu. Sáu thuyền vẫn xuất phát cùng hàng.
- Nhặt khối vật phẩm nổi trên nước; mỗi thuyền chỉ giữ một vật phẩm. Nhấn **E**, nút **X trên tay cầm**, hoặc chạm nút vật phẩm ở giữa dưới màn hình để dùng.

| Vật phẩm | Hiệu ứng |
|---|---|
| Tăng tốc, màu vàng | Tăng lực đẩy 1,75 lần và tốc độ cân bằng 1,4 lần trong 2 giây. Tác động qua lực vật lý, không dịch chuyển thuyền |
| Khiên, màu xanh | Đỡ lần va chạm đủ mạnh kế tiếp hoặc một lần trúng gỗ; không nhận giảm tốc/stun của lần đó. Vòng xanh hiển thị khi đang bảo vệ |
| Khúc gỗ, màu nâu | Thả phía sau, tồn tại tối đa 8 giây và trôi theo nước. Thuyền trúng còn 45% vận tốc ngang và bị cản chèo 1,2 giây; mỗi khúc chỉ đánh một thuyền một lần, miễn chủ thuyền |

Không dùng vật phẩm trong đếm ngược, sau khi về đích hoặc hết giờ. Đã tăng từ 19 lên **tối đa 240 điểm nhặt**, đặt thành nhiều vị trí ngang sông, khoảng 18–24 m một cụm, có lệch vị trí ngẫu nhiên. Điểm nhặt hồi lại sau **5 giây**; loại vật phẩm cũng ngẫu nhiên. Các điểm được lọc theo mặt nước/vật cản và phủ đủ ba nhánh sông. Pool gỗ đã tăng lên 24, không tạo vô hạn vật thể.

## Bố cục vật cản mỗi lượt

- Bắt đầu lại hoặc sang lượt Cúp tạo seed mới, đổi vị trí/loại vật phẩm và bố cục đá/gỗ trong dòng nước. Có thể truyền seed cụ thể vào `BoatRace.Begin` để chạy lại một bố cục khi kiểm tra lỗi.
- `RaceLayout` tạm thay các đá thuộc `RiverRocks` và `BranchRock_` bằng đá/gỗ ngẫu nhiên: chọn mesh đá, kích thước, góc và vị trí khác nhau. Các seed đã thử tạo khoảng 28–37 vật cản. Đá bờ, công trình hang, đá chân thác và chướng ngại bắt buộc rẽ nhánh được giữ nguyên.
- Tránh vùng xuất phát/đích, cửa nhánh, sát thác, bờ và collider hiện có. Tâm vật cản mới cách các lộ trình ít nhất 4,2 m để chừa lối đi, thay vì xếp kín ngang sông.
- Vật cản mới chỉ tồn tại trong cuộc đua offline; thoát/chơi lại dọn bố cục cũ và khôi phục đá gốc. Không dựng lại hoặc lưu sửa cảnh quan trong scene.

AI cũng nhặt/dùng vật phẩm. Quyết định dùng vật phẩm là luật bổ sung: tăng tốc khi sông chính thông thoáng, tránh cửa nhánh và gần thác; bật khiên khi có; thả gỗ khi có thuyền phía sau. Model PPO tiếp tục điều khiển lái/chèo; **chưa train lại model để học chiến thuật vật phẩm**, không coi các luật dùng vật phẩm là kỹ năng học được của model.

## Cúp và kết quả

- Đua một lượt bốc ngẫu nhiên **5 loại thuyền máy trong cả 10 loại**, không trùng loại giữa năm máy; vị trí các loại thuyền trên hàng xuất phát cũng thay đổi theo đội hình. Thuyền người chơi vẫn do người chơi chọn.
- Cúp bốc đội máy mới lúc bắt đầu, giữ năm đối thủ đó qua ba lượt trên cùng map để cộng điểm **10 / 8 / 6 / 4 / 2 / 1** theo thứ hạng về đích. Chơi Cúp mới bốc lại đội khác.
- Bố cục vật phẩm/vật cản được bốc mới mỗi lượt. Sang lượt tiếp theo dọn thuyền/vật phẩm cũ, tạo kho trống và đếm ngược lại.
- Chỉ chốt điểm một lần khi các thuyền đều xong hoặc kết thúc thời gian chờ. Thuyền chưa về đích được 0 điểm.
- Sau khi người chơi về đích, các thuyền còn lại có tối đa 45 giây. Tổng thời gian một lượt tối đa 420 giây; chưa xong được đánh dấu hết giờ, không được cộng checkpoint hay thời gian về đích giả.
- Hòa điểm xét số lần thắng, thứ hạng lượt cuối, rồi thứ tự người tham gia cố định. Không có điểm do chưa từng về đích thì không được nhận cúp thắng.
- Kết thúc Cúp có vàng/bạc/đồng cho top 3; có bảng điểm, nút lượt tiếp, chơi lại và chọn thuyền. Nút lượt tiếp khóa trong lúc chờ chốt kết quả.
- Thành tích thời gian và hạng Cúp tốt nhất lưu cục bộ bằng PlayerPrefs. Chưa lưu cloud hoặc mở khóa thuyền.

Phòng bạn bè vẫn là đua thuần kỹ năng: **chưa áp dụng vật phẩm và Cúp online**. Không gửi các hiệu ứng mới qua mạng khi chưa có xử lý quyền quyết định và đồng bộ chúng.

## Kiểm tra đã chạy

- `PhaseFourValidation.Run()` hiện đạt **31/31**: kho một ô, cấm dùng trước xuất phát, hết hạn tăng tốc, khiên một lần, gỗ làm chậm/không đánh chủ/không đánh lặp/hết hạn, nhặt thực, dọn khi thoát, đủ ba lượt Cúp, chống cộng điểm trùng, chơi lại, chờ 45 giây, hết giờ và Cúp không về đích; thêm ba kiểm tra giữ đúng đội máy qua từng lượt Cúp.
- `PhaseFourValidation.RunRace()` chạy vật lý thật với sáu thuyền do PPO điều khiển, có vật phẩm: **6/6 về đích, đủ 12 checkpoint**, kết thúc sau **166,02 giây**. Tổng **21 lượt nhặt, 19 vật phẩm được dùng**, có cả ba loại; ba khiên đã hấp thụ va chạm. Đây là một lượt tích hợp, không phải thống kê cân bằng game.
- Console Unity không có lỗi sau các bài kiểm tra. Đã xem ảnh màn chọn để kiểm tra bố cục nút chế độ, độ khó và nguồn AI.
- Sau thay đổi tăng mật độ/ngẫu nhiên: `RaceSpawnValidation.Run()` đạt **24/24** trên năm ca, xác nhận cùng seed tái hiện được, khác seed đổi cả vật phẩm/vật cản, chơi lại tự đổi seed, đủ điểm trên mọi nhánh và khôi phục đá gốc khi thoát. Cả năm ca có 240 điểm nhặt và hành lang dẫn đường đạt giới hạn 4,2 m.
- Ba lượt tích hợp bố cục ngẫu nhiên đều **6/6 về đích**: seed `-430327670` có 31 vật cản, dùng 48 vật phẩm; seed `-1702756168` có 37 vật cản, dùng 42 vật phẩm; seed `1192514895` có 34 vật cản, dùng 27 vật phẩm. Kiểm tra luật/UI vẫn **28/28**; lượt xác nhận cuối không có lỗi Console. Chi tiết thêm ở `Builds/Validation/random-spawns.txt` và `random-race-1/2/3.txt`.
- Bổ sung đội máy ngẫu nhiên: 100 seed tạo **100 đội hình khác nhau**, mỗi đội 5 loại không trùng, đã chọn đủ **10/10 loại**. Hai lần bắt đầu đua qua UI cho hai đội khác nhau và đều có 5 agent `InferenceOnly`. Nguồn thuyền người chơi không bị thay đổi.
- Model bổ sung `suoirung-ppo-allboats-v1` train thêm **80.028 bước** từ V5, dùng dữ liệu đủ 10 loại. Bốn nhóm đua bao phủ cả 10 loại và kiểm tra thêm vị trí/nhánh khác nhau đều **6/6 qua 12 checkpoint**. Báo cáo: `Builds/Validation/random-boats-group-0/1/2/3.txt`, `opponent-rosters.txt`, `random-ai-ui.txt`. Thân dài hoặc thuyền nhanh có tốc độ quay hạn chế được dẫn vào cửa nhánh ít gấp; điều khiển lái/chèo vẫn do model quyết định.
- Kết quả chi tiết: `Builds/Validation/phase4-rules.txt`, `Builds/Validation/phase4-race.txt`. Script kiểm tra chỉ nằm trong assembly Editor; kết quả được mô phỏng để kiểm tra luật Cúp không được lưu thành kỷ lục người chơi.

## Code chính

- `BoatItemState`: kho, hiệu ứng và vòng khiên.
- `RaceItems`: điểm nhặt, pool gỗ, kiểm tra trúng và quyết định dùng vật phẩm của máy.
- `RaceLayout`: seed và vật cản ngẫu nhiên từng lượt, khôi phục bố cục gốc khi dọn cuộc đua.
- `BoatController`: lực tăng tốc và xử lý tác động khiên/gỗ.
- `CupSeries`: tính điểm và phân hạng ba lượt.
- `BoatSelectPhaseFour`: HUD, chuyển lượt, kết quả, hết giờ và thành tích cục bộ.

Các việc còn phải đo: Android và cảm ứng trên thiết bị thật, hiệu năng, cân bằng vật phẩm/độ khó qua nhiều lượt, chơi cùng người thật. Vật phẩm online cần bước triển khai riêng; APK cũ chưa có giai đoạn 4.
