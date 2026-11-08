using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VuotThac.Backend;

namespace VuotThac.Boats
{
    /// <summary>Nối màn chọn thuyền và cuộc đua với Backend: gửi kết quả, bảng xếp hạng, bóng ma, thử thách ngày, tiến độ.</summary>
    public partial class BoatSelectScreen
    {
        const int LeaderboardRows = 10;
        const string CheckpointMismatch = "Thiếu dữ liệu checkpoint, không gửi được kết quả";
        static readonly (string mode, string label)[] BoardModes = { ("single", "ĐUA 1 LƯỢT"), ("cup", "CÚP"), ("daily", "HÔM NAY") };

        GameObject leaderboardPanel, ghostBoat;
        InputField boardName;
        Text boardStatus, boardMe, boardFilterLabel, ghostHud, ghostChoiceLabel;
        RectTransform ghostChoice;
        readonly List<BoardRow> boardRows = new List<BoardRow>();
        readonly Dictionary<string, Image> boardTabs = new Dictionary<string, Image>();
        string boardMode = "single";
        bool boardThisBoat, modeBusy, dailySelected;
        int boardRequest;
        DailyChallenge dailyInfo;
        GhostRecorder recorder;
        GhostData chosenGhost;
        string chosenGhostBoat, chosenGhostName, backendLine = "";
        int chosenGhostMs;
        Material ghostMaterial;

        sealed class BoardRow
        {
            public GameObject Root; public Text Rank, Name, Boat, Time; public Button Ghost; public Text GhostLabel; public LeaderboardEntry Entry;
        }

        // ---------- Giao diện ----------

        void BuildBackendUI(Transform root)
        {
            if (!BackendClient.Enabled) return;
            var open = Button(selectRoot.transform, "Leaderboard", "BẢNG XẾP HẠNG", new Color(.42f, .3f, .1f), OpenLeaderboard);
            // Xếp ngang hàng với nút bạn bè để không che thêm thuyền đang trưng bày
            Place(open, new Vector2(.3f, 0), new Vector2(155, 290), new Vector2(300, 82));
            open.GetComponentInChildren<Text>().fontSize = 27;
            if (selectRoot.transform.Find("Friends") is RectTransform friends)
            {
                Place(friends, new Vector2(.3f, 0), new Vector2(-155, 290), new Vector2(300, 82));
                friends.GetComponentInChildren<Text>().fontSize = 27;
            }

            ghostChoice = Button(selectRoot.transform, "GhostChoice", "", new Color(.1f, .32f, .42f, .92f), ClearChosenGhost);
            Place(ghostChoice, new Vector2(.3f, 0), new Vector2(0, 372), new Vector2(760, 60));
            ghostChoiceLabel = ghostChoice.GetComponentInChildren<Text>();
            ghostChoiceLabel.fontSize = 26; ghostChoiceLabel.supportRichText = false;
            ghostChoice.gameObject.SetActive(false);

            ghostHud = Label(hudRoot.transform, "GhostHud", "", 28, FontStyle.Bold, TextAnchor.UpperRight);
            ghostHud.supportRichText = false;
            Place(ghostHud.rectTransform, new Vector2(1, 1), new Vector2(-40, -24), new Vector2(760, 44), new Vector2(1, 1));
            Shadow(ghostHud);

            leaderboardPanel = Rect(root, "LeaderboardPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            leaderboardPanel.AddComponent<Image>().color = new Color(.02f, .06f, .09f, .97f);
            var panel = leaderboardPanel.transform;
            var title = Label(panel, "Title", "BẢNG XẾP HẠNG SUỐI RỪNG", 52, FontStyle.Bold, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(.5f, 1), new Vector2(0, -55), new Vector2(1400, 80));

            var nameBox = Rect(panel, "NameBox", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Place(nameBox, new Vector2(.5f, 1), new Vector2(-170, -135), new Vector2(560, 64));
            nameBox.gameObject.AddComponent<Image>().color = new Color(.13f, .2f, .25f);
            boardName = nameBox.gameObject.AddComponent<InputField>();
            var nameText = Label(nameBox, "Value", "", 30, FontStyle.Normal, TextAnchor.MiddleLeft);
            nameText.supportRichText = false;
            nameText.rectTransform.offsetMin = new Vector2(18, 0); nameText.rectTransform.offsetMax = new Vector2(-18, 0);
            boardName.textComponent = nameText; boardName.characterLimit = 20;
            var saveName = Button(panel, "SaveName", "ĐỔI TÊN", new Color(.15f, .45f, .65f), SaveBoardName);
            Place(saveName, new Vector2(.5f, 1), new Vector2(240, -135), new Vector2(240, 64));
            saveName.GetComponentInChildren<Text>().fontSize = 28;

            for (int i = 0; i < BoardModes.Length; i++)
            {
                string mode = BoardModes[i].mode;
                var tab = Button(panel, "Tab_" + mode, BoardModes[i].label, new Color(.14f, .24f, .3f), () => { boardMode = mode; RefreshLeaderboard(); });
                Place(tab, new Vector2(.5f, 1), new Vector2(-510 + i * 290, -212), new Vector2(280, 60));
                tab.GetComponentInChildren<Text>().fontSize = 27;
                boardTabs[mode] = tab.GetComponent<Image>();
            }
            var filter = Button(panel, "BoatFilter", "", new Color(.14f, .24f, .3f), () => { boardThisBoat = !boardThisBoat; RefreshLeaderboard(); });
            Place(filter, new Vector2(.5f, 1), new Vector2(480, -212), new Vector2(480, 60));
            boardFilterLabel = filter.GetComponentInChildren<Text>(); boardFilterLabel.fontSize = 25;

            for (int i = 0; i < LeaderboardRows; i++) boardRows.Add(MakeBoardRow(panel, -282 - i * 58));

            boardMe = Label(panel, "Me", "", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            boardMe.supportRichText = false; boardMe.color = new Color(1, .86f, .45f);
            Place(boardMe.rectTransform, new Vector2(.5f, 0), new Vector2(0, 150), new Vector2(1500, 44));
            boardStatus = Label(panel, "Status", "", 30, FontStyle.Normal, TextAnchor.MiddleCenter);
            boardStatus.supportRichText = false;
            Place(boardStatus.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 0), new Vector2(1400, 90));
            var close = Button(panel, "Close", "QUAY LẠI", new Color(.3f, .3f, .35f), CloseLeaderboard);
            Place(close, new Vector2(.5f, 0), new Vector2(0, 75), new Vector2(420, 76));
            close.GetComponentInChildren<Text>().fontSize = 32;
            leaderboardPanel.SetActive(false);
        }

        BoardRow MakeBoardRow(Transform panel, float y)
        {
            var root = Rect(panel, "Row", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Place(root, new Vector2(.5f, 1), new Vector2(0, y), new Vector2(1500, 54));
            root.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .04f);
            Text Column(string name, float x, float width, TextAnchor align)
            {
                var text = Label(root, name, "", 29, FontStyle.Normal, align);
                text.supportRichText = false; text.horizontalOverflow = HorizontalWrapMode.Overflow;
                Place(text.rectTransform, new Vector2(0, .5f), new Vector2(x, 0), new Vector2(width, 50), new Vector2(0, .5f));
                return text;
            }
            var row = new BoardRow
            {
                Root = root.gameObject,
                Rank = Column("Rank", 16, 70, TextAnchor.MiddleLeft),
                Name = Column("Name", 90, 440, TextAnchor.MiddleLeft),
                Boat = Column("Boat", 540, 300, TextAnchor.MiddleLeft),
                Time = Column("Time", 850, 170, TextAnchor.MiddleRight),
            };
            var ghost = Button(root, "Ghost", "ĐUA BÓNG MA", new Color(.1f, .38f, .5f), () => ChooseGhost(row));
            Place(ghost, new Vector2(1, .5f), new Vector2(-8, 0), new Vector2(300, 46), new Vector2(1, .5f));
            row.Ghost = ghost.GetComponent<Button>();
            row.GhostLabel = ghost.GetComponentInChildren<Text>(); row.GhostLabel.fontSize = 24;
            return row;
        }

        static string FormatMs(int ms) => $"{ms / 60000:00}:{ms / 1000 % 60:00}.{ms / 100 % 10}";

        string BoatDisplayName(string id)
        {
            foreach (var boat in catalog.boats) if (boat.name == id) return boat.displayName;
            return id;
        }

        void OpenLeaderboard()
        {
            selectRoot.SetActive(false);
            boardName.text = BackendClient.DisplayName;
            leaderboardPanel.SetActive(true);
            RefreshLeaderboard();
        }

        void CloseLeaderboard()
        {
            boardRequest++;
            leaderboardPanel.SetActive(false);
            if (state == State.Selecting) selectRoot.SetActive(true);
        }

        async void RefreshLeaderboard()
        {
            int request = ++boardRequest;
            foreach (var pair in boardTabs) pair.Value.color = pair.Key == boardMode ? new Color(.15f, .45f, .65f) : new Color(.14f, .24f, .3f);
            boardFilterLabel.text = boardThisBoat ? "THUYỀN: " + catalog.boats[index].displayName.ToUpperInvariant() : "THUYỀN: TẤT CẢ";
            boardStatus.text = "Đang tải…"; boardMe.text = "";
            foreach (var row in boardRows) row.Root.SetActive(false);
            // Gửi nốt kết quả còn chờ để bảng có cả lượt mới nhất của mình
            await BackendClient.FlushRuns();
            var board = await BackendClient.GetLeaderboard(boardMode, boardThisBoat ? catalog.boats[index].name : null, LeaderboardRows);
            if (this == null || request != boardRequest) return;
            if (board == null) { boardStatus.text = "Không kết nối được máy chủ. Bạn vẫn chơi bình thường."; return; }
            int count = board.entries?.Length ?? 0;
            boardStatus.text = count == 0 ? "Chưa có kết quả nào. Hãy về đích đầu tiên!" : "";
            for (int i = 0; i < boardRows.Count; i++)
            {
                var row = boardRows[i];
                row.Root.SetActive(i < count);
                if (i >= count) continue;
                var entry = board.entries[i];
                row.Entry = entry;
                row.Rank.text = entry.rank.ToString();
                row.Name.text = entry.displayName;
                row.Boat.text = BoatDisplayName(entry.boatId);
                row.Time.text = FormatMs(entry.timeMs);
                row.Name.color = row.Rank.color = entry.playerId == BackendClient.PlayerId ? new Color(1, .86f, .45f) : Color.white;
                row.Ghost.gameObject.SetActive(!string.IsNullOrEmpty(entry.ghostId));
                row.Ghost.interactable = true; row.GhostLabel.text = "ĐUA BÓNG MA";
            }
            boardMe.text = board.me != null && board.me.rank > 0
                ? $"Bạn: hạng {board.me.rank}/{board.total} · {FormatMs(board.me.timeMs)} · {BoatDisplayName(board.me.boatId)}"
                : count > 0 ? $"Có {board.total} người trên bảng. Bạn chưa có kết quả ở đây." : "";
        }

        async void SaveBoardName()
        {
            boardStatus.text = "Đang đổi tên…";
            string error = await BackendClient.Rename(boardName.text);
            if (this == null) return;
            boardName.text = BackendClient.DisplayName;
            if (error != null) { boardStatus.text = "Chưa đổi được tên trên máy chủ: " + error; return; }
            RefreshLeaderboard();
        }

        // ---------- Bóng ma ----------

        async void ChooseGhost(BoardRow row)
        {
            var entry = row.Entry;
            if (entry == null || string.IsNullOrEmpty(entry.ghostId)) return;
            row.Ghost.interactable = false; row.GhostLabel.text = "ĐANG TẢI…";
            var bytes = await BackendClient.DownloadGhost(entry.ghostId);
            if (this == null) return;
            var ghost = bytes != null ? GhostData.FromGzip(bytes) : null;
            if (ghost == null || BoatPrefab(entry.boatId) == null)
            {
                row.Ghost.interactable = true; row.GhostLabel.text = "ĐUA BÓNG MA";
                boardStatus.text = "Không tải được bóng ma này.";
                return;
            }
            chosenGhost = ghost; chosenGhostBoat = entry.boatId; chosenGhostName = entry.displayName; chosenGhostMs = entry.timeMs;
            RefreshGhostChoice();
            CloseLeaderboard();
        }

        void ClearChosenGhost()
        {
            chosenGhost = null;
            RefreshGhostChoice();
        }

        void RefreshGhostChoice()
        {
            if (ghostChoice == null) return;
            ghostChoice.gameObject.SetActive(chosenGhost != null);
            if (chosenGhost != null) ghostChoiceLabel.text = $"BÓNG MA: {chosenGhostName} · {FormatMs(chosenGhostMs)} · BẤM ĐỂ BỎ";
        }

        GameObject BoatPrefab(string id)
        {
            foreach (var boat in catalog.boats) if (boat.name == id) return boat.prefab;
            return null;
        }

        void SpawnGhostBoat()
        {
            if (ghostHud != null) ghostHud.text = "";
            if (chosenGhost == null) return;
            var prefab = BoatPrefab(chosenGhostBoat);
            if (prefab == null) return;
            if (ghostMaterial == null)
            {
                var shader = Resources.Load<Shader>("Ghost/GhostBoat");
                if (shader == null) { Debug.LogWarning("[Backend] Thiếu shader bóng ma."); return; }
                ghostMaterial = new Material(shader) { name = "GhostBoat" };
            }
            ghostBoat = GhostBoat.Spawn(prefab, chosenGhost, race, ghostMaterial);
            ghostHud.text = $"BÓNG MA: {chosenGhostName} · {FormatMs(chosenGhostMs)}";
        }

        void ClearGhostBoat()
        {
            if (ghostBoat != null) { ghostBoat.SetActive(false); Destroy(ghostBoat); }
            ghostBoat = null;
        }

        // ---------- Thử thách ngày ----------

        async void CycleRaceMode()
        {
            if (modeBusy) return;
            if (!cupSelected && !dailySelected) cupSelected = true;
            else if (cupSelected && BackendClient.Enabled)
            {
                cupSelected = false; modeBusy = true;
                modeText.text = "ĐANG TẢI THỬ THÁCH…";
                var challenge = await BackendClient.GetDailyChallenge();
                if (this == null) return;
                modeBusy = false;
                dailyInfo = challenge; dailySelected = challenge != null;
                if (challenge == null) { modeText.text = "KHÔNG TẢI ĐƯỢC THỬ THÁCH — BẤM ĐỂ ĐỔI >"; return; }
            }
            else { cupSelected = false; dailySelected = false; }
            RefreshModeText();
        }

        void RefreshModeText()
        {
            modeText.text = dailySelected ? $"THỬ THÁCH NGÀY · {RaceDifficulty.Names[Mathf.Clamp(dailyInfo.difficulty, 0, 4)].ToUpperInvariant()} >"
                : cupSelected ? "CHẾ ĐỘ: CÚP 3 LƯỢT >" : "CHẾ ĐỘ: ĐUA 1 LƯỢT >";
        }

        bool DailyRace => dailySelected && dailyInfo != null && !cupSelected;

        // ---------- Gửi kết quả ----------

        async void BootBackend()
        {
            if (!BackendClient.Enabled) return;
            await BackendClient.FlushRuns();
            if (BackendClient.HasAccount) await BackendClient.SyncSave();
        }

        void BeginRecording()
        {
            backendLine = "";
            recorder = BackendClient.Enabled && !onlinePlaying ? new GhostRecorder() : null;
        }

        void TickRecording()
        {
            if (recorder != null && player != null && race.CountdownRemaining <= 0) recorder.Tick(race.Elapsed, player.transform);
        }

        async void SubmitFinishedRun()
        {
            var recorded = recorder;
            recorder = null;
            if (!BackendClient.Enabled || onlinePlaying || race.Player == null || !race.Player.Finished) return;
            var times = race.Player.CheckpointTimesMs;
            if (times.Count == 0) { backendLine = CheckpointMismatch; return; }
            backendLine = "Đang gửi kết quả…";
            bool daily = DailyRace && !cup.Active;
            var run = new PendingRun
            {
                clientRunId = Guid.NewGuid().ToString(),
                mode = cup.Active ? "cup" : daily ? "daily" : "single",
                boatId = catalog.boats[index].name,
                brain = BoatBrainSettings.UseModel ? "ppo" : "fsm",
                difficulty = daily ? dailyInfo.difficulty : (int)difficulty,
                timeMs = times[times.Count - 1],
                checkpointTimesMs = times.ToArray(),
                finishRank = Mathf.Clamp(race.Player.FinishOrder > 0 ? race.Player.FinishOrder : race.PlayerRank, 1, 6),
                layoutSeed = race.Layout != null ? race.Layout.Seed : 0,
                dailyDate = daily ? dailyInfo.date : "",
                clientVersion = Application.version,
                ghost = recorded != null && recorded.Data.Positions.Count > 0 ? Convert.ToBase64String(recorded.Data.ToGzip()) : "",
            };
            var result = await BackendClient.SubmitRun(run);
            if (this == null) return;
            backendLine = result.State switch
            {
                RunState.Sent => $"BẢNG XẾP HẠNG: HẠNG {result.Response.rank}" + (result.Response.isPersonalBest ? " · KỶ LỤC MỚI CỦA THUYỀN NÀY!" : ""),
                RunState.Rejected => "Máy chủ không nhận kết quả: " + result.Message,
                _ => "Chưa gửi được. Game sẽ tự gửi lại khi có mạng.",
            };
            if (result.State == RunState.Sent) _ = BackendClient.SyncSave();
        }

        string BackendResultLine()
        {
            string line = backendLine;
            if (!onlinePlaying && ghostBoat != null && race.Player != null && race.Player.Finished)
            {
                float difference = race.Player.FinishTime - chosenGhostMs / 1000f;
                line = (difference < 0 ? $"THẮNG BÓNG MA {-difference:0.0} GIÂY" : $"THUA BÓNG MA {difference:0.0} GIÂY") + (line.Length > 0 ? "\n" + line : "");
            }
            return line.Length > 0 ? "\n" + line : "";
        }
    }
}
