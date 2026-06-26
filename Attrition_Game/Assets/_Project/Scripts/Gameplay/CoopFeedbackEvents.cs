using System;

namespace Attrition.Controllers
{
    /// <summary>
    /// Cầu nối phản hồi coop → UI (Gameplay không ref UI). Host phát qua RPC tới mọi peer khi rest/
    /// fast-travel thành công; GameUIController (assembly UI) lắng nghe để hiện thanh load đồng bộ
    /// trên CẢ HAI máy (không chỉ máy bấm). Tránh cảnh 1 người bị teleport mà màn hình giật, không loading.
    /// </summary>
    public static class CoopFeedbackEvents
    {
        /// <summary>(label) — host báo cả phòng đang rest/teleport: mọi máy hiện thanh load.</summary>
        public static event Action<string> OnTravelLoading;

        public static void RaiseTravelLoading(string label) => OnTravelLoading?.Invoke(label);
    }
}
