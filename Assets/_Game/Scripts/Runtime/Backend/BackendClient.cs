using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace VuotThac.Backend
{
    [Serializable] class PlayerResponse { public string id; public string displayName; public string token; }
    [Serializable] public class RunResponse { public string id; public string ghostId; public int rank; public bool isPersonalBest; public int timeMs; }
    [Serializable] public class LeaderboardEntry { public int rank; public string playerId; public string displayName; public string boatId; public int difficulty; public int timeMs; public string runId; public string ghostId; }
    [Serializable] public class LeaderboardResponse { public string mode; public int total; public LeaderboardEntry[] entries; public LeaderboardEntry me; }
    [Serializable] public class DailyChallenge { public string date; public int seed; public int difficulty; public string trackId; }
    [Serializable] class ApiError { public string detail; }
    [Serializable] class SaveData { public float bestTimeSeconds; public int cupBestRank; }
    [Serializable] class SaveResponse { public int revision; public SaveData data; }

    /// <summary>Một lượt đua chờ gửi. Lưu thành file để mất mạng hoặc tắt game vẫn không mất.</summary>
    [Serializable]
    public class PendingRun
    {
        public string clientRunId, mode, boatId, brain, dailyDate, clientVersion, ghost;
        public int difficulty, timeMs, finishRank, layoutSeed;
        public int[] checkpointTimesMs;

        public string ToRequestJson()
        {
            var json = new StringBuilder("{");
            void Text(string key, string value) => json.Append('"').Append(key).Append("\":\"").Append(BackendClient.Escape(value)).Append("\",");
            void Number(string key, int value) => json.Append('"').Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture)).Append(',');
            Text("clientRunId", clientRunId); Text("mode", mode); Text("boatId", boatId); Text("brain", brain); Text("clientVersion", clientVersion);
            Number("difficulty", difficulty); Number("timeMs", timeMs); Number("finishRank", finishRank); Number("layoutSeed", layoutSeed);
            // Trường không có thì bỏ hẳn: server hiểu là null, còn chuỗi rỗng sẽ bị từ chối
            if (!string.IsNullOrEmpty(dailyDate)) Text("dailyDate", dailyDate);
            if (!string.IsNullOrEmpty(ghost)) Text("ghost", ghost);
            json.Append("\"checkpointTimesMs\":[");
            for (int i = 0; i < checkpointTimesMs.Length; i++) json.Append(i > 0 ? "," : "").Append(checkpointTimesMs[i].ToString(CultureInfo.InvariantCulture));
            return json.Append("]}").ToString();
        }
    }

    public enum RunState { Sent, Queued, Rejected }
    public readonly struct RunResult
    {
        public readonly RunState State; public readonly RunResponse Response; public readonly string Message;
        public RunResult(RunState state, RunResponse response = null, string message = null) { State = state; Response = response; Message = message; }
    }

    /// <summary>
    /// Gọi API Backend. Mọi lỗi mạng đều không làm hỏng game: kết quả nằm trong hàng chờ và tự gửi lại sau.
    /// Không có bí mật nào của server trong file này; token chỉ là mã riêng của từng người chơi.
    /// </summary>
    public static class BackendClient
    {
        // Giai đoạn 7: điền địa chỉ HTTPS thật vào đây rồi build lại. Bản phát hành để trống thì tắt mọi tính năng online.
        const string ProductionUrl = "";
        const string DevelopmentUrl = "http://localhost:8088";
        const int TimeoutSeconds = 12;
        const int MaxQueuedRuns = 50;
        const string TokenKey = "VuotThac.Backend.Token", PlayerIdKey = "VuotThac.Backend.PlayerId", NameKey = "VuotThac.PlayerName";
        const string BestTimeKey = "VuotThac.BestTime", CupRankKey = "VuotThac.CupBestRank";

        public static string BaseUrl => Application.isEditor || Debug.isDebugBuild ? DevelopmentUrl : ProductionUrl;
        public static bool Enabled => !string.IsNullOrEmpty(BaseUrl);
        public static bool HasAccount => !string.IsNullOrEmpty(PlayerPrefs.GetString(TokenKey, ""));
        public static string PlayerId => PlayerPrefs.GetString(PlayerIdKey, "");
        public static string DisplayName => CleanName(PlayerPrefs.GetString(NameKey, "Người chơi"));

        static readonly SemaphoreSlim queueLock = new SemaphoreSlim(1, 1);
        static string QueueFolder => Path.Combine(Application.persistentDataPath, "backend_runs");

        // ---------- Gửi yêu cầu ----------

        readonly struct Reply
        {
            public readonly long Code; public readonly string Text; public readonly byte[] Data;
            public Reply(long code, string text, byte[] data) { Code = code; Text = text; Data = data; }
            public bool Ok => Code >= 200 && Code < 300;
            /// <summary>Lỗi tạm thời: giữ dữ liệu lại để thử sau.</summary>
            public bool Retryable => Code == 0 || Code == 429 || Code >= 500;
            public string Detail
            {
                get
                {
                    if (string.IsNullOrEmpty(Text)) return Code == 0 ? "Không kết nối được máy chủ" : "Lỗi " + Code;
                    try { var error = JsonUtility.FromJson<ApiError>(Text); if (!string.IsNullOrEmpty(error?.detail)) return error.detail; } catch (ArgumentException) { }
                    return "Lỗi " + Code;
                }
            }
        }

        static Task<Reply> Send(string method, string path, string body, bool auth)
        {
            var result = new TaskCompletionSource<Reply>();
            var request = new UnityWebRequest(BaseUrl + path, method) { downloadHandler = new DownloadHandlerBuffer(), timeout = TimeoutSeconds };
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (auth && HasAccount) request.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString(TokenKey));
            UnityWebRequestAsyncOperation operation;
            try { operation = request.SendWebRequest(); }
            catch (InvalidOperationException error)
            {
                // Unity chặn HTTP không mã hoá khi cấu hình không cho phép
                Debug.LogWarning("[Backend] " + error.Message);
                request.Dispose();
                return Task.FromResult(new Reply(0, null, null));
            }
            operation.completed += _ =>
            {
                bool connected = request.result != UnityWebRequest.Result.ConnectionError && request.result != UnityWebRequest.Result.DataProcessingError;
                result.SetResult(new Reply(connected ? request.responseCode : 0, connected ? request.downloadHandler.text : null, connected ? request.downloadHandler.data : null));
                request.Dispose();
            };
            return result.Task;
        }

        /// <summary>Gửi kèm token. Token hết hiệu lực (ví dụ database được dựng lại) thì tạo tài khoản mới và thử lại một lần.</summary>
        static async Task<Reply> SendAuthed(string method, string path, string body)
        {
            if (!await EnsureAccount()) return new Reply(0, null, null);
            var reply = await Send(method, path, body, true);
            if (reply.Code != 401) return reply;
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.DeleteKey(PlayerIdKey);
            return await EnsureAccount() ? await Send(method, path, body, true) : new Reply(0, null, null);
        }

        internal static string Escape(string value)
        {
            var text = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                if (c == '"' || c == '\\') text.Append('\\').Append(c);
                else if (c < ' ') text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else text.Append(c);
            }
            return text.ToString();
        }

        static string CleanName(string name)
        {
            name = string.Join(" ", (name ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            if (name.Length > 20) name = name.Substring(0, 20).TrimEnd();
            return name.Length < 2 ? "Người chơi" : name;
        }

        // ---------- Tài khoản ----------

        public static async Task<bool> EnsureAccount()
        {
            if (HasAccount) return true;
            var reply = await Send("POST", "/v1/players", "{\"displayName\":\"" + Escape(DisplayName) + "\"}", false);
            if (!reply.Ok) return false;
            var player = JsonUtility.FromJson<PlayerResponse>(reply.Text);
            PlayerPrefs.SetString(TokenKey, player.token);
            PlayerPrefs.SetString(PlayerIdKey, player.id);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Đổi tên hiển thị. Trả về null nếu thành công, ngược lại là thông báo lỗi.</summary>
        public static async Task<string> Rename(string name)
        {
            name = CleanName(name);
            PlayerPrefs.SetString(NameKey, name);
            PlayerPrefs.Save();
            var reply = await SendAuthed("PATCH", "/v1/players/me", "{\"displayName\":\"" + Escape(name) + "\"}");
            return reply.Ok ? null : reply.Detail;
        }

        // ---------- Kết quả đua ----------

        static string RunFile(string id) => Path.Combine(QueueFolder, id + ".json");

        /// <summary>Lưu lượt đua vào hàng chờ rồi gửi ngay nếu có mạng.</summary>
        public static async Task<RunResult> SubmitRun(PendingRun run)
        {
            try
            {
                Directory.CreateDirectory(QueueFolder);
                File.WriteAllText(RunFile(run.clientRunId), JsonUtility.ToJson(run));
                TrimQueue();
            }
            catch (IOException error) { Debug.LogWarning("[Backend] Không lưu được hàng chờ: " + error.Message); }
            return await FlushRuns(run.clientRunId);
        }

        static void TrimQueue()
        {
            var files = new DirectoryInfo(QueueFolder).GetFiles("*.json");
            if (files.Length <= MaxQueuedRuns) return;
            Array.Sort(files, (a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
            for (int i = 0; i < files.Length - MaxQueuedRuns; i++) files[i].Delete();
        }

        /// <summary>Gửi lần lượt hàng chờ, cũ trước. Gặp lỗi mạng thì dừng, để dành lần sau.</summary>
        public static async Task<RunResult> FlushRuns(string wantedId = null)
        {
            var wanted = new RunResult(RunState.Queued);
            if (!Enabled || !Directory.Exists(QueueFolder)) return wanted;
            await queueLock.WaitAsync();
            try
            {
                var files = new DirectoryInfo(QueueFolder).GetFiles("*.json");
                Array.Sort(files, (a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                foreach (var file in files)
                {
                    PendingRun run;
                    try { run = JsonUtility.FromJson<PendingRun>(File.ReadAllText(file.FullName)); }
                    catch (Exception error) when (error is IOException || error is ArgumentException) { file.Delete(); continue; }
                    bool isWanted = run.clientRunId == wantedId;
                    var reply = await SendAuthed("POST", "/v1/runs", run.ToRequestJson());
                    // Ghost bị từ chối thì vẫn giữ được thành tích: gửi lại không kèm ghost
                    if (!reply.Ok && !reply.Retryable && !string.IsNullOrEmpty(run.ghost))
                    {
                        Debug.LogWarning("[Backend] Gửi lại không kèm ghost vì: " + reply.Detail);
                        run.ghost = "";
                        reply = await SendAuthed("POST", "/v1/runs", run.ToRequestJson());
                    }
                    if (reply.Retryable) break;
                    file.Delete();
                    if (reply.Ok)
                    {
                        if (isWanted) wanted = new RunResult(RunState.Sent, JsonUtility.FromJson<RunResponse>(reply.Text));
                    }
                    else
                    {
                        Debug.LogWarning("[Backend] Máy chủ từ chối kết quả: " + reply.Detail);
                        if (isWanted) wanted = new RunResult(RunState.Rejected, null, reply.Detail);
                    }
                }
            }
            catch (IOException error) { Debug.LogWarning("[Backend] Lỗi hàng chờ: " + error.Message); }
            finally { queueLock.Release(); }
            return wanted;
        }

        // ---------- Đọc dữ liệu ----------

        /// <summary>Trả về null khi không có mạng hoặc máy chủ lỗi.</summary>
        public static async Task<LeaderboardResponse> GetLeaderboard(string mode, string boatId, int limit)
        {
            string path = "/v1/leaderboard?mode=" + mode + "&limit=" + limit.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(boatId)) path += "&boatId=" + UnityWebRequest.EscapeURL(boatId);
            var reply = await Send("GET", path, null, HasAccount);
            // Token cũ không làm hỏng việc xem bảng: API coi như chưa đăng nhập
            return reply.Ok ? JsonUtility.FromJson<LeaderboardResponse>(reply.Text) : null;
        }

        public static async Task<DailyChallenge> GetDailyChallenge()
        {
            var reply = await Send("GET", "/v1/daily-challenge", null, false);
            return reply.Ok ? JsonUtility.FromJson<DailyChallenge>(reply.Text) : null;
        }

        public static async Task<byte[]> DownloadGhost(string ghostId)
        {
            var reply = await Send("GET", "/v1/ghosts/" + UnityWebRequest.EscapeURL(ghostId), null, false);
            return reply.Ok ? reply.Data : null;
        }

        // ---------- Tiến độ ----------

        /// <summary>
        /// Gộp thành tích trên máy và trên server, lấy cái tốt hơn của mỗi bên.
        /// Có máy khác vừa lưu (409) thì đọc lại rồi gộp lần nữa, nên không bên nào mất dữ liệu.
        /// </summary>
        public static async Task<bool> SyncSave()
        {
            if (!Enabled) return false;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var reply = await SendAuthed("GET", "/v1/save", null);
                if (!reply.Ok) return false;
                var server = JsonUtility.FromJson<SaveResponse>(reply.Text);
                var remote = server.data ?? new SaveData();

                float best = PlayerPrefs.GetFloat(BestTimeKey, float.MaxValue);
                if (remote.bestTimeSeconds > 0 && remote.bestTimeSeconds < best) { best = remote.bestTimeSeconds; PlayerPrefs.SetFloat(BestTimeKey, best); }
                int rank = PlayerPrefs.GetInt(CupRankKey, 7);
                if (remote.cupBestRank > 0 && remote.cupBestRank < rank) { rank = remote.cupBestRank; PlayerPrefs.SetInt(CupRankKey, rank); }
                PlayerPrefs.Save();

                var merged = new SaveData { bestTimeSeconds = best == float.MaxValue ? 0 : best, cupBestRank = rank >= 7 ? 0 : rank };
                if (Mathf.Approximately(merged.bestTimeSeconds, remote.bestTimeSeconds) && merged.cupBestRank == remote.cupBestRank) return true;
                string body = "{\"revision\":" + server.revision.ToString(CultureInfo.InvariantCulture) + ",\"data\":" + JsonUtility.ToJson(merged) + "}";
                var put = await SendAuthed("PUT", "/v1/save", body);
                if (put.Ok) return true;
                if (put.Code != 409) return false;
            }
            return false;
        }
    }
}
