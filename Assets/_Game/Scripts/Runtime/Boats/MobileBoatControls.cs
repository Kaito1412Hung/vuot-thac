using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VuotThac.Boats
{
    // Each held button owns its pointers, so releasing one finger cannot cancel another.
    public class BoatHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        readonly HashSet<int> pointers = new HashSet<int>();
        public bool Held => pointers.Count > 0;
        public void OnPointerDown(PointerEventData e) => pointers.Add(e.pointerId);
        public void OnPointerUp(PointerEventData e) => pointers.Remove(e.pointerId);
        public void OnPointerExit(PointerEventData e) => pointers.Remove(e.pointerId);
        public void Clear() => pointers.Clear();
        void OnDisable() => Clear();
    }

    public class MobileBoatControls : MonoBehaviour
    {
        Rect previousArea; int previousWidth, previousHeight;
        PlayerBoatInput input;
        RectTransform safe;
        GameObject canvas;
        BoatHoldButton left, right, forward, reverse;
        Text mode;
        Font font;
        public void Initialize(PlayerBoatInput controls, BoatCamera camera, BoatSelectScreen selection)
        {
            input = controls;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvas = new GameObject("MobileBoatControls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 20;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;
            safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(canvas.transform, false);
            left = Hold("TRÁI", new Vector2(0, 0), new Vector2(100, 115));
            right = Hold("PHẢI", new Vector2(0, 0), new Vector2(285, 115));
            reverse = Hold("LÙI", new Vector2(1, 0), new Vector2(-285, 115));
            forward = Hold("CHÈO", new Vector2(1, 0), new Vector2(-100, 115));
            Click("GÓC NHÌN", -190, () => camera.ToggleView());
            Click("VỀ GIỮA DÒNG", -470, () => input.RequestReset());
            Click("CHỌN THUYỀN", -750, () => selection.ReturnToSelection());
            mode = Click("", -1030, () => { input.SetTilt(!input.TiltEnabled); UpdateMode(); });
            Click("CÂN BẰNG MÁY", -1310, () => input.Calibrate());
            UpdateMode();
        }
        void UpdateMode() => mode.text = input.TiltEnabled ? "LÁI: NGHIÊNG" : "LÁI: NÚT BẤM";
        RectTransform Box(string label, Vector2 anchor, Vector2 position, Vector2 size, out Text text)
        {
            var rt = new GameObject(label, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rt.SetParent(safe, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size; rt.anchoredPosition = position;
            rt.GetComponent<Image>().color = new Color(0.035f, 0.12f, 0.16f, 0.8f);
            var tr = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
            tr.SetParent(rt, false); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.sizeDelta = Vector2.zero;
            text = tr.GetComponent<Text>(); text.font = font; text.fontSize = 28; text.text = label;
            text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false;
            return rt;
        }
        BoatHoldButton Hold(string label, Vector2 anchor, Vector2 pos)
        {
            var rt = Box(label, anchor, pos, new Vector2(165, 165), out _);
            return rt.gameObject.AddComponent<BoatHoldButton>();
        }
        Text Click(string label, float x, UnityEngine.Events.UnityAction click)
        {
            var rt = Box(label, new Vector2(1, 1), new Vector2(x, -105), new Vector2(260, 90), out var text);
            rt.gameObject.AddComponent<Button>().onClick.AddListener(click);
            return text;
        }
        void Update()
        {
            if (input == null || safe == null) return;
            var area = Screen.safeArea;
            if(area==previousArea && Screen.width==previousWidth && Screen.height==previousHeight) { UpdateInput(); return; }
            previousArea=area; previousWidth=Screen.width; previousHeight=Screen.height;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            UpdateInput();
        }
        void UpdateInput()
        {
            input.TouchSteeringActive = left.Held || right.Held;
            input.TouchSteer = (right.Held ? 1 : 0) - (left.Held ? 1 : 0);
            input.TouchThrottle = (forward.Held ? 1 : 0) - (reverse.Held ? 1 : 0);
        }
        void Release()
        {
            if (left == null) return;
            foreach (var b in new[] { left, right, forward, reverse }) b.Clear();
            if (input != null) input.ReleaseTouch();
        }
        void OnApplicationFocus(bool focus) { if (!focus) Release(); }
        void OnApplicationPause(bool pause) { if (pause) Release(); }
        void OnDisable() { Release(); if (canvas != null) canvas.SetActive(false); }
        void OnDestroy() { if (canvas != null) Destroy(canvas); }
    }
}
