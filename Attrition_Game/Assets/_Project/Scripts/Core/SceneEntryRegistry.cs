using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sổ đăng ký ĐIỂM VÀO của scene (id → vị trí world), sống xuyên scene.
///
/// Tồn tại ở Attrition.Core để `NetworkSpawner` (assembly Networking) đọc được mà KHÔNG cần ref
/// Attrition.Gameplay — chiều ref là Gameplay → Networking, ref ngược sẽ tạo vòng lặp asmdef.
/// Component `SceneEntryPoint` (Gameplay) tự đăng ký/huỷ đăng ký vào đây.
/// </summary>
public static class SceneEntryRegistry
{
    // Lưu cả CHỦ SỞ HỮU (component đã đăng ký) để việc huỷ đăng ký không xoá oan entry của scene khác.
    private static readonly Dictionary<string, Vector3> _points = new Dictionary<string, Vector3>();
    private static readonly Dictionary<string, object> _owners = new Dictionary<string, object>();

    /// <summary>ID điểm vào đang CHỜ áp dụng cho scene kế tiếp (cửa nối ghi trước khi load scene).</summary>
    public static string PendingEntryId;

    /// <summary>
    /// True khi đang FAST-TRAVEL cross-map: đích đến là 1 CHECKPOINT cụ thể, không phải cửa nối.
    ///
    /// VÌ SAO CẦN: player NetworkObject sống sót qua LoadScene, nên sau khi scene mới load, CẢ HAI bên
    /// cùng muốn đặt lại vị trí và chúng ĐUA nhau:
    ///   • `PendingTravelSpawner` (Gameplay) đặt player tại checkpoint đích — và vì player đã tồn tại
    ///     sẵn nên vòng chờ "players.Length > 0" thoả NGAY, nó chạy TRƯỚC.
    ///   • `NetworkSpawner.ServerSpawnPlayer` (Networking) sau đó teleport player về `Player_SpawnPoint`
    ///     (đầu map) → GHI ĐÈ vị trí checkpoint vừa đặt.
    /// Kết quả: chọn "Rest 3" nhưng lại xuất hiện ở đầu map, tức cạnh "Rest 1". Đúng lỗi user báo
    /// "teleport từ map 2 về map 1 nhận diện sai checkpoint". Travel TRONG CÙNG map không bị vì không
    /// có LoadScene → `ServerSpawnPlayer` không chạy.
    ///
    /// Cờ này để `ServerSpawnPlayer` biết KHÔNG được dời player đang sống sót; `PendingTravelSpawner`
    /// mới là nơi quyết định vị trí. Đặt ở Core vì Networking KHÔNG ref được Gameplay (WorldMapState).
    /// </summary>
    public static bool PendingTravelActive;

    public static void Register(string id, Vector3 pos, object owner = null)
    {
        if (string.IsNullOrEmpty(id)) return;
        // z PHẢI về 0. Điểm vào thường là con của một object đã bị đẩy z (vd BossExitGate ở Map 2 có
        // z = 3.59) nên position thừa hưởng z đó. Player là 2D: teleport tới z ≠ 0 làm nhân vật nằm
        // SAU lớp tilemap nền → nhìn như "spawn dưới lòng đất", dù x/y hoàn toàn đúng.
        // Chuẩn hoá tại đây chứ không ở chỗ đọc, để mọi nguồn đăng ký đều sạch.
        _points[id] = new Vector3(pos.x, pos.y, 0f);
        _owners[id] = owner;
    }

    /// <summary>
    /// Huỷ đăng ký. CHỈ xoá nếu `owner` đúng là người đã đăng ký — khi đổi scene, object của scene MỚI
    /// có thể OnEnable TRƯỚC khi object scene CŨ OnDisable; nếu xoá theo id không kiểm chủ sở hữu thì
    /// entry vừa đăng ký của scene mới bị xoá oan (player sẽ rơi về spawn point mặc định).
    /// </summary>
    public static void Unregister(string id, object owner = null)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (owner != null && _owners.TryGetValue(id, out var cur) && !ReferenceEquals(cur, owner))
            return;   // entry đã thuộc về object khác (scene mới) → giữ nguyên
        _points.Remove(id);
        _owners.Remove(id);
    }

    /// <summary>Vị trí của điểm vào đang chờ trong scene hiện tại. False nếu không có lệnh chờ / không khớp id.</summary>
    public static bool TryGetPendingPosition(out Vector3 pos)
    {
        pos = Vector3.zero;
        if (string.IsNullOrEmpty(PendingEntryId)) return false;
        return _points.TryGetValue(PendingEntryId, out pos);
    }

    /// <summary>
    /// Xoá lệnh chờ (sau khi đã đặt xong mọi player) để không dính sang lần chuyển scene sau.
    /// Xoá luôn <see cref="PendingTravelActive"/> làm LƯỚI AN TOÀN: nếu scene đích thiếu
    /// `PendingTravelSpawner` (nó là nơi bình thường xoá cờ) thì cờ sẽ kẹt true vĩnh viễn và MỌI lần
    /// đổi map sau đó player không được dời về cửa nối nữa. Gọi sau khi đã đặt xong mọi player nên
    /// xoá ở đây không ảnh hưởng lần travel hiện tại.
    /// </summary>
    public static void ClearPending()
    {
        PendingEntryId = null;
        PendingTravelActive = false;
    }
}
