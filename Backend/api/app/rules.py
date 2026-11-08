"""Luật kiểm tra kết quả, lấy từ code game (RaceProgress, BoatDefinition, BoatSelectPhaseFour).

Server không chạy lại cuộc đua nên chỉ chặn được gian lận đơn giản: thời gian phi lý,
thiếu checkpoint hoặc sai thứ tự. Người sửa cả game vẫn có thể gửi số liệu trông hợp lệ.
"""

import gzip
import struct
import zlib
from math import isfinite

BOAT_IDS = frozenset({
    "BeTre", "CaNoMay", "DocMoc", "Kayak", "PhaoCaoSu",
    "TamBan", "ThuyenBuom", "ThuyenRong", "ThuyenThung", "XuongBaLa",
})

CHECKPOINT_COUNT = 12
GATE_SPACING_M = 80.0
MAX_RACE_MS = 420_000  # Game xử thua khi hết 420 giây
# Thuyền nhanh nhất 15 m/s, tăng tốc nhân 1,4 thành 21 m/s, cộng dòng chảy và thác: 30 m/s là rất rộng rãi.
MAX_SPEED_MPS = 30.0
MIN_GATE_SPLIT_MS = int(GATE_SPACING_M / MAX_SPEED_MPS * 1000)

GHOST_MAGIC = b"VTG1"
GHOST_HZ = 10
GHOST_HEADER = struct.Struct("<4sHI")  # magic, số mẫu mỗi giây, số khung
GHOST_FRAME = struct.Struct("<fffh")  # x, y, z (mét), hướng (phần trăm độ)
MAX_GHOST_GZIP_BYTES = 128 * 1024
MAX_GHOST_RAW_BYTES = GHOST_HEADER.size + GHOST_FRAME.size * (MAX_RACE_MS // 1000 + 2) * GHOST_HZ
GHOST_FRAME_TOLERANCE = 20  # Lệch tối đa 2 giây giữa số khung và thời gian về đích
MAX_GHOST_JUMPS = 12  # Mỗi lần hồi về checkpoint là một cú nhảy hợp lệ
MAX_FRAME_MOVE_M = MAX_SPEED_MPS / GHOST_HZ * 1.5


class RuleError(ValueError):
    """Dữ liệu vi phạm luật. Thông báo được gửi về cho game."""


def check_race_times(time_ms: int, checkpoints: list[int]) -> None:
    if len(checkpoints) != CHECKPOINT_COUNT:
        raise RuleError(f"Cần đủ {CHECKPOINT_COUNT} checkpoint")
    if time_ms > MAX_RACE_MS:
        raise RuleError("Thời gian vượt giới hạn đường đua")
    if checkpoints[-1] != time_ms:
        raise RuleError("Checkpoint cuối phải trùng thời gian về đích")
    if checkpoints[0] < 0:
        raise RuleError("Thời gian checkpoint không hợp lệ")
    for index in range(1, CHECKPOINT_COUNT):
        split = checkpoints[index] - checkpoints[index - 1]
        if split <= 0:
            raise RuleError("Checkpoint phải đi đúng thứ tự")
        # Cổng cuối nằm ở vạch đích nên đoạn cuối có thể ngắn hơn 80 m
        if index < CHECKPOINT_COUNT - 1 and split < MIN_GATE_SPLIT_MS:
            raise RuleError("Tốc độ giữa hai checkpoint phi lý")


def check_ghost(data: bytes, time_ms: int) -> int:
    """Kiểm tra ghost dạng gzip, trả về số khung."""
    if len(data) > MAX_GHOST_GZIP_BYTES:
        raise RuleError("Ghost quá lớn")
    # Giải nén có giới hạn để chặn file nén cực nhỏ nhưng bung ra rất lớn
    decoder = zlib.decompressobj(wbits=16 + zlib.MAX_WBITS)
    try:
        raw = decoder.decompress(data, MAX_GHOST_RAW_BYTES + 1)
    except zlib.error as error:
        raise RuleError("Ghost không phải gzip hợp lệ") from error
    if len(raw) > MAX_GHOST_RAW_BYTES or not decoder.eof or decoder.unused_data:
        raise RuleError("Ghost quá lớn hoặc bị hỏng")
    if len(raw) < GHOST_HEADER.size:
        raise RuleError("Ghost thiếu phần đầu")
    magic, hz, count = GHOST_HEADER.unpack_from(raw)
    if magic != GHOST_MAGIC or hz != GHOST_HZ:
        raise RuleError("Sai định dạng ghost")
    if len(raw) != GHOST_HEADER.size + count * GHOST_FRAME.size:
        raise RuleError("Số khung ghost không khớp dữ liệu")
    if abs(count - round(time_ms / 1000 * GHOST_HZ)) > GHOST_FRAME_TOLERANCE:
        raise RuleError("Độ dài ghost không khớp thời gian về đích")
    jumps = 0
    previous = None
    for x, y, z, _heading in GHOST_FRAME.iter_unpack(raw[GHOST_HEADER.size:]):
        if not (isfinite(x) and isfinite(y) and isfinite(z)):
            raise RuleError("Toạ độ ghost không hợp lệ")
        if previous is not None:
            dx, dz = x - previous[0], z - previous[1]
            if dx * dx + dz * dz > MAX_FRAME_MOVE_M * MAX_FRAME_MOVE_M:
                jumps += 1
        previous = (x, z)
    if jumps > MAX_GHOST_JUMPS:
        raise RuleError("Ghost có quá nhiều cú nhảy vị trí")
    return count


def encode_ghost(frames: list[tuple[float, float, float, int]]) -> bytes:
    """Mẫu định dạng ghost, dùng cho test và làm tài liệu cho phía Unity."""
    raw = GHOST_HEADER.pack(GHOST_MAGIC, GHOST_HZ, len(frames)) + b"".join(GHOST_FRAME.pack(*frame) for frame in frames)
    return gzip.compress(raw)
