#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    [CustomEditor(typeof(Elevator))]
    public class ElevatorEditor : UnityEditor.Editor
    {
        private SerializedProperty _stopOffsets;

        private void OnEnable()
        {
            _stopOffsets = serializedObject.FindProperty("stopOffsets");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Chỉnh điểm dừng bằng các handle Stop trong Scene View. Không kéo khung Rect của Tilemap: " +
                "khung đó thay đổi scale của cả thang máy.", MessageType.Info);
            DrawDefaultInspector();
        }

        private void OnSceneGUI()
        {
            if (_stopOffsets == null || !_stopOffsets.isArray) return;

            serializedObject.Update();
            var elevator = (Elevator)target;
            Vector3 origin = elevator.transform.position;

            Handles.color = new Color(0.2f, 0.85f, 1f, 1f);
            for (int i = 0; i < _stopOffsets.arraySize; i++)
            {
                var item = _stopOffsets.GetArrayElementAtIndex(i);
                Vector3 world = origin + (Vector3)item.vector2Value;

                Handles.Label(world + Vector3.up * 0.45f, $"Stop {i}");
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(world, Quaternion.identity);
                if (!EditorGUI.EndChangeCheck()) continue;

                Undo.RecordObject(elevator, "Move elevator stop");
                item.vector2Value = (Vector2)(moved - origin);
                serializedObject.ApplyModifiedProperties();
            }

            Handles.DrawAAPolyLine(3f, BuildPath(origin));
        }

        private Vector3[] BuildPath(Vector3 origin)
        {
            var points = new Vector3[_stopOffsets.arraySize];
            for (int i = 0; i < points.Length; i++)
                points[i] = origin + (Vector3)_stopOffsets.GetArrayElementAtIndex(i).vector2Value;
            return points;
        }
    }
}
#endif
