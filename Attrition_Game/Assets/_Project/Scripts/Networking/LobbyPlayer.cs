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

        public override void Spawned()
        {
            if (!HasInputAuthority) return; // chỉ chủ sở hữu local gửi identity của mình

            // Tên hiển thị lobby = TÊN NHÂN VẬT của save slot người chơi đặt (không phải username account).
            int level = 1;
            string name = null;
            var slot = Attrition.Persistence.SaveManager.LoadSlot(Attrition.Persistence.GameLaunch.SelectedSlot);
            if (slot != null)
            {
                level = Mathf.Max(1, slot.level);
                if (!string.IsNullOrEmpty(slot.characterName)) name = slot.characterName;
            }
            if (string.IsNullOrEmpty(name)) name = Attrition.Persistence.GameLaunch.CharacterName;
            if (string.IsNullOrEmpty(name)) name = "Wanderer";
            if (name.Length > 16) name = name.Substring(0, 16);

            if (HasStateAuthority)
            {
                // Host: ghi thẳng + tự sẵn sàng (host luôn ready). RoomName = tên phòng host đặt.
                DisplayName = name;
                Level = level;
                IsHostPlayer = true;
                IsReady = true;
                string room = Attrition.Persistence.GameLaunch.RoomName;
                if (!string.IsNullOrEmpty(room))
                {
                    if (room.Length > 32) room = room.Substring(0, 32);
                    RoomName = room;
                }
            }
            else
            {
                RpcSetIdentity(name, level);
            }
        }

        /// <summary>Client gửi tên + level lên host để hiện ở thẻ lobby.</summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcSetIdentity(NetworkString<_16> name, int level)
        {
            DisplayName = name;
            Level = level > 0 ? level : 1;
            IsHostPlayer = false;
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
