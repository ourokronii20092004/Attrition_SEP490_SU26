using UnityEngine;

/// <summary>
/// Đối tượng có thể được DỊCH CHUYỂN bởi code ở assembly thấp hơn (vd Networking).
/// Tồn tại để NetworkSpawner đặt lại vị trí nhân vật khi sang scene mới mà KHÔNG cần ref
/// Attrition.Gameplay (Gameplay → Networking là một chiều; ref ngược sẽ tạo vòng lặp asmdef).
/// PlayerController hiện thực interface này.
/// </summary>
public interface ITeleportable
{
    /// <summary>Dịch chuyển về vị trí (host-authoritative — client gọi sẽ no-op).</summary>
    void TeleportTo(Vector3 position);
}
