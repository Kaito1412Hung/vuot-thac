using UnityEditor;
using UnityEngine;
using VuotThac.Track;

namespace VuotThac.EditorTools.Track
{
    [CustomEditor(typeof(RiverTrack))]
    public class RiverTrackEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var track = (RiverTrack)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"Sông dài {track.Length:0} m. Đường đua dài {track.RaceLength:0} m, " +
                $"từ mét {track.StartDistance:0} đến mét {track.FinishDistance:0}.\n" +
                "Sửa spline hoặc thông số xong thì bấm nút bên dưới để dựng lại.",
                MessageType.Info);

            if (GUILayout.Button("Dựng lại địa hình, nước, đá và cây", GUILayout.Height(32)))
                RiverTrackBuilder.Build(track);
        }

        [MenuItem("Vượt Thác/Dựng lại sông trong scene")]
        static void RebuildAll()
        {
            var tracks = Object.FindObjectsByType<RiverTrack>();
            if (tracks.Length == 0)
            {
                Debug.LogWarning("[Vượt Thác] Scene đang mở không có RiverTrack nào.");
                return;
            }

            foreach (var t in tracks)
                RiverTrackBuilder.Build(t);
        }
    }
}
