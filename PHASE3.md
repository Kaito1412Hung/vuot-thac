# Giai đoạn 3 — Model AI được huấn luyện

Trạng thái ngày 08/11/2026: **đã hoàn thành phần triển khai và kiểm tra prototype trong Unity Editor**: model BC/PPO được huấn luyện thực, ONNX đã tích hợp vào 5 đối thủ, có lựa chọn PPO/FSM và báo cáo so sánh. Chưa nghiệm thu Android, hiệu năng điện thoại hoặc chất lượng cân bằng độ khó ở quy mô nhiều lượt chơi.

## Model hiện tại — bổ sung đủ 10 loại thuyền

- `Assets/_Game/Resources/AI/SuoiRungBoat.onnx` hiện dùng run **suoirung-ppo-allboats-v1**, SHA-256 `2a9b75bf0bd7101a6d7577316e455540cfa5fa623995973309fa9a978d8a242f`.
- Train tiếp **80.028 bước** từ V5, cùng kiến trúc và schema 4. Tổng chuỗi v4 → v5 → allboats là **276.592 bước**; các phép hiệu chỉnh tương thích không được tính là train.
- Ghi mẫu đủ 10 thân thuyền, dùng 19 tệp trong `Training/Demos-allboats`; loại khỏi tập học một tệp thuyền rồng đi nhánh trái không hoàn thành được tuyến. Bộ chọn lộ trình ưu tiên cửa nhánh ít gấp cho thân dài hoặc thuyền nhanh có tốc độ quay hạn chế. Chuyên gia/agent học không nhìn các thuyền học khác như vật cản khi va chạm giữa chúng đã được bỏ qua.
- Cấu hình: `Training/config/boat_allboats.yaml`, mức episode toàn tuyến, 10 agent học. Chạy lại từ V5 bằng `Training/train.ps1 -RunId <ten-moi> -InitializeFrom suoirung-ppo-v5 -Config Training/config/boat_allboats.yaml`, kết nối menu **Train all boat types**. Menu **Record all boat types** ghi dữ liệu mẫu cả danh sách thuyền.
- Kiểm tra tuyến gốc mức Thường: **6/6**, trung bình **131,76 giây**, 37 va chạm, 2 phục hồi. Kiểm tra có vật phẩm/bố cục ngẫu nhiên: bốn nhóm bao phủ đủ 10 loại đều **6/6 về đích**. Chưa chạy lại đủ 5 mức trên model bổ sung; bảng bên dưới là số đo V5 ban đầu.
- Metadata đã qua kiểm tra schema/hash/giới hạn phục hồi; suy luận vẫn không dùng FSM làm nguồn hành động. Báo cáo train: `Training/Reports/suoirung-ppo-allboats-v1.json`. Android chưa kiểm tra.

## Model V5 ban đầu và số liệu tham chiếu

- Model V5 cũ: `Training/results/suoirung-ppo-v5/SuoiRungBoat.onnx`, SHA-256 `040d3439ab1908b3599b1c261918c844228e72f2a4d7b2eb87aad03ca5924a3b`. Các số đo trong mục này không phải số đo model bổ sung hiện tại; tổng hợp cũ được giữ ở `Training/Reports/phase3-summary-v5.json` và `phase3-races-v5.csv`.
- V5 train thêm **75.324 bước**, khởi tạo từ v4 đã train **121.240 bước** sau hiệu chỉnh thang hành động. Tổng hai lượt nối tiếp là **196.564 bước**; không tính các lượt thử v1–v3 đã loại bỏ.
- Hai lớp ẩn 128 nút, 42 đầu vào, 2 đầu ra liên tục. Mạng được train trong project từ dữ liệu FSM và PPO, không phải model tải sẵn hoặc LLM.
- Các phép đo dùng 6 thuyền, vật lý/collider thật, đủ 12 checkpoint và va chạm giữa thuyền. Số lần phục hồi là đưa về checkpoint đã qua, không tự cộng checkpoint hoặc đưa tới đích.

| Bộ điều khiển / mức | Về đích | Thời gian trung bình (giây) | Tổng va chạm | Tổng phục hồi |
|---|---:|---:|---:|---:|
| FSM / Thường | 6/6 | 117,76 | 52 | 2 |
| PPO / Rất dễ | 6/6 | 143,56 | 37 | 2 |
| PPO / Dễ | 6/6 | 153,14 | 45 | 3 |
| PPO / Thường | 6/6 | 153,36 | 44 | 3 |
| PPO / Khó | 6/6 | 154,35 | 62 | 4 |
| PPO / Rất khó | 6/6 | 153,51 | 45 | 3 |

Đây là một batch mỗi mức, không phải thống kê đủ lớn để kết luận mức Khó luôn nhanh hơn mức Dễ. PPO hiện **chậm hơn FSM**, và vẫn cần phục hồi khi kẹt; không coi model là đã vượt FSM. Cần đo nhiều lượt để cân bằng độ khó. Bài đổi ngược các làn xuất phát ở mức Thường cũng đạt **6/6**, tổng 2 lần phục hồi. Báo cáo từng thuyền, hash model, dữ liệu TensorBoard và hash dữ liệu mẫu nằm trong `Training/Reports`.

Kiểm tra luồng game thực: màn chọn hiển thị `AI: PPO`, bắt đầu đua tạo **6 thuyền, gồm 5 `BoatLearningAgent` ở chế độ `InferenceOnly` và 0 `BoatAIInput`**. Hai kiểm tra hồi quy miền hành động BC đạt; Console Unity không có lỗi sau kiểm tra.

## Mục tiêu và cách làm

- Model mạng nơ-ron 2 lớp ẩn, mỗi lớp 128 nút, nhận 42 quan sát và trả về 2 hành động liên tục: bẻ lái và chèo.
- ML-Agents Unity 4.2.0, Python trainer 1.1.0, giao thức 1.5.0. Môi trường portable Python 3.10.11, PyTorch CPU 2.2.2, NumPy 1.23.5 và protobuf 3.20.3 nằm trong `Training` trên ổ D.
- Mục tiêu dẫn đường được lấy từ spline/nhánh, giống dữ liệu dẫn đường của bản FSM. Model học lệnh điều khiển; không lấy hành động từ FSM khi chạy inference.
- Ghi dữ liệu chuyên gia từ FSM để khởi tạo bằng Behavioral Cloning, sau đó cải thiện bằng PPO trên vật lý thật. Phải công bố việc dùng dữ liệu FSM trong báo cáo, không gọi đây là học hoàn toàn từ số không.
- Tám agent dùng nhiều thân thuyền, vị trí bắt đầu và góc mũi ngẫu nhiên. Cảnh quan chỉ tạm ẩn trong Play Mode huấn luyện; collider map giữ nguyên. Va chạm giữa các agent học được bỏ qua để không gây nhiễu các episode song song. Tương tác nhiều đối thủ cần kiểm tra riêng trong cuộc đua thật.

## Quan sát, hành động, phần thưởng

42 quan sát (schema 4) gồm vận tốc, tốc độ quay, hướng mục tiêu ở 6/12/24m, lệch khỏi lộ trình, khoảng còn lại, hướng trục đứng, dòng chảy, trạng thái mặt nước, góc dẫn đường tại khoảng nhìn trước theo tốc độ, tốc độ mục tiêu, thông số thân thuyền và 9 cặp quan sát cảm biến vật cản/loại thuyền. Không đưa hành động trước vào đầu vào để tránh lối tắt bắt chước lệnh cũ. Góc/tốc độ mục tiêu là dữ liệu dẫn đường; mạng vẫn phải tự dự đoán hai lệnh điều khiển.

Hai hành động đều trong [-1,1]: steer và lực chèo có dấu, bao gồm hãm/lùi. Thuyền vẫn dùng `BoatController` và `IBoatInput`; không đặt trực tiếp vận tốc để giả lập AI chạy tốt. Cùng model dùng cho năm mức, không phải năm mạng train riêng. Giữ nhịp quyết định 0,1 giây đã học; chỉ điều chỉnh lực chèo dương trên sông chính thông thoáng sau đoạn nhánh đầu, khi hướng ít lệch và cảm biến phía trước đủ trống. Lệnh hãm/lùi, cửa nhánh và thao tác cua giữ nhịp điều khiển chuẩn. Nếu không tiến lên trong 8 giây, agent dùng phục hồi checkpoint để tránh xoay tại chỗ vô hạn; số lần này được tính vào báo cáo.

Thưởng cho tiến lên dọc lộ trình và hoàn thành đoạn mục tiêu; trừ theo thời gian, va chạm, cạ bờ. Kết thúc thất bại khi lật, ra khỏi nước quá xa, kẹt hoặc hết thời gian. Curriculum tăng chiều dài episode từ 90 → 180 → 400m → toàn tuyến theo phần thưởng thực tế.

## Chạy lại

1. Trong Unity, mở Suối Rừng và chọn **Vuot Thac → AI → Record expert demonstrations** để ghi dữ liệu vào `Training/Demos-v4`. Tám thuyền ghi khoảng 6.000 quyết định mỗi thuyền trên toàn tuyến, với vị trí ngang và góc xuất phát thay đổi.
2. Mở PowerShell ở project, chạy `Training/train.ps1 -RunId <ten-luot-moi>`.
3. Khi trainer đang chờ, chọn **Vuot Thac → AI → Connect PPO trainer** trong Unity. Agent gửi quan sát qua cổng localhost 5004.
4. Dữ liệu huấn luyện, TensorBoard, checkpoint và ONNX được lưu trong `Training/results/<run-id>`; dùng `-Resume` để tiếp tục một lượt đã có.
5. Chọn **Stop training or recording** để dừng; dữ liệu ghi được flush khi thoát Play Mode. Marker huấn luyện được xóa để lần Play tiếp theo trở về game bình thường.

`Training/.python310/python.exe Training/report.py --run-id <ten-luot>` xuất thống kê TensorBoard thật, thông số quan sát/hành động và SHA-256 của dữ liệu/model vào `Training/Reports`. Checkpoint và cache lớn không đưa lên Git.

Đưa checkpoint vào để thử bằng `Training/.python310/python.exe Training/deploy.py --run-id <ten-luot> --candidate`, refresh Unity, vào Play rồi gọi `BoatLearningValidation.Run(true)`. Chỉ chạy `deploy.py` không có `--candidate` sau khi phép đo đạt 6/6, không quá 3 phục hồi mỗi thuyền/6 tổng và hash báo cáo khớp chính model đó. Metadata schema ngăn game dùng nhầm mạng cũ dù số lượng đầu vào vẫn là 42.

Trong màn chọn thuyền, nút **AI: FSM / AI: PPO** chọn nguồn điều khiển đối thủ. Model đã qua kiểm tra được chọn mặc định nếu người chơi chưa lưu lựa chọn khác; model thử không tự thay mặc định. Suy luận PPO không cần Python, trainer hay LLM; nó chạy bằng Unity Inference Engine.

## Các lượt phát triển và lỗi đã phát hiện

- `suoirung-ppo-v1`: khoảng 100.000 bước; bài tập ngắn có kết quả tốt nhưng phép đo toàn tuyến 300 giây đạt 0/6. Đây là checkpoint thất bại, không dùng tỷ lệ thành công episode ngắn để quảng cáo khả năng đua.
- `suoirung-ppo-v2`: khởi tạo từ v1, bổ sung dữ liệu mẫu toàn tuyến và vị trí lệch làn; lưu checkpoint 98.691 bước. Phát hiện bộ giải mã hành động đang ép lực hãm/lùi về 0 nên phải sửa trước khi đánh giá tiếp.
- `suoirung-ppo-v3`: dùng dữ liệu mới với lực chèo có dấu. `migrate_v2_actions.py` chỉ đổi tham số đầu ra lực chèo theo phép affine để khởi tạo tương thích; đây không phải một lượt train. V3 tiếp tục học bằng BC/PPO và phải đánh giá lại bằng vật lý thật.
- V3 đạt sai số bắt chước thấp nhưng vẫn thất bại ở nhánh đầu. Đối chiếu ONNX bằng Python và Unity cho đầu ra khớp nhau; thay đổi riêng đầu vào hành động cũ làm dự đoán thay đổi mạnh. Schema 4 bỏ hai đầu vào lệnh cũ và thay bằng góc dẫn đường/tốc độ mục tiêu.
- `suoirung-ppo-v4`: train mới từ đầu bằng schema 4, không khởi tạo từ mạng có lối tắt cũ. Chỉ số mất mát trên dữ liệu mẫu không thay thế phép đánh giá điều khiển khép kín trên toàn tuyến.
- Kiểm tra mã ML-Agents Python 1.1.0 cho thấy BC so sánh mẫu Gaussian thô với nhãn, trong khi `SimpleActor` và ONNX gửi `clamp(raw,-3,3)/3` tới môi trường. `Training/learn.py` sửa riêng loss BC để so sánh trong đúng miền hành động Unity; không sửa cache package hay thuật toán PPO. Hai kiểm tra trong `test_bc_domain.py` đối chiếu với phép đổi của SDK và kiểm tra hướng gradient đã đạt.
- `suoirung-ppo-v5`: khởi tạo từ v4 (121.240 bước) sau hiệu chỉnh đầu ra bằng `correct_bc_scale.py`, rồi train tiếp bằng loss BC đã sửa. Hiệu chỉnh trọng số là bước chuẩn bị tương thích, không được tính là một lượt train riêng. File log ghi rõ trainer dùng bản sửa này.
- `BoatLearningValidation.Run(...)` kiểm tra 6 thuyền, đủ 12 checkpoint và va chạm giữa thuyền trên Suối Rừng. Thuyền đã về đích được bỏ collider trong bài đo để không tạo chướng ngại giả tại cổng.

`Training/Dockerfile` chuẩn bị môi trường trainer. Docker daemon hiện chưa chạy nên lượt đầu dùng Python portable; chưa đánh dấu Docker là đã kiểm tra. Không cần cài hoặc thay Python hệ thống để chạy môi trường portable này.

Trên máy clone project chưa có runtime portable, dùng Python 3.10.1–3.10.12 tạo `Training/.venv`, rồi cài `Training/requirements.txt` vào môi trường đó. `train.ps1` tự tìm portable trước, sau đó tìm `.venv`; cache, model và thư mục tạm được đặt trong `Training` trên ổ chứa project. File `requirements.lock.txt` lưu các phiên bản thực đã dùng. Khi cài từ lock cần thêm index CPU PyTorch: `https://download.pytorch.org/whl/cpu`.

Dockerfile dùng build context `Training`: `docker build -t vuotthac-trainer Training`. Khi chạy cần mount thư mục project vào `/workspace` và chuyển cổng 5004 tới trainer; đây là hướng dẫn chuẩn bị, chưa phải kết quả kiểm tra Docker.

`Training/summarize.py` tổng hợp báo cáo hiện có thành `phase3-summary.json` và `phase3-races.csv`, đồng thời từ chối báo cáo PPO có hash khác model đang dùng. Chạy `Training/test_bc_domain.py` để kiểm tra lại bản sửa BC.

Khi chỉ tổng hợp các bài đã chạy lại cho model mới, dùng `Training/summarize.py --allow-partial --config config/boat_allboats.yaml`: các báo cáo hash cũ bị bỏ qua và được liệt kê rõ trong `omitted_older_model_reports`, không trộn thành kết quả model mới.

## Tiêu chí nghiệm thu

- Có ONNX thực được xuất từ lượt train, kèm cấu hình, seed, nhật ký và thông số model.
- Import vào Unity thành công và chạy inference mà không dùng bộ điều khiển FSM làm nguồn hành động.
- Đánh giá với cùng điều kiện spawn/route/loại thuyền cho model và FSM; báo cáo về đích, thời gian, va chạm, kẹt. Không dùng thành công trên các đoạn ngắn để khẳng định đã hoàn thành toàn tuyến.
- Nêu rõ phần chưa đạt, chưa kiểm tra trên Android và mức huấn luyện cần tăng thêm. Không đánh dấu giai đoạn 3 xong chỉ vì đã sinh được một file `.onnx`.
