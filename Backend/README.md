# Backend Vượt Thác

API lưu kết quả đua, bảng xếp hạng, ghost, thử thách ngày và tiến độ. Game đã nối vào, xem [PHASE6.md](../PHASE6.md). Gồm 3 phần chạy bằng Docker Compose:

| Phần | Công nghệ | Việc |
|---|---|---|
| `proxy` | Caddy | Cổng vào duy nhất, chặn dữ liệu trên 1 MB, tự cấp HTTPS khi có tên miền |
| `api` | FastAPI, Python 3.12 | Xử lý yêu cầu, chạy từ `api/app` |
| `db` | PostgreSQL 17 | Lưu dữ liệu, không mở cổng ra ngoài |

Phòng chơi cùng bạn bè không nằm ở đây: phần đó dùng Unity Lobby/Relay, xem [ONLINE.md](../ONLINE.md).

## Chạy trên máy

Mở Docker Desktop, rồi chạy trong thư mục `Backend`.

Tạo file cấu hình, sau đó mở `.env` và đổi `POSTGRES_PASSWORD`, `DAILY_SECRET`:

```bash
cp .env.example .env
```

Khởi động:

```bash
docker compose up --build -d
```

Kiểm tra:

```bash
curl http://localhost:8088/health
```

Trang tài liệu API: <http://localhost:8088/docs> (tắt được bằng `ENABLE_DOCS=false`).

Dừng, giữ nguyên dữ liệu:

```bash
docker compose down
```

Chạy test tự động. Test dùng database riêng tên `vuotthac_test`, không đụng dữ liệu thật:

```bash
docker compose --profile test run --rm --build api-test
```

## API

Dữ liệu dạng JSON, tên trường kiểu `camelCase`. Yêu cầu cần đăng nhập gửi header `Authorization: Bearer <token>`.

| Endpoint | Đăng nhập | Việc |
|---|---|---|
| `GET /health` | Không | Kiểm tra API và database |
| `POST /v1/players` | Không | Tạo tài khoản ẩn danh, trả về `token` một lần duy nhất |
| `GET`, `PATCH /v1/players/me` | Có | Xem, đổi tên hiển thị |
| `POST /v1/runs` | Có | Gửi kết quả về đích, có thể kèm ghost |
| `GET /v1/leaderboard` | Tuỳ chọn | Bảng xếp hạng, gửi token thì có thêm `me` |
| `GET /v1/ghosts/{id}` | Không | Tải ghost |
| `GET /v1/daily-challenge` | Không | Seed và độ khó của ngày |
| `GET`, `PUT /v1/save` | Có | Đọc, ghi tiến độ |

### Gửi kết quả

```json
{
  "clientRunId": "uuid do game tạo cho mỗi lượt",
  "mode": "single",
  "boatId": "Kayak",
  "difficulty": 2,
  "brain": "ppo",
  "timeMs": 120000,
  "checkpointTimesMs": [10000, 20000, 30000, 40000, 50000, 60000, 70000, 80000, 90000, 100000, 110000, 120000],
  "finishRank": 1,
  "layoutSeed": 12345,
  "dailyDate": null,
  "clientVersion": "0.5.0",
  "ghost": null
}
```

- `mode`: `single`, `cup` hoặc `daily`. `cup` lưu thời gian từng lượt trong Cúp, chưa xếp hạng theo điểm cả Cúp.
- `clientRunId`: gửi lại cùng mã khi mạng lỗi thì trả `200` với kết quả cũ, không bị tính hai lần. Lần đầu trả `201`.
- `checkpointTimesMs`: đủ 12 mốc tăng dần, mốc cuối bằng `timeMs`.
- `mode=daily`: cần `dailyDate`, `layoutSeed`, `difficulty` đúng như `/v1/daily-challenge`. Nhận kết quả của hôm nay và hôm qua.

### Bảng xếp hạng

`GET /v1/leaderboard?mode=single&boatId=Kayak&limit=50&offset=0`. Với `mode=daily` thêm `date=2026-11-08`, bỏ trống là hôm nay. Mỗi người chỉ hiện lượt tốt nhất. Bằng giờ thì ai đạt trước đứng trên.

### Tiến độ

`PUT /v1/save` gửi `{"revision": 0, "data": {...}}`, tối đa 32 KB. `revision` là phiên bản game đang giữ, lấy từ lần đọc gần nhất. Máy khác đã lưu mới hơn thì trả `409`, không ghi đè: game cần đọc lại rồi quyết định.

### Định dạng ghost

Ghi 10 mẫu mỗi giây, gói gzip rồi mã hoá base64 trong trường `ghost`. Dữ liệu nhị phân little-endian:

| Phần | Kiểu | Nội dung |
|---|---|---|
| Đầu file | 4 byte | Chữ `VTG1` |
| | `uint16` | Số mẫu mỗi giây, luôn là 10 |
| | `uint32` | Số khung |
| Mỗi khung | 3 × `float32` | Vị trí x, y, z theo mét |
| | `int16` | Hướng, đơn vị phần trăm độ |

Tối đa 128 KB sau khi nén. Mỗi người chỉ giữ ghost của lượt tốt nhất theo từng chế độ và thuyền. `GET /v1/ghosts/{id}` trả về đúng file gzip đã gửi.

## Kiểm tra gian lận

Số liệu lấy từ code game, nằm trong [`api/app/rules.py`](api/app/rules.py). **Nếu đổi luật đua trong Unity thì cần sửa file này theo.**

| Luật | Giá trị |
|---|---|
| Số checkpoint | 12, đúng thứ tự, mốc cuối bằng thời gian về đích |
| Thời gian tối đa | 420 giây, giống giới hạn trong game |
| Tốc độ tối đa giữa hai cổng | 30 m/s (cổng cách nhau 80 m, đoạn cuối được ngắn hơn) |
| Ghost | Độ dài khớp thời gian đua, lệch tối đa 2 giây; tối đa 12 cú nhảy vị trí |

**Giới hạn:** server không chạy lại cuộc đua, nên chỉ chặn được số liệu phi lý. Người sửa game vẫn có thể gửi kết quả trông hợp lệ, ví dụ nhanh nhưng dưới ngưỡng 30 m/s.

## Bảo mật

- Không commit file `.env`. Không đưa mật khẩu database hay `DAILY_SECRET` vào code Unity hoặc APK.
- Database chỉ lưu SHA-256 của token. Mất token là mất tài khoản, vì chưa có đăng nhập khôi phục.
- Mỗi IP tạo tối đa 20 tài khoản mỗi giờ, mỗi người gửi tối đa 30 kết quả mỗi phút. Bộ đếm nằm trong bộ nhớ nên về 0 khi API khởi động lại.
- API và database không mở cổng ra ngoài, chỉ đi qua Caddy.

## Đổi cấu trúc database

Sửa `api/app/models.py`, thêm file mới vào `api/migrations/versions`. API tự chạy migration khi khởi động. Test `test_models_match_migrations` báo lỗi nếu hai bên lệch nhau.

## Chưa làm

- Đưa lên server thật, tên miền, HTTPS và sao lưu database (giai đoạn 7).
