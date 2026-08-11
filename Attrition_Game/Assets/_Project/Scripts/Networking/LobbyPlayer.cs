using Fusion;
using UnityEngine;

namespace Attrition.Networking
{
    /// <summary>
    /// Object networked NHẸ đại diện 1 người chơi trong PHÒNG CHỜ coop (không physics/AI/camera).
    /// Chỉ mang dữ liệu để 2 thẻ lobby hiển thị: tên nhân vật, level, có phải host, đã sẵn sàng chưa.
    /// Spawn bởi NetworkLauncher khi peer join lobby; despawn khi vào scene gameplay.
    ///
    /// Tên/level đọc từ GameLaunch (đã set lúc chọn nhân vật ở menu). Client gửi qua RPC vì
    /// các biến [Networked] chỉ host ghi được; host tự ghi thẳng.
    /// Đặt trong assembly Networking (không phải Gameplay) vì Gameplay→Networking đã có,
    /// để Networking đọc được type này mà không tạo vòng lặp assembly.
    /// </summary>
    public class LobbyPlayer : NetworkBehaviour
    {
        [Networked] public NetworkString<_16> DisplayName { get; set; }
        [Networked] public int Level { get; set; }
        [Networked] public NetworkBool IsHostPlayer { get; set; }
        [Networked] public NetworkBool IsReady { get; set; }
        // Tên phòng do host đặt (chỉ host ghi). Client đọc từ LobbyPlayer của host để hiển thị.
        [Networked] public NetworkString<_32> RoomName { get; set; }
        // Avatar URL từ login (đường dẫn tương đối /api/account/media/... hoặc tuyệt đối Google).
        // Client gửi kèm RpcSetIdentity; host tự ghi. Lobby UI load ảnh từ đó.
        [Networked] public NetworkString<_256> AvatarUrl { get; set; }

        public override void Spawned()
        {
            if (!HasInputAuthority) return; // chỉ chủ sở hữu local gửi identity của mình

            // Tên hiển thị lobby = TÊN NHÂN VẬT đã chọn cho coop (GameLaunch.CharacterName, set từ
            // character server lúc chọn slot). KHÔNG đọc save slot LOCAL: ParrelSync 2 clone chung
            // thư mục save → cùng slot → cùng tên. Coop character nằm trên server, mỗi peer có tên riêng.
            string name = Attrition.Persistence.GameLaunch.CharacterName;
            if (string.IsNullOrEmpty(name)) name = "Wanderer";
            if (name.Length > 16) name = name.Substring(0, 16);

            int level = Mathf.Max(1, Attrition.Persistence.GameLaunch.CharacterLevel);
            string avatar = Attrition.Persistence.GameLaunch.AvatarUrl ?? "";
            if (avatar.Length > 256) avatar = avatar.Substring(0, 256);

            if (HasStateAuthority)
            {
                // Host: ghi thẳng + tự sẵn sàng (host luôn ready). RoomName = tên phòng host đặt.
                DisplayName = name;
                Level = level;
                IsHostPlayer = true;
                IsReady = true;
                AvatarUrl = avatar;
                string room = Attrition.Persistence.GameLaunch.RoomName;
                if (!string.IsNullOrEmpty(room))
                {
                    if (room.Length > 32) room = room.Substring(0, 32);
                    RoomName = room;
                }
            }
            else
            {
                RpcSetIdentity(name, level, avatar);
            }
        }

        /// <summary>Client gửi tên + level + avatar lên host để hiện ở thẻ lobby.</summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcSetIdentity(NetworkString<_16> name, int level, NetworkString<_256> avatar)
        {
            DisplayName = name;
            Level = level > 0 ? level : 1;
            IsHostPlayer = false;
            AvatarUrl = avatar;
        }

        /// <summary>Client bật/tắt sẵn sàng. Host bỏ qua (luôn ready).</summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcSetReady(NetworkBool ready)
        {
            if (IsHostPlayer) return;
            IsReady = ready;
        }
    }
}
