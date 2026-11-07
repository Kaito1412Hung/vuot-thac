using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VuotThac.Track;

namespace VuotThac.Boats
{
    /// <summary>
    /// Màn chọn thuyền ngay trên đường đua: thuyền đang chọn nổi trên sông trước vạch xuất phát và xoay chậm,
    /// bên dưới hiện tên, mô tả và bốn chỉ số. Chọn xong thì thuyền được thả xuống nước cho người chơi lái.
    /// Giao diện được dựng bằng code nên không cần chỉnh tay trong scene.
    /// </summary>
    public class BoatSelectScreen : MonoBehaviour
    {
        public BoatCatalog catalog;
        public RiverTrack track;
        public BoatCamera boatCamera;
        [Tooltip("Thuyền đứng chờ cách vạch xuất phát bao nhiêu mét về phía thượng nguồn")]
        public float spawnBeforeStart = 12f;

        const string PrefKey = "VuotThac.SelectedBoat";
        static readonly string[] StatNames = { "Tốc độ", "Tăng tốc", "Bẻ lái", "Độ bền" };

        enum State { Selecting, Playing }

        State state;
        int index;
        GameObject preview, playerBoat;
        BoatController player;
        Vector3 spot, spotForward;
        Quaternion spotRotation;
        float spin;

        Font font;
        GameObject selectRoot, hudRoot;
        Text nameText, descText, counterText, speedText;
        RectTransform[] statFills;
        Text[] statValues;
        InputAction prevAction, nextAction, confirmAction, backAction;

        void Awake()
        {
            if (track == null)
                track = FindAnyObjectByType<RiverTrack>();
            if (boatCamera == null && Camera.main != null)
            {
                boatCamera = Camera.main.GetComponent<BoatCamera>();
                if (boatCamera == null)
                    boatCamera = Camera.main.gameObject.AddComponent<BoatCamera>();
            }

            prevAction = MakeButton("<Keyboard>/a", "<Keyboard>/leftArrow", "<Gamepad>/dpad/left");
            nextAction = MakeButton("<Keyboard>/d", "<Keyboard>/rightArrow", "<Gamepad>/dpad/right");
            confirmAction = MakeButton("<Keyboard>/enter", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            backAction = MakeButton("<Keyboard>/escape", "<Gamepad>/start");
        }

        static InputAction MakeButton(params string[] bindings)
        {
            var a = new InputAction(type: InputActionType.Button);
            foreach (var b in bindings)
                a.AddBinding(b);
            a.Enable();
            return a;
        }

        void OnDestroy()
        {
            foreach (var a in new[] { prevAction, nextAction, confirmAction, backAction })
                a?.Dispose();
        }

        void Start()
        {
            if (catalog == null || catalog.boats == null || catalog.boats.Length == 0 || track == null)
            {
                Debug.LogError("[Vượt Thác] Màn chọn thuyền thiếu danh sách thuyền hoặc đường đua.", this);
                enabled = false;
                return;
            }

            var s = track.EvaluateAtDistance(track.StartDistance - spawnBeforeStart);
            spot = track.AfterRaceInsertion(s.center);
            spot.y = s.WaterHeight;
            spotForward = s.tangent;
            spotRotation = Quaternion.LookRotation(s.tangent, Vector3.up);

            BuildUI();
            index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, catalog.boats.Length - 1);
            EnterSelect();
        }

        void Update()
        {
            if (state == State.Selecting)
            {
                if (prevAction.WasPressedThisFrame()) Show(index - 1);
                if (nextAction.WasPressedThisFrame()) Show(index + 1);
                if (confirmAction.WasPressedThisFrame()) StartRace();
                AnimatePreview();
            }
            else
            {
                if (backAction.WasPressedThisFrame())
                {
                    EnterSelect();
                    return;
                }
                if (player != null)
                    speedText.text = $"{player.Speed * 3.6f:0} km/h";
            }
        }

        // ---------- Luồng chơi ----------

        void EnterSelect()
        {
            state = State.Selecting;
            if (playerBoat != null)
                Destroy(playerBoat);
            player = null;
            if (boatCamera != null)
                boatCamera.target = null;
            selectRoot.SetActive(true);
            hudRoot.SetActive(false);
            Show(index);
        }

        void Show(int i)
        {
            int n = catalog.boats.Length;
            index = ((i % n) + n) % n;
            var def = catalog.boats[index];

            if (preview != null)
                Destroy(preview);
            preview = Instantiate(def.prefab, spot, spotRotation);
            preview.name = "Preview_" + def.name;
            preview.transform.position=spot+Vector3.up*preview.GetComponent<BoatController>().FloatHeight;
            // Thuyền trưng bày đứng yên trên mặt nước, không bị nước cuốn đi
            var rb = preview.GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = true;

            nameText.text = def.displayName.ToUpperInvariant();
            descText.text = def.description;
            counterText.text = $"{index + 1} / {n}";
            int[] stats = { def.speed, def.acceleration, def.handling, def.toughness };
            for (int k = 0; k < stats.Length; k++)
            {
                statFills[k].anchorMax = new Vector2(stats[k] / 10f, 1f);
                statValues[k].text = stats[k].ToString();
            }
        }

        void AnimatePreview()
        {
            if (preview == null)
                return;

            spin += Time.deltaTime * 22f;
            float bob = preview.GetComponent<BoatController>().FloatHeight+Mathf.Sin(Time.time * 1.6f)*0.008f;
            preview.transform.SetPositionAndRotation(spot + Vector3.up * bob, spotRotation * Quaternion.Euler(0f, spin, 0f));

            if (boatCamera == null)
                return;
            var ctrl = preview.GetComponent<BoatController>();
            float length = ctrl != null ? ctrl.hullLength : 4f;
            float dist = Mathf.Max(5.5f, length * 1.3f);
            Vector3 right = Vector3.Cross(Vector3.up, spotForward);
            Vector3 camPos = spot + spotForward * (dist * 0.85f) + right * (dist * 0.55f) + Vector3.up * (dist * 0.3f);
            // Nhắm lệch sang phải thuyền để thuyền nằm ở nửa trái màn hình, chừa nửa phải cho bảng thông tin
            Vector3 view = spot - camPos;
            view.y = 0f;
            Vector3 camRight = Vector3.Cross(Vector3.up, view.normalized);
            boatCamera.transform.position = camPos;
            boatCamera.transform.LookAt(spot + camRight * (dist * 0.42f) + Vector3.up * 0.2f);
        }

        void StartRace()
        {
            var def = catalog.boats[index];
            PlayerPrefs.SetInt(PrefKey, index);
            PlayerPrefs.Save();

            if (preview != null)
                Destroy(preview);

            float spawnHeight=def.prefab.GetComponent<BoatController>().FloatHeight;
            playerBoat = Instantiate(def.prefab, spot + Vector3.up * (spawnHeight+0.02f), spotRotation);
            playerBoat.name = "PlayerBoat_" + def.name;
            player = playerBoat.GetComponent<BoatController>();
            player.Track = track;
            player.Input = playerBoat.AddComponent<PlayerBoatInput>();

            if (boatCamera != null)
                boatCamera.Follow(playerBoat.transform, player.hullLength);

            selectRoot.SetActive(false);
            hudRoot.SetActive(true);
            state = State.Playing;
        }

        // ---------- Giao diện ----------

        void BuildUI()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("BoatSelectUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            // Bám chữ và hình vào lưới điểm ảnh để nét chữ không bị nhòe
            canvas.pixelPerfect = true;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Co giãn theo chiều cao: màn hình điện thoại nằm ngang dù dài cỡ nào bảng cũng vừa khít theo chiều dọc
            scaler.matchWidthOrHeight = 1f;
            var root = canvasGo.transform;

            // Màn chọn thuyền
            selectRoot = Rect(root, "Select", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            // Bố cục: thuyền ở nửa trái màn hình, bảng thông tin dọc ở bên phải, nút đổi thuyền ở dưới thuyền
            var title = Label(selectRoot.transform, "Title", "CHỌN THUYỀN", 60, FontStyle.Bold, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.3f, 1f), new Vector2(0f, -70f), new Vector2(800f, 90f));
            Shadow(title);

            var panel = Rect(selectRoot.transform, "InfoPanel", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Place(panel, new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(660f, 900f), new Vector2(1f, 0.5f));
            panel.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.1f, 0.75f);

            nameText = Label(panel, "Name", "", 52, FontStyle.Bold, TextAnchor.UpperLeft);
            Place(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(580f, 70f), new Vector2(0f, 1f));
            nameText.color = new Color(1f, 0.85f, 0.35f);
            descText = Label(panel, "Description", "", 31, FontStyle.Normal, TextAnchor.UpperLeft);
            descText.lineSpacing = 1.1f;
            Place(descText.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -125f), new Vector2(580f, 200f), new Vector2(0f, 1f));

            statFills = new RectTransform[StatNames.Length];
            statValues = new Text[StatNames.Length];
            for (int k = 0; k < StatNames.Length; k++)
            {
                float y = -360f - k * 70f;
                var label = Label(panel, "Stat_" + k, StatNames[k], 32, FontStyle.Bold, TextAnchor.MiddleLeft);
                Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(170f, 44f), new Vector2(0f, 1f));

                var bar = Rect(panel, "Bar_" + k, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                Place(bar, new Vector2(0f, 1f), new Vector2(215f, y - 11f), new Vector2(320f, 22f), new Vector2(0f, 1f));
                bar.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
                var fill = Rect(bar, "Fill", Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
                fill.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.8f, 0.45f);
                statFills[k] = fill;

                statValues[k] = Label(panel, "Value_" + k, "", 32, FontStyle.Bold, TextAnchor.MiddleLeft);
                Place(statValues[k].rectTransform, new Vector2(0f, 1f), new Vector2(560f, y), new Vector2(60f, 44f), new Vector2(0f, 1f));
            }

            var start = Button(panel, "StartButton", "BẮT ĐẦU", new Color(0.2f, 0.62f, 0.3f), StartRace);
            Place(start, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(420f, 96f), new Vector2(0.5f, 0f));

            var prev = Button(selectRoot.transform, "PrevButton", "<", new Color(0f, 0f, 0f, 0.5f), () => Show(index - 1));
            Place(prev, new Vector2(0.3f, 0f), new Vector2(-210f, 150f), new Vector2(110f, 110f));
            var next = Button(selectRoot.transform, "NextButton", ">", new Color(0f, 0f, 0f, 0.5f), () => Show(index + 1));
            Place(next, new Vector2(0.3f, 0f), new Vector2(210f, 150f), new Vector2(110f, 110f));
            counterText = Label(selectRoot.transform, "Counter", "", 40, FontStyle.Bold, TextAnchor.MiddleCenter);
            Place(counterText.rectTransform, new Vector2(0.3f, 0f), new Vector2(0f, 150f), new Vector2(260f, 60f));
            Shadow(counterText);

            var keys = Label(selectRoot.transform, "Keys", "A/D hoặc ← → để đổi thuyền · Enter để bắt đầu", 28, FontStyle.Normal, TextAnchor.MiddleCenter);
            Place(keys.rectTransform, new Vector2(0.3f, 0f), new Vector2(0f, 60f), new Vector2(900f, 30f));
            Shadow(keys);

            // Giao diện khi đang chơi
            hudRoot = Rect(root, "Hud", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            var hint = Label(hudRoot.transform, "Hint", "W/S: chèo tới/lùi    A/D: bẻ lái    R: về giữa dòng    C: đổi góc nhìn    Esc: chọn lại thuyền", 28, FontStyle.Normal, TextAnchor.UpperLeft);
            Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(1200f, 40f), new Vector2(0f, 1f));
            Shadow(hint);
            speedText = Label(hudRoot.transform, "Speed", "0 km/h", 56, FontStyle.Bold, TextAnchor.LowerRight);
            Place(speedText.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(400f, 80f), new Vector2(1f, 0f));
            Shadow(speedText);
        }

        RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        Text Label(Transform parent, string name, string text, int size, FontStyle style, TextAnchor align)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static void Shadow(Text t)
        {
            var s = t.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.7f);
            s.effectDistance = new Vector2(2f, -2f);
        }

        RectTransform Button(Transform parent, string name, string text, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var btn = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var label = Label(rt, "Label", text, 40, FontStyle.Bold, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
            return rt;
        }
    }
}
