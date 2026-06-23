using UnityEngine;
using UnityEditor;
using Attrition.Gameplay.Environment;

namespace Attrition.Gameplay.Environment.Editor
{
    public class RoomSetupEditor
    {
        [MenuItem("Tools/Create Room Logic Template")]
        public static void CreateRoomTemplate()
        {
            // Tạo cha
            GameObject roomRoot = new GameObject("RoomLogic_Template");
            Undo.RegisterCreatedObjectUndo(roomRoot, "Create Room Template");

            // 1. Tạo Camera Bounds
            GameObject boundsObj = new GameObject("CameraBounds");
            boundsObj.transform.SetParent(roomRoot.transform);
            var boundsCol = boundsObj.AddComponent<BoxCollider2D>();
            boundsCol.isTrigger = true;
            boundsCol.size = new Vector2(20f, 10f); // Kích thước mặc định
            boundsObj.AddComponent<CameraBoundsZone>();

            // 2. Tạo Cửa đi tới phòng tiếp theo (Bên phải)
            GameObject doorNext = new GameObject("Door_To_NextRoom");
            doorNext.transform.SetParent(roomRoot.transform);
            doorNext.transform.localPosition = new Vector3(10f, -3f, 0f);
            var doorNextCol = doorNext.AddComponent<BoxCollider2D>();
            doorNextCol.isTrigger = true;
            doorNextCol.size = new Vector2(1f, 4f);
            doorNext.AddComponent<RoomTransitionTrigger>();

            // 3. Tạo Điểm xuất hiện từ phòng trước (Bên trái)
            GameObject spawnFromPrev = new GameObject("Spawn_From_PrevRoom");
            spawnFromPrev.transform.SetParent(roomRoot.transform);
            spawnFromPrev.transform.localPosition = new Vector3(-8f, -4f, 0f);

            // 4. Tạo Cửa đi về phòng trước (Bên trái)
            GameObject doorPrev = new GameObject("Door_To_PrevRoom");
            doorPrev.transform.SetParent(roomRoot.transform);
            doorPrev.transform.localPosition = new Vector3(-10f, -3f, 0f);
            var doorPrevCol = doorPrev.AddComponent<BoxCollider2D>();
            doorPrevCol.isTrigger = true;
            doorPrevCol.size = new Vector2(1f, 4f);
            doorPrev.AddComponent<RoomTransitionTrigger>();

            // 5. Tạo Điểm xuất hiện từ phòng tiếp theo (Bên phải)
            GameObject spawnFromNext = new GameObject("Spawn_From_NextRoom");
            spawnFromNext.transform.SetParent(roomRoot.transform);
            spawnFromNext.transform.localPosition = new Vector3(8f, -4f, 0f);

            Selection.activeGameObject = roomRoot;
            Debug.Log("[Room Setup] Đã tạo bộ khung logic cho một căn phòng!");
        }
    }
}
