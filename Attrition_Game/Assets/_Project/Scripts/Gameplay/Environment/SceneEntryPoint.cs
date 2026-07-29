using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// ĐIỂM VÀO của scene, có ID. Khi đi cửa nối giữa 2 map, `RoomTransitionZone` ghi lại id điểm vào
    /// mong muốn TRƯỚC khi load scene; `NetworkSpawner` ở scene mới đọc lại và đặt player tại đây.
    ///
    /// Vì sao cần: mỗi map chỉ có 1 `Player_SpawnPoint` (đầu map). Nếu đi NGƯỢC từ Map 2 về Map 1 mà
    /// dùng spawn point đó thì player bị ném về đầu Map 1 thay vì đứng ở cửa nối sang Map 2.
    ///
    /// Cách dùng: đặt 1 GameObject có component này ngay CẠNH mỗi cửa nối, đặt `entryId` trùng với
    /// `Entry Point Id` mà zone ở map bên kia trỏ tới. KHÔNG cần NetworkObject (chỉ là mốc vị trí,
    /// mỗi máy tự có bản của mình).
    ///
    /// Vị trí được đăng ký vào `SceneEntryRegistry` (assembly Core) để Networking đọc được mà không
    /// tạo vòng lặp asmdef.
    /// </summary>
    public class SceneEntryPoint : MonoBehaviour
    {
        [Tooltip("ID duy nhất trong scene. VD: 'from_map2' = chỗ player xuất hiện khi từ Map 2 về.")]
        [SerializeField] private string entryId = "";

        public string EntryId => entryId;

        private void OnEnable()
        {
            // Đăng ký NGAY khi scene load (trước khi NetworkSpawner đặt player).
            // Truyền `this` làm chủ sở hữu để OnDisable của scene CŨ không xoá oan entry của scene MỚI.
            SceneEntryRegistry.Register(entryId, transform.position, this);
        }

        private void OnDisable()
        {
            SceneEntryRegistry.Unregister(entryId, this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.6f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
        }
    }
}
