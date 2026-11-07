namespace VuotThac.Boats
{
    /// <summary>
    /// Nguồn điều khiển thuyền. Người chơi, AI dự phòng dùng FSM và AI ML-Agents sau này đều điều khiển
    /// thuyền qua cùng giao diện này, nên khi so sánh trong báo cáo sẽ công bằng.
    /// </summary>
    public interface IBoatInput
    {
        /// <summary>Bẻ lái từ -1 (trái) đến 1 (phải).</summary>
        float Steer { get; }

        /// <summary>Chèo từ -1 (lùi) đến 1 (tới hết sức).</summary>
        float Throttle { get; }

        /// <summary>Trả về true một lần khi người chơi muốn đưa thuyền về giữa dòng.</summary>
        bool ConsumeReset();
    }
}
