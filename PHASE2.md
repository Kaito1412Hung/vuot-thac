# Giai đoạn 2 — Đua với AI dự phòng

Bản Unity triển khai ngày 07/11/2026. Chỉ dùng map Suối Rừng hiện có; không dựng lại hoặc sửa cảnh quan.

Bổ sung ngày 08/11/2026: 5 độ khó đấu máy và phòng bạn bè qua Internet; hướng dẫn, kiểm tra và giới hạn hiện tại xem [ONLINE.md](ONLINE.md).

## Đã triển khai

- Người chơi chọn một trong 10 thuyền, đua cùng 5 đối thủ. Sáu thuyền đứng cạnh nhau trên một hàng ngang, mũi thuyền cùng vạch và có khoảng hở giữa các làn. Đếm ngược 3 giây; các thuyền được giữ đứng yên đến lúc cùng xuất phát.
- AI điều khiển qua `IBoatInput`, dùng cùng vật lý nổi, lực chèo và giới hạn lái với người chơi. Không dùng LLM hoặc model ML-Agents ở bước này.
- Lộ trình lấy từ spline chính và các nhánh. AI chọn một trong hai nhánh đầu để vòng qua vật chắn sông; một số đối thủ đi thêm nhánh thứ ba. Giảm tốc trước ngã rẽ.
- FSM: Racing, Avoiding, Overtaking, Recovering, Finished. Cảm biến cầu/quét trước và hai phía, bỏ qua thân thuyền của chính nó, xử lý đá, bờ và thuyền khác. AI quay về lộ trình sau khi tránh.
- Gỡ kẹt đo tiến trình bằng mét: không tiến ít nhất 0,8m trong 2,5 giây thì thử lùi và xoay trong 1,6 giây. Sau ba lần thử, dùng hồi phục về checkpoint đã qua. Đây là phục hồi khi kẹt, không phải cơ chế tăng tiến trình.
- Rubber-banding chỉ điều chỉnh nhẹ lực chèo theo khoảng cách với người chơi. Không thay vị trí, vận tốc hay checkpoint để bắt kịp; giới hạn lực của BoatController vẫn áp dụng.
- 12 checkpoint tuần tự từ xuất phát đến đích. Khoảng cách trên nhánh được quy đổi về khoảng tương đương dọc sông chính để xếp hạng. Không nhận mốc khi dịch chuyển xa bất thường, ở quá cao/thấp so với mặt nước hoặc chưa vượt mốc theo thứ tự.
- Hồi vị đưa thuyền về mốc đã qua gần nhất. HUD hiện thứ hạng /6, tốc độ, tiến trình, số checkpoint và thời gian.
- Bảng kết quả giữ thời gian/thứ hạng của người đã về đích; đối thủ chưa xong tiếp tục đua. AI đi tiếp khỏi vạch rồi đỗ, nhường đường cho các thuyền sau.
- Chọn lại thuyền/chơi lại dọn toàn bộ đối thủ và tiến trình cũ.

## Chạy thử

Mở `Assets/_Game/Scenes/Track01_SuoiRung.unity`, vào Play Mode, chọn thuyền rồi bấm BẮT ĐẦU. Điều khiển người chơi giữ như giai đoạn 1. Trong Editor, F3 bật/tắt tia cảm biến và hướng dẫn đường của AI; dùng Scene View với Gizmos để quan sát.

## Kết quả kiểm tra

| Kiểm tra | Kết quả |
|---|---|
| Cuộc đua với collider thực của map, cả 6 thuyền do AI điều khiển để kiểm tra tự động | 6/6 về đích; đủ 12 checkpoint; thứ hạng cuối từ 1 đến 6; lượt chạy lại sau khi đổi sang hàng ngang hoàn thành sau khoảng 135 giây mô phỏng |
| Hồi phục trong lượt đua cuối | 2 lần AI dùng phục hồi checkpoint; các thuyền sau đó hoàn thành tuyến. Chưa coi AI là không bao giờ kẹt |
| Luật đua | 7/7: cổng xuất phát, chặn nhảy thẳng đến đích, chặn mốc khi trên cao, thứ tự checkpoint, hồi vị về mốc đã qua, hoàn thành đủ mốc, thời gian về đích không đổi |
| UI và chơi lại | Có HUD hạng/checkpoint, bảng kết quả và nút chơi lại; sau quay lại chọn thuyền còn 1 preview, 0 AI, 0 racer; bắt đầu lại tạo đúng 6 racer |
| Đếm ngược | 6 thuyền đứng yên trong 3 giây chờ |
| Hàng ngang xuất phát với cả 10 lựa chọn thuyền người chơi | 10/10 đạt: mũi cùng vạch, tất cả ở trên nước, không chồng collider; báo cáo `Builds/Validation/phase2-start-grid.txt` |

Kiểm tra tự động trong Play Mode bằng `VuotThac.EditorTools.Boats.PhaseTwoValidation.RunRace()` và `RunRules()`. Báo cáo: `Builds/Validation/phase2-race.txt`, `phase2-rules.txt`. Bài kiểm tra tự động dùng vật lý thực của scene, điều khiển bước thời gian cố định và tạm ẩn thuyền ngoài cuộc kiểm tra; không mô phỏng thao tác người chơi trên điện thoại.

## Các bước còn cần xác nhận

- Chơi trọn tuyến bằng tay với nhiều loại thuyền và kiểm tra mức độ cạnh tranh của đối thủ. AI vẫn có thể cần tinh chỉnh ở tình huống giao thông khác các lượt đã kiểm tra.
- Build lại APK, thử điều khiển và hiệu năng khi có 6 thuyền trên điện thoại. Chưa có kết quả FPS/nhiệt máy cho giai đoạn 2.
- APK giai đoạn 1 hiện có chưa chứa các tính năng này. ML-Agents tiếp tục ở giai đoạn 3; vật phẩm ở giai đoạn 4.
